using System;
using UnityEngine;

/// <summary>
/// BOSS. RunManager perde sonunda doğurduğu düşmana ekler (prefab'a
/// elle eklemene gerek yok; özel bir boss prefab'ı da kullanabilirsin).
///
///  - Faz 1: boss hamle seti (Düellocu + dörtlü kombo).
///  - Canı 'phase2At' oranına inince FAZ 2: daha uzun / karışık kombolar,
///    biraz daha hızlı. Sersem / execute sırasında geçiş beklenir.
///  - Arayüz (RunUI) üstte isim + can + denge çubuğu gösterir.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossController : MonoBehaviour
{
    public static BossController Current { get; private set; }

    public string BossName { get; private set; } = "Boss";
    public bool InPhase2 { get; private set; }

    // Faz 2'ye geçiş can oranı (infaz barı bunu kullanır).
    public float Phase2At => phase2At;

    public event Action PhaseChanged;

    [Tooltip("Denge çubuğu normal düşmanın kaç katı (büyük = parry barı az doldurur).")]
    [Min(1f)]
    public float balanceMultiplier = 4f;

    [Header("Gölge Hilali (perde 4)")]
    [Tooltip("Boss'un canı (RunManager'ın verdiği değerin kaç katı).")]
    [Min(1f)]
    public float trialHealthMultiplier = 4f;

    [Tooltip("Boss'un denge çubuğu normal düşmanın kaç katı.")]
    [Min(1f)]
    public float trialBalanceMultiplier = 5f;

    [Tooltip("Denge kırılınca infazın vuracağı can oranı (0.18 = %18).")]
    [Range(0.02f, 1f)]
    public float trialExecutePercent = 0.28f;

    [Tooltip("Denge çubuğu dolunca (sersemlemek yerine) boss'un yiyeceği can oranı (Sekiro gibi).")]
    [Range(0.05f, 0.6f)]
    public float trialPostureBreakPercent = 0.25f;

    [Tooltip("Parry ile yansıtılan ok boss'a değince yiyeceği can oranı.")]
    [Range(0f, 0.3f)]
    public float reflectHealthPercent = 0.04f;

    private float phase2At = 0.5f;
    private float startTime;
    private int act = 1;

    private EnemyController enemy;
    private EnemyMoveset moveset;
    private EnemyBalance balance;

    public Health Health { get; private set; }

    public float HealthPercent =>
        Health != null && Health.MaxHealth > 0
            ? (float)Health.CurrentHealth / Health.MaxHealth
            : 0f;

    public float BalancePercent =>
        balance != null ? balance.BalancePercent : 0f;

    /// <summary>Gölge Hilali: infaz barı ölümcül değil, canın % kadarını alır.</summary>
    public bool PercentExecute => act >= 4;

    [Tooltip("Gölge Hilali'nin vuruşları bloğa bu kadar kat posture hasarı verir (blok riskli olsun).")]
    public float trialBlockPostureMultiplier = 1.6f;

    // ---------- DÖVÜŞ ALANI (Gölge Hilali) ----------
    // Boss çağrıldığı anda kameranın gördüğü alan dövüş alanı olur;
    // boss bu alanın dışına çıkmaz (koşu atağı da burada biter).
    public bool HasArena { get; private set; }
    public float ArenaMinX { get; private set; }
    public float ArenaMaxX { get; private set; }

    private Rigidbody2D arenaRb;

    private void SetupArena()
    {
        Camera cam = Camera.main;

        if (cam == null || !cam.orthographic)
            return;

        float half = cam.orthographicSize * cam.aspect;

        // Kenarlardan biraz içeride: boss ekranın tam kenarına yapışmasın.
        float pad = 1.5f;

        ArenaMinX = cam.transform.position.x - half + pad;
        ArenaMaxX = cam.transform.position.x + half - pad;

        HasArena = ArenaMaxX > ArenaMinX + 6f;

        arenaRb = GetComponent<Rigidbody2D>();

        Debug.Log("BOSS ALANI: x = " + ArenaMinX.ToString("F1") + " … " + ArenaMaxX.ToString("F1"));
    }

    private void LateUpdate()
    {
        if (!HasArena || arenaRb == null)
            return;

        float x = arenaRb.position.x;
        float cl = Mathf.Clamp(x, ArenaMinX, ArenaMaxX);

        if (Mathf.Abs(cl - x) > 0.001f)
        {
            arenaRb.position = new Vector2(cl, arenaRb.position.y);

            Vector2 v = arenaRb.linearVelocity;

            // Duvara yaslanan boss dışarı doğru hız biriktirmesin.
            if ((x < ArenaMinX && v.x < 0f) || (x > ArenaMaxX && v.x > 0f))
                arenaRb.linearVelocity = new Vector2(0f, v.y);
        }
    }

    public float ExecutePercent => trialExecutePercent;

    [Tooltip("Diğer boss'larda infaz canın bu oranını alır (faz atlatmaz, öldürmez).")]
    public float defaultExecutePercent = 0.28f;

    [Tooltip("İnfazda harcanan parçaya göre boss canından alınan oran (1, 2, 3 parça).")]
    public float[] executePercentBySegments = { 0.05f, 0.10f, 0.18f };

    [Tooltip("Parça başına: infazın denge barına ittiği oran (barın tamamına göre).")]
    public float[] executeBalancePushBySegments = { 0.15f, 0.30f, 0.50f };

    [Tooltip("Parça başına: boss'un can oranı bunun ALTINDAYSA infaz öldürür (bitirici vuruş).")]
    public float[] executeKillBelowBySegments = { 0.06f, 0.12f, 0.20f };

    /// <summary>
    /// İnfazın boss'a etkisi: cana doğrudan hasar + denge barına itme.
    /// Canı eşiğin altındaysa öldürür. Dönen değer: cana uygulanacak hasar.
    /// </summary>
    public int ExecuteHealthDamage(int segs, float multiplier)
    {
        if (Health == null)
            return 0;

        int idx = Mathf.Clamp(segs, 1, 3) - 1;

        // Bitirici vuruş: can eşiğin altındaysa ölür.
        if (executeKillBelowBySegments != null &&
            idx < executeKillBelowBySegments.Length &&
            HealthPercent <= executeKillBelowBySegments[idx])
        {
            return Mathf.Max(1, Health.CurrentHealth);
        }

        // Denge barına it (kırılırsa normal denge kırılma akışı çalışır).
        if (balance != null && !balance.IsBroken &&
            executeBalancePushBySegments != null && idx < executeBalancePushBySegments.Length)
        {
            int units = Mathf.RoundToInt(balance.MaxBalance * executeBalancePushBySegments[idx] * multiplier);

            if (units > 0)
                balance.AddBalanceDamage(units, false);
        }

        return Mathf.Max(1, Mathf.RoundToInt(Health.MaxHealth * ExecutePercentFor(segs) * multiplier));
    }

    public float ExecutePercentFor(int segs)
    {
        if (executePercentBySegments == null || executePercentBySegments.Length == 0)
            return ExecuteDamagePercent;

        int i = Mathf.Clamp(segs, 1, executePercentBySegments.Length) - 1;

        return executePercentBySegments[i];
    }

    /// <summary>İnfazın boss'a vereceği can oranı (tüm boss'larda yüzde, aşırı güçlü olmasın).</summary>
    public float ExecuteDamagePercent =>
        PercentExecute ? trialExecutePercent : defaultExecutePercent;

    /// <summary>
    /// Denge dolunca: sersemleme YOK, denge sıfırlanır, boss canının %X'ini yer.
    /// Can bu hasara yetmiyorsa false döner (normal sersemleme / infaz fırsatı).
    /// </summary>
    // Gölge Hilali'nde sersemlemeyi engelle (can ölümcül değilse).
    public bool BlocksStagger()
    {
        if (!PercentExecute || Health == null || Health.IsDead)
            return false;

        int damage = Mathf.Max(1, Mathf.RoundToInt(Health.MaxHealth * trialPostureBreakPercent));

        return Health.CurrentHealth > damage;
    }

    public bool TryPostureBreak()
    {
        if (!PercentExecute || Health == null || balance == null || enemy == null)
        {
            Debug.Log("TryPostureBreak: kapalı (PercentExecute=" + PercentExecute + ")");
            return false;
        }

        int damage = Mathf.Max(1, Mathf.RoundToInt(Health.MaxHealth * trialPostureBreakPercent));

        if (Health.CurrentHealth <= damage)
            return false;

        Debug.Log("DENGE KIRILDI → sersemleme yok, can -" + damage);

        BossStats.postureBreaks++;

        balance.RecoverBalance();

        Health.TakeDamage(damage);

        enemy.PlayBalanceDamageFlash();

        CombatCallout.PopupAbove(enemy, "DENGE KIRILDI", new Color(1f, 0.85f, 0.2f), 1.4f);

        HitStop.Request(0.14f, 0.04f);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.9f);

        return true;
    }

    /// <summary>Yansıtılan ok boss'a değdi: doğrudan can hasarı.</summary>
    public void OnReflectedHit()
    {
        if (!PercentExecute || Health == null || Health.IsDead)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(Health.MaxHealth * reflectHealthPercent));

        Health.TakeDamage(damage);

        BossStats.reflectedHits++;

        CombatCallout.PopupAbove(enemy, "CAN HASARI!", new Color(1f, 0.5f, 0.4f), 1.1f, 1.1f);
    }

    public bool IsAlive => enemy != null && !enemy.IsDead;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        Current = null;
    }

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        Health = GetComponent<Health>();
        balance = GetComponent<EnemyBalance>();
    }

    public void Setup(string bossName, float phase2Threshold)
    {
        Setup(bossName, phase2Threshold, 1);
    }

    /// <summary>act: perde (1 Kılıç Ustası, 2 Kızıl Düellocu, 3 Gölge Efendisi).</summary>
    public void Setup(string bossName, float phase2Threshold, int bossAct)
    {
        act = Mathf.Max(1, bossAct);
        BossName = bossName;
        phase2At = Mathf.Clamp01(phase2Threshold);
        InPhase2 = false;

        moveset = GetComponent<EnemyMoveset>();

        if (moveset == null)
            moveset = gameObject.AddComponent<EnemyMoveset>();

        moveset.moves = EnemyMoveset.CreateBossMoves(act, false);

        // Boss'un dengesi normal düşmana göre çok daha geç kırılır:
        // parry/vuruş aynı hasarı verir ama bar 'balanceMultiplier' kat büyüktür.
        float balMult = act >= 4 ? trialBalanceMultiplier : balanceMultiplier;

        if (balance != null && balMult > 1f)
            balance.SetMaxBalance(Mathf.RoundToInt(balance.MaxBalance * balMult));

        // Gölge Hilali: uzun savaş (4-5 dk) ve tek atmayan infaz.
        if (act >= 4 && Health != null)
        {
            Health.SetMaxHealth(
                Mathf.RoundToInt(Health.MaxHealth * trialHealthMultiplier),
                true
            );

            enemy.executeDamage =
                Mathf.Max(1, Mathf.RoundToInt(Health.MaxHealth * trialExecutePercent));
        }

        // Perdeye göre karakter.
        if (act == 2)
        {
            enemy.chaseSpeed *= 1.25f;
            enemy.attackRecoveryTime *= 0.8f;
        }
        else if (act >= 4)
        {
            // Gölge Hilali: mesafeden dalga atar, geri kaçmaz / geri dash atmaz.
            EnemyArcher archer = GetComponent<EnemyArcher>();

            if (archer == null)
                archer = gameObject.AddComponent<EnemyArcher>();

            archer.retreatDistance = 0f;
            archer.enableBackDash = false;
            archer.arrowScale = 3.2f;
            archer.arrowSpeed = 17f;
            archer.arrowColor = new Color(0.75f, 0.55f, 1f);
            archer.matchTargetHeight = true;

            // Zıpla-Ez: alan saldırısı.
            SetupArena();

            // Akıllı boss: oyuncunun durumuna göre hamle seçer.
            BossBrain brain = GetComponent<BossBrain>();

            if (brain == null)
                brain = gameObject.AddComponent<BossBrain>();

            // Blok riskli olsun: boss vuruşları bloğa daha fazla posture hasarı verir.
            enemy.blockPostureDamage =
                Mathf.RoundToInt(enemy.blockPostureDamage * trialBlockPostureMultiplier);

            if (GetComponent<BossSlam>() == null)
                gameObject.AddComponent<BossSlam>();

            // Atlayış: uzaktan üstüne zıplar (dash ile kaçılır).
            if (GetComponent<BossLeap>() == null)
                gameObject.AddComponent<BossLeap>();

            // Yakalamadan dash ile kurtulma payı (uzun menzil için).
            enemy.unblockableDodgeGrace = Mathf.Max(enemy.unblockableDodgeGrace, 0.3f);

            // Aynı hamleyi art arda seçme.
            moveset.repeatPenalty = 0.05f;

            // Beyin: hamle ağırlıklarını oyuncunun durumuna göre çarpar.
            BossBrain brainRef = GetComponent<BossBrain>();

            if (brainRef != null)
                moveset.weightModifier = brainRef.Modify;

            moveset.comboBlockPostureMultiplier = 0.55f;

            // Çevik ve saldırgan: hızlı koşar, toparlanması kısa.
            enemy.chaseSpeed *= 1.3f;
            enemy.attackRecoveryTime *= 0.6f;

            // Kusursuz kaçış / atla-vur ödülleri boss'un barını az doldursun.
            UnblockableCounter counter = GetComponent<UnblockableCounter>();

            if (counter == null)
                counter = gameObject.AddComponent<UnblockableCounter>();

            counter.dashCounterBalancePercent = 0.1f;
            counter.jumpCounterBalancePercent = 0.07f;

            // Haritaya vuran kaçış saldırısı.
            if (GetComponent<BossNova>() == null)
                gameObject.AddComponent<BossNova>();

            BossStats.Reset();
            startTime = Time.time;
        }
        else if (act >= 3)
        {
            enemy.attackRecoveryTime *= 0.9f;
            enemy.balanceHitKnockbackForce *= 0.5f;
            enemy.healthKnockbackForce *= 0.5f;
        }

        Current = this;
    }

    private void OnEnable()
    {
        CombatEvents.PlayerDamaged += OnPlayerDamaged;
    }

    private void OnDisable()
    {
        CombatEvents.PlayerDamaged -= OnPlayerDamaged;
    }

    private void OnPlayerDamaged(PlayerDamageReport report)
    {
        if (!PercentExecute || report.source != enemy)
            return;

        string skill =
            BossSkillGate.IsRunning
                ? BossSkillGate.CurrentId
                : (moveset != null ? moveset.LastMoveName : "?");

        BossStats.Record(skill, report.amount, report.lethal);

        if (report.lethal)
            BossStats.Print("OYUNCU ÖLDÜ", Time.time - startTime);
    }

    private void OnDestroy()
    {
        if (PercentExecute && enemy != null && enemy.IsDead)
            BossStats.Print("BOSS ÖLDÜ", Time.time - startTime);

        if (Current == this)
            Current = null;
    }

    // Faz 2 sahnesi: boss kükrer, kısa süre saldırmaz; oyuncu ne olduğunu görür.
    private System.Collections.IEnumerator Phase2Scene()
    {
        BossSkillGate.Block(2.4f);

        enemy.StartAttackRecovery(2.2f);

        BossStats.phase2At = Time.time - startTime;

        if (RunManager.Instance != null)
            RunManager.Instance.ShowBanner("GÖLGE UYANDI", 2f);

        CombatCallout.PopupAbove(enemy, "GÖLGE UYANDI", new Color(0.75f, 0.55f, 1f), 1.8f);

        enemy.PlayTintFlash(new Color(0.6f, 0.4f, 1f), 0.8f);

        HitStop.Request(0.3f, 0.05f);

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(1.5f);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        float t = 0f;

        while (t < 1.4f && enemy != null && !enemy.IsDead)
        {
            enemy.StartAttackRecovery(0.5f);

            if (rb != null)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            t += Time.deltaTime;

            yield return null;
        }
    }

    private void Update()
    {
        if (InPhase2 || Health == null || enemy == null || enemy.IsDead)
            return;

        if (HealthPercent > phase2At)
            return;

        // Sersem / execute sırasında faz geçişi yapma: execute fırsatı
        // bozulmasın. Sonra geçer.
        if (enemy.IsStaggered || enemy.CurrentState is EnemyExecuteState)
            return;

        EnterPhase2();
    }

    private void EnterPhase2()
    {
        InPhase2 = true;

        if (moveset != null)
            moveset.moves = EnemyMoveset.CreateBossMoves(act, true);

        enemy.chaseSpeed *= 1.15f;
        enemy.attackRecoveryTime *= 0.85f;

        // Kısa nefes: oyuncu ne olduğunu görsün.
        if (balance != null && !balance.IsBroken)
            balance.ResetBalance();

        enemy.PlayAlertFlash();

        HitStop.Request(0.25f, 0.1f);

        if (RunManager.Instance != null)
            RunManager.Instance.ShowBanner("FAZ 2", 1.4f);

        PhaseChanged?.Invoke();

        if (PercentExecute)
            StartCoroutine(Phase2Scene());

        Debug.Log("BOSS FAZ 2: " + BossName);
    }
}
