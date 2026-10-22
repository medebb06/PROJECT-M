using System.Collections.Generic;
using UnityEngine;

// Düşmanın üstünde vurdukça ADIM ADIM artan toplam hasar sayacı.
// 20 iken 5 vurunca: 21-22-23-24-25 diye hızlıca sayar. Bir süre vurulmazsa söner ve sıfırlanır.
public class DamageCounter : MonoBehaviour
{
    private class Entry
    {
        public EnemyBase enemy;
        public int target;       // gerçek toplam
        public float shown;      // ekranda sayılan
        public float lastHit;
        public float pop;        // son artışta büyüme
        public float tick;
    }

    private static DamageCounter instance;
    private readonly Dictionary<EnemyBase, Entry> entries = new Dictionary<EnemyBase, Entry>();
    private readonly List<EnemyBase> remove = new List<EnemyBase>();
    private GUIStyle style;

    public float resetAfter = 2.0f;
    public float fadeTime = 0.4f;
    public float stepInterval = 0.025f;   // her sayı artışı arası (sn)
    public float heightAbove = 1.65f;     // can barı / yük çizgilerinin üstü

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        GameObject go = new GameObject("DamageCounter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DamageCounter>();
    }

    private void OnEnable() { CombatEvents.EnemyHit += OnHit; CombatEvents.EnemyKilled += OnKilled; }
    private void OnDisable() { CombatEvents.EnemyHit -= OnHit; CombatEvents.EnemyKilled -= OnKilled; }

    private void OnHit(EnemyBase enemy, DamageInfo info, HitResult r)
    {
        if (enemy == null || !r.hit || r.amount <= 0) return;
        AddInternal(enemy, r.amount);
    }

    // Zehir / patlama gibi vuruş dışı hasarlar da sayaca yansısın.
    public static void Add(EnemyBase enemy, int amount)
    {
        if (instance != null && enemy != null && amount > 0) instance.AddInternal(enemy, amount);
    }

    private void AddInternal(EnemyBase enemy, int amount)
    {
        Entry e;
        if (!entries.TryGetValue(enemy, out e))
        {
            e = new Entry();
            e.enemy = enemy;
            entries[enemy] = e;
        }
        if (Time.time - e.lastHit > resetAfter + fadeTime) { e.target = 0; e.shown = 0f; }
        e.target += amount;
        e.lastHit = Time.time;
        e.pop = 1f;   // squash: vuruş başına bir kez
    }

    private void OnKilled(EnemyBase enemy)
    {
        // Ölen düşmanda sayaç kısa süre kalıp söner.
        Entry e;
        if (entries.TryGetValue(enemy, out e)) e.lastHit = Mathf.Min(e.lastHit, Time.time - resetAfter + 0.6f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        remove.Clear();
        foreach (var kv in entries)
        {
            Entry e = kv.Value;
            if (e.enemy == null || Time.time - e.lastHit > resetAfter + fadeTime) { remove.Add(kv.Key); continue; }
            e.pop = Mathf.MoveTowards(e.pop, 0f, dt * 12f);
            if (e.shown < e.target)
            {
                e.tick -= dt;
                // Fark büyükse daha hızlı say (en fazla ~0.5 sn'de yetişsin).
                float interval = Mathf.Min(stepInterval, 0.5f / Mathf.Max(1f, e.target - e.shown));
                while (e.tick <= 0f && e.shown < e.target)
                {
                    e.shown += 1f;
                    e.tick += interval;
                }
            }
        }
        for (int i = 0; i < remove.Count; i++) entries.Remove(remove[i]);
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || entries.Count == 0) return;
        Camera cam = GameCamera.Get();
        if (cam == null) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.clipping = TextClipping.Overflow;
            style.wordWrap = false;
            Font pf = Resources.Load<Font>("Pixeltype");
            if (pf != null) style.font = pf;
        }

        GUI.depth = -50;
        float gs = GameSettings.GuiScale(0.7f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(gs, gs, 1f));

        foreach (var kv in entries)
        {
            Entry e = kv.Value;
            if (e.enemy == null) continue;

            float top = e.enemy.transform.position.y;
            SpriteRenderer sr = e.enemy.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) top = sr.bounds.max.y;
            Vector3 world = new Vector3(e.enemy.transform.position.x, top + heightAbove, 0f);
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) continue;

            float age = Time.time - e.lastHit;
            float alpha = age < resetAfter ? 1f : Mathf.Clamp01(1f - (age - resetAfter) / fadeTime);

            int v = Mathf.RoundToInt(e.shown);
            // Küçük = beyaz, büyüdükçe sarı -> turuncu -> kırmızı.
            float k = Mathf.Clamp01(v / 60f);
            Color c = k < 0.5f ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.25f), k * 2f)
                               : Color.Lerp(new Color(1f, 0.85f, 0.25f), new Color(1f, 0.3f, 0.2f), (k - 0.5f) * 2f);
            c.a = alpha;

            style.fontSize = 20;
            float x = sp.x / gs;
            float y = (Screen.height - sp.y) / gs;
            // Squash & stretch: her artışta dikey uzar, yatay incelir, sonra yaylanarak yerine oturur.
            float sy = 1f + 0.55f * e.pop;
            float sx = 1f - 0.3f * e.pop;
            GUI.matrix = Matrix4x4.Scale(new Vector3(gs, gs, 1f)) *
                         Matrix4x4.TRS(new Vector3(x, y, 0f), Quaternion.identity, new Vector3(sx, sy, 1f)) *
                         Matrix4x4.TRS(new Vector3(-x, -y, 0f), Quaternion.identity, Vector3.one);
            Rect r = new Rect(x - 100f, y - 24f, 200f, 48f);
            string s = v.ToString();

            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f * alpha);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s, style);
            GUI.Label(new Rect(r.x - 1f, r.y + 2f, r.width, r.height), s, style);
            style.normal.textColor = c;
            GUI.Label(r, s, style);
        }

        GUI.matrix = old;
    }
}
