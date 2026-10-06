using UnityEngine;
using UnityEngine.VFX;

public class CombatVFXManager : MonoBehaviour
{
    public static CombatVFXManager Instance { get; private set; }

    [Header("Hit VFX")]
    [SerializeField] private GameObject healthHitVFX;
    [SerializeField] private GameObject balanceHitVFX;

    [Header("Defense VFX")]
    [SerializeField] private GameObject parryVFX;

    [Header("Parry (kodla çizilen çatışma efekti)")]
    [Tooltip("Açık: parry'de ParryVFX (yıldız flaşı + halka + kıvılcım) oynar.")]
    [SerializeField] private bool proceduralParryVFX = true;

    [Tooltip("Açık: eski 'Parry VFX' prefab'ı da oynar. Normal vuruşla aynı görünüyorsa KAPALI bırak.")]
    [SerializeField] private bool alsoPlayParryPrefab = false;
    [SerializeField] private GameObject blockVFX;

    [Header("Balance VFX")]
    [SerializeField] private GameObject balanceBreakVFX;

    [Header("VFX Render")]
    [SerializeField] private string vfxSortingLayer = "Default";
    [SerializeField] private int vfxSortingOrder = 100;

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
        if (healthHitVFX == null)
        {
            Debug.LogWarning("HEALTH HIT VFX ASSIGNED DEĞİL!");
            return;
        }

        SpawnVFX(healthHitVFX, position, direction);
    }

    public void PlayBalanceHit(Vector3 position, Vector2 direction)
    {
        if (balanceHitVFX == null)
        {
            Debug.LogWarning("BALANCE HIT VFX ASSIGNED DEĞİL!");
            return;
        }

        SpawnVFX(balanceHitVFX, position, direction);
    }

    public void PlayParry(Vector3 position, Vector2 direction)
    {
        if (proceduralParryVFX)
        {
            ParryVFX.Play(position, direction, vfxSortingLayer, vfxSortingOrder + 20);

            if (!alsoPlayParryPrefab)
                return;
        }

        if (parryVFX == null)
        {
            Debug.LogWarning("PARRY VFX ASSIGNED DEĞİL!");
            return;
        }

        SpawnVFX(parryVFX, position, direction);
    }

    public void PlayBlock(Vector3 position, Vector2 direction)
    {
        if (blockVFX == null)
        {
            Debug.LogWarning("BLOCK VFX ASSIGNED DEĞİL!");
            return;
        }

        SpawnVFX(blockVFX, position, direction);
    }

    public void PlayBalanceBreak(Vector3 position, Vector2 direction)
    {
        if (balanceBreakVFX == null)
        {
            Debug.LogWarning("BALANCE BREAK VFX ASSIGNED DEĞİL!");
            return;
        }

        SpawnVFX(balanceBreakVFX, position, direction);
    }

    private void SpawnVFX(
        GameObject prefab,
        Vector3 position,
        Vector2 direction)
    {
        Debug.Log("VFX MANAGER: INSTANTIATE -> " + prefab.name);

        Quaternion rotation = Quaternion.identity;

        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rotation = Quaternion.Euler(0f, 0f, angle);
        }

        GameObject vfx = Instantiate(prefab, position, rotation);

        if (vfx == null)
        {
            Debug.LogError("VFX MANAGER: INSTANTIATE BAŞARISIZ!");
            return;
        }

        Debug.Log("VFX MANAGER: VFX OLUŞTU -> " + vfx.name);

        // --------------------------------------------------
        // PARTICLE SYSTEM RENDER ORDER
        // --------------------------------------------------

        ParticleSystemRenderer[] particleRenderers =
            vfx.GetComponentsInChildren<ParticleSystemRenderer>(true);

        Debug.Log(
            "VFX MANAGER: PARTICLE RENDERER SAYISI -> " +
            particleRenderers.Length
        );

        foreach (ParticleSystemRenderer renderer in particleRenderers)
        {
            if (renderer == null)
                continue;

            renderer.sortingLayerName = vfxSortingLayer;
            renderer.sortingOrder = vfxSortingOrder;

            Debug.Log(
                "VFX RENDER SET -> Layer: " +
                renderer.sortingLayerName +
                " | Order: " +
                renderer.sortingOrder
            );
        }

        // --------------------------------------------------
        // VISUAL EFFECT GRAPH
        // --------------------------------------------------

        VisualEffect[] visualEffects =
            vfx.GetComponentsInChildren<VisualEffect>(true);

        Debug.Log(
            "VFX MANAGER: VISUAL EFFECT SAYISI -> " +
            visualEffects.Length
        );

        foreach (VisualEffect visualEffect in visualEffects)
        {
            if (visualEffect == null)
                continue;

            visualEffect.Play();
        }

        // --------------------------------------------------
        // PARTICLE SYSTEM
        // --------------------------------------------------

        ParticleSystem[] particleSystems =
            vfx.GetComponentsInChildren<ParticleSystem>(true);

        Debug.Log(
            "VFX MANAGER: PARTICLE SYSTEM SAYISI -> " +
            particleSystems.Length
        );

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
                continue;

            particleSystem.Play(true);
        }

        DestroyVFXWhenFinished(
            vfx,
            particleSystems,
            visualEffects
        );
    }

    private void DestroyVFXWhenFinished(
        GameObject vfx,
        ParticleSystem[] particleSystems,
        VisualEffect[] visualEffects)
    {
        if (vfx == null)
            return;

        float longestLifetime = 0f;

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
                continue;

            var main = particleSystem.main;

            float lifetime = main.duration;

            if (main.startLifetime.mode ==
                ParticleSystemCurveMode.Constant)
            {
                lifetime += main.startLifetime.constant;
            }
            else
            {
                lifetime += 2f;
            }

            if (lifetime > longestLifetime)
                longestLifetime = lifetime;
        }

        if (visualEffects.Length > 0)
        {
            if (longestLifetime < 2f)
                longestLifetime = 2f;
        }

        if (longestLifetime <= 0f)
            longestLifetime = 2f;

        Destroy(vfx, longestLifetime + 0.2f);
    }
}