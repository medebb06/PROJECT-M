using UnityEngine;

namespace RonniSkin
{
    /// A textured grid mesh deformed by a chain of bones (linear blend skinning).
    /// Chain = joints j0..jn; bone i covers segment j(i)->j(i+1). Rigid parts use one bone.
    [DefaultExecutionOrder(200)]
    public class RonniSoftPart : MonoBehaviour
    {
        RonniSkinRig rig; Transform[] joints; Mesh mesh;
        Vector3[] bindVerts, outVerts; int[] wIdx; float[] wVal; int nInf; Matrix4x4[] bindInv; int nBones;

        public void Init(RonniSkinRig r, Texture2D tex, Material mat, Transform[] chain, Vector3 min, Vector3 max, int cols, int rows, string layer, int order, float wPow = 4f, float wFall = 0.12f, System.Func<Vector3, Vector3> remap = null)
        {
            rig = r; joints = chain; qMin = min; qMax = max;
            nBones = Mathf.Max(1, chain.Length - 1);
            var mf = gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat; mr.sortingLayerName = layer; mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;

            int vc = (cols + 1) * (rows + 1);
            bindVerts = new Vector3[vc]; outVerts = new Vector3[vc];
            var uv = new Vector2[vc]; var col = new Color[vc];
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= cols; x++)
                {
                    int i = y * (cols + 1) + x; float u = (float)x / cols, v = (float)y / rows;
                    bindVerts[i] = new Vector3(Mathf.Lerp(min.x, max.x, u), Mathf.Lerp(min.y, max.y, v), 0);
                    if (remap != null) bindVerts[i] = remap(bindVerts[i]);
                    uv[i] = new Vector2(u, v); col[i] = Color.white;
                }
            var tris = new int[cols * rows * 6]; int t = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int a = y * (cols + 1) + x, b = a + 1, c = a + cols + 1, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b; tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            mesh.vertices = bindVerts; mesh.uv = uv; mesh.colors = col; mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            mf.sharedMesh = mesh;

            // bind matrices in rig-root space
            Matrix4x4 rootInv = rig.transform.worldToLocalMatrix;
            bindInv = new Matrix4x4[nBones];
            for (int i = 0; i < nBones; i++) bindInv[i] = (rootInv * joints[i].localToWorldMatrix).inverse;

            // weights
            nInf = Mathf.Min(3, nBones);
            wIdx = new int[vc * nInf]; wVal = new float[vc * nInf];
            var jp = new Vector3[chain.Length];
            for (int i = 0; i < chain.Length; i++) jp[i] = rootInv.MultiplyPoint3x4(chain[i].position);
            float avgLen = 0; for (int i = 0; i < nBones && chain.Length > 1; i++) avgLen += Vector3.Distance(jp[i], jp[i + 1]);
            avgLen = chain.Length > 1 ? avgLen / nBones : 1f;
            var tmp = new float[nBones];
            for (int v = 0; v < vc; v++)
            {
                if (nBones == 1) { wIdx[v * nInf] = 0; wVal[v * nInf] = 1; continue; }
                for (int i = 0; i < nBones; i++)
                {
                    float d = DistSeg(bindVerts[v], jp[i], jp[i + 1]);
                    tmp[i] = 1f / Mathf.Pow(d + wFall * avgLen, wPow);
                }
                // keep strongest nInf
                for (int k = 0; k < nInf; k++)
                {
                    int best = 0; float bw = -1;
                    for (int i = 0; i < nBones; i++) if (tmp[i] > bw) { bw = tmp[i]; best = i; }
                    wIdx[v * nInf + k] = best; wVal[v * nInf + k] = bw; tmp[best] = -2;
                }
                float s = 0; for (int k = 0; k < nInf; k++) s += Mathf.Max(0, wVal[v * nInf + k]);
                for (int k = 0; k < nInf; k++) wVal[v * nInf + k] = Mathf.Max(0, wVal[v * nInf + k]) / s;
            }
        }

        // ---- Pin: dış kenar sabit (parent kemiği ile birlikte), iç kenara doğru zincir hareketi giderek artar ----
        Transform pinAnchor; Matrix4x4 pinAnchorBindInv; float[] pinM;
        /// xOuter / xInner = rig-yerel x. xOuter'da vertex sadece parent kemiğini izler, xInner'da zincirin tam deformasyonunu alır.
        public void SetPin(float xOuter, float xInner, float pow)
        {
            if (bindVerts == null || joints == null || joints.Length == 0 || joints[0].parent == null || Mathf.Approximately(xOuter, xInner)) return;
            pinAnchor = joints[0].parent;
            pinAnchorBindInv = (rig.transform.worldToLocalMatrix * pinAnchor.localToWorldMatrix).inverse;
            pinM = new float[bindVerts.Length];
            for (int v = 0; v < pinM.Length; v++)
            {
                float t = Mathf.Clamp01((bindVerts[v].x - xOuter) / (xInner - xOuter));
                pinM[v] = Mathf.Pow(Mathf.SmoothStep(0f, 1f, t), Mathf.Max(0.1f, pow));
            }
        }

        static float DistSeg(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        Vector3 qMin, qMax; Vector2[] winUv = new Vector2[4]; Color[] winCol = new Color[4];
        /// Tek parça (quad) için: görüntünün yatayda [u0,u1] aralığını gösterir; sol kenar c0, sağ kenar c1 renginde (iz solması).
        public void SetWindow(float u0, float u1, Color c0, Color c1, bool mirror = false)
        {
            if (mesh == null || bindVerts == null || bindVerts.Length != 4) return;
            bool vis = u1 - u0 > 0.003f;
            SetVisible(vis); if (!vis) return;
            float x0 = Mathf.Lerp(qMin.x, qMax.x, u0), x1 = Mathf.Lerp(qMin.x, qMax.x, u1);
            bindVerts[0].x = x0; bindVerts[2].x = x0; bindVerts[1].x = x1; bindVerts[3].x = x1;
            float v0 = mirror ? 1f - u0 : u0, v1 = mirror ? 1f - u1 : u1;
            winUv[0] = new Vector2(v0, 0f); winUv[1] = new Vector2(v1, 0f); winUv[2] = new Vector2(v0, 1f); winUv[3] = new Vector2(v1, 1f);
            winCol[0] = c0; winCol[1] = c1; winCol[2] = c0; winCol[3] = c1;
            mesh.uv = winUv; mesh.colors = winCol;
            curColor = new Color(-1f, -1f, -1f, -1f);
        }
        Color curColor = Color.white; Color[] colBuf; MeshRenderer mrCache;
        bool partVisible = true, rigShown = true;
        public void SetVisible(bool v) { partVisible = v; ApplyVis(); }
        public void SetRigShown(bool v) { rigShown = v; ApplyVis(); }
        void ApplyVis()
        {
            if (mrCache == null) mrCache = GetComponent<MeshRenderer>();
            bool on = partVisible && rigShown;
            if (mrCache != null && mrCache.enabled != on) mrCache.enabled = on;
        }
        public void SetColor(Color c, bool visible)
        {
            if (mesh == null || c == curColor) return;
            curColor = c;
            if (colBuf == null) colBuf = new Color[bindVerts.Length];
            for (int i = 0; i < colBuf.Length; i++) colBuf[i] = c;
            mesh.colors = colBuf;
        }

        readonly Matrix4x4[] skin = new Matrix4x4[8];
        void LateUpdate()
        {
            if (mesh == null) return;
            Matrix4x4 rootInv = rig.transform.worldToLocalMatrix;
            for (int i = 0; i < nBones; i++) skin[i] = rootInv * joints[i].localToWorldMatrix * bindInv[i];
            Matrix4x4 anchorSkin = pinM != null ? rootInv * pinAnchor.localToWorldMatrix * pinAnchorBindInv : Matrix4x4.identity;
            for (int v = 0; v < bindVerts.Length; v++)
            {
                Vector3 acc = Vector3.zero;
                for (int k = 0; k < nInf; k++) { float w = wVal[v * nInf + k]; if (w > 0) acc += skin[wIdx[v * nInf + k]].MultiplyPoint3x4(bindVerts[v]) * w; }
                if (pinM != null) acc = Vector3.Lerp(anchorSkin.MultiplyPoint3x4(bindVerts[v]), acc, pinM[v]);
                outVerts[v] = acc;
            }
            mesh.vertices = outVerts;
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;
        }
    }
}
