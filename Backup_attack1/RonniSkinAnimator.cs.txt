using System.Collections.Generic;
using UnityEngine;

namespace RonniSkin
{
    /// Procedural Idle / Run animation for RonniSkinRig. Toggle `running` (or call SetRunning).
    [RequireComponent(typeof(RonniSkinRig))]
    [DefaultExecutionOrder(0)]
    public class RonniSkinAnimator : MonoBehaviour
    {
        public bool running = false;
        public bool grounded = true;
        [Tooltip("Character is drawn facing left. Tick to mirror it to face right.")]
        public bool faceRight = false;
        [Range(0.1f, 3f)] public float runSpeed = 1f;
        [Range(0.1f, 3f)] public float idleSpeed = 1f;
        public float blendTime = 0.25f;
        [Tooltip("Physics-like lag on ponytail and coat tails.")] public bool addSprings = true;
        [Header("Blink")] public Vector2 blinkInterval = new Vector2(2f, 5f);

        RonniSkinRig rig;
        readonly Dictionary<string, Vector3> basePos = new Dictionary<string, Vector3>();
        float idleT, runT, runW, airW, blinkTimer, blinkPhase = -1f;

        struct K { public float r, x, y, sx, sy; }
        static readonly string[] Driven = {
            "hip","torso","chest","head","hat","armR1","armR2","armR3","armL1","armL2","armL3",
            "katana","katana2","legR1","legR2","footR","toeR","legL1","legL2","footL","toeL",
            "coatR1","coatR2","coatR3","coatL1","coatL2","coatL3","beltTailL","beltTailR","sword",
            "pony1","pony2","pony3","hair_tip_L","hair_tip_M","hair_tip_R","eye_R","eye_L" };

        public void SetRunning(bool r) { running = r; }

        void Start()
        {
            rig = GetComponent<RonniSkinRig>(); rig.Build();
            LoadClips();
            foreach (var n in Driven) if (rig.bones.TryGetValue(n, out var t)) basePos[n] = t.localPosition;
            blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
            if (addSprings)
            {
                AddSpring(0.5f, "pony1", "pony2", "pony3", "pony_end");
            }
        }

        void AddSpring(float amount, params string[] names)
        {
            var arr = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++) if (!rig.bones.TryGetValue(names[i], out arr[i])) return;
            var sp = arr[0].gameObject.AddComponent<RonniSpringChain>();
            sp.chain = arr; sp.amount = amount;
        }

        static float S(float a) => Mathf.Sin(a);
        static float C(float a) => Mathf.Cos(a);
        static float P(float a) => Mathf.Max(0f, a);
        static K R(float r) => new K { r = r, sx = 1, sy = 1 };
        static K RT(float r, float x, float y) => new K { r = r, x = x, y = y, sx = 1, sy = 1 };

        // t = phase 0..2PI over a 3 s loop
        static K Idle(string n, float t)
        {
            switch (n)
            {
                case "hip": return RT(0, 0, 18 * S(t - Mathf.PI / 2));
                case "torso": { var k = R(0.7f * S(t - 0.3f)); k.sy = 1 + 0.008f * S(t - Mathf.PI / 2); return k; }
                case "head": return RT(1.0f * S(t - 0.9f), 0, -8 * S(t - Mathf.PI / 2 - 0.4f));
                case "hat": return R(0.25f * S(t - 1.4f));
                case "armR1": return R(-1.4f * S(t - 1.0f));
                case "armL1": return R(1.4f * S(t - 1.0f));
                case "armR2": return R(1.5f * S(t - 1.5f));
                case "armL2": return R(-1.5f * S(t - 1.5f));
                case "armR3": return R(2f * S(t - 2.0f));
                case "armL3": return R(-2f * S(t - 2.0f));
                case "coatR2": return R(0);
                case "coatR3": return R(1.5f * S(t - 2.0f));
                case "coatL2": return R(0);
                case "coatL3": return R(1.5f * S(t - 2.2f));
                case "beltTailL": return R(1.5f * S(t - 1.2f));
                case "beltTailR": return R(1.5f * S(t - 1.6f));
                case "sword": return R(0.5f * S(t - 0.6f));
                case "pony1": return R(2.5f * S(t - 2.2f) + 1.0f * S(2 * t - 3.2f));
                case "pony2": return R(3.0f * S(t - 2.9f) + 1.2f * S(2 * t - 3.9f));
                case "pony3": return R(4.0f * S(t - 3.6f) + 1.6f * S(2 * t - 4.6f));
                case "hair_tip_L": return R(2.2f * S(t - 1.6f) + 0.8f * S(2 * t - 2.0f));
                case "hair_tip_M": return R(2.0f * S(t - 2.0f) + 0.7f * S(2 * t - 2.6f));
                case "hair_tip_R": return R(2.2f * S(t - 2.4f) + 0.8f * S(2 * t - 3.1f));
            }
            return R(0);
        }

        // p = stride phase 0..2PI over one run cycle (2 steps)
        // ---------- baked key-pose clips (Resources/RonniSkin/ronni_anims.json) ----------
        [System.Serializable] class BoneTrack { public string name; public float[] r; public float[] x; public float[] y; }
        [System.Serializable] class Clip { public string name; public float duration; public int frames; public bool loop; public BoneTrack[] bones; }
        [System.Serializable] class ClipFile { public Clip[] clips; }
        readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        readonly Dictionary<string, Dictionary<string, BoneTrack>> tracks = new Dictionary<string, Dictionary<string, BoneTrack>>();
        float runClipT;

        // ---------- combat actions (attack1-4, block, parry) ----------
        string actionClip; float actionTime, actionW, actionWTarget;
        [Header("Combat")] public float actionBlendIn = 0.07f, actionBlendOut = 0.2f;
        /// Play a baked action clip at clip-time t (seconds; attacks use 0..1 = progress).
        static bool IsGuardClip(string c) => c == "block" || c == "parry" || c == "block_hit";
        public void SetAction(string clip, float t)
        {
            if (clip != null && tracks.ContainsKey(clip))
            {
                // guard poses snap in on the first frame (no sweep from idle/run through other poses)
                if (IsGuardClip(clip) && actionW < 1f) actionW = 1f;
                actionClip = clip; actionTime = t; actionWTarget = 1f;
            }
            else ClearAction();
        }

        // Parry = block pose + one short, hard punch (no raised-sword deflect pose). u = 0..1 over the clip.
        static void ParryPunch(string bone, ref K k, float time, float duration)
        {
            float u = Mathf.Clamp01(time / Mathf.Max(0.01f, duration));
            float p = u < 0.2f ? u / 0.2f : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.2f) / 0.8f);
            switch (bone)
            {
                case "katana2": k.r += 30f * p; break;
                case "armR2": k.r -= 10f * p; break;
                case "torso": k.r += 3f * p; break;
                case "hip": k.y -= 12f * p; break;
            }
        }
        public void ClearAction() { actionWTarget = 0f; }
        public bool ActionActive => actionW > 0.02f && actionClip != null;
        public bool HasClip(string clip) => tracks.ContainsKey(clip);
        public float ClipDuration(string clip) => clips.TryGetValue(clip, out var c) ? c.duration : 0f;

        void LoadClips()
        {
            var ta = Resources.Load<TextAsset>("RonniSkin/ronni_anims");
            if (ta == null) return;
            var f = JsonUtility.FromJson<ClipFile>(ta.text);
            if (f?.clips == null) return;
            foreach (var c in f.clips)
            {
                clips[c.name] = c; var d = new Dictionary<string, BoneTrack>();
                foreach (var b in c.bones) d[b.name] = b;
                tracks[c.name] = d;
            }
        }

        bool SampleClip(string clip, string bone, float t, out K k)
        {
            k = R(0);
            if (!tracks.TryGetValue(clip, out var d)) return false;
            if (!d.TryGetValue(bone, out var tr)) return true;
            var c = clips[clip]; int i0, i1; float u;
            if (c.loop || clip == "run")
            {
                float f = (t / c.duration) * c.frames; i0 = Mathf.FloorToInt(f); u = f - i0;
                i0 = ((i0 % c.frames) + c.frames) % c.frames; i1 = (i0 + 1) % c.frames;
            }
            else
            {
                float f = Mathf.Clamp01(t / c.duration) * (c.frames - 1); i0 = Mathf.Min(Mathf.FloorToInt(f), c.frames - 1);
                i1 = Mathf.Min(i0 + 1, c.frames - 1); u = f - i0;
            }
            k = RT(Mathf.Lerp(tr.r[i0], tr.r[i1], u), Mathf.Lerp(tr.x[i0], tr.x[i1], u), Mathf.Lerp(tr.y[i0], tr.y[i1], u));
            return true;
        }

        static K Run(string n, float p)
        {
            switch (n)
            {
                case "hip": return RT(0, 0, -20 * C(2 * p));
                case "torso": return R(5 + 0.8f * S(p));
                case "head": return RT(-2f + 0.7f * C(2 * p - 0.6f), 0, -6 * C(2 * p - 0.5f));
                case "hat": return RT(-0.5f * C(2 * p - 1.0f), 0, -4 * C(2 * p - 1.2f));
                // legs: thigh swings, knee bends while the leg recovers forward
                case "legR1": return RT(-12 * S(p) - 7 * P(C(p)), 0, 50 * P(C(p)));
                case "legL1": return RT(12 * S(p) + 7 * P(-C(p)), 0, 50 * P(-C(p)));
                case "legR2": return R(36 * Mathf.Pow(P(C(p - 0.5f)), 1.2f) + 3);
                case "legL2": return R(-(36 * Mathf.Pow(P(-C(p - 0.5f)), 1.2f) + 3));
                case "footR": return R(-0.6f * (-12 * S(p) + 36 * Mathf.Pow(P(C(p - 0.5f)), 1.2f)) + 8 * P(C(p - 0.3f)));
                case "footL": return R(-0.6f * (12 * S(p) - 36 * Mathf.Pow(P(-C(p - 0.5f)), 1.2f)) - 6 * P(-C(p - 0.3f)));
                // arms: bent at the elbow, swing opposite to legs
                case "armR1": return RT(-25 + 12 * S(p), 0, 10 * P(-S(p)));
                case "armL1": return RT(45 - 12 * S(p), 0, 10 * P(S(p)));
                case "armR2": return R(-95 + 12 * S(p - 0.5f));
                case "armL2": return R(-40 + 10 * S(p - 0.5f));
                case "armR3": return R(-10 + 5 * S(p - 0.9f));
                case "armL3": return R(-8 + 5 * S(p - 0.9f));
                // coat tails flutter back in a wave
                case "coatR1": return R(0);
                case "coatR2": return R(0);
                case "coatR3": return R(3.5f + 3 * S(2 * p - 2.7f));
                case "coatL1": return R(0);
                case "coatL2": return R(0);
                case "coatL3": return R(3.5f + 3 * S(2 * p - 2.4f));
                case "beltTailL": return R(6 + 3.5f * S(2 * p - 1.5f));
                case "beltTailR": return R(6 + 3.5f * S(2 * p - 1.9f));
                case "sword": return R(1.5f * S(2 * p - 1.0f));
                case "pony1": return R(8 + 3 * S(2 * p - 1.6f));
                case "pony2": return R(7 + 6 * S(2 * p - 2.4f));
                case "pony3": return R(7 + 9 * S(2 * p - 3.2f));
                case "hair_tip_L": return R(3 + 2.5f * S(2 * p - 1.2f));
                case "hair_tip_M": return R(3 + 2.5f * S(2 * p - 1.5f));
                case "hair_tip_R": return R(3 + 2.5f * S(2 * p - 1.8f));
            }
            return R(0);
        }

        static K Air(string n, float t)
        {
            switch (n)
            {
                case "torso": return R(4);
                case "head": return R(-2);
                case "legR1": return RT(-22, 0, 60);
                case "legR2": return R(45);
                case "footR": return R(-10);
                case "legL1": return RT(14, 0, 30);
                case "legL2": return R(-30);
                case "footL": return R(8);
                case "armR1": return R(-35 + 3 * S(t * 6));
                case "armR2": return R(25);
                case "armL1": return R(35 + 3 * S(t * 6 + 1));
                case "armL2": return R(-25);
                case "coatR1": return R(0); case "coatR2": return R(0); case "coatR3": return R(6 + 3 * S(t * 8 - 1));
                case "coatL1": return R(0); case "coatL2": return R(0); case "coatL3": return R(6 + 3 * S(t * 8 - 0.5f));
                case "pony1": return R(14); case "pony2": return R(10 + 4 * S(t * 8)); case "pony3": return R(10 + 6 * S(t * 8 - 1));
                case "hair_tip_L": return R(-4); case "hair_tip_M": return R(-4); case "hair_tip_R": return R(-4);
                case "beltTailL": return R(15); case "beltTailR": return R(15);
            }
            return R(0);
        }

        void Update()
        {
            if (rig == null || !rig.IsBuilt) return;
            float dt = Time.deltaTime;
            idleT = (idleT + dt * idleSpeed * (2 * Mathf.PI / 3f)) % (2 * Mathf.PI);
            runT = (runT + dt * runSpeed * (2 * Mathf.PI / (16f / 30f))) % (2 * Mathf.PI);
            if (clips.TryGetValue("run", out var rc)) runClipT = (runClipT + dt * runSpeed) % rc.duration;
            runW = Mathf.MoveTowards(runW, running ? 1f : 0f, blendTime > 0 ? dt / blendTime : 1f);
            float w = Mathf.SmoothStep(0, 1, runW);
            rig.SetRunLayers(runW > 0.5f || (actionClip == "dash" && actionW > 0.3f));
            airW = Mathf.MoveTowards(airW, grounded ? 0f : 1f, dt / 0.12f);
            float aw = Mathf.SmoothStep(0, 1, airW);

            // blink
            float eyeY = 1f;
            if (blinkPhase < 0) { blinkTimer -= dt; if (blinkTimer <= 0) blinkPhase = 0; }
            else
            {
                blinkPhase += dt / 0.16f;
                eyeY = blinkPhase < 0.4f ? Mathf.Lerp(1, 0.08f, blinkPhase / 0.4f) : blinkPhase < 0.6f ? 0.08f : Mathf.Lerp(0.08f, 1, (blinkPhase - 0.6f) / 0.4f);
                if (blinkPhase >= 1) { blinkPhase = -1; blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y); }
            }

            actionW = Mathf.MoveTowards(actionW, actionWTarget, dt / Mathf.Max(0.001f, actionWTarget > actionW ? actionBlendIn : actionBlendOut));
            float acw = Mathf.SmoothStep(0, 1, actionW);
            bool drawn = acw > 0.02f && actionClip != null && actionClip != "dash";
            bool guardGrip = actionClip == "block" || actionClip == "block_hit" || actionClip == "parry" || actionClip == "parry_success" || actionClip == "guard_break";
            bool thickArm = drawn && guardGrip && actionClip != "guard_break";
            rig.SetVisible("katana", drawn && !guardGrip); rig.SetVisible("katana2", drawn && guardGrip); rig.SetVisible("saya", drawn); rig.SetVisible("sword", !drawn);
            // two-handed guard: arms swap to split upper/lower pieces (clean elbows, thick sword arm)
            rig.SetVisible("arm_R", !thickArm); rig.SetVisible("arm_L", !thickArm); rig.SetVisible("arm_R_thick", false);
            rig.SetVisible("arm_R_up", thickArm); rig.SetVisible("arm_R_lo", thickArm); rig.SetVisible("arm_L_up", thickArm); rig.SetVisible("arm_L_lo", thickArm);
            // slash smear: flashes in during the swing, fades over the follow-through
            float sa = 0f;
            if (actionClip == "attack1" && acw > 0.5f)
            {
                float u = actionTime;
                sa = u < 0.27f ? Mathf.Clamp01((u - 0.12f) / 0.06f) : Mathf.Clamp01(1f - (u - 0.27f) / 0.16f);
            }
            rig.SetVisible("slash1", sa > 0.01f);
            if (sa > 0.01f) rig.SetPartColor("slash1", new Color(1f, 1f, 1f, sa));
            // guard break: sword arm + katana fall back behind the body (base layer order)
            rig.SetActLayers(drawn && acw > 0.5f && actionClip != "guard_break");

            float upp = rig.unitsPerPixel;
            foreach (var n in Driven)
            {
                if (!rig.bones.TryGetValue(n, out var t)) continue;
                K a = Idle(n, idleT), c = Air(n, Time.time);
                if (!SampleClip("run", n, runClipT, out K b)) b = Run(n, runT);
                float r = Mathf.Lerp(Mathf.Lerp(a.r, b.r, w), c.r, aw), x = Mathf.Lerp(Mathf.Lerp(a.x, b.x, w), c.x, aw), y = Mathf.Lerp(Mathf.Lerp(a.y, b.y, w), c.y, aw);
                float sx = Mathf.Lerp(Mathf.Lerp(a.sx, b.sx, w), c.sx, aw), sy = Mathf.Lerp(Mathf.Lerp(a.sy, b.sy, w), c.sy, aw);
                // parry reads from the (static) block pose; only bones missing there fall back to the parry clip
                string src = actionClip; float st = actionTime;
                bool parryPunch = actionClip == "parry" && tracks.TryGetValue("block", out var bt) && bt.ContainsKey(n);
                if (parryPunch) { src = "block"; st = 0f; }
                if (acw > 0f && actionClip != null && tracks[src].ContainsKey(n) && SampleClip(src, n, st, out K act))
                {
                    if (parryPunch) ParryPunch(n, ref act, actionTime, ClipDuration("parry"));
                    r = Mathf.LerpAngle(r, act.r, acw); x = Mathf.Lerp(x, act.x, acw); y = Mathf.Lerp(y, act.y, acw);
                }
                if (n == "eye_R" || n == "eye_L") sy *= eyeY;
                t.localRotation = Quaternion.Euler(0, 0, r);
                t.localPosition = basePos[n] + new Vector3(x * upp, y * upp, 0);
                t.localScale = new Vector3(sx, sy, 1);
            }
            var s = transform.localScale;
            float fx = Mathf.Abs(s.x) * (faceRight ? -1 : 1);
            if (!Mathf.Approximately(s.x, fx)) transform.localScale = new Vector3(fx, s.y, s.z);
        }
    }
}
