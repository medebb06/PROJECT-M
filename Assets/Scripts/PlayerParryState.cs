using UnityEngine;

/// <summary>
/// PARRY PENCERESİ. Tuşa BASINCA açılır ve tuşu bıraksan bile SÜRESİ
/// DOLANA KADAR açık kalır (hızlı tıklama tam pencere alır).
/// Süre dolunca: tuş hâlâ basılıysa Block'a, değilse savunmasız duruma geçer.
/// </summary>
public class PlayerParryState : IPlayerDefenseState
{
    private readonly PlayerDefenseController defense;
    private readonly PlayerController player;
    private readonly float window;

    private float timer;
    private float startTime;

    public PlayerParryState(
        PlayerDefenseController defense,
        PlayerController player,
        float window
    )
    {
        this.defense = defense;
        this.player = player;
        this.window = Mathf.Max(0.02f, window);
    }

    public float StartTime => startTime;

    public void Enter()
    {
        timer = window;
        startTime = Time.time;

        defense.SetParrying(true);
        defense.SetBlocking(false);
    }

    public void Tick()
    {
        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        // Pencere bitti: başarılı mıydı? (spam cezası için)
        defense.NotifyParryWindowEnded(startTime);

        if (Input.GetMouseButton(1))
        {
            defense.ChangeState(
                new PlayerBlockState(
                    defense,
                    player
                )
            );
        }
        else
        {
            defense.ChangeState(null);
        }
    }

    public void Exit()
    {
        defense.SetParrying(false);
    }
}