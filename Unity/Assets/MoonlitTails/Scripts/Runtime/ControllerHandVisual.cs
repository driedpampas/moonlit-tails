using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MoonlitTails
{
    /// <summary>
    /// Replaces the default plastic controller visuals on Left/Right Controller with the project's rigged hand models
    /// (cloned from LeftHandQuestVisual / RightHandQuestVisual) and curls the fingers from controller Trigger/Grip.
    /// Optical hand tracking remains untouched: XRInputModalityManager switches between Controller and Hand modes.
    /// </summary>
    public class ControllerHandVisual : MonoBehaviour
    {
        [Tooltip("Local offset of each hand relative to its Controller root.")]
        public Vector3 handLocalPosition = new Vector3(0f, 0f, -0.02f);
        [Tooltip("Roll (degrees around Z) so the palm faces inward in a natural controller grip.")]
        public float inwardRollDegrees = 80f;
        [Tooltip("Finger curl smoothing speed (units per second).")]
        public float curlSpeed = 14f;

        struct FingerRig
        {
            public Transform proximal, intermediate, distal;
            public Quaternion proxRest, interRest, distRest;
        }

        class HandRig
        {
            public Transform controller;
            public XRNode node;
            public IXRSelectInteractor[] interactors;
            public FingerRig index, middle, ring, little, thumb;
            public float triggerSmooth, gripSmooth;
        }

        readonly List<HandRig> hands = new List<HandRig>(2);

        void Awake()
        {
            SetupSide("Left", "L_", XRNode.LeftHand, +inwardRollDegrees);
            SetupSide("Right", "R_", XRNode.RightHand, -inwardRollDegrees);
        }

        void SetupSide(string side, string bonePrefix, XRNode node, float rollZ)
        {
            var controller = transform.Find($"Camera Offset/{side} Controller");
            var template = transform.Find($"Camera Offset/{side} Hand/{side}HandQuestVisual");
            if (!controller || !template) return;

            var controllerVisual = controller.Find($"{side} Controller Visual");
            if (controllerVisual)
            {
                foreach (var r in controllerVisual.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;
            }

            // Template starts inactive, so strip hand-tracking drivers before activating the clone.
            var clone = Instantiate(template.gameObject, controller, false);
            clone.name = $"{side} Controller Hand Visual";
            clone.transform.localPosition = handLocalPosition;
            clone.transform.localRotation = Quaternion.Euler(0f, 0f, rollZ);
            clone.transform.localScale = Vector3.one;

            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!mb) continue;
                string typeName = mb.GetType().Name;
                if (typeName.StartsWith("XRHand") || typeName.Contains("Affordance") || typeName.Contains("Interactor"))
                {
                    mb.enabled = false;
                    Destroy(mb);
                }
            }

            // Remove hand-mode affordance children that belong to optical hand tracking.
            for (int i = clone.transform.childCount - 1; i >= 0; i--)
            {
                var child = clone.transform.GetChild(i);
                if (child.name.Contains("Affordance")) Destroy(child.gameObject);
            }

            foreach (var smr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.enabled = true;
                smr.updateWhenOffscreen = true;
            }

            clone.SetActive(true);

            var rig = new HandRig
            {
                controller = controller,
                node = node,
                interactors = controller.GetComponentsInChildren<IXRSelectInteractor>(true),
                index = BuildFinger(clone.transform, bonePrefix + "Index"),
                middle = BuildFinger(clone.transform, bonePrefix + "Middle"),
                ring = BuildFinger(clone.transform, bonePrefix + "Ring"),
                little = BuildFinger(clone.transform, bonePrefix + "Little"),
                thumb = BuildFinger(clone.transform, bonePrefix + "Thumb"),
            };
            hands.Add(rig);
        }

        static FingerRig BuildFinger(Transform root, string prefix)
        {
            var p = FindDeep(root, prefix + "Proximal");
            var m = FindDeep(root, prefix + "Intermediate");
            var d = FindDeep(root, prefix + "Distal");
            return new FingerRig
            {
                proximal = p,
                intermediate = m,
                distal = d,
                proxRest = p ? p.localRotation : Quaternion.identity,
                interRest = m ? m.localRotation : Quaternion.identity,
                distRest = d ? d.localRotation : Quaternion.identity,
            };
        }

        static Transform FindDeep(Transform root, string targetName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == targetName) return t;
            return null;
        }

        void LateUpdate()
        {
            float step = curlSpeed * Time.deltaTime;
            for (int i = 0; i < hands.Count; i++)
            {
                var h = hands[i];
                if (!h.controller || !h.controller.gameObject.activeInHierarchy) continue;

                float trigger = 0f, grip = 0f;
                var dev = InputDevices.GetDeviceAtXRNode(h.node);
                if (dev.isValid)
                {
                    dev.TryGetFeatureValue(CommonUsages.trigger, out trigger);
                    dev.TryGetFeatureValue(CommonUsages.grip, out grip);
                }

                h.triggerSmooth = Mathf.MoveTowards(h.triggerSmooth, trigger, step);
                h.gripSmooth = Mathf.MoveTowards(h.gripSmooth, grip, step);

                bool holdingProp = IsHolding(h.interactors);
                // Wrap all fingers when holding a prop; only point with the index finger when empty-handed (grip without trigger).
                float indexCurl = holdingProp
                    ? Mathf.Max(h.gripSmooth, h.triggerSmooth)
                    : (h.triggerSmooth > 0.05f ? h.triggerSmooth : Mathf.Lerp(0f, -0.25f, h.gripSmooth));
                ApplyFinger(h.index, indexCurl, 55f, 65f, 45f);
                ApplyFinger(h.middle, h.gripSmooth, 55f, 70f, 50f);
                ApplyFinger(h.ring, h.gripSmooth, 55f, 70f, 50f);
                ApplyFinger(h.little, h.gripSmooth, 55f, 65f, 45f);
                ApplyFinger(h.thumb, Mathf.Max(h.gripSmooth * 0.9f, h.triggerSmooth * 0.75f), 25f, 0f, 35f);
            }
        }

        static bool IsHolding(IXRSelectInteractor[] interactors)
        {
            if (interactors == null) return false;
            for (int i = 0; i < interactors.Length; i++)
                if (interactors[i] != null && !(interactors[i] is XRPokeInteractor) && interactors[i].hasSelection) return true;
            return false;
        }

        static void ApplyFinger(FingerRig f, float curl, float proxDeg, float interDeg, float distDeg)
        {
            if (f.proximal) f.proximal.localRotation = f.proxRest * Quaternion.Euler(proxDeg * curl, 0f, 0f);
            if (f.intermediate) f.intermediate.localRotation = f.interRest * Quaternion.Euler(interDeg * curl, 0f, 0f);
            if (f.distal) f.distal.localRotation = f.distRest * Quaternion.Euler(distDeg * curl, 0f, 0f);
        }
    }
}
