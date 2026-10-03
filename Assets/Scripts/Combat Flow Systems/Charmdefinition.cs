using UnityEngine;

/// <summary>
/// Bir charm'ın VERİSİ. Kod yazmadan yeni charm eklemek için:
/// Project penceresinde Create > Charms > Charm, bir CharmEffect ata,
/// RunManager'daki 'Extra Charms' listesine ekle.
/// (Varsayılan charm'lar kodla kendiliğinden üretilir, asset gerekmez.)
/// </summary>
[CreateAssetMenu(menuName = "Charms/Charm", fileName = "NewCharm")]
public class CharmDefinition : ScriptableObject
{
    public string displayName = "Charm";

    [TextArea]
    public string description = "";

    public Sprite icon;

    [Tooltip("En fazla kaç kez alınabilir (istiflenir). 0 = sınırsız.")]
    [Min(0)]
    public int maxStacks = 5;

    [Tooltip("Seçim ekranında çıkma ağırlığı. Büyük = daha sık.")]
    [Min(0f)]
    public float weight = 1f;

    public CharmEffect effect;
}