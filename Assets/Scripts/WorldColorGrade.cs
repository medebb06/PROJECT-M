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

    [Tooltip("Soluk renkleri daha cok canlandirir, zaten canli olanlara az dokunur.")]
    [Range(0f, 2f)] public float vibrance = 0f;

    [Tooltip("Acikken TimeOfDay'in post-process renk filtresi ve bloom tonu notr (beyaz) olur; yoksa gece mavisi dunyayi boyar.")]
    public bool neutralizePostTint = true;

    /// <summary>TimeOfDay bunu okur: true ise renk filtresi / bloom tonu uygulanmaz.</summary>
    public static bool NeutralizeGrade { get; private set; }

    private static readonly int GrayId = Shader.PropertyToID("_WorldGray");
    private static readonly int GradeId = Shader.PropertyToID("_WorldGrade");
    private static readonly int Grade2Id = Shader.PropertyToID("_WorldGrade2");

    private void Push()
    {
        Shader.SetGlobalFloat(GrayId, gray);
        Shader.SetGlobalVector(GradeId, new Vector4(brightness - 1f, contrast - 1f, saturation - 1f, vibrance));
        Shader.SetGlobalVector(Grade2Id, new Vector4(blackPoint, gamma, 0f, 0f));
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
        NeutralizeGrade = false;
    }
}
