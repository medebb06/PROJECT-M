public class GroundedState : IPlayerState
{
    private PlayerController player;
    private PlayerStateMachine sm;

    public GroundedState(PlayerController player, PlayerStateMachine sm)
    {
        this.player = player;
        this.sm = sm;
    }

    public void Enter() { }

    public void Exit() { }

    public void Update()
    {
        player.ApplyMovement(); // 🔥 artık smooth sistem

        if (player.jumpPressed)
        {
            sm.ChangeState(new JumpState(player, sm));
            return;
        }

        if (player.dashPressed)
        {
            sm.ChangeState(new DashState(player, sm));
            return;
        }
    }

    public void FixedUpdate() { }
}