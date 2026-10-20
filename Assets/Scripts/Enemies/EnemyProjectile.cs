using UnityEngine;

// Okçunun mermisi: yatay, düz çizgide, sabit hızla gider (tahmin edilebilir).
// Oyuncuya değince savunma kararını sahibi (EnemyUnit.ResolveProjectileHit) verir:
//   dash/iframe = içinden geçer | parry = GERİ YANSIR, sahibine posture hasarı | blok = söner | aksi = hasar.
// Zemin/duvar gibi katı nesneye çarpınca söner. Hız EnemyTime ile yavaşlar (parry slow-mo).
public class EnemyProjectile : MonoBehaviour
{
    private EnemyUnit owner;
    private AttackDef atk;
    private Vector2 dir;
    private float speed;
    private float life;
    private float radius;
    private bool reflected;
    private bool ignorePlayer;
    private SpriteRenderer sr;

    public static EnemyProjectile Spawn(EnemyUnit owner, AttackDef atk, Vector3 pos, float dirX, Sprite sprite, int sortingLayerId, int sortingOrder)
    {
        GameObject go = new GameObject("EnemyProjectile");
        go.transform.position = pos;
        EnemyProjectile p = go.AddComponent<EnemyProjectile>();
        p.owner = owner;
        p.atk = atk;
        p.dir = new Vector2(dirX >= 0f ? 1f : -1f, 0f);
        p.speed = atk.projectileSpeed;
        p.life = atk.projectileLife;
        p.radius = Mathf.Max(0.05f, atk.projectileRadius);

        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sprite = sprite;
        p.sr.sortingLayerID = sortingLayerId;
        p.sr.sortingOrder = sortingOrder;
        p.sr.color = new Color(1f, 0.95f, 0.8f, 1f);
        go.transform.localScale = new Vector3(p.radius * 4.5f, p.radius * 0.9f, 1f);
        return p;
    }

    private void Update()
    {
        float dt = EnemyTime.DeltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        float dist = speed * dt;
        Vector2 from = transform.position;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(from, radius, dir, dist);
        float bestDist = float.MaxValue;
        RaycastHit2D best = default(RaycastHit2D);
        bool any = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger) continue;
            if (!IsRelevant(c)) continue;
            if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = hits[i]; any = true; }
        }

        if (!any) { transform.position = from + dir * dist; return; }

        transform.position = from + dir * Mathf.Max(0f, best.distance);
        Collider2D hit = best.collider;

        PlayerController player = hit.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            int r = owner != null ? owner.ResolveProjectileHit(dir, atk, transform.position) : 0;
            if (r == 3) { ignorePlayer = true; transform.position = from + dir * dist; return; }   // dodge: içinden geçti
            if (r == 1) { Reflect(); return; }                                                     // parry: geri yansıdı
            Destroy(gameObject);                                                                   // hasar / blok
            return;
        }

        EnemyBase e = hit.GetComponentInParent<EnemyBase>();
        if (e != null)
        {
            if (reflected)
            {
                EnemyUnit u = e as EnemyUnit;
                if (u != null) u.ReceiveReflected(dir); else e.ApplyBalanceHit(dir);
                Destroy(gameObject);
            }
            return;
        }

        Destroy(gameObject);   // zemin / duvar
    }

    // Çarpışma filtresi: sahibini ve (yansımadıysa) diğer düşmanları geç; dodge sonrası oyuncuyu geç.
    private bool IsRelevant(Collider2D c)
    {
        EnemyBase e = c.GetComponentInParent<EnemyBase>();
        if (e != null) return reflected;
        PlayerController p = c.GetComponentInParent<PlayerController>();
        if (p != null) return !reflected && !ignorePlayer;
        return true;
    }

    private void Reflect()
    {
        reflected = true;
        dir = -dir;
        speed *= 1.35f;
        life = atk.projectileLife;
        if (sr != null) sr.color = new Color(1f, 0.85f, 0.35f, 1f);
        transform.position += (Vector3)(dir * (radius * 2f));
    }
}
