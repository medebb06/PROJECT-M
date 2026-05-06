using System.Collections;
using UnityEngine;

public class HitStop : MonoBehaviour
{
    public static HitStop instance;

    void Awake()
    {
        instance = this;
    }

    public void Stop(float duration)
    {
        StartCoroutine(Freeze(duration));
    }

    IEnumerator Freeze(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}