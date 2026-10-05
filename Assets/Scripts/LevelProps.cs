using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HARİTA NESNELERİ (LevelGenerator kurar, harita silinince kendiliğinden gider).
///
///   C  SANDIK : vurunca ya da yanında [W]/[↑]/[F] → altın + şansla charm (çıkışta)
///               ya da iyileşme. Genelde üst yolda / zor yerde durur.
///   V  VAZO   : vurunca kırılır → biraz altın.
///   ^  DİKEN  : değen oyuncu hasar alır ve yukarı fırlar. Aşağı vuruşla
///               (↓ + saldırı) dikenden de ZIPLANIR (pogo).
///
/// Görseller Jungle sayfasından (hücreler Inspector'dan değiştirilebilir);
/// tema yoksa düz renkli kutular çizilir.
/// </summary>
public class LevelProps : MonoBehaviour
{
    public static LevelProps Instance { get; private set; }

    [Header("Sayfa hücreleri (sütun, satır, genişlik, yükseklik) — sol üst 0,0")]
    public RectInt chestClosedCells = new RectInt(18, 17, 2, 2);
    public RectInt chestOpenCells = new RectInt(20, 17, 2, 2);
    public RectInt vaseCells = new RectInt(21, 19, 1, 2);
    public RectInt spikeCells = new RectInt(21, 16, 1, 1);

    [Header("Diken")]
    [Range(0f, 1f)] public float spikeDamagePercent = 0.12f;
    public float spikeBounce = 12f;
    [Tooltip("Dikenin çarpma kutusu yüksekliği (hücre oranı).")]
    [Range(0.1f, 1f)] public float spikeHitHeight = 0.55f;

    [Header("Vazo")]
    [Range(0f, 1f)] public float vaseSpawnChance = 0.7f;

    private class Prop
    {
        public GameObject obj;
        public SpriteRenderer sr;
        public Rect area;
        public bool chest;
        public bool cursed;
        public bool used;
    }

    private readonly List<Prop> props = new List<Prop>();
    private readonly List<Rect> spikes = new List<Rect>();

    private JungleTheme theme;
    private Transform levelRoot;
    private float levelCell = 1f;
    private int levelLayer;
    private int levelOrder = 1;

    [Header("Lanetli sandık")]
    public Color cursedTint = new Color(0.75f, 0.45f, 1f);
    private PlayerController player;
    private Collider2D playerCol;
    private PlayerDamageReceiver receiver;
    private float nextSpikeTime;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static LevelProps Ensure(GameObject host)
    {
        LevelProps p = host.GetComponent<LevelProps>();

        if (p == null)
            p = host.AddComponent<LevelProps>();

        return p;
    }

    /// <summary>Yeni harita: eski listeyi sıfırla (nesneler haritayla silinir).</summary>
    public void ResetLevel(JungleTheme jungle, Transform root)
    {
        ResetLevel(jungle, root, levelCell, levelLayer, levelOrder);
    }

    public void ResetLevel(JungleTheme jungle, Transform root, float cell, int sortingLayer, int order)
    {
        theme = jungle;
        levelRoot = root;
        levelCell = cell;
        levelLayer = sortingLayer;
        levelOrder = order;
        props.Clear();
        spikes.Clear();
    }

    // =========================================================
    // KURULUM (LevelGenerator çağırır)
    // =========================================================

    /// <param name="ground">Hücrenin alt-orta noktası (zemin yüzeyi).</param>
    public void SpawnChest(Vector3 ground, Transform parent, float cell, int sortingLayer, int order)
    {
        Prop p = Create("Sandık", ground, parent, cell, chestClosedCells, new Color(0.65f, 0.42f, 0.18f), sortingLayer, order);
        p.chest = true;
        props.Add(p);
    }

    /// <summary>Haritaya sonradan sandık (RunManager: lanetli sandık).</summary>
    public void SpawnExtraChest(Vector3 ground, bool cursed)
    {
        if (levelRoot == null)
            return;

        Prop p = Create(cursed ? "Lanetli Sandık" : "Sandık", ground, levelRoot, levelCell, chestClosedCells, new Color(0.65f, 0.42f, 0.18f), levelLayer, levelOrder);

        p.chest = true;
        p.cursed = cursed;

        if (cursed)
            p.sr.color = cursedTint;

        props.Add(p);
    }

    public bool TrySpawnVase(Vector3 ground, Transform parent, float cell, int sortingLayer, int order, System.Random rng)
    {
        if (rng != null && rng.NextDouble() > vaseSpawnChance)
            return false;

        Prop p = Create("Vazo", ground, parent, cell, vaseCells, new Color(0.75f, 0.55f, 0.4f), sortingLayer, order);
        props.Add(p);
        return true;
    }

    /// <param name="ground">Diken hücresinin alt-orta noktası.</param>
    public void SpawnSpike(Vector3 ground, Transform parent, float cell, int sortingLayer, int order)
    {
        GameObject obj = new GameObject("Diken");
        obj.transform.SetParent(parent, true);
        obj.transform.position = ground;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = order;

        Sprite s = theme != null ? theme.PropSprite(spikeCells.x, spikeCells.y, spikeCells.width, spikeCells.height) : null;

        if (s != null)
        {
            sr.sprite = s;
            obj.transform.localScale = Vector3.one * cell;
        }
        else
        {
            sr.sprite = WhiteSprite();
            sr.color = new Color(0.8f, 0.8f, 0.85f);
            obj.transform.localScale = new Vector3(cell * 0.9f, cell * 0.45f, 1f);
        }

        spikes.Add(new Rect(ground.x - cell * 0.5f, ground.y, cell, cell * spikeHitHeight));
    }

    private Prop Create(string name, Vector3 ground, Transform parent, float cell, RectInt cells, Color fallback, int sortingLayer, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, true);
        obj.transform.position = ground;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = order;

        Sprite s = theme != null ? theme.PropSprite(cells.x, cells.y, cells.width, cells.height) : null;

        float w = cells.width * cell;
        float h = cells.height * cell;

        if (s != null)
        {
            sr.sprite = s;
            obj.transform.localScale = Vector3.one * cell;
        }
        else
        {
            sr.sprite = WhiteSprite();
            sr.color = fallback;
            obj.transform.localScale = new Vector3(w * 0.8f, h * 0.8f, 1f);
        }

        return new Prop
        {
            obj = obj,
            sr = sr,
            area = new Rect(ground.x - w * 0.5f, ground.y, w, h)
        };
    }

    // =========================================================
    // ETKİLEŞİM
    // =========================================================

    /// <summary>Oyuncu saldırısı bu kutuya vurdu (AttackState, AirAttackState, Slam).</summary>
    public static bool HitArea(Vector2 center, Vector2 size)
    {
        if (Instance == null)
            return false;

        Rect box = new Rect(center - size * 0.5f, size);

        bool any = false;

        for (int i = 0; i < Instance.props.Count; i++)
        {
            Prop p = Instance.props[i];

            if (p.used || p.obj == null || !p.area.Overlaps(box))
                continue;

            Instance.Use(p);
            any = true;
        }

        return any;
    }

    /// <summary>Aşağı vuruş: kutuda diken var mı (pogo yüzeyi).</summary>
    public static bool SpikeIn(Vector2 center, Vector2 size)
    {
        if (Instance == null)
            return false;

        Rect box = new Rect(center - size * 0.5f, size);

        for (int i = 0; i < Instance.spikes.Count; i++)
        {
            if (Instance.spikes[i].Overlaps(box))
                return true;
        }

        return false;
    }

    private void Use(Prop p)
    {
        p.used = true;

        Vector3 at = p.obj.transform.position + Vector3.up * p.area.height * 0.6f;

        RunManager run = RunManager.Instance;

        if (p.chest)
        {
            Sprite open = theme != null ? theme.PropSprite(chestOpenCells.x, chestOpenCells.y, chestOpenCells.width, chestOpenCells.height) : null;

            if (open != null)
                p.sr.sprite = open;
            else
                p.sr.color = p.sr.color * 0.6f;

            if (run != null)
                run.OnChestOpened(at, p.cursed);

            return;
        }

        // Vazo: kırılır (kısa söner).
        if (run != null)
            run.OnVaseBroken(at);

        Destroy(p.obj, 0.05f);
    }

    private void Update()
    {
        if (props.Count == 0 && spikes.Count == 0)
            return;

        // Harita silindiyse liste boşalsın.
        if (levelRoot == null)
        {
            props.Clear();
            spikes.Clear();
            return;
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();

            if (player == null)
                return;

            playerCol = player.col;
            receiver = player.GetComponent<PlayerDamageReceiver>();
        }

        if (playerCol == null || Time.timeScale < 0.01f)
            return;

        Bounds b = playerCol.bounds;
        Rect body = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);

        // SANDIK: yanındayken etkileşim tuşu.
        if (player.canControl && InteractPressed())
        {
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = props[i];

                if (!p.chest || p.used || p.obj == null)
                    continue;

                Rect near = new Rect(p.area.x - 0.8f, p.area.y - 0.5f, p.area.width + 1.6f, p.area.height + 1f);

                if (near.Overlaps(body))
                {
                    Use(p);
                    break;
                }
            }
        }

        // DİKEN
        if (Time.time < nextSpikeTime)
            return;

        for (int i = 0; i < spikes.Count; i++)
        {
            if (!spikes[i].Overlaps(body))
                continue;

            nextSpikeTime = Time.time + 0.4f;

            HitPlayerWithSpike(spikes[i]);
            break;
        }
    }

    private void HitPlayerWithSpike(Rect spike)
    {
        Health health = player.GetComponent<Health>();

        if (health == null || health.IsDead || player.isInvincible)
            return;

        int damage = Mathf.Max(1, Mathf.CeilToInt(health.MaxHealth * spikeDamagePercent));

        float side = Mathf.Sign(player.transform.position.x - spike.center.x);

        if (receiver != null)
        {
            receiver.TakeDamage(
                damage,
                new Vector2(side, 1f).normalized,
                4f,
                spikeBounce,
                0.15f,
                -1f,
                null,
                PlayerHitKind.Other
            );
        }
        else
        {
            health.TakeDamage(damage);
        }

        CombatCallout.Popup(player.transform.position + Vector3.up * 2f, "DİKEN!", new Color(1f, 0.45f, 0.35f), 0.9f);
    }

    private static bool InteractPressed()
    {
        RunManager run = RunManager.Instance;

        if (run != null && run.IsPaused)
            return false;

        return
            Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.F);
    }

    private static Sprite white;

    private static Sprite WhiteSprite()
    {
        if (white != null)
            return white;

        Texture2D tex = Texture2D.whiteTexture;
        white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), tex.width);

        return white;
    }
}
