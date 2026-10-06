using UnityEngine;

/// <summary>
/// TEST: kendi kendini kurar. F9 ya da 0 (sıfır) tuşu = Gölge Hilali boss'unu doğurur.
/// </summary>
public class BossTestHotkey : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        GameObject go = new GameObject("BossTestHotkey");
        DontDestroyOnLoad(go);
        go.AddComponent<BossTestHotkey>();

        Debug.Log("BossTestHotkey hazır: F9 veya 0 = Gölge Hilali");
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F9) && !Input.GetKeyDown(KeyCode.Alpha0))
            return;

        RunManager rm = RunManager.Instance;

        if (rm == null)
        {
            Debug.LogWarning("BossTestHotkey: sahnede RunManager yok.");
            return;
        }

        Debug.Log("BossTestHotkey: Gölge Hilali doğuruluyor");

        rm.DebugSpawnTrialBoss();
    }
}
