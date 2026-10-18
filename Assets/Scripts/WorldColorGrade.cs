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

    [Tooltip("Acikken TimeOfDay'in post-process renk filtresi ve bloom tonu notr (beyaz) olur; yoksa gece mavisi dunyayi boyar.")]
    public bool neutralizePostTint = true;

    /// <summary>TimeOfDay bunu okur: true ise renk filtresi / bloom tonu uygulanmaz.</summary>
    public static bool NeutralizeGrade { get; private set; }

    private static readonly int GrayId = Shader.PropertyToID("_WorldGray");

    private void Push()
    {
        Shader.SetGlobalFloat(GrayId, gray);
        NeutralizeGrade = neutralizePostTint && gray > 0.001f;
    }

    private void OnEnable() { Push(); }
    private void OnValidate() { Push(); }
    private void Update() { Push(); }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(GrayId, 0f);
        NeutralizeGrade = false;
    }
}
