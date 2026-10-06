using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TEK YERDEN EFEKT YÖNETİMİ (elle çizilmiş kare kare efektler).
///
/// Sahnede bir objeye ekle (CombatVFXManager ile aynı objeye olabilir).
/// Inspector'da her efekt için bir satır ekle: id + sprite kareleri.
/// Koddan: SpriteFxLibrary.Play("parry", pos, dir);
/// Satır yoksa / kare boşsa false döner; çağıran eski (kodla çizilen) efekte düşer.
///
/// Hazır id'ler (bağlı olanlar): hit_health, hit_balance, parry, block,
/// balance_break, dust. Yeni id'yi istediğin yerden çağırabilirsin.
/// </summary>
public class SpriteFxLibrary : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string id;

        [Tooltip("Kareler sırayla oynar.")]
        public Sprite[] frames;

        public float fps = 24f;
        public float scale = 1f;

        [Tooltip("Açık: efekt verilen yöne döner (kılıç izi, kıvılcım). Kapalı: sadece sağa/sola bakar.")]
        public bool rotateToDirection = false;

        [Tooltip("Açık: yön sola ise yatay çevrilir.")]
        public bool flipByDirection = true;

        [Tooltip("Açık: ışık gibi toplanır (Additive). Koyu efektler için kapalı bırak.")]
        public bool additive = false;

        [Tooltip("Efektin sprite'ının ofseti (yerel).")]
        public Vector2 offset = Vector2.zero;

        public int sortingOrderOffset = 0;
        public Color tint = Color.white;
    }

    public static SpriteFxLibrary Instance { get; private set; }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Header("Render")]
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int baseSortingOrder = 100;

    private readonly Dictionary<string, Entry> map = new Dictionary<string, Entry>();

    private static Material additiveMat;
    private static Material normalMat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Rebuild();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            Rebuild();
    }

    private void Rebuild()
    {
        map.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if (e == null || string.IsNullOrEmpty(e.id))
                continue;

            map[e.id] = e;
        }
    }

    public static bool Has(string id)
    {
        if (Instance == null || string.IsNullOrEmpty(id))
            return false;

        return Instance.map.TryGetValue(id, out Entry e) && e.frames != null && e.frames.Length > 0;
    }

    /// <summary>Efekti oynatır. Tanımlı değilse false (çağıran yedek efekte geçsin).</summary>
    public static bool Play(string id, Vector3 position, Vector2 direction, Transform follow = null)
    {
        if (!Has(id))
            return false;

        Instance.Spawn(Instance.map[id], position, direction, follow);

        return true;
    }

    private void Spawn(Entry e, Vector3 pos, Vector2 dir, Transform follow)
    {
        GameObject go = new GameObject("Fx_" + e.id);

        float flipSign = 1f;

        if (dir.sqrMagnitude > 0.001f)
        {
            if (e.rotateToDirection)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            else if (e.flipByDirection && dir.x < 0f)
            {
                flipSign = -1f;
            }
        }

        Vector3 off = new Vector3(e.offset.x * flipSign, e.offset.y, 0f);

        go.transform.position = pos + off;
        go.transform.localScale = new Vector3(e.scale * flipSign, e.scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = e.frames[0];
        sr.color = e.tint;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = baseSortingOrder + e.sortingOrderOffset;
        sr.sharedMaterial = GetMaterial(e.additive);

        SpriteFxPlayer p = go.AddComponent<SpriteFxPlayer>();
        p.Init(sr, e.frames, e.fps, follow, off);
    }

    private static Material GetMaterial(bool additive)
    {
        if (additive)
        {
            if (additiveMat == null)
            {
                Shader s = Shader.Find("Sprites/Default");

                if (s != null)
                {
                    additiveMat = new Material(s);
                    additiveMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    additiveMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                }
            }

            return additiveMat;
        }

        if (normalMat == null)
        {
            Shader s = Shader.Find("Sprites/Default");

            if (s != null)
                normalMat = new Material(s);
        }

        return normalMat;
    }
}

/// <summary>Kareleri oynatır, bitince objeyi siler. Hitstop'tan etkilenmez (gerçek zaman).</summary>
public class SpriteFxPlayer : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite[] frames;
    private float fps;
    private float t;
    private Transform follow;
    private Vector3 followOffset;

    public void Init(SpriteRenderer renderer, Sprite[] f, float framesPerSecond, Transform followTarget, Vector3 offset)
    {
        sr = renderer;
        frames = f;
        fps = Mathf.Max(1f, framesPerSecond);
        follow = followTarget;
        followOffset = offset;
    }

    private void Update()
    {
        if (sr == null || frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        t += Time.unscaledDeltaTime;

        int i = Mathf.FloorToInt(t * fps);

        if (i >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        sr.sprite = frames[i];

        if (follow != null)
            transform.position = follow.position + followOffset;
    }
}
