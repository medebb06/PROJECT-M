using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] int hp = 3;

    EnemyController controller;

    bool isInvulnerable;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        if (isInvulnerable)
            return;

        hp -= damage;

        // Hit yönünü EnemyHitState'e gönderiyoruz.
        controller.ChangeState(
            new EnemyHitState(controller, hitDirection)
        );

        StartCoroutine(IFrame());

        if (hp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }

    System.Collections.IEnumerator IFrame()
    {
        isInvulnerable = true;

        yield return new WaitForSeconds(0.06f);

        isInvulnerable = false;
    }
}