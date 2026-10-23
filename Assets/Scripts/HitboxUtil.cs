using UnityEngine;

/// <summary>
/// Hitbox / collider yardimcilari: iki govde ust uste bindiginde oyuncuyu temiz sekilde disari alir
/// (fizigin yavas, zipirdayan "itme"sine birakmaz).
/// </summary>
public static class HitboxUtil
{
    static readonly Collider2D[] buf = new Collider2D[8];

    /// <summary>
    /// 'mover' (oyuncu govdesi) 'obstacle' icindeyse YATAY olarak en yakin kenara tasir.
    /// Dondurulen: tasindi mi.
    /// </summary>
    public static bool SeparateHorizontally(Rigidbody2D rb, Collider2D mover, Collider2D obstacle, float margin = 0.05f, float forceSide = 0f)
    {
        if (rb == null || mover == null || obstacle == null || !obstacle.enabled || obstacle.isTrigger)
            return false;

        ColliderDistance2D d = mover.Distance(obstacle);
        if (!d.isOverlapped)
            return false;

        Bounds m = mover.bounds;
        Bounds o = obstacle.bounds;

        float side = Mathf.Sign(m.center.x - o.center.x);
        if (forceSide != 0f)
            side = Mathf.Sign(forceSide);   // dash yönünde: düşmanın ARKASINA çıkar, geri fırlamaz
        if (side == 0f)
            side = 1f;

        float shift = side > 0f
            ? (o.max.x - m.min.x)
            : -(m.max.x - o.min.x);

        rb.position += new Vector2(shift + side * margin, 0f);
        return true;
    }

    /// <summary>Dash vb. sonrasi oyuncu bir dusmanin govdesinin icinde kaldiysa disari alir.</summary>
    public static void EjectFromEnemies(PlayerController p, float preferDir = 0f)
    {
        if (p == null || p.col == null || p.rb == null)
            return;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0)
            return;

        ContactFilter2D f = new ContactFilter2D();
        f.useTriggers = false;
        f.useLayerMask = true;
        f.SetLayerMask(1 << enemyLayer);

        int n = Physics2D.OverlapCollider(p.col, f, buf);

        for (int i = 0; i < n; i++)
        {
            if (buf[i] == null)
                continue;

            if (SeparateHorizontally(p.rb, p.col, buf[i], 0.05f, preferDir))
                Physics2D.SyncTransforms();
        }
    }
}
