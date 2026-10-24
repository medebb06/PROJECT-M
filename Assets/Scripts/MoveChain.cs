using UnityEngine;

/// <summary>
/// HAREKET ZİNCİRİ: dash, infaz kayması ve parry atılması art arda gelince mesafeler toplanıp
/// kontrolü zorlaştırmasın. Her hareket, bir öncekinin (tahmini) bitişinden sonra kısa pencere içinde
/// başlarsa mesafesi azalır (1 -> 0.8 -> 0.65 -> 0.55). Pencere dışında her hareket tam güçtedir.
/// Hız tavanı ve ortak çıkış hızı da burada, tek yerden ayarlanır.
/// </summary>
public static class MoveChain
{
    public static bool Enabled = true;

    [Tooltip("Bir hareket bittikten sonra bu süre (sn) içinde başlayan hareket zincir sayılır.")]
    public static float ChainWindow = 0.35f;

    /// <summary>Zincirdeki sıraya göre mesafe çarpanı (0. = tam güç).</summary>
    public static readonly float[] Scale = { 1f, 0.88f, 0.75f, 0.65f };

    /// <summary>Dash iptali / parry atılması bittiğinde ortak çıkış hızı (koşu hızının oranı).</summary>
    public static float ExitSpeedMult = 0.75f;

    /// <summary>Dash ve infaz kayması DIŞINDAKİ yatay hız tavanı (birim/sn). Kazara birikmeye karşı emniyet.</summary>
    public static float SpeedCap = 40f;

    private static int depth;
    private static float busyUntil = -99f;
    private static float lastBegin = -99f;

    /// <summary>Yeni bir hareket başlarken çağır. Mesafe çarpanını (0..1) döndürür.</summary>
    public static float Begin(float expectedDuration)
    {
        if (!Enabled)
            return 1f;

        float now = Time.unscaledTime;

        // Aynı karede ikinci çağrı (örn. dash, kaymayı keserken) zinciri iki kez ilerletmesin.
        if (now - lastBegin < 0.02f)
            return Scale[depth];

        if (now <= busyUntil + ChainWindow)
            depth = Mathf.Min(depth + 1, Scale.Length - 1);
        else
            depth = 0;

        lastBegin = now;
        busyUntil = now + Mathf.Max(0.01f, expectedDuration);

        return Scale[depth];
    }

    // ---- MESAFE BÜTÇESİ: dash + saldırı atılması art arda gelince toplam kayma sınırlı kalır. ----
    // Bütçe zamanla dolar; tek bir dash tam güçte kalır, spamda mesafe bütçenin elverdiği kadar olur.
    public static float Budget = 18f;     // tam doluyken harcanabilecek toplam mesafe (birim)
    public static float Regen = 11f;      // saniyede dolan mesafe (birim/sn)
    private static float budgetNow = 18f;
    private static float budgetTime = -99f;

    /// <summary>İstenen kayma mesafesini ister; bütçenin izin verdiğini (en az minGrant) döndürür ve bütçeden düşer.</summary>
    public static float Spend(float want, float minGrant)
    {
        if (!Enabled || want <= 0f)
            return want;

        float now = Time.unscaledTime;
        budgetNow = Mathf.Min(Budget, budgetNow + Mathf.Max(0f, now - budgetTime) * Regen);
        budgetTime = now;

        float granted = Mathf.Min(want, Mathf.Max(minGrant, budgetNow));
        budgetNow = Mathf.Max(0f, budgetNow - granted);
        return granted;
    }

    public static int Depth { get { return depth; } }
}
