using UnityEngine;

/// <summary>
/// BOSS ÖZEL YETENEK KAPISI: Zıpla-Ez ve Zemin Dalgası art arda aynı
/// yeteneği spamlamasın, iki özel yetenek arasında nefes payı olsun.
/// </summary>
public static class BossSkillGate
{
    /// <summary>Aynı yeteneğin tekrar kullanılabilmesi için en az süre (sn).</summary>
    public const float SameSkillGap = 12f;

    /// <summary>Herhangi iki özel yetenek arası en az süre (sn).</summary>
    public const float AnySkillGap = 3.5f;

    private static string lastId = "";
    private static float lastEnd = -999f;
    private static bool running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lastId = "";
        lastEnd = -999f;
        running = false;
    }

    public static bool CanStart(string id)
    {
        if (running)
            return false;

        if (Time.time - lastEnd < AnySkillGap)
            return false;

        if (lastId == id && Time.time - lastEnd < SameSkillGap)
            return false;

        return true;
    }

    /// <summary>Çalışan (ya da son çalışan) özel yetenek adı.</summary>
    public static string CurrentId => lastId;

    public static bool IsRunning => running;

    /// <summary>Özel yetenekleri 's' sn engelle (faz geçişi vb.).</summary>
    public static void Block(float seconds)
    {
        lastEnd = Mathf.Max(lastEnd, Time.time + seconds - AnySkillGap);
    }

    public static void Begin(string id)
    {
        running = true;
        lastId = id;
    }

    public static void End(string id)
    {
        running = false;
        lastId = id;
        lastEnd = Time.time;
    }
}
