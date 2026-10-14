using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HIT SPARK: düşmanın duruşuna (denge) hasar veren vuruşta metal kıvılcımları.
/// Vuruş yönüne ve yukarı saçılır, yerçekimiyle düşer, zemine çarpınca bir kez
/// seker, sonra yerde kayıp sönen kor olur. Görseller koddan üretilir; renk
/// toplamalı HDR (Resources/AtkGlow/SpriteGlowAdd) → bloom ile parlar.
///
/// KURULUM YOK: CombatVFXManager.PlayBalanceHit çağırır. Ayar için sahnede bir
/// objeye ekleyebilirsin (o kullanılır); yoksa kendiliğinden oluşur.
/// </summary>
public class HitSparks : MonoBehaviour
{
    [Header("Miktar")]
    [Range(0, 40)] public int sparkCount2 = 7;
    [Range(0, 10)] public int heavyCount2 = 2;
    [Tooltip("Havada süzülüp yükselen kıvılcımlar (yere dökülmez, havada söner).")]
    [Range(0, 20)] public int floatCount = 4;
    public Vector2 floatSpeed = new Vector2(1.8f, 4.2f);
    public Vector2 floatAngle = new Vector2(35f, 130f);
    [Tooltip("Süzülenlerin yerçekimi çarpanı (0 = hiç düşmez).")]
    [Range(0f, 1f)] public float floatGravity = 0.1f;
    public Vector2 floatLife = new Vector2(0.55f, 0.9f);

    [Header("Hareket")]
    [Tooltip("Hız aralığı (birim/sn).")]
    public Vector2 speed = new Vector2(5.5f, 12f);
    [Tooltip("Ağır (büyük, yavaş) kıvılcım hız aralığı.")]
    public Vector2 heavySpeed = new Vector2(3f, 6.5f);
    [Tooltip("Saçılma açısı aralığı (derece; 0 = vuruş yönü, 90 = yukarı).")]
    public Vector2 angle2 = new Vector2(-10f, 105f);
    public float gravity = 32f;
    [Tooltip("Hava sürtünmesi (1/sn).")]
    public float drag = 2.2f;
    [Tooltip("Zemine çarpınca dikey sekme oranı.")]
    [Range(0f, 1f)] public float bounce = 0.35f;
    [Tooltip("Sekmede yatay hız korunma oranı.")]
    [Range(0f, 1f)] public float bounceFriction = 0.55f;

    [Header("Ömür")]
    public Vector2 life = new Vector2(0.3f, 0.55f);
    [Tooltip("Yere düşünce kor olarak kalma süresi.")]
    public float emberTime = 0.28f;

    [Header("Görünüm")]
    [Tooltip("Kıvılcım kalınlığı (birim).")]
    public float sparkWidth = 0.14f;
    [Tooltip("Uzunluk = taban + hız × çarpan.")]
    public float sparkBaseLength = 0.3f;
    public float sparkLengthPerSpeed = 0.065f;
    [Tooltip("Parlaklık (HDR, Impact Mat ≈ 8.5 beyaz). Renk aşağıdaki ateş gradyanından gelir.")]
    [ColorUsage(true, true)] public Color fireGlow2 = new Color(4.5f, 4.5f, 4.5f, 1f);
    [Tooltip("Ateş gradyanı: doğduğunda sarı → turuncu → sönerken kırmızı.")]
    public Color fireHot2 = new Color(1f, 0.78f, 0.22f, 1f);
    public Color fireMid2 = new Color(1f, 0.36f, 0.04f, 1f);
    public Color fireCool2 = new Color(0.9f, 0.07f, 0.02f, 1f);
    [Tooltip("Aynı anda gelen vuruşlarda en az bu aralıkla (sn) kıvılcım çıkar.")]
    public float minInterval = 0.06f;
    private float lastSpawn = -10f;
    [Tooltip("Vuruş noktasında kısa parlama.")]
    public bool flash = true;
    public float sparkFlashSize = 1.1f;
    public float flashTime = 0.07f;

    [Header("Zemin")]
    public LayerMask groundMask = 0;

    // ---------------------------------------------------------

    private class Spark
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 pos, vel;
        public float age, life, w, grav = 1f, dragMul = 1f;
        public int bounces;
        public bool ember, isFlash;
        public float emberAge;
    }

    private static HitSparks instance;
    private static Sprite streak, dot;
    private static Material mat;

    private readonly List<Spark> live = new List<Spark>();
    private readonly Stack<Spark> pool = new Stack<Spark>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        if (groundMask.value == 0) groundMask = LayerMask.GetMask("Ground", "Wall");
    }

    private static HitSparks Get()
    {
        if (instance != null) return instance;
        instance = FindFirstObjectByType<HitSparks>();
        if (instance == null) instance = new GameObject("HitSparks").AddComponent<HitSparks>();
        if (instance.groundMask.value == 0) instance.groundMask = LayerMask.GetMask("Ground", "Wall");
        return instance;
    }

    /// <summary>Duruş vuruşu kıvılcımı. dir: vuruş yönü (oyuncudan düşmana).</summary>
    public static void Play(Vector3 position, Vector2 direction, string sortingLayer, int sortingOrder, float power = 1f)
    {
        Get().Spawn(position, direction, sortingLayer, sortingOrder, power);
    }

    private void Spawn(Vector3 p, Vector2 dir, string layer, int order, float power)
    {
        if (Time.unscaledTime - lastSpawn < minInterval) return;
        lastSpawn = Time.unscaledTime;

        float side = Mathf.Abs(dir.x) > 0.01f ? Mathf.Sign(dir.x) : 1f;
        int n = Mathf.RoundToInt(sparkCount2 * power), h = Mathf.RoundToInt(heavyCount2 * power);

        // havaya süzülenler
        for (int i = 0; i < floatCount; i++)
        {
            float a = Random.Range(floatAngle.x, floatAngle.y) * Mathf.Deg2Rad;
            Spark k = Take(layer, order, streak);
            k.pos = (Vector2)p + Random.insideUnitCircle * 0.12f;
            k.vel = new Vector2(Mathf.Cos(a) * side, Mathf.Sin(a)) * Random.Range(floatSpeed.x, floatSpeed.y);
            k.life = Random.Range(floatLife.x, floatLife.y);
            k.w = sparkWidth * Random.Range(0.5f, 0.75f);
            k.grav = floatGravity; k.dragMul = 1.6f;
        }

        for (int i = 0; i < n + h; i++)
        {
            bool heavy = i >= n;
            float a = Random.Range(angle2.x, angle2.y) * Mathf.Deg2Rad;
            float s = heavy ? Random.Range(heavySpeed.x, heavySpeed.y) : Random.Range(speed.x, speed.y);
            Spark k = Take(layer, order, streak);
            k.pos = (Vector2)p + Random.insideUnitCircle * 0.06f;
            k.vel = new Vector2(Mathf.Cos(a) * side, Mathf.Sin(a)) * s * Mathf.Lerp(0.85f, 1.1f, power - 0.5f);
            k.life = Random.Range(life.x, life.y) * (heavy ? 1.4f : 1f);
            k.w = sparkWidth * (heavy ? 1.7f : Random.Range(0.7f, 1.1f));
        }

        if (flash)
        {
            Spark f = Take(layer, order + 1, dot);
            f.isFlash = true; f.pos = p; f.vel = Vector2.zero; f.life = flashTime; f.w = sparkFlashSize * power;
        }
    }

    private Spark Take(string layer, int order, Sprite sprite)
    {
        Spark k = pool.Count > 0 ? pool.Pop() : null;
        if (k == null)
        {
            var go = new GameObject("~spark");
            go.transform.SetParent(transform, false);
            k = new Spark { t = go.transform, sr = go.AddComponent<SpriteRenderer>() };
            k.sr.sharedMaterial = GetMat();
        }
        Build();
        k.sr.sprite = sprite;
        k.sr.sortingLayerName = layer;
        k.sr.sortingOrder = order;
        k.sr.enabled = true;
        k.age = 0f; k.bounces = 0; k.ember = false; k.isFlash = false; k.emberAge = 0f; k.grav = 1f; k.dragMul = 1f;
        live.Add(k);
        return k;
    }

    private void Update()
    {
        float dt = Time.deltaTime;   // hit-stop'ta donar, sonra patlar
        if (mat != null) mat.SetColor("_GlowColor", fireGlow2);

        for (int i = live.Count - 1; i >= 0; i--)
        {
            Spark k = live[i];
            k.age += dt;

            if (k.isFlash)
            {
                float u = k.age / Mathf.Max(0.001f, k.life);
                if (u >= 1f) { Recycle(i); continue; }
                float sz = k.w * (0.6f + 0.6f * u);
                k.t.position = new Vector3(k.pos.x, k.pos.y, 0f);
                k.t.rotation = Quaternion.identity;
                k.t.localScale = new Vector3(sz, sz, 1f);
                k.sr.color = new Color(fireHot2.r, fireHot2.g, fireHot2.b, (1f - u) * 0.6f);   // HDR parlaklıkta çok büyümesin
                continue;
            }

            if (!k.ember)
            {
                k.vel.y -= gravity * k.grav * dt;
                k.vel *= 1f / (1f + drag * k.dragMul * dt);
                Vector2 step = k.vel * dt;
                RaycastHit2D hit = step.y < 0f ? Physics2D.Raycast(k.pos, step.normalized, step.magnitude + 0.01f, groundMask) : default;

                if (hit.collider != null && hit.normal.y > 0.5f)
                {
                    k.pos = hit.point + hit.normal * 0.01f;
                    if (k.bounces == 0 && -k.vel.y > 2.5f)
                    {
                        k.vel = new Vector2(k.vel.x * bounceFriction, -k.vel.y * bounce);
                        k.bounces++;
                    }
                    else
                    {
                        k.ember = true; k.emberAge = 0f;
                        k.vel = new Vector2(k.vel.x * 0.35f, 0f);
                    }
                }
                else k.pos += step;

                if (k.age >= k.life && !k.ember) { Recycle(i); continue; }
            }
            else
            {
                k.emberAge += dt;
                k.vel.x *= 1f / (1f + 10f * dt);
                k.pos.x += k.vel.x * dt;
                if (k.emberAge >= emberTime) { Recycle(i); continue; }
            }

            // görünüm: hıza göre uzayan çizgi, sıcak → soğuk, söner
            float sp = k.vel.magnitude;
            float t01 = k.ember ? 1f : Mathf.Clamp01(k.age / k.life);
            float len = k.ember ? k.w * 1.6f : sparkBaseLength + sp * sparkLengthPerSpeed;
            float alpha = k.ember ? 1f - k.emberAge / emberTime : 1f - t01 * t01;
            float ang = sp > 0.01f && !k.ember ? Mathf.Atan2(k.vel.y, k.vel.x) * Mathf.Rad2Deg : 0f;

            k.t.position = new Vector3(k.pos.x, k.pos.y, 0f);
            k.t.rotation = Quaternion.Euler(0f, 0f, ang);
            k.t.localScale = new Vector3(len, k.w * (k.ember ? 0.9f : 1f), 1f);
            Color c = Fire(k.ember ? 1f : Mathf.Clamp01(t01 * 1.25f));
            c.a = Mathf.Clamp01(alpha);
            k.sr.color = c;
        }
    }

    // sarı → turuncu → kırmızı
    private Color Fire(float t)
    {
        return t < 0.5f ? Color.Lerp(fireHot2, fireMid2, t * 2f) : Color.Lerp(fireMid2, fireCool2, (t - 0.5f) * 2f);
    }

    private void Recycle(int i)
    {
        Spark k = live[i];
        live.RemoveAt(i);
        k.sr.enabled = false;
        pool.Push(k);
    }

    // ---------------------------------------------------------
    // Görseller (koddan): 1 birimlik yumuşak çizgi + yuvarlak parlama
    // ---------------------------------------------------------

    private static void Build()
    {
        if (streak == null)
        {
            const int W = 64, H = 16;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = Mathf.Abs((y + 0.5f) / H * 2f - 1f);
                    float body = Mathf.Clamp01(1f - v * v);                       // yuvarlak kenar
                    float tail = Mathf.SmoothStep(0f, 1f, u / 0.85f);               // arkada incelen kuyruk
                    float head = Mathf.Clamp01((1f - u) / 0.12f);                   // ön uç yuvarlak
                    float a = body * tail * head;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            streak = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.85f, 0.5f), W);   // pivot ön uçta
        }

        if (dot == null)
        {
            const int S = 48;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f)) / (S * 0.5f);
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            dot = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        }
    }

    private static Material GetMat()
    {
        if (mat != null) return mat;
        Shader sh = Resources.Load<Shader>("AtkGlow/SpriteGlowAdd");
        if (sh == null) sh = Shader.Find("ProjectM/SpriteGlowAdd");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        mat = new Material(sh) { name = "HitSparks" };
        return mat;
    }
}
