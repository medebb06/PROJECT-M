using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("Clips")]
    public AudioClip jumpClip;

    [Header("Audio Source")]
    public AudioSource source;

    void Awake()
    {
        if (!source)
            source = GetComponent<AudioSource>();
    }

    public void PlayDash(float speedFactor)
    {
        if (!jumpClip) return; // (istersen ayrı dash clip koyarsın)

        float pitch = Mathf.Lerp(0.85f, 1.4f, speedFactor);
        pitch += Random.Range(-0.05f, 0.05f);

        pitch = Mathf.Clamp(pitch, 0.75f, 1.5f);

        source.pitch = pitch;
        source.PlayOneShot(jumpClip); // şimdilik aynı clip, sonra dashClip yaparız
        source.pitch = 1f;
    }
    public void PlayJump(float jumpStrength)
    {
        if (!jumpClip) return;

        float pitch = Mathf.Lerp(0.9f, 1.15f, jumpStrength);
        pitch += Random.Range(-0.05f, 0.05f);
        pitch = Mathf.Clamp(pitch, 0.85f, 1.2f);
        pitch = Mathf.SmoothStep(0.85f, 1.2f, jumpStrength);

        source.pitch = pitch;
        source.clip = jumpClip;
        source.Play();
    }
}