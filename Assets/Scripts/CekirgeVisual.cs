using UnityEngine;

/// <summary>
/// Cekirge'nin gorseli (kemikli rig) ile EnemyController'i baglar:
///  - Yone bakma: kok SpriteRenderer.flipX -> rig ters cevrilir (anlik, incelme yok).
///  - Vurus/flas rengi: kok SpriteRenderer rengi tum parcalara yayilir.
///  - Atak: Attack klibinin firlama anindan itibaren kok, oyuncuya dogru ziplar
///    (hasar anina kadar iner); yukselme rig'in y'sinde gorsel olarak yapilir.
/// Hasar, blok, parry, posture vb. tamamen mevcut Enemy sistemi tarafindan yonetilir.
/// </summary>
[DefaultExecutionOrder(500)]
public class CekirgeVisual : MonoBehaviour
{
    public Transform rig;
    public Animator rigAnimator;

    [Header("Ziplayarak vurus (Attack klibi saniyeleri)")]
    public float launchTime = 0.55f;
    public float landTime = 1.05f;
    public float travelEndTime = 1.0f;
    public float jumpHeight = 2.2f;
    public float landOffset = 1.5f;
    public float maxJump = 7f;

    EnemyController enemy;
    SpriteRenderer rootSr;
    Rigidbody2D rb;
    SpriteRenderer[] parts;
    Color[] baseColors;
    Material[] baseMats;
    float rigBaseScale = 1f;
    bool flashing;

    bool launched;
    float startX, travel;
    float rigY;

    void Awake()
    {
        enemy = GetComponent<EnemyController>();
        rootSr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (rig == null) { var a = GetComponentInChildren<Animator>(); if (a != null) rig = a.transform; }
        if (rigAnimator == null && rig != null) rigAnimator = rig.GetComponent<Animator>();
        if (rig != null)
        {
            rigBaseScale = Mathf.Abs(rig.localScale.x);
            parts = rig.GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[parts.Length];
            baseMats = new Material[parts.Length];
            for (int i = 0; i < parts.Length; i++) { baseColors[i] = parts[i].color; baseMats[i] = parts[i].sharedMaterial; }
        }
    }

    // Bas ustu cubuklari (EnemyOverheadBars) koku olcer: gorunmez kok sprite'i kafanin ustune kadar uzat.
    void Start()
    {
        if (rootSr == null || rig == null) return;
        float top = transform.position.y;
        if (parts != null) for (int k = 0; k < parts.Length; k++) top = Mathf.Max(top, parts[k].bounds.max.y);
        float h = Mathf.Max(2f, top - transform.position.y);
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var clear = new Color32[16]; tex.SetPixels32(clear); tex.Apply();
        rootSr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f / h);
        rootSr.forceRenderingOff = true;
    }

    public Material BaseMaterialOf(SpriteRenderer sr)
    {
        if (parts != null) for (int k = 0; k < parts.Length; k++) if (parts[k] == sr && baseMats[k] != null) return baseMats[k];
        return sr.sharedMaterial;
    }

    public Color BaseColorOf(SpriteRenderer sr)
    {
        if (parts != null) for (int k = 0; k < parts.Length; k++) if (parts[k] == sr) return baseColors[k];
        return sr.color;
    }

    void LateUpdate()
    {
        if (rig == null) return;

        // Firlamaya kadar oyuncuyu takip et (yon anlik doner)
        if (enemy != null && enemy.CurrentState is EnemyAttackState && !launched && !enemy.IsDead) enemy.RetargetFacing();

        // ---- yon ----
        bool flip = rootSr != null && rootSr.flipX;
        var s = rig.localScale;
        s.x = rigBaseScale * (flip ? -1f : 1f);
        rig.localScale = s;

        // ---- renk: flas / sersem tonu / beyaz silüet YOK; her kare orijinal koyu renk ve materyal ----
        if (parts != null)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                if (baseMats[i] != null && parts[i].sharedMaterial != baseMats[i]) parts[i].sharedMaterial = baseMats[i];
                Color o = parts[i].color; Color bc = baseColors[i];
                if (o.r != bc.r || o.g != bc.g || o.b != bc.b) parts[i].color = new Color(bc.r, bc.g, bc.b, o.a);
            }
        }

        // ---- ziplama ----
        bool attacking = enemy != null && enemy.CurrentState is EnemyAttackState && !enemy.IsDead;
        float targetY = 0f;

        if (attacking && rigAnimator != null)
        {
            var st = rigAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName("Attack"))
            {
                float t = st.normalizedTime * st.length;

                if (!launched && t >= launchTime)
                {
                    launched = true;
                    startX = rb != null ? rb.position.x : transform.position.x;
                    float tx = enemy.target != null ? enemy.target.position.x : startX;
                    float dx = tx - startX;
                    float dir = dx >= 0f ? 1f : -1f;
                    travel = dir * Mathf.Clamp(Mathf.Abs(dx) - landOffset, 0f, maxJump);
                }

                if (launched)
                {
                    float uh = Mathf.Clamp01((t - launchTime) / Mathf.Max(0.01f, travelEndTime - launchTime));
                    float e = uh * uh * (3f - 2f * uh);
                    float x = startX + travel * e;
                    var p = transform.position; p.x = x; transform.position = p;
                    if (rb != null) rb.position = new Vector2(x, rb.position.y);

                    float uv = Mathf.Clamp01((t - launchTime) / Mathf.Max(0.01f, landTime - launchTime));
                    targetY = 4f * jumpHeight * uv * (1f - uv);
                }
            }
        }
        else
        {
            launched = false;
        }

        rigY = Mathf.MoveTowards(rigY, targetY, 30f * Time.deltaTime);
        if (!attacking) rigY = Mathf.MoveTowards(rigY, 0f, 30f * Time.deltaTime);
        var lp = rig.localPosition; lp.y = rigY; rig.localPosition = lp;
    }
}
