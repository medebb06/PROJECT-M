using UnityEngine;

/// <summary>
/// GEÇİCİ: oyuncuya başlangıç yeteneğini verir (graybox testi).
/// Oyuncu sahne yüklendikten SONRA doğsa veya yeniden doğsa da çalışsın diye
/// yarım saniyede bir kontrol eder; yeteneği yoksa takar.
/// İleride 2 butonlu seçim ekranı bu değeri belirleyecek.
/// Kapatmak için active = false yap.
/// </summary>
public class StartingAbility : MonoBehaviour
{
    public static bool active = true;
    public static AbilityType type = AbilityType.DashSlash;

    private static StartingAbility instance;

    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        active = true;
        type = AbilityType.DashSlash;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
            return;

        GameObject obj = new GameObject("StartingAbility");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<StartingAbility>();
    }

    private void Update()
    {
        if (!active)
            return;

        if (Time.unscaledTime < nextCheck)
            return;

        nextCheck = Time.unscaledTime + 0.5f;

        PlayerController player = FindAnyObjectByType<PlayerController>();

        if (player == null)
            return;

        PlayerAbility ability = PlayerAbility.Ensure(player.gameObject);

        bool needsWind = type == AbilityType.WindStore || type == AbilityType.DashSlash;

        if (
            ability.HasAbility &&
            ability.Type == type &&
            (!needsWind || player.GetComponent<WindCharge>() != null)
        )
        {
            return;
        }

        ability.Equip(type);

        Debug.Log("StartingAbility: " + AbilityInfo.Name(type) + " verildi → " + player.name);
    }
}
