using System.Collections;
using System.Collections.Generic;
using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// The serve spot and its panel: put a finished glass on the coaster, use the end-of-drink stat adjustment
    /// (1 point for a mixed drink, 2 for a layered one), then ring the bell (or press Serve) to hand it over.
    ///
    /// The coaster catches glasses: let go of one close to or just above it and it stands up straight in the middle,
    /// pinned upright, until someone picks it up again. Without this a glass let go at an angle tips over.
    /// </summary>
    public class ServeStation : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("The coaster: where glasses snap and are served (MK_ServeSpot in the bar).")]
        [SerializeField] Transform serveSpot;
        [SerializeField] DrinkGame game;
        [Tooltip("Ringing it serves the glass on the coaster.")]
        [SerializeField] XRBaseInteractable bell;

        [Header("Panel")]
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text info;
        [SerializeField] StatBarsView bars = new StatBarsView();

        [Header("Coaster")]
        [Tooltip("How close (m) to the middle of the coaster a glass on it counts as being served.")]
        public float radius = 0.15f;
        [Tooltip("How far (m, sideways) from the middle of the coaster a released glass still snaps onto it.")]
        public float snapRadius = 0.13f;
        [Tooltip("How high above the coaster (m) a released glass still snaps onto it.")]
        public float snapHeight = 0.3f;

        public Transform ServeSpot => serveSpot;
        public Transform Bell => bell ? bell.transform : null;
        /// <summary>The panel with the title, the taste bars and the buttons.</summary>
        public Transform Panel => panel;
        /// <summary>The glass standing on the coaster, or null.</summary>
        public DrinkContainer CoasterGlass { get; private set; }

        Transform panel;
        DrinkContainer glass;       // the glass being served (on or near the coaster, not held)
        Rigidbody glassBody;
        RigidbodyConstraints constraintsBefore;
        readonly List<XRGrabInteractable> watched = new List<XRGrabInteractable>();
        bool bellHooked;
        int shownState = int.MinValue;   // what the panel shows, as a hash: it's redrawn only when this changes
        float serveCooldownUntil;

        const RigidbodyConstraints Pinned =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        void OnEnable() => HookBell(true);
        void OnDisable() => HookBell(false);

        void Start()
        {
            if (!serveSpot || !game || !title || !info)
            {
                Debug.LogError($"[Moonlit] {name}: ServeStation needs its serve spot, Drink Game, title and info set", this);
                enabled = false;
                return;
            }
            panel = title.transform.parent;
            HookBell(true);
            foreach (var c in DrinkContainer.All)
            {
                if (!c.IsGlass || !c.TryGetComponent<XRGrabInteractable>(out var grab)) continue;
                grab.selectEntered.AddListener(OnGlassGrabbed);
                grab.selectExited.AddListener(OnGlassReleased);
                watched.Add(grab);
            }
        }

        void OnDestroy()
        {
            foreach (var grab in watched)
            {
                if (!grab) continue;
                grab.selectEntered.RemoveListener(OnGlassGrabbed);
                grab.selectExited.RemoveListener(OnGlassReleased);
            }
            watched.Clear();
        }

        void HookBell(bool on)
        {
            if (!bell || on == bellHooked) return;
            bellHooked = on;
            if (on) bell.selectEntered.AddListener(OnBellRung); else bell.selectEntered.RemoveListener(OnBellRung);
        }

        void OnBellRung(SelectEnterEventArgs _) => Serve();

        // ------------------------------------------------------------------ the panel
        void Update()
        {
            glass = FindGlass();
            int state = PanelState();
            if (state == shownState) return;
            shownState = state;
            if (!glass)
            {
                title.text = "<b>Serve here</b>";
                info.text = $"Put a glass on the coaster\n<size=80%>Served tonight: {game.ServedCount}</size>";
                bars.Show(Taste.Zero, "");
                return;
            }
            title.text = $"<b>{glass.variant}</b> <size=70%>{(glass.IsEmpty ? "empty" : glass.Method.ToString())}</size>";
            string extras = (glass.Cold ? "Cold" : "not cold") + " · " + (glass.Garnish ? glass.Garnish.color + " garnish" : "no garnish");
            info.text = $"{extras}\n<size=80%>Adjustment points left: {glass.AdjustmentLeft} · then ring the bell</size>";
            bars.Show(glass.FinalStats, "");
        }

        int PanelState()
        {
            if (!glass) return System.HashCode.Combine(0, game.ServedCount);
            var s = glass.FinalStats;
            int flags = (glass.IsEmpty ? 1 : 0) | (glass.Cold ? 2 : 0) | ((int)glass.Method << 2);
            return System.HashCode.Combine(glass.GetInstanceID(), flags, glass.Garnish ? glass.Garnish.color : null,
                                           s.Bitter, s.Sweet, s.Sour, glass.AdjustmentLeft);
        }

        DrinkContainer FindGlass()
        {
            DrinkContainer best = null; float bestD = radius;
            foreach (var c in DrinkContainer.All)
            {
                if (!c.IsGlass || c.IsHeld) continue;
                Vector3 d = c.transform.position - serveSpot.position;
                if (Mathf.Abs(d.y) > 0.2f) continue;
                d.y = 0;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = c; }
            }
            return best;
        }

        // UnityEvent targets for the panel's buttons (one int each: +1 or -1)
        public void AdjustBitter(int delta) => Adjust(new Taste(delta, 0, 0));
        public void AdjustSweet(int delta) => Adjust(new Taste(0, delta, 0));
        public void AdjustSour(int delta) => Adjust(new Taste(0, 0, delta));

        void Adjust(Taste d)
        {
            if (!glass) return;
            if (!glass.TryAdjust(d, out var msg) && msg != null) glass.Label.Flash(msg, GameVisuals.Bad);
        }

        /// <summary>Hands the glass on the coaster to the customers. The bell and the Serve button call this.</summary>
        public void Serve()
        {
            // not while switched off (Start does that when a reference is missing); one ring = one serve, even if two inputs fire
            if (!isActiveAndEnabled || Time.time < serveCooldownUntil) return;
            serveCooldownUntil = Time.time + 0.5f;
            glass = FindGlass();
            if (!glass || glass.IsEmpty)
            {
                info.text = "Put a filled glass on the coaster first";   // stays until what the panel shows changes
                return;
            }
            bool ok = game.TryServe(glass.ToServed(), out string summary);
            glass.Label.Flash(summary, ok ? GameVisuals.Good : GameVisuals.Bad);
            if (ok) glass.ServedAndReturned();
        }

        // ------------------------------------------------------------------ the coaster
        void OnGlassGrabbed(SelectEnterEventArgs args)
        {
            if (CoasterGlass && args.interactableObject.transform == CoasterGlass.transform) Release();
        }

        void OnGlassReleased(SelectExitEventArgs args)
        {
            // a cancelled release (e.g. the reset button taking the glass away) isn't the player putting it down
            if (args.isCanceled) return;
            var g = args.interactableObject.transform.GetComponent<DrinkContainer>();   // only glasses are watched
            if (InReach(g)) StartCoroutine(SnapAfterRelease(g));
        }

        /// <summary>Is the glass close enough to the coaster to snap onto it?</summary>
        public bool InReach(DrinkContainer g)
        {
            Vector3 p = serveSpot.position;
            float low = Mathf.Min(g.BottomWorld.y, g.CenterWorld.y) - p.y;
            if (low < -0.05f || low > snapHeight) return false;
            Vector3 d = g.CenterWorld - p; d.y = 0;
            return d.magnitude < snapRadius;
        }

        IEnumerator SnapAfterRelease(DrinkContainer g)
        {
            // XRI applies the throw in LateUpdate after the release: snap on the next physics step, so it can't knock it off
            yield return new WaitForFixedUpdate();
            if (g && !g.IsHeld) TrySnap(g);
        }

        /// <summary>Stand the glass upright in the middle of the coaster. False if another glass is already there.</summary>
        public bool TrySnap(DrinkContainer g)
        {
            if (CoasterGlass && CoasterGlass != g)
            {
                if (OnCoaster(CoasterGlass)) return false;
                Release();
            }

            var t = g.transform;
            Quaternion r = Quaternion.FromToRotation(g.UpWorld, Vector3.up) * t.rotation;
            t.rotation = r;
            Vector3 c = g.CenterWorld, p = serveSpot.position;
            Vector3 pos = t.position + new Vector3(p.x - c.x, p.y - g.BottomWorld.y + 0.001f, p.z - c.z);
            t.position = pos;

            if (t.TryGetComponent(out Rigidbody body))
            {
                // teleport the body too: with interpolation on, moving only the transform gets overwritten
                body.position = pos; body.rotation = r;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                // pinned sideways and upright so it can't be knocked over; it can still be lifted off
                if (CoasterGlass != g) constraintsBefore = body.constraints;
                body.constraints = Pinned;
            }
            CoasterGlass = g;
            glassBody = body;
            Physics.SyncTransforms();
            return true;
        }

        bool OnCoaster(DrinkContainer g)
        {
            if (!g || g.IsHeld) return false;   // it can be destroyed while it stands here
            Vector3 d = g.CenterWorld - serveSpot.position; d.y = 0;
            return d.magnitude < 0.03f && Mathf.Abs(g.BottomWorld.y - serveSpot.position.y) < 0.05f;
        }

        void Release()
        {
            if (glassBody) glassBody.constraints = constraintsBefore;
            CoasterGlass = null;
            glassBody = null;
        }

        void FixedUpdate()
        {
            // picked up, sent home by a reset, or returned after serving: let it go
            if (CoasterGlass != null && !OnCoaster(CoasterGlass)) Release();
        }
    }
}
