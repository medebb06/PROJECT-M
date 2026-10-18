using UnityEngine;

/// <summary>
/// SIYAH-BEYAZ DUNYA — oyuncu ve demirci disindaki her sey gri tonlamali.
///
/// Dunya shader'lari (SilhouetteRim, Water, Sky, Stars, Mist, SpriteAdditive, Aurora, ParticleGlow, SpriteLitGray, SpriteUnlitGray)
/// kuresel _WorldGray degerini okur: 0 = orijinal renk, 1 = siyah-beyaz.
/// Karakter shader'lari (CharacterRim, ProjectedShadow, FootShade, Spine) bu degeri okumaz, renkli kalir.
///
/// Bu bilesen sahnedeki bir objeye konur; degeri her karede gonderir. Sahnede yoksa deger 0'dir (renkli).
/// Yeni eklenen sprite'lari gri materyale gecirmek icin: Tools > World Gray > Apply To Open Scene.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-1000)]
public class WorldColorGrade : MonoBehaviour
{
    [Tooltip("1 = dunya tamamen siyah-beyaz, 0 = orijinal renkler.")]
    [Range(0f, 1f)] public float gray = 1f;

    [Header("Renk ayari (1 = degisiklik yok)")]
    [Range(0f, 2f)] public float brightness = 1f;
    [Range(0f, 2f)] public float contrast = 1f;
    [Range(0f, 2.5f)] public float saturation = 1f;
    [Tooltip("Siyah noktasi: + degerler siyahlari derinlestirir, - degerler siyahlari acar (sisli/mat gorunum).")]
    [Range(-0.3f, 0.3f)] public float blackPoint = 0f;
    [Tooltip("Orta tonlar: 1 = degisiklik yok, buyuk deger ortalari aydinlatir.")]
    [Range(0.5f, 2f)] public float gamma = 1f;

    [Header("Renk dengesi (golge / orta / isik tonu)")]
    [Tooltip("Golgelere verilecek renk. Beyaz = degisiklik yok.")]
    public Color shadowTint = Color.white;
    [Range(0f, 1f)] public float shadowAmount = 0f;
    [Tooltip("Orta tonlara verilecek renk. Beyaz = degisiklik yok.")]
    public Color midTint = Color.white;
    [Range(0f, 1f)] public float midAmount = 0f;
    [Tooltip("Isiklara verilecek renk. Beyaz = degisiklik yok.")]
    public Color highlightTint = Color.white;
    [Range(0f, 1f)] public float highlightAmount = 0f;

    [Header("Renk bazli doygunluk (0 = degisiklik yok)")]
    [Range(-1f, 1f)] public float reds = 0f;
    [Range(-1f, 1f)] public float yellows = 0f;
    [Range(-1f, 1f)] public float greens = 0f;
    [Range(-1f, 1f)] public float cyans = 0f;
    [Range(-1f, 1f)] public float blues = 0f;
    [Range(-1f, 1f)] public float magentas = 0f;

    [Tooltip("Soluk renkleri daha cok canlandirir, zaten canli olanlara az dokunur.")]
    [Range(0f, 2f)] public float vibrance = 0f;

    [Tooltip("Acikken TimeOfDay'in post-process renk filtresi ve bloom tonu notr (beyaz) olur; yoksa gece mavisi dunyayi boyar.")]
    public bool neutralizePostTint = true;

    /// <summary>TimeOfDay bunu okur: true ise renk filtresi / bloom tonu uygulanmaz.</summary>
    public static bool NeutralizeGrade { get; private set; }

    private static readonly int GrayId = Shader.PropertyToID("_WorldGray");
    private static readonly int GradeId = Shader.PropertyToID("_WorldGrade");
    private static readonly int Grade2Id = Shader.PropertyToID("_WorldGrade2");
    private static readonly int ShadowId = Shader.PropertyToID("_WorldShadow");
    private static readonly int MidId = Shader.PropertyToID("_WorldMid");
    private static readonly int HighId = Shader.PropertyToID("_WorldHigh");
    private static readonly int Hue1Id = Shader.PropertyToID("_WorldHue1");
    private static readonly int Hue2Id = Shader.PropertyToID("_WorldHue2");

    // Beyaz-disi ton: parlakliktan bagimsiz renk kaymasi (offset) olarak uygulanir.
    private static Vector4 Offset(Color c, float amount)
    {
        float a = (c.r + c.g + c.b) / 3f;
        return new Vector4((c.r - a) * amount * 0.5f, (c.g - a) * amount * 0.5f, (c.b - a) * amount * 0.5f, 0f);
    }

    private void Push()
    {
        Shader.SetGlobalFloat(GrayId, gray);
        Shader.SetGlobalVector(GradeId, new Vector4(brightness - 1f, contrast - 1f, saturation - 1f, vibrance));
        Shader.SetGlobalVector(Grade2Id, new Vector4(blackPoint, gamma, 0f, 0f));
        Shader.SetGlobalVector(ShadowId, Offset(shadowTint, shadowAmount));
        Shader.SetGlobalVector(MidId, Offset(midTint, midAmount));
        Shader.SetGlobalVector(HighId, Offset(highlightTint, highlightAmount));
        Shader.SetGlobalVector(Hue1Id, new Vector4(reds, yellows, greens, 0f));
        Shader.SetGlobalVector(Hue2Id, new Vector4(cyans, blues, magentas, 0f));
        NeutralizeGrade = neutralizePostTint && gray > 0.001f;
    }

    private void OnEnable() { Push(); }
    private void OnValidate() { Push(); }
    private void Update() { Push(); }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(GrayId, 0f);
        Shader.SetGlobalVector(GradeId, Vector4.zero);
        Shader.SetGlobalVector(Grade2Id, Vector4.zero);
        Shader.SetGlobalVector(ShadowId, Vector4.zero);
        Shader.SetGlobalVector(MidId, Vector4.zero);
        Shader.SetGlobalVector(HighId, Vector4.zero);
        Shader.SetGlobalVector(Hue1Id, Vector4.zero);
        Shader.SetGlobalVector(Hue2Id, Vector4.zero);
        NeutralizeGrade = false;
    }
}
