using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails
{
    /// <summary>
    /// Where a prop lives, so it can be put back: when it leaves the bar (thrown out of a window, through the floor),
    /// when the red reset button is pressed, or with R on the keyboard. Home is stored relative to the bar, so it
    /// follows any rescale. A prop with no parent stores it in world space.
    ///
    /// This holds the data only. The Drink Game checks every prop once per frame (<see cref="CheckLost"/>) instead of
    /// each prop running its own Update.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PropRespawn : MonoBehaviour
    {
        [Tooltip("The bar root. Home is stored in its space (empty = the prop's parent, or the world).")] public Transform space;
        [SerializeField] Vector3 homePosition;
        [SerializeField] Quaternion homeRotation = Quaternion.identity;

        public static readonly List<PropRespawn> All = new List<PropRespawn>();
        static int lastResetFrame = -1;

        Rigidbody body;
        XRBaseInteractable grab;
        bool homeNextStep;

        public Vector3 HomePosition => ToWorld(homePosition);

        /// <summary>Stuck to something else right now (a fruit slice on a glass's rim): it moves with that, not on its own.</summary>
        bool Attached => grab && !grab.enabled;

        Vector3 ToWorld(Vector3 p) => space ? space.TransformPoint(p) : p;
        Quaternion ToWorld(Quaternion r) => space ? space.rotation * r : r;

        public void RememberHome()
        {
            if (!space) space = transform.parent;
            homePosition = space ? space.InverseTransformPoint(transform.position) : transform.position;
            homeRotation = space ? Quaternion.Inverse(space.rotation) * transform.rotation : transform.rotation;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRBaseInteractable>();
            if (!space) space = transform.parent;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        /// <summary>Called once per frame by the Drink Game: sends the prop home if it has left the bar.</summary>
        public void CheckLost()
        {
            if ((grab && grab.isSelected) || Attached) return;
            Vector3 origin = space ? space.position : Vector3.zero;
            Vector3 p = transform.position;
            if (p.y < origin.y - 1f || (p - origin).sqrMagnitude > 30f * 30f) SendHome();
        }

        /// <summary>Called once per physics step by the Drink Game: finishes a reset that had to wait for a hand to let go.</summary>
        public void ApplyPendingHome()
        {
            if (!homeNextStep) return;
            homeNextStep = false;
            SendHome();
        }

        /// <summary>Every prop back where it started, even ones in a hand (the hand lets go).</summary>
        public static void ResetAll()
        {
            if (lastResetFrame == Time.frameCount) return;   // a button and a key can both fire in one frame
            lastResetFrame = Time.frameCount;
            foreach (var p in All.ToArray()) p.ResetToHome();
        }

        /// <summary>Back home. If something is holding it (a hand, a socket), it lets go first.</summary>
        public void ResetToHome()
        {
            if (Attached) return;
            if (grab && grab.isSelected && grab.interactionManager)
            {
                // let go first, then move it on the next physics step: XRI applies the hand's throw speed in
                // LateUpdate after letting go, which would fling it away from home
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
                homeNextStep = true;
                return;
            }
            SendHome();
        }

        public void SendHome()
        {
            Vector3 p = ToWorld(homePosition);
            Quaternion r = ToWorld(homeRotation);
            // teleport the body too: with interpolation on, moving only the transform gets overwritten
            if (body) { body.position = p; body.rotation = r; }
            transform.SetPositionAndRotation(p, r);
            if (!body || body.isKinematic) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
