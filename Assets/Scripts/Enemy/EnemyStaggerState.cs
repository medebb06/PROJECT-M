using UnityEngine;

public class EnemyStaggerState : IEnemyState
{
    private EnemyController enemy;
    private Rigidbody2D rb;

    private float staggerTimer;

    public EnemyStaggerState(
        EnemyController enemy
    )
    {
        this.enemy = enemy;
    }

    public void Enter()
    {
        staggerTimer =
            enemy.staggerDuration;

        rb =
            enemy.GetComponent<Rigidbody2D>();

        // Finisher için hedef çerçevesi.
        // (FinisherTargetHighlight daha önce hiç açılmıyordu.)
        enemy.SetFinisherHighlight(true);

        // Not: Stagger rengi artık EnemyController.ForceStagger
        // içinde yönetiliyor. Burada tekrar renk değiştirmiyoruz;
        // aksi halde iki sistem birbirinin rengini eziyordu.

        Debug.Log(
            "ENEMY STAGGER!"
        );
    }

    public void Tick()
    {
        // ==========================================
        // KNOCKBACK YAVAŞLAMASI
        // Stagger sırasında alınan vuruşlar hız veriyor
        // ama yavaşlatan bir state yoktu; düşman kayıyordu.
        // ==========================================

        if (rb != null)
        {
            float newX =
                Mathf.MoveTowards(
                    rb.linearVelocity.x,
                    0f,
                    enemy.knockbackDeceleration *
                    EnemyTime.DeltaTime
                );

            rb.linearVelocity =
                new Vector2(
                    newX,
                    rb.linearVelocity.y
                );
        }

        // ==========================================
        // STAGGER TIMER
        // ==========================================

        staggerTimer -=
            EnemyTime.DeltaTime;

        if (staggerTimer <= 0f)
        {
            enemy.ChangeState(
                new EnemyChaseState(enemy)
            );
        }
    }

    public void Exit()
    {
        enemy.SetFinisherHighlight(false);

        enemy.ClearStaggerTint();

        // FIX: Denge toparlanması artık Exit'te.
        // Eskiden sadece süre dolunca toparlanıyordu;
        // execute ile çıkılıp düşman hayatta kalırsa denge
        // sonsuza kadar kırık kalıyordu.
        RecoverBalance();
    }

    private void RecoverBalance()
    {
        EnemyBalance balance =
            enemy.GetComponent<EnemyBalance>();

        if (balance == null)
            return;

        balance.RecoverBalance();

        Debug.Log(
            "ENEMY BALANCE RECOVERED!"
        );
    }
}