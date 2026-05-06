using UnityEngine;
using static UnityEngine.Analytics.IAnalytic;

public interface IDamageable
{
    void TakeDamage(int damage, Vector2 knockback);
}