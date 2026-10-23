using UnityEngine;

/// <summary>
/// OYUNCU SALDIRI SESI ("hah!"): her vuruşta Resources/PlayerSfx/grunt_* içinden rastgele biri çalar.
/// Aynı klip üst üste gelmez, perde/ses seviyesi çok az oynar. Sessiz yapmak için Volume = 0.
/// </summary>
public static class PlayerGrunt
{
    public static float Volume = 0.85f;
    public static Vector2 Pitch = new Vector2(0.97f, 1.03f);

    static AudioClip[] clips;
    static AudioSource src;
    static int last = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { clips = null; src = null; last = -1; }

    static void Init()
    {
        if (clips == null)
        {
            var all = Resources.LoadAll<AudioClip>("PlayerSfx");
            var l = new System.Collections.Generic.List<AudioClip>();
            foreach (var c in all) if (c.name.StartsWith("grunt")) l.Add(c);
            clips = l.ToArray();
        }

        if (src == null)
        {
            var go = new GameObject("PlayerGrunt");
            Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
        }
    }

    public static void Play()
    {
        Init();
        if (clips == null || clips.Length == 0 || src == null || Volume <= 0f) return;

        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == last) i = (i + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        last = i;

        src.pitch = Random.Range(Pitch.x, Pitch.y);
        src.PlayOneShot(clips[i], Volume * Random.Range(0.9f, 1f));
    }
}
