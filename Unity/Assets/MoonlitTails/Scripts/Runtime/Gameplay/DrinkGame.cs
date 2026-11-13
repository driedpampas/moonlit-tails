using System;
using System.Collections.Generic;
using System.Linq;
using MoonlitTails.Drinks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// The night at the bar: the customers and their orders (one ticket per customer on the ticket rail, a speech
    /// bubble over each customer, the check when a drink is served), the reset button, and putting props back when
    /// they leave the bar.
    ///
    /// Customers come one at a time: one walks in from the door to the bar, orders, and waits until they get a drink
    /// that matches. Then they tell the next line of their story, walk out, and the next customer comes in.
    /// Only the customer in the bar is switched on, so the others cost nothing to draw or animate.
    ///
    /// It lives on the "Drink Game" object in the scene; the customers, their tickets, the serve panel and the reset
    /// button are set on it in the Inspector. The props carry their own gameplay components, set in their prefabs.
    /// </summary>
    public class DrinkGame : MonoBehaviour
    {
        /// <summary>A customer: who they are, their character model, and which ticket on the rail is theirs.</summary>
        [Serializable]
        public class Seat
        {
            public CustomerProfile profile;
            [Tooltip("The character model.")] public Transform body;
            [Tooltip("Their ticket on the ticket rail.")] public Transform ticketSpot;
        }

        /// <summary>A customer during play. Read it from tools; change it only through DrinkGame's methods.</summary>
        public sealed class SeatState
        {
            public CustomerProfile Profile { get; internal set; }
            public string DisplayName => Profile.displayName;
            public Transform Body { get; internal set; }
            public Transform TicketSpot { get; internal set; }
            public FloatingLabel Bubble { get; internal set; }
            public FloatingLabel Ticket { get; internal set; }
            public DrinkOrder Order { get; internal set; }
            public int StoryIndex { get; internal set; }
            public bool Missed { get; internal set; }
        }

        /// <summary>Where the customer whose turn it is has got to.</summary>
        public enum Visit { Away, WalkingIn, AtBar, Served, WalkingOut }

        [Header("Customers")]
        [Tooltip("They come in one at a time, in this order, and start again after the last one.")]
        [SerializeField] List<Seat> seats = new List<Seat>();
        [Tooltip("The way in, from the door to the spot at the bar. Customers leave the same way back.")]
        [SerializeField] Transform[] walkPath = new Transform[0];
        [Tooltip("Metres per second.")]
        public float walkSpeed = 1.2f;
        [Tooltip("How high (m) a walking customer bobs, and how many bobs per metre.")]
        public float hopHeight = 0.04f;
        public float hopsPerMetre = 2.5f;
        [Tooltip("Seconds a served customer stays to say their line before they leave.")]
        public float leaveDelay = 5f;
        [Tooltip("Seconds between one customer leaving and the next one coming in.")]
        public float nextCustomerDelay = 2f;

        [Header("Scene")]
        [SerializeField] ServeStation serve;
        [Tooltip("Where the player stands (MK_PlayerStart).")]
        [SerializeField] Transform playerSpot;
        [SerializeField] IceScoop iceScoop;
        [Tooltip("Pressing it puts every prop back where it started.")]
        [SerializeField] XRBaseInteractable resetButton;
        [Tooltip("The rules written on the chalkboard.")]
        [SerializeField] Transform menuBoardText;
        [Tooltip("Editor/dev shortcut: R on the keyboard resets the props too.")]
        [SerializeField] bool keyboardReset = true;

        public IReadOnlyList<SeatState> Seats => states;
        public int ServedCount { get; private set; }
        /// <summary>The customer in the bar (an index into <see cref="Seats"/>), or -1 between customers.</summary>
        public int Current { get; private set; } = -1;
        public Visit Phase { get; private set; }
        public ServeStation Station => serve;
        public Transform PlayerSpot => playerSpot;
        public IceScoop IceScoop => iceScoop;
        public Transform MenuBoardText => menuBoardText;
        public Transform ResetButtonObject => resetButton ? resetButton.transform : null;

        readonly List<SeatState> states = new List<SeatState>();
        readonly List<CustomerProfile> runtimeProfiles = new List<CustomerProfile>();
        IReadOnlyList<GlassVariant> glasses = new GlassVariant[0];
        IReadOnlyList<string> garnishColors = new string[0];
        System.Random rng;
        bool ordersEnabled, resetHooked;
        int nextCustomer, pathIndex;
        float phaseUntil, walked;

        // ------------------------------------------------------------------ start
        void Start()
        {
            rng = new System.Random();
            glasses = FindObjectsByType<DrinkContainer>(FindObjectsSortMode.None).Where(c => c.IsGlass && !string.IsNullOrEmpty(c.variant))
                .GroupBy(c => c.variant).Select(g => new GlassVariant(g.Key, g.First().length)).OrderBy(g => g.Name).ToList();
            garnishColors = FindObjectsByType<Garnish>(FindObjectsSortMode.None).Where(g => !string.IsNullOrEmpty(g.color))
                .Select(g => g.color).Distinct().OrderBy(c => c).ToList();

            ordersEnabled = true;
            if (!serve) { Debug.LogError($"[Moonlit] {name}: no ServeStation set, so drinks can't be served", this); ordersEnabled = false; }
            if (seats.Count == 0) { Debug.LogError($"[Moonlit] {name}: no customer seats set", this); ordersEnabled = false; }
            if (glasses.Count == 0) { Debug.LogError($"[Moonlit] {name}: no glasses in the scene, so customers can't order", this); ordersEnabled = false; }
            if (walkPath.Length < 2 || walkPath.Any(p => !p)) { Debug.LogError($"[Moonlit] {name}: the walk path needs at least a door and a bar spot", this); ordersEnabled = false; }

            foreach (var seat in seats)
            {
                if (!seat.body) continue;
                var profile = seat.profile;
                if (!profile) { profile = CustomerProfile.Fallback(seat.body.name); runtimeProfiles.Add(profile); }
                var s = new SeatState { Profile = profile, Body = seat.body, TicketSpot = seat.ticketSpot };
                s.Bubble = FloatingLabel.Create(seat.body, HeadHeight(seat.body) + 0.12f, 0.28f);
                if (seat.ticketSpot) s.Ticket = FloatingLabel.Create(seat.ticketSpot, 0.06f, 0.2f);
                states.Add(s);
                ShowTicket(states.Count - 1);
                if (ordersEnabled) seat.body.gameObject.SetActive(false);   // away until it's their turn
            }
            phaseUntil = Time.time + nextCustomerDelay;
            HookReset(true);

            Debug.Log($"[Moonlit] DrinkGame ready: {FindObjectsByType<IngredientSource>(FindObjectsSortMode.None).Length} ingredient props, " +
                      $"{DrinkContainer.All.Count(c => c.IsGlass)} glasses, {FindObjectsByType<Garnish>(FindObjectsSortMode.None).Length} garnishes, " +
                      $"{states.Count} customers.");
        }

        void OnEnable() => HookReset(true);
        void OnDisable() => HookReset(false);

        void OnDestroy()
        {
            foreach (var p in runtimeProfiles) if (p) Destroy(p);
            runtimeProfiles.Clear();
        }

        void HookReset(bool on)
        {
            if (!resetButton || on == resetHooked) return;
            resetHooked = on;
            if (on) resetButton.selectEntered.AddListener(OnResetPressed); else resetButton.selectEntered.RemoveListener(OnResetPressed);
        }

        void OnResetPressed(SelectEnterEventArgs _) => ResetAllProps();

        /// <summary>Every prop back where it started, including ones in a hand or behind the bar. Contents stay.</summary>
        public void ResetAllProps() => PropRespawn.ResetAll();

        static float HeadHeight(Transform body)
        {
            float top = 1f;
            foreach (var r in body.GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y - body.position.y);
            return top;
        }

        // ------------------------------------------------------------------ every frame
        void Update()
        {
            if (keyboardReset && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetAllProps();
            for (int i = 0; i < PropRespawn.All.Count; i++) PropRespawn.All[i].CheckLost();

            if (!ordersEnabled || states.Count == 0) return;
            switch (Phase)
            {
                case Visit.Away: if (Time.time >= phaseUntil) ComeIn(); break;
                case Visit.WalkingIn: if (Walk(+1)) Order(); break;
                case Visit.Served: if (Time.time >= phaseUntil) { Phase = Visit.WalkingOut; pathIndex = walkPath.Length - 2; } break;
                case Visit.WalkingOut: if (Walk(-1)) GoAway(); break;
            }
        }

        // ------------------------------------------------------------------ a customer's visit
        void ComeIn()
        {
            Current = nextCustomer;
            nextCustomer = (nextCustomer + 1) % states.Count;
            var body = states[Current].Body;
            body.SetPositionAndRotation(walkPath[0].position, Quaternion.LookRotation(Flat(walkPath[1].position - walkPath[0].position)));
            body.gameObject.SetActive(true);
            pathIndex = 1;
            walked = 0f;
            Phase = Visit.WalkingIn;
        }

        /// <summary>Moves the customer along the walk path, bobbing. True once they're past its end (+1) or its start (-1).</summary>
        bool Walk(int direction)
        {
            var body = states[Current].Body;
            Vector3 target = walkPath[pathIndex].position;
            Vector3 at = body.position; at.y = target.y;
            Vector3 to = target - at;
            float step = walkSpeed * Time.deltaTime;
            if (to.magnitude <= step) { at = target; pathIndex += direction; }
            else
            {
                at += to.normalized * step;
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(Flat(to)), 360f * Time.deltaTime);
            }
            walked += step;
            bool done = pathIndex < 0 || pathIndex >= walkPath.Length;
            float hop = done ? 0f : Mathf.Abs(Mathf.Sin(walked * hopsPerMetre * Mathf.PI)) * hopHeight;
            body.position = at + Vector3.up * hop;
            return done;
        }

        void Order()
        {
            var c = states[Current];
            if (playerSpot) c.Body.rotation = Quaternion.LookRotation(Flat(playerSpot.position - c.Body.position));
            Phase = Visit.AtBar;
            if (!OrderGenerator.TryGenerate(rng, glasses, garnishColors, out var order)) return;
            c.Order = order;
            c.Missed = false;
            Say(Current, Pick(c.Profile.greetings), GameVisuals.Ink, 6f);
            ShowTicket(Current);
        }

        void GoAway()
        {
            var c = states[Current];
            c.Bubble.Flash("", GameVisuals.Ink, 0f);
            c.Body.gameObject.SetActive(false);
            Current = -1;
            Phase = Visit.Away;
            phaseUntil = Time.time + nextCustomerDelay;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v : Vector3.forward; }

        void LateUpdate() => DrinkContainer.RefreshChanged();

        void FixedUpdate()
        {
            for (int i = 0; i < PropRespawn.All.Count; i++) PropRespawn.All[i].ApplyPendingHome();
        }

        // ------------------------------------------------------------------ orders
        /// <summary>Gives a customer this order (replacing theirs) and redraws their ticket. Used by the demo and tests.</summary>
        public void SetOrder(int seat, DrinkOrder order)
        {
            var c = states[seat];
            c.Order = order;
            c.Missed = false;
            ShowTicket(seat);
        }

        /// <summary>Redraws a customer's ticket from their current order.</summary>
        public void ShowTicket(int seat)
        {
            var c = states[seat];
            if (!c.Ticket) return;
            if (c.Order == null) { c.Ticket.SetTitle($"<b>{c.DisplayName}</b>\n<size=75%>...</size>"); c.Ticket.ShowBars(null); return; }
            var o = c.Order;
            var extras = new List<string> { o.Method.ToString(), o.GlassVariant };
            if (o.Cold) extras.Add("Cold");
            if (o.Color != null) extras.Add(o.Color + " garnish");
            string hint = c.Missed ? $"\n<size=60%><color=#BFE3FF>Tip: {o.Hint}</color></size>" : "";
            c.Ticket.SetTitle($"<b>{c.DisplayName}</b>\n<size=75%>{string.Join(" · ", extras)}</size>{hint}");
            c.Ticket.ShowBars(o.Target);
        }

        /// <summary>Shows a line in a customer's speech bubble.</summary>
        public void Say(int seat, string text, Color color, float seconds)
        {
            states[seat].Bubble.Flash(text, color, seconds);
        }

        /// <summary>Hands a drink over. Picks the open order it matches best and says how it went.</summary>
        public bool TryServe(ServedDrink drink, out string summary)
        {
            int best = -1, bestScore = int.MaxValue;
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].Order == null) continue;
                int score = Score(Finishing.Check(states[i].Order, drink));
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best < 0) { summary = "Nobody's waiting for a drink"; return false; }

            var c = states[best];
            var miss = Finishing.Check(c.Order, drink);
            if (miss == Mismatch.None)
            {
                ServedCount++;
                var story = c.Profile.story;
                string line = c.StoryIndex < story.Length ? story[c.StoryIndex++] : c.Profile.storyDone;
                Say(best, Pick(c.Profile.thanks) + "\n<size=85%>" + line + "</size>", GameVisuals.Good, 10f);
                c.Order = null;
                if (best == Current && Phase == Visit.AtBar) { Phase = Visit.Served; phaseUntil = Time.time + leaveDelay; }
                ShowTicket(best);
                summary = $"{c.DisplayName} loved it!";
                return true;
            }

            c.Missed = true;
            Say(best, Pick(c.Profile.wrong) + "\n<size=80%>" + Explain(c.Order, drink, miss) + "</size>", GameVisuals.Bad, 8f);
            ShowTicket(best);
            summary = $"Not quite right for {c.DisplayName}";
            return false;
        }

        static int Score(Mismatch m)
        {
            int s = 0;
            if ((m & Mismatch.Taste) != 0) s += 10;
            if ((m & Mismatch.Method) != 0) s += 4;
            if ((m & Mismatch.Glass) != 0) s += 3;
            if ((m & Mismatch.Cold) != 0) s += 1;
            if ((m & Mismatch.Color) != 0) s += 1;
            return s;
        }

        static string Explain(DrinkOrder o, ServedDrink d, Mismatch m)
        {
            var bits = new List<string>();
            if ((m & Mismatch.Taste) != 0)
            {
                if (d.Stats.Bitter != o.Target.Bitter) bits.Add(d.Stats.Bitter > o.Target.Bitter ? "too bitter" : "not bitter enough");
                if (d.Stats.Sweet != o.Target.Sweet) bits.Add(d.Stats.Sweet > o.Target.Sweet ? "too sweet" : "not sweet enough");
                if (d.Stats.Sour != o.Target.Sour) bits.Add(d.Stats.Sour > o.Target.Sour ? "too sour" : "not sour enough");
            }
            if ((m & Mismatch.Method) != 0) bits.Add(o.Method == PrepMethod.Layered ? "I wanted it layered" : "I wanted it mixed");
            if ((m & Mismatch.Glass) != 0) bits.Add($"in a {o.GlassVariant} glass, please");
            if ((m & Mismatch.Cold) != 0) bits.Add("with ice");
            if ((m & Mismatch.Color) != 0) bits.Add($"with a {o.Color} garnish");
            return "(" + string.Join(", ", bits) + ")";
        }

        string Pick(string[] lines) => lines.Length == 0 ? "" : lines[rng.Next(lines.Length)];
    }
}
