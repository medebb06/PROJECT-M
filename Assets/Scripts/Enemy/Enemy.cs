using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int hp = 3;

    private EnemyController controller;
    private bool isInvulnerable;

    void Awake()
    {
        controller = GetComponent<EnemyController>();
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        if (isInvulnerable)
            return;

        hp -= damage;

        // Enemy saldırıyı bırakıp HitState'e girer.
        controller.ChangeState(
            new EnemyHitState(controller, hitDirection)
        );

        StartCoroutine(IFrame());

        if (hp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private System.Collections.IEnumerator IFrame()
    {
        isInvulnerable = true;

        yield return new WaitForSeconds(0.06f);

        isInvulnerable = false;
    }
}