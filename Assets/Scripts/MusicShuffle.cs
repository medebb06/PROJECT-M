using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 'Theme Music' kaynağına eklenir: Resources/Music içindeki parçalar + kaynağın kendi klibi
/// rastgele sırayla (tekrarsız, karışık torba) çalınır. Biri bitince sıradaki başlar.
/// </summary>
public class MusicShuffle : MonoBehaviour
{
    private AudioSource src;
    private readonly List<AudioClip> bag = new List<AudioClip>();
    private List<AudioClip> all;
    private AudioClip last;
    private float wait;

    private void Awake()
    {
        src = GetComponent<AudioSource>();
        all = new List<AudioClip>(Resources.LoadAll<AudioClip>("Music"));

        if (src != null && src.clip != null && !all.Contains(src.clip))
            all.Add(src.clip);

        if (src != null)
        {
            src.loop = false;
            src.playOnAwake = false;
        }
    }

    private AudioClip Next()
    {
        if (all.Count == 0) return null;

        if (bag.Count == 0)
        {
            bag.AddRange(all);

            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                AudioClip t = bag[i]; bag[i] = bag[j]; bag[j] = t;
            }

            // Yeni torbanın ilki, bir önceki parçayla aynı olmasın.
            if (bag.Count > 1 && bag[bag.Count - 1] == last)
            {
                AudioClip t = bag[0]; bag[0] = bag[bag.Count - 1]; bag[bag.Count - 1] = t;
            }
        }

        AudioClip c = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        last = c;
        return c;
    }

    private void Update()
    {
        if (src == null || !src.enabled || !Application.isPlaying) return;
        if (src.isPlaying) return;

        wait -= Time.unscaledDeltaTime;
        if (wait > 0f) return;

        AudioClip c = Next();
        if (c == null) return;

        src.clip = c;
        src.loop = false;
        src.Play();
        wait = 1.5f;   // iki parça arası kısa nefes
    }
}
