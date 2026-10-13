using System.Collections.Generic;
using UnityEngine;

namespace RonniSkin
{
    /// Dash sırasında karakterin her bölgesinden (bacak, kol, kılıç, pelerin, saç...) o bölgenin rengiyle
    /// pixel-art tarzı basamaklı solan bir iz bırakır. Her iz bir TrailRenderer; bir kemiğin ucuna takılı.
    /// RonniPlayerBridge dash'te Init + SetEmitting çağırır.
    [DefaultExecutionOrder(300)]
    public class RonniDashTrail : MonoBehaviour
    {
        [System.Serializable]
        public class Streak { public string bone; public string part; public float width = 0.09f; public float lift = 0f; }

        [Tooltip("Hangi kemikten hangi parçanın renginde iz çıkacak.")]
        public List<Streak> streaks = new List<Streak>
        {
            // Tek parça iz: gövde hizasında, karakterin gövde kalınlığında tek bir şerit.
            new Streak { bone = "chest", part = "torso", width = 0.5f },
        };

        [Tooltip("Beyaz düz çizgi (true) ya da parça renkleri (false).")] public bool whiteStraight = true;
        public Color lineColor = Color.white;
        [Tooltip("İzin ömrü (sn). Basamaklı solar.")] public float trailTime = 0.30f;
        [Tooltip("Parça renginin parlaklığı/doygunluğu (koyu parçalar da görünsün).")] public float minValue = 0.45f;
        [Range(0f, 1.5f)] public float saturationBoost = 1.15f;
        [Tooltip("Pixel-art tarzı: renk/alpha kaç basamakta solsun.")] public int steps = 5;
        [Range(0f, 1f)] public float startAlpha = 0.95f;
        public float minVertexDistance = 0.03f;

        RonniSkinRig rig;
        readonly List<TrailRenderer> trails = new List<TrailRenderer>();
        readonly List<Transform> bonesList = new List<Transform>();
        readonly List<float> lineY = new List<float>();
        Material mat;
        bool emitting;

        public void Init(RonniSkinRig r)
        {
            if (rig == r && trails.Count > 0) return;
            Clear();
            rig = r;
            if (rig == null || !rig.IsBuilt) return;

            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            mat = new Material(sh) { name = "Ronni_DashTrail" };

            // sıralama: karakterin en arkasının bir altı
            int minOrder = int.MaxValue; string layer = rig.sortingLayerName;
            foreach (var mr in rig.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.sortingOrder < minOrder) { minOrder = mr.sortingOrder; layer = mr.sortingLayerName; }
            if (minOrder == int.MaxValue) minOrder = 0;

            float worldH = rig.HeightPx * rig.unitsPerPixel * Mathf.Abs(rig.transform.lossyScale.y);
            float wScale = worldH / 0.9f; // width değerleri ~0.9 birim boyundaki karaktere göre

            foreach (var s in streaks)
            {
                if (!rig.bones.TryGetValue(s.bone, out Transform bone)) continue;
                Color c = whiteStraight ? lineColor : PartColor(s.part);
                var go = new GameObject("~dashTrail_" + s.bone);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(transform, false); // iz kemiğe takılı değil: Y sabit, sadece X takip eder (düz çizgi)
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = mat;
                tr.time = trailTime;
                tr.minVertexDistance = minVertexDistance;
                tr.numCapVertices = 0; tr.numCornerVertices = 0;
                tr.alignment = LineAlignment.View;
                tr.textureMode = LineTextureMode.Stretch;
                tr.sortingLayerName = layer;
                tr.sortingOrder = minOrder - 3;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                tr.emitting = false;
                tr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f); // sabit genişlik: dikdörtgen
                tr.widthMultiplier = s.width * wScale;
                tr.colorGradient = SteppedGradient(c);
                trails.Add(tr); bonesList.Add(bone); lineY.Add(bone.position.y);
            }
        }

        // Tek renk, tam opak, basamaksız: düz beyaz dikdörtgen şerit.
        Gradient SteppedGradient(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        // Parçanın dokusundan alfa ağırlıklı ortalama renk (doku okunabilir olmasa da RenderTexture ile).
        Color PartColor(string part)
        {
            Texture2D tex = Resources.Load<Texture2D>(rig.resourceFolder + "/" + part);
            Color c = new Color(0.8f, 0.8f, 0.8f, 1f);
            if (tex != null)
            {
                var rt = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var prev = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var read = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                read.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Color[] px = read.GetPixels();
                Destroy(read);
                float r = 0, g = 0, b = 0, w = 0;
                foreach (var p in px) { r += p.r * p.a; g += p.g * p.a; b += p.b * p.a; w += p.a; }
                if (w > 0.01f) c = new Color(r / w, g / w, b / w, 1f);
            }
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            sat = Mathf.Clamp01(sat * saturationBoost);
            v = Mathf.Max(v, minValue);
            Color o = Color.HSVToRGB(h, sat, v); o.a = 1f; return o;
        }

        public void SetEmitting(bool on)
        {
            if (on == emitting) return;
            emitting = on;
            for (int i = 0; i < trails.Count; i++)
            {
                var t = trails[i]; if (t == null) continue;
                if (on)
                {
                    lineY[i] = bonesList[i].position.y; // dash başındaki yükseklik sabit kalır
                    t.transform.position = new Vector3(bonesList[i].position.x, lineY[i], bonesList[i].position.z);
                    t.Clear();
                }
                t.emitting = on;
            }
        }

        void LateUpdate()
        {
            if (!emitting) return;
            for (int i = 0; i < trails.Count; i++)
                if (trails[i] != null && bonesList[i] != null)
                    trails[i].transform.position = new Vector3(bonesList[i].position.x, lineY[i], bonesList[i].position.z);
        }

        void Clear()
        {
            foreach (var t in trails) if (t != null) Destroy(t.gameObject);
            trails.Clear();
            if (mat != null) { Destroy(mat); mat = null; }
        }

        void OnDestroy() { Clear(); }
    }
}
