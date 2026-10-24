using UnityEngine;
using System.Collections;

public class DashState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private float timer;
    private float dir;
    private float speed;
    private float fxTimer;
    private float total;
    private bool cancelIntent;
    private bool endedByCancel;

    // Hız çanı (eski değer: 3): 3 = ortada ortalama hızın ~2.2 katı. Büyük = ortası daha keskin, başı/sonu daha yavaş.
    private const float DashBellPower = 1f;   // artik kullanilmiyor (eski cani; geri almak icin birakildi)

    // Dash hiz dususu: buyudukce son kisim daha yavas (3 = hizli firlar, sonda cok yavaslar).
    private const float DashDecayPower = 3f;

    // Dash DOGAL bittiginde cikis hizi (moveSpeed carpani). Eskiden hep 0.9'du: dash yavaslayip
    // bitince kosu hizina ziplatiyordu. Iptal ile (ziplama/saldiri/parry) bitince 0.9 korunur.
    private const float DashExitSpeedNatural = 0.04f;
    private const float DashExitSpeedCancel = 0.9f;

    // Dash süresi çarpanı (mesafe sabit kalır, hız buna göre düşer). Animasyon köprüsü de bunu kullanır.
    public const float DurationScale = 2.5f;

    // Dash mesafesi çarpanı (dashDistance * bu).
    // Dash'ten sonra EN AZ bu kadar sn beklenir (Inspector'daki dashCooldown çok kısa; spam'i bu keser).
    public static float MinCooldown = 0.25f;

    public const float DistanceScale = 1.45f;

    // Havada tek dash: yere degene kadar ikinci dash yok (PlayerMovement.GroundCheck sifirlar).
    public static bool AirDashUsed = false;

    // Havada dash: dash'in son kisminda yere dogru suzulur (duz cizgi cizip havada kalmaz).
    public static float AirFallStart = 0.45f;   // dash'in bu orani gectikten sonra dusmeye baslar
    public static float AirFallSpeed = 13f;     // en sonda inis hizi (birim/sn)

    private int originalLayer;
    private GameObject colObject;
    private int colOriginalLayer;

    public DashState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter()
    {
        // Tamponu tüket (dash bitince tekrar tetiklenmesin).
        player.dashBufferTimer = 0f;
        player.dashPressed = false;

        // Dash HER ŞEYİ iptal eder: saldırı ve savunma.
        // (Saldırı Exit'i hızı sıfırladığı için dash hızından ÖNCE.)
        PlayerCombatController combat =
            player.GetComponent<PlayerCombatController>();

        if (combat != null)
            combat.CancelAttack();

        PlayerDefenseController defense =
            player.GetComponent<PlayerDefenseController>();

        if (defense != null)
            defense.CancelDefense();

        CombatEvents.RaisePlayerDash();

        float speedFactor = Mathf.InverseLerp(0f, 20f, player.rb.linearVelocity.magnitude);

        // FIX: audioPlayer atanmamışsa NullReferenceException atıyordu.
        if (player.audioPlayer != null)
            player.audioPlayer.PlayDash(speedFactor);

        if (!player.isGrounded) AirDashUsed = true;

        player.isDashing = true;
        player.canControl = false;
        player.isInvincible = true;
        player.isAttackLocked = true;

        timer = player.dashTime * DurationScale;
        total = timer;
        cancelIntent = false;
        endedByCancel = false;
        redirected = false;
        blendTimer = 0f;

        dir = player.GetDashDirection();
        player.dashPressDir = 0f;   // basış yönü kullanıldı

        speed = player.dashDistance * DistanceScale / (player.dashTime * DurationScale);
        float chainScale = MoveChain.Begin(player.dashTime * DurationScale);   // zincirde art arda hareket: mesafe azalır
        float planned = player.dashDistance * DistanceScale * chainScale;
        // Önde (mesafe içinde) düşman varsa dash onun İÇİNDEN geçip hemen ARKASINDA biter:
        // dönüp vurunca tam kılıç menzilinde olursun (çok ötede kalmaz, içinde sıkışıp fırlamaz).
        float landAt = PassLandDistance(planned);
        if (landAt < planned) { chainScale *= landAt / planned; planned = landAt; }
        float granted = MoveChain.Spend(planned, 3f);   // dash+saldırı spamında toplam mesafe bütçesi
        if (granted < planned) chainScale *= granted / planned;
        speed *= chainScale;

        // collision layer save
        originalLayer = player.gameObject.layer;

        // FIX: "Dash" layer'ı projede yoksa NameToLayer -1 döner
        // ve gameObject.layer = -1 hata verir.
        int dashLayer = LayerMask.NameToLayer("Dash");

        if (dashLayer >= 0)
        {
            player.gameObject.layer = dashLayer;

            // Gövde collider'ı çocuk objedeyse onun layer'ı da değişmeli,
            // yoksa dash sırasında düşmanlara çarpar.
            colObject = null;

            if (player.col != null && player.col.gameObject != player.gameObject)
            {
                colObject = player.col.gameObject;
                colOriginalLayer = colObject.layer;
                colObject.layer = dashLayer;
            }
        }
        else
        {
            Debug.LogWarning(
                "DashState: 'Dash' layer'ı bulunamadı! " +
                "Project Settings > Tags and Layers'tan ekle " +
                "ve Collision Matrix'i ayarla."
            );
        }

        // dash başlangıcında vertical velocity temizle
        Vector2 vel = player.rb.linearVelocity;
        vel.y = 0f;

        player.SetVelocity(vel);

        fxTimer = 0f;

        // Basta: ileri dogru UZAMA (squash & stretch) + ilk iz.
        DashFx.Begin(player);

        SpawnGhost();
    }

    // Düşmanın içinden geçip arkasında bitiş: düşmanın arka kenarının bu kadar ötesinde (oyuncu yarı genişliğine EK) durur.
    public static float PassGap = 1.0f;
    // Bu mesafeden (düşman merkezine) yakın düşmanlar için de aynı kural geçerli (çok yakınsa dash çok kısa kalmasın: en az bu kadar gider).
    public static float MinPassDistance = 3f;

    private float PassLandDistance(float planned)
    {
        float sgn = dir >= 0f ? 1f : -1f;
        Vector3 pp = player.transform.position;
        float half = player.col != null ? player.col.bounds.extents.x : 0.5f;

        EnemyBase best = null;
        float bestDx = float.MaxValue;
        for (int i = 0; i < EnemyBase.All.Count; i++)
        {
            EnemyBase e = EnemyBase.All[i];
            if (e == null || e.IsDead) continue;
            float dx = (e.transform.position.x - pp.x) * sgn;
            if (dx <= 0f || Mathf.Abs(e.transform.position.y - pp.y) > 3f) continue;
            if (dx < bestDx) { bestDx = dx; best = e; }
        }
        if (best == null) return planned;

        // düşmanın arka kenarı (dash yönünde en uzak collider ucu)
        float edge = bestDx;
        Collider2D[] cols = best.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || cols[i].isTrigger || !cols[i].enabled) continue;
            Bounds bb = cols[i].bounds;
            float far = (sgn > 0f ? bb.max.x - pp.x : pp.x - bb.min.x);
            if (far > edge) edge = far;
        }

        float land = Mathf.Max(MinPassDistance, edge + half + PassGap);
        return land < planned ? land : planned;   // içinden geçecek kadar mesafe yoksa dash normal gider
    }

    public void Exit()
    {
        ExecutionSlide.LastDashEnd = Time.unscaledTime;
        player.isDashing = false;
        player.canControl = true;
        player.isInvincible = false;

        // restore layer
        player.gameObject.layer = originalLayer;

        if (colObject != null)
        {
            colObject.layer = colOriginalLayer;
            colObject = null;
        }

        // Dash bir dusmanin (ozellikle genis boss govdesinin) icinde bittiyse fizigin yavas
        // itmesini bekleme: en yakin kenara hemen al.
        HitboxUtil.EjectFromEnemies(player);   // düşmanın içinde bittiyse EN YAKIN kenara çık (karşı tarafa fırlatma, dash mesafesi şişmesin)

        player.dashCooldownTimer = Mathf.Max(player.dashCooldown * player.dashCooldownScale, MinCooldown);

        // Sonda: durma ani SIKISMA (squash) + son iz.
        DashFx.End(player);

        // dash çıkışında momentum koru
        Vector2 vel = player.rb.linearVelocity;

        vel.x = dir * player.moveSpeed * (endedByCancel ? MoveChain.ExitSpeedMult : DashExitSpeedNatural);

        player.SetVelocity(vel);

        PlayerCombatController afterCombat = player.GetComponent<PlayerCombatController>();
        if (afterCombat != null)
            afterCombat.OnDashEnded();

        player.StartCoroutine(UnlockAttack());
    }

    IEnumerator UnlockAttack()
    {
        yield return null;

        player.isAttackLocked = false;
    }

    // Dash'in ilk bu kadarında ters yön tuşuna basılırsa dash anında o yöne döner (yön değiştirme tepkisi).
    private const float RedirectWindow = 0.7f;
    private bool redirected;
    private float blendTimer;   // yön değişiminden sonra hız yumuşak geçiş süresi

    public void Update()
    {
        timer -= Time.deltaTime;

        // Dash boyunca YÖN DEĞİŞMEZ; dash bitince oyuncu istediği yöne döner (eski ters yöne yönlendirme kaldırıldı).

        // Zıplama / saldırı / parry isteği dash sırasında hatırlanır; dash'in son kısmında
        // (yavaş kuyruk) varsa dash erken biter ve istek hemen çalışır.
        if (MobileInput.GetMouseButtonDown(0) || MobileInput.GetMouseButtonDown(1) ||
            player.Movement.jumpBufferCounter > 0f)
            cancelIntent = true;

        bool cancelNow =
            cancelIntent &&
            total > 0f &&
            (1f - timer / total) >= player.dashCancelPoint;

        if (timer <= 0f || cancelNow)
        {
            endedByCancel = cancelNow && timer > 0f;

            if (player.isGrounded)
                sm.ChangeState(new GroundedState(player, sm));
            else
                sm.ChangeState(new AirState(player, sm));

            return;
        }

        HandleAfterImage();
    }

    // Cok yumusak yavaslama: hiz egrisi smootherstep ile kapanir (basi ve sonu turevi sifir), mesafe sabit.
    private const float SmoothPower = 3.2f;
    private static float SmoothG(float p)
    {
        p = Mathf.Clamp01(p);
        float s5 = p * p * p * (p * (p * 6f - 15f) + 10f);
        float ramp = Mathf.SmoothStep(0f, 1f, p / 0.015f);
        return Mathf.Pow(1f - s5, SmoothPower) * ramp;
    }

    private static float smoothNorm;
    private static float SmoothNorm
    {
        get
        {
            if (smoothNorm <= 0f)
            {
                const int N = 400;
                float sum = 0f;
                for (int i = 0; i < N; i++) sum += SmoothG((i + 0.5f) / N);
                smoothNorm = sum / N;
            }
            return smoothNorm;
        }
    }

    static float Gamma(float x)
    {
        // Sadece tam sayı + 1 için (faktöriyel): Gamma(k+1) = k!
        float r = 1f;
        for (int k = 2; k < Mathf.RoundToInt(x); k++) r *= k;
        return r;
    }

    public void FixedUpdate()
    {
        // dash boyunca stabil velocity
        Vector2 vel = player.rb.linearVelocity;

        // Yavaş başla, ortada çok hız kazan, yavaşça dur:
        // v(p) = ortalama * (2n+1)!/(n!)^2 * p^n * (1-p)^n  (integral = dashDistance, mesafe sabit).
        // n büyüdükçe orta kısım daha keskin/hızlı, uçlar daha yavaş olur.
        float p = Mathf.Clamp01(1f - timer / Mathf.Max(0.0001f, player.dashTime * DurationScale));
        // Balon gibi: ilk anda hizli firlar, sona dogru cok yavaslar.
        // v(p) = ortalama * (m+1) * (1-p)^m  (integral = 1, mesafe sabit).
        // Basta ~%4'luk kisa yumusak giris sert sicramayi onler.
        float m = DashDecayPower;
        float curve = SmoothG(p) / SmoothNorm;

        float targetVx = dir * speed * curve;

        if (blendTimer > 0f)
        {
            // Yön değişiminde hız anında ters dönmesin: sıfırdan geçerek yumuşakça yeni yöne otursun (tek akıcı dönüş, iki ayrı dash değil).
            blendTimer -= Time.fixedDeltaTime;
            vel.x = Mathf.MoveTowards(vel.x, targetVx, speed * 22f * Time.fixedDeltaTime);
        }
        else
        {
            vel.x = targetVx;
        }

        // Havadaysa: ilk kisim duz, sonra yumusakca asagi suzul; hiz Exit'te korunur (dogal dusus).
        float fall = 0f;
        if (!player.isGrounded)
            fall = -AirFallSpeed * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(AirFallStart, 1f, p));
        vel.y = fall;

        player.SetVelocity(vel);
    }

    // =========================================================
    // AFTER IMAGE
    // =========================================================

    void HandleAfterImage()
    {
        fxTimer -= Time.deltaTime;

        if (fxTimer > 0f)
            return;

        fxTimer = Mathf.Min(Mathf.Max(0.02f, player.afterImageSpacing), DashFx.GhostSpacing);

        SpawnGhost();
    }

    // Eski sprite'ın (Ronni'den önceki çizim) dash hayaletleri: Ronni'ye geçildi, eski çizim görünmesin diye kapalı.
    public static bool oldSpriteAfterImages = false;

    void SpawnGhost()
    {
        // Ronni (mesh iskelet) icin iz: DashFx tum ciziminin anlik kopyasini birakir.
        DashFx.Ghost(player);

        if (!oldSpriteAfterImages)
            return;

        if (!player.afterImagePrefab || !player.playerSprite)
            return;

        GameObject obj = Object.Instantiate(
            player.afterImagePrefab,
            player.transform.position,
            Quaternion.identity
        );

        AfterImage ghost = obj.GetComponent<AfterImage>();

        if (ghost != null)
        {
            // FIX: Yön artık gerçekten kullanılan kaynaktan
            // (SpriteRenderer.flipX) okunuyor.
            bool flipX =
                player.playerSprite.flipX;

            Vector3 ghostScale =
                player.modelPivot != null
                    ? player.modelPivot.localScale
                    : Vector3.one;

            // Scale'i her zaman pozitif tut,
            // yön bilgisi flipX'te.
            ghostScale.x =
                Mathf.Abs(ghostScale.x);

            ghost.Init(
                player.playerSprite.sprite,
                ghostScale,
                flipX
            );
        }
    }
}