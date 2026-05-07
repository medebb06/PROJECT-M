using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource movementSource; // RUN
    public AudioSource sfxSource;      // JUMP / DASH

    [Header("Clips")]
    public AudioClip jumpClip;
    public AudioClip dashClip;
    public AudioClip runClip;

    void Awake()
    {
        // RUN source
        if (!movementSource)
            movementSource = GetComponent<AudioSource>();

        // SFX source
        if (!sfxSource)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        sfxSource.playOnAwake = false;
    }

    // =========================================================
    // JUMP (SFX)
    // =========================================================
    public void PlayJump(float jumpStrength)
    {
        if (!jumpClip) return;

        float pitch = Mathf.Lerp(0.85f, 1.2f, jumpStrength);
        pitch += Random.Range(-0.05f, 0.05f);

        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(jumpClip);
    }

    // =========================================================
    // DASH (SFX)
    // =========================================================
    public void PlayDash(float speedFactor)
    {
        if (!dashClip) return;

        float pitch = Mathf.Lerp(0.85f, 1.3f, speedFactor);
        pitch += Random.Range(-0.05f, 0.05f);

        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(dashClip);
    }

    // =========================================================
    // RUN (LOOP CONTROLLED)
    // =========================================================
    public void StartRun(float speedFactor)
    {
        if (!runClip) return;

        float pitch = Mathf.Lerp(0.85f, 1.25f, speedFactor);
        pitch += Random.Range(-0.05f, 0.05f);

        movementSource.pitch = pitch;
        movementSource.clip = runClip;

        if (!movementSource.isPlaying)
            movementSource.Play();
    }

    public void StopRun()
    {
        if (movementSource.isPlaying && movementSource.clip == runClip)
        {
            movementSource.Stop();
        }
    }
}