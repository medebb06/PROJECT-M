using UnityEngine;

public enum HitType
{
    Light,
    Heavy,
    Air
}

public struct HitData
{
    public int damage;
    public Vector2 knockback;
    public HitType type;

    public HitData(int damage, Vector2 knockback, HitType type)
    {
        this.damage = damage;
        this.knockback = knockback;
        this.type = type;
    }
}