
using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource movementSource; // RUN
    public AudioSource sfxSource;      // JUMP / DASH / ATTACK

    [Header("Clips")]
    public AudioClip jumpClip;
    public AudioClip dashClip;
    public AudioClip runClip;

    [Header("Attack Woosh")]
    public AudioClip attack1WooshClip;
    public AudioClip attack2WooshClip;
    public AudioClip attack3WooshClip;
    public AudioClip attack4WooshClip;

    void Awake()
    {
        // RUN source
        if (!movementSource)
            movementSource = GetComponent<AudioSource>();

        // SFX source
        if (!sfxSource)
        {
            sfxSource =
                gameObject.AddComponent<AudioSource>();
        }

        sfxSource.playOnAwake = false;
    }

    // =========================================================
    // JUMP (SFX)
    // =========================================================

    public void PlayJump(float jumpStrength)
    {
        if (!jumpClip)
            return;

        float pitch =
            Mathf.Lerp(
                0.85f,
                1.2f,
                jumpStrength
            );

        pitch +=
            Random.Range(
                -0.05f,
                0.05f
            );

        sfxSource.pitch = pitch;

        sfxSource.PlayOneShot(
            jumpClip
        );
    }

    // =========================================================
    // DASH (SFX)
    // =========================================================

    public void PlayDash(float speedFactor)
    {
        if (!dashClip)
            return;

        float pitch =
            Mathf.Lerp(
                0.85f,
                1.3f,
                speedFactor
            );

        pitch +=
            Random.Range(
                -0.05f,
                0.05f
            );

        sfxSource.pitch = pitch;

        sfxSource.PlayOneShot(
            dashClip
        );
    }

    // =========================================================
    // ATTACK WOOSH (SFX)
    // =========================================================

    public void PlayAttackWoosh(int attackStep)
    {
        AudioClip clip = null;

        switch (attackStep)
        {
            case 1:
                clip = attack1WooshClip;
                break;

            case 2:
                clip = attack2WooshClip;
                break;

            case 3:
                clip = attack3WooshClip;
                break;

            case 4:
                clip = attack4WooshClip;
                break;
        }

        if (!clip)
            return;

        // Her saldırının kendi sesi var.
        sfxSource.pitch = 1f;

        sfxSource.PlayOneShot(
            clip
        );
    }

    // =========================================================
    // RUN (LOOP CONTROLLED)
    // =========================================================

    public void StartRun(float speedFactor)
    {
        if (!runClip)
            return;

        float pitch =
            Mathf.Lerp(
                0.85f,
                1.25f,
                speedFactor
            );

        pitch +=
            Random.Range(
                -0.05f,
                0.05f
            );

        movementSource.pitch = pitch;
        movementSource.clip = runClip;

        if (!movementSource.isPlaying)
            movementSource.Play();
    }

    public void StopRun()
    {
        if (movementSource.isPlaying &&
            movementSource.clip == runClip)
        {
            movementSource.Stop();
        }
    }
}

