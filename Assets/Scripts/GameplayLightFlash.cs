using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// OYNANIŞA BAĞLI IŞIK ÇAKMALARI: parry, denge kırma, 4. vuruş, infaz, öldürme, odak yeteneği.
/// Kısa ömürlü 2D nokta ışığı: hızla parlar, yumuşakça söner (ağır çekimde de gerçek zamanla).
/// Sahneye eklemek gerekmez; CombatEvents'e kendiliğinden bağlanır.
/// </summary>
public static class GameplayLightFlash
{
    private static Runner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (runner != null) return;

        GameObject go = new GameObject("~GameplayLightFlash");
        go.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();

        // Isimli metodlar: -= ile once cikarilir, play'ler arasi cift abonelik olmaz.
        CombatEvents.ParrySucceeded -= OnParry; CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.BalanceBroken -= OnBroken; CombatEvents.BalanceBroken += OnBroken;
        CombatEvents.EnemyKilled -= OnKilled; CombatEvents.EnemyKilled += OnKilled;
        CombatEvents.EnemyHit -= OnHit; CombatEvents.EnemyHit += OnHit;
    }

    private static void OnParry(EnemyBase e, bool broke) =>
        Burst(Mid(e), new Color(0.75f, 0.88f, 1f), broke ? 3.2f : 2.4f, broke ? 11f : 8f, 0.28f);

    private static void OnBroken(EnemyBase e) =>
        Burst(Pos(e), new Color(1f, 0.78f, 0.5f), 2.2f, 9f, 0.32f);

    private static void OnKilled(EnemyBase e) =>
        Burst(Pos(e), new Color(1f, 0.6f, 0.35f), 1.6f, 7f, 0.3f);

    private static void OnHit(EnemyBase e, DamageInfo info, HitResult res)
    {
        if (info.source == DamageSource.Attack && info.comboStep >= 4)
            Burst(Pos(e), new Color(1f, 0.86f, 0.62f), 2.6f, 11f, 0.38f);
    }

    private static Vector2 Pos(EnemyBase e)
    {
        return e != null ? (Vector2)e.transform.position + Vector2.up * 1.6f : Vector2.zero;
    }

    private static Vector2 Mid(EnemyBase e)
    {
        PlayerController p = Object.FindFirstObjectByType<PlayerController>();
        Vector2 a = Pos(e);
        return p != null ? Vector2.Lerp(a, (Vector2)p.transform.position + Vector2.up * 1.6f, 0.5f) : a;
    }

    /// <summary>Kısa ışık çakması. duration gerçek zaman (sn).</summary>
    public static void Burst(Vector2 pos, Color color, float intensity, float radius, float duration)
    {
        if (runner == null || !Application.isPlaying) return;
        runner.StartCoroutine(Run(pos, color, intensity, radius, duration));
    }

    private static IEnumerator Run(Vector2 pos, Color color, float intensity, float radius, float duration)
    {
        GameObject go = new GameObject("~Flash");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        Light2D l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = radius * 0.08f;
        l.falloffIntensity = 0.6f;
        l.shadowsEnabled = false;
        l.color = color;

        float t = 0f;
        float up = Mathf.Min(0.04f, duration * 0.2f);

        while (t < duration && l != null)
        {
            t += Time.unscaledDeltaTime;
            float k = t < up ? t / up : 1f - Mathf.SmoothStep(0f, 1f, (t - up) / Mathf.Max(0.001f, duration - up));
            l.intensity = intensity * k;
            l.pointLightOuterRadius = radius * (0.85f + 0.15f * k);
            yield return null;
        }

        if (go != null) Object.Destroy(go);
    }

    private class Runner : MonoBehaviour { }
}
