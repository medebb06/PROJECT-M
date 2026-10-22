using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Global Light 2D'nin etkin rengini (renk * şiddet) shader'lara verir: _ProjectM_Ambient2D.
/// SilhouetteRim bunu ışık dokusundan çıkarıp sadece meşale/fener gibi noktasal ışıkları ekler.
/// Sahneye eklemek gerekmez: oyunda ve editörde kendiliğinden çalışır.
/// </summary>
public static class Ambient2DSync
{
    private static readonly int Id = Shader.PropertyToID("_ProjectM_Ambient2D");
    private static Light2D global;
    private static float nextFind;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        global = null;
        nextFind = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        GameObject go = new GameObject("~Ambient2DSync");
        go.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
    }

    public static void Tick()
    {
        float now = Time.realtimeSinceStartup;

        if ((global == null || !global.isActiveAndEnabled) && now >= nextFind)
        {
            nextFind = now + 0.5f;
            global = null;

            foreach (Light2D l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (l.lightType == Light2D.LightType.Global && l.isActiveAndEnabled)
                {
                    global = l;
                    break;
                }
            }
        }

        Color c = global != null ? global.color * global.intensity : Color.white;
        Shader.SetGlobalVector(Id, new Vector4(c.r, c.g, c.b, 1f));

    }


    private class Runner : MonoBehaviour
    {
        private void LateUpdate() => Tick();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void EditorHook()
    {
        UnityEditor.EditorApplication.update += () =>
        {
            if (!Application.isPlaying)
                Tick();
        };
    }
#endif
}
