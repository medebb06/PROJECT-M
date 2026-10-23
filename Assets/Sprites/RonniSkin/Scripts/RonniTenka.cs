using System.Collections.Generic;
using UnityEngine;

namespace RonniSkin
{
    /// Tenka chiburi + sakate nōtō efektleri. RonniIdleKatana rig'indeki "tenka" klibiyle birlikte çalışır;
    /// RonniPlayerBridge her karede Drive(rig) çağırır, bitince Stop().
    /// Klip şu kemiklerden okunur:
    ///   fxFlags  scale.x = el + kabza gövdenin önünde, scale.y = kılıç kında (kına girmiş kabza görünür)
    ///   fxMouth  scale.x = bıçak kının ağzında kırpılsın (kınının içi görünmez)
    ///   fxGlint  scale.x = parlama gücü (bıçak düzlemi kameraya döndüğünde)
    ///   fxTip    scale.x = kan damlası tetiği (silkeleme anı), scale.y = dönüş izi gücü
    [DefaultExecutionOrder(-100)]   // CharacterRim'den önce: gizlenen/gösterilen parçaların arka ışık kopyaları aynı karede eşitlensin
    public class RonniTenka : MonoBehaviour
    {
        [Header("Kılıç ölçüleri (kaynak piksel, kılıç ekseni üzerinde)")]
        public float bladeTLeft = -1266f, bladeTRight = 252f, gripT = 483f, bladeLength = 1745f;
        [Header("Parlama")]
        public bool glint = true;
        [Tooltip("Parlamanın boyu (kaynak piksel).")] public float glintPx = 420f;
        [Header("Kına oturma parlaması")]
        [Tooltip("Kılıç kına oturduğu anda (tık) kının ağzında kısa bir parlama.")] public bool sheathGlint = true;
        [Tooltip("Yıldız boyu (kaynak piksel).")] public float sheathGlintPx = 1100f;
        [Tooltip("Arkadaki yumuşak hale boyu (kaynak piksel).")] public float sheathHaloPx = 950f;
        [Tooltip("Süresi (sn).")] public float sheathGlintTime = 0.7f;
        [Header("Ses (Resources/TenkaSfx)")]
        [Tooltip("Savurma (chiburi) sesi: dönüş izi başlayınca çalar; kına oturunca kına sesi. Kapalı = sessiz.")] public bool sfx = true;
        [Range(0f, 1f)] public float swingVolume = 0.9f, sheathVolume = 1f;
        [Tooltip("Kına oturma sesinin başı bu kadar atlanır (sn): tık, kılıç kına oturduğu karede duyulsun.")] public float sheathSkip = 0.11f;

        public Color sheathGlintColor = new Color(1f, 0.96f, 0.82f, 1f);
        [Header("Dönüş izi")]
        public bool smear = true;
        [Range(0f, 1f)] public float smearAlpha = 0.5f;
        public Color smearColor = new Color(0.86f, 0.93f, 1f, 1f);
        [Header("Kan (gerçek kan rengi) — silkeleme anında")]
        public bool blood = true;
        public int dropCount = 22;
        [Tooltip("Artik SliceBloodFx.BloodColor kullanilir (koyu kirmizi, ton rastgele).")]
        public Color dropColor = new Color(0.8f, 0.03f, 0.04f, 1f);
        [Tooltip("Rig birimi / sn (1 birim = 1000 kaynak piksel).")] public float dropSpeed = 3.4f;
        public float dropGravity = 16f, dropLife = 1.4f;
        [Tooltip("Damla boyu (kaynak piksel).")] public Vector2 dropPx = new Vector2(60f, 130f);
        [Header("Yerdeki kan lekeleri")]
        public bool groundSplats = true;
        [Tooltip("Leke genişliği (kaynak piksel).")] public Vector2 splatPx = new Vector2(220f, 560f);
        [Tooltip("Lekenin yerde kalma süresi (sn); son 2.5 sn'de solar.")] public float splatLife = 6f;

        RonniSkinRig rig;
        Transform kblade, fxGlint, fxTip, fxMouth, fxFlags;
        MeshRenderer bladeMR;
        SpriteRenderer glintSR, smearSR;
        Sprite glintSp, smearSp, dropSp;
        static Sprite circleHard, circleSoft;
        Material fxMat;
        bool active, wasDrop, cutOn, wasSheathed;
        bool wasSwing, wasSheathSfx; float lastSwingT = -99f;
        AudioSource swingSrc, sheathSrc; AudioClip swingClip, sheathClip;

        SpriteRenderer sheathSR, haloSR; float sheathAge = -1f; Vector3 sheathPos;
        class Drop { public Transform t; public SpriteRenderer sr; public Vector3 v; public float age, life, g, groundY; public Color c; public int order; public string layer; }
        readonly List<Drop> drops = new List<Drop>();
        class Splat { public SpriteRenderer sr; public float age, w; }
        readonly List<Splat> splats = new List<Splat>();

        public bool Active { get { return active; } }

        /// Klip oynarken her karede çağır.
        public void Drive(RonniSkinRig r)
        {
            if (r == null || !r.IsBuilt) return;
            if (rig != r) Bind(r);
            active = true;
        }

        /// Animasyon bitti / kesildi: rig'i normal kılıç çekik hâline döndür.
        public void Stop()
        {
            if (!active) return;
            active = false; wasDrop = false; wasSheathed = false;
            wasSwing = false; wasSheathSfx = false; if (swingSrc != null && swingSrc.isPlaying) swingSrc.Stop();

            if (rig == null) return;
            if (cutOn) { rig.SetPartWindow("idle_kilic", 0f, 1f, Color.white, Color.white); cutOn = false; }
            rig.SetActLayers(false);
            rig.SetVisible("idle_kilic", true); rig.SetVisible("idle_kilic_tsuka", true); rig.SetVisible("idle_tsuka", false);
            if (glintSR != null) glintSR.enabled = false;
            if (smearSR != null) smearSR.enabled = false;
        }

        void Bind(RonniSkinRig r)
        {
            rig = r;
            r.bones.TryGetValue("kblade", out kblade); r.bones.TryGetValue("fxGlint", out fxGlint);
            r.bones.TryGetValue("fxTip", out fxTip); r.bones.TryGetValue("fxMouth", out fxMouth); r.bones.TryGetValue("fxFlags", out fxFlags);
            bladeMR = null;
            foreach (var p in r.parts) if (p != null && p.name == "idle_kilic") bladeMR = p.GetComponent<MeshRenderer>();
            if (fxMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null) fxMat = new Material(sh) { name = "Ronni_TenkaFx" };
            }
            glintSp = glintSp ?? MakeSprite("RonniTenka/tenka_glint");
            smearSp = smearSp ?? MakeSprite("RonniTenka/tenka_smear");
            dropSp = dropSp ?? MakeSprite("RonniTenka/tenka_drop");
            if (circleHard == null) circleHard = MakeCircle(64, 0.82f);
            if (circleSoft == null) circleSoft = MakeCircle(128, 0f);
            if (glintSR != null) Destroy(glintSR.gameObject);
            if (smearSR != null) Destroy(smearSR.gameObject);
            glintSR = MakeSR("Tenka_Glint", glintSp);
            smearSR = MakeSR("Tenka_Smear", smearSp);
        }

        static Sprite MakeSprite(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);   // 1 birim genişlik
        }

        // SliceBloodFx ile aynı daire (1 birim çap)
        static Sprite MakeCircle(int size, float hardness)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01((1f - d) / Mathf.Max(0.02f, 1f - hardness));
                    a = a * a * (3f - 2f * a);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        SpriteRenderer MakeSR(string name, Sprite sp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp; if (fxMat != null) sr.sharedMaterial = fxMat; sr.enabled = false;
            return sr;
        }

        int TopOrder()
        {
            int o = rig.sortingOrderOffset + 100;
            foreach (var p in rig.parts) if (p != null) { var mr = p.GetComponent<MeshRenderer>(); if (mr != null && mr.sortingOrder > o) o = mr.sortingOrder; }
            return o;
        }

        void LateUpdate()
        {
            UpdateDrops();
            UpdateSplats();
            UpdateSheathGlint();
            if (!active || rig == null || kblade == null || fxFlags == null) return;
            float upp = rig.unitsPerPixel;
            bool front = fxFlags.localScale.x > 0.5f, sheathed = fxFlags.localScale.y > 0.5f;
            if (sheathed && !wasSheathed && sheathGlint && rig.Shown && fxMouth != null) StartSheathGlint();
            wasSheathed = sheathed;
            rig.SetActLayers(front);
            if (sheathed && !wasSheathSfx && sfx && rig.Shown) PlaySheath();
            wasSheathSfx = sheathed;
            bool swingNow = fxTip != null && fxTip.localScale.y > 0.5f && !sheathed;
            if (swingNow && !wasSwing && sfx && rig.Shown && Time.time - lastSwingT > 1.5f) PlaySwing();
            if (!swingNow && swingSrc != null && swingSrc.isPlaying) { swingSrc.volume = Mathf.MoveTowards(swingSrc.volume, 0f, Time.deltaTime * 9f); if (swingSrc.volume <= 0.001f) swingSrc.Stop(); }   // sallama bitti: ses de biter
            wasSwing = swingNow;


            // bıçak kınının ağzında kırpılır (ağızdan içerisi görünmez)
            bool cut = !sheathed && fxMouth != null && fxMouth.localScale.x > 0.5f;
            if (cut)
            {
                Vector3 l = kblade.InverseTransformPoint(fxMouth.position);
                float tM = gripT + l.x / upp;
                float u = Mathf.Clamp01((tM - bladeTLeft) / Mathf.Max(1f, bladeTRight - bladeTLeft));
                rig.SetPartWindow("idle_kilic", u, 1f, Color.white, Color.white); cutOn = true;
            }
            else if (cutOn) { rig.SetPartWindow("idle_kilic", 0f, 1f, Color.white, Color.white); cutOn = false; }
            if (!cut) rig.SetVisible("idle_kilic", !sheathed);   // kırpma açıkken görünürlüğü SetPartWindow yönetir (tamamen kındaysa gizler)
            rig.SetVisible("idle_kilic_tsuka", !sheathed);
            rig.SetVisible("idle_tsuka", sheathed);

            int baseOrder = bladeMR != null ? bladeMR.sortingOrder : rig.sortingOrderOffset + 40;
            string layer = bladeMR != null ? bladeMR.sortingLayerName : rig.sortingLayerName;
            Transform root = rig.transform;

            // parlama: bıçakla aynı katmanda (gövdenin arkasındayken görünmez)
            float gv = fxGlint != null ? fxGlint.localScale.x : 0f;
            if (glintSR != null)
            {
                bool on = glint && gv > 0.02f && !sheathed && rig.Shown;
                glintSR.enabled = on;
                if (on)
                {
                    glintSR.transform.localPosition = root.InverseTransformPoint(fxGlint.position);
                    glintSR.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 160f);
                    glintSR.transform.localScale = Vector3.one * glintPx * upp * (0.55f + 0.45f * Mathf.Clamp01(gv));
                    glintSR.color = new Color(1f, 1f, 1f, Mathf.Clamp01(gv));
                    glintSR.sortingLayerName = layer; glintSR.sortingOrder = baseOrder + 1;
                }
            }
            // dönüş izi: kabza merkezli halka; parlak kenar bıçağın ucunu izler
            float sv = fxTip != null ? fxTip.localScale.y : 0f;
            if (smearSR != null)
            {
                bool on = smear && sv > 0.01f && !sheathed && rig.Shown;
                smearSR.enabled = on;
                if (on)
                {
                    Vector3 tipDir = root.InverseTransformVector(-kblade.right);
                    float a = Mathf.Atan2(tipDir.y, tipDir.x) * Mathf.Rad2Deg;
                    smearSR.transform.localPosition = root.InverseTransformPoint(kblade.position);
                    smearSR.transform.localRotation = Quaternion.Euler(0f, 0f, a);
                    smearSR.transform.localScale = Vector3.one * (2f * bladeLength * upp * Mathf.Clamp(Mathf.Abs(kblade.localScale.x), 0.3f, 1f));   // perspektifle kısalan bıçağın ucunu izler
                    var c = smearColor; c.a = Mathf.Clamp01(sv) * smearAlpha; smearSR.color = c;
                    smearSR.sortingLayerName = layer; smearSR.sortingOrder = baseOrder - 1;
                }
            }
            // kan: silkeleme anında bir kez
            bool dropNow = fxTip != null && fxTip.localScale.x > 0.5f;
            if (dropNow && !wasDrop && blood && rig.Shown) SpawnDrops(root, upp, layer);
            wasDrop = dropNow;
        }

        // kılıç kına oturdu: kının ağzında kısa parlama (tenka bitip rig değişse de sönene kadar kalır)
        AudioSource MakeSrc() { var s = gameObject.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; return s; }

        // savurma: dönüş izi başlayınca (klipte ~0.6 sn) çalar; 1 sn'lik klibin son savurması silkeleme anına denk gelir
        void PlaySwing()
        {
            if (swingClip == null) swingClip = Resources.Load<AudioClip>("TenkaSfx/tenka_swing");
            if (swingClip == null) return;
            if (swingSrc == null) swingSrc = MakeSrc();
            swingSrc.clip = swingClip; swingSrc.volume = swingVolume; swingSrc.time = 0f; swingSrc.Play();
            lastSwingT = Time.time;
        }

        // kılıç kına tam oturdu anı (sheathed bayrağı)
        void PlaySheath()
        {
            if (sheathClip == null) sheathClip = Resources.Load<AudioClip>("TenkaSfx/tenka_sheath");
            if (sheathClip == null) return;
            if (sheathSrc == null) sheathSrc = MakeSrc();
            sheathSrc.clip = sheathClip; sheathSrc.volume = sheathVolume;
            sheathSrc.time = Mathf.Clamp(sheathSkip, 0f, sheathClip.length * 0.5f); sheathSrc.Play();
        }

        void StartSheathGlint()
        {
            if (glintSp == null) return;
            if (sheathSR == null) sheathSR = MakeSR("Tenka_SheathGlint", glintSp);
            if (haloSR == null) haloSR = MakeSR("Tenka_SheathHalo", circleSoft);
            sheathSR.transform.SetParent(rig.transform, false);
            haloSR.transform.SetParent(rig.transform, false);
            sheathPos = rig.transform.InverseTransformPoint(fxMouth.position);
            sheathPos.z = -0.05f;
            sheathAge = 0f;
            int ord = TopOrder() + 20;                          // elin / kolun da üstünde
            string layer = bladeMR != null ? bladeMR.sortingLayerName : rig.sortingLayerName;
            haloSR.sortingLayerName = layer; haloSR.sortingOrder = ord;
            sheathSR.sortingLayerName = layer; sheathSR.sortingOrder = ord + 1;
            sheathSR.enabled = true; haloSR.enabled = true;
            UpdateSheathGlint();
        }

        void UpdateSheathGlint()
        {
            if (sheathSR == null || sheathAge < 0f) return;
            sheathAge += Time.deltaTime;
            float d = Mathf.Max(0.05f, sheathGlintTime), u = sheathAge / d;
            if (u >= 1f || rig == null) { sheathSR.enabled = false; if (haloSR != null) haloSR.enabled = false; sheathAge = -1f; return; }
            float grow = Mathf.Clamp01(u / 0.12f); grow = grow * grow * (3f - 2f * grow);
            float tail = Mathf.Clamp01((u - 0.3f) / 0.7f);
            float fade = 1f - tail * tail;
            float s = grow * (1.15f - 0.55f * tail);
            float upp = rig.unitsPerPixel;
            sheathSR.transform.localPosition = sheathPos;
            sheathSR.transform.localRotation = Quaternion.Euler(0f, 0f, 15f + 50f * u);
            sheathSR.transform.localScale = Vector3.one * sheathGlintPx * upp * Mathf.Max(0.001f, s);
            var c = sheathGlintColor; c.a = fade; sheathSR.color = c;
            if (haloSR != null)
            {
                haloSR.transform.localPosition = sheathPos;
                haloSR.transform.localScale = Vector3.one * sheathHaloPx * upp * Mathf.Max(0.001f, grow * (0.8f + 0.4f * u));
                var h = sheathGlintColor; h.a = 0.55f * fade; haloSR.color = h;
            }
        }

        float GroundY()
        {
            // oyuncunun ayak hizası: collider'ın altı (tenka sadece yerdeyken oynar)
            var col = GetComponentInParent<Collider2D>();
            if (col == null) col = GetComponentInChildren<Collider2D>();
            if (col != null) return col.bounds.min.y;
            return rig != null ? rig.transform.position.y : transform.position.y;
        }

        void SpawnDrops(Transform root, float upp, string layer)
        {
            if (circleHard == null) return;
            Vector3 tipDir = root.InverseTransformVector(-kblade.right).normalized;
            Vector3 swing = new Vector3(-tipDir.y, tipDir.x, 0f);      // saat yönü tersine savrulma yönü (aşağı)
            Vector3 fwd = new Vector3(-1f, -0.25f, 0f);                 // karakter sola bakar (rig yerel uzayı)
            float tipX = (bladeTLeft - gripT) * upp;
            float gY = GroundY();
            float gWorld = dropGravity * Mathf.Abs(root.lossyScale.y);
            int order = TopOrder() + 10;
            for (int i = 0; i < dropCount; i++)
            {
                float f = Mathf.Lerp(0.45f, 1f, (i + Random.value) / dropCount);
                Vector3 w = kblade.TransformPoint(new Vector3(tipX * f, 0f, 0f));
                var go = new GameObject("Tenka_Drop");               // dünya uzayında: karakter yürüse de kan yerinde kalır
                go.transform.position = new Vector3(w.x, w.y, root.position.z);
                float s = Random.Range(dropPx.x, dropPx.y) * upp;
                Vector3 ls = new Vector3(s, s * Random.Range(1.0f, 1.5f), 1f);
                go.transform.localScale = Vector3.Scale(ls, new Vector3(Mathf.Abs(root.lossyScale.x), Mathf.Abs(root.lossyScale.y), 1f));
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = circleHard; if (fxMat != null) sr.sharedMaterial = fxMat;   // beyaz daire: renk tam uygulanır
                float shade = Random.Range(0.45f, 0.9f);   // SliceBloodFx damla tonları
                Color c = SliceBloodFx.BloodColor(shade, 1f);
                sr.color = c;
                sr.sortingLayerName = layer; sr.sortingOrder = order;
                Vector3 vLocal = (swing * (0.6f + 0.6f * f) + fwd * 0.55f + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.2f, 0.3f), 0f)) * dropSpeed * Random.Range(0.6f, 1.1f);
                Vector3 v = root.TransformVector(vLocal); v.z = 0f;
                drops.Add(new Drop { t = go.transform, sr = sr, v = v, age = 0f, life = dropLife * Random.Range(0.8f, 1.2f), g = gWorld, groundY = gY, c = c, order = rig.sortingOrderOffset - 2, layer = layer });
            }
        }

        void UpdateDrops()
        {
            float dt = Time.deltaTime;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                if (d.t == null) { drops.RemoveAt(i); continue; }
                d.age += dt;
                d.v.y -= d.g * dt;
                Vector3 p = d.t.position + d.v * dt;
                if (p.y <= d.groundY)
                {
                    if (groundSplats) SpawnSplat(new Vector3(p.x, d.groundY + 0.06f, p.z), d);
                    Destroy(d.t.gameObject); drops.RemoveAt(i); continue;
                }
                if (d.age >= d.life) { Destroy(d.t.gameObject); drops.RemoveAt(i); continue; }
                d.t.position = p;
                // damla hız yönüne uzar
                float ang = Mathf.Atan2(d.v.y, d.v.x) * Mathf.Rad2Deg + 90f;
                d.t.rotation = Quaternion.Euler(0f, 0f, ang);
                float a = 1f - Mathf.Clamp01((d.age - d.life * 0.75f) / (d.life * 0.25f));
                var c = d.c; c.a = a; d.sr.color = c;
            }
        }

        void SpawnSplat(Vector3 pos, Drop d)
        {
            if (circleHard == null) return;
            float upp = rig != null ? rig.unitsPerPixel : 0.001f;
            float sc = rig != null ? Mathf.Abs(rig.transform.lossyScale.x) : 1f;
            var go = new GameObject("Tenka_BloodSplat");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.001f, 0.001f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleHard; if (fxMat != null) sr.sharedMaterial = fxMat;
            float sh = Random.Range(0.35f, 0.7f);   // SliceBloodFx leke tonları
            var c = SliceBloodFx.BloodColor(sh * 0.85f, 0.9f); sr.color = c;
            sr.sortingLayerName = d.layer; sr.sortingOrder = d.order;
            splats.Add(new Splat { sr = sr, age = 0f, w = Random.Range(splatPx.x, splatPx.y) * upp * sc });
        }

        void UpdateSplats()
        {
            float dt = Time.deltaTime;
            for (int i = splats.Count - 1; i >= 0; i--)
            {
                var s = splats[i];
                if (s.sr == null) { splats.RemoveAt(i); continue; }
                s.age += dt;
                if (s.age >= splatLife) { Destroy(s.sr.gameObject); splats.RemoveAt(i); continue; }
                float g = Mathf.Clamp01(s.age / 0.12f); g = 1f - (1f - g) * (1f - g);
                s.sr.transform.localScale = new Vector3(s.w * g, s.w * 0.22f * g, 1f);
                var c = s.sr.color; c.a = 0.9f * Mathf.Clamp01((splatLife - s.age) / 2.5f); s.sr.color = c;
            }
        }

        void OnDisable() { Stop(); }
    }
}
