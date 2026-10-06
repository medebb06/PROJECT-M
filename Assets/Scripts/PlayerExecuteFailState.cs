using UnityEngine;

/// <summary>
/// İNFAZ TUTMADI: düşman "açık an"da değildi. Oyuncu kısa bir hamle yapar, ardından
/// kısa süre savunmasız kalır (dokunulmaz DEĞİL). Bar boşa gitmiştir.
/// </summary>
public class PlayerExecuteFailState : IPlayerState
{
    private readonly PlayerController player;
    private readonly PlayerStateMachine sm;

    private readonly float lungeDir;
    private readonly float lungeDistance;

    private Vector2 start;
    private float timer;

    private float originalGravityScale;

    private const float LungeTime = 0.14f;   // gerçek sn
    private const float StunTime = 0.6f;     // oyun sn: savunmasız süre

    public PlayerExecuteFailState(
        PlayerController player,
        PlayerStateMachine sm,
        float lungeDir,
        float lungeDistance
    )
    {
        this.player = player;
        this.sm = sm;
        this.lungeDir = lungeDir;
        this.lungeDistance = lungeDistance;
    }

    public void Enter()
    {
        start = player.rb.position;
        timer = 0f;

        originalGravityScale = player.rb.gravityScale;
        player.rb.gravityScale = 0f;

        player.canControl = false;
        player.rb.linearVelocity = Vector2.zero;
    }

    public void Update()
    {
        if (lunging)
        {
            // Atılma kısmı gerçek zamanla (ağır çekimden çıkarken de hızlı).
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / LungeTime);
            float eased = 1f - (1f - t) * (1f - t);

            player.rb.position = start + new Vector2(lungeDir * lungeDistance * eased, 0f);
            player.rb.linearVelocity = Vector2.zero;

            if (t >= 1f)
            {
                lunging = false;
                stunTimer = StunTime;
            }

            return;
        }

        stunTimer -= Time.deltaTime;

        if (stunTimer <= 0f)
            Finish();
    }

    private bool lunging = true;
    private float stunTimer;

    public void FixedUpdate()
    {
        player.rb.linearVelocity = Vector2.zero;
    }

    private void Finish()
    {
        player.rb.gravityScale = originalGravityScale;
        player.canControl = true;

        if (player.IsGrounded())
            sm.ChangeState(new GroundedState(player, sm));
        else
            sm.ChangeState(new AirState(player, sm));
    }

    public void Exit()
    {
        player.rb.gravityScale = originalGravityScale;
        player.canControl = true;
    }
}
