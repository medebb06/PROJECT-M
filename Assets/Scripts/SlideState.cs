using UnityEngine;

/// <summary>
/// EĞİMDE KAYMA durumu. Ayarlar SlopeSlide bileşeninde.
///
///  - Eğimin inişi yönünde, yavaş başlayıp hızlanarak kayar (en fazla maxSpeed).
///  - Yerçekimi kapatılır, zemine doğru küçük bir sabit hızla çekilir (mıknatıs) ve hız zemin boyunca verilir:
///    yüzeyi izler, tepeden taşınca da havaya fırlamaz. Zeminden gerçekten kopunca (uçurum) hız korunarak havaya geçer.
///  - Kayarken sürtünme sıfırlanır (yoksa yüzey yavaşlatırdı).
///  - Sadece zıplama (Space) ile çıkılır (PlayerController.HandleJump); dash kapalı. Zıplamada yatay hız korunur.
///  - Eğim bitince (düz zemin) sürtünmeyle sönerek koşuya geri döner.
/// </summary>
public class SlideState : IPlayerState
{
    /// <summary>Şu an kayılıyor mu (zemin kontrolü, ses vb. okur).</summary>
    public static bool Active;

    /// <summary>Kaymanın en son bırakıldığı an (hemen tekrar yapışmasın diye).</summary>
    public static float LastExitTime = -99f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = false;
        LastExitTime = -99f;
    }

    private static PhysicsMaterial2D noFriction;

    private readonly PlayerController player;
    private readonly PlayerStateMachine sm;
    private readonly SlopeSlide cfg;

    private float speed;
    private float dirX = 1f;          // kaymanın yatay yönü (+1 sağ / -1 sol)
    private float prevGravity;
    private PhysicsMaterial2D prevMaterial;
    private bool leaving;

    public SlideState(PlayerController player, PlayerStateMachine sm, SlopeSlide cfg)
    {
        this.player = player;
        this.sm = sm;
        this.cfg = cfg;
    }

    // Yüzey normalinden "saat yönünde" teğet: normal yukarıyken (1,0).
    private static Vector2 Tangent(Vector2 n)
    {
        return new Vector2(n.y, -n.x);
    }

    // Yüzeyin iniş yönü (aşağı bakan teğet).
    private static Vector2 Downhill(Vector2 n)
    {
        Vector2 t = Tangent(n);
        return t.y <= 0f ? t : -t;
    }

    public void Enter()
    {
        Active = true;
        leaving = false;

        player.jumpConsumed = false;
        player.isGrounded = true;

        Rigidbody2D rb = player.rb;

        // Yerçekimini kapat: mıknatıs + teğet hızı tek başına yönetir.
        prevGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // Sürtünmeyi sıfırla (yüzeye bastırırken sürtünme kaymayı yavaşlatırdı).
        if (player.col != null)
        {
            prevMaterial = player.col.sharedMaterial;

            if (noFriction == null)
            {
                noFriction = new PhysicsMaterial2D("SlideNoFriction");
                noFriction.friction = 0f;
                noFriction.bounciness = 0f;
            }

            player.col.sharedMaterial = noFriction;
        }

        Vector2 n = Vector2.up;
        RaycastHit2D hit;

        if (SlopeSlide.Probe(player, cfg.stickDistance, out hit))
            n = hit.normal;

        Vector2 down = Downhill(n);
        dirX = Mathf.Abs(down.x) > 0.01f ? Mathf.Sign(down.x) : (player.facingDir >= 0f ? 1f : -1f);

        // Koşarak / zıplayarak girdiysen YATAY hızın korunur; durarak ya da düz aşağı düşerek girdiysen yavaş başlar.
        float along = Vector2.Dot(new Vector2(rb.linearVelocity.x, 0f), down);
        speed = Mathf.Clamp(Mathf.Max(cfg.startSpeed, along), cfg.startSpeed, cfg.maxSpeed);

        rb.linearVelocity = down * speed - n * cfg.stickSpeed;

        Face();
    }

    public void Exit()
    {
        Active = false;
        LastExitTime = Time.time;

        player.rb.gravityScale = prevGravity;

        if (player.col != null)
            player.col.sharedMaterial = prevMaterial;

        // Zıplayınca / kopunca yatay hız hemen sönmesin (PlayerMovement hava hareketi okur).
        player.slideCarryTimer = cfg.airCarryTime;
    }

    private void Face()
    {
        if (player.attackFacingLocked)
            return;

        player.facingDir = dirX;

        if (player.playerSprite != null)
            player.playerSprite.flipX = dirX < 0f;
    }

    private void Leave(bool toGround)
    {
        if (leaving)
            return;

        leaving = true;

        if (toGround)
            sm.ChangeState(new GroundedState(player, sm));
        else
            sm.ChangeState(new AirState(player, sm));
    }

    public void Update()
    {
        // Kontrol kilitlendiyse (sinematik vb.) kaymayı bırak.
        if (!player.canControl)
        {
            Leave(true);
            return;
        }

        // DASH KAPALI: kayarken dash atılamaz (basış tamponu da silinir; kayma bitince eski basış tetiklenmesin).
        player.dashPressed = false;
        player.dashBufferTimer = 0f;

        // Savunmaya (blok / parry) geçince kayma bırakılır.
        if (player.IsDefending())
        {
            Leave(true);
            return;
        }

        // Zıplama PlayerController.HandleJump'ta başlar (yerdeyiz: isGrounded sürekli true).

        Face();
    }

    public void FixedUpdate()
    {
        if (leaving)
            return;

        float dt = Time.fixedDeltaTime;
        Rigidbody2D rb = player.rb;

        RaycastHit2D hit;

        // Yüzeyden koptu (uçurum / tepe): hız korunarak havaya geç.
        if (!SlopeSlide.Probe(player, cfg.stickDistance, out hit))
        {
            Leave(false);
            return;
        }

        Vector2 n = hit.normal;
        float angle = Vector2.Angle(n, Vector2.up);

        // Duvara dönüştü: kayma biter.
        if (angle > cfg.maxSlideAngle)
        {
            Leave(false);
            return;
        }

        bool sloped = angle >= cfg.flatAngle;

        Vector2 dir;

        if (sloped)
        {
            Vector2 down = Downhill(n);
            float downSign = Mathf.Abs(down.x) > 0.01f ? Mathf.Sign(down.x) : dirX;

            // Dikliğe göre hızlanma: 45° tam, yatay eğimde yarıya kadar iner, dikte biraz artar.
            float accel = cfg.acceleration * Mathf.Clamp(Mathf.Sin(angle * Mathf.Deg2Rad) / 0.7071f, 0.5f, 1.3f);

            if (downSign == dirX)
            {
                // İNİŞ: hızlan. Tuşa ters basmak yavaşlatmaz, geri tırmandırmaz.
                speed = Mathf.MoveTowards(speed, cfg.maxSpeed, accel * dt);

                dir = down;
            }
            else
            {
                // YOKUŞ YUKARI (önde eğim yükseliyor): yavaşla, durunca geri kay.
                speed -= accel * cfg.uphillDecelMultiplier * dt;
                dir = -down;

                if (speed <= 0.05f)
                {
                    dirX = downSign;
                    speed = 0.05f;
                    dir = down;
                }
            }
        }
        else
        {
            // DÜZ ZEMİN: sürtünmeyle sön, koşu hızına inince normal harekete dön.
            Vector2 t = Tangent(n);
            dir = Mathf.Sign(t.x) == dirX ? t : -t;

            float friction = cfg.runOutFriction;
            speed = Mathf.MoveTowards(speed, 0f, friction * dt);

            if (speed <= player.moveSpeed * cfg.endSpeedRatio)
            {
                rb.linearVelocity = dir * speed;
                Leave(true);
                return;
            }
        }

        // Teğet hız + yüzeye doğru küçük çekim (mıknatıs).
        rb.linearVelocity = dir * speed - n * cfg.stickSpeed;

        // Eğimde OverlapCircle zemin kontrolü kaçırabilir; kayarken zemin her zaman "temas var".
        player.isGrounded = true;
    }
}
