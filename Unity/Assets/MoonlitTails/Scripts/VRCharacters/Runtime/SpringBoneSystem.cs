using System.Collections.Generic;
using UnityEngine;

namespace VRCharacters
{
    /// <summary>
    /// Lightweight spring-bone solver for hair, ears, tail and coat chains (Quest friendly, no allocations per frame).
    /// Runs after the Animator in LateUpdate: whatever pose the animation or a designer sets on a chain bone
    /// becomes the spring's rest target, so tails and ears stay posable and animated clips still play through.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class SpringBoneSystem : MonoBehaviour
    {
        [System.Serializable]
        public class Chain
        {
            public string label = "Chain";
            [Tooltip("First bone of the chain, e.g. Tail_01. The solver walks down the first child of each bone.")]
            public Transform root;
            [Range(0f, 1f), Tooltip("How strongly the chain returns to the animated/posed shape.")]
            public float stiffness = 0.35f;
            [Range(0f, 1f), Tooltip("Velocity damping. Higher = calmer.")]
            public float drag = 0.2f;
            [Tooltip("Extra acceleration in world space (m/s^2).")]
            public Vector3 gravity = new Vector3(0f, -0.5f, 0f);
            [Range(0f, 2f)] public float windInfluence = 1f;
            [Min(0f), Tooltip("Collision radius of every joint in this chain (metres).")]
            public float radius = 0.012f;
            [Range(0f, 1f), Tooltip("0 = pure animation/pose, 1 = full physics on top of the pose.")]
            public float physicsWeight = 1f;
            [Min(0.001f), Tooltip("Length of the virtual tip after the last bone (metres).")]
            public float tipLength = 0.03f;

            [System.NonSerialized] public Node[] nodes;
        }

        public class Node
        {
            public Transform bone;
            public Vector3 localAxis;       // direction bone -> child, in the bone's local space
            public float length;
            public Quaternion poseLocal;    // last animated / posed local rotation
            public Quaternion writtenLocal; // rotation written by the solver last frame
            public Vector3 prevTip, currTip;
        }

        public List<Chain> chains = new List<Chain>();
        public List<SpringBoneCollider> colliders = new List<SpringBoneCollider>();

        [Header("Wind")]
        [Tooltip("Optional. When set, uses the WindZone's direction, Main, Turbulence and Pulse values.")]
        public WindZone windZone;
        public Vector3 windDirection = new Vector3(1f, 0f, 0.35f);
        [Min(0f), Tooltip("Wind acceleration (m/s^2) used when no WindZone is assigned.")]
        public float windStrength = 0f;
        [Range(0f, 1f)] public float windGust = 0.5f;
        [Min(0f)] public float windGustSpeed = 0.7f;

        [Header("Simulation")]
        [Tooltip("Reference rate the stiffness/drag values are tuned for.")]
        public float referenceRate = 60f;
        [Tooltip("Large frame hitches (teleports, loading) reset the chains instead of exploding.")]
        public float resetDistance = 0.5f;

        Vector3 lastRootPos;
        bool built;

        void OnEnable()
        {
            Build();
        }

        /// <summary>(Re)collect chain bones. Call after changing chain roots at runtime.</summary>
        public void Build()
        {
            foreach (var c in chains)
            {
                var list = new List<Node>();
                var t = c.root;
                while (t != null && !t.name.EndsWith("_end"))
                {
                    Transform child = t.childCount > 0 ? t.GetChild(0) : null;
                    var n = new Node { bone = t };
                    if (child != null)
                    {
                        Vector3 d = child.localPosition;
                        n.length = d.magnitude;
                        n.localAxis = n.length > 1e-6f ? d / n.length : Vector3.up;
                    }
                    else
                    {
                        // last bone: continue in the parent bone's direction
                        Vector3 dirWorld = t.parent != null ? (t.position - t.parent.position) : t.up;
                        if (dirWorld.sqrMagnitude < 1e-10f) dirWorld = t.up;
                        n.localAxis = Quaternion.Inverse(t.rotation) * dirWorld.normalized;
                        n.length = c.tipLength * t.lossyScale.x;
                    }
                    n.poseLocal = t.localRotation;
                    n.writtenLocal = t.localRotation;
                    list.Add(n);
                    t = child;
                }
                c.nodes = list.ToArray();
            }
            built = true;
            ResetSimulation();
        }

        /// <summary>Snap all chains to the current pose (use after teleports).</summary>
        public void ResetSimulation()
        {
            if (!built) return;
            foreach (var c in chains)
            {
                if (c.nodes == null) continue;
                foreach (var n in c.nodes)
                {
                    Vector3 tip = n.bone.position + n.bone.rotation * n.localAxis * n.length;
                    n.prevTip = tip; n.currTip = tip;
                }
            }
            lastRootPos = transform.position;
        }

        Vector3 Wind(float time)
        {
            if (windZone != null)
            {
                float pulse = 1f + Mathf.Sin(time * Mathf.PI * 2f * Mathf.Max(0.01f, windZone.windPulseFrequency)) * windZone.windPulseMagnitude;
                float turb = (Mathf.PerlinNoise(time * 0.9f, 0.37f) - 0.5f) * 2f * windZone.windTurbulence;
                return windZone.transform.forward * (windZone.windMain * pulse + turb);
            }
            if (windStrength <= 0f) return Vector3.zero;
            float g = 1f + (Mathf.PerlinNoise(time * windGustSpeed, 0.11f) - 0.5f) * 2f * windGust;
            return windDirection.normalized * windStrength * g;
        }

        void LateUpdate()
        {
            if (!built) Build();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if ((transform.position - lastRootPos).sqrMagnitude > resetDistance * resetDistance) ResetSimulation();
            lastRootPos = transform.position;

            float steps = dt * referenceRate;
            Vector3 wind = Wind(Time.time);

            for (int ci = 0; ci < chains.Count; ci++)
            {
                var c = chains[ci];
                if (c.nodes == null || c.root == null) continue;
                float keep = Mathf.Pow(1f - c.drag, steps);
                float pull = 1f - Mathf.Pow(1f - c.stiffness, steps);
                Vector3 accel = (c.gravity + wind * c.windInfluence) * dt * dt;

                // 1) pick up the animated / posed rotation (only if something else changed the bone since our write)
                for (int i = 0; i < c.nodes.Length; i++)
                {
                    var n = c.nodes[i];
                    if (n.bone.localRotation != n.writtenLocal) n.poseLocal = n.bone.localRotation;
                }

                // 2) simulate root -> tip
                for (int i = 0; i < c.nodes.Length; i++)
                {
                    var n = c.nodes[i];
                    n.bone.localRotation = n.poseLocal;
                    Vector3 origin = n.bone.position;
                    Quaternion poseWorld = n.bone.rotation;
                    Vector3 restTip = origin + poseWorld * n.localAxis * n.length;

                    Vector3 next = n.currTip + (n.currTip - n.prevTip) * keep + (restTip - n.currTip) * pull + accel;
                    Vector3 dir = next - origin;
                    if (dir.sqrMagnitude < 1e-12f) dir = restTip - origin;
                    next = origin + dir.normalized * n.length;

                    for (int k = 0; k < colliders.Count; k++)
                        if (colliders[k] != null && colliders[k].isActiveAndEnabled) colliders[k].Collide(ref next, c.radius);
                    next = origin + (next - origin).normalized * n.length;

                    n.prevTip = n.currTip;
                    n.currTip = next;

                    Quaternion aim = Quaternion.FromToRotation(poseWorld * n.localAxis, next - origin) * poseWorld;
                    n.bone.rotation = Quaternion.Slerp(poseWorld, aim, c.physicsWeight);
                    n.writtenLocal = n.bone.localRotation;
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (chains == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            foreach (var c in chains)
            {
                if (c.nodes == null) continue;
                foreach (var n in c.nodes)
                {
                    if (n.bone == null) continue;
                    Gizmos.DrawLine(n.bone.position, n.currTip);
                    Gizmos.DrawWireSphere(n.currTip, c.radius);
                }
            }
        }
    }
}
