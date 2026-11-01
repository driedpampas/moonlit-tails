using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails
{
    /// <summary>
    /// Puts a prop back where it started when it leaves the bar (thrown out of a window, through the floor),
    /// or for every prop at once when someone presses R on the keyboard.
    /// Home is stored relative to the bar, so it follows any rescale.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PropRespawn : MonoBehaviour
    {
        [Tooltip("The bar root. Home is stored in its space.")] public Transform space;
        [SerializeField] Vector3 homePosition;
        [SerializeField] Quaternion homeRotation = Quaternion.identity;

        Rigidbody body;
        XRBaseInteractable grab;

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

        void Update()
        {
            bool held = grab && grab.isSelected;
            bool lost = transform.position.y < space.position.y - 1f || (transform.position - space.position).sqrMagnitude > 30f * 30f;
            bool resetKey = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            if (!held && (lost || resetKey)) SendHome();
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
