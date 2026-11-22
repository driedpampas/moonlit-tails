using System.Collections;
using System.Collections.Generic;
using MoonlitTails.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails
{
    /// <summary>
    /// Where a prop lives (its Default Slot), so it snaps cleanly when let go nearby and returns when dropped on the
    /// floor, lost, or reset. Home is recorded from the prop's starting transform relative to the bar so hand edits in
    /// the scene/prefab stay in sync automatically.
    ///
    /// Filled glasses and mixing vessels with liquid inside are an exception on floor-catch / reset: instead of going
    /// back to a side shelf, they are placed upright on an unoccupied spot on the front of the bar counter.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PropRespawn : MonoBehaviour
    {
        [Tooltip("The bar root. Home is stored in its space (empty = the prop's parent, or the world).")]
        public Transform space;
        [Tooltip("How close (m) to the default slot a released prop snaps back into place.")]
        public float snapRadius = 0.14f;

        Vector3 homePosition;
        Quaternion homeRotation = Quaternion.identity;

        public static readonly List<PropRespawn> All = new List<PropRespawn>();
        static int lastResetFrame = -1;

        // Open, easily reachable spots along the front bar counter in bar-local space (ordered center-out).
        static readonly Vector3[] FrontBarSpots =
        {
            new Vector3( 0.00f, 1.022f, -0.56f),
            new Vector3( 0.24f, 1.022f, -0.78f),
            new Vector3( 0.40f, 1.022f, -0.78f),
            new Vector3(-0.38f, 1.022f, -0.60f),
            new Vector3( 0.40f, 1.022f, -0.60f),
            new Vector3( 0.00f, 1.022f, -0.42f),
            new Vector3( 0.56f, 1.022f, -0.78f),
            new Vector3(-0.38f, 1.022f, -0.44f),
            new Vector3( 0.42f, 1.022f, -0.44f),
            new Vector3( 0.72f, 1.022f, -0.80f),
            new Vector3(-0.64f, 1.022f, -0.80f),
            new Vector3( 0.88f, 1.022f, -0.80f),
            new Vector3(-0.82f, 1.022f, -0.80f),
            new Vector3( 0.14f, 1.022f, -0.42f),
            new Vector3(-0.14f, 1.022f, -0.42f),
        };

        Rigidbody body;
        XRBaseInteractable grab;
        DrinkContainer container;
        bool homeNextStep;
        Coroutine pendingSnap;

        public Vector3 HomePosition => ToWorld(homePosition);
        public Quaternion HomeRotation => ToWorld(homeRotation);
        public bool IsHeld => grab && grab.isSelected;
        /// <summary>True for a glass or mixing vessel with liquid inside: reset/floor-catch sends it to the front counter.</summary>
        public bool KeepOnFrontBar => container && !container.IsEmpty;

        /// <summary>
        /// Stuck to something else right now (a fruit slice on a glass's rim, a cap on the shaker): it moves with that,
        /// not on its own, so a reset leaves it there. Set by whatever sticks it on and takes it off.
        /// </summary>
        public bool Attached { get; set; }

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
            container = GetComponent<DrinkContainer>();
            if (!space) space = transform.parent;
            RememberHome();
            if (grab)
            {
                grab.selectEntered.AddListener(OnGrabbed);
                grab.selectExited.AddListener(OnReleased);
            }
        }

        void OnDestroy()
        {
            if (!grab) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void OnGrabbed(SelectEnterEventArgs _)
        {
            if (pendingSnap != null) { StopCoroutine(pendingSnap); pendingSnap = null; }
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (args.isCanceled || Attached || snapRadius <= 0f) return;
            if (Vector3.Distance(transform.position, HomePosition) > snapRadius) return;
            if (pendingSnap != null) StopCoroutine(pendingSnap);
            pendingSnap = StartCoroutine(SnapToSlotAfterRelease());
        }

        IEnumerator SnapToSlotAfterRelease()
        {
            // Let Garnish/ContainerCap (next frame) and ServeStation coaster snap + XRI throw (next physics step) run first.
            yield return null;
            yield return new WaitForFixedUpdate();
            pendingSnap = null;
            if (!this || Attached || IsHeld) yield break;
            if (container && ServeStation.IsOnAnyCoaster(container)) yield break;
            if (!body.isKinematic && body.linearVelocity.sqrMagnitude > 1.5f * 1.5f) yield break;
            if (Vector3.Distance(transform.position, HomePosition) <= snapRadius * 1.5f)
                SendHome();
        }

        /// <summary>Called once per frame by the Drink Game: catches props that somehow escape outside the room.</summary>
        public void CheckLost()
        {
            if (IsHeld || Attached) return;
            Vector3 origin = space ? space.position : Vector3.zero;
            Vector3 p = transform.position;
            if (p.y < origin.y - 1f || (p - origin).sqrMagnitude > 30f * 30f) SendToSafeSpot();
        }

        /// <summary>Called once per physics step by the Drink Game: finishes a reset that had to wait for a hand to let go.</summary>
        public void ApplyPendingHome()
        {
            if (!homeNextStep) return;
            homeNextStep = false;
            SendToSafeSpot();
        }

        /// <summary>
        /// Teleports every prop to its Default Slot (or front of the bar counter for filled glasses/vessels).
        /// Does not clear drink contents or reset orders.
        /// </summary>
        public static void ResetAll()
        {
            if (lastResetFrame == Time.frameCount) return;   // a button and a key can both fire in one frame
            lastResetFrame = Time.frameCount;
            var snapshot = All.ToArray();
            // Pass 1: return empty props / bottles / tools / garnishes first so the front bar is clear.
            for (int i = 0; i < snapshot.Length; i++)
                if (snapshot[i] && !snapshot[i].KeepOnFrontBar) snapshot[i].ResetToHome();
            Physics.SyncTransforms();
            // Pass 2: place filled glasses and mixing vessels on open spots along the front of the bar.
            for (int i = 0; i < snapshot.Length; i++)
                if (snapshot[i] && snapshot[i].KeepOnFrontBar) snapshot[i].ResetToHome();
        }

        /// <summary>Returns this prop to its Default Slot (or front bar if filled). Releases hand first if held.</summary>
        public void ResetToHome()
        {
            if (Attached) return;
            if (pendingSnap != null) { StopCoroutine(pendingSnap); pendingSnap = null; }
            if (grab && grab.isSelected && grab.interactionManager)
            {
                // Let go first, then move on the next physics step so XRI's LateUpdate throw can't fling it away.
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
                homeNextStep = true;
                return;
            }
            SendToSafeSpot();
        }

        /// <summary>Sends a filled container to the front counter, or any other prop to its Default Slot.</summary>
        public void SendToSafeSpot()
        {
            if (KeepOnFrontBar) SendToFrontBar();
            else SendHome();
        }

        public void SendHome()
        {
            Teleport(ToWorld(homePosition), ToWorld(homeRotation));
        }

        /// <summary>Places a filled glass or mixing vessel upright on an unoccupied spot at the front of the bar.</summary>
        public void SendToFrontBar()
        {
            Quaternion upright = ToWorld(homeRotation);
            transform.rotation = upright;

            float footprint = 0.055f;
            float height = 0.20f;
            if (TryGetComponent<MeshRenderer>(out var mr))
            {
                var wb = mr.bounds;
                footprint = Mathf.Clamp(Mathf.Max(wb.extents.x, wb.extents.z), 0.04f, 0.09f);
                height = Mathf.Clamp(wb.size.y, 0.10f, 0.30f);
            }

            Vector3 chosen = FindOpenFrontSpot(footprint, height, out float surfaceY);
            Vector3 pos;
            if (container)
            {
                Vector3 c = container.CenterWorld;
                pos = transform.position + new Vector3(chosen.x - c.x, surfaceY - container.BottomWorld.y + 0.002f, chosen.z - c.z);
            }
            else
            {
                pos = new Vector3(chosen.x, surfaceY + 0.002f, chosen.z);
            }
            Teleport(pos, upright);
            Physics.SyncTransforms();
        }

        Vector3 FindOpenFrontSpot(float radius, float height, out float surfaceY)
        {
            float clearance = Mathf.Max(0.14f, radius * 2.2f);
            float checkRadius = radius + 0.015f;
            int mask = ~(1 << 2);   // ignore Ignore Raycast (includes FloorCatch)

            // Tier 1: free of both other props and static obstacles on the counter.
            for (int i = 0; i < FrontBarSpots.Length; i++)
            {
                Vector3 candidate = ToWorld(FrontBarSpots[i]);
                float y = SampleSurfaceY(candidate, mask);
                if (IsSpotOccupied(candidate, y, clearance, checkRadius, height, mask, checkStatic: true)) continue;
                surfaceY = y;
                return candidate;
            }

            // Tier 2: free of other props (never stack two filled glasses on top of each other).
            for (int i = 0; i < FrontBarSpots.Length; i++)
            {
                Vector3 candidate = ToWorld(FrontBarSpots[i]);
                float y = SampleSurfaceY(candidate, mask);
                if (IsSpotOccupied(candidate, y, clearance, checkRadius, height, mask, checkStatic: false)) continue;
                surfaceY = y;
                return candidate;
            }

            Vector3 fallback = ToWorld(FrontBarSpots[0]);
            surfaceY = SampleSurfaceY(fallback, mask);
            return fallback;
        }

        float SampleSurfaceY(Vector3 spot, int mask)
        {
            var hits = Physics.RaycastAll(spot + Vector3.up * 0.35f, Vector3.down, 0.55f, mask, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (!col || col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<PropRespawn>()) continue;
                float y = hits[i].point.y;
                if (y <= spot.y + 0.08f && y >= spot.y - 0.08f && y > best) best = y;
            }
            return best > float.NegativeInfinity ? best : spot.y;
        }

        bool IsSpotOccupied(Vector3 spot, float surfaceY, float propClearance, float checkRadius, float height, int mask, bool checkStatic)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var other = All[i];
                if (!other || other == this || other.transform.IsChildOf(transform)) continue;
                Vector3 d = other.transform.position - spot;
                if (Mathf.Abs(d.y) > 0.25f) continue;
                d.y = 0f;
                if (d.sqrMagnitude < propClearance * propClearance) return true;
            }

            if (!checkStatic) return false;

            Vector3 p0 = new Vector3(spot.x, surfaceY + checkRadius + 0.045f, spot.z);
            Vector3 p1 = new Vector3(spot.x, surfaceY + Mathf.Max(height, checkRadius * 2f + 0.06f), spot.z);
            var overlaps = Physics.OverlapCapsule(p0, p1, checkRadius, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                var col = overlaps[i];
                if (!col || col.transform.IsChildOf(transform)) continue;
                if (col.bounds.max.y <= surfaceY + 0.045f) continue;
                if (IsCounterSurface(col.transform)) continue;
                return true;
            }
            return false;
        }

        static bool IsCounterSurface(Transform t)
        {
            while (t)
            {
                string n = t.name;
                if (n.StartsWith("ENV_BarCounter") || n.StartsWith("ENV_BarMat") || n.StartsWith("DECO_Coaster")) return true;
                t = t.parent;
            }
            return false;
        }

        void Teleport(Vector3 p, Quaternion r)
        {
            // Teleport the body too: with interpolation on, moving only the transform gets overwritten.
            body.position = p; body.rotation = r;
            transform.SetPositionAndRotation(p, r);
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
