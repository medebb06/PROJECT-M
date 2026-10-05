using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// KRİTİK VURUŞ GERİ BİLDİRİMİ. Her kritik vuruşta (kaynak fark etmez:
/// Odak, Keskin Gözler, riposte...) normal vuruştan AYRILAN bir his:
///
///   1) Altın yıldız patlaması (vuruş noktasında büyüyüp söner)
///   2) Etrafa saçılan kıvılcımlar (vuruş yönüne doğru)
///   3) Yükselip kaybolan "KRİTİK!" yazısı
///   4) Düşmanda altın renk flaşı
///   5) Biraz daha uzun hit-stop + kamera sarsıntısı (+ isteğe bağlı ses)
///
/// KURULUM YOK: sahnede yoksa oyun başında kendiliğinden oluşur. Ayar
/// yapmak istersen sahnede boş bir objeye ekle (o kullanılır).
/// Görseller koddan üretilir (ek sprite gerekmez), pixel art'a uygun
/// (Point filtre). Animasyonlar gerçek zamanla akar: hit-stop sırasında
/// da net görünür.
/// </summary>
public class CritFeedback : MonoBehaviour
{
    [Header("Genel")]
    public bool enableCritFeedback = true;

    [Header("Yıldız Patlaması")]
    public Color burstColor = new Color(1f, 0.85f, 0.25f);

    [Tooltip("Patlamanın en büyük hali (dünya birimi çarpanı).")]
    [Min(0.1f)]
    public float burstScale = 1.3f;

    [Min(0.05f)]
    public float burstDuration = 0.2f;

    [Header("Kıvılcımlar")]
    [Range(0, 20)]
    public int sparkCount = 7;

    public float sparkSpeed = 7f;
    public float sparkLife = 0.3f;
    public float sparkGravity = 18f;
    public Color sparkColor = new Color(1f, 0.95f, 0.6f);

    [Header("Yazı")]
    public bool showText = true;
    public string critText = "KRİTİK!";
    public Color textColor = new Color(1f, 0.8f, 0.15f);
    public Color textShadowColor = new Color(0.25f, 0.08f, 0f);

    [Tooltip("Yazının yükseldiği mesafe.")]
    public float textRise = 0.8f;

    public float textLife = 0.65f;

    [Tooltip("Yazı boyutu (dünya birimi). Büyük/küçük gelirse değiştir.")]
    public float textSize = 0.045f;

    [Header("His")]
    [Tooltip("Kritikte ek hit-stop süresi (gerçek sn). 0 = kapalı.")]
    [Min(0f)]
    public float hitStopDuration = 0.05f;

    [Range(0f, 1f)]
    public float hitStopTimeScale = 0.02f;

    [Tooltip("Kamera sarsıntısı (oyuncunun Impulse Source'u). 0 = kapalı.")]
    [Min(0f)]
    public float shakeForce = 0.45f;

    public Color enemyTint = new Color(1f, 0.8f, 0.2f);
    public float enemyTintDuration = 0.1f;

    [Tooltip("Kritik sesi (boşsa sessiz).")]
    public AudioClip critClip;

    [Range(0f, 1f)]
    public float critVolume = 1f;

    // =========================================================
    // İÇ
    // =========================================================

    private enum FxKind { Burst, Spark, Text }

    private class Fx
    {
        public FxKind kind;
        public GameObject obj;
        public SpriteRenderer sprite;
        public TextMesh text;
        public TextMesh shadow;
        public Vector3 start;
        public Vector3 velocity;
        public float age;
        public float life;
        public float spin;
    }

    private static CritFeedback instance;

    private static Sprite burstSprite;
    private static Sprite sparkSprite;
    private static Material unlitMaterial;
    private static Font font;

    private readonly List<Fx> active = new List<Fx>();

    private AudioSource audioSource;
    private CinemachineImpulseSource impulse;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        instance = null;
    }

    // Sahnede yoksa kendiliğinden oluştur.
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad
    )]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<CritFeedback>() != null)
            return;

        new GameObject("CritFeedback").AddComponent<CritFeedback>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyHit -= OnEnemyHit;

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].obj != null)
                Destroy(active[i].obj);
        }

        active.Clear();
    }

    // =========================================================
    // TETİK
    // =========================================================

    private void OnEnemyHit(
        EnemyController enemy,
        DamageInfo info,
        HitResult result
    )
    {
        if (!enableCritFeedback || !result.hit || !result.critical)
            return;

        if (enemy == null)
            return;

        Vector3 position =
            info.hitPosition.sqrMagnitude > 0.0001f
                ? info.hitPosition
                : enemy.transform.position + Vector3.up * 0.8f;

        position.z = enemy.transform.position.z;

        Vector2 direction =
            info.direction.sqrMagnitude > 0.0001f
                ? info.direction.normalized
                : Vector2.right;

        GetSorting(enemy, out int layer, out int order);

        SpawnBurst(position, layer, order + 600);
        SpawnSparks(position, direction, layer, order + 601);

        if (showText)
        {
            SpawnText(
                position + Vector3.up * 0.55f,
                result.amount,
                layer,
                order + 602
            );
        }

        // Altın flaş (PlayerDamage'ın beyaz flaşının yerine geçer).
        if (!result.killed)
            enemy.PlayTintFlash(enemyTint, enemyTintDuration);

        if (hitStopDuration > 0f)
            HitStop.Request(hitStopDuration, hitStopTimeScale);

        Shake();

        if (critClip != null)
            audioSource.PlayOneShot(critClip, critVolume);
    }

    private void Shake()
    {
        if (shakeForce <= 0f)
            return;

        if (impulse == null)
        {
            PlayerController player =
                FindFirstObjectByType<PlayerController>();

            if (player != null)
                impulse = player.impulseSource;
        }

        if (impulse != null)
            impulse.GenerateImpulse(shakeForce);
    }

    // =========================================================
    // ÜRETİM
    // =========================================================

    private void SpawnBurst(Vector3 position, int layer, int order)
    {
        Fx fx = new Fx
        {
            kind = FxKind.Burst,
            life = burstDuration,
            start = position,
            spin = Random.Range(-1f, 1f) > 0f ? 220f : -220f
        };

        fx.obj = new GameObject("CritBurst");
        fx.obj.transform.position = position;
        fx.obj.transform.rotation =
            Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));

        fx.sprite = CreateSprite(fx.obj, GetBurstSprite(), layer, order);
        fx.sprite.color = burstColor;

        active.Add(fx);
    }

    private void SpawnSparks(
        Vector3 position,
        Vector2 direction,
        int layer,
        int order
    )
    {
        for (int i = 0; i < sparkCount; i++)
        {
            // Vuruş yönünde ±70° yelpaze, biraz yukarı eğimli.
            float angle =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg +
                Random.Range(-70f, 70f) + 15f * Mathf.Sign(direction.x);

            Vector2 dir =
                new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

            Fx fx = new Fx
            {
                kind = FxKind.Spark,
                life = sparkLife * Random.Range(0.7f, 1.2f),
                start = position,
                velocity = dir * sparkSpeed * Random.Range(0.6f, 1.2f)
            };

            fx.obj = new GameObject("CritSpark");
            fx.obj.transform.position = position;

            fx.sprite = CreateSprite(fx.obj, GetSparkSprite(), layer, order);
            fx.sprite.color = sparkColor;

            active.Add(fx);
        }
    }

    private void SpawnText(Vector3 position, int amount, int layer, int order)
    {
        Fx fx = new Fx
        {
            kind = FxKind.Text,
            life = textLife,
            start = position
        };

        fx.obj = new GameObject("CritText");
        fx.obj.transform.position = position;

        string label =
            amount > 0
                ? critText + "  " + amount
                : critText;

        // Gölge (okunurluk) + asıl yazı.
        fx.shadow = CreateText(fx.obj.transform, label, textShadowColor, layer, order);
        fx.shadow.transform.localPosition = new Vector3(0.04f, -0.04f, 0f);

        fx.text = CreateText(fx.obj.transform, label, textColor, layer, order + 1);

        active.Add(fx);
    }

    // =========================================================
    // GÜNCELLEME (gerçek zaman: hit-stop'ta da akar)
    // =========================================================

    private void LateUpdate()
    {
        if (active.Count == 0)
            return;

        float dt = Time.unscaledDeltaTime;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Fx fx = active[i];

            if (fx.obj == null)
            {
                active.RemoveAt(i);
                continue;
            }

            fx.age += dt;

            float t = Mathf.Clamp01(fx.age / Mathf.Max(0.01f, fx.life));

            switch (fx.kind)
            {
                case FxKind.Burst:
                    UpdateBurst(fx, t, dt);
                    break;

                case FxKind.Spark:
                    UpdateSpark(fx, t, dt);
                    break;

                case FxKind.Text:
                    UpdateText(fx, t);
                    break;
            }

            if (fx.age >= fx.life)
            {
                Destroy(fx.obj);
                active.RemoveAt(i);
            }
        }
    }

    private void UpdateBurst(Fx fx, float t, float dt)
    {
        // Hızlı büyür (ilk %30), sonra hafif küçülerek söner.
        float grow =
            t < 0.3f
                ? Mathf.SmoothStep(0.3f, 1f, t / 0.3f)
                : Mathf.Lerp(1f, 0.85f, (t - 0.3f) / 0.7f);

        fx.obj.transform.localScale = Vector3.one * burstScale * grow;
        fx.obj.transform.Rotate(0f, 0f, fx.spin * dt);

        Color c = burstColor;

        // İlk karelerde beyaza yakın parlasın.
        c = Color.Lerp(Color.white, c, Mathf.Clamp01(t * 4f));
        c.a = 1f - Mathf.SmoothStep(0.4f, 1f, t);

        fx.sprite.color = c;
    }

    private void UpdateSpark(Fx fx, float t, float dt)
    {
        fx.velocity += Vector3.down * sparkGravity * dt;
        fx.velocity *= 1f - 3f * dt;

        fx.obj.transform.position += fx.velocity * dt;

        Color c = sparkColor;
        c.a = 1f - t * t;

        fx.sprite.color = c;
        fx.obj.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, t);
    }

    private void UpdateText(Fx fx, float t)
    {
        // Pop-in, yükselme, sonda solma.
        float pop =
            t < 0.15f
                ? Mathf.Lerp(1.6f, 1f, t / 0.15f)
                : 1f;

        float rise = 1f - (1f - t) * (1f - t);

        fx.obj.transform.position =
            fx.start + Vector3.up * textRise * rise;

        fx.obj.transform.localScale = Vector3.one * pop;

        float alpha = 1f - Mathf.SmoothStep(0.6f, 1f, t);

        Color c = textColor;
        c.a = alpha;
        fx.text.color = c;

        Color s = textShadowColor;
        s.a = alpha;
        fx.shadow.color = s;
    }

    // =========================================================
    // YARDIMCILAR
    // =========================================================

    private static void GetSorting(
        EnemyController enemy,
        out int layer,
        out int order
    )
    {
        SpriteRenderer sr =
            enemy.GetComponentInChildren<SpriteRenderer>();

        layer = sr != null ? sr.sortingLayerID : 0;
        order = sr != null ? sr.sortingOrder : 0;
    }

    private static SpriteRenderer CreateSprite(
        GameObject obj,
        Sprite sprite,
        int layer,
        int order
    )
    {
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();

        sr.sprite = sprite;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;

        Material m = GetUnlitMaterial();

        if (m != null)
            sr.sharedMaterial = m;

        return sr;
    }

    private TextMesh CreateText(
        Transform parent,
        string label,
        Color color,
        int layer,
        int order
    )
    {
        GameObject obj = new GameObject("Text");

        obj.transform.SetParent(parent, false);

        TextMesh tm = obj.AddComponent<TextMesh>();

        Font f = GetFont();

        if (f != null)
            tm.font = f;

        tm.text = label;
        tm.fontSize = 64;
        tm.characterSize = textSize * GameSettings.WorldTextScale;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;

        MeshRenderer mr = obj.GetComponent<MeshRenderer>();

        if (f != null)
            mr.sharedMaterial = f.material;

        mr.sortingLayerID = layer;
        mr.sortingOrder = order;

        return tm;
    }

    private static Font GetFont()
    {
        if (font != null)
            return font;

        // Unity 2022.2+ / Unity 6 yerleşik fontu.
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return font;
    }

    private static Material GetUnlitMaterial()
    {
        if (unlitMaterial != null)
            return unlitMaterial;

        // 2D ışıktan etkilenmesin: her zaman parlak.
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        unlitMaterial = new Material(shader);

        return unlitMaterial;
    }

    // 8 köşeli pixel yıldız (17x17), beyaz: renk SpriteRenderer'dan.
    private static Sprite GetBurstSprite()
    {
        if (burstSprite != null)
            return burstSprite;

        const int size = 17;
        const int c = size / 2;

        Texture2D tex =
            new Texture2D(size, size, TextureFormat.RGBA32, false);

        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                int dx = Mathf.Abs(x - c);
                int dy = Mathf.Abs(y - c);

                bool core = dx * dx + dy * dy <= 6;
                bool cross = (dx <= 1 && dy <= c) || (dy <= 1 && dx <= c);
                bool diagonal = dx == dy && dx <= 5;

                tex.SetPixel(
                    x,
                    y,
                    core || cross || diagonal
                        ? Color.white
                        : new Color(0f, 0f, 0f, 0f)
                );
            }
        }

        tex.Apply();

        burstSprite =
            Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                16f
            );

        return burstSprite;
    }

    // 2x2 kare kıvılcım.
    private static Sprite GetSparkSprite()
    {
        if (sparkSprite != null)
            return sparkSprite;

        Texture2D tex =
            new Texture2D(2, 2, TextureFormat.RGBA32, false);

        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
                tex.SetPixel(x, y, Color.white);
        }

        tex.Apply();

        sparkSprite =
            Sprite.Create(
                tex,
                new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f),
                16f
            );

        return sparkSprite;
    }
}