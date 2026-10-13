using UnityEngine;

namespace RonniSkin
{
    /// Idle "wait" animasyonundaki ateş böceği: uzaktan gelir, uzanan elin üstüne konar, parlar, sonra uçup gider.
    /// Zaman çizelgesi Resources/RonniIdle "wait" klibiyle (9.6 sn) aynı akar. Köprü (RonniPlayerBridge) başlatır.
    [DefaultExecutionOrder(500)]
    public class RonniFirefly : MonoBehaviour
    {
        [Header("Zaman çizelgesi (wait klibiyle aynı, sn)")]
        public float tAppear = 1.0f;
        public float tLand = 2.9f;
        public float tLeave = 6.6f;
        public float tEnd = 9.4f;

        [Header("Görünüm (kaynak piksel; 1 px = rig birimi)")]
        public Color coreColor = new Color(1f, 0.97f, 0.6f, 1f);
        public Color haloColor = new Color(0.72f, 1f, 0.3f, 1f);
        public float coreSize = 46f, haloSize = 250f, outerSize = 560f;
        [Tooltip("Eldeki konma noktasına göre başlangıç uzaklığı (px); x dışa doğru işaretlenir.")] public Vector2 startOffset = new Vector2(1700f, 1500f);
        public Vector2 leaveOffset = new Vector2(700f, 1750f);

        RonniSkinRig rig;
        Transform perch;
        float outward = -1f, t, cancelT;
        bool active, cancelling;
        SpriteRenderer core, halo, outer;
        static Sprite softSprite, coreSprite;
        static Material mat;

        void Awake()
        {
            if (softSprite == null) softSprite = MakeGlow(64, 2.2f);
            if (coreSprite == null) coreSprite = MakeGlow(64, 0.8f);
            outer = MakeSr("outer", softSprite);
            halo = MakeSr("halo", softSprite);
            core = MakeSr("core", coreSprite);
            SetVisible(false);
        }

        static Sprite MakeGlow(int n, float pow)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), pow);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 1 dünya birimi genişliğinde
        }

        static Material GetMat()
        {
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                mat = new Material(sh) { name = "Ronni_Firefly" };
            }
            return mat;
        }

        SpriteRenderer MakeSr(string n, Sprite s)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sharedMaterial = GetMat();
            return sr;
        }

        void SetVisible(bool v)
        {
            if (core != null) core.enabled = v;
            if (halo != null) halo.enabled = v;
            if (outer != null) outer.enabled = v;
        }

        public void Begin(RonniSkinRig r, Transform perchPoint, float outwardSign)
        {
            rig = r; perch = perchPoint; outward = outwardSign == 0f ? -1f : Mathf.Sign(outwardSign);
            t = 0f; cancelT = 0f; cancelling = false; active = true;
            SetVisible(false);
        }

        /// Oyuncu kıpırdadıysa: ateş böceği hızla sönüp kaybolur.
        public void Cancel()
        {
            if (!active) return;
            cancelling = true; cancelT = 0f;
        }

        void Finish() { active = false; cancelling = false; SetVisible(false); }

        // Konma noktasına göre ofset (px) ve saydamlık
        Vector2 Offset(float tt, out float alpha)
        {
            alpha = 1f;
            var s0 = new Vector2(startOffset.x * outward, startOffset.y);
            if (tt < tAppear) { alpha = 0f; return s0; }
            if (tt < tLand)
            {
                float u = (tt - tAppear) / (tLand - tAppear);
                float e = u * u * (3f - 2f * u);
                Vector2 dir = s0.normalized, perp = new Vector2(-dir.y, dir.x);
                float w = Mathf.Pow(1f - e, 1.2f), a = tt - tAppear;
                alpha = Mathf.Clamp01(a / 0.45f);
                return s0 * (1f - e) + perp * (300f * w * Mathf.Sin(a * 10f)) + dir * (110f * w * Mathf.Sin(a * 7.3f + 1f));
            }
            if (tt < tLand + 0.35f)
            {
                float v = (tt - tLand) / 0.35f;
                return new Vector2(0f, 26f * Mathf.Sin(Mathf.PI * v));      // küçük konma sekmesi
            }
            if (tt < tLeave) return new Vector2(1.5f * Mathf.Sin(tt * 1.7f), 1f * Mathf.Sin(tt * 2.3f));
            float uu = Mathf.Clamp01((tt - tLeave) / (tEnd - tLeave));
            var d = new Vector2(leaveOffset.x * outward, leaveOffset.y);
            Vector2 dn = d.normalized, pp = new Vector2(-dn.y, dn.x);
            alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((uu - 0.45f) / 0.55f));
            return d * (uu * uu) + pp * (160f * uu * Mathf.Sin((tt - tLeave) * 6f));
        }

        // Parlaklık: uçarken kısa kısa yanıp söner, konunca yavaş nefes alır gibi parlar
        float Glow(float tt)
        {
            if (tt >= tLand + 0.2f && tt < tLeave)
            {
                float ph = Mathf.Repeat((tt - tLand) / 2.1f, 1f);
                return 0.35f + 0.65f * Mathf.Pow(Mathf.Sin(Mathf.PI * ph), 2f);
            }
            float p = Mathf.Repeat(tt / 0.95f, 1f);
            return p < 0.6f ? 0.45f + 0.55f * Mathf.Pow(Mathf.Sin(Mathf.PI * p / 0.6f), 2f) : 0.3f;
        }

        void LateUpdate()
        {
            if (!active) return;
            if (perch == null || rig == null) { Finish(); return; }
            t += Time.deltaTime;
            if (t >= tEnd) { Finish(); return; }

            float alpha;
            Vector2 off = Offset(t, out alpha);
            if (cancelling)
            {
                cancelT += Time.deltaTime;
                alpha *= 1f - Mathf.Clamp01(cancelT / 0.35f);
                if (cancelT >= 0.35f) { Finish(); return; }
            }
            if (alpha <= 0.001f) { SetVisible(false); return; }
            SetVisible(true);

            float upw = rig.unitsPerPixel * Mathf.Abs(rig.transform.lossyScale.y);
            Vector3 p = perch.position; p.x += off.x * upw; p.y += off.y * upw;
            transform.position = p;

            float g = Glow(t);
            float breathe = 0.85f + 0.3f * g;
            core.transform.localScale = Vector3.one * (coreSize * breathe * upw);
            halo.transform.localScale = Vector3.one * (haloSize * breathe * upw);
            outer.transform.localScale = Vector3.one * (outerSize * (0.8f + 0.4f * g) * upw);

            Color cc = coreColor; cc.a = alpha * Mathf.Lerp(0.55f, 1f, g);
            Color hc = haloColor; hc.a = alpha * 0.55f * g;
            Color oc = haloColor; oc.a = alpha * 0.18f * g;
            core.color = cc; halo.color = hc; outer.color = oc;

            string layer = rig.sortingLayerName; int order = rig.sortingOrderOffset + 260;
            outer.sortingLayerName = layer; halo.sortingLayerName = layer; core.sortingLayerName = layer;
            outer.sortingOrder = order; halo.sortingOrder = order + 1; core.sortingOrder = order + 2;
        }
    }
}
