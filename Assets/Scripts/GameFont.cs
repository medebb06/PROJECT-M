using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// OYUN YAZI TIPI (KG Perfect Penmanship): Resources/UI/KGPerfectPenmanship.otf (yedekler: Hoshiko_Satsuki, Knight_Souls).
/// Tum yazilarda kullanilir: OnGUI (GUI.skin.font), UI Text, TextMesh (kritik sayilari, callout'lar) ve
/// kodla kurulan stiller (GameFont.Get / Or). Font yoksa hicbir sey degismez.
/// Not: font buyuk harflidir (kucuk harfler kucuk-buyuk harf gibi gorunur).
/// </summary>
public static class GameFont
{
    static Font font;
    static bool tried;

    public static Font Get()
    {
        if (!tried)
        {
            tried = true;
            font = Resources.Load<Font>("UI/KGPerfectPenmanship");
            if (font == null) font = Resources.Load<Font>("UI/Hoshiko_Satsuki");
            if (font == null) font = Resources.Load<Font>("UI/Knight_Souls");
            if (font == null) Debug.LogWarning("[GameFont] Resources/UI/KGPerfectPenmanship bulunamadi: varsayilan font kullanilacak.");
        }
        return font;
    }

    /// <summary>Oyun fontu varsa onu, yoksa verilen yedegi dondurur.</summary>
    public static Font Or(Font fallback)
    {
        Font f = Get();
        return f != null ? f : fallback;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Get() == null) return;
        if (Object.FindFirstObjectByType<GameFontApplier>() != null) return;
        GameObject go = new GameObject("GameFontApplier");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<GameFontApplier>();
    }
}

/// <summary>Sahnedeki tum UI Text / TextMesh / OnGUI yazilarini oyun fontuna cevirir (yeni olusanlar dahil).</summary>
[DefaultExecutionOrder(-32000)]
public class GameFontApplier : MonoBehaviour
{
    float next;

    void OnGUI()
    {
        Font f = GameFont.Get();
        if (f != null && GUI.skin.font != f) GUI.skin.font = f;
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;

        Font f = GameFont.Get();
        if (f == null) return;

        Text[] texts = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
            if (texts[i].font != f) texts[i].font = f;

        TextMesh[] meshes = FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < meshes.Length; i++)
        {
            TextMesh tm = meshes[i];
            if (tm.font == f) continue;
            tm.font = f;
            MeshRenderer mr = tm.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = f.material;
        }
    }
}
