using UnityEngine;

/// <summary>
/// DÜŞMAN KAPSÜL GÖVDE: pixel-art sprite + animasyon yerine kodla çizilen dikey kapsül.
///  - Ana SpriteRenderer çizilmez (bounds için durur), Animator kapatılır.
///  - Kapsül çocuk bir SpriteRenderer'dır; ayaklarından squash/stretch yapar (TrainingDummy gibi).
///  - Vurulunca / parry / posture kırılınca ezilip esner, vuruş yönüne hafifçe savrulur.
///  - Saldırıya hazırlanırken çömelir, vuruş kararlı olunca esner (kırmızı flaş EnemyAttackTelegraph'te).
/// </summary>
[DefaultExecutionOrder(-1000)]
public class EnemyCapsuleBody : MonoBehaviour
{
    [Header("Görünüm")]
    public Color bodyColor = new Color(0.9f, 0.91f, 0.94f, 1f);
    [Tooltip("Gövde boyutu, collider'ın boyuna göre çarpan.")]
    public Vector2 sizeMultiplier = new Vector2(1f, 1f);

    [Header("Squash / Stretch (TrainingDummy ile aynı)")]
    public float hitSquash = 0.6f;
    public float parrySquash = 0.5f;
    public float breakSquash = 0.9f;
    [Tooltip("Saldırı hazırlığında çömelme.")]
    public float windupCrouch = 0.55f;
    [Tooltip("Vuruş kararlı olunca esneme (negatif).")]
    public float commitStretch = -0.3f;
    [Tooltip("Hazırlıkta hedeften uzağa eğilme (derece).")]
    public float leanBack = 9f;
    [Tooltip("Vuruşta hedefe doğru atılma eğimi (derece).")]
    public float leanForward = 7f;
    public float knockbackWobble = 0.22f;

    public SpriteRenderer Renderer { get; private set; }

    private EnemyController enemy;
    private EnemyAttackTelegraph telegraph;
    private Transform body;
    private Vector3 basePos;

    private float impulse;
    private float state;
    private float lean;
    private Vector2 wobbleOffset;
    private Vector2 wobbleVel;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        telegraph = GetComponent<EnemyAttackTelegraph>();

        SpriteRenderer root = GetComponent<SpriteRenderer>();

        // Animator'ı kapat (sprite'ı geri yazmasın). Controller yerinde kalır:
        // saldırı zamanlaması klip sürelerinden okunuyor.
        Animator[] anims = GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < anims.Length; i++)
            anims[i].enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        Bounds b = col != null ? col.bounds : (root != null ? root.bounds : new Bounds(transform.position, Vector3.one));

        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(ls.y));

        float localW = b.size.x / sx * sizeMultiplier.x;
        float localH = b.size.y / sy * sizeMultiplier.y;

        Sprite capsule = BuildCapsule(localW, localH, bodyColor);

        // Ana renderer: sprite = kapsül (bounds / diğer sistemler için), ama çizilmez.
        if (root != null)
        {
            root.sprite = capsule;
            root.forceRenderingOff = true;
        }

        GameObject go = new GameObject("CapsuleBody");
        body = go.transform;
        body.SetParent(transform, false);

        // Ayak noktası: collider'ın alt orta noktası (kökün yerel uzayında).
        Vector3 feetWorld = new Vector3(b.center.x, b.min.y, transform.position.z);
        basePos = transform.InverseTransformPoint(feetWorld);
        body.localPosition = basePos;

        Renderer = go.AddComponent<SpriteRenderer>();
        Renderer.sprite = capsule;
        Renderer.color = Color.white;

        if (root != null)
        {
            Renderer.sharedMaterial = root.sharedMaterial;
            Renderer.sortingLayerID = root.sortingLayerID;
            Renderer.sortingOrder = root.sortingOrder;
        }
    }

    private void Start()
    {
        // Telegraph'ın Awake'i kök renderer'ı yakalamıştı: kırmızı flaş kapsülde görünsün.
        if (telegraph != null && Renderer != null)
            telegraph.SetRenderer(Renderer);
    }

    private void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.BalanceBroken += OnBreak;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.BalanceBroken -= OnBreak;
    }

    private void OnEnemyHit(EnemyController e, DamageInfo d, HitResult r)
    {
        if (e != enemy || enemy == null)
            return;

        impulse = Mathf.Min(1f, impulse + hitSquash);

        Vector2 dir = d.direction.sqrMagnitude > 0.001f ? d.direction.normalized : Vector2.right;
        wobbleVel += dir * (knockbackWobble * 18f);

        if (d.fromAbove)
            wobbleVel += Vector2.down * 3f;
    }

    private void OnParry(EnemyController e, bool broke)
    {
        if (e != enemy || enemy == null)
            return;

        impulse = Mathf.Min(1f, impulse + (broke ? breakSquash : parrySquash));
    }

    private void OnBreak(EnemyController e)
    {
        if (e != enemy || enemy == null)
            return;

        impulse = Mathf.Min(1f, impulse + breakSquash);
    }

    private void Update()
    {
        if (body == null)
            return;

        float dt = Time.unscaledDeltaTime;

        // Yaylı savrulma (dünya birimi → kök yerel uzayı).
        Vector2 force = -wobbleOffset * 220f - wobbleVel * 16f;
        wobbleVel += force * dt;
        wobbleOffset += wobbleVel * dt;

        impulse = Mathf.MoveTowards(impulse, 0f, dt * 5f);

        // Saldırı durumu: hazırlıkta çömel, kararlıyken esne.
        float target = 0f;

        if (telegraph != null)
        {
            if (telegraph.IsCommitted)
                target = commitStretch;
            else if (telegraph.IsWarning)
                target = windupCrouch;
        }

        state = Mathf.MoveTowards(state, target, dt * 6f);

        // Eğim: hazırlıkta hedeften uzağa, vuruşta hedefe doğru.
        float dir = 1f;

        if (enemy != null && enemy.target != null)
            dir = enemy.target.position.x >= transform.position.x ? 1f : -1f;

        float leanTarget = 0f;

        if (telegraph != null)
        {
            if (telegraph.IsCommitted)
                leanTarget = -dir * leanForward;
            else if (telegraph.IsWarning)
                leanTarget = dir * leanBack;
        }

        lean = Mathf.MoveTowards(lean, leanTarget, dt * 120f);
        body.localRotation = Quaternion.Euler(0f, 0f, lean);

        float sq = Mathf.Clamp(impulse + state, -0.5f, 1f);

        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(ls.y));

        body.localScale = new Vector3(1f + sq * 0.14f, 1f - sq * 0.12f, 1f);

        body.localPosition = basePos + new Vector3(
            wobbleOffset.x / sx,
            wobbleOffset.y * 0.3f / sy,
            0f);
    }

    // ---------------------------------------------------------
    // Dikey kapsül sprite'ı (yumuşak kenar, koyu çerçeve, hafif gölge).
    // Ayakları (alt orta) pivot.
    // ---------------------------------------------------------

    private static Sprite BuildCapsule(float localW, float localH, Color fill)
    {
        localW = Mathf.Max(0.05f, localW);
        localH = Mathf.Max(localW, localH);

        const int texH = 256;
        int texW = Mathf.Max(24, Mathf.RoundToInt(texH * localW / localH));

        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float r = texW * 0.5f - 1f;
        float cx = texW * 0.5f;
        float y0 = r + 1f;
        float y1 = texH - r - 1f;

        Color outline = new Color(fill.r * 0.22f, fill.g * 0.22f, fill.b * 0.25f, 1f);
        Color shade = new Color(fill.r * 0.72f, fill.g * 0.72f, fill.b * 0.76f, 1f);
        Color light = Color.Lerp(fill, Color.white, 0.35f);

        Color[] px = new Color[texW * texH];

        float edge = Mathf.Max(3f, texW * 0.06f);

        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                float py = y + 0.5f;
                float qx = x + 0.5f - cx;
                float qy = py - Mathf.Clamp(py, y0, y1);
                float sd = Mathf.Sqrt(qx * qx + qy * qy) - r;

                float a = Mathf.Clamp01(0.5f - sd);

                if (a <= 0f)
                {
                    px[y * texW + x] = new Color(0, 0, 0, 0);
                    continue;
                }

                float inner = -sd; // kenardan içeri uzaklık
                Color c;

                if (inner < edge)
                {
                    c = outline;
                }
                else
                {
                    float vy = py / texH;
                    float vx = (x + 0.5f) / texW;

                    // Alta doğru gölge, sol üstte hafif ışık.
                    c = Color.Lerp(shade, fill, Mathf.SmoothStep(0f, 0.55f, vy));
                    c = Color.Lerp(c, light, Mathf.Clamp01((0.45f - vx) * 1.2f) * Mathf.Clamp01(vy - 0.3f) * 0.6f);
                }

                c.a = a;
                px[y * texW + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();

        float ppu = texH / localH;

        Sprite s = Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
        s.name = "Kapsül gövde";

        return s;
    }
}
