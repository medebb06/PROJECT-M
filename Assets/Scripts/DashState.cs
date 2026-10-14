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

    // Hız çanı: 3 = ortada ortalama hızın ~2.2 katı. Büyük = ortası daha keskin, başı/sonu daha yavaş.
    private const float DashBellPower = 3f;

    // Dash süresi çarpanı (mesafe sabit kalır, hız buna göre düşer). Animasyon köprüsü de bunu kullanır.
    public const float DurationScale = 2.2f;

    // Dash mesafesi çarpanı (dashDistance * bu).
    public const float DistanceScale = 1.5f;

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

        player.isDashing = true;
        player.canControl = false;
        player.isInvincible = true;
        player.isAttackLocked = true;

        timer = player.dashTime * DurationScale;

        dir = player.GetDashDirection();

        speed = player.dashDistance * DistanceScale / (player.dashTime * DurationScale);

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

        SpawnGhost();
    }

    public void Exit()
    {
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

        player.dashCooldownTimer = player.dashCooldown;

        // dash çıkışında momentum koru
        Vector2 vel = player.rb.linearVelocity;

        vel.x = dir * player.moveSpeed * 0.9f;

        player.SetVelocity(vel);

        player.StartCoroutine(UnlockAttack());
    }

    IEnumerator UnlockAttack()
    {
        yield return null;

        player.isAttackLocked = false;
    }

    public void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            if (player.isGrounded)
                sm.ChangeState(new GroundedState(player, sm));
            else
                sm.ChangeState(new AirState(player, sm));

            return;
        }

        HandleAfterImage();
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
        float n = DashBellPower;
        float norm = Gamma(2f * n + 2f) / (Gamma(n + 1f) * Gamma(n + 1f));
        float curve = norm * Mathf.Pow(p, n) * Mathf.Pow(1f - p, n);

        vel.x = dir * speed * curve;
        vel.y = 0f;

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

        fxTimer = player.afterImageSpacing;

        SpawnGhost();
    }

    // Eski sprite'ın (Ronni'den önceki çizim) dash hayaletleri: Ronni'ye geçildi, eski çizim görünmesin diye kapalı.
    public static bool oldSpriteAfterImages = false;

    void SpawnGhost()
    {
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