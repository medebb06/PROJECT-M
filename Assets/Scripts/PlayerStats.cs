using System;
using System.Collections.Generic;
using UnityEngine;

public enum StatType
{
    BalanceDamage,   // düşman dengesine verilen hasar
    HealthDamage,    // düşman canına verilen hasar
    CritChance,      // 0..1
    CritMultiplier,  // kritikte hasar çarpanı

    // --- Davranış charm'ları için (taban değer kullanan yerde okunur) ---
    DamageTaken,     // oyuncunun aldığı hasar çarpanı (taban = gelen hasar)
    ParryWindow,     // parry penceresine eklenen saniye (taban = Inspector değeri)
    RiposteHits,     // riposte'a eklenen vuruş hakkı (taban 0)
    RiposteDuration, // riposte'a eklenen saniye (taban 0)
    RiposteStrength, // riposte bonuslarının çarpanı (taban 1)
    BlockHealthCost  // > 0 ise block posture yerine max canın bu oranı kadar CAN yer
}

/// <summary>
/// Oyuncunun istatistik katmanı. Charm'lar buraya DEĞİŞTİRİCİ (modifier)
/// ekler/çıkarır, hasar hesabı bu değerleri okur.
///
///   değer = (taban + Σ ekleme) × Π çarpan
///
/// Charm kendi nesnesini 'source' olarak verir; çıkarırken aynı nesneyle
/// o charm'ın tüm değiştiricileri tek çağrıda silinir:
///
///   stats.AddModifier(this, StatType.CritChance, add: 0.25f);
///   stats.RemoveModifiers(this);
///
/// Player üzerinde yoksa PlayerDamage ilk kullanımda kendiliğinden ekler.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    private class Modifier
    {
        public StatType type;
        public float add;
        public float mult;
        public object source;
    }

    private static PlayerStats current;
    private static float nextSearchTime;

    [Header("Base Stats")]
    [Tooltip("Charm'sız kritik şansı (0..1). Hasar akışını denemek için 0.5 yapabilirsin.")]
    [Range(0f, 1f)]
    [SerializeField] private float baseCritChance = 0f;

    [Tooltip("Kritik şansı tavanı (charm'lar ne kadar eklerse eklesin).")]
    [Range(0f, 1f)]
    [SerializeField] private float critChanceCap = 0.5f;

    [Tooltip("Kritik vuruşta hasar çarpanı.")]
    [Min(1f)]
    [SerializeField] private float baseCritMultiplier = 2f;

    [Header("Debug")]
    [Tooltip("Her vuruşta konsola neyin, ne kadar, kritik mi vurduğunu yazar.")]
    [SerializeField] private bool debugLog = false;

    // Sadece Inspector'da görmek için (salt okunur).
    [SerializeField]
    private List<string> activeModifiers =
        new List<string>();

    private readonly List<Modifier> modifiers =
        new List<Modifier>();

    // Değiştirici eklenince / çıkarılınca (arayüz için).
    public event Action StatsChanged;

    public bool DebugLog => debugLog;

    // =========================================================
    // ERİŞİM
    // =========================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        current = null;
        nextSearchTime = 0f;
    }

    // Sahnedeki oyuncunun PlayerStats'ı. Yoksa oluşturur.
    // Oyuncu henüz yoksa null döner (hasar yine de çalışır, stat'sız).
    public static PlayerStats Current
    {
        get
        {
            if (current != null)
                return current;

            if (Time.unscaledTime < nextSearchTime)
                return null;

            nextSearchTime = Time.unscaledTime + 0.5f;

            PlayerController player =
                FindFirstObjectByType<PlayerController>();

            if (player == null)
                return null;

            current =
                player.GetComponent<PlayerStats>();

            if (current == null)
            {
                current =
                    player.gameObject.AddComponent<PlayerStats>();
            }

            return current;
        }
    }

    private void Awake()
    {
        current = this;
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }

    // =========================================================
    // DEĞİŞTİRİCİLER
    // =========================================================

    public void AddModifier(
        object source,
        StatType type,
        float add = 0f,
        float mult = 1f
    )
    {
        modifiers.Add(
            new Modifier
            {
                type = type,
                add = add,
                mult = mult,
                source = source
            }
        );

        Changed();
    }

    // Bu kaynağın (charm'ın) tüm değiştiricilerini kaldırır.
    public void RemoveModifiers(object source)
    {
        int removed =
            modifiers.RemoveAll(m => m.source == source);

        if (removed > 0)
            Changed();
    }

    // (taban + ekleme) × çarpan
    public float Get(StatType type, float baseValue)
    {
        float add = 0f;
        float mult = 1f;

        for (int i = 0; i < modifiers.Count; i++)
        {
            Modifier m = modifiers[i];

            if (m.type != type)
                continue;

            add += m.add;
            mult *= m.mult;
        }

        return (baseValue + add) * mult;
    }

    // ---------------------------------------------------------
    // Hazır erişim yolları
    // ---------------------------------------------------------

    public float CritChance =>
        Mathf.Clamp(Get(StatType.CritChance, baseCritChance), 0f, critChanceCap);

    public float CritMultiplier =>
        Mathf.Max(1f, Get(StatType.CritMultiplier, baseCritMultiplier));

    // Stat'sız durumda da güvenle çağrılabilen kısa yol.
    public static float GetOr(StatType type, float baseValue)
    {
        PlayerStats stats = Current;

        return stats != null
            ? stats.Get(type, baseValue)
            : baseValue;
    }

    private void Changed()
    {
        activeModifiers.Clear();

        for (int i = 0; i < modifiers.Count; i++)
        {
            Modifier m = modifiers[i];

            activeModifiers.Add(
                m.type + "  +" + m.add + "  x" + m.mult +
                "  (" + (m.source != null ? m.source.ToString() : "?") + ")"
            );
        }

        StatsChanged?.Invoke();
    }
}
