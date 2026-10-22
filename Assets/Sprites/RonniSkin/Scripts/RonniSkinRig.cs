using System;
using System.Collections.Generic;
using UnityEngine;

namespace RonniSkin
{
    /// Builds Ronni's skeleton and skinned parts from Resources/Ronni at runtime.
    /// Add this to an empty GameObject (plus RonniSkinAnimator) and press Play.
    [DefaultExecutionOrder(-100)]
    public class RonniSkinRig : MonoBehaviour
    {
        [Tooltip("World units per source pixel (canvas is 3000x5000 px).")]
        public float unitsPerPixel = 0.001f;
        public string sortingLayerName = "Default";
        public int sortingOrderOffset = 0;
        [Tooltip("Grid density for bending parts (columns x rows).")]
        public Vector2Int skinGrid = new Vector2Int(8, 14);

        [Serializable] class BoneData { public string name; public string parent; public float x; public float y; }
        [Serializable] class PartData { public string name; public float x; public float y; public float w; public float h; public int order; public string[] bones; public float wPow = 4f; public float wFall = 0.12f; public float runOrder = -1f; public float actOrder = -1f; public bool hidden; public int[] grid; public float pinOuter = -1f; public float pinInner = -1f; public float pinPow = 1.2f; }
        [Serializable] class RigData { public BoneData[] bones; public PartData[] parts; public float[] origin; public float heightPx; }

        public readonly List<RonniSoftPart> parts = new List<RonniSoftPart>();
        readonly Dictionary<string, RonniSoftPart> partByName = new Dictionary<string, RonniSoftPart>();
        bool rigShown = true;
        /// Show/hide the whole character (keeps per-part visibility).
        public void SetShown(bool on) { if (on == rigShown) return; rigShown = on; foreach (var p in parts) if (p != null) p.SetRigShown(on); }
        public bool Shown => rigShown;
        public void SetPartColor(string part, Color c) { if (partByName.TryGetValue(part, out var p) && p != null) p.SetColor(c, true); }
        public void SetPartWindow(string part, float u0, float u1, Color c0, Color c1, bool mirror = false) { if (partByName.TryGetValue(part, out var p) && p != null) p.SetWindow(u0, u1, c0, c1, mirror); }
        public void SetVisible(string part, bool v) { if (partByName.TryGetValue(part, out var p) && p != null) p.SetVisible(v); }
        readonly List<int> baseOrder = new List<int>(), runOrderList = new List<int>(), actOrderList = new List<int>();
        bool actLayers;
        /// While the katana is drawn both hands come in front of the body.
        public void SetActLayers(bool on) { if (on == actLayers) return; actLayers = on; ApplyOrders(); }
        bool runLayers;
        public void SetSorting(string layer, int offset)
        {
            sortingLayerName = layer; sortingOrderOffset = offset; ApplyOrders();
        }
        /// Some parts (outer coat panel) go behind the legs while running.
        public void SetRunLayers(bool on)
        {
            if (on == runLayers) return; runLayers = on; ApplyOrders();
        }
        void ApplyOrders()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                var mr = parts[i] != null ? parts[i].GetComponent<MeshRenderer>() : null;
                if (mr == null) continue;
                mr.sortingLayerName = sortingLayerName;
                mr.sortingOrder = sortingOrderOffset + (actLayers ? actOrderList[i] : runLayers ? runOrderList[i] : baseOrder[i]);
            }
        }
        /// Son verilen renk alfası (CharacterRim: solan çizimin kenar ışığını gizlemek için).
        public float LastAlpha = 1f;
        public void SetColor(Color c, bool visible) { LastAlpha = c.a; foreach (var p in parts) if (p != null) p.SetColor(c, visible); }
        public readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        public bool IsBuilt { get; private set; }
        float OX = 1500f, OY = 3640f;
        [Tooltip("Resources sub-folder holding ronni_rig.json and the part images.")] public string resourceFolder = "RonniSkin";
        public float HeightPx { get; private set; } = 3350f;

        Vector3 ToLocal(float px, float py) => new Vector3((px - OX) * unitsPerPixel, (OY - py) * unitsPerPixel, 0f);

        void Awake() { Build(); }

        public void Build()
        {
            if (IsBuilt) return;
            var json = Resources.Load<TextAsset>(resourceFolder + "/ronni_rig");
            if (json == null) { Debug.LogError("Ronni: Resources/Ronni/ronni_rig.json not found"); return; }
            var data = JsonUtility.FromJson<RigData>(json.text);
            if (data.origin != null && data.origin.Length == 2) { OX = data.origin[0]; OY = data.origin[1]; }
            if (data.heightPx > 0) HeightPx = data.heightPx;

            // bones
            var abs = new Dictionary<string, Vector3>();
            foreach (var b in data.bones)
            {
                var go = new GameObject(b.name);
                Transform parent = string.IsNullOrEmpty(b.parent) ? transform : bones[b.parent];
                go.transform.SetParent(parent, false);
                var p = ToLocal(b.x, b.y);
                abs[b.name] = p;
                go.transform.localPosition = string.IsNullOrEmpty(b.parent) ? p : p - abs[b.parent];
                bones[b.name] = go.transform;
            }

            // parts
            var partsRoot = new GameObject("Parts").transform;
            partsRoot.SetParent(transform, false);
            // Karakter isigi: mesale isigi + ates kenar isigi + sahte normal map (ProjectM/CharacterLit).
            var litRef = Resources.Load<Material>("CharacterLit");
            Shader sh = litRef != null ? litRef.shader : null;
            if (sh == null || !sh.isSupported) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            foreach (var pd in data.parts)
            {
                var tex = Resources.Load<Texture2D>(resourceFolder + "/" + pd.name);
                if (tex == null) { Debug.LogWarning("Ronni: missing texture " + pd.name); continue; }
                var go = new GameObject(pd.name);
                go.transform.SetParent(partsRoot, false);
                var sp = go.AddComponent<RonniSoftPart>(); parts.Add(sp); baseOrder.Add(pd.order * 10); partByName[pd.name] = sp; runOrderList.Add(pd.runOrder >= 0 ? Mathf.RoundToInt(pd.runOrder * 10) : pd.order * 10); actOrderList.Add(pd.actOrder >= 0 ? Mathf.RoundToInt(pd.actOrder * 10) : pd.order * 10);
                var bl = new List<Transform>();
                foreach (var bn in pd.bones) bl.Add(bones[bn]);
                bool skinned = bl.Count > 1;
                Vector3 min = ToLocal(pd.x, pd.y + pd.h), max = ToLocal(pd.x + pd.w, pd.y);
                var mat = new Material(sh) { mainTexture = tex, name = "Ronni_" + pd.name };
                sp.Init(this, tex, mat, bl.ToArray(), min, max,
                        skinned ? (pd.grid != null && pd.grid.Length == 2 ? pd.grid[0] : skinGrid.x) : 1, skinned ? (pd.grid != null && pd.grid.Length == 2 ? pd.grid[1] : skinGrid.y) : 1,
                        sortingLayerName, sortingOrderOffset + pd.order * 10, pd.wPow > 0 ? pd.wPow : 4f, pd.wFall > 0 ? pd.wFall : 0.12f);
                if (skinned && pd.pinOuter >= 0f && pd.pinInner >= 0f) sp.SetPin(ToLocal(pd.pinOuter, 0f).x, ToLocal(pd.pinInner, 0f).x, pd.pinPow);
                if (pd.hidden) sp.SetVisible(false);
            }
            IsBuilt = true;
        }
    }
}
