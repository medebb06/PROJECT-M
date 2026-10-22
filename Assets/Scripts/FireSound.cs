using UnityEngine;

/// <summary>
/// Ateş (meşale/kamp ateşi) çıtırtısı: oyuncu yaklaştıkça ses açılır, uzaklaşınca kısılır.
/// Yeni bir ateşe eklemek için bu bileşeni ekle ve Clip alanına sesi ata.
/// </summary>
[DisallowMultipleComponent]
public class FireSound : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.55f;
    public float minDistance = 3f;     // bu mesafede tam ses
    public float maxDistance = 20f;    // bu mesafeden uzakta sessiz
    public float fadeSpeed = 2.5f;

    private AudioSource src;
    private Transform player;
    private float cur;

    private void Start()
    {
        if (!Application.isPlaying) return;

        src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        src.priority = 160;

        // Her ateş farklı yerden başlasın (hepsi aynı anda çıtırdamasın).
        if (clip != null)
            src.time = Random.Range(0f, Mathf.Max(0f, clip.length - 1f));
    }

    private void Update()
    {
        if (src == null || clip == null) return;

        if (player == null)
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            else return;
        }

        float d = Mathf.Abs(player.position.x - transform.position.x);
        float k = 1f - Mathf.Clamp01((d - minDistance) / Mathf.Max(0.01f, maxDistance - minDistance));
        k = k * k * (3f - 2f * k);

        cur = Mathf.MoveTowards(cur, k * volume, Time.unscaledDeltaTime * fadeSpeed * volume);
        src.volume = cur;

        if (cur > 0.003f)
        {
            if (!src.isPlaying) src.Play();
        }
        else if (src.isPlaying)
        {
            src.Pause();
        }
    }
}
