using UnityEngine;

// PROJECT M — Smear dağılması: saldırı karesi değişince/bitince smear'ın "hayaleti" dünyada kalır,
// genişleyip sis gibi soluklaşır. Sonraki saldırı başlasa da kendi süresini tamamlar.
public class SmearGhostSpawner : MonoBehaviour
{
    [Tooltip("Dağılma süresi (sn).")] public float life = 0.5f;
    [Tooltip("Boy/genişlik büyümesi (1 = büyümez).")] public float grow = 1.1f;
    [Tooltip("Başlangıç opaklığı (0-1).")] [Range(0f, 1f)] public float startAlpha = 0.7f;
    [Tooltip("Yukarı süzülme (dünya birimi).")] public float rise = 0.1f;
    [Tooltip("Sis katmanı: daha büyük, daha soluk ikinci kopya.")] public bool haze = true;
    public float hazeGrow = 1.12f;

    SpriteRenderer sr, parentSR;
    Sprite prevSprite; Vector3 prevPos, prevScale; Quaternion prevRot; bool prevFlip; int prevLayer, prevOrder; Material prevMat; bool prevVis;

    void Awake() { sr = GetComponent<SpriteRenderer>(); parentSR = transform.parent != null ? transform.parent.GetComponent<SpriteRenderer>() : null; }

    bool Visible() { return sr != null && sr.enabled && gameObject.activeInHierarchy && sr.sprite != null && (parentSR == null || parentSR.enabled); }

    void LateUpdate()
    {
        bool vis = Visible();
        if (prevVis && (!vis || sr.sprite != prevSprite)) Spawn();
        prevVis = vis;
        if (vis)
        {
            prevSprite = sr.sprite; prevPos = transform.position; prevScale = transform.lossyScale; prevRot = transform.rotation;
            prevFlip = sr.flipX; prevLayer = sr.sortingLayerID; prevOrder = sr.sortingOrder; prevMat = sr.sharedMaterial;
        }
    }
    void OnDisable() { if (prevVis && prevSprite != null) Spawn(); prevVis = false; }

    void Spawn()
    {
        if (prevSprite == null) return;
        Make(1f, startAlpha, grow, 0);
        if (haze) Make(1.04f, startAlpha * 0.45f, grow * hazeGrow, -1);
        prevVis = false;
    }
    void Make(float s0, float a0, float g, int orderOff)
    {
        var go = new GameObject("~smear_ghost");
        go.transform.position = prevPos; go.transform.rotation = prevRot; go.transform.localScale = prevScale * s0;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = prevSprite; r.flipX = prevFlip; r.sharedMaterial = prevMat;
        r.sortingLayerID = prevLayer; r.sortingOrder = prevOrder + orderOff;
        var gh = go.AddComponent<SmearGhost>(); gh.Init(r, prevScale * s0, prevPos, a0, g, rise, life);
    }
}

public class SmearGhost : MonoBehaviour
{
    SpriteRenderer r; Vector3 s0, p0; float a0, g, rise, life, t;
    static int alive;
    public void Init(SpriteRenderer rr, Vector3 scale, Vector3 pos, float alpha, float grow, float riseUp, float lifeT)
    { r = rr; s0 = scale; p0 = pos; a0 = alpha; g = grow; rise = riseUp; life = Mathf.Max(0.05f, lifeT); alive++; if (alive > 24) Destroy(gameObject); }
    void OnDestroy() { alive--; }
    void Update()
    {
        t += Time.deltaTime; float k = Mathf.Clamp01(t / life);
        float e = 1f - (1f - k) * (1f - k);                 // çıkışta yavaşlayan büyüme
        transform.localScale = s0 * Mathf.Lerp(1f, g, e);
        transform.position = p0 + Vector3.up * (rise * e);
        float fade = 1f - k; fade *= fade;                   // sisin soluşu: başta hızlı, sonda uzun kuyruk
        r.color = new Color(1f, 1f, 1f, a0 * fade);
        if (k >= 1f) Destroy(gameObject);
    }
}
