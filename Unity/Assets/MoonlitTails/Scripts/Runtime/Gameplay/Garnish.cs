using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>A fruit slice. Let go of it near a glass's rim and it sticks there, giving the drink its colour.</summary>
    public class Garnish : MonoBehaviour
    {
        public string color;
        public float reach = 0.07f;

        XRGrabInteractable grab;
        Rigidbody body;
        Collider[] colliders;
        PropRespawn home;
        DrinkContainer glass;
        Transform originalParent;

        public void Init(string garnishColor)
        {
            color = garnishColor;
            originalParent = transform.parent;
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            home = GetComponent<PropRespawn>();
            if (grab) grab.selectExited.AddListener(OnReleased);
        }

        void OnDestroy() { if (grab) grab.selectExited.RemoveListener(OnReleased); }

        void OnReleased(SelectExitEventArgs args)
        {
            if (args.isCanceled || glass) return;
            StartCoroutine(StickNextFrame());
        }

        // XRI restores the rigidbody after the release event, so attach a frame later
        IEnumerator StickNextFrame()
        {
            yield return null;
            var g = DrinkContainer.FindGlassNear(transform.position, reach);
            if (g) g.AttachGarnish(this);
        }

        public void AttachTo(DrinkContainer target, Vector3 rimPoint)
        {
            glass = target;
            if (body) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var c in colliders) c.enabled = false;
            if (grab) grab.enabled = false;
            transform.SetParent(target.transform, true);
            transform.position = rimPoint;
        }

        /// <summary>Take it off the glass. With <paramref name="sendHome"/> it goes back to the garnish tray.</summary>
        public void Detach(bool sendHome)
        {
            var from = glass;
            glass = null;
            transform.SetParent(originalParent, true);
            foreach (var c in colliders) c.enabled = true;
            if (body) { body.isKinematic = false; body.detectCollisions = true; }
            if (grab) grab.enabled = true;
            if (from) from.ForgetGarnish(this);
            if (sendHome && home) home.SendHome();
        }
    }
}
