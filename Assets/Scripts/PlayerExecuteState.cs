using UnityEngine;

/// <summary>
/// İNFAZ GEÇİŞİ: oyuncu düşmanın İÇİNDEN çok hızlı geçer (gerçek zamanla), dünya ağır çekimdedir.
/// Geçerken art görüntü (afterimage) bırakır; sonunda düşmana doğru döner.
/// </summary>
public class PlayerExecuteState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    private Vector2 startPosition;
    private Vector2 targetPosition;

    private float timer;
    private float duration;

    private float ghostTimer;

    private float originalGravityScale;

    // Geçişin yönü (sonunda tersine dönüp düşmana bakılır).
    private float moveDir;

    // Art görüntü sıklığı (gerçek sn).
    private const float GhostSpacing = 0.014f;

    public PlayerExecuteState(
        PlayerController player,
        PlayerStateMachine sm,
        Vector2 targetPosition,
        float duration
    )
    {
        this.player = player;
        this.sm = sm;
        this.targetPosition = targetPosition;
        this.duration = Mathf.Max(0.05f, duration);
    }

    public void Enter()
    {
        startPosition =
            player.rb.position;

        timer = 0f;
        ghostTimer = 0f;

        moveDir = Mathf.Sign(targetPosition.x - startPosition.x);

        if (moveDir == 0f)
            moveDir = player.facingDir;

        originalGravityScale =
            player.rb.gravityScale;

        player.rb.gravityScale = 0f;

        player.canControl = false;
        player.isInvincible = true;
        player.isDashing = true;

        // Hareket yönüne bak.
        FaceDirection(moveDir);

        player.SetVelocity(
            Vector2.zero
        );
    }

    public void Update()
    {
        // GERÇEK ZAMAN: dünya ağır çekimdeyken bile geçiş hızlıdır.
        float dt = Time.unscaledDeltaTime;

        timer += dt;

        float t = Mathf.Clamp01(timer / duration);

        // Hızlı başlar, sonda yumuşakça yavaşlar.
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);

        Vector2 pos = Vector2.Lerp(startPosition, targetPosition, eased);

        player.rb.position = pos;
        player.rb.linearVelocity = Vector2.zero;

        ghostTimer -= dt;

        if (ghostTimer <= 0f)
        {
            ghostTimer = GhostSpacing;
            SpawnGhost();
        }

        if (t >= 1f)
            Finish();
    }

    public void FixedUpdate()
    {
        player.rb.linearVelocity = Vector2.zero;
    }

    private void SpawnGhost()
    {
        if (!player.afterImagePrefab || !player.playerSprite)
            return;

        GameObject obj = Object.Instantiate(
            player.afterImagePrefab,
            player.transform.position,
            Quaternion.identity
        );

        AfterImage ghost = obj.GetComponent<AfterImage>();

        if (ghost == null)
            return;

        Vector3 ghostScale =
            player.modelPivot != null
                ? player.modelPivot.localScale
                : Vector3.one;

        ghostScale.x = Mathf.Abs(ghostScale.x);

        ghost.Init(
            player.playerSprite.sprite,
            ghostScale,
            player.playerSprite.flipX
        );

        // Ağır çekimde uzun süre kalsın (iz bırakma hissi).
        ghost.fadeSpeed = 1.3f;
    }

    private void FaceDirection(float dir)
    {
        player.facingDir = dir < 0f ? -1f : 1f;

        if (player.playerSprite != null)
            player.playerSprite.flipX = player.facingDir < 0f;
    }

    private void Finish()
    {
        player.rb.gravityScale =
            originalGravityScale;

        player.isDashing = false;
        player.isInvincible = false;
        player.canControl = true;

        player.SetVelocity(
            Vector2.zero
        );

        // Geçtikten sonra düşmana doğru dön (sinematik bitiş).
        FaceDirection(-moveDir);

        if (player.IsGrounded())
        {
            sm.ChangeState(
                new GroundedState(
                    player,
                    sm
                )
            );
        }
        else
        {
            sm.ChangeState(
                new AirState(
                    player,
                    sm
                )
            );
        }
    }

    public void Exit()
    {
        player.rb.gravityScale =
            originalGravityScale;

        player.isDashing = false;
        player.isInvincible = false;
        player.canControl = true;
    }
}
