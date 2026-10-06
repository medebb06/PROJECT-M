using UnityEngine;

/// <summary>
/// İNFAZ ODAĞI: E basılı tutulurken oyuncu yerinde durur (dokunulmaz değildir).
/// (PlayerFinisher yönetir; bırakınca EnemyExecuteState → PlayerExecuteState devralır.)
/// </summary>
public class PlayerExecuteChargeState : IPlayerState
{
    private readonly PlayerController player;

    private float originalGravityScale;

    public PlayerExecuteChargeState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        originalGravityScale = player.rb.gravityScale;

        player.rb.gravityScale = 0f;
        player.rb.linearVelocity = Vector2.zero;

        // Dokunulmaz DEĞİL: tutturamazsan / düşman saldırırsa hasar yiyebilirsin.
        player.canControl = false;
    }

    public void Update()
    {
        // Odaklanırken input yok.
    }

    public void FixedUpdate()
    {
        player.rb.linearVelocity = Vector2.zero;
    }

    public void Exit()
    {
        player.rb.gravityScale = originalGravityScale;

        player.canControl = true;
    }
}
