using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails
{
    /// <summary>
    /// Puts a prop back where it started when it leaves the bar (thrown out of a window, through the floor).
    /// <see cref="ResetAll"/> sends every prop home at once: the red reset button on the counter, or R on the keyboard.
    /// Home is stored relative to the bar, so it follows any rescale.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PropRespawn : MonoBehaviour
    {
        [Tooltip("The bar root. Home is stored in its space.")] public Transform space;
        [SerializeField] Vector3 homePosition;
        [SerializeField] Quaternion homeRotation = Quaternion.identity;

        public static readonly List<PropRespawn> All = new List<PropRespawn>();
        static int lastResetFrame = -1;

        Rigidbody body;
        XRBaseInteractable grab;
        bool homeNextStep;

        public Vector3 HomePosition => space.TransformPoint(homePosition);

        /// <summary>Stuck to something else right now (a fruit slice on a glass's rim): it moves with that, not on its own.</summary>
        bool Attached => grab && !grab.enabled;

        public void RememberHome()
        {
            if (!space) space = transform.parent;
            homePosition = space.InverseTransformPoint(transform.position);
            homeRotation = Quaternion.Inverse(space.rotation) * transform.rotation;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRBaseInteractable>();
            if (!space) space = transform.parent;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetAll();
            bool held = grab && grab.isSelected;
            bool lost = transform.position.y < space.position.y - 1f || (transform.position - space.position).sqrMagnitude > 30f * 30f;
            if (!held && !Attached && lost) SendHome();
        }

        void FixedUpdate()
        {
            if (!homeNextStep) return;
            homeNextStep = false;
            SendHome();
        }

        /// <summary>Every prop back where it started, even ones in a hand (the hand lets go).</summary>
        public static void ResetAll()
        {
            if (lastResetFrame == Time.frameCount) return;   // R reaches every prop's Update in the same frame
            lastResetFrame = Time.frameCount;
            foreach (var p in All.ToArray()) p.ResetToHome();
        }

        void ResetToHome()
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
            Vector3 p = space.TransformPoint(homePosition);
            Quaternion r = space.rotation * homeRotation;
            // teleport the body too: with interpolation on, moving only the transform gets overwritten
            body.position = p; body.rotation = r;
            transform.SetPositionAndRotation(p, r);
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
