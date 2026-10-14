
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


    [Header("Koşu Sesi")]
    [Tooltip("Koşu pitch çarpanı: büyüdükçe adımlar tizleşir/hızlanır (1 = eski ses).")]
    [Range(0.5f, 1.6f)] public float runPitchMul = 1.2f;
    [Tooltip("Adımların gücü (tok hissi için biraz yüksek tut).")]
    [Range(0f, 1f)] public float runVol = 1f;

    [Header("Attack Woosh")]
    public AudioClip attack1WooshClip;
    public AudioClip attack2WooshClip;
    public AudioClip attack3WooshClip;
    public AudioClip attack4WooshClip;

    [Range(0f, 1f)]
    [Tooltip("Kılıç savurma sesi seviyesi.")]
    public float wooshVol = 0.1f;

    [Header("Woosh Kuyruğu (ses tık diye kesilmesin)")]
    [Tooltip("Klibin sonuna eklenen yumuşak sönme süresi (sn). 0 = eski davranış.")]
    [Range(0f, 0.6f)] public float wooshTail = 0.22f;
    [Tooltip("Klibin son kısmının ne kadarı yumuşakça kısılsın (0-1).")]
    [Range(0f, 0.8f)] public float wooshEndFade = 0.3f;

    [Header("Zıplama Sesi (Resources/JumpSfx)")]
    [Range(0f, 2f)] public float jumpVol = 1f;

    AudioClip[] jumpSet;
    int lastJump = -1;

    AudioClip[] wooshSoft = new AudioClip[5];

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

        jumpSet = Resources.LoadAll<AudioClip>("JumpSfx");

        wooshSoft[1] = SoftEnd(attack1WooshClip);
        wooshSoft[2] = SoftEnd(attack2WooshClip);
        wooshSoft[3] = SoftEnd(attack3WooshClip);
        wooshSoft[4] = SoftEnd(attack4WooshClip);
    }

    // Klibin sonunu yumuşatır ve kısa, sönen bir kuyruk ekler.
    AudioClip SoftEnd(AudioClip src)
    {
        if (!src || wooshTail <= 0f)
            return src;

        try
        {
            int ch = src.channels;
            int n = src.samples;
            float[] d = new float[n * ch];

            if (!src.GetData(d, 0))
                return src;

            int tail = Mathf.RoundToInt(wooshTail * src.frequency);
            int total = n + tail;
            float[] o = new float[total * ch];
            System.Array.Copy(d, o, d.Length);

            // Sondaki kısmı yumuşak kıs.
            int fade = Mathf.Max(1, Mathf.RoundToInt(n * wooshEndFade));

            for (int i = 0; i < fade; i++)
            {
                float g = Mathf.Cos(i / (float)fade * Mathf.PI * 0.5f);
                int f = n - fade + i;

                for (int c = 0; c < ch; c++)
                    o[f * ch + c] *= g * 0.85f + 0.15f * (1f - i / (float)fade);
            }

            // Kuyruk: son ~60 ms döngülenir, üstel olarak söner.
            int seg = Mathf.Clamp(Mathf.RoundToInt(0.06f * src.frequency), 16, n);
            float startAmp = 0.35f;

            for (int i = 0; i < tail; i++)
            {
                float t = i / (float)tail;
                float g = startAmp * Mathf.Pow(1f - t, 2.2f);
                int srcI = n - seg + (i % seg);

                for (int c = 0; c < ch; c++)
                {
                    float v = d[srcI * ch + c];
                    // döngü dikişini yumuşat
                    float w = Mathf.Sin((i % seg) / (float)seg * Mathf.PI);
                    o[(n + i) * ch + c] = v * g * w;
                }
            }

            AudioClip r = AudioClip.Create(src.name + "_soft", total, ch, src.frequency, false);
            r.SetData(o, 0);
            return r;
        }
        catch
        {
            return src;
        }
    }

    // =========================================================
    // JUMP (SFX)
    // =========================================================

    public void PlayJump(float jumpStrength)
    {
        // Resources/JumpSfx içindeki klipler varsa rastgele biri çalar.
        if (jumpSet != null && jumpSet.Length > 0)
        {
            int i = Random.Range(0, jumpSet.Length);

            if (jumpSet.Length > 1 && i == lastJump)
                i = (i + 1 + Random.Range(0, jumpSet.Length - 1)) % jumpSet.Length;

            lastJump = i;

            sfxSource.pitch =
                Mathf.Lerp(0.95f, 1.1f, jumpStrength) +
                Random.Range(-0.05f, 0.05f);

            sfxSource.PlayOneShot(jumpSet[i], jumpVol);
            return;
        }

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

        if (attackStep >= 1 && attackStep <= 4 && wooshSoft[attackStep])
            clip = wooshSoft[attackStep];

        // Her saldırının kendi sesi var.
        sfxSource.pitch = 1f;

        sfxSource.PlayOneShot(
            clip,
            wooshVol
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

        movementSource.pitch = pitch * runPitchMul;
        movementSource.volume = runVol;
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

