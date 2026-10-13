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

    private readonly List<SpriteRenderer> beams = new List<SpriteRenderer>();
    private readonly List<float> baseAlpha = new List<float>();
    private readonly List<float> factor = new List<float>();

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
        }
    }

    private void Update()
    {
        if (needsBuild)
        {
            needsBuild = false;
            Build();
        }

        if (!Application.isPlaying || flicker <= 0f)
            return;

        float time = Time.time * flickerSpeed;

        for (int i = 0; i < beams.Count; i++)
        {
            SpriteRenderer sr = beams[i];

            if (sr == null)
                continue;

            float k = 1f - flicker * 0.5f * (1f + Mathf.Sin(time * (1f + i * 0.41f) + i * 2.3f));
            Color c = sr.color;
            c.a = baseAlpha[i] * k;
            sr.color = c;
        }
    }
}
