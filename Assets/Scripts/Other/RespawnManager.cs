
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;

    private Health playerHealth;

    private void Awake()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        if (player != null)
        {
            playerHealth = player.GetComponent<Health>();
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.R))
            return;

        RespawnAtMouse();
    }

    private void RespawnAtMouse()
    {
        if (player == null)
        {
            Debug.LogError("RespawnManager: Player bulunamadı!");
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = player.GetComponent<Health>();
        }

        if (playerHealth == null)
        {
            Debug.LogError("RespawnManager: Player üzerinde Health yok!");
            return;
        }

        if (!playerHealth.IsDead)
        {
            Debug.Log("RespawnManager: Player ölü değil.");
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("RespawnManager: Main Camera bulunamadı!");
            return;
        }

        Vector3 mouseScreenPosition = Input.mousePosition;

        Vector3 mouseWorldPosition =
            mainCamera.ScreenToWorldPoint(
                mouseScreenPosition
            );

        mouseWorldPosition.z =
            player.transform.position.z;

        // Önce Player'ı mouse konumuna taşı.
        player.transform.position =
            mouseWorldPosition;

        // Health'i tamamen yenile.
        playerHealth.Revive();

        // Rigidbody varsa hareketini sıfırla.
        if (player.rb != null)
        {
            player.rb.linearVelocity = Vector2.zero;
        }

        // Player'ı tekrar normal GroundedState'e sok.
        player.stateMachine.ChangeState(
            new GroundedState(
                player,
                player.stateMachine
            )
        );

        Debug.Log(
            "PLAYER RESPAWNED AT MOUSE: " +
            mouseWorldPosition
        );
    }
}

