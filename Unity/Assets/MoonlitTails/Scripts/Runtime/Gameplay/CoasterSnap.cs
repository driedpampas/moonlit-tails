using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// Makes the serve coaster catch glasses. Let go of a glass close to or just above the coaster, and it stands
    /// up straight in the middle of the coaster and stays there until someone picks it up again. Without this, a glass
    /// let go at an angle (easy with the XR Interaction Simulator) tips over or slides off.
    /// </summary>
    public class CoasterSnap : MonoBehaviour
    {
        public Transform spot;
        [Tooltip("How far (m, sideways) from the middle of the coaster a released glass still snaps onto it.")]
        public float snapRadius = 0.13f;
        [Tooltip("How high above the coaster (m) a released glass still snaps onto it.")]
        public float snapHeight = 0.3f;

        /// <summary>The glass standing on the coaster, or null.</summary>
        public DrinkContainer Glass { get; private set; }
        Rigidbody glassBody;
        RigidbodyConstraints constraintsBefore;

        const RigidbodyConstraints Pinned =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        public void Init(Transform serveSpot)
        {
            spot = serveSpot;
            foreach (var c in DrinkContainer.All)
            {
                if (!c.IsGlass || !c.TryGetComponent<XRGrabInteractable>(out var grab)) continue;
                var glass = c;
                grab.selectEntered.AddListener((SelectEnterEventArgs _) => { if (glass == Glass) Free(); });
                grab.selectExited.AddListener((SelectExitEventArgs args) =>
                {
                    // a cancelled release (e.g. the reset button taking the glass away) isn't the player putting it down
                    if (!args.isCanceled && InReach(glass)) StartCoroutine(SnapAfterRelease(glass));
                });
            }
        }

        /// <summary>Is the glass close enough to the coaster to snap onto it?</summary>
        public bool InReach(DrinkContainer glass)
        {
            Vector3 p = spot.position;
            float low = Mathf.Min(glass.BottomWorld.y, glass.CenterWorld.y) - p.y;
            if (low < -0.05f || low > snapHeight) return false;
            Vector3 d = glass.CenterWorld - p; d.y = 0;
            return d.magnitude < snapRadius;
        }

        IEnumerator SnapAfterRelease(DrinkContainer glass)
        {
            // XRI throws the glass in LateUpdate and HandGrab in the next FixedUpdate: snap after both, so they can't
            // knock it off again
            yield return new WaitForFixedUpdate();
            if (glass && !glass.IsHeld) Snap(glass);
        }

        /// <summary>Stand the glass upright in the middle of the coaster. False if another glass is already there.</summary>
        public bool Snap(DrinkContainer glass)
        {
            if (Glass && Glass != glass)
            {
                if (OnCoaster(Glass)) return false;
                Free();
            }

            var t = glass.transform;
            Quaternion r = Quaternion.FromToRotation(glass.UpWorld, Vector3.up) * t.rotation;
            t.rotation = r;
            Vector3 c = glass.CenterWorld, p = spot.position;
            Vector3 pos = t.position + new Vector3(p.x - c.x, p.y - glass.BottomWorld.y + 0.001f, p.z - c.z);
            t.position = pos;

            if (t.TryGetComponent(out Rigidbody body))
            {
                // teleport the body too: with interpolation on, moving only the transform gets overwritten
                body.position = pos; body.rotation = r;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                // pinned sideways and upright so it can't be knocked over; it can still be lifted off
                if (Glass != glass) constraintsBefore = body.constraints;
                body.constraints = Pinned;
            }
            Glass = glass;
            glassBody = body;
            Physics.SyncTransforms();
            return true;
        }

        bool OnCoaster(DrinkContainer glass)
        {
            if (!glass || glass.IsHeld) return false;
            Vector3 d = glass.CenterWorld - spot.position; d.y = 0;
            return d.magnitude < 0.03f && Mathf.Abs(glass.BottomWorld.y - spot.position.y) < 0.05f;
        }

        void Free()
        {
            if (glassBody) glassBody.constraints = constraintsBefore;
            Glass = null;
            glassBody = null;
        }

        void FixedUpdate()
        {
            // picked up, sent home by a reset, or returned after serving: let it go
            if (Glass != null && !OnCoaster(Glass)) Free();
        }
    }
}
