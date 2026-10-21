using System.Collections.Generic;
using UnityEngine;

// Tüm düşmanların ortak sözleşmesi. Oyuncu/koordinatör tarafı bu tipe bağlanır.
// Legacy EnemyController ve yeni EnemyUnit bundan türer.
public abstract class EnemyBase : MonoBehaviour
{
    public static readonly List<EnemyBase> All = new List<EnemyBase>();
    private static int nextSpawnIndex;

    public int SpawnIndex { get; private set; }

    public Transform target;

    public abstract bool IsDead { get; }
    public abstract bool IsStaggered { get; }
    public abstract bool IsAttackCommitted { get; }
    public abstract bool IsOpen { get; }
    public abstract float FacingDirection { get; }

    public abstract void ApplyAttackHit(Vector2 hitDirection, bool healthHit);
    public abstract void ApplyAttackHit(Vector2 hitDirection, bool healthHit, int attackStep);
    public abstract void ApplyBalanceHit(Vector2 hitDirection);
    public abstract void ApplyBlockKnockback(Vector2 hitDirection);
    public abstract void ForceStagger();
    public abstract void StartAttackRecovery();
    public abstract void StartAttackRecovery(float duration);
    public abstract void PlayTintFlash(Color color, float duration);
    public abstract void PlayBalanceDamageFlash();
    public abstract void PlayParrySlowMotion(bool brokeBalance);
    public abstract void PlayHealthHitSound();
    public abstract void PlayPostureHitSound();

    // Slam'in dengeye vereceği hasar (düşman başına).
    public virtual int SlamBalanceDamage { get { return 3; } }

    // Kararlı saldırıyı kesip kısa süre hit-stun'a sokar (slam / şok).
    public virtual void InterruptWithHitStun(float duration) { }

    // Sersemlemiş (stagger) düşmana tek düz vuruşun götüreceği MAX can oranı (infaz). 0 = kapalı.
    public virtual float StaggerHitHealthFraction { get { return 0f; } }

    /// <summary>false: sıradan düşman, posture barı yok (sadece can). Parry kısa sersemletme verir.</summary>
    public virtual bool UsesPosture { get { return true; } }
    // true: düz vuruşlar posture'a gider, düşman bloklar (can gitmez, tepki yok).
    public virtual bool BlocksPlainHits { get { return false; } }
    // Oyuncunun saldırısı düşmanın saldırısıyla çarpışırsa (clash): true => düşman saldırısı iptal, hasar yok.
    public virtual bool TryClash(Vector2 hitDirection) { return false; }

    protected virtual void Awake()
    {
        SpawnIndex = nextSpawnIndex++;
    }

    protected virtual void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);
    }
}
