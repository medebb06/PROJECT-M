using UnityEngine;

/// <summary>
/// DUSMAN VURUS UYARISI SESI (EnemyUnit sistemi): saldiri hazirligi (windup) baslarken Resources/EnemyWarn'dan rastgele klip.
/// Uzaktaki dusmanin sesi mesafeyle kisilir (oyuncuya gore).
/// </summary>
public static class EnemyWarnSfx
{
    public static bool Enabled = false;   // simdilik kapali: EnemyWarn klasoru bos, yeni ses eklenince true yap
    public static float Volume = 0.22f;
    public static Vector2 Pitch = new Vector2(0.96f, 1.06f);
    public static float UnblockablePitchMul = 1.25f;
    public static float NearDist = 10f, FarDist = 28f;

    static AudioClip[] clips;
    static AudioSource src;
    static int last = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { clips = null; src = null; last = -1; }

    public static void Play(Vector3 pos, bool unblockable)
    {
        if (!Enabled) return;
        if (clips == null) clips = Resources.LoadAll<AudioClip>("EnemyWarn");
        if (clips.Length == 0) return;
        if (src == null)
        {
            var go = new GameObject("EnemyWarnSfx");
            Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
        }

        float vol = Volume;
        var pc = Object.FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            float d = Mathf.Abs(pc.transform.position.x - pos.x);
            vol *= 1f - Mathf.Clamp01((d - NearDist) / Mathf.Max(0.1f, FarDist - NearDist));
        }
        if (vol <= 0.01f) return;

        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == last) i = (i + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        last = i;
        src.pitch = Random.Range(Pitch.x, Pitch.y) * (unblockable ? UnblockablePitchMul : 1f);
        src.PlayOneShot(clips[i], vol);
    }
}
