using UnityEngine;

public class PlayerBlockState : IPlayerDefenseState
{
    private PlayerDefenseController defense;
    private PlayerController player;

    public PlayerBlockState(
        PlayerDefenseController defense,
        PlayerController player
    )
    {
        this.defense = defense;
        this.player = player;
    }

    public void Enter()
    {
        defense.SetBlocking(true);
        defense.SetParrying(false);

        Debug.Log("BLOCK ACTIVE");
    }

    public void Tick()
    {
        if (!Input.GetMouseButton(1))
        {
            defense.ChangeState(null);
        }
    }

    public void Exit()
    {
        defense.SetBlocking(false);
    }
}