using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sahnedeki ateşleri (meşale, kamp ateşi) shader'lara bildirir: kenar ışığı + sahte normal map
/// (ProjectMFire.hlsl). TorchFire her kare kendi konum / renk / şiddetini yazar.
/// En fazla 4 ateş; kameraya en yakın olanlar seçilir.
/// </summary>
public static class FireLights
{
    public class Fire
    {
        public Vector2 pos;
        public float radius;
        public float intensity;
        public Color color;
    }

    private static readonly List<Fire> fires = new List<Fire>();
    private static readonly Vector4[] posArr = new Vector4[4];
    private static readonly Vector4[] colArr = new Vector4[4];
    private static readonly int PosId = Shader.PropertyToID("_ProjectM_Fire4Pos");
    private static readonly int ColId = Shader.PropertyToID("_ProjectM_Fire4Col");
    private static readonly int CountId = Shader.PropertyToID("_ProjectM_Fire4Count");
    private static int lastFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        fires.Clear();
        lastFrame = -1;
    }

    public static void Register(Fire f)
    {
        if (!fires.Contains(f)) fires.Add(f);
    }

    public static void Unregister(Fire f)
    {
        fires.Remove(f);
        Publish(true);
    }

    /// <summary>Karede bir kez yayınlar (birden çok TorchFire çağırsa da).</summary>
    public static void Publish(bool force = false)
    {
        if (!force && Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;

        Vector3 cp = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        if (fires.Count > 4)
            fires.Sort((a, b) => Mathf.Abs(a.pos.x - cp.x).CompareTo(Mathf.Abs(b.pos.x - cp.x)));

        int n = Mathf.Min(4, fires.Count);
        for (int i = 0; i < 4; i++)
        {
            if (i < n)
            {
                Fire f = fires[i];
                posArr[i] = new Vector4(f.pos.x, f.pos.y, f.radius, f.intensity);
                colArr[i] = new Vector4(f.color.r, f.color.g, f.color.b, 0f);
            }
            else
            {
                posArr[i] = new Vector4(0f, 0f, 0f, 0f);
                colArr[i] = Vector4.zero;
            }
        }

        Shader.SetGlobalVectorArray(PosId, posArr);
        Shader.SetGlobalVectorArray(ColId, colArr);
        Shader.SetGlobalFloat(CountId, n);
    }
}
