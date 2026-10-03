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

        bool standbyActive = enemy.useStandby;

        bool queued =
            standbyActive &&
            EnemyAttackCoordinator.IsQueued(enemy);

        // -----------------------------------------
        // TOO FAR
        // Sıradaki düşman geride beklerken Idle'a düşüp
        // tekrar Chase'e dönmesin (salınım): sıradakilere
        // standbyMaxDistance'a kadar daha geniş bir bağ tanınır.
        // -----------------------------------------

        float leash =
            queued
                ? Mathf.Max(
                    enemy.chaseRange * 1.5f,
                    enemy.standbyMaxDistance + 1f
                )
                : enemy.chaseRange * 1.5f;

        if (!enemy.alwaysHunt && dist > leash)
        {
            enemy.ChangeState(
                new EnemyIdleState(enemy)
            );

            return;
        }

        // -----------------------------------------
        // STANDBY (SIRA BEKLEME / DAĞILMA)
        // Saldırıya hazır düşmanlar sıraya girer.
        //  - rank 0: oyuncuya yaklaşır, halka slotunda bekler/saldırır
        //  - rank 1, 2, ...: sıra numarasına göre geride kademeli bekler
        // -----------------------------------------

        if (
            standbyActive &&
            enemy.CanAttack &&
            (dist <= enemy.chaseRange * 1.3f || queued)
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

            FrontOfQueue(dist);

            return;
        }

        // -----------------------------------------
        // STOP DISTANCE  (standby kapalıysa / hazır değilse eski akış)
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
                        enemy.PlannedWindup
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
        // düşman yavaşlamasın). Hız enemy.ScaledChaseSpeed.

        float deltaX =
            enemy.target.position.x -
            enemy.transform.position.x;

        float dirX =
            Mathf.Abs(deltaX) > 0.05f
                ? Mathf.Sign(deltaX)
                : 0f;

        rb.linearVelocity = new Vector2(
            dirX * enemy.ScaledChaseSpeed,
            rb.linearVelocity.y
        );
    }

    // =========================================================
    // SIRANIN ÖNÜ (rank 0)
    // =========================================================

    private void FrontOfQueue(float dist)
    {
        // Menzildeyse hemen saldırı sırası iste.
        if (dist <= enemy.attackRange)
        {
            if (
                EnemyAttackCoordinator.TryRequestAttack(
                    enemy,
                    enemy.PlannedWindup
                )
            )
            {
                enemy.ChangeState(
                    new EnemyAttackState(enemy)
                );

                return;
            }
        }

        UpdateSide();

        float slot =
            ChooseRingSlot();

        MoveToRingSlot(slot);
    }

    // Halkada (saldırı menzili) iki kademe var:
    //  dış slot: menzilin sınırı (tek düşmanın klasik duruşu)
    //  iç slot : bir adım daha yakın
    // Ritim gereği iki düşman aynı anda uyarıda olabiliyor; ikincisi
    // iç slota geçerek birincinin üstüne binmez.
    // İkisi de doluysa halkanın hemen arkasında bekler.
    private float ChooseRingSlot()
    {
        float spacing = enemy.SlotSpacing;
        float outer = enemy.RingOuter;
        float inner = outer - spacing;

        if (!IsSlotTaken(outer, spacing))
            return outer;

        if (
            inner >= enemy.ringInnerMinDistance &&
            !IsSlotTaken(inner, spacing)
        )
        {
            return inner;
        }

        return outer + spacing;
    }

    // Aynı tarafta, bu slotta duran (saldırı state'indeki)
    // başka düşman var mı?
    private bool IsSlotTaken(float slotDistance, float spacing)
    {
        float targetX =
            enemy.target.position.x;

        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController other = EnemyController.All[i];

            if (
                other == null ||
                other == enemy ||
                other.IsDead ||
                !(other.CurrentState is EnemyAttackState)
            )
            {
                continue;
            }

            float otherDelta =
                other.transform.position.x - targetX;

            if (
                Mathf.Sign(otherDelta) != standbySide &&
                Mathf.Abs(otherDelta) > 0.1f
            )
            {
                continue;
            }

            if (
                Mathf.Abs(Mathf.Abs(otherDelta) - slotDistance) <
                spacing * 0.6f
            )
            {
                return true;
            }
        }

        return false;
    }

    // Slota yaklaşır (tam slotta durur), fazla yakınsa geri çekilir.
    private void MoveToRingSlot(float slotDistance)
    {
        float horizontal =
            Mathf.Abs(
                enemy.target.position.x -
                enemy.transform.position.x
            );

        float velocityX;

        if (horizontal > slotDistance + 0.001f)
        {
            // Yaklaş (hedef yönünde).
            velocityX =
                -standbySide * enemy.ScaledChaseSpeed;
        }
        else if (
            horizontal <
            slotDistance - enemy.standbyArrivalTolerance * 1.5f
        )
        {
            // Çok yakın: temkinli geri çekil.
            velocityX =
                standbySide *
                enemy.ScaledChaseSpeed *
                enemy.standbySpeedMultiplier;
        }
        else
        {
            velocityX = 0f;
        }

        rb.linearVelocity = new Vector2(
            velocityX,
            rb.linearVelocity.y
        );
    }

    // =========================================================
    // STANDBY HAREKETİ (rank >= 1)
    // =========================================================

    private void MoveToStandby(int rank)
    {
        UpdateSide();

        // Aralık, düşmanın gerçek genişliğinden küçük olamaz;
        // aksi halde sabit 1.5 geniş sprite'larda üst üste biner.
        float spacing = enemy.SlotSpacing;

        // rank 1 -> halkanın hemen arkası, rank 2 -> +spacing, ...
        float baseDistance =
            Mathf.Max(
                enemy.standbyDistance,
                enemy.RingOuter + spacing
            );

        float desiredDistance =
            Mathf.Min(
                baseDistance + (rank - 1) * spacing,
                enemy.standbyMaxDistance
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
            enemy.ScaledChaseSpeed *
            enemy.standbySpeedMultiplier,
            rb.linearVelocity.y
        );
    }

    private void UpdateSide()
    {
        float deltaToPlayer =
            enemy.transform.position.x -
            enemy.target.position.x;

        // Oyuncunun tam üstündeyken önceki tarafı koru.
        if (Mathf.Abs(deltaToPlayer) > 0.1f)
            standbySide = Mathf.Sign(deltaToPlayer);
    }

    public void Exit()
    {
        rb.linearVelocity = Vector2.zero;
    }
}