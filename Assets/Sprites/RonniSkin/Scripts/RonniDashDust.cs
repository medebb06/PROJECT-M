using System.Collections.Generic;
using UnityEngine;

namespace RonniSkin
{
    /// Dash toz efektleri (Resources/RonniFX/dash_burst + dash_skid sprite sheet'leri):
    ///  - burst: dash'in başladığı yerde, ayak hizasında yere bırakılan toz patlaması
    ///  - skid : dash boyunca yerde bırakılan kısa toz çizgileri
    /// Efektler dünyada sabit kalır (karakteri takip etmez), kendi kendine oynayıp yok olur.
    public class RonniDashDust : MonoBehaviour
    {
        public string burstResource = "RonniFX/dash_burst";
        public string skidResource = "RonniFX/dash_skid";
        public int burstFrames = 4, skidFrames = 3;
        public float burstFps = 14f, skidFps = 12f;
        [Tooltip("Efekt çerçevesinin dünya genişliği = karakter boyu x bu.")] public float burstSize = 2.0f;
        public float skidSize = 1.7f;
        [Tooltip("İki yer çizgisi arasındaki mesafe = karakter boyu x bu.")] public float skidSpacing = 0.8f;
        public Color tint = Color.white;

        PlayerController player; RonniSkinRig rig;
        Sprite[] burst, skid;
        string layer; int order; float worldH;
        float lastSkidX; bool haveSkid;

        class Fx { public SpriteRenderer sr; public Sprite[] frames; public float fps, t; }
        readonly List<Fx> live = new List<Fx>();

        public void Init(PlayerController p, RonniSkinRig r)
        {
            if (player == p && rig == r && burst != null) return;
            player = p; rig = r;
            if (rig == null || !rig.IsBuilt) return;
            int minOrder = int.MaxValue; layer = rig.sortingLayerName;
            foreach (var mr in rig.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.sortingOrder < minOrder) { minOrder = mr.sortingOrder; layer = mr.sortingLayerName; }
            order = (minOrder == int.MaxValue ? 0 : minOrder) - 1;
            worldH = rig.HeightPx * rig.unitsPerPixel * Mathf.Abs(rig.transform.lossyScale.y);
            burst = Slice(burstResource, burstFrames, new Vector2(0.78f, 0.06f), burstSize);
            skid = Slice(skidResource, skidFrames, new Vector2(0.42f, 0.10f), skidSize);
        }

        Sprite[] Slice(string res, int n, Vector2 pivot, float size)
        {
            var tex = Resources.Load<Texture2D>(res);
            if (tex == null) { Debug.LogWarning("RonniDashDust: " + res + " bulunamadı"); return null; }
            int fw = tex.width / 4; // sheet 4 slot genişliğinde (kullanılmayan slotlar boş)
            float ppu = fw / (worldH * size);
            var arr = new Sprite[n];
            for (int i = 0; i < n; i++)
                arr[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, tex.height), pivot, ppu);
            return arr;
        }

        float GroundY()
        {
            if (player != null && player.col != null) return player.col.bounds.min.y;
            return transform.position.y;
        }

        void Spawn(Sprite[] frames, float fps, float x, float dir)
        {
            if (frames == null || frames.Length == 0) return;
            var go = new GameObject("~dashFx");
            go.hideFlags = HideFlags.DontSave;
            go.transform.position = new Vector3(x, GroundY(), 0f);
            go.transform.localScale = new Vector3(dir >= 0f ? 1f : -1f, 1f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0]; sr.color = tint;
            sr.sortingLayerName = layer; sr.sortingOrder = order;
            live.Add(new Fx { sr = sr, frames = frames, fps = fps });
        }

        /// Dash başında bir kez.
        public void Begin(float dir)
        {
            haveSkid = false;
            Spawn(burst, burstFps, transform.position.x, dir);
        }

        /// Dash boyunca her karede (aktifken yerde çizgi bırakır).
        public void Tick(bool active, float dir)
        {
            if (!active || player == null || !player.isGrounded) return;
            float x = transform.position.x;
            if (!haveSkid) { lastSkidX = x; haveSkid = true; Spawn(skid, skidFps, x, dir); return; }
            if (Mathf.Abs(x - lastSkidX) >= worldH * skidSpacing)
            {
                lastSkidX = x;
                Spawn(skid, skidFps, x, dir);
            }
        }

        void Update()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var f = live[i];
                if (f.sr == null) { live.RemoveAt(i); continue; }
                f.t += Time.deltaTime;
                int idx = Mathf.FloorToInt(f.t * f.fps);
                if (idx >= f.frames.Length) { Destroy(f.sr.gameObject); live.RemoveAt(i); continue; }
                f.sr.sprite = f.frames[idx];
            }
        }

        void OnDestroy()
        {
            foreach (var f in live) if (f.sr != null) Destroy(f.sr.gameObject);
            live.Clear();
        }
    }
}
