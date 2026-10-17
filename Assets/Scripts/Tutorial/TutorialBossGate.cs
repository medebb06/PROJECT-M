using UnityEngine;

// Tutorial sahnesi icin: boss olunce kapi nesnesi acilir (kapanir) ve mesaj gosterilir.
// Boss1 Health bilesenini Play'de kendisi ekledigi icin, boss referansi bos ise calisirken aranir.
// Bu script kapi nesnesinin KENDISINE konmamali (kapi kapaninca script de durur).
public class TutorialBossGate : MonoBehaviour
{
    public Health boss;
    public GameObject gate;
    public float showMessageSeconds = 6f;

    bool opened;
    bool bossSeen;
    float openedAt;

    void Update()
    {
        if (opened) return;

        if (boss == null)
        {
            if (bossSeen) { Open(); return; }

            Boss1Enemy b = FindAnyObjectByType<Boss1Enemy>();
            if (b == null) return;
            boss = b.GetComponent<Health>();
            if (boss == null) return;
            bossSeen = true;
            return;
        }

        bossSeen = true;
        if (boss.IsDead) Open();
    }

    void Open()
    {
        opened = true;
        openedAt = Time.unscaledTime;
        if (gate != null) gate.SetActive(false);
    }

    void OnGUI()
    {
        if (!opened || Time.unscaledTime - openedAt > showMessageSeconds) return;
        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.fontSize = Mathf.RoundToInt(16f * Screen.height / 1080f);
        s.alignment = TextAnchor.MiddleCenter;
        s.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), "BOSS YENILDI - TUTORIAL BITTI", s);
    }
}
