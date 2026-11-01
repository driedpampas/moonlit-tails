using UnityEngine;

namespace VRCharacters
{
    /// <summary>Sphere or capsule that keeps spring chains (coat, tail, hair) out of the body.</summary>
    public class SpringBoneCollider : MonoBehaviour
    {
        public enum Shape { Sphere, Capsule }
        public Shape shape = Shape.Sphere;
        [Min(0f)] public float radius = 0.05f;
        [Tooltip("Centre (sphere) or start point (capsule) in local space.")]
        public Vector3 offset = Vector3.zero;
        [Tooltip("Capsule end point in local space.")]
        public Vector3 endOffset = new Vector3(0f, 0.1f, 0f);

        public void Collide(ref Vector3 p, float jointRadius)
        {
            Vector3 a = transform.TransformPoint(offset);
            float r = radius * Mathf.Abs(transform.lossyScale.x) + jointRadius;
            Vector3 c = a;
            if (shape == Shape.Capsule)
            {
                Vector3 b = transform.TransformPoint(endOffset);
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                c = a + ab * t;
            }
            Vector3 d = p - c;
            float dist = d.magnitude;
            if (dist < r && dist > 1e-6f) p = c + d * (r / dist);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            float r = radius * Mathf.Abs(transform.lossyScale.x);
            Vector3 a = transform.TransformPoint(offset);
            Gizmos.DrawWireSphere(a, r);
            if (shape == Shape.Capsule)
            {
                Vector3 b = transform.TransformPoint(endOffset);
                Gizmos.DrawWireSphere(b, r);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
