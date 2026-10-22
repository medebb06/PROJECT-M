using UnityEngine;
using UnityEngine.VFX;

public class CombatVFXManager : MonoBehaviour
{
    public static CombatVFXManager Instance { get; private set; }

    [Header("Hit VFX")]
    [SerializeField] private GameObject healthHitVFX;
    [SerializeField] private GameObject balanceHitVFX;

    [Header("Impact (denge vurusu) yerlesimi")]
    [Tooltip("Impact efekti dusmanin vurulan yonde, oyuncudan uzaga bu kadar geride cikar (birim).")]
    [SerializeField] private float impactBackOffset = 2.5f;
    [Tooltip("Kivilcimlarin (Shafts Streched) ekstra buyutme carpani.")]
    [SerializeField] private float impactSparkScale = 2.2f;
    [Tooltip("Cekirdek (Light/Shafts) dunya olcegi.")]
    [SerializeField] private float impactCoreScale = 3f;
    [Tooltip("Shafts Streched (cizgi) dunya olcegi.")]
    [SerializeField] private float impactLineScale = 1.2f;
    [SerializeField] private float impactScale = 3f;

    [Header("Defense VFX")]
    [SerializeField] private GameObject parryVFX;
    [Tooltip("Acik: parry'de parryVFX prefab'i (Impact02_White) oynar; kodla cizilen efektin yerine gecer.")]
    [SerializeField] private bool parryUsesPrefab = true;
    [Tooltip("Parry efekti oyuncudan dusmana dogru bu kadar uzakta cikar (birim).")]
    [SerializeField] private float parryOffset = 3f;
    private PlayerController parryPlayer;

    [Header("Parry (Casual_Hit Hit_1 varyasyonlari)")]
    [Tooltip("Parry'de bunlardan biri rastgele oynar. Doluysa eski parry efektleri tamamen devre disi.")]
    [SerializeField] private GameObject[] parryVariants;
    [SerializeField] private float parryScale = 3.5f;
    [Tooltip("Parry efektinin oynatma hizi (1 = normal, kucuk = daha akiskan/yavas).")]
    [SerializeField] private float parrySpeed = 0.75f;

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

        
    }

    public void PlayHealthHit(Vector3 position, Vector2 direction)
    {
        if (SpriteFxLibrary.Play("hit_health", position, direction))
            return;

        if (healthHitVFX == null)
        {
            Debug.LogWarning("HEALTH HIT VFX ASSIGNED DEĞİL!");
            return;
        }

        SpawnVFX(healthHitVFX, position, direction);
    }

    public void PlayBalanceHit(Vector3 position, Vector2 direction)
    {
        PlayerController pc = parryPlayer != null ? parryPlayer : (parryPlayer = FindAnyObjectByType<PlayerController>());
        float side = direction.x != 0f ? Mathf.Sign(direction.x) : 1f;
        if (pc != null && !Mathf.Approximately(position.x, pc.transform.position.x))
            side = Mathf.Sign(position.x - pc.transform.position.x);
        Vector3 corePos = position;

        if (balanceHitVFX == null)
        {
            SpriteFxLibrary.Play("hit_balance", position, direction);
            return;
        }

        SpawnVFX(balanceHitVFX, new Vector3(corePos.x + side * impactBackOffset, corePos.y, corePos.z), direction, impactScale, go =>
        {
            // Kok (Impact yayilma efekti) arkada ve buyuk; cekirdek (Light/Shafts) dusmanin ustunde kalir; Shafts Streched kucuk.
            float r = Mathf.Max(0.01f, go.transform.lossyScale.x);
            foreach (string n in new[] { "Light", "Shafts" })
            {
                Transform c = go.transform.Find(n);
                if (c == null) continue;
                c.position = corePos;
                c.localScale = Vector3.one * (impactCoreScale / r);
            }
            Transform sp = go.transform.Find("Shafts Streched");
            if (sp != null)
            {
                sp.position = corePos;
                sp.localScale = Vector3.one * (impactLineScale / r);
            }
        });
    }

    public void PlayParry(Vector3 position, Vector2 direction)
    {
        if (parryVariants != null && parryVariants.Length > 0)
        {
            GameObject v = parryVariants[0]; // hep mavi (Hit_1_Blue)
            if (v != null)
            {
                Vector3 vp = position;
                PlayerController pc = parryPlayer != null ? parryPlayer : (parryPlayer = FindAnyObjectByType<PlayerController>());
                if (pc != null)
                {
                    float sd = Mathf.Sign(position.x - pc.transform.position.x);
                    if (sd == 0f) sd = 1f;
                    vp.x += sd * parryOffset;
                }
                SpawnVFX(v, vp, Vector2.zero, parryScale);
                return;
            }
        }

        // Parry efekti: beyaz + parlak Impact02 prefab'i (atanmissa her seyden once bu oynar).
        if (parryUsesPrefab && parryVFX != null)
        {
            Vector3 fxPos = position;
            PlayerController pl = parryPlayer != null ? parryPlayer : (parryPlayer = FindAnyObjectByType<PlayerController>());
            if (pl != null)
            {
                float side = Mathf.Sign(position.x - pl.transform.position.x);
                if (side == 0f) side = 1f;
                fxPos.x += side * parryOffset;
            }
            SpawnVFX(parryVFX, fxPos, Vector2.zero);
            return;
        }

        if (SpriteFxLibrary.Play("parry", position, direction))
            return;

        if (proceduralParryVFX)
        {
            BalanceBreakFx.PlayParry(position);

            // Parry artik denge-kirilma efektinin beyaz hali; eski prefab (Impact) ayrica oynamaz.
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
        if (SpriteFxLibrary.Play("block", position, direction))
            return;

        if (blockVFX == null)
        {
            Debug.LogWarning("BLOCK VFX ASSIGNED DEĞİL!");
            return;
        }

        SpawnVFX(blockVFX, position, direction);
    }

    public void PlayBalanceBreak(Vector3 position, Vector2 direction)
    {
        if (SpriteFxLibrary.Play("balance_break", position, direction))
            return;

        if (balanceBreakVFX == null)
        {
            Debug.LogWarning("BALANCE BREAK VFX ASSIGNED DEĞİL!");
            return;
        }

        SpawnVFX(balanceBreakVFX, position, direction);
    }

    private readonly System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.Stack<PooledVfx>> pools =
        new System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.Stack<PooledVfx>>();

    private void SpawnVFX(GameObject prefab, Vector3 position, Vector2 direction, float scale = 1f, System.Action<GameObject> post = null)
    {
        Quaternion rotation = Quaternion.identity;

        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rotation = Quaternion.Euler(0f, 0f, angle);
        }

        System.Collections.Generic.Stack<PooledVfx> stack;

        if (!pools.TryGetValue(prefab, out stack))
        {
            stack = new System.Collections.Generic.Stack<PooledVfx>();
            pools[prefab] = stack;
        }

        PooledVfx fx = null;

        while (fx == null && stack.Count > 0)
            fx = stack.Pop();

        if (fx == null)
        {
            GameObject vfx = Instantiate(prefab, position, rotation);

            if (vfx == null)
            {
                Debug.LogError("VFX MANAGER: INSTANTIATE BAŞARISIZ!");
                return;
            }

            fx = vfx.GetComponent<PooledVfx>();

            if (fx == null)
                fx = vfx.AddComponent<PooledVfx>();

            fx.Setup(this, prefab, vfxSortingLayer, vfxSortingOrder);

            if (!Mathf.Approximately(scale, 1f))
            {
                vfx.transform.localScale = Vector3.one * scale;
                foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var m = ps.main;
                    m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    if (prefab != null && parryVariants != null && System.Array.IndexOf(parryVariants, prefab) >= 0) m.simulationSpeed = parrySpeed;
                }
            }
        }
        else
        {
            fx.transform.SetPositionAndRotation(position, rotation);
            fx.gameObject.SetActive(true);
        }

        if (post != null) post(fx.gameObject);
        fx.Restart();
    }

    internal void Release(PooledVfx fx)
    {
        if (fx == null)
            return;

        System.Collections.Generic.Stack<PooledVfx> stack;

        if (fx.Key != null && pools.TryGetValue(fx.Key, out stack))
        {
            fx.gameObject.SetActive(false);
            stack.Push(fx);
        }
        else
        {
            Destroy(fx.gameObject);
        }
    }
}

/// <summary>Havuzdaki vuruş efekti: oynar, süre dolunca kapanıp havuza döner (Destroy yok).</summary>
public class PooledVfx : MonoBehaviour
{
    public GameObject Key;

    private CombatVFXManager owner;
    private ParticleSystem[] systems;
    private VisualEffect[] effects;
    private float life = 2.2f;
    private float releaseAt;

    public void Setup(CombatVFXManager manager, GameObject key, string sortingLayer, int sortingOrder)
    {
        owner = manager;
        Key = key;

        ParticleSystemRenderer[] rs = GetComponentsInChildren<ParticleSystemRenderer>(true);

        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] == null) continue;
            rs[i].sortingLayerName = sortingLayer;
            rs[i].sortingOrder = sortingOrder;
        }

        systems = GetComponentsInChildren<ParticleSystem>(true);
        effects = GetComponentsInChildren<VisualEffect>(true);

        float longest = 0f;

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;

            var main = systems[i].main;
            float lifetime = main.duration;

            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                lifetime += main.startLifetime.constant;
            else
                lifetime += 2f;

            if (lifetime > longest) longest = lifetime;
        }

        if (effects.Length > 0 && longest < 2f) longest = 2f;
        if (longest <= 0f) longest = 2f;

        life = longest + 0.2f;
    }

    public void Restart()
    {
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            systems[i].Clear(true);
            systems[i].Play(true);
        }

        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] == null) continue;
            effects[i].Reinit();
            effects[i].Play();
        }

        releaseAt = Time.time + life;
    }

    private void Update()
    {
        if (Time.time < releaseAt) return;

        if (owner != null) owner.Release(this);
        else Destroy(gameObject);
    }
}
