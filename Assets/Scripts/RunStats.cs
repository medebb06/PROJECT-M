using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// KOŞU İSTATİSTİKLERİ. Dengeyi sayıyla ölçmek için.
///
/// Oyun kodunu değiştirmez; sadece CombatEvents'i dinler. RunManager
/// kendiliğinden ekler ve koşu/bölüm başlangıç-bitişlerini bildirir.
///
/// En önemli ölçü: DÜŞMAN SALDIRILARINA CEVAP DAĞILIMI. Her düşman
/// saldırısı tam olarak bir sonuçla biter:
///   parry / block / dash / korumalı dönem / menzil dışı / kesildi / yendi
/// "Parry'e ihtiyaç duymuyorum" sorunu burada görünür: parry payı düşük,
/// block ve "kesildi" payı yüksekse oyuncu parry'i atlıyordur.
///
/// Koşu sonunda: ekranda özet, konsola özet ve (isteğe bağlı)
/// persistentDataPath/run_stats.csv dosyasına bir satır.
/// Oyun sırasında TAB: canlı istatistik paneli (RunUI).
/// </summary>
public class RunStats : MonoBehaviour
{
    public static RunStats Current { get; private set; }

    [Header("Kayıt")]
    [Tooltip("Koşu bitince özet konsola yazılsın.")]
    [SerializeField] private bool logSummaryToConsole = true;

    [Tooltip(
        "Her koşu bir CSV dosyasına bir satır olarak eklensin " +
        "(Application.persistentDataPath). Koşular arası karşılaştırma için.")]
    [SerializeField] private bool appendCsv = true;

    [SerializeField] private string csvFileName = "run_stats.csv";

    [Tooltip(
        "Dash'ten sonra bu kadar saniye içinde kaçılan saldırı 'dash' " +
        "sayılır; daha sonrası 'korumalı dönem' (hasar sonrası i-frame).")]
    [SerializeField] private float dashCreditWindow = 0.6f;

    // =========================================================
    // VERİ TİPLERİ
    // =========================================================

    // Bir düşman saldırısının nasıl bittiği.
    [Serializable]
    public class AttackOutcomes
    {
        public int parried;
        public int blocked;
        public int dashed;
        public int iFrame;       // hasar sonrası korumalı dönemde emildi
        public int missed;       // menzil dışı / arkada / üstünden zıpladı
        public int interrupted;  // vuruş anından önce kesildi
        public int hit;          // oyuncu yedi

        public int Total =>
            parried + blocked + dashed + iFrame +
            missed + interrupted + hit;

        public void Clear()
        {
            parried = blocked = dashed = iFrame = 0;
            missed = interrupted = hit = 0;
        }
    }

    [Serializable]
    public class StageRecord
    {
        public int stage;
        public float time;
        public int kills;
        public int parries;
        public int blocks;
        public int hitsTaken;
        public int damageTaken;
        public bool cleared;
    }

    // =========================================================
    // SAYAÇLAR (arayüz okur)
    // =========================================================

    public bool Recording { get; private set; }
    public bool HasResult { get; private set; }

    public float RunTime { get; private set; }

    public int StageReached { get; private set; }
    public int WaveReached { get; private set; }
    public int WaveCountAtEnd { get; private set; }

    // Düşman saldırılarına cevap
    public readonly AttackOutcomes Normal = new AttackOutcomes();
    public readonly AttackOutcomes Unblockable = new AttackOutcomes();

    // Savunma
    public int ParryBreaks { get; private set; }     // dengeyi kıran parry
    public int PostureBreaks { get; private set; }   // block yüzünden kırılan posture
    public int Dashes { get; private set; }

    // Alınan hasar
    public int DamageNormal { get; private set; }
    public int DamageUnblockable { get; private set; }
    public int DamageOther { get; private set; }
    public int HitsOther { get; private set; }
    public int DamageTotal => DamageNormal + DamageUnblockable + DamageOther;
    public int Healed { get; private set; }
    public int LowestHealth { get; private set; }

    // Verilen hasar
    public int Hits { get; private set; }
    public int Crits { get; private set; }
    public int BalanceDealt { get; private set; }
    public int HealthDealt { get; private set; }
    public int BalanceBreaks { get; private set; }  // düşman dengesi kırıldı (her kaynak)

    // Öldürmeler
    public int KillsByAttack { get; private set; }
    public int KillsBySlam { get; private set; }
    public int KillsByExecute { get; private set; }
    public int KillsOther { get; private set; }     // zehir vb.
    public int Kills =>
        KillsByAttack + KillsBySlam + KillsByExecute + KillsOther;

    // Ölüm
    public PlayerHitKind DeathKind { get; private set; }
    public string DeathSource { get; private set; } = "";
    public bool DiedByHit { get; private set; }
    public int DeathStage { get; private set; }
    public int DeathWave { get; private set; }

    public IReadOnlyList<StageRecord> Stages => stages;
    public string BuildText { get; private set; } = "";

    // =========================================================
    // İÇ DURUM
    // =========================================================

    private readonly List<StageRecord> stages = new List<StageRecord>();
    private StageRecord currentStage;

    private readonly Dictionary<EnemyController, DamageSource> lastKillingHit =
        new Dictionary<EnemyController, DamageSource>();

    private Health playerHealth;
    private PlayerController player;
    private int lastKnownHealth;
    private float lastDashTime = -999f;

    // =========================================================
    // KURULUM
    // =========================================================

    private void Awake()
    {
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;

        if (playerHealth != null)
            playerHealth.OnHealthChanged -= OnPlayerHealthChanged;
    }

    public void Init(PlayerController player, Health playerHealth)
    {
        this.player = player;

        if (this.playerHealth != null)
            this.playerHealth.OnHealthChanged -= OnPlayerHealthChanged;

        this.playerHealth = playerHealth;

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnPlayerHealthChanged;
            lastKnownHealth = playerHealth.CurrentHealth;
        }
    }

    private void OnEnable()
    {
        CombatEvents.EnemyHit += OnEnemyHit;
        CombatEvents.EnemyKilled += OnEnemyKilled;
        CombatEvents.BalanceBroken += OnBalanceBroken;
        CombatEvents.ParrySucceeded += OnParry;
        CombatEvents.Dodged += OnDodged;
        CombatEvents.PlayerDamaged += OnPlayerDamaged;
        CombatEvents.PlayerBlocked += OnBlocked;
        CombatEvents.AttackMissed += OnMissed;
        CombatEvents.AttackInterrupted += OnInterrupted;
        CombatEvents.PlayerDashed += OnDashed;
    }

    private void OnDisable()
    {
        CombatEvents.EnemyHit -= OnEnemyHit;
        CombatEvents.EnemyKilled -= OnEnemyKilled;
        CombatEvents.BalanceBroken -= OnBalanceBroken;
        CombatEvents.ParrySucceeded -= OnParry;
        CombatEvents.Dodged -= OnDodged;
        CombatEvents.PlayerDamaged -= OnPlayerDamaged;
        CombatEvents.PlayerBlocked -= OnBlocked;
        CombatEvents.AttackMissed -= OnMissed;
        CombatEvents.AttackInterrupted -= OnInterrupted;
        CombatEvents.PlayerDashed -= OnDashed;
    }

    // Süre: sadece dövüş sırasında (charm seçimi ve ölüm ekranı sayılmaz).
    // Gerçek zaman: slow-mo süreyi uzatmaz.
    private void Update()
    {
        if (!Recording)
            return;

        RunManager run = RunManager.Instance;

        if (run == null)
            return;

        if (
            run.State != RunState.Fighting &&
            run.State != RunState.Cleared
        )
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;

        RunTime += dt;

        if (currentStage != null)
            currentStage.time += dt;
    }

    // =========================================================
    // RUNMANAGER ÇAĞIRIR
    // =========================================================

    public void BeginRun()
    {
        Normal.Clear();
        Unblockable.Clear();

        RunTime = 0f;
        StageReached = 0;
        WaveReached = 0;
        WaveCountAtEnd = 0;

        ParryBreaks = 0;
        PostureBreaks = 0;
        Dashes = 0;

        DamageNormal = 0;
        DamageUnblockable = 0;
        DamageOther = 0;
        HitsOther = 0;
        Healed = 0;

        Hits = 0;
        Crits = 0;
        BalanceDealt = 0;
        HealthDealt = 0;
        BalanceBreaks = 0;

        KillsByAttack = 0;
        KillsBySlam = 0;
        KillsByExecute = 0;
        KillsOther = 0;

        DeathKind = PlayerHitKind.Other;
        DeathSource = "";
        DiedByHit = false;
        DeathStage = 0;
        DeathWave = 0;

        BuildText = "";

        stages.Clear();
        currentStage = null;
        lastKillingHit.Clear();

        if (playerHealth != null)
        {
            lastKnownHealth = playerHealth.CurrentHealth;
            LowestHealth = playerHealth.CurrentHealth;
        }

        HasResult = false;
        Recording = true;
    }

    public void BeginStage(int stage)
    {
        StageReached = stage;

        currentStage = new StageRecord { stage = stage };

        stages.Add(currentStage);
    }

    public void SetWave(int wave, int waveCount)
    {
        WaveReached = wave;
        WaveCountAtEnd = waveCount;
    }

    public void EndStage(bool cleared)
    {
        if (currentStage != null)
            currentStage.cleared = cleared;
    }

    public void EndRun(RunManager run)
    {
        if (!Recording)
            return;

        Recording = false;
        HasResult = true;

        if (run != null)
        {
            StageReached = run.Stage;
            WaveReached = run.Wave;
            WaveCountAtEnd = run.WaveCount;
            BuildText = DescribeBuild(run.Inventory);
        }

        if (logSummaryToConsole)
            Debug.Log("===== KOŞU ÖZETİ =====\n" + BuildSummary());

        if (appendCsv)
            WriteCsv();
    }

    // =========================================================
    // OLAYLAR
    // =========================================================

    private void OnEnemyHit(
        EnemyController enemy,
        DamageInfo info,
        HitResult result
    )
    {
        if (!Recording || !result.hit)
            return;

        Hits++;

        if (result.critical)
            Crits++;

        if (result.onBalance)
            BalanceDealt += result.amount;
        else
            HealthDealt += result.amount;

        if (result.killed && enemy != null)
            lastKillingHit[enemy] = info.source;
    }

    private void OnEnemyKilled(EnemyController enemy)
    {
        if (!Recording)
            return;

        // Execute: düşman ölürken hâlâ execute state'inde kalır.
        if (enemy != null && enemy.CurrentState is EnemyExecuteState)
        {
            KillsByExecute++;
        }
        else if (
            enemy != null &&
            lastKillingHit.TryGetValue(enemy, out DamageSource source)
        )
        {
            if (source == DamageSource.Slam)
                KillsBySlam++;
            else if (source == DamageSource.Attack)
                KillsByAttack++;
            else
                KillsOther++;
        }
        else
        {
            KillsOther++;
        }

        if (enemy != null)
            lastKillingHit.Remove(enemy);

        if (currentStage != null)
            currentStage.kills++;
    }

    private void OnBalanceBroken(EnemyController enemy)
    {
        if (!Recording)
            return;

        BalanceBreaks++;
    }

    private void OnParry(EnemyController enemy, bool brokeBalance)
    {
        if (!Recording)
            return;

        Normal.parried++;

        if (brokeBalance)
            ParryBreaks++;

        if (currentStage != null)
            currentStage.parries++;
    }

    private void OnBlocked(EnemyController enemy, bool postureBroken)
    {
        if (!Recording)
            return;

        Normal.blocked++;

        if (postureBroken)
            PostureBreaks++;

        if (currentStage != null)
            currentStage.blocks++;
    }

    private void OnDodged(EnemyController enemy, bool unblockable)
    {
        if (!Recording)
            return;

        AttackOutcomes o = unblockable ? Unblockable : Normal;

        bool byDash =
            (player != null && player.isDashing) ||
            Time.time - lastDashTime <= dashCreditWindow;

        if (byDash)
            o.dashed++;
        else
            o.iFrame++;
    }

    private void OnMissed(EnemyController enemy, bool unblockable)
    {
        if (!Recording)
            return;

        (unblockable ? Unblockable : Normal).missed++;
    }

    private void OnInterrupted(EnemyController enemy, bool unblockable)
    {
        if (!Recording)
            return;

        (unblockable ? Unblockable : Normal).interrupted++;
    }

    private void OnDashed()
    {
        lastDashTime = Time.time;

        if (Recording)
            Dashes++;
    }

    private void OnPlayerDamaged(PlayerDamageReport r)
    {
        if (!Recording)
            return;

        switch (r.kind)
        {
            case PlayerHitKind.Normal:
                Normal.hit++;
                DamageNormal += r.amount;
                break;

            case PlayerHitKind.Unblockable:
                Unblockable.hit++;
                DamageUnblockable += r.amount;
                break;

            default:
                HitsOther++;
                DamageOther += r.amount;
                break;
        }

        if (currentStage != null)
        {
            currentStage.hitsTaken++;
            currentStage.damageTaken += r.amount;
        }

        if (r.lethal)
        {
            DiedByHit = true;
            DeathKind = r.kind;
            DeathSource =
                r.source != null
                    ? CleanName(r.source.name)
                    : "";

            DeathStage = StageReached;

            RunManager run = RunManager.Instance;

            DeathWave = run != null ? run.Wave : 0;
        }
    }

    // İyileşme: canın her artışı (charm, bölüm arası, parry...).
    private void OnPlayerHealthChanged(int current, int max)
    {
        if (Recording)
        {
            if (current > lastKnownHealth)
                Healed += current - lastKnownHealth;

            if (current < LowestHealth)
                LowestHealth = current;
        }

        lastKnownHealth = current;
    }

    // =========================================================
    // ÖZET
    // =========================================================

    public static string Pct(int part, int total)
    {
        if (total <= 0)
            return "-";

        return "%" + Mathf.RoundToInt(100f * part / total);
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));

        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    public string DeathText()
    {
        if (!DiedByHit)
            return "Ölüm nedeni: bilinmiyor";

        string kind;

        switch (DeathKind)
        {
            case PlayerHitKind.Normal:
                kind = "normal vuruş (parry/block edilebilirdi)";
                break;

            case PlayerHitKind.Unblockable:
                kind = "ENGELLENEMEZ vuruş (dash/kaçış gerekirdi)";
                break;

            default:
                kind = "diğer hasar";
                break;
        }

        return
            "Ölüm: " + kind +
            (DeathSource.Length > 0 ? " — " + DeathSource : "") +
            "  (bölüm " + DeathStage + ", dalga " + DeathWave + ")";
    }

    // Normal saldırılara cevap dağılımı (tek satır).
    public string NormalLine()
    {
        AttackOutcomes o = Normal;
        int t = o.Total;

        return
            "Normal saldırı " + t + ":  " +
            "parry " + o.parried + " (" + Pct(o.parried, t) + ")  " +
            "block " + o.blocked + " (" + Pct(o.blocked, t) + ")  " +
            "dash " + o.dashed + " (" + Pct(o.dashed, t) + ")  " +
            "kaçtı " + o.missed + " (" + Pct(o.missed, t) + ")  " +
            "kesildi " + o.interrupted + " (" + Pct(o.interrupted, t) + ")  " +
            "i-frame " + o.iFrame + " (" + Pct(o.iFrame, t) + ")  " +
            "YENDİ " + o.hit + " (" + Pct(o.hit, t) + ")";
    }

    public string UnblockableLine()
    {
        AttackOutcomes o = Unblockable;
        int t = o.Total;

        return
            "Engellenemez " + t + ":  " +
            "dash " + o.dashed + " (" + Pct(o.dashed, t) + ")  " +
            "kaçtı " + o.missed + " (" + Pct(o.missed, t) + ")  " +
            "kesildi " + o.interrupted + " (" + Pct(o.interrupted, t) + ")  " +
            "i-frame " + o.iFrame + " (" + Pct(o.iFrame, t) + ")  " +
            "YENDİ " + o.hit + " (" + Pct(o.hit, t) + ")";
    }

    public string DefenseLine()
    {
        return
            "Parry " + Normal.parried +
            " (dengeyi kıran " + ParryBreaks + ")   " +
            "Block " + Normal.blocked +
            " (posture kırıldı " + PostureBreaks + ")   " +
            "Dash " + Dashes;
    }

    public string DamageTakenLine()
    {
        int max =
            playerHealth != null ? playerHealth.MaxHealth : 0;

        return
            "Alınan hasar " + DamageTotal + ":  " +
            "normal " + DamageNormal + " (" + Normal.hit + " vuruş)  " +
            "engellenemez " + DamageUnblockable +
            " (" + Unblockable.hit + " vuruş)" +
            (DamageOther > 0
                ? "  diğer " + DamageOther + " (" + HitsOther + ")"
                : "") +
            "   İyileşme " + Healed +
            "   En düşük can " + LowestHealth +
            (max > 0 ? "/" + max : "");
    }

    public string DamageDealtLine()
    {
        return
            "Verilen:  denge " + BalanceDealt +
            "   can " + HealthDealt +
            "   isabet " + Hits +
            "   kritik " + Crits + " (" + Pct(Crits, Hits) + ")" +
            "   denge kırma " + BalanceBreaks;
    }

    public string KillsLine()
    {
        return
            "Öldürme " + Kills +
            ":  execute " + KillsByExecute +
            "  vuruş " + KillsByAttack +
            "  slam " + KillsBySlam +
            "  diğer " + KillsOther;
    }

    public string HeaderLine()
    {
        return
            "Süre " + FormatTime(RunTime) +
            "   Bölüm " + StageReached +
            (WaveCountAtEnd > 1
                ? "  (dalga " + WaveReached + "/" + WaveCountAtEnd + ")"
                : "");
    }

    public string BuildSummary()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine(HeaderLine());
        sb.AppendLine(DeathText());
        sb.AppendLine(KillsLine());
        sb.AppendLine();
        sb.AppendLine(NormalLine());
        sb.AppendLine(UnblockableLine());
        sb.AppendLine(DefenseLine());
        sb.AppendLine();
        sb.AppendLine(DamageTakenLine());
        sb.AppendLine(DamageDealtLine());

        if (BuildText.Length > 0)
            sb.AppendLine("Build: " + BuildText);

        sb.AppendLine();
        sb.AppendLine("Bölüm | süre | öldürme | parry | block | yendi | hasar");

        for (int i = 0; i < stages.Count; i++)
        {
            StageRecord s = stages[i];

            sb.AppendLine(
                s.stage + " | " +
                FormatTime(s.time) + " | " +
                s.kills + " | " +
                s.parries + " | " +
                s.blocks + " | " +
                s.hitsTaken + " | " +
                s.damageTaken +
                (s.cleared ? "" : "  (öldü)")
            );
        }

        return sb.ToString();
    }

    // =========================================================
    // CSV
    // =========================================================

    private const string CsvHeader =
        "tarih;sure_sn;bolum;dalga;oldurme;execute;vurus_oldurme;" +
        "n_toplam;n_parry;n_block;n_dash;n_kacti;n_kesildi;n_iframe;n_yendi;" +
        "u_toplam;u_dash;u_kacti;u_kesildi;u_iframe;u_yendi;" +
        "parry_kiran;posture_kirildi;dash_sayisi;" +
        "hasar_normal;hasar_engellenemez;hasar_diger;iyilesme;en_dusuk_can;" +
        "denge_verilen;can_verilen;isabet;kritik;" +
        "olum_turu;olum_kaynak;build";

    private void WriteCsv()
    {
        try
        {
            string path =
                Path.Combine(Application.persistentDataPath, csvFileName);

            bool newFile = !File.Exists(path);

            CultureInfo inv = CultureInfo.InvariantCulture;

            string[] fields =
            {
                DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv),
                RunTime.ToString("0.0", inv),
                StageReached.ToString(inv),
                WaveReached.ToString(inv),
                Kills.ToString(inv),
                KillsByExecute.ToString(inv),
                KillsByAttack.ToString(inv),

                Normal.Total.ToString(inv),
                Normal.parried.ToString(inv),
                Normal.blocked.ToString(inv),
                Normal.dashed.ToString(inv),
                Normal.missed.ToString(inv),
                Normal.interrupted.ToString(inv),
                Normal.iFrame.ToString(inv),
                Normal.hit.ToString(inv),

                Unblockable.Total.ToString(inv),
                Unblockable.dashed.ToString(inv),
                Unblockable.missed.ToString(inv),
                Unblockable.interrupted.ToString(inv),
                Unblockable.iFrame.ToString(inv),
                Unblockable.hit.ToString(inv),

                ParryBreaks.ToString(inv),
                PostureBreaks.ToString(inv),
                Dashes.ToString(inv),

                DamageNormal.ToString(inv),
                DamageUnblockable.ToString(inv),
                DamageOther.ToString(inv),
                Healed.ToString(inv),
                LowestHealth.ToString(inv),

                BalanceDealt.ToString(inv),
                HealthDealt.ToString(inv),
                Hits.ToString(inv),
                Crits.ToString(inv),

                DiedByHit ? DeathKind.ToString() : "bilinmiyor",
                Csv(DeathSource),
                Csv(BuildText)
            };

            StringBuilder sb = new StringBuilder();

            if (newFile)
                sb.AppendLine(CsvHeader);

            sb.AppendLine(string.Join(";", fields));

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);

            Debug.Log("RunStats: koşu CSV'ye eklendi → " + path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("RunStats: CSV yazılamadı → " + e.Message);
        }
    }

    // Noktalı virgül ve satır sonu CSV'yi bozmasın.
    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value
            .Replace(";", ",")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }

    // =========================================================
    // YARDIMCI
    // =========================================================

    private static string DescribeBuild(CharmInventory inventory)
    {
        if (inventory == null)
            return "";

        IReadOnlyList<CharmInventory.Entry> entries = inventory.Entries;

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < entries.Count; i++)
        {
            CharmInventory.Entry e = entries[i];

            if (e.definition == null)
                continue;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(e.definition.displayName);

            if (e.stacks > 1)
                sb.Append(" x").Append(e.stacks);
        }

        return sb.ToString();
    }

    // "Enemy(Clone)" → "Enemy"
    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";

        return name.Replace("(Clone)", "").Trim();
    }
}