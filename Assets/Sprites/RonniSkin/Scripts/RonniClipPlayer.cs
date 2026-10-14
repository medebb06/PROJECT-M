using System.Collections.Generic;
using UnityEngine;

namespace RonniSkin
{
    /// Plays baked looping clips (Resources/<folder>/ronni_anims.json) on a RonniSkinRig.
    /// Used for the side-profile (B) view of Ronni.
    [RequireComponent(typeof(RonniSkinRig))]
    public class RonniClipPlayer : MonoBehaviour
    {
        public string clip = "run";
        public float speed = 1f;
        [Tooltip("Cross-fade time when the clip changes.")] public float fadeTime = 0.12f;
        [Tooltip("If >= 0, the clip is sampled at this normalized position instead of playing by time.")] public float manual01 = -1f;

        [System.Serializable] class BoneTrack { public string name; public float[] r; public float[] x; public float[] y; public float[] sx; public float[] sy; }
        [System.Serializable] class Clip { public string name; public float duration; public int frames; public bool loop; public BoneTrack[] bones; }
        [System.Serializable] class ClipFile { public Clip[] clips; }

        [Header("Blink (bones eyeA / eyeB)")]
        public bool autoBlink = true;
        public Vector2 blinkInterval = new Vector2(2f, 5f);
        [Range(0f, 1f)] public float doubleBlinkChance = 0.2f;
        Transform eyeA, eyeB; float blinkTimer = -1f, blinkPhase = -1f; bool blinkAgain;

        [Header("Bakış kaçırma (gözler)")]
        [Tooltip("Idle'da arada bir gözleri ters tarafa çekip geri getirir (klipte göz izi yoksa).")] public bool autoGlance = true;
        public Vector2 glanceInterval = new Vector2(3.5f, 8f);
        [Tooltip("Gözlerin kayma miktarı (kaynak piksel). Çizimin bakış yönünün tersine, sağa doğru.")] public float glanceX = 55f;
        public Vector2 glanceHold = new Vector2(0.6f, 1.4f);
        float glanceTimer = -1f, glanceT = -1f, glanceHoldT, glanceAmp = 1f, glanceDy;

        RonniSkinRig rig;
        readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        readonly Dictionary<Transform, Vector3> basePos = new Dictionary<Transform, Vector3>();
        float t; Clip lastClip;
        readonly Dictionary<Transform, float> snapR = new Dictionary<Transform, float>();
        readonly Dictionary<Transform, Vector3> snapP = new Dictionary<Transform, Vector3>();
        float fadeT = 1f;
        readonly HashSet<string> used = new HashSet<string>();

        void Start()
        {
            rig = GetComponent<RonniSkinRig>(); rig.Build();
            var ta = Resources.Load<TextAsset>(rig.resourceFolder + "/ronni_anims");
            if (ta != null)
            {
                var f = JsonUtility.FromJson<ClipFile>(ta.text);
                if (f?.clips != null) foreach (var c in f.clips) clips[c.name] = c;
            }
            foreach (var b in rig.bones.Values) basePos[b] = b.localPosition;
            rig.bones.TryGetValue("eyeA", out eyeA); rig.bones.TryGetValue("eyeB", out eyeB);
            blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
        }

        // Göz kırpma: gözler kendi kemiklerinde dikeyde kapanıp açılır (zaman rastgele, bazen çift kırpma).
        void UpdateBlink()
        {
            if (!autoBlink || (eyeA == null && eyeB == null)) return;
            float dt = Time.deltaTime, eyeY = 1f;
            if (blinkPhase < 0f)
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0f) { blinkPhase = 0f; blinkAgain = Random.value < doubleBlinkChance; }
            }
            else
            {
                blinkPhase += dt / 0.16f;
                eyeY = blinkPhase < 0.4f ? Mathf.Lerp(1f, 0.08f, blinkPhase / 0.4f)
                     : blinkPhase < 0.6f ? 0.08f
                     : Mathf.Lerp(0.08f, 1f, (blinkPhase - 0.6f) / 0.4f);
                if (blinkPhase >= 1f)
                {
                    if (blinkAgain) { blinkAgain = false; blinkPhase = 0f; }
                    else { blinkPhase = -1f; blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y); }
                }
            }
            if (eyeA != null) eyeA.localScale = new Vector3(1f, eyeY, 1f);
            if (eyeB != null) eyeB.localScale = new Vector3(1f, eyeY, 1f);
        }

        // Bakış kaçırma: gözler hızla yana kayar, kısa süre bakar, geri döner. Klipte göz izi varsa (ör. "wait") karışmaz.
        void UpdateGlance()
        {
            if (!autoGlance || eyeA == null || eyeB == null || used.Contains("eyeA")) { glanceT = -1f; return; }
            float dt = Time.deltaTime;
            if (glanceT < 0f)
            {
                if (glanceTimer < 0f) glanceTimer = Random.Range(glanceInterval.x, glanceInterval.y);
                glanceTimer -= dt;
                if (glanceTimer > 0f) return;
                glanceTimer = -1f; glanceT = 0f;
                glanceHoldT = Random.Range(glanceHold.x, glanceHold.y);
                glanceAmp = Random.Range(0.65f, 1f); glanceDy = Random.Range(-14f, 8f);
            }
            glanceT += dt;
            const float tOut = 0.11f, tBack = 0.2f;
            float k;
            if (glanceT < tOut) { k = glanceT / tOut; k = k * k * (3f - 2f * k); }
            else if (glanceT < tOut + glanceHoldT) k = 1f;
            else if (glanceT < tOut + glanceHoldT + tBack) { k = 1f - (glanceT - tOut - glanceHoldT) / tBack; k = k * k * (3f - 2f * k); }
            else { glanceT = -1f; return; }
            float upp = rig.unitsPerPixel;
            var off = new Vector3(glanceX * glanceAmp * k * upp, glanceDy * k * upp, 0f);
            eyeA.localPosition = basePos[eyeA] + off; eyeB.localPosition = basePos[eyeB] + off;
        }

        public bool HasClip(string name) { return clips.ContainsKey(name); }

        /// Oynayan klibin normalleştirilmiş konumu (0..1).
        public float Phase01 { get { return clips.TryGetValue(clip, out var c) && c.duration > 0f ? Mathf.Repeat(t, c.duration) / c.duration : 0f; } }
        /// Döngüsüz klip sonuna geldi mi?
        public bool ClipFinished { get { return clips.TryGetValue(clip, out var c) && !c.loop && t >= c.duration; } }

        public void Restart(float phase01 = 0f)
        {
            if (clips.TryGetValue(clip, out var c)) t = phase01 * c.duration;
        }

        /// Klibi hemen (aynı karede) örnekle ve önceki klipten çapraz geçiş yapma: yeniden gösterilen rig eski pozunu bir an bile göstermesin.
        public void ForceUpdate()
        {
            lastClip = null;
            float t0 = t;
            Update();
            t = t0;
        }

        void Update()
        {
            if (rig == null || !rig.IsBuilt || !clips.TryGetValue(clip, out var c) || c.frames < 2) return;
            float f;
            if (manual01 >= 0f) t = Mathf.Clamp01(manual01) * c.duration; else
            t += Time.deltaTime * speed;
            if (c.loop) { t %= c.duration; f = t / c.duration * c.frames; }
            else f = Mathf.Clamp01(t / c.duration) * (c.frames - 1);
            int i0 = Mathf.FloorToInt(f); float u = f - i0;
            int i1 = c.loop ? (i0 + 1) % c.frames : Mathf.Min(i0 + 1, c.frames - 1);
            i0 = c.loop ? i0 % c.frames : Mathf.Min(i0, c.frames - 1);
            float upp = rig.unitsPerPixel;
            if (c != lastClip)
            {
                // snapshot the current pose so the new clip fades in from it
                bool first = lastClip == null;
                lastClip = c; snapR.Clear(); snapP.Clear();
                foreach (var b in rig.bones.Values) { snapR[b] = b.localEulerAngles.z; snapP[b] = b.localPosition; }
                fadeT = first ? 1f : 0f;
            }
            fadeT += fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
            float w = Mathf.Clamp01(fadeT); w = w * w * (3f - 2f * w);
            used.Clear();
            foreach (var tr in c.bones)
            {
                if (!rig.bones.TryGetValue(tr.name, out var b)) continue;
                used.Add(tr.name);
                float r = Mathf.LerpAngle(tr.r[i0], tr.r[i1], u);
                Vector3 p = basePos[b] + new Vector3(Mathf.Lerp(tr.x[i0], tr.x[i1], u) * upp, Mathf.Lerp(tr.y[i0], tr.y[i1], u) * upp, 0);
                if (w < 1f) { r = Mathf.LerpAngle(snapR[b], r, w); p = Vector3.Lerp(snapP[b], p, w); }
                b.localRotation = Quaternion.Euler(0, 0, r); b.localPosition = p;
                // optional scale tracks (breathing); clips without them keep scale 1
                float ssx = tr.sx != null && tr.sx.Length > i1 ? Mathf.Lerp(tr.sx[i0], tr.sx[i1], u) : 1f;
                float ssy = tr.sy != null && tr.sy.Length > i1 ? Mathf.Lerp(tr.sy[i0], tr.sy[i1], u) : 1f;
                b.localScale = new Vector3(ssx, ssy, 1f);
            }
            foreach (var kv in rig.bones)
            {
                if (used.Contains(kv.Key)) continue;
                var b = kv.Value;
                float r = w < 1f ? Mathf.LerpAngle(snapR[b], 0f, w) : 0f;
                Vector3 p = w < 1f ? Vector3.Lerp(snapP[b], basePos[b], w) : basePos[b];
                b.localRotation = Quaternion.Euler(0, 0, r); b.localPosition = p; b.localScale = Vector3.one;
            }
            UpdateGlance();
            UpdateBlink();
        }
    }
}
