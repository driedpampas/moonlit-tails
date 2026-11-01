using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace MoonlitTails
{
    /// <summary>
    /// Natural grabbing for tracked hands, replacing the default pinch-only select:
    /// - close your hand around a prop (fingers curl) and it's held in your palm, open your hand to let go
    /// - pinch small things (garnish, cherries) between thumb and index to hold them at the fingertips
    /// - on release the prop flies with your hand's own speed and spin from the last moment before letting go
    /// Thresholds live in BarTuning (Hands section).
    /// </summary>
    [DefaultExecutionOrder(-200)]   // decide grab/release before the XR Interaction Manager reads the select input
    public class HandGrab : MonoBehaviour
    {
        public BarTuning tuning;
        public NearFarInteractor interactor;
        public Handedness handedness = Handedness.Left;
        public XROrigin origin;

        [Header("Live readout (for tuning thresholds in the headset)")]
        [SerializeField, Range(0, 1)] float fingerCurl;
        [SerializeField] float pinchDistance;
        [SerializeField] bool holding;
        [SerializeField] bool holdingWithPinch;

        Transform grabPoint;
        XRHandSubsystem hands;
        float lastTrackedTime;
        readonly List<(float t, Vector3 p, Quaternion r)> history = new List<(float, Vector3, Quaternion)>();
        readonly List<(Rigidbody body, Vector3 velocity, Vector3 spin)> pendingThrows = new List<(Rigidbody, Vector3, Vector3)>();
        static readonly List<XRHandSubsystem> found = new List<XRHandSubsystem>();
        static readonly XRHandFingerID[] Fingers = { XRHandFingerID.Index, XRHandFingerID.Middle, XRHandFingerID.Ring, XRHandFingerID.Little };

        void Awake()
        {
            if (!interactor) interactor = GetComponent<NearFarInteractor>();
            if (!origin) origin = GetComponentInParent<XROrigin>();
            grabPoint = new GameObject(handedness + " Hand Grab Point").transform;
            grabPoint.SetParent(origin ? origin.transform : transform.parent, false);
        }

        void OnEnable() { if (interactor) interactor.selectExited.AddListener(OnReleased); }
        void OnDisable()
        {
            if (interactor) interactor.selectExited.RemoveListener(OnReleased);
            SetHolding(false);
        }

        void Update()
        {
            if (!tuning || !interactor) return;
            TakeOverInteractor();

            if (hands == null || !hands.running) hands = FindHands();
            if (hands == null) { SetHolding(false); return; }
            var hand = handedness == Handedness.Left ? hands.leftHand : hands.rightHand;
            if (!hand.isTracked)
            {
                if (Time.time - lastTrackedTime > 0.3f) SetHolding(false);   // hand lost for a while: drop it
                return;
            }
            lastTrackedTime = Time.time;

            Transform space = origin && origin.CameraFloorOffsetObject ? origin.CameraFloorOffsetObject.transform : origin.transform;
            if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmLocal) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbLocal) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexLocal)) return;
            Pose palm = palmLocal.GetTransformedBy(space);
            Vector3 thumb = thumbLocal.GetTransformedBy(space).position;
            Vector3 index = indexLocal.GetTransformedBy(space).position;

            // where a held glass sits: just in front of the palm (+Y is the back of the hand, +Z points to the fingers)
            Vector3 palmPoint = palm.position + palm.rotation * new Vector3(0f, -tuning.palmGrabOffset, 0.02f);
            Vector3 pinchPoint = (thumb + index) * 0.5f;

            fingerCurl = AverageCurl(hand);
            pinchDistance = Vector3.Distance(thumb, index);

            // thresholds with hysteresis: harder to start a grab than to keep it
            bool fist = tuning.grabWithWholeHand && fingerCurl > (holding ? tuning.releaseCurl : tuning.grabCurl);
            bool pinch = tuning.allowPinchGrab && pinchDistance < (holding ? tuning.pinchReleaseDistance : tuning.pinchDistance);
            bool want = fist || pinch;

            if (!holding)
            {
                // reach with the palm, unless the fingers are closing into a pinch
                holdingWithPinch = pinch && !fist;
                bool aimingPinch = tuning.allowPinchGrab && pinchDistance < tuning.pinchReleaseDistance * 1.5f && fingerCurl < tuning.grabCurl;
                grabPoint.SetPositionAndRotation(holdingWithPinch || aimingPinch ? pinchPoint : palmPoint, palm.rotation);
            }
            else
            {
                // while holding, the grab point stays the same kind (palm or fingertips) so the prop doesn't jump
                grabPoint.SetPositionAndRotation(holdingWithPinch ? pinchPoint : palmPoint, palm.rotation);
            }

            RecordHistory(grabPoint.position, grabPoint.rotation);
            SetHolding(want);
        }

        void FixedUpdate()
        {
            // applied after the interactable has detached, so the hand's speed wins over anything else
            foreach (var t in pendingThrows)
            {
                if (!t.body || t.body.isKinematic) continue;
                t.body.linearVelocity = t.velocity;
                t.body.angularVelocity = t.spin;
            }
            pendingThrows.Clear();
        }

        void TakeOverInteractor()
        {
            var select = interactor.selectInput;
            if (select.inputSourceMode != XRInputButtonReader.InputSourceMode.ManualValue)
                select.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;   // close the hand *on* the prop
            interactor.attachTransform = grabPoint;
            if (interactor.nearInteractionCaster is InteractionCasterBase caster) caster.castOrigin = grabPoint;
            if (interactor.nearInteractionCaster is SphereInteractionCaster sphere) sphere.castRadius = tuning.grabReach;
        }

        void SetHolding(bool on)
        {
            if (on == holding) return;
            holding = on;
            if (!interactor) return;
            var select = interactor.selectInput;
            select.manualPerformed = on;
            select.manualValue = on ? 1f : 0f;
            if (on) select.manualFramePerformed = Time.frameCount;
            else select.manualFrameCompleted = Time.frameCount;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (args.isCanceled || !tuning) return;
            if (!(args.interactableObject is XRGrabInteractable grab) || !grab.TryGetComponent(out Rigidbody body)) return;
            HandVelocity(out var velocity, out var spin);
            pendingThrows.Add((body, velocity * tuning.throwStrength, spin * tuning.throwSpin));
        }

        // ------------------------------------------------------------------ hand motion
        void RecordHistory(Vector3 p, Quaternion r)
        {
            history.Add((Time.time, p, r));
            while (history.Count > 2 && Time.time - history[0].t > 0.3f) history.RemoveAt(0);
        }

        /// <summary>
        /// Fastest movement of the hand over the last throwWindow seconds (measured over ~3-frame spans so a single
        /// noisy frame doesn't count), plus its average spin. Opening the hand to let go slows it down a little,
        /// so the peak just before release is the speed the player meant.
        /// </summary>
        void HandVelocity(out Vector3 velocity, out Vector3 spin)
        {
            velocity = Vector3.zero; spin = Vector3.zero;
            int n = history.Count;
            if (n < 2) return;
            float now = history[n - 1].t;
            int first = n - 1;
            while (first > 0 && now - history[first - 1].t <= tuning.throwWindow) first--;

            const int span = 3;
            float best = -1f;
            for (int i = Mathf.Max(first + 1, 1); i < n; i++)
            {
                int j = Mathf.Max(i - span, Mathf.Max(first - 1, 0));
                float dt = history[i].t - history[j].t;
                if (dt <= 1e-4f) continue;
                Vector3 v = (history[i].p - history[j].p) / dt;
                if (v.sqrMagnitude > best) { best = v.sqrMagnitude; velocity = v; }
            }

            int a = Mathf.Max(first - 1, 0);
            float spinDt = history[n - 1].t - history[a].t;
            if (spinDt > 1e-4f)
            {
                (history[n - 1].r * Quaternion.Inverse(history[a].r)).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (!float.IsNaN(axis.x)) spin = axis * (angle * Mathf.Deg2Rad / spinDt);
            }
        }

        static float AverageCurl(XRHand hand)
        {
            float sum = 0f; int count = 0;
            foreach (var f in Fingers)
                if (hand.CalculateFingerShape(f, XRFingerShapeTypes.FullCurl).TryGetFullCurl(out float c)) { sum += c; count++; }
            return count > 0 ? sum / count : 0f;
        }

        static XRHandSubsystem FindHands()
        {
            SubsystemManager.GetSubsystems(found);
            foreach (var s in found) if (s.running) return s;
            return null;
        }
    }
}
