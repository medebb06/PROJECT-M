using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Boss 1: PSD katmanlarindan kurulan kemik hiyerarsisi + nefes (idle) animasyonu.
/// Resources/Boss1/boss1_rig.json + katman PNG'lerini okur. TrainingDummy ile birlikte calisir
/// (oyuncu vurabilir); vurus aninda hafif sarsilir.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class Boss1Rig : MonoBehaviour
{
    [Serializable] class BoneDef { public string name; public string parent; public float px; public float py; }
    [Serializable] class LayerDef { public string name; public string file; public float x, y, w, h; public int order; public string bone; }
    [Serializable] class RigDef { public float ppu; public float originX; public float originY; public BoneDef[] bones; public LayerDef[] layers; }

    [Header("Gorunum")]
    [Tooltip("1 = PSD boyutu. Boss'u buyutup kucultur.")]
    public float scale = 1f;
    public bool flipX = false;
    public Color tint = Color.white;
    public Material material;
    public string sortingLayerName = "";
    public int sortingOrder = 4;

    [Header("Nefes")]
    [Range(0.05f, 1.2f)] public float breathsPerSecond = 0.22f;
    [Range(0f, 0.08f)] public float chestScale = 0.022f;
    [Range(0f, 0.3f)] public float chestLift = 0.06f;
    [Range(0f, 6f)] public float chestTiltDeg = 1.2f;
    [Range(0f, 0.2f)] public float bellyLift = 0.03f;
    [Range(0f, 6f)] public float headNodDeg = 1.8f;
    [Range(0f, 8f)] public float armSwayDeg = 2.2f;
    [Range(0f, 8f)] public float swordSwayDeg = 1.4f;
    [Range(0f, 0.1f)] public float rootSquash = 0.012f;

    [Header("Vurus tepkisi")]
    [Range(0f, 20f)] public float flinchDeg = 7f;
    [Range(0f, 0.6f)] public float flinchShove = 0.18f;
    [Range(4f, 30f)] public float flinchStiffness = 16f;

    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    readonly Dictionary<string, Vector3> restPos = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Quaternion> restRot = new Dictionary<string, Quaternion>();
    TrainingDummy dummy;
    int lastTotal;
    float flinch, flinchVel, phase;
    Transform visual;

    void Awake()
    {
        dummy = GetComponent<TrainingDummy>();
        if (visual == null) visual = transform.Find("~boss");
        if (visual == null || visual.childCount == 0) Rebuild();
        else Cache();
        phase = UnityEngine.Random.value * 6.28f;
        if (dummy != null) lastTotal = dummy.TotalDamage;
    }

    [ContextMenu("Yeniden Kur")]
    public void Rebuild()
    {
        Transform old = transform.Find("~boss");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        TextAsset json = Resources.Load<TextAsset>("Boss1/boss1_rig");
        if (json == null) { Debug.LogError("Boss1Rig: Resources/Boss1/boss1_rig.json bulunamadi."); return; }
        RigDef rig = JsonUtility.FromJson<RigDef>(json.text);

        GameObject v = new GameObject("~boss");
        v.transform.SetParent(transform, false);
        visual = v.transform;
        float s = scale / rig.ppu;
        visual.localScale = new Vector3(flipX ? -scale : scale, scale, 1f);

        SortingGroup sg = v.AddComponent<SortingGroup>();
        sg.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
        sg.sortingOrder = sortingOrder;

        // Kemikler (birim: kok uzayi, scale visual'de)
        bones.Clear();
        Dictionary<string, Vector2> world = new Dictionary<string, Vector2>();
        foreach (BoneDef b in rig.bones)
            world[b.name] = new Vector2((b.px - rig.originX) / rig.ppu, (rig.originY - b.py) / rig.ppu);

        foreach (BoneDef b in rig.bones) // sirali: ebeveyn once
        {
            GameObject go = new GameObject(b.name);
            Transform parent = string.IsNullOrEmpty(b.parent) ? visual : bones[b.parent];
            go.transform.SetParent(parent, false);
            Vector2 pw = string.IsNullOrEmpty(b.parent) ? Vector2.zero : world[b.parent];
            Vector2 lp = world[b.name] - pw;
            go.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
            bones[b.name] = go.transform;
        }

        Material mat = material != null ? material : DefaultMaterial();

        foreach (LayerDef l in rig.layers)
        {
            Sprite sp = Resources.Load<Sprite>("Boss1/" + l.file);
            if (sp == null) { Debug.LogWarning("Boss1Rig: sprite yok: " + l.file); continue; }
            GameObject go = new GameObject(l.name);
            Transform bone = bones[l.bone];
            go.transform.SetParent(bone, false);
            Vector2 center = new Vector2((l.x + l.w * 0.5f - rig.originX) / rig.ppu, (rig.originY - (l.y + l.h * 0.5f)) / rig.ppu);
            Vector2 lp = center - world[l.bone];
            go.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.color = tint;
            sr.sharedMaterial = mat;
            sr.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
            sr.sortingOrder = l.order;
            go.layer = gameObject.layer;
        }
        v.layer = gameObject.layer;
        foreach (Transform t in bones.Values) t.gameObject.layer = gameObject.layer;
        Cache();
    }

    Material DefaultMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        return new Material(sh) { name = "Boss1_Runtime" };
    }

    void Cache()
    {
        bones.Clear(); restPos.Clear(); restRot.Clear();
        if (visual == null) return;
        foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
        {
            if (t == visual) continue;
            bones[t.name] = t;
            restPos[t.name] = t.localPosition;
            restRot[t.name] = t.localRotation;
        }
    }

    void Set(string n, Vector3 dPos, float dRot, Vector3? scl = null)
    {
        Transform t;
        if (!bones.TryGetValue(n, out t)) return;
        t.localPosition = restPos[n] + dPos;
        t.localRotation = restRot[n] * Quaternion.Euler(0f, 0f, dRot);
        t.localScale = scl ?? Vector3.one;
    }

    void LateUpdate()
    {
        if (visual == null || bones.Count == 0) return;
        float dt = Time.deltaTime;

        // Vurus tespiti
        if (dummy != null && dummy.TotalDamage != lastTotal)
        {
            lastTotal = dummy.TotalDamage;
            flinchVel += 1f * flinchStiffness * 1.4f;
        }
        flinchVel += (-flinch * flinchStiffness * flinchStiffness - flinchVel * flinchStiffness * 0.5f) * dt;
        flinch += flinchVel * dt;
        float f = Mathf.Clamp(flinch, -1.5f, 1.5f);
        float dir = flipX ? -1f : 1f;

        float t = Time.time * breathsPerSecond * 6.2832f + phase;
        float b = Mathf.Sin(t);               // nefes: -1..1
        float b2 = Mathf.Sin(t - 0.7f);       // gecikmeli (bas)
        float b3 = Mathf.Sin(t - 1.3f);       // daha gecikmeli (kol/kilic)
        float inhale = b * 0.5f + 0.5f;

        // Boss sola bakiyor (kilic solda): geriye = +x
        Set("root", new Vector3(f * flinchShove * 0.6f * dir, -inhale * 0.0f, 0f), 0f,
            new Vector3(1f + rootSquash * 0.5f * b, 1f - rootSquash * b, 1f));
        Set("belly", new Vector3(0f, bellyLift * inhale, 0f), -f * flinchDeg * 0.3f * dir);
        Set("chest", new Vector3(0f, chestLift * inhale, 0f), (-chestTiltDeg * inhale + f * flinchDeg) * dir,
            new Vector3(1f + chestScale * inhale, 1f + chestScale * 1.4f * inhale, 1f));
        Set("head", new Vector3(0f, 0.02f * b2, 0f), (headNodDeg * b2 - f * flinchDeg * 0.7f) * dir);
        Set("shoulderR", Vector3.zero, (armSwayDeg * b3 + f * flinchDeg * 0.5f) * dir);
        Set("sword", Vector3.zero, (swordSwayDeg * -b3) * dir);
    }
}
