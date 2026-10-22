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
        p.sr.sprite = ArrowSprite();
        p.sr.sortingLayerID = sortingLayerId;
        p.sr.sortingOrder = sortingOrder + 3;
        p.sr.color = new Color(1f, 0.97f, 0.7f, 1f);
        // Okunur ok: uzun, kalın, parlak; sprite 4x1 birim (64x16 px, 16 ppu) → uzunluk ~3.6, yükseklik ~0.9.
        float k = Mathf.Max(0.9f, p.radius * 2.6f);
        go.transform.localScale = new Vector3(k, k, 1f);
        if (p.dir.x < 0f) p.sr.flipX = true;

        // Kısa, parlak iz: ok hızlı da olsa nereden geldiği görünsün.
        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.14f;
        tr.startWidth = 0.45f * k;
        tr.endWidth = 0f;
        tr.material = new Material(Shader.Find("Sprites/Default"));
        tr.startColor = new Color(1f, 0.95f, 0.5f, 0.85f);
        tr.endColor = new Color(1f, 0.8f, 0.2f, 0f);
        tr.sortingLayerID = sortingLayerId;
        tr.sortingOrder = sortingOrder + 2;
        return p;
    }

    private static Sprite arrowSprite;

    // Kodla çizilen ok: koyu konturlu, parlak gövde, üçgen uç, kuyruk tüyü. Sağa bakar.
    private static Sprite ArrowSprite()
    {
        if (arrowSprite != null) return arrowSprite;

        const int w = 64, h = 16;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < px.Length; i++) px[i] = clear;

        Color fill = Color.white;
        Color edge = new Color(0.12f, 0.08f, 0.02f, 0.95f);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float cy = y + 0.5f - h * 0.5f;
                float half = 0f;

                if (x >= 46) half = Mathf.Lerp(7.5f, 0f, (x - 46) / 17f);        // uç (üçgen)
                else if (x >= 6) half = 2.0f;                                      // gövde
                if (x < 14) half = Mathf.Max(half, Mathf.Lerp(7f, 2f, x / 14f));  // kuyruk tüyü

                float d = half - Mathf.Abs(cy);
                if (d > 1.1f) px[y * w + x] = fill;
                else if (d > 0f) px[y * w + x] = edge;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        return arrowSprite;
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
