using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private PlayerController player;

    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 45f;
    public float deceleration = 60f;
    public float airControl = 0.6f;

    // =========================================================
    // JUMP
    // =========================================================

    [Header("Jump")]
    public float jumpForce = 18f;
    [Tooltip("Sahnedeki eski jumpForce degerini ezer. 0 = Inspector degerini kullan.")]
    public float jumpForceOverride = 18f;

    [Header("Jump Feel")]
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.12f;
    public float jumpCutMultiplier = 2f;
    public float jumpCutVelocityMultiplier = 0.3f;
    public float fallMultiplier = 2.5f;
    [Tooltip("Yukselirken ek yercekimi: yuksek = hizli cikar, tepeye cabuk ulasir (seri / responsive).")]
    public float riseMultiplier = 2.2f;
    public float maxFallSpeed = 20f;

    // =========================================================
    // JUMP v2  (sifirdan: yukseklik + sure ile tanimlanir)
    // Eski jumpForce / riseMultiplier / fallMultiplier / jumpCut* /
    // maxFallSpeed alanlari geriye uyumluluk icin duruyor ama artik KULLANILMIYOR.
    // =========================================================

    [Header("Jump v2")]
    [Tooltip("Tek zıplamanın çıkacağı yükseklik (dünya birimi).")]
    public float jumpHeight = 8f;
    [Tooltip("Tepe noktasına çıkış süresi (sn). Küçük = daha çevik / sert zıplama.")]
    public float timeToApex = 0.36f;
    [Tooltip("Düşüş yerçekimi = yükseliş yerçekimi x bu oran (büyük = hızlı iner).")]
    public float fallGravityRatio = 1.5f;
    [Tooltip("Space bırakılınca yükseliş yerçekimi x bu oran (değişken zıplama).")]
    public float jumpCutGravityRatio = 3f;
    [Tooltip("Zıplamadan sonra bu kadar sn Space bırakılsa da zıplama kesilmez (kısa tık da düzgün zıplatır).")]
    public float minJumpHoldTime = 0.1f;
    [Tooltip("En yüksek düşüş hızı.")]
    public float terminalVelocity = 55f;

    [Header("Air Control v2")]
    [Tooltip("Havada hızlanma (birim/sn²). Büyük = anında tepki.")]
    public float airAcceleration = 260f;
    [Tooltip("Havada tuş bırakılınca yavaşlama.")]
    public float airDeceleration = 180f;
    [Tooltip("Havada ters yöne basınca hızlanma çarpanı (çevik dönüş).")]
    public float airTurnMultiplier = 1.5f;

    [Header("Wall v2")]
    [Tooltip("Duvar algılama: oyuncu kenarının ötesine bu kadar bakılır.")]
    public float wallGrabMargin = 0.45f;
    [Tooltip("Duvarda kayma (düşüş) hızı. Küçük = duvara yapışık gibi.")]
    public float wallSlideFallSpeed = 3f;
    [Tooltip("Duvar zıplamasında yatay hız.")]
    public float wallJumpSpeedX = 24f;
    [Tooltip("Duvar zıplamasının yüksekliği = jumpHeight x bu oran.")]
    public float wallJumpHeightRatio = 1f;
    [Tooltip("Duvar zıplamasında yatay hızın sabit kaldığı süre (sn).")]
    public float wallJumpLockTime = 0.12f;
    [Tooltip("Sabit süreden sonra hava kontrolüne geçiş süresi (sn).")]
    public float wallJumpBlendTime = 0.1f;

    [Header("Ledge Climb")]
    [Tooltip("Kenar tırmanma açık mı.")]
    public bool ledgeClimbEnabled = true;
    [Tooltip("Tırmanmanın toplam süresi (sn). Küçük = daha çevik.")]
    public float ledgeClimbTime = 0.2f;
    [Tooltip("Kafanın ne kadar üstündeki kenar yakalanır (gövde boyunun oranı).")]
    public float ledgeReachAbove = 0f;
    [Tooltip("Kenar ayaktan en az bu kadar yukarıda olmalı (gövde boyunun oranı). Alçak basamaklar normal zıplamayla geçilir.")]
    public float ledgeMinAboveFeet = 0.8f;
    [Tooltip("Tırmanma bitince yöne basılıysa koşu hızının bu oranıyla devam eder.")]
    public float ledgeExitSpeedRatio = 0.7f;
    [Tooltip("Tırmanmadan sonra tekrar kenar yakalamadan önce bekleme (sn).")]
    public float ledgeRegrabCooldown = 0.2f;
    [HideInInspector] public float ledgeCooldownTimer;

    // Calisma zamani
    [HideInInspector] public bool jumpRising;
    [HideInInspector] public float jumpCutLockTimer;

    // =========================================================
    // WALL
    // =========================================================

    [Header("Wall")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.3f;
    public LayerMask wallMask;

    [Header("Wall Slide")]
    public float wallSlideSpeed = 2f;

    [Header("Wall Jump")]
    public float wallJumpForceX = 9f;
    public float wallJumpForceY = 12f;
    public float wallJumpControlLock = 0.08f;
    public float wallJumpBufferTime = 0.12f;
    public float wallJumpControlBlendTime = 0.18f;
    public float wallJumpControlAccelerationMultiplier = 1f;

    // =========================================================
    // GROUND CHECK
    // =========================================================

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundMask;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    [HideInInspector] public float jumpBufferCounter;
    [HideInInspector] public float coyoteCounter;

    [HideInInspector] public float wallJumpBufferCounter;
    [HideInInspector] public int wallDirection;
    [HideInInspector] public bool isWallSliding;
    [HideInInspector] public int wallJumpBlockedDirection;

    private bool wasGrounded;
    private bool wasAirborne;

    private float airTime;
    private float maxAirHeight;

    // =========================================================
    // INITIALIZE
    // =========================================================

    public void Initialize(PlayerController controller)
    {
        player = controller;

        RecalculateJump();
    }

    // Zıplama hızı yükseklik ve sürenin sonucudur: v0 = 2h / t
    public void RecalculateJump()
    {
        float t = Mathf.Max(0.05f, timeToApex);
        jumpForce = 2f * Mathf.Max(0.1f, jumpHeight) / t;
    }

    // Yükseliş yerçekimi: g = 2h / t²
    public float RiseGravity
    {
        get
        {
            float t = Mathf.Max(0.05f, timeToApex);
            return 2f * Mathf.Max(0.1f, jumpHeight) / (t * t);
        }
    }

    // =========================================================
    // UPDATE TIMERS
    // =========================================================

    public void UpdateTimers()
    {
        TickParryBurst();

        jumpBufferCounter =
            Mathf.Max(
                0f,
                jumpBufferCounter - Time.deltaTime
            );

        wallJumpBufferCounter =
            Mathf.Max(
                0f,
                wallJumpBufferCounter - Time.deltaTime
            );

        ledgeCooldownTimer =
            Mathf.Max(
                0f,
                ledgeCooldownTimer - Time.deltaTime
            );

        // FIX: Eskiden coyoteCounter hiç azalmıyordu.
        // Bu yüzden platformdan yürüyerek düşünce havada,
        // istediğin zaman bir kez zıplayabiliyordun.
        //
        // Artık: yerdeyken sürekli dolu, havadayken geri sayar.
        if (
            player != null &&
            player.isGrounded
        )
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter =
                Mathf.Max(
                    0f,
                    coyoteCounter - Time.deltaTime
                );
        }
    }

    // =========================================================
    // FIXED UPDATE
    // =========================================================

    public void FixedUpdateMovement()
    {
        GroundCheck();

        if (!player.isGrounded)
        {
            wasAirborne = true;

            airTime +=
                Time.fixedDeltaTime;

            if (
                player.transform.position.y >
                maxAirHeight
            )
            {
                maxAirHeight =
                    player.transform.position.y;
            }
        }

        ApplyBetterGravity();
    }

    // =========================================================
    // HORIZONTAL MOVEMENT
    // =========================================================

    // =========================================================
    // PARRY SONRASI ATILMA: parry'den sonra ilk yon tusu kisa, hizli bir atilma verir.
    // =========================================================

    [Header("Parry Sonrasi Atilma")]
    [Tooltip("Basarili parry'den sonra bu sure (sn) icinde ilk yon basisi atilma verir.")]
    public float parryBurstWindow = 0.6f;
    [Tooltip("Atilmanin baslangic hizi = kosu hizi x bu carpan (dash 10 birim / 0.15 sn).")]
    public float parryBurstSpeedMult = 4f;
    [Tooltip("Atilmanin suresi (sn); hiz bu surede kosu hizina iner.")]
    public float parryBurstTime = 0.16f;
    [Tooltip("Atılmanın TOPLAM mesafesi (birim). Hız zirvesi bundan hesaplanır: her seferinde aynı adım (zincirde MoveChain ile azalır). Eski: ~6.7.")]
    public float parryStepDistance = 4.5f;
    private float burstPeakSpeed;

    /// <summary>Başka bir hareket (infaz kayması) başlarsa atılma kesilir; hareketler toplanmaz.</summary>
    public void CancelParryBurst()
    {
        parryBurstArmedUntil = -1f;
        parryBurstEnd = -1f;
        EndParryBurstFx();
    }

    private float parryBurstArmedUntil = -1f;
    private float parryBurstEnd = -1f;
    private float parryBurstDir = 1f;
    private bool parryBurstActive;
    private int burstOrigLayer, burstColOrigLayer;
    private GameObject burstColObject;
    private float burstGhostTimer;

    // Atilma basliyor: dash gibi 'Dash' katmanina gecer (dusmanlarin icinden gecilir) ve iz birakir.
    private void BeginParryBurstFx()
    {
        parryBurstActive = true;
        burstGhostTimer = 0f;
        burstOrigLayer = player.gameObject.layer;
        burstColObject = null;

        int dashLayer = LayerMask.NameToLayer("Dash");
        if (dashLayer >= 0)
        {
            player.gameObject.layer = dashLayer;

            if (player.col != null && player.col.gameObject != player.gameObject)
            {
                burstColObject = player.col.gameObject;
                burstColOrigLayer = burstColObject.layer;
                burstColObject.layer = dashLayer;
            }
        }

        DashFx.Begin(player);
        DashFx.Ghost(player);
    }

    private void EndParryBurstFx()
    {
        if (!parryBurstActive)
            return;

        parryBurstActive = false;
        parryBurstEnd = -1f;
        player.gameObject.layer = burstOrigLayer;

        if (burstColObject != null)
        {
            burstColObject.layer = burstColOrigLayer;
            burstColObject = null;
        }

        // Atilma bir dusmanin icinde bittiyse en yakin kenara al.
        HitboxUtil.EjectFromEnemies(player);
        DashFx.End(player);
    }

    // Her karede: atilma suresi bitince / dash vb. girince katmani geri verir, iz birakir.
    private void TickParryBurst()
    {
        if (!parryBurstActive)
            return;

        if (Time.time >= parryBurstEnd || player.isDashing || !player.canControl)
        {
            EndParryBurstFx();
            return;
        }

        burstGhostTimer -= Time.deltaTime;
        if (burstGhostTimer <= 0f)
        {
            burstGhostTimer = DashFx.GhostSpacing;
            DashFx.Ghost(player);
        }
    }

    public void ArmParryBurst()
    {
        parryBurstArmedUntil = Time.time + parryBurstWindow;
    }

    public void DisarmParryBurst()
    {
        parryBurstArmedUntil = -1f;
    }

    // true = bu fizik adiminda hiz atilmaya ait, normal hareket calismasin.
    private bool UpdateParryBurst()
    {
        if (Time.time < parryBurstEnd)
        {
            float t = 1f - (parryBurstEnd - Time.time) / Mathf.Max(0.01f, parryBurstTime);
            float speed = Mathf.Lerp(burstPeakSpeed, moveSpeed * MoveChain.ExitSpeedMult, t * t);
            player.rb.linearVelocity = new Vector2(parryBurstDir * speed, player.rb.linearVelocity.y);
            return true;
        }

        if (Time.time < parryBurstArmedUntil &&
            player.isGrounded &&
            !ExecutionSlide.Active &&
            Mathf.Abs(player.moveInput) > 0.1f)
        {
            parryBurstArmedUntil = -1f;

            // Sabit adım: zirve hızı, hedef mesafeyi ve çıkış hızını verecek şekilde hesaplanır.
            // Hız profili: Lerp(zirve, çıkış, t^2)  ->  mesafe = T * (zirve*2/3 + çıkış/3)
            float exitV = moveSpeed * MoveChain.ExitSpeedMult;
            float stepD = parryStepDistance * MoveChain.Begin(parryBurstTime);
            burstPeakSpeed = Mathf.Max(exitV, (stepD / Mathf.Max(0.01f, parryBurstTime) - exitV / 3f) * 1.5f);
            parryBurstDir = Mathf.Sign(player.moveInput);
            parryBurstEnd = Time.time + parryBurstTime;
            BeginParryBurstFx();
            player.SpawnDust();
            player.rb.linearVelocity = new Vector2(parryBurstDir * burstPeakSpeed, player.rb.linearVelocity.y);
            return true;
        }

        return false;
    }

    public void ApplyMovement(float control)
    {
        if (!player.canControl)
            return;

        if (player.inputLocked)
            return;

        // Yerde saldirirken yatay hareketi AttackState'in kaymasi yonetir. Burada hiz birikirse
        // (kosarken saldiri) fizik her adimda pozisyonu oynatip karakter titrer / vurus hizlanir.
        if (player.attackFacingLocked && player.isGrounded)
        {
            float runVx = AttackState.RunWhileAttacking ? player.moveInput * moveSpeed * AttackState.RunSpeedMult : 0f;
            player.rb.linearVelocity = new Vector2(runVx, player.rb.linearVelocity.y);
            return;
        }

        if (UpdateParryBurst())
            return;

        float targetSpeed =
            player.moveInput *
            moveSpeed;

        bool moving =
            Mathf.Abs(
                player.moveInput
            ) > 0.01f;

        float accelerationRate;

        if (player.isGrounded)
        {
            accelerationRate =
                (moving ? acceleration : deceleration) * control;
        }
        else
        {
            // HAVA KONTROLU v2: yerdekinden bagimsiz, cok daha keskin.
            accelerationRate =
                moving ? airAcceleration : airDeceleration;

            float vx = player.rb.linearVelocity.x;

            // Ters yone basinca hizli don.
            if (moving && vx * targetSpeed < 0f)
                accelerationRate *= airTurnMultiplier;

            // control = airControl -> tam; (duvar zıplaması 0..airControl arası geçiş yapar)
            accelerationRate *= Mathf.Clamp01(control / Mathf.Max(0.01f, airControl));
        }

        float newVelocityX =
            Mathf.MoveTowards(
                player.rb.linearVelocity.x,
                targetSpeed,
                accelerationRate *
                Time.fixedDeltaTime
            );

        player.rb.linearVelocity =
            new Vector2(
                newVelocityX,
                player.rb.linearVelocity.y
            );
    }

    // =========================================================
    // VELOCITY
    // =========================================================

    public void SetVelocity(Vector2 velocity)
    {
        if (player == null)
            return;

        if (player.rb == null)
            return;

        player.rb.linearVelocity =
            velocity;
    }

    // =========================================================
    // GRAVITY / JUMP CUT
    // =========================================================

    private void ApplyBetterGravity()
    {
        if (!player.canControl)
            return;

        if (player.slamGroundLock)
            return;

        // ZIPLAMA v2: yercekimi, istenen yukseklik/sureden hesaplanir.
        //   yukselis  : g = 2h / t²
        //   dusus     : g x fallGravityRatio
        //   Space birak: g x jumpCutGravityRatio (sadece zıplama yukselirken)
        float baseG =
            Mathf.Abs(Physics2D.gravity.y) *
            player.rb.gravityScale;

        // Yercekimi kapaliysa (dash / slam vb.) dokunma.
        if (baseG < 0.01f)
            return;

        float vy = player.rb.linearVelocity.y;

        if (jumpCutLockTimer > 0f)
            jumpCutLockTimer -= Time.fixedDeltaTime;

        if (vy <= 0f)
            jumpRising = false;

        // Duvarda kayarken hizi WallSlideState sinirliyor; ek yercekimi ekleme.
        if (isWallSliding)
            return;

        float riseG = RiseGravity;
        float targetG;

        if (vy < 0f)
        {
            targetG = riseG * fallGravityRatio;
        }
        else if (vy > 0f)
        {
            bool cut =
                jumpRising &&
                !player.jumpHeld &&
                jumpCutLockTimer <= 0f;

            targetG = cut ? riseG * jumpCutGravityRatio : riseG;
        }
        else
        {
            return;
        }

        // Hedef yercekimi ile temel yercekimi arasindaki fark kadar ek ivme.
        player.rb.linearVelocity +=
            Vector2.down *
            (targetG - baseG) *
            Time.fixedDeltaTime;

        if (
            player.rb.linearVelocity.y <
            -terminalVelocity
        )
        {
            player.rb.linearVelocity =
                new Vector2(
                    player.rb.linearVelocity.x,
                    -terminalVelocity
                );
        }
    }

    // =========================================================
    // GROUND CHECK
    // =========================================================

    private void GroundCheck()
    {
        if (player.slamGroundLock)
        {
            player.isGrounded = true;
            return;
        }

        if (groundCheck == null)
        {
            player.isGrounded = false;
            return;
        }

        bool groundedNow =
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundMask
            );

        // =====================================================
        // LAND
        // =====================================================

        if (
            groundedNow &&
            !wasGrounded
        )
        {
            if (wasAirborne)
            {
                player.OnPlayerLand();

                wasAirborne = false;
            }

            airTime = 0f;

            maxAirHeight =
                player.transform.position.y;
        }

        // =====================================================
        // COYOTE TIME
        // =====================================================

        if (
            !groundedNow &&
            wasGrounded
        )
        {
            coyoteCounter =
                coyoteTime;

            wasAirborne = true;
        }

        if (groundedNow) DashState.AirDashUsed = false;

        wasGrounded =
            groundedNow;

        player.isGrounded =
            groundedNow;
    }

    // =========================================================
    // WALL CHECK
    // =========================================================

    private Collider2D bodyCollider;

    private Collider2D GetBodyCollider()
    {
        if (bodyCollider != null)
            return bodyCollider;

        Collider2D[] cols =
            GetComponents<Collider2D>();

        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && !cols[i].isTrigger)
            {
                bodyCollider = cols[i];
                break;
            }
        }

        return bodyCollider;
    }

    // WALL CHECK v2: oyuncunun gercek govdesinin kenarindan, uc yukseklikte
    // (alt / orta / ust) bakar. Olcekten bagimsiz; duvara dokunmasa bile
    // wallGrabMargin kadar yaklasinca yakalar.
    public bool IsTouchingWall()
    {
        Collider2D col = GetBodyCollider();

        if (col == null)
        {
            wallDirection = 0;
            return false;
        }

        Bounds b = col.bounds;

        float reach =
            b.extents.x + Mathf.Max(0.05f, wallGrabMargin);

        float[] heights =
            new float[]
            {
                b.center.y,
                b.center.y + b.extents.y * 0.6f,
                b.center.y - b.extents.y * 0.6f
            };

        int rightHits = 0;
        int leftHits = 0;

        for (int i = 0; i < heights.Length; i++)
        {
            Vector2 origin =
                new Vector2(b.center.x, heights[i]);

            if (
                Physics2D.Raycast(
                    origin,
                    Vector2.right,
                    reach,
                    wallMask
                ).collider != null
            )
                rightHits++;

            if (
                Physics2D.Raycast(
                    origin,
                    Vector2.left,
                    reach,
                    wallMask
                ).collider != null
            )
                leftHits++;
        }

        // En az iki yukseklikte (govde duvara gercekten yakin) temas gerekir.
        if (rightHits >= 2 && rightHits >= leftHits)
        {
            wallDirection = 1;
            return true;
        }

        if (leftHits >= 2)
        {
            wallDirection = -1;
            return true;
        }

        wallDirection = 0;

        return false;
    }

    // =========================================================
    // LEDGE CHECK
    // =========================================================

    // dir yönünde (1 sağ, -1 sol) tırmanılabilir bir kenar var mı?
    // Varsa standPos = tırmanma bitince oyuncunun (transform) duracağı yer.
    //  1) Önde, ayağa yakın yükseklikte duvar olmalı.
    //  2) Duvarın biraz içinden yukarıdan aşağı bakılır: düz bir üst yüzey
    //     ayak +ledgeMinAboveFeet ile kafa +ledgeReachAbove arasında olmalı.
    //  3) Kenarın üstünde ve yukarı çıkış yolunda gövde sığmalı.
    public bool TryGetLedge(int dir, out Vector2 standPos)
    {
        standPos = Vector2.zero;

        if (!ledgeClimbEnabled || dir == 0 || ledgeCooldownTimer > 0f)
            return false;

        Collider2D col = GetBodyCollider();

        if (col == null)
            return false;

        Bounds b = col.bounds;
        float h = b.size.y;
        Vector2 forward = dir > 0 ? Vector2.right : Vector2.left;
        // Kenar yakalama icin gövde duvara gercekten yakin olmali (wall slide'dan dar).
        float reach = b.extents.x + 0.08f;

        // 1) Duvar (govdenin ust yarisinda: el hizasi)
        RaycastHit2D wall =
            Physics2D.Raycast(
                new Vector2(b.center.x, b.center.y + h * 0.25f),
                forward,
                reach,
                wallMask
            );

        if (wall.collider == null || wall.distance <= 0f)
            return false;

        if (Mathf.Abs(wall.normal.x) < 0.7f)
            return false;

        float wallX = wall.point.x;

        // 2) Kenarın üst yüzeyi
        float probeX = wallX + dir * 0.3f;
        float probeTop = b.max.y + h * Mathf.Max(0f, ledgeReachAbove);
        float lowest = b.min.y + h * Mathf.Max(0f, ledgeMinAboveFeet);

        if (probeTop <= lowest)
            return false;

        RaycastHit2D top =
            Physics2D.Raycast(
                new Vector2(probeX, probeTop),
                Vector2.down,
                probeTop - lowest,
                wallMask
            );

        // distance 0 = çıkış noktası duvarın içinde (kenar çok yukarıda).
        if (top.collider == null || top.distance <= 0.01f)
            return false;

        if (top.normal.y < 0.7f)
            return false;

        float ledgeTop = top.point.y;

        // 3) Yer var mı?
        Vector2 boxSize = new Vector2(b.size.x * 0.9f, b.size.y * 0.9f);

        Vector2 standCenter =
            new Vector2(
                wallX + dir * (b.extents.x + 0.1f),
                ledgeTop + b.extents.y + 0.12f
            );

        if (Physics2D.OverlapBox(standCenter, boxSize, 0f, wallMask) != null)
            return false;

        Vector2 riseCenter = new Vector2(b.center.x, standCenter.y);

        if (Physics2D.OverlapBox(riseCenter, boxSize, 0f, wallMask) != null)
            return false;

        // Collider bounds fizik pozisyonunu izler (transform interpolasyonlu olabilir).
        Vector2 bodyPos =
            player != null && player.rb != null
                ? player.rb.position
                : (Vector2)transform.position;

        Vector2 offset = (Vector2)b.center - bodyPos;

        standPos = standCenter - offset;
        return true;
    }

    // =========================================================
    // JUMP RESET
    // =========================================================

    public void ResetJump()
    {
        jumpConsumedReset();
    }

    private void jumpConsumedReset()
    {
        // JumpState / PlayerController yönetir.
        // Compatibility amacıyla tutuluyor.
    }

    // =========================================================
    // EXTERNAL SETTERS
    // =========================================================

    public void SetGroundCheck(
        Transform check,
        float radius,
        LayerMask mask
    )
    {
        groundCheck =
            check;

        groundCheckRadius =
            radius;

        groundMask =
            mask;
    }

    public void SetWallCheck(
        Transform check,
        float distance,
        LayerMask mask
    )
    {
        wallCheck =
            check;

        wallCheckDistance =
            distance;

        wallMask =
            mask;
    }

    // =========================================================
    // AIR DATA
    // =========================================================

    public float GetMaxAirHeight()
    {
        return maxAirHeight;
    }

    public float GetAirTime()
    {
        return airTime;
    }

    public void ResetAirData()
    {
        airTime = 0f;

        if (player != null)
        {
            maxAirHeight =
                player.transform.position.y;
        }
    }
}