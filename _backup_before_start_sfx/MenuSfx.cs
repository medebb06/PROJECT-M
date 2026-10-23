using UnityEngine;

/// <summary>
/// MENU SESLERI: secenekler arasinda gezinirken (menu_nav_*) ve secimde (menu_sel_*) Resources/MenuSfx'ten rastgele klik.
/// </summary>
public static class MenuSfx
{
    public static float NavVolume = 0.6f;
    public static float SelectVolume = 0.9f;
    public static Vector2 NavPitch = new Vector2(0.97f, 1.03f);

    static AudioClip[] nav, sel;
    static AudioSource src;
    static int lastNav = -1, lastSel = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { nav = sel = null; src = null; lastNav = lastSel = -1; }

    static void Init()
    {
        if (nav != null && src != null) return;
        var all = Resources.LoadAll<AudioClip>("MenuSfx");
        var n = new System.Collections.Generic.List<AudioClip>();
        var s = new System.Collections.Generic.List<AudioClip>();
        foreach (var c in all)
        {
            if (c.name.StartsWith("menu_nav")) n.Add(c);
            else if (c.name.StartsWith("menu_sel")) s.Add(c);
        }
        nav = n.ToArray(); sel = s.ToArray();
        if (src == null)
        {
            var go = new GameObject("MenuSfx");
            Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;
        }
    }

    static void Play(AudioClip[] set, ref int last, float vol, float pitch)
    {
        if (set == null || set.Length == 0 || src == null) return;
        int i = Random.Range(0, set.Length);
        if (set.Length > 1 && i == last) i = (i + 1 + Random.Range(0, set.Length - 1)) % set.Length;
        last = i;
        src.pitch = pitch;
        src.PlayOneShot(set[i], vol);
    }

    public static void Nav() { Init(); Play(nav, ref lastNav, NavVolume, Random.Range(NavPitch.x, NavPitch.y)); }
    public static void Select() { Init(); Play(sel, ref lastSel, SelectVolume, 1f); }
}
