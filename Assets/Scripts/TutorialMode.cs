using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Ana menüdeki TUTORIAL seçeneği bunu açar.</summary>
public static class TutorialMode
{
    public static bool Active;
}

/// <summary>
/// TUTORIAL: sabit düşman → SALDIR → BLOCK → PARRY → RAGE → bitince ana menü.
/// Öğretilen an geldiğinde zaman yavaşlar/durur, sinematik kamera oyuncuya yaklaşır, ekrana tuş yazar.
/// Oyun başlayınca kendiliğinden kurulur; sadece TutorialMode.Active iken çalışır.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    enum Step { Attack, Block, Parry, Rage, Done }
    enum Phase { Prompt, Doing, WaitAttack, Slowing, Frozen, Resolving, Gap }

    [Header("Ayarlar")]
    public int hitsToPass = 3;
    [Tooltip("Saldırı bu kadar sn kala yavaşlama başlar.")]
    public float slowStartWindup = 0.8f;
    [Tooltip("BLOCK öğretisinde saldırı bu kadar sn kala zaman durur (parry penceresinden uzun).")]
    public float blockFreezeWindup = 0.45f;
    [Tooltip("PARRY öğretisinde saldırı bu kadar sn kala zaman durur (vuruştan hemen önce).")]
    public float parryFreezeWindup = 0.10f;
    public float slowScale = 0.12f;
    public float enemyDistance = 3.2f;
    public float focusZoom = 0.22f;

    Step step;
    Phase phase;
    PlayerController player;
    Health pHealth;
    EnemyController enemy;
    Health eHealth;
    GameObject prefab;
    bool prefabSearched;

    bool wasRunning, started;
    bool passive;
    float passiveUntil;
    int hits;
    float phaseTimer;
    float cleanTimer;
    string toast; float toastUntil;
    float doneTimer;
    bool leaving;

    bool gotParry, gotBlock;
    int hpBefore;
    bool rageStarted;
    bool savedUnlimited, hadSavedUnlimited;

    string promptTitle = "", promptSub = "";
    GUIStyle big, small, top;
    Font fontBold, fontRegular;
    const int FontPt = 10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<TutorialManager>() != null) return;
        var go = new GameObject("TutorialManager");
        DontDestroyOnLoad(go);
        go.AddComponent<TutorialManager>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.PlayerBlocked += OnBlocked;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.PlayerBlocked -= OnBlocked;
        Cleanup();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) { ResetAll(); }

    void ResetAll()
    {
        started = false; wasRunning = false; leaving = false;
        player = null; pHealth = null; enemy = null; eHealth = null;
        Cleanup();
    }

    void Cleanup()
    {
        RunCameraZoom.ForceCinematic = false;
        if (hadSavedUnlimited) { RageMeter.Unlimited = savedUnlimited; hadSavedUnlimited = false; }
    }

    void OnEnemyHit(EnemyController e, DamageInfo info, HitResult r)
    {
        if (TutorialMode.Active && step == Step.Attack && phase == Phase.Doing && e == enemy) hits++;
    }

    void OnParry(EnemyController e, bool broke) { gotParry = true; }
    void OnBlocked(EnemyController e, bool b) { gotBlock = true; }

    bool Running() { return TutorialMode.Active && !MainMenu.Active; }

    // ------------------------------------------------------------ akış

    void Update()
    {
        if (!TutorialMode.Active)
        {
            if (wasRunning) { wasRunning = false; Cleanup(); }
            return;
        }

        if (MainMenu.Active) return;
        if (leaving) return;

        wasRunning = true;

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
            pHealth = player.GetComponent<Health>();
        }

        if (!hadSavedUnlimited) { savedUnlimited = RageMeter.Unlimited; hadSavedUnlimited = true; }
        RageMeter.Unlimited = true;

        if (!started) Begin();

        // Başka (sahnede kalan / başka mod) düşmanları temizle.
        cleanTimer -= Time.unscaledDeltaTime;
        if (cleanTimer <= 0f) { cleanTimer = 0.5f; ClearStrangers(); }

        // Oyuncu ölürse tamamen iyileş (tutorial ölmez).
        if (pHealth != null && !pHealth.IsDead && pHealth.CurrentHealth < pHealth.MaxHealth * 0.2f)
            pHealth.Heal(pHealth.MaxHealth);

        if (enemy == null || (eHealth != null && eHealth.IsDead))
        {
            if (step != Step.Done) SpawnEnemy();
        }

        // Pasifken düşman saldırmasın.
        bool nowPassive = passive || Time.unscaledTime < passiveUntil;
        if (enemy != null)
        {
            if (nowPassive) enemy.StartAttackRecovery(5f);
            else if (wasPassive) enemy.StartAttackRecovery(0.3f);   // pasiflikten cikinca beklemeden saldirsin
        }
        wasPassive = nowPassive;

        // Saldırı öğretisinde düşman ölmesin.
        if (enemy != null && eHealth != null && !eHealth.IsDead && step == Step.Attack)
            eHealth.Heal(eHealth.MaxHealth);

        phaseTimer += Time.unscaledDeltaTime;

        switch (step)
        {
            case Step.Attack: UpdateAttack(); break;
            case Step.Block: UpdateDefense(false); break;
            case Step.Parry: UpdateDefense(true); break;
            case Step.Rage: UpdateRage(); break;
            case Step.Done: UpdateDone(); break;
        }
    }

    void Begin()
    {
        started = true;
        ClearStrangers();
        SpawnEnemy();
        SetStep(Step.Attack);
        Freeze(MobileInput.Txt("PRESS LMB TO ATTACK", "TAP VUR TO ATTACK"), MobileInput.Txt("left mouse button", "big button, bottom right"));
        phase = Phase.Prompt;
    }

    void SetStep(Step s)
    {
        step = s; phaseTimer = 0f; hits = 0;
        gotParry = gotBlock = false;
    }

    // ---- Adım 1: saldır ----
    void UpdateAttack()
    {
        passive = true;

        if (phase == Phase.Prompt)
        {
            FreezeTime();
            if (MobileInput.GetMouseButtonDown(0))
            {
                phase = Phase.Doing;
                promptTitle = ""; promptSub = "";
                RunCameraZoom.ForceCinematic = false;
            }
            return;
        }

        promptTitle = "";
        promptSub = "";
        if (hits >= hitsToPass)
        {
            Toast("NICE!");
            ResetEnemy();
            passive = false;
            passiveUntil = Time.unscaledTime + 1.0f;
            SetStep(Step.Block);
            phase = Phase.WaitAttack;
        }
    }

    // ---- Adım 2/3: block ve parry ----
    void UpdateDefense(bool parry)
    {
        passive = false;
        EnemyAttackState atk = enemy != null ? enemy.CurrentState as EnemyAttackState : null;
        bool normalAtk = atk != null && atk.CurrentHitType == MoveHitType.Normal && !atk.IsUnblockable;
        float freezeAt = parry ? parryFreezeWindup : blockFreezeWindup;

        // Dusman oyuncuyu aktif ARASIN (menzil/standby yuzunden saldirmama sorunu).
        if (enemy != null) enemy.alwaysHunt = true;

        if (phase == Phase.WaitAttack)
        {
            waitTimer += Time.unscaledDeltaTime;
            // Uzun sure saldirmazsa dusmani oyuncunun onune getir.
            if (waitTimer > 3.5f && enemy != null && !passive && Time.unscaledTime >= passiveUntil)
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
                if (normalAtk && atk.RemainingWindup > 0f && atk.RemainingWindup <= slowStartWindup)
                {
                    phase = Phase.Slowing; phaseTimer = 0f;
                    gotParry = gotBlock = false;
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
                    if (parry) Freeze(MobileInput.Txt("TAP RMB AT THE LAST MOMENT", "TAP PARRY AT THE LAST MOMENT"), "PARRY!");
                    else Freeze(MobileInput.Txt("HOLD RMB TO BLOCK", "HOLD PARRY TO BLOCK"), MobileInput.Txt("right mouse button", "PARRY button"));
                }
                break;

            case Phase.Frozen:
                RunCameraZoom.ForceCinematic = true;
                FreezeTime();
                if (MobileInput.GetMouseButtonDown(1))
                {
                    promptTitle = ""; promptSub = "";
                    phase = Phase.Resolving; phaseTimer = 0f;
                }
                break;

            case Phase.Resolving:
            {
                RunCameraZoom.ForceCinematic = true;
                bool hurt = pHealth != null && pHealth.CurrentHealth < hpBefore;
                bool ok = parry ? gotParry : (gotBlock || gotParry);

                if (ok)
                {
                    Toast(parry ? "PERFECT PARRY!" : "BLOCKED!");
                    passiveUntil = Time.unscaledTime + 1.6f;
                    phase = Phase.Gap; phaseTimer = 0f;
                }
                else if (hurt || phaseTimer > 2.2f)
                {
                    Toast("TRY AGAIN");
                    if (pHealth != null) pHealth.Heal(pHealth.MaxHealth);
                    passiveUntil = Time.unscaledTime + 1.2f;
                    phase = Phase.Gap; phaseTimer = 0f;
                    retry = true;
                }
                break;
            }

            case Phase.Gap:
                if (phaseTimer > 0.6f) RunCameraZoom.ForceCinematic = false;
                if (phaseTimer > 1.6f)
                {
                    if (retry)
                    {
                        retry = false;
                        phase = Phase.WaitAttack; phaseTimer = 0f;
                    }
                    else if (step == Step.Block)
                    {
                        SetStep(Step.Parry);
                        phase = Phase.WaitAttack;
                    }
                    else
                    {
                        EnterRage();
                    }
                }
                break;
        }
    }

    bool retry, wasPassive;
    float waitTimer;

    // ---- Adım 4: rage ----
    void EnterRage()
    {
        SetStep(Step.Rage);
        rageStarted = false;
        passive = true;
        ResetEnemy();
        PlaceEnemyInFront();
        phase = Phase.Prompt;
        Freeze(MobileInput.Txt("PRESS E TO LUNGE", "TAP E TO LUNGE"), "Dashes to the nearest enemy and strikes once");
        RunCameraZoom.ForceCinematic = true;
    }

    void UpdateRage()
    {
        passive = true;
        var rm = RageMeter.Instance;

        if (phase == Phase.Prompt)
        {
            FreezeTime();
            if (MobileInput.GetKeyDown(KeyCode.E))
            {
                promptTitle = ""; promptSub = "";
                phase = Phase.Doing; phaseTimer = 0f;
                RunCameraZoom.ForceCinematic = false;
            }
            return;
        }

        if (rm != null && rm.Busy) rageStarted = true;

        if (rageStarted && (rm == null || !rm.Busy) && phaseTimer > 0.5f)
        {
            step = Step.Done; phase = Phase.Doing; doneTimer = 0f;
            Toast("TUTORIAL COMPLETE");
        }
        else if (!rageStarted && phaseTimer > 3f)
        {
            // E'ye basmadı: tekrar hatırlat.
            phase = Phase.Prompt; phaseTimer = 0f;
            Freeze(MobileInput.Txt("PRESS E TO LUNGE", "TAP E TO LUNGE"), "Dashes to the nearest enemy and strikes once");
        }
    }

    void UpdateDone()
    {
        passive = true;
        doneTimer += Time.unscaledDeltaTime;
        if (!leaving)
        {
            leaving = true;
            Cleanup();
            TutorialMode.Active = false;
            var flow = FindFirstObjectByType<GameFlow>();
            if (flow != null) flow.GoToMainMenu();
        }
    }

    // ------------------------------------------------------------ yardımcılar

    void Freeze(string title, string sub)
    {
        promptTitle = title; promptSub = sub;
        RunCameraZoom.ForceCinematic = true;
        FreezeTime();
    }

    void FreezeTime() { HitStop.Request(0.12f, 0f, 90); }

    void Toast(string t) { toast = t; toastUntil = Time.unscaledTime + 1.6f; }

    void ClearStrangers()
    {
        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            var e = EnemyController.All[i];
            if (e != null && e != enemy) Destroy(e.gameObject);
        }
    }

    void ResetEnemy()
    {
        if (enemy == null) return;
        if (eHealth != null && !eHealth.IsDead) eHealth.Heal(eHealth.MaxHealth);
        var bal = enemy.GetComponent<EnemyBalance>();
        if (bal != null) { bal.ResetBalance(); }
    }

    float FacingSign()
    {
        float f = player != null ? player.facingDir : 1f;
        return f < 0f ? -1f : 1f;
    }

    void PlaceEnemyInFront()
    {
        if (enemy == null || player == null) return;
        Vector3 p = player.transform.position;
        enemy.transform.position = new Vector3(p.x + FacingSign() * enemyDistance, p.y + 0.1f, enemy.transform.position.z);
        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    GameObject FindPrefab()
    {
        if (prefab != null) return prefab;
        if (prefabSearched) return null;
        prefabSearched = true;
        prefab = Resources.Load<GameObject>("Endless/Duelist");
        if (prefab == null)
        {
            var sp = FindFirstObjectByType<EnemySpawner>();
            if (sp != null && sp.enemyPrefab != null) prefab = sp.enemyPrefab;
        }
        if (prefab == null) Debug.LogError("Tutorial: düşman prefab'ı bulunamadı (Resources/Endless/Duelist).");
        return prefab;
    }

    void SpawnEnemy()
    {
        GameObject p = FindPrefab();
        if (p == null || player == null) return;

        Vector3 pp = player.transform.position;
        var go = Instantiate(p, new Vector3(pp.x + FacingSign() * enemyDistance, pp.y + 0.1f, 0f), Quaternion.identity);
        go.name = "TutorialEnemy";
        enemy = go.GetComponent<EnemyController>();
        eHealth = go.GetComponent<Health>();
        if (enemy != null) enemy.alwaysHunt = false;
    }

    // ------------------------------------------------------------ yazılar

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || !TutorialMode.Active || MainMenu.Active || !started) return;

        float s = Screen.height / 1080f * MobileInput.UiMul;

        if (big == null)
        {
            // Ana menüyle aynı yazı tipi (Resources/UI/ProjectM_Serif_*).
            fontBold = Resources.Load<Font>("UI/ProjectM_Serif_Bold");
            fontRegular = Resources.Load<Font>("UI/ProjectM_Serif_Regular");
            fontBold = GameFont.Or(fontBold);        // oyun fontu (Knight Souls)
            fontRegular = GameFont.Or(fontRegular);
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = fontBold != null ? FontStyle.Normal : FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            top = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = fontBold != null ? FontStyle.Normal : FontStyle.Bold };
            if (fontBold != null) { big.font = fontBold; top.font = fontBold; }
            if (fontRegular != null) small.font = fontRegular;
        }

        int oldDepth = GUI.depth;
        GUI.depth = -9000;

        string stepName =
            step == Step.Attack ? "1 / 4   ATTACK" :
            step == Step.Block ? "2 / 4   BLOCK" :
            step == Step.Parry ? "3 / 4   PARRY" :
            step == Step.Rage ? "4 / 4   RAGE" : "COMPLETE";

        top.fontSize = Mathf.RoundToInt(FontPt * s);
        Shadow(new Rect(0f, 70f * s, Screen.width, 40f * s), "TUTORIAL  -  " + stepName, top, new Color(1f, 1f, 1f, 0.55f), s);

        if (step == Step.Attack && phase == Phase.Doing)
        {
            small.fontSize = Mathf.RoundToInt(FontPt * s);
            Shadow(new Rect(0f, Screen.height * 0.72f, Screen.width, 50f * s), "HIT THE DUMMY   " + Mathf.Min(hits, hitsToPass) + " / " + hitsToPass, small, Color.white, s);
        }

        if (promptTitle.Length > 0)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            big.fontSize = Mathf.RoundToInt(FontPt * s);
            small.fontSize = Mathf.RoundToInt(FontPt * s);
            Shadow(new Rect(0f, Screen.height * 0.64f, Screen.width, 90f * s), promptTitle, big, new Color(1f, 1f, 1f, pulse), s);
            Shadow(new Rect(0f, Screen.height * 0.64f + 84f * s, Screen.width, 40f * s), promptSub, small, new Color(0.7f, 0.85f, 1f, 0.9f), s);
        }

        if (toast != null && Time.unscaledTime < toastUntil)
        {
            big.fontSize = Mathf.RoundToInt(FontPt * s);
            Shadow(new Rect(0f, Screen.height * 0.30f, Screen.width, 90f * s), toast, big, new Color(0.6f, 0.85f, 1f, 1f), s);
        }

        GUI.depth = oldDepth;
        GUI.color = Color.white;
    }

    static void Shadow(Rect r, string text, GUIStyle st, Color c, float s)
    {
        st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.8f);
        Rect o = r; o.x += 2f * s; o.y += 2f * s;
        GUI.Label(o, text, st);
        st.normal.textColor = c;
        GUI.Label(r, text, st);
    }
}
