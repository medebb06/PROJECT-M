using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject enemyPrefab;

    [Header("Optional")]
    public Transform spawnParent;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            SpawnEnemyAtMouse();
        }
    }

    void SpawnEnemyAtMouse()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy Prefab yok!");
            return;
        }

        Vector3 mouse = Input.mousePosition;
        mouse.z = 10f; // kamera mesafesi

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mouse);
        worldPos.z = 0f;

        GameObject enemy = Instantiate(enemyPrefab, worldPos, Quaternion.identity);

        if (spawnParent != null)
            enemy.transform.SetParent(spawnParent);
    }
}