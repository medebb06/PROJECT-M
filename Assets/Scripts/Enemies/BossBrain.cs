using UnityEngine;

// Boss beyni (EnemyUnit üzerine): can bari, faz 2 geçişi (yeni EnemyDef'e geçer, can korunur).
// Bütün saldırılar EnemyDef verisinde; burada sadece faz ve arayüz var.
[RequireComponent(typeof(EnemyUnit))]
public class BossBrain : MonoBehaviour
{
    public string bossName = "RONIN";
    public EnemyDef phase2Def;
    [Range(0.1f, 0.9f)] public float phase2At = 0.5f;
    [Tooltip("Faz geçişinde saldırmadan bekleme (kükreme) süresi.")]
    public float roarPause = 1.4f;

    private EnemyUnit unit;
    private Health health;
    private bool phase2;
    private float barShown;   // yumuşak bar
    private float roarTextUntil;

    public bool InPhase2 { get { return phase2; } }

    private void Awake()
    {
        unit = GetComponent<EnemyUnit>();
        health = GetComponent<Health>();
    }

    private void Update()
    {
        if (health == null || health.IsDead) return;
        float f = Frac();
        if (!phase2 && phase2Def != null && f <= phase2At)
        {
            phase2 = true;
            unit.PhaseShift(phase2Def, roarPause);
            roarTextUntil = Time.unscaledTime + 1.6f;
        }
    }

    private float Frac()
    {
        return health.MaxHealth > 0 ? Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth) : 0f;
    }

    private static Texture2D white;

    private void OnGUI()
    {
        if (health == null) return;
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        float f = health.IsDead ? 0f : Frac();
        barShown = Mathf.MoveTowards(barShown <= 0f ? f : barShown, f, Time.unscaledDeltaTime * 0.6f);
        float w = Mathf.Min(Screen.width * 0.6f, 900f), h = 16f;
        float x = (Screen.width - w) * 0.5f, y = Screen.height - 70f;
        GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 6), white);
        GUI.color = new Color(1f, 0.85f, 0.6f, 0.8f); GUI.DrawTexture(new Rect(x, y, w * barShown, h), white);
        GUI.color = phase2 ? new Color(0.85f, 0.15f, 0.12f) : new Color(0.75f, 0.2f, 0.18f); GUI.DrawTexture(new Rect(x, y, w * f, h), white);
        GUI.color = new Color(1f, 1f, 1f, 0.35f); GUI.DrawTexture(new Rect(x + w * phase2At - 1, y, 2, h), white);
        GUI.color = Color.white;
        var st = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
        GUI.Label(new Rect(x, y - 30, w, 28), bossName + (phase2 ? "  —  ÖFKE" : ""), st);
        if (Time.unscaledTime < roarTextUntil)
        {
            var big = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.color = new Color(1f, 0.3f, 0.2f, Mathf.Clamp01((roarTextUntil - Time.unscaledTime) / 0.5f));
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 60), "ÖFKE", big);
            GUI.color = Color.white;
        }
    }
}
