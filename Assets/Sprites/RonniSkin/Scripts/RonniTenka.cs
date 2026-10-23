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
        [Header("Dönüş izi")]
        public bool smear = true;
        [Range(0f, 1f)] public float smearAlpha = 0.5f;
        public Color smearColor = new Color(0.86f, 0.93f, 1f, 1f);
        [Header("Kan damlaları")]
        public bool blood = true;
        public int dropCount = 10;
        public Color dropColor = new Color(0.42f, 0.03f, 0.05f, 1f);
        [Tooltip("Rig birimi / sn (1 birim = 1000 kaynak piksel).")] public float dropSpeed = 6.5f;
        public float dropGravity = 16f, dropLife = 0.6f;
        [Tooltip("Damla boyu (kaynak piksel).")] public Vector2 dropPx = new Vector2(30f, 70f);

        RonniSkinRig rig;
        Transform kblade, fxGlint, fxTip, fxMouth, fxFlags;
        MeshRenderer bladeMR;
        SpriteRenderer glintSR, smearSR;
        Sprite glintSp, smearSp, dropSp;
        Material fxMat;
        bool active, wasDrop, cutOn;
        class Drop { public Transform t; public SpriteRenderer sr; public Vector3 v; public float age, life; }
        readonly List<Drop> drops = new List<Drop>();

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
            active = false; wasDrop = false;
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

        SpriteRenderer MakeSR(string name, Sprite sp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp; if (fxMat != null) sr.sharedMaterial = fxMat; sr.enabled = false;
            return sr;
        }

        void LateUpdate()
        {
            UpdateDrops();
            if (!active || rig == null || kblade == null || fxFlags == null) return;
            float upp = rig.unitsPerPixel;
            bool front = fxFlags.localScale.x > 0.5f, sheathed = fxFlags.localScale.y > 0.5f;
            rig.SetActLayers(front);

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
            // kan damlaları: silkeleme anında bir kez
            bool dropNow = fxTip != null && fxTip.localScale.x > 0.5f;
            if (dropNow && !wasDrop && blood && rig.Shown) SpawnDrops(root, upp, layer);
            wasDrop = dropNow;
        }

        void SpawnDrops(Transform root, float upp, string layer)
        {
            if (dropSp == null) return;
            Vector3 tipDir = root.InverseTransformVector(-kblade.right).normalized;
            Vector3 swing = new Vector3(-tipDir.y, tipDir.x, 0f);      // saat yönü tersine savrulma yönü (aşağı)
            Vector3 fwd = new Vector3(-1f, -0.25f, 0f);                 // karakter sola bakar (rig yerel uzayı)
            float tipX = (bladeTLeft - gripT) * upp;
            for (int i = 0; i < dropCount; i++)
            {
                float f = Mathf.Lerp(0.45f, 1f, (i + Random.value) / dropCount);
                Vector3 w = kblade.TransformPoint(new Vector3(tipX * f, 0f, 0f));
                var go = new GameObject("Tenka_Drop");
                go.transform.SetParent(root, false);
                go.transform.localPosition = root.InverseTransformPoint(w);
                float s = Random.Range(dropPx.x, dropPx.y) * upp;
                go.transform.localScale = new Vector3(s, s * Random.Range(1.0f, 1.5f), 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = dropSp; if (fxMat != null) sr.sharedMaterial = fxMat; sr.color = dropColor;
                sr.sortingLayerName = layer; sr.sortingOrder = rig.sortingOrderOffset + 400;
                Vector3 v = (swing * (0.6f + 0.6f * f) + fwd * 0.55f + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.2f, 0.3f), 0f)) * dropSpeed * Random.Range(0.6f, 1.1f);
                drops.Add(new Drop { t = go.transform, sr = sr, v = v, age = 0f, life = dropLife * Random.Range(0.7f, 1.2f) });
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
                if (d.age >= d.life) { Destroy(d.t.gameObject); drops.RemoveAt(i); continue; }
                d.v.y -= dropGravity * dt;
                d.t.localPosition += d.v * dt;
                float a = 1f - Mathf.Clamp01((d.age - d.life * 0.6f) / (d.life * 0.4f));
                var c = dropColor; c.a = a; d.sr.color = c;
            }
        }

        void OnDisable() { Stop(); }
    }
}
