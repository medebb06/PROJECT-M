using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    public int hp = 3;

    EnemyController controller;
    bool isInvulnerable;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
    }

    public void TakeDamage(int damage, Vector2 knockback)
    {
        if (isInvulnerable) return;

        hp -= damage;

        controller.ChangeState(new EnemyHitState(controller, knockback));

        StartCoroutine(IFrame());

        if (hp <= 0)
            Destroy(gameObject);
    }

    System.Collections.IEnumerator IFrame()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(0.06f);
        isInvulnerable = false;
    }
}