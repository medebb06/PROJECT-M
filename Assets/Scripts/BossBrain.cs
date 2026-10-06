using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BOSS BEYNİ (Gölge Hilali): oyuncunun durumunu okuyup hamle ağırlıklarını değiştirir.
/// Boss ANLIK tepki vermez: bir durumu fark etmesi için 'reactionDelay' kadar sürmesi gerekir.
///
///   • İKSİR İÇİYOR   → yakın baskı hamleleri + özel yetenekler erken gelir (içmeyi bozmak için).
///   • UZAKTA         → menzilli hamleler (Hilal Dalgası, Çifte Hilal).
///   • ÇOK KAÇIYOR    → (3+ dash / 6 sn) geniş/menzilli hamleler, Gölge Zinciri.
///   • HEP BLOK       → engellenemez hamleler (Gölge Atılışı, Gölge Kesiği) ve Keşiş Seli.
///   • HAVADA         → menzilli hamleler.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class BossBrain : MonoBehaviour
{
    [Header("Tepki")]
    [Tooltip("Boss bir durumu fark edip tepki vermeden önce geçen süre (sn).")]
    public float reactionDelay = 0.4f;

    [Header("Eşikler")]
    public float farDistance = 7f;
    public float nearDistance = 3.5f;
    public int dashSpamCount = 3;
    public float dashWindow = 6f;
    [Range(0f, 1f)] public float blockSpamLevel = 0.45f;

    private EnemyController enemy;
    private PlayerController player;
    private PlayerDefenseController defense;

    private float drinkTimer;
    private float airTimer;
    private float blockEma;
    private bool wasDashing;
    private readonly List<float> dashTimes = new List<float>();

    private bool loggedPotion;

    public bool PotionActive { get; private set; }
    public bool PlayerFar { get; private set; }
    public bool PlayerNear { get; private set; }
    public bool DashSpam { get; private set; }
    public bool BlockSpam { get; private set; }
    public bool PlayerAirborne { get; private set; }

    /// <summary>Özel yeteneklerin (koşu atağı, zemin dalgası) bekleme süresinden kaç sn erken gelebileceği.</summary>
    public float EarlySkillBonus => PotionActive ? 3.5f : (DashSpam ? 1.5f : 0f);

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
    }

    private void Update()
    {
        if (enemy == null || enemy.IsDead || enemy.target == null)
            return;

        if (player == null || player.transform != enemy.target)
        {
            player = enemy.target.GetComponent<PlayerController>();
            defense = enemy.target.GetComponent<PlayerDefenseController>();
        }

        if (player == null)
            return;

        float dt = Time.deltaTime;

        // ---- İksir: içmeye başladıktan 'reactionDelay' sn sonra fark eder ----
        PlayerPotion potion = PlayerPotion.Instance;

        if (potion != null && potion.IsDrinking)
            drinkTimer += dt;
        else
            drinkTimer = Mathf.Max(0f, drinkTimer - dt * 3f);

        bool was = PotionActive;
        PotionActive = drinkTimer >= reactionDelay;

        if (PotionActive && !was && !loggedPotion)
        {
            loggedPotion = true;
            Debug.Log("BOSS BEYNİ: oyuncunun iksir içtiğini fark etti → baskı.");
        }

        if (!PotionActive)
            loggedPotion = false;

        // ---- Mesafe ----
        float d = Mathf.Abs(enemy.target.position.x - transform.position.x);

        PlayerFar = d >= farDistance;
        PlayerNear = d <= nearDistance;

        // ---- Havada mı? (kısa gecikmeyle) ----
        if (!player.isGrounded)
            airTimer += dt;
        else
            airTimer = 0f;

        PlayerAirborne = airTimer >= reactionDelay;

        // ---- Dash spamı ----
        if (player.isDashing && !wasDashing)
            dashTimes.Add(Time.time);

        wasDashing = player.isDashing;

        dashTimes.RemoveAll(t => Time.time - t > dashWindow);

        DashSpam = dashTimes.Count >= dashSpamCount;

        // ---- Blok alışkanlığı (yavaş değişen ortalama) ----
        float blocking = defense != null && defense.IsBlocking ? 1f : 0f;

        blockEma += (blocking - blockEma) * Mathf.Clamp01(dt / 4f);

        BlockSpam = blockEma >= blockSpamLevel;
    }

    // EnemyMoveset hamle ağırlığı çarpanı.
    public float Modify(AttackMove move)
    {
        if (move == null)
            return 1f;

        string n = move.name;

        bool ranged = n == "Hilal Dalgası" || n == "Çifte Hilal";
        bool pressure = n == "Keşiş Seli" || n == "Kırık Tempo" || n == "Gölge Atılışı" || n == "Ritim Kırıcı";
        bool unblockable = n == "Gölge Atılışı" || n == "Gölge Kesiği";

        float m = 1f;

        if (PotionActive)
        {
            if (n == "Keşiş Seli")
                m *= 2.5f;
            else if (pressure)
                m *= 1.8f;
            else if (ranged)
                m *= 1.5f; // uzaktan da içmeyi bozar
        }

        if (PlayerFar && ranged)
            m *= 1.6f;

        if (PlayerNear && pressure)
            m *= 1.3f;

        if (DashSpam)
        {
            if (ranged)
                m *= 1.6f;

            if (n == "Gölge Zinciri")
                m *= 1.4f;
        }

        if (BlockSpam)
        {
            if (unblockable)
                m *= 2.0f;

            if (n == "Keşiş Seli")
                m *= 1.3f;
        }

        if (PlayerAirborne && ranged)
            m *= 1.4f;

        return m;
    }
}
