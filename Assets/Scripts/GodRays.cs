using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ÇAPRAZ IŞIK HÜZMELERİ (god rays). Ekranın üstünden çapraz inen, yumuşak, toplamalı ışık
/// şeritleri. Kameraya bağlı çapanın (SkyAnchor) altında durur, hep görüş alanındadır.
/// Inspector'da her değişiklikte anında yeniden kurulur (Edit modunda da görünür, sahneye kaydedilmez).
/// </summary>
[ExecuteAlways]
public class GodRays : MonoBehaviour
{
    public Sprite raySprite;
    public Material material;

    [Header("Yön ve yerleşim")]
    [Tooltip("Derece. 0 = dik aşağı, + = sağ-aşağı, - = sol-aşağı.")]
    [Range(-80f, 80f)] public float angle = 32f;
    [Tooltip("Hüzmelerin çıktığı nokta (kamera merkezine göre; X ekran genişliği, Y ekran yüksekliği cinsinden).")]
    public Vector2 origin = new Vector2(-0.38f, 0.62f);
    [Tooltip("Hüzmelerin yana dağılımı (ekran genişliği cinsinden).")]
    public float spread = 0.75f;

    [Header("Işık kaynağını takip et (ay / güneş)")]
    [Tooltip("Açıkken hüzmeler gökteki ay/güneşten çıkar ve sahneye doğru yelpaze gibi açılır; kaynak hareket edince birlikte döner.")]
    public bool followLight = true;
    public float fanSpread = 14f;
    public float sourceWidth = 9f;
    [Tooltip("Hüzmelerin hedeflediği nokta (kamera merkezine göre, ekran yüksekliği cinsinden). Negatif Y = zemine doğru.")]
    public Vector2 aimPoint = new Vector2(0f, -0.35f);
    [Tooltip("Kaynak ufkun bu kadar altına inince hüzmeler tamamen söner (dünya birimi).")]
    public float fadeBelow = 1.5f;

    [Header("Hüzmeler")]
    [Range(1, 14)] public int count = 7;
    [Tooltip("Boy (ekran yüksekliği cinsinden).")]
    public float length = 1.9f;
    [Tooltip("En ince / en kalın (ekran genişliği cinsinden).")]
    public Vector2 widthRange = new Vector2(0.07f, 0.24f);
    public Color color = new Color(1f, 0.87f, 0.68f, 1f);
    [Range(0f, 1f)] public float intensity = 0.16f;
    public int sortingOrder = -5;
    public int seed = 7;

    [Header("Canlılık (Play'de)")]
    [Range(0f, 1f)] public float flicker = 0.35f;
    public float flickerSpeed = 0.18f;
    [Tooltip("Hüzmelerin yana doğru yavaş, düzenli salınımı (ekran genişliği cinsinden).")]
    [Range(0f, 0.2f)] public float sway = 0.04f;
    [Tooltip("Salınımın hızı (devir/10 sn gibi düşün; küçük = yavaş).")]
    public float swaySpeed = 0.35f;
    [Tooltip("Hüzme açısının derece cinsinden hafif sallanması.")]
    [Range(0f, 8f)] public float angleSway = 2.5f;

    private readonly List<SpriteRenderer> beams = new List<SpriteRenderer>();
    private readonly List<float> baseAlpha = new List<float>();
    private readonly List<float> factor = new List<float>();
    private readonly List<Vector3> basePos = new List<Vector3>();
    private Vector2 swayDir = Vector2.right;
    private float swayWorldW = 1f;

    // Saat dilimi (TimeOfDay) çarpanları: Inspector değerlerine dokunmaz.
    private bool todActive;
    private Color todColor = Color.white;
    private float todScale = 1f;

    /// <summary>TimeOfDay: renk ve şiddet çarpanı. Yeniden kurmadan anında uygular.</summary>
    public void ApplyTint(Color rgb, float scale)
    {
        // Değişmediyse dokunma (flicker animasyonu bozulmasın).
        if (todActive && beams.Count > 0 && Mathf.Approximately(scale, todScale) &&
            Mathf.Approximately(rgb.r, todColor.r) && Mathf.Approximately(rgb.g, todColor.g) && Mathf.Approximately(rgb.b, todColor.b))
            return;

        todActive = true;
        todColor = rgb;
        todScale = scale;

        for (int i = 0; i < beams.Count; i++)
        {
            if (beams[i] == null || i >= factor.Count)
                continue;

            baseAlpha[i] = intensity * todScale * factor[i];
            Color c = todColor;
            c.a = baseAlpha[i];
            beams[i].color = c;
        }
    }

    private bool needsBuild;

    // Aktifleşme sırasında hiyerarşi değiştirilemez: kurulumu bir sonraki kareye ertele.
    private void OnEnable()
    {
        needsBuild = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled && needsBuild)
                {
                    needsBuild = false;
                    Build();
                }
            };
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled)
                Build();
        };
    }
#endif

    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);

            if (c.name != "~ray")
                continue;

            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }

        beams.Clear();
        baseAlpha.Clear();
        factor.Clear();
        basePos.Clear();
    }

    [ContextMenu("Yeniden Kur")]
    public void Build()
    {
        Clear();

        if (raySprite == null || material == null)
            return;

        Camera cam = Camera.main;
        float viewH = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 20f;
        float viewW = viewH * (cam != null ? Mathf.Max(1.2f, cam.aspect) : 1.78f);

        System.Random rng = new System.Random(seed);
        float rad = angle * Mathf.Deg2Rad;
        Vector2 down = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
        Vector2 perp = new Vector2(-down.y, down.x);
        Vector2 sp = raySprite.bounds.size;
        float len = length * viewH;

        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? i / (float)(count - 1) - 0.5f : 0f;
            float jitter = (float)(rng.NextDouble() - 0.5) * 0.08f;

            Vector2 start =
                new Vector2(origin.x * viewW, origin.y * viewH) +
                perp * ((t + jitter) * spread * viewW);

            float w = Mathf.Lerp(widthRange.x, widthRange.y, (float)rng.NextDouble()) * viewW;
            float f = Mathf.Lerp(0.45f, 1f, (float)rng.NextDouble());
            float a = intensity * (todActive ? todScale : 1f) * f;
            float lenK = Mathf.Lerp(0.75f, 1.1f, (float)rng.NextDouble());

            GameObject g = new GameObject("~ray");
            g.hideFlags = HideFlags.DontSave;
            g.transform.SetParent(transform, false);

            Vector2 center = start + down * (len * lenK * 0.5f);
            g.transform.localPosition = new Vector3(center.x, center.y, 0f);
            g.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            g.transform.localScale = new Vector3(w / Mathf.Max(0.0001f, sp.x), len * lenK / Mathf.Max(0.0001f, sp.y), 1f);

            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = raySprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = sortingOrder;

            Color c = todActive ? todColor : color;
            c.a = a;
            sr.color = c;

            beams.Add(sr);
            baseAlpha.Add(a);
            factor.Add(f);
            basePos.Add(g.transform.localPosition);
        }

        swayDir = perp;
        swayWorldW = viewW;
    }

    private Transform sunT, moonT;
    private SpriteRenderer sunR, moonR;
    private float lightFade = 1f;

    private void FindBodies()
    {
        if (sunT == null) { GameObject g = GameObject.Find("Sun"); if (g != null) { sunT = g.transform; sunR = g.GetComponent<SpriteRenderer>(); } }
        if (moonT == null) { GameObject g = GameObject.Find("Moon"); if (g != null) { moonT = g.transform; moonR = g.GetComponent<SpriteRenderer>(); } }
    }

    // Aktif isik kaynagi: rengi daha gorunur olan (gunduz gunes, gece ay).
    private bool PickLight(out Vector3 pos)
    {
        FindBodies();
        float sa = (sunR != null && sunR.enabled) ? sunR.color.a : -1f;
        float ma = (moonR != null && moonR.enabled) ? moonR.color.a : -1f;
        pos = Vector3.zero;
        if (sa < 0f && ma < 0f) return false;
        pos = (sa >= ma ? sunT : moonT).position;
        return true;
    }

    private void LayoutFromLight()
    {
        if (beams.Count == 0) return;
        Vector3 src;
        if (!PickLight(out src)) return;

        Camera cam = Camera.main;
        float viewH = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 20f;
        Vector3 camPos = cam != null ? cam.transform.position : transform.position;
        Vector3 aim = camPos + new Vector3(aimPoint.x * viewH * (cam != null ? cam.aspect : 1.78f), aimPoint.y * viewH, 0f);

        // Kaynak ufuk cizgisinin altina inince sonsun
        lightFade = Mathf.Clamp01((src.y - (aim.y - fadeBelow)) / Mathf.Max(0.01f, fadeBelow + 2f));

        Vector2 d = ((Vector2)(aim - src)).normalized;
        float baseAngle = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
        float dist = Vector2.Distance(src, aim);
        float swayT = Application.isPlaying ? Time.time : 0f;

        for (int i = 0; i < beams.Count; i++)
        {
            SpriteRenderer sr = beams[i];
            if (sr == null) continue;

            float t = beams.Count > 1 ? i / (float)(beams.Count - 1) - 0.5f : 0f;
            float a = baseAngle + t * fanSpread + Mathf.Sin(swayT * swaySpeed + i * 0.9f) * angleSway;
            float rad = a * Mathf.Deg2Rad;
            Vector2 down = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));

            float len = Mathf.Max(dist * 1.25f, length * viewH * 0.8f);
            Transform tr = sr.transform;
            Vector2 perpD = new Vector2(-down.y, down.x);
            Vector3 centerWorld = src + (Vector3)(perpD * (t * sourceWidth)) + (Vector3)(down * (len * 0.5f));
            centerWorld.z = transform.position.z;
            tr.position = centerWorld;
            tr.rotation = Quaternion.Euler(0f, 0f, a);

            Vector2 sp = raySprite != null ? (Vector2)raySprite.bounds.size : Vector2.one;
            Vector3 sc = tr.localScale;
            tr.localScale = new Vector3(sc.x, len / Mathf.Max(0.0001f, sp.y), 1f);

            Color c = sr.color;
            c.a = baseAlpha[i] * lightFade;
            sr.color = c;
        }
    }

    private void Update()
    {
        if (needsBuild)
        {
            needsBuild = false;
            Build();
        }

        if (followLight)
            LayoutFromLight();

        if (!Application.isPlaying)
            return;

        // Düzenli, yumuşak salınım: her hüzme sırayla aynı ritimde, hafif faz farkıyla yana kayar.
        float sw = Time.time * swaySpeed;

        for (int i = 0; !followLight && i < beams.Count && i < basePos.Count; i++)
        {
            if (beams[i] == null)
                continue;

            float ph = sw + i * 0.9f;
            float off = Mathf.Sin(ph) * sway * swayWorldW;
            Transform tr = beams[i].transform;
            tr.localPosition = basePos[i] + (Vector3)(swayDir * off);
            tr.localRotation = Quaternion.Euler(0f, 0f, angle + Mathf.Sin(ph * 0.7f + 1.3f) * angleSway);
        }

        if (flicker <= 0f)
            return;

        float time = Time.time * flickerSpeed;

        for (int i = 0; i < beams.Count; i++)
        {
            SpriteRenderer sr = beams[i];

            if (sr == null)
                continue;

            float k = 1f - flicker * 0.5f * (1f + Mathf.Sin(time * (1f + i * 0.41f) + i * 2.3f));
            Color c = sr.color;
            c.a = baseAlpha[i] * k * lightFade;
            sr.color = c;
        }
    }
}
