using UnityEngine;

public class EnemyChaseState : IEnemyState
{
    EnemyController enemy;
    Rigidbody2D rb;

    // Oyuncuya göre hangi tarafta bekliyoruz (+1 sağ, -1 sol).
    // Tam üstündeyken (dx ~ 0) yön değiştirmesin diye hatırlanır.
    float standbySide = 1f;

    public EnemyChaseState(EnemyController enemy)
    {
        this.enemy = enemy;
        rb = enemy.GetComponent<Rigidbody2D>();
    }

    public void Enter()
    {
        if (enemy.target != null)
        {
            standbySide =
                enemy.transform.position.x >= enemy.target.position.x
                    ? 1f
                    : -1f;
        }
    }

    public void Tick()
    {
        if (enemy.target == null)
            return;

        // Oyuncu öldüyse kovalamayı bırak.
        if (enemy.IsTargetDead)
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

        // -----------------------------------------
        // BLOCK KNOCKBACK / MOVEMENT LOCK
        // -----------------------------------------

        if (enemy.IsMovementLocked)
        {
            return;
        }

        float dist = Vector2.Distance(
            enemy.transform.position,
            enemy.target.position
        );

        // -----------------------------------------
        // TOO FAR
        // -----------------------------------------

        if (dist > enemy.chaseRange * 1.5f)
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

        // -----------------------------------------
        // STANDBY (SIRA BEKLEME / DAĞILMA)
        // Saldırıya hazır düşmanlar sıraya girer.
        // Sıradaki (rank 0) oyuncuya yaklaşır, diğerleri
        // sıra numaralarına göre kademeli geride bekler.
        // Böylece kalabalık üst üste yığılmaz, okunur.
        // -----------------------------------------

        if (
            enemy.useStandby &&
            enemy.CanAttack &&
            dist <= enemy.chaseRange * 1.3f
        )
        {
            EnemyAttackCoordinator.JoinQueue(enemy);

            int rank =
                EnemyAttackCoordinator.GetStandbyRank(enemy);

            if (rank > 0)
            {
                MoveToStandby(rank);

                return;
            }

            // rank 0: aşağıdaki normal akış
            // (yaklaş + saldırı sırası iste).
        }

        // -----------------------------------------
        // STOP DISTANCE
        // -----------------------------------------

        if (dist <= enemy.chaseStopDistance)
        {
            // Enemy oyuncuya yeterince yaklaştı.
            // Artık chase hareketi yapma.
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );

            // -----------------------------------------
            // ATTACK RANGE
            // -----------------------------------------

            if (dist <= enemy.attackRange)
            {
                // Saldırı cooldown'daysa bekle.
                if (!enemy.CanAttack)
                {
                    return;
                }

                // RİTİM: Kalabalıkta herkes aynı anda saldırmasın.
                // Koordinatör sıra gelene kadar false döner;
                // düşman yerinde bekler ve tekrar sorar.
                if (
                    !EnemyAttackCoordinator.TryRequestAttack(
                        enemy,
                        enemy.attackWarningTime
                    )
                )
                {
                    return;
                }

                enemy.ChangeState(
                    new EnemyAttackState(enemy)
                );

                return;
            }

            return;
        }

        // -----------------------------------------
        // CHASE
        // -----------------------------------------
        // Sadece yatay yön kullanılıyor (oyuncu yukarıdaysa
        // düşman yavaşlamasın). Hız enemy.chaseSpeed.

        float deltaX =
            enemy.target.position.x -
            enemy.transform.position.x;

        float dirX =
            Mathf.Abs(deltaX) > 0.05f
                ? Mathf.Sign(deltaX)
                : 0f;

        rb.linearVelocity = new Vector2(
            dirX * enemy.chaseSpeed,
            rb.linearVelocity.y
        );
    }

    // =========================================================
    // STANDBY HAREKETİ
    // =========================================================

    private void MoveToStandby(int rank)
    {
        float deltaToPlayer =
            enemy.transform.position.x -
            enemy.target.position.x;

        // Oyuncunun tam üstündeyken önceki tarafı koru.
        if (Mathf.Abs(deltaToPlayer) > 0.1f)
            standbySide = Mathf.Sign(deltaToPlayer);

        // rank 1 -> standbyDistance, rank 2 -> +spacing, ...
        // ChaseState'in "too far" sınırının (chaseRange * 1.5) içinde kal;
        // yoksa düşman Idle'a düşüp tekrar Chase'e dönerdi (salınım).
        float desiredDistance =
            Mathf.Min(
                enemy.standbyDistance +
                (rank - 1) * enemy.standbySpacing,
                enemy.chaseRange * 1.3f
            );

        float desiredX =
            enemy.target.position.x +
            standbySide * desiredDistance;

        float deltaX =
            desiredX -
            enemy.transform.position.x;

        if (Mathf.Abs(deltaX) <= enemy.standbyArrivalTolerance)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );

            return;
        }

        // Geri çekilirken de yaklaşırken de oyuncuya dönük kalır
        // (EnemyController.FaceTarget).
        rb.linearVelocity = new Vector2(
            Mathf.Sign(deltaX) *
            enemy.chaseSpeed *
            enemy.standbySpeedMultiplier,
            rb.linearVelocity.y
        );
    }

    public void Exit()
    {
        rb.linearVelocity = Vector2.zero;
    }
}