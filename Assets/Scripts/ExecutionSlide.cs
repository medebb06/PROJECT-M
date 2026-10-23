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
    public static float Duration = 0.65f;      // kayma süresi (sn, gerçek zaman) — uzun, süzülerek
    public static float PastEdge = 4.5f;       // düşman gövdesinin ARKA KENARININ bu kadar ötesinde durur (birim)
    public static float GhostGap = 0.05f;
    public static float HoldTime = 0.12f;   // kayma bittikten sonra da hedef alinamaz / hasar almaz kalir

    private PlayerController pl;
    private Rigidbody2D rb;
    private Collider2D[] ignored;
    private Collider2D[] myCols;
    private float x0, x1, age, ghostT;
    private bool finished;
    private bool fxEnded;
    private RigidbodyInterpolation2D savedInterp;
    private bool interpSaved;
    private static ExecutionSlide current;

    public static void Play(PlayerController player, Transform enemy, float dir)
    {
        // Infaz: keskin, beyaz-sicak isik cakmasi.
        if (enemy != null) GameplayLightFlash.Burst((Vector2)enemy.position + Vector2.up * 1.6f, new Color(1f, 0.93f, 0.8f), 3.4f, 12f, 0.45f);

        if (!Enabled || Active || player == null || enemy == null || player.rb == null)
            return;

        dir = dir >= 0f ? 1f : -1f;

        float ex = enemy.position.x;
        float px = player.rb.position.x;

        // düşman oyuncunun önünde değilse (zaten arkada) kayma yok
        if ((ex - px) * dir <= 0f)
            return;

        if (current != null) current.Finish();

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

        savedInterp = rb.interpolation;
        interpSaved = true;
        rb.interpolation = RigidbodyInterpolation2D.None;   // Update'ten konum yazarken interpolasyon titreme yapiyordu

        SlideCount++;
        finished = false;
        fxEnded = false;
        current = this;
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

        // Dash başladıysa slide hemen bırakır: iki sistem aynı pozisyonu çekiştirmesin (takılma).
        if (pl.isDashing)
        {
            Finish();
            return;
        }

        age += Time.unscaledDeltaTime;

        if (age > Duration + HoldTime)
        {
            Finish();
            return;
        }

        float k = Mathf.Clamp01(age / Mathf.Max(0.02f, Duration));
        float e = (1f - Mathf.Pow(1f - k, 2.6f)) * Mathf.SmoothStep(0.45f, 1f, Mathf.Clamp01(k / 0.14f));   // kısa ivmelenme, sonra uzun süzülerek yavaşlama

        if (k < 1f)
        {
            Vector2 np = new Vector2(Mathf.Lerp(x0, x1, e), rb.position.y);
            rb.position = np;
            pl.transform.position = new Vector3(np.x, pl.transform.position.y, pl.transform.position.z);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            ghostT -= Time.unscaledDeltaTime;

            if (ghostT <= 0f)
            {
                ghostT = GhostGap;
                DashFx.Ghost(pl);
            }
        }
        else if (Active)
        {
            // Kayma bitti: oyuncu ARTIK serbest (eskiden HoldTime boyunca pozisyona çivileniyordu,
            // dash / saldırı / hareket çalışmıyordu). Sadece dokunulmazlık kısa süre daha sürer.
            rb.position = new Vector2(x1, rb.position.y);
            pl.transform.position = new Vector3(x1, pl.transform.position.y, pl.transform.position.z);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            RestoreInterp();
            Active = false;

            if (!fxEnded)
            {
                fxEnded = true;
                DashFx.End(pl);
            }
        }
    }

    private void RestoreInterp()
    {
        if (interpSaved && rb != null) rb.interpolation = savedInterp;
        interpSaved = false;
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
        if (finished) return;
        finished = true;

        if (current == this) current = null;

        SetIgnore(false);
        RestoreInterp();

        if (pl != null)
        {
            ReleaseInvincible();

            if (!fxEnded && !pl.isDashing)
            {
                fxEnded = true;
                DashFx.End(pl);
            }
        }

        Active = false;
        Destroy(gameObject);
    }

// Eskiden slide basindaki isInvincible degeri geri yaziliyordu; getter dash/hasar korumasini da
    // icerdigi icin true geri yazilip kalici dokunulmazlik (düşman vuramiyor) bug'i çıkıyordu.
    // Artik sadece slide'in actigi state dokunulmazligi kapatilir (dash / ölüm devam ediyorsa dokunma).
    private void ReleaseInvincible()
    {
        if (pl == null) return;

        bool dead = pl.stateMachine != null && pl.stateMachine.CurrentState is PlayerDeathState;

        if (!pl.isDashing && !dead)
            pl.isInvincible = false;
    }


private void OnDestroy()
    {
        if (!finished)
        {
            finished = true;
            SetIgnore(false);
            ReleaseInvincible();
        }

        if (current == this) current = null;

        // Yeni bir slide başladıysa onun Active bayrağını ezme.
        if (current == null) Active = false;
    }
}
