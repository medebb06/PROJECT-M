using UnityEngine;

// Bir charm'ın etkilediği şeylere erişim.
public class CharmContext
{
    public PlayerStats stats;
    public Health playerHealth;
    public PlayerController player;
    public PlayerPosture posture;

    // Bu charm örneğinin kimliği: stat değiştiricileri bununla kaydedilir
    // ve çıkarılırken bununla silinir.
    public object owner;
}

/// <summary>
/// Bir charm'ın DAVRANIŞI. Her charm envantere girerken bu efektin KOPYASI
/// oluşturulur, yani durum (state) o koşuya özeldir.
///
/// Apply: charm eklenince ve her istif (stack) artışında çağrılır.
///        Tekrar çağrılabilir olmalı (idempotent).
/// Remove: koşu bitince çağrılır; her şeyi geri al (olay aboneliği, stat).
/// Status: HUD'da charm adının yanında gösterilen anlık durum
///         (ör. "Ritim 3/5", "AKTİF"). Boş = gösterme.
/// </summary>
public abstract class CharmEffect : ScriptableObject
{
    public abstract void Apply(CharmContext context, int stacks);

    public abstract void Remove(CharmContext context);

    // Seçim ekranında gösterilecek "bu istifte ne yapar" satırı.
    public virtual string Describe(int stacks)
    {
        return "";
    }

    public virtual string Status()
    {
        return "";
    }
}
