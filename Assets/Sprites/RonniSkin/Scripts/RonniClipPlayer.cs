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

        [System.Serializable] class BoneTrack { public string name; public float[] r; public float[] x; public float[] y; }
        [System.Serializable] class Clip { public string name; public float duration; public int frames; public bool loop; public BoneTrack[] bones; }
        [System.Serializable] class ClipFile { public Clip[] clips; }

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
        }

        public bool HasClip(string name) { return clips.ContainsKey(name); }

        public void Restart(float phase01 = 0f)
        {
            if (clips.TryGetValue(clip, out var c)) t = phase01 * c.duration;
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
            }
            foreach (var kv in rig.bones)
            {
                if (used.Contains(kv.Key)) continue;
                var b = kv.Value;
                float r = w < 1f ? Mathf.LerpAngle(snapR[b], 0f, w) : 0f;
                Vector3 p = w < 1f ? Vector3.Lerp(snapP[b], basePos[b], w) : basePos[b];
                b.localRotation = Quaternion.Euler(0, 0, r); b.localPosition = p;
            }
        }
    }
}
