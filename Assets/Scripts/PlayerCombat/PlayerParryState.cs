
using UnityEngine;

public class PlayerParryState : IPlayerDefenseState
{
    private PlayerDefenseController defense;
    private PlayerController player;

    private float timer;

    public PlayerParryState(
        PlayerDefenseController defense,
        PlayerController player
    )
    {
        this.defense = defense;
        this.player = player;
    }

    public void Enter()
    {
        timer = defense.ParryWindow;

        defense.SetParrying(true);
        defense.SetBlocking(false);

        Debug.Log("PARRY WINDOW START");
    }

    public void Tick()
    {
        if (!Input.GetMouseButton(1))
        {
            defense.ChangeState(null);
            return;
        }

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            defense.ChangeState(
                new PlayerBlockState(
                    defense,
                    player
                )
            );
        }
    }

    public void Exit()
    {
        defense.SetParrying(false);
    }
}
