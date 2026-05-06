using UnityEngine;

public class DustFX : MonoBehaviour
{
    public float lifeTime = 0.6f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }
}