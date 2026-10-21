using UnityEngine;

// BOSS ARENASI: boss uyanip dovus basladiginda alanin iki ucunda KAPILAR (duvarlar) yukselir.
// Dovus boyunca ne oyuncu ne boss alandan cikabilir; dalgalar da alan disina gitmez.
// Boss olunce (ya da oyuncu olunce) kapilar iner. GiantWarriorEnemy calisirken kendisi olusturur;
// sahnede elle kurulum GEREKMEZ.
public class BossArena : MonoBehaviour
{
    public float minX, maxX;
    public float doorWidth = 4f;
    public float doorHeight = 90f;

    public bool Closed { get; private set; }

    GiantWarriorEnemy boss;
    Health bossHealth;
    PlayerController player;
    Rigidbody2D bossRb;

    GameObject colL, colR;
    Transform slabL, slabR;
    float groundL, groundR;
    float anim;            // 0 = acik (yerde), 1 = tam kapali
    bool pendingClose;
    bool finished;

    static Sprite white;

    public void Init(GiantWarriorEnemy b, float min, float max, float startY)
    {
        boss = b;
        minX = min;
        maxX = max;
        bossHealth = b.GetComponent<Health>();
        bossRb = b.GetComponent<Rigidbody2D>();

        groundL = FindGround(minX, startY);
        groundR = FindGround(maxX, startY);

        colL = BuildDoor("ArenaDoorL", minX, groundL, out slabL);
        colR = BuildDoor("ArenaDoorR", maxX, groundR, out slabR);
        colL.SetActive(false);
        colR.SetActive(false);
    }

    static float FindGround(float x, float fallbackY)
    {
        int mask = LayerMask.GetMask("Ground");
        if (mask != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, fallbackY + 60f), Vector2.down, 300f, mask);
            if (hit.collider != null) return hit.point.y;
        }
        return fallbackY - 0.2f;
    }

    GameObject BuildDoor(string n, float x, float groundY, out Transform slab)
    {
        // Kolayca fizik: gorunmeyen kutu collider (Ground katmani: oyuncu ve boss carpar).
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        int gl = LayerMask.NameToLayer("Ground");
        if (gl >= 0) go.layer = gl;
        go.transform.position = new Vector3(x, groundY + doorHeight * 0.5f - 2f, 0f);

        BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(doorWidth, doorHeight);

        // Gorsel: yerden yukselen koyu kirmizi levha + parlak ic serit.
        if (white == null)
        {
            Texture2D t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            t.SetPixels(px);
            t.Apply();
            t.filterMode = FilterMode.Point;
            white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);   // 1x1 birim, pivot alt orta
        }

        GameObject vis = new GameObject(n + "_Visual");
        vis.transform.SetParent(transform, false);
        vis.transform.position = new Vector3(x, groundY - 2f, 0f);
        slab = vis.transform;

        SpriteRenderer sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = white;
        sr.color = new Color(0.22f, 0.03f, 0.04f, 0.96f);
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 60;
        vis.transform.localScale = new Vector3(doorWidth, 0.01f, 1f);

        GameObject core = new GameObject("Core");
        core.transform.SetParent(vis.transform, false);
        core.transform.localScale = new Vector3(0.25f, 1f, 1f);
        SpriteRenderer cs = core.AddComponent<SpriteRenderer>();
        cs.sprite = white;
        cs.color = new Color(1f, 0.15f, 0.08f, 0.9f);
        cs.sortingLayerName = "Default";
        cs.sortingOrder = 61;

        return go;
    }

    // Dovus basladi: kapilari kapat (oyuncu alanin icindeyse).
    public void RequestClose()
    {
        if (finished) return;
        pendingClose = true;
    }

    // Dovus bitti: kapilari ac.
    public void Open()
    {
        pendingClose = false;
        if (!Closed) return;
        Closed = false;
        if (colL != null) colL.SetActive(false);
        if (colR != null) colR.SetActive(false);
    }

    public bool Contains(float x, float margin = 0f)
    {
        return x >= minX - margin && x <= maxX + margin;
    }

    void Update()
    {
        if (boss == null || (bossHealth != null && bossHealth.IsDead))
        {
            Open();
            finished = true;
        }

        if (player == null) player = FindFirstObjectByType<PlayerController>();

        if (pendingClose && !Closed && !finished && player != null)
        {
            float px = player.transform.position.x;
            if (px > minX + doorWidth && px < maxX - doorWidth)
            {
                Closed = true;
                pendingClose = false;
                if (colL != null) colL.SetActive(true);
                if (colR != null) colR.SetActive(true);
                RunCameraZoom.AddShake(2.5f);
            }
        }

        // Oyuncu olduyse dovus bitti: kapilar acilir (yeniden denemede tekrar kapanir).
        if (Closed && boss != null && boss.PlayerDeadForArena())
            Open();

        float target = Closed ? 1f : 0f;
        anim = Mathf.MoveTowards(anim, target, Time.unscaledDeltaTime * (Closed ? 2.2f : 1.5f));
        float h = Mathf.Max(0.01f, doorHeight * anim);
        if (slabL != null) slabL.localScale = new Vector3(doorWidth, h, 1f);
        if (slabR != null) slabR.localScale = new Vector3(doorWidth, h, 1f);

        if (finished && anim <= 0f)
            Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (!Closed) return;

        float lo = minX + doorWidth * 0.5f + 0.6f;
        float hi = maxX - doorWidth * 0.5f - 0.6f;

        Clamp(player != null ? player.rb : null, lo, hi);
        Clamp(bossRb, lo, hi);
    }

    static void Clamp(Rigidbody2D rb, float lo, float hi)
    {
        if (rb == null) return;
        Vector2 p = rb.position;
        float cx = Mathf.Clamp(p.x, lo, hi);
        if (Mathf.Approximately(cx, p.x)) return;
        rb.position = new Vector2(cx, p.y);
        Vector2 v = rb.linearVelocity;
        if ((p.x < lo && v.x < 0f) || (p.x > hi && v.x > 0f)) v.x = 0f;
        rb.linearVelocity = v;
    }
}
