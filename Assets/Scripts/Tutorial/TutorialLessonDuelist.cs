using UnityEngine;

// Tutorial sahnesi icin: yerlestirilmis bir Duelist ile BLOCK -> PARRY -> DODGE (dash) dersi.
// TutorialManager (ana menu TUTORIAL modu) ile ayni yaklasim: saldiri vurusuna yaklasirken zaman
// yavaslar / durur, ekranda hangi tusa basilacagi yazar. Mevcut sistemlere dokunmaz, sadece
// onlarin public API'lerini kullanir. Ders bitince Duelist serbest kalir; olunce kapi acilir.
public class TutorialLessonDuelist : MonoBehaviour
{
    public EnemyController enemy;
    public GameObject gateToOpen;
    public float triggerDistance = 30f;
    public float enemyDistance = 7f;

    public float slowStartWindup = 0.8f;
    public float blockFreezeWindup = 0.45f;
    public float parryFreezeWindup = 0.10f;
    public float dodgeFreezeWindup = 0.30f;
    public float slowScale = 0.12f;

    enum Lesson { Waiting, Block, Parry, Dodge, Free }
    enum Phase { WaitAttack, Slowing, Frozen, Resolving, Gap }

    Lesson lesson = Lesson.Waiting;
    Phase phase = Phase.WaitAttack;

    PlayerController player;
    Health pHealth, eHealth;
    float phaseTimer, waitTimer, passiveUntil;
    int hpBefore;
    bool gotParry, gotBlock, dashSeen, retry, gateOpened;
    string promptTitle = "", promptSub = "";
    string toast; float toastUntil;
    GUIStyle big, small;

    void OnEnable()
    {
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.PlayerBlocked += OnBlocked;
    }

    void OnDisable()
    {
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.PlayerBlocked -= OnBlocked;
        RunCameraZoom.ForceCinematic = false;
    }

    void OnParry(EnemyController e, bool broke) { gotParry = true; }
    void OnBlocked(EnemyController e, bool b) { gotBlock = true; }

    void Update()
    {
        if (MainMenu.Active) return;

        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;
            pHealth = player.GetComponent<Health>();
        }

        if (enemy == null)
        {
            OpenGate();
            return;
        }

        if (eHealth == null) eHealth = enemy.GetComponent<Health>();
        if (eHealth != null && eHealth.IsDead)
        {
            RunCameraZoom.ForceCinematic = false;
            OpenGate();
            return;
        }

        if (lesson == Lesson.Free) return;

        // Dersler boyunca Duelist olmesin.
        if (eHealth != null && eHealth.CurrentHealth < eHealth.MaxHealth)
            eHealth.Heal(eHealth.MaxHealth);

        if (lesson == Lesson.Waiting)
        {
            // Oyuncu yaklasana kadar Duelist saldirmasin.
            enemy.StartAttackRecovery(5f);
            float d = Mathf.Abs(player.transform.position.x - enemy.transform.position.x);
            if (d < triggerDistance)
            {
                lesson = Lesson.Block;
                phase = Phase.WaitAttack;
                passiveUntil = Time.unscaledTime + 1.5f;
                phaseTimer = 0f;
            }
            return;
        }

        if (pHealth != null && !pHealth.IsDead && pHealth.CurrentHealth < pHealth.MaxHealth * 0.2f)
            pHealth.Heal(pHealth.MaxHealth);

        bool passive = Time.unscaledTime < passiveUntil;
        if (passive) enemy.StartAttackRecovery(5f);

        phaseTimer += Time.unscaledDeltaTime;
        enemy.alwaysHunt = true;

        if (player.isDashing) dashSeen = true;

        EnemyAttackState atk = enemy.CurrentState as EnemyAttackState;
        bool normalAtk = atk != null && atk.CurrentHitType == MoveHitType.Normal && !atk.IsUnblockable;
        float freezeAt = lesson == Lesson.Parry ? parryFreezeWindup : (lesson == Lesson.Dodge ? dodgeFreezeWindup : blockFreezeWindup);

        if (phase == Phase.WaitAttack)
        {
            waitTimer += Time.unscaledDeltaTime;
            if (waitTimer > 3.5f && !passive)
            {
                waitTimer = 0f;
                PlaceEnemyInFront();
            }
            if (atk != null) waitTimer = 0f;
        }
        else waitTimer = 0f;

        switch (phase)
        {
            case Phase.WaitAttack:
                RunCameraZoom.ForceCinematic = false;
                if (!passive && normalAtk && atk.RemainingWindup > 0f && atk.RemainingWindup <= slowStartWindup)
                {
                    phase = Phase.Slowing; phaseTimer = 0f;
                    gotParry = gotBlock = false; dashSeen = false;
                    hpBefore = pHealth != null ? pHealth.CurrentHealth : 0;
                }
                break;

            case Phase.Slowing:
                RunCameraZoom.ForceCinematic = true;
                if (!normalAtk) { phase = Phase.WaitAttack; break; }
                HitStop.Request(0.12f, slowScale, 90);
                if (atk.RemainingWindup <= freezeAt)
                {
                    phase = Phase.Frozen; phaseTimer = 0f;
                    if (lesson == Lesson.Block) Prompt("BLOCK:  RMB BASILI TUT", "sag fare tusu");
                    else if (lesson == Lesson.Parry) Prompt("PARRY:  SON ANDA RMB'YE BAS", "vurustan hemen once");
                    else Prompt("DODGE:  SHIFT = DASH", "saldirinin icinden gec");
                }
                break;

            case Phase.Frozen:
                RunCameraZoom.ForceCinematic = true;
                HitStop.Request(0.12f, 0f, 90);
                bool pressed = lesson == Lesson.Dodge
                    ? MobileInput.GetKeyDown(KeyCode.LeftShift)
                    : MobileInput.GetMouseButtonDown(1);
                if (pressed)
                {
                    promptTitle = ""; promptSub = "";
                    phase = Phase.Resolving; phaseTimer = 0f;
                }
                break;

            case Phase.Resolving:
            {
                RunCameraZoom.ForceCinematic = true;
                bool hurt = pHealth != null && pHealth.CurrentHealth < hpBefore;
                bool ok;
                if (lesson == Lesson.Parry) ok = gotParry;
                else if (lesson == Lesson.Block) ok = gotBlock || gotParry;
                else ok = dashSeen && !hurt && phaseTimer > 0.6f;

                if (ok)
                {
                    SetToast(lesson == Lesson.Parry ? "MUKEMMEL PARRY!" : (lesson == Lesson.Block ? "BLOK!" : "KACTIN!"));
                    passiveUntil = Time.unscaledTime + 1.6f;
                    phase = Phase.Gap; phaseTimer = 0f; retry = false;
                }
                else if (hurt || phaseTimer > 2.2f)
                {
                    SetToast("TEKRAR DENE");
                    if (pHealth != null) pHealth.Heal(pHealth.MaxHealth);
                    passiveUntil = Time.unscaledTime + 1.2f;
                    phase = Phase.Gap; phaseTimer = 0f; retry = true;
                }
                break;
            }

            case Phase.Gap:
                if (phaseTimer > 0.6f) RunCameraZoom.ForceCinematic = false;
                if (phaseTimer > 1.6f)
                {
                    phase = Phase.WaitAttack; phaseTimer = 0f;
                    if (!retry)
                    {
                        if (lesson == Lesson.Block) lesson = Lesson.Parry;
                        else if (lesson == Lesson.Parry) lesson = Lesson.Dodge;
                        else
                        {
                            lesson = Lesson.Free;
                            RunCameraZoom.ForceCinematic = false;
                            SetToast("DERS BITTI - DUELLOYU KAZAN");
                        }
                    }
                    retry = false;
                }
                break;
        }
    }

    void PlaceEnemyInFront()
    {
        if (enemy == null || player == null) return;
        float f = player.facingDir < 0f ? -1f : 1f;
        Vector3 p = player.transform.position;
        Vector3 e = enemy.transform.position;
        enemy.transform.position = new Vector3(p.x + f * enemyDistance, e.y, e.z);
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void OpenGate()
    {
        if (gateOpened) return;
        gateOpened = true;
        if (gateToOpen != null) gateToOpen.SetActive(false);
        SetToast("KAPI ACILDI - BOSS'A ILERLE");
    }

    void Prompt(string title, string sub) { promptTitle = title; promptSub = sub; }
    void SetToast(string t) { toast = t; toastUntil = Time.unscaledTime + 3f; }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (MainMenu.Active) return;
        bool showPrompt = promptTitle.Length > 0;
        bool showToast = toast != null && Time.unscaledTime < toastUntil;
        if (!showPrompt && !showToast) return;

        float s = Screen.height / 1080f;
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
        big.fontSize = Mathf.RoundToInt(18f * s);
        small.fontSize = Mathf.RoundToInt(13f * s);

        if (showPrompt)
        {
            Shadow(new Rect(0f, Screen.height * 0.66f, Screen.width, 40f * s), promptTitle, big, Color.white);
            Shadow(new Rect(0f, Screen.height * 0.66f + 36f * s, Screen.width, 30f * s), promptSub, small, new Color(0.7f, 0.85f, 1f, 0.9f));
        }
        if (showToast)
            Shadow(new Rect(0f, Screen.height * 0.30f, Screen.width, 40f * s), toast, big, new Color(0.6f, 0.85f, 1f, 1f));
        GUI.color = Color.white;
    }

    static void Shadow(Rect r, string text, GUIStyle st, Color c)
    {
        st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.8f);
        Rect o = r; o.x += 1.5f; o.y += 1.5f;
        GUI.Label(o, text, st);
        st.normal.textColor = c;
        GUI.Label(r, text, st);
    }
}
