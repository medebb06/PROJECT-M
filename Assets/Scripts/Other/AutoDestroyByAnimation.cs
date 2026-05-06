using UnityEngine;

public class AutoDestroyByAnimation : MonoBehaviour
{
    void Start()
    {
        Animator anim = GetComponent<Animator>();
        float time = anim.GetCurrentAnimatorStateInfo(0).length;
        Destroy(gameObject, time);
    }
}