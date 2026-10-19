using UnityEngine;

/// Rüzgâr sallantısını (SilhouetteRim _WindAmount) yalnızca Play modunda açar.
/// Sahnede (edit modunda) ağaç sabit durur. Materyalin kendisi 0 kalır, değer renderer başına yazılır.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayOnlyWind : MonoBehaviour
{
    public float windAmount = 0.55f;
    static readonly int WindId = Shader.PropertyToID("_WindAmount");
    SpriteRenderer sr;
    MaterialPropertyBlock mpb;

    void OnEnable() { Apply(); }
    void OnDisable()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.SetPropertyBlock(null);
    }
    void LateUpdate() { Apply(); }

    void Apply()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (mpb == null) mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(WindId, Application.isPlaying ? windAmount : 0f);
        sr.SetPropertyBlock(mpb);
    }
}
