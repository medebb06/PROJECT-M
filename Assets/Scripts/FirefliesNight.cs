using UnityEngine;

/// <summary>
/// ATESBOCEKLERI: gece yavasca suzulen, yanip sonen kucuk isiklar. Gunduz kapanir.
/// Parcacik sistemini kendisi kurar (Play'de ve Edit'te). TimeOfDay saatine gore acilir.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
public class FirefliesNight : MonoBehaviour
{
    public float maxRate = 7f;
    public Color color = new Color(1f, 0.2f, 0.05f, 1f);      // kirmizi
    public Color colorB = new Color(1f, 0.85f, 0.15f, 1f);    // sari (her ates bocegi ikisi arasinda rastgele bir renk alir)
    public float areaWidth = 46f;
    public float areaHeight = 7f;
    public float size = 0.2f;

    private ParticleSystem ps;
    private TimeOfDay tod;
    private bool built;

    private void OnEnable() { built = false; }

    private void Build()
    {
        ps = GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.maxParticles = 80;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 13f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(color, colorB);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = maxRate;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(areaWidth, areaHeight, 0.1f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f); vel.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.22f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.7f; noise.frequency = 0.25f; noise.scrollSpeed = 0.3f; noise.damping = true;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.35f, 0.3f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.3f, 0.6f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        built = true;
    }

    private void Update()
    {
        if (!built) Build();
        if (tod == null) tod = FindObjectOfType<TimeOfDay>();
        float night = 1f;
        if (tod != null)
        {
            float h = tod.hour;
            // 18-19.5 arasi acilir, 4.5-6 arasi kapanir
            float dusk = Mathf.Clamp01((h - 18f) / 1.5f);
            float dawn = Mathf.Clamp01((6f - h) / 1.5f);
            night = (h >= 12f) ? dusk : dawn;
        }
        var em = ps.emission; em.rateOverTime = maxRate * night;
        if (!Application.isPlaying && night <= 0.01f) { /* edit modunda gunduz bos kalir */ }
    }
}
