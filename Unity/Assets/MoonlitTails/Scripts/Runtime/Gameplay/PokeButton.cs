using System;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace MoonlitTails.Gameplay
{
    /// <summary>Where the index fingertips are this frame, in world space (empty when hands aren't tracked).</summary>
    public static class HandTips
    {
        static readonly List<Vector3> tips = new List<Vector3>();
        static readonly List<XRHandSubsystem> found = new List<XRHandSubsystem>();
        static int frame = -1;
        static XROrigin origin;

        public static IReadOnlyList<Vector3> Current
        {
            get
            {
                if (frame == Time.frameCount) return tips;
                frame = Time.frameCount;
                tips.Clear();
                XRHandSubsystem hands = null;
                SubsystemManager.GetSubsystems(found);
                foreach (var s in found) if (s.running) { hands = s; break; }
                if (hands == null) return tips;
                if (!origin) origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                Transform space = origin ? (origin.CameraFloorOffsetObject ? origin.CameraFloorOffsetObject.transform : origin.transform) : null;
                Add(hands.leftHand, space);
                Add(hands.rightHand, space);
                return tips;
            }
        }

        static void Add(XRHand hand, Transform space)
        {
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose)) return;
            tips.Add(space ? space.TransformPoint(pose.position) : pose.position);
        }
    }

    /// <summary>
    /// A button you press by touching it with an index fingertip. It also works with a mouse click (for testing in the
    /// editor with the XR Device Simulator).
    /// </summary>
    public class PokeButton : MonoBehaviour
    {
        public event Action Pressed;
        public float radius = 0.025f;

        bool down;
        float coolUntil;
        Renderer face;
        Color normal;
        TextMeshPro text;

        /// <summary>A flat square button facing the panel's front (Cube), or a dome sitting on a surface (Sphere).</summary>
        public static PokeButton Create(Transform parent, string caption, Vector3 localPos, float size, Color color,
                                        PrimitiveType shape = PrimitiveType.Cube)
        {
            var go = GameVisuals.Primitive(shape, parent, "Button " + caption, color);
            go.transform.localPosition = localPos;
            go.transform.localScale = shape == PrimitiveType.Sphere ? new Vector3(size, size * 0.45f, size) : new Vector3(size, size, size * 0.4f);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var b = go.AddComponent<PokeButton>();
            b.radius = size * 0.75f;
            b.face = go.GetComponent<Renderer>();
            b.normal = color;
            b.text = GameVisuals.Text(parent, "Caption " + caption, Mathf.Min(size * 10f, 0.2f));   // beside the cube, not under it, so it isn't squashed
            b.text.text = caption;
            b.text.transform.localPosition = localPos + new Vector3(0, 0, -size * 0.3f);
            return b;
        }

        public void SetCaption(string caption) { if (text) text.text = caption; }

        /// <summary>Press it from code (tests).</summary>
        public void Press() => Fire();

        void Update()
        {
            float r = radius;
            bool touching = false;
            foreach (var tip in HandTips.Current)
                if ((tip - transform.position).sqrMagnitude < r * r) { touching = true; break; }

            if (touching && !down) { down = true; Fire(); }
            else if (!touching && down)
            {
                bool farEnough = true;
                foreach (var tip in HandTips.Current)
                    if ((tip - transform.position).sqrMagnitude < r * r * 2.5f) { farEnough = false; break; }
                if (farEnough) down = false;
            }

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse != null && cam && mouse.leftButton.wasPressedThisFrame)
            {
                var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 5f, ~0, QueryTriggerInteraction.Collide) && hit.collider.gameObject == gameObject) Fire();
            }

            if (face && Time.time > coolUntil && face.sharedMaterial != GameVisuals.Flat(normal)) face.sharedMaterial = GameVisuals.Flat(normal);
        }

        void Fire()
        {
            if (Time.time < coolUntil) return;
            coolUntil = Time.time + 0.25f;
            if (face) face.sharedMaterial = GameVisuals.Flat(Color.Lerp(normal, Color.white, 0.6f));
            Pressed?.Invoke();
        }
    }
}
