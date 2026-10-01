
using UnityEngine;

public class CombatVFXManager : MonoBehaviour
{
    public static CombatVFXManager Instance { get; private set; }

    [Header("Hit VFX")]
    [SerializeField] private GameObject healthHitVFX;
    [SerializeField] private GameObject balanceHitVFX;

    [Header("Defense VFX")]
    [SerializeField] private GameObject parryVFX;
    [SerializeField] private GameObject blockVFX;

    [Header("Balance VFX")]
    [SerializeField] private GameObject balanceBreakVFX;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Debug.Log("COMBAT VFX MANAGER AWAKE");
    }

    public void PlayHealthHit(Vector3 position, Vector2 direction)
    {
        Debug.Log("VFX MANAGER: PLAY HEALTH HIT");

        if (healthHitVFX == null)
        {
            Debug.LogError(
                "VFX MANAGER: HEALTH HIT VFX ATANMAMIŞ!"
            );

            return;
        }

        SpawnVFX(
            healthHitVFX,
            position,
            direction
        );
    }

    public void PlayBalanceHit(Vector3 position, Vector2 direction)
    {
        Debug.Log("VFX MANAGER: PLAY BALANCE HIT");

        if (balanceHitVFX == null)
        {
            Debug.LogError(
                "VFX MANAGER: BALANCE HIT VFX ATANMAMIŞ!"
            );

            return;
        }

        SpawnVFX(
            balanceHitVFX,
            position,
            direction
        );
    }

    public void PlayParry(Vector3 position, Vector2 direction)
    {
        Debug.Log("VFX MANAGER: PLAY PARRY");

        if (parryVFX == null)
        {
            Debug.LogError(
                "VFX MANAGER: PARRY VFX ATANMAMIŞ!"
            );

            return;
        }

        SpawnVFX(
            parryVFX,
            position,
            direction
        );
    }

    public void PlayBlock(Vector3 position, Vector2 direction)
    {
        Debug.Log("VFX MANAGER: PLAY BLOCK");

        if (blockVFX == null)
        {
            Debug.LogError(
                "VFX MANAGER: BLOCK VFX ATANMAMIŞ!"
            );

            return;
        }

        SpawnVFX(
            blockVFX,
            position,
            direction
        );
    }

    public void PlayBalanceBreak(Vector3 position, Vector2 direction)
    {
        Debug.Log("VFX MANAGER: PLAY BALANCE BREAK");

        if (balanceBreakVFX == null)
        {
            Debug.LogError(
                "VFX MANAGER: BALANCE BREAK VFX ATANMAMIŞ!"
            );

            return;
        }

        SpawnVFX(
            balanceBreakVFX,
            position,
            direction
        );
    }

    private void SpawnVFX(
        GameObject prefab,
        Vector3 position,
        Vector2 direction)
    {
        Debug.Log(
            "VFX MANAGER: INSTANTIATE -> " +
            prefab.name
        );

        Quaternion rotation = Quaternion.identity;

        if (direction.sqrMagnitude > 0.001f)
        {
            float angle =
                Mathf.Atan2(
                    direction.y,
                    direction.x
                ) * Mathf.Rad2Deg;

            rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                );
        }

        GameObject vfx =
            Instantiate(
                prefab,
                position,
                rotation
            );

        if (vfx == null)
        {
            Debug.LogError(
                "VFX MANAGER: INSTANTIATE BAŞARISIZ!"
            );

            return;
        }

        Debug.Log(
            "VFX MANAGER: VFX OLUŞTU -> " +
            vfx.name
        );

        ParticleSystem[] particleSystems =
            vfx.GetComponentsInChildren<ParticleSystem>(
                true
            );

        Debug.Log(
            "VFX MANAGER: PARTICLE SYSTEM SAYISI -> " +
            particleSystems.Length
        );

        foreach (
            ParticleSystem particleSystem
            in particleSystems)
        {
            particleSystem.Play(true);
        }

        DestroyVFXWhenFinished(
            vfx,
            particleSystems
        );
    }

    private void DestroyVFXWhenFinished(
        GameObject vfx,
        ParticleSystem[] particleSystems)
    {
        if (vfx == null)
            return;

        float longestLifetime = 0f;

        foreach (
            ParticleSystem particleSystem
            in particleSystems)
        {
            if (particleSystem == null)
                continue;

            var main =
                particleSystem.main;

            float lifetime =
                main.duration;

            if (
                main.startLifetime.mode ==
                ParticleSystemCurveMode.Constant)
            {
                lifetime +=
                    main.startLifetime.constant;
            }
            else
            {
                lifetime += 2f;
            }

            if (lifetime > longestLifetime)
                longestLifetime = lifetime;
        }

        if (longestLifetime <= 0f)
            longestLifetime = 2f;

        Destroy(
            vfx,
            longestLifetime + 0.2f
        );
    }
}
