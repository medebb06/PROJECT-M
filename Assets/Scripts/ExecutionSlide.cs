using UnityEngine;

/// <summary>
/// INFAZ SÜZÜLMESİ: ikiye bölen infaz vuruşunda oyuncu, dash'le kesmiş gibi çok hızlı bir
/// kaymayla düşmanın içinden geçip arkasına çıkar. Dash'ten çok daha hızlı, daha kısa mesafeli.
/// Dünya donma (hit stop) ânında bile akar: gerçek zamanla (unscaled) ilerler.
/// </summary>
public class ExecutionSlide : MonoBehaviour
{
    public static bool Active;
    public static int SlideCount;

    public static bool Enabled = true;
    public static float Duration = 0.34f;      // kayma süresi (sn, gerçek zaman)
    public static float PastEdge = 1.0f;       // düşman gövdesinin ARKA KENARININ bu kadar ötesinde durur (birim)
    public static float GhostGap = 0.03f;
    public static float HoldTime = 0.25f;   // kayma bittikten sonra da hedef alinamaz / hasar almaz kalir

    private PlayerController pl;
    private Rigidbody2D rb;
    private Collider2D[] ignored;
    private Collider2D[] myCols;
    private float x0, x1, age, ghostT;
    private bool wasInvincible;

    public static void Play(PlayerController player, Transform enemy, float dir)
    {
        if (!Enabled || Active || player == null || enemy == null || player.rb == null)
            return;

        dir = dir >= 0f ? 1f : -1f;

        float ex = enemy.position.x;
        float px = player.rb.position.x;

        // düşman oyuncunun önünde değilse (zaten arkada) kayma yok
        if ((ex - px) * dir <= 0f)
            return;

        GameObject go = new GameObject("ExecutionSlide");
        ExecutionSlide s = go.AddComponent<ExecutionSlide>();
        s.Begin(player, enemy, dir, ex);
    }

    private void Begin(PlayerController player, Transform enemy, float dir, float targetX)
    {
        pl = player;
        rb = player.rb;
        x0 = rb.position.x;

        // hedef: düşman collider'larının arka kenarı + oyuncu yarı genişliği + pay (içinde bitip geri itilmesin)
        float edge = targetX;
        Collider2D[] ecs = enemy.GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < ecs.Length; i++)
        {
            if (ecs[i] == null || ecs[i].isTrigger) continue;
            Bounds b = ecs[i].bounds;
            float e = dir > 0f ? b.max.x : b.min.x;
            if ((e - edge) * dir > 0f) edge = e;
        }

        float half = 0.8f;
        Collider2D mine = player.GetComponent<Collider2D>();
        if (mine != null) half = mine.bounds.extents.x;

        x1 = edge + dir * (half + PastEdge);

        // oyuncunun tum govde collider'lari (pl.col atanmamis olabilir)
        myCols = pl.GetComponentsInChildren<Collider2D>();

        // duvar varsa onun önünde dur
        Collider2D col = pl.col != null ? pl.col : pl.GetComponent<Collider2D>();

        if (col != null && pl.Movement != null)
        {
            ContactFilter2D f = new ContactFilter2D();
            f.SetLayerMask(pl.Movement.wallMask);
            f.useTriggers = false;
            RaycastHit2D[] hits = new RaycastHit2D[6];
            float dist = Mathf.Abs(x1 - x0);
            int n = col.Cast(new Vector2(dir, 0f), f, hits, dist + 0.1f);
            float allowed = dist;

            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider != null && hits[i].distance < allowed)
                    allowed = Mathf.Max(0f, hits[i].distance - 0.05f);
            }

            x1 = x0 + dir * allowed;
        }

        // düşman gövdesinin içinden geç
        ignored = enemy.GetComponentsInChildren<Collider2D>();

        SetIgnore(true);

        SlideCount++;
        wasInvincible = pl.isInvincible;
        pl.isInvincible = true;
        Active = true;

        DashFx.Begin(pl);
        DashFx.Ghost(pl);
    }

    private void Update()
    {
        if (pl == null || rb == null)
        {
            Finish();
            return;
        }

        age += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(age / Mathf.Max(0.02f, Duration));
        float e = 1f - Mathf.Pow(1f - k, 5f);   // hizli baslar, cok uzun ve yumusak durur
        if (age > Duration + HoldTime) { Finish(); return; }

        if (k < 1f) rb.position = new Vector2(Mathf.Lerp(x0, x1, e), rb.position.y);
        else rb.position = new Vector2(x1, rb.position.y);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        ghostT -= Time.unscaledDeltaTime;

        if (k < 1f && ghostT <= 0f)
        {
            ghostT = GhostGap;
            DashFx.Ghost(pl);
        }


    }

    private void SetIgnore(bool on)
    {
        if (ignored == null || myCols == null) return;

        for (int a = 0; a < myCols.Length; a++)
        {
            if (myCols[a] == null) continue;

            for (int b = 0; b < ignored.Length; b++)
            {
                if (ignored[b] != null && ignored[b] != myCols[a])
                    Physics2D.IgnoreCollision(myCols[a], ignored[b], on);
            }
        }
    }

    private void Finish()
    {
        SetIgnore(false);

        if (pl != null)
        {
            pl.isInvincible = wasInvincible;
            DashFx.End(pl);
        }

        Active = false;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        Active = false;
    }
}
