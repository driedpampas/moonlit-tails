using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// A cap for a mixing vessel that needs one (<see cref="DrinkContainer.NeedsCap"/>), like the Shake Mixer's.
    /// Let go of it near the vessel's mouth and it snaps on, upright. Grab it to take it off again.
    /// While it's on, the vessel can be shaken, and nothing pours in or out.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ContainerCap : MonoBehaviour
    {
        [Tooltip("How close to the vessel's mouth (m) it has to be let go to snap on.")]
        public float reach = 0.12f;
        [Tooltip("How far (m) it sits down into the mouth.")]
        public float seatDepth = 0.015f;

        XRGrabInteractable grab;
        Rigidbody body;
        Collider[] colliders;
        PropRespawn home;
        Transform originalParent;
        Vector3 upAxis;
        DrinkContainer vessel;   // the vessel it's on, or null

        void Awake()
        {
            originalParent = transform.parent;
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();   // the grab component requires one
            colliders = GetComponentsInChildren<Collider>();
            home = GetComponent<PropRespawn>();
            upAxis = GameVisuals.LocalUpAxis(transform);
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        void OnDestroy()
        {
            if (!grab) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        void OnGrabbed(SelectEnterEventArgs _) { if (vessel) TakeOff(); }

        void OnReleased(SelectExitEventArgs args)
        {
            if (!args.isCanceled) StartCoroutine(SnapNextFrame());
        }

        // XRI restores the rigidbody after the release event, so snap a frame later
        IEnumerator SnapNextFrame()
        {
            yield return null;
            var v = DrinkContainer.FindNeedingCap(GameVisuals.WorldBounds(transform).center, reach);
            if (v) PutOn(v);
            else body.isKinematic = false;   // it was grabbed off a vessel while kinematic, and XRI restored that
        }

        /// <summary>Seats the cap on the vessel's mouth. It moves with the vessel until it's taken off.</summary>
        public void PutOn(DrinkContainer v)
        {
            vessel = v;
            body.isKinematic = true;
            IgnoreVessel(true);
            transform.SetParent(v.transform, true);
            transform.rotation = Quaternion.FromToRotation(transform.TransformDirection(upAxis), v.UpWorld) * transform.rotation;
            var b = GameVisuals.WorldBounds(transform);
            Vector3 bottom = b.center - v.UpWorld * Vector3.Dot(b.extents, Abs(v.UpWorld));
            transform.position += v.OpeningWorld - v.UpWorld * seatDepth - bottom;
            if (home) home.Attached = true;
            v.SetCap(this);
        }

        /// <summary>Takes the cap off its vessel.</summary>
        public void TakeOff()
        {
            var v = vessel;
            if (!v) return;
            vessel = null;
            transform.SetParent(originalParent, true);
            IgnoreVessel(false, v);
            if (!grab.isSelected) body.isKinematic = false;
            if (home) home.Attached = false;
            v.SetCap(null);
        }

        void IgnoreVessel(bool ignore, DrinkContainer v = null)
        {
            foreach (var theirs in (v ? v : vessel).GetComponentsInChildren<Collider>())
                foreach (var mine in colliders)
                    if (mine != theirs) Physics.IgnoreCollision(mine, theirs, ignore);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
