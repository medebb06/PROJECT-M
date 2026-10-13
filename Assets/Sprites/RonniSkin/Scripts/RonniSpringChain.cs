using UnityEngine;

namespace RonniSkin
{
    /// Secondary motion for a bone chain (ponytail, coat tails). Runs after RonniSkinAnimator,
    /// adds a lagging rotation so the chain trails behind body movement.
    [DefaultExecutionOrder(100)]
    public class RonniSpringChain : MonoBehaviour
    {
        public Transform[] chain;            // joints j0..jn (last one is the end point)
        public float stiffness = 60f;
        public float damping = 8f;
        [Range(0, 1)] public float amount = 0.8f;
        public float maxAngle = 25f;

        Vector3[] q, vel; bool init;

        void LateUpdate()
        {
            if (chain == null || chain.Length < 2) return;
            int n = chain.Length - 1;
            if (!init) { q = new Vector3[n]; vel = new Vector3[n]; for (int i = 0; i < n; i++) q[i] = chain[i + 1].position; init = true; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            for (int i = 0; i < n; i++)
            {
                Vector3 j = chain[i].position, p = chain[i + 1].position;
                vel[i] += (p - q[i]) * stiffness * dt;
                vel[i] *= Mathf.Exp(-damping * dt);
                q[i] += vel[i] * dt;
                float ang = Vector2.SignedAngle(p - j, q[i] - j);
                if (chain[i].lossyScale.x < 0) ang = -ang;
                ang = Mathf.Clamp(ang * amount, -maxAngle, maxAngle);
                chain[i].localRotation *= Quaternion.Euler(0, 0, ang);
            }
        }
    }
}
