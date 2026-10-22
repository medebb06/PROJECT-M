using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spine karakterlerine (Demirci vs.) karakter ışığı: meşale ışığı + ateş kenar ışığı + sahte normal map.
/// Spine her kare materyali atlastan yeniden atar; bu yüzden SkeletonRenderer.CustomMaterialOverride ile
/// her atlas materyalinin ProjectM/CharacterLit kopyası kullanılır. Karakterin kök objesine ekle.
/// </summary>
public class SpineCharacterLit : MonoBehaviour
{
    [Range(0f, 3f)] public float pointLit = 0.7f;
    [Range(0f, 4f)] public float fireRim = 1.8f;
    public float rimWidth = 0.09f;
    public float softness = 0.3f;

    private Spine.Unity.SkeletonRenderer sr;
    private MeshRenderer mr;
    private Shader lit;
    private readonly HashSet<Material> mine = new HashSet<Material>();

    private void Start()
    {
        sr = GetComponentInChildren<Spine.Unity.SkeletonRenderer>(true);
        mr = sr != null ? sr.GetComponent<MeshRenderer>() : null;
        Material refMat = Resources.Load<Material>("CharacterLit");
        lit = refMat != null ? refMat.shader : null;

        if (sr == null || mr == null || lit == null || !lit.isSupported)
            enabled = false;
    }

    private void LateUpdate()
    {
        Material[] mats = mr.sharedMaterials;

        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null || mine.Contains(m) || sr.CustomMaterialOverride.ContainsKey(m))
                continue;

            Material l = new Material(lit) { name = m.name + " (CharacterLit)", mainTexture = m.mainTexture };
            bool straight = m.HasProperty("_StraightAlphaInput") && m.GetFloat("_StraightAlphaInput") > 0.5f;
            l.SetFloat("_PMA", straight ? 0f : 1f);
            l.SetFloat("_PointLit", pointLit);
            l.SetFloat("_FireRim", fireRim);
            l.SetFloat("_FireRimWidth", rimWidth);
            l.SetFloat("_FireSoft", softness);
            mine.Add(l);
            sr.CustomMaterialOverride[m] = l;
        }
    }

    private void OnDestroy()
    {
        if (sr != null)
        {
            List<Material> keys = new List<Material>();
            foreach (var kv in sr.CustomMaterialOverride)
                if (mine.Contains(kv.Value)) keys.Add(kv.Key);
            foreach (Material k in keys) sr.CustomMaterialOverride.Remove(k);
        }

        foreach (Material m in mine)
            if (m != null) Destroy(m);
        mine.Clear();
    }
}
