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
        [Tooltip("If >= 0, the clip is sampled at this normalized position instead of playing by time.")] public float manual01 = -1f;

        [System.Serializable] class BoneTrack { public string name; public float[] r; public float[] x; public float[] y; }
        [System.Serializable] class Clip { public string name; public float duration; public int frames; public bool loop; public BoneTrack[] bones; }
        [System.Serializable] class ClipFile { public Clip[] clips; }

        RonniSkinRig rig;
        readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        readonly Dictionary<Transform, Vector3> basePos = new Dictionary<Transform, Vector3>();
        float t;

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
            foreach (var tr in c.bones)
            {
                if (!rig.bones.TryGetValue(tr.name, out var b)) continue;
                b.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(tr.r[i0], tr.r[i1], u));
                b.localPosition = basePos[b] + new Vector3(Mathf.Lerp(tr.x[i0], tr.x[i1], u) * upp, Mathf.Lerp(tr.y[i0], tr.y[i1], u) * upp, 0);
            }
        }
    }
}
