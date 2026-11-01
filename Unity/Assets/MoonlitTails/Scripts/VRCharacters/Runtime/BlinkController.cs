using UnityEngine;

namespace VRCharacters
{
    /// <summary>
    /// Two-state blink: the "Blink" blendshape is snapped to 0 (open) or 100 (closed: eye whites + irises hidden,
    /// lash dropped to the closed line). No in-between frames, so it reads like a 2D cartoon blink.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public class BlinkController : MonoBehaviour
    {
        public SkinnedMeshRenderer target;
        public string blendShapeName = "Blink";
        public bool autoBlink = true;
        [Tooltip("Random time between blinks (seconds).")]
        public Vector2 openSeconds = new Vector2(2.5f, 6f);
        [Tooltip("How long the closed frame stays on screen (seconds).")]
        public float closedSeconds = 0.12f;
        [Range(0f, 1f), Tooltip("Chance of a quick double blink.")]
        public float doubleBlinkChance = 0.15f;

        int index = -1;
        float nextBlink, closedUntil, secondBlinkAt = -1f;
        bool forced;
        bool forcedValue;

        void Awake()
        {
            if (target == null) target = GetComponentInChildren<SkinnedMeshRenderer>();
            if (target != null && target.sharedMesh != null) index = target.sharedMesh.GetBlendShapeIndex(blendShapeName);
            if (index < 0) Debug.LogWarning($"BlinkController: blendshape '{blendShapeName}' not found", this);
            Schedule();
        }

        void Schedule() { nextBlink = Time.time + Random.Range(openSeconds.x, openSeconds.y); }

        /// <summary>Hold the eyes closed (true) or open (false), e.g. for sleeping or emotes.</summary>
        public void ForceClosed(bool closed) { forced = true; forcedValue = closed; }
        /// <summary>Return to automatic blinking.</summary>
        public void ReleaseForce() { forced = false; }

        // LateUpdate: runs after the Animator, so clips can never overwrite the blink frame
        void LateUpdate()
        {
            if (index < 0 || target == null) return;
            bool closed;
            if (forced) closed = forcedValue;
            else
            {
                float t = Time.time;
                if (autoBlink && t >= nextBlink)
                {
                    closedUntil = t + closedSeconds;
                    secondBlinkAt = Random.value < doubleBlinkChance ? t + closedSeconds * 2.2f : -1f;
                    Schedule();
                }
                if (secondBlinkAt > 0f && t >= secondBlinkAt) { closedUntil = t + closedSeconds; secondBlinkAt = -1f; }
                closed = t < closedUntil;
            }
            target.SetBlendShapeWeight(index, closed ? 100f : 0f);
        }
    }
}
