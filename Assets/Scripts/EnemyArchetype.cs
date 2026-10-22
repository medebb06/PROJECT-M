using UnityEngine;

// Küçük köprü: eski düşman sınıflarının (LegacyBoss) derlenmesi için.
public enum EnemyArchetypeType
{
    Duelist,
    Quick,
    Heavy,
    Archer,
    Swarm,
    Shielded,
    Flyer,
    Bomber,
    Practice    // Antrenman Düellocusu: özel saldırı yok, oyuncuya 0 hasar
}

public class EnemyArchetype : MonoBehaviour
{
    public EnemyArchetypeType type = EnemyArchetypeType.Duelist;
}
