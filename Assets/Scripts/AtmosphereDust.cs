using UnityEngine;

/// <summary>
/// IŞIK TOZU (sadece Play'de): havada süzülen ince tozlar + birkaç büyük, çok soluk bulanık ışık topu.
/// Kameranın etrafında doğar, dünyada kalır (kamera geçerken parallax hissi verir).
/// Rengi ve miktarı TimeOfDay verir. Ayarlar: bu bileşenin alanları.
/// </summary>
[DefaultExecutionOrder(10050)]
public class AtmosphereDust : MonoBehaviour
{
    public Shader shader;
    public Texture2D glow;

    [Header("İnce toz")]
    public int speckMax = 220;
    public float speckRate = 16f;
    public Vector2 speckSize = new Vector2(0.02f, 0.055f);
    [Range(0f, 1f)] public float speckAlpha = 0.7f;
    public int speckOrder = 440;

    [Header("Küçük çekirdek + geniş yumuşak ışıma")]
    public int haloMax = 90;
    public float haloRate = 5f;
    public Vector2 haloSize = new Vector2(0.10f, 0.30f);
    [Range(0f, 1f)] public float haloAlpha = 0.07f;
    public int haloOrder = 470;

    [Header("Hareket")]
    [Tooltip("Rüzgâr yönünde sürüklenme (birim/sn).")]
    public Vector2 drift = new Vector2(0.18f, 0.02f);
    public float turbulence = 0.35f;

    private ParticleSystem speck, halo;
    private Material mat;
    private Color color = new Color(1f, 0.9f, 0.75f);
    private float amount = 1f;
    private Color appliedColor;
    private float appliedAmount = -1f;

    public void SetLook(Color c, float amt)
    {
        color = c;
        amount = amt;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        Build();
    }

    private void OnDisable()
    {
        if (speck != null) Destroy(speck.gameObject);
        if (halo != null) Destroy(halo.gameObject);
        if (mat != null) Destroy(mat);
        speck = halo = null;
        mat = null;
    }

    private ParticleSystem Make(string name, int max, float rate, Vector2 size, int order, float life0, float life1)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.maxParticles = max;

        var em = ps.emission;
        em.rateOverTime = rate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(drift.x * 0.4f, drift.x * 1.4f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.03f + drift.y, 0.08f + drift.y);
        vel.z = 0f;

        var nz = ps.noise;
        nz.enabled = true;
        nz.strength = turbulence;
        nz.frequency = 0.22f;
        nz.scrollSpeed = 0.1f;
        nz.damping = true;
        nz.quality = ParticleSystemNoiseQuality.Low;

        // Doğarken ve ölürken yumuşakça belirir; ortada hafifçe pırıldar.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.22f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = order;

        ps.Play();
        return ps;
    }

    private void Build()
    {
        if (shader == null || glow == null)
            return;

        mat = new Material(shader) { name = "Dust (runtime)" };
        mat.mainTexture = glow;

        speck = Make("~dust speck", speckMax, speckRate, speckSize, speckOrder, 9f, 15f);
        halo = Make("~dust halo", haloMax, haloRate, haloSize, haloOrder, 14f, 22f);
    }

    private void Apply()
    {
        if (speck == null)
            return;

        if (Mathf.Approximately(amount, appliedAmount) && color == appliedColor)
            return;

        appliedAmount = amount;
        appliedColor = color;

        SetSystem(speck, speckRate, speckAlpha);
        SetSystem(halo, haloRate, haloAlpha);
    }

    private void SetSystem(ParticleSystem ps, float baseRate, float alpha)
    {
        var main = ps.main;
        Color a = new Color(color.r, color.g, color.b, alpha * 0.45f);
        Color b = new Color(color.r, color.g, color.b, alpha);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);

        var em = ps.emission;
        em.rateOverTime = baseRate * Mathf.Clamp(amount, 0f, 3f);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || speck == null)
            return;

        Camera cam = Camera.main;

        if (cam != null)
        {
            float h = cam.orthographic ? cam.orthographicSize * 2f : 20f;
            float w = h * Mathf.Max(1.2f, cam.aspect);
            Vector3 p = cam.transform.position;

            foreach (ParticleSystem ps in new[] { speck, halo })
            {
                ps.transform.position = new Vector3(p.x, p.y, 0f);
                var sh = ps.shape;
                sh.scale = new Vector3(w * 1.25f, h * 1.05f, 0.1f);
            }
        }

        Apply();
    }
}
