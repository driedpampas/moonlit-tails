using System;
using System.Collections.Generic;
using System.Linq;
using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// The night at the bar: the customers and their orders (one ticket per customer on the ticket rail, a speech
    /// bubble over each customer, the check when a drink is served), the reset button, and putting props back when
    /// they leave the bar. A correct drink tells the next line of that customer's story.
    ///
    /// It lives on the "Drink Game" object in the scene; the customers, their tickets and the player spot are set
    /// on it in the Inspector. The props carry their own gameplay components, set in their prefabs.
    ///
    /// Transition: until the scene has its own serve panel, reset button and rules signs, the game builds them when
    /// Play starts, finding the serve spot, bell and menu board by name (see <see cref="Plan"/>).
    /// </summary>
    public class DrinkGame : MonoBehaviour
    {
        /// <summary>A customer's place: who sits there and which ticket on the rail is theirs.</summary>
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
            public string DisplayName => Profile ? Profile.displayName : "Customer";
            public Transform Body { get; internal set; }
            public Transform TicketSpot { get; internal set; }
            public FloatingLabel Bubble { get; internal set; }
            public FloatingLabel Ticket { get; internal set; }
            public DrinkOrder Order { get; internal set; }
            public int StoryIndex { get; internal set; }
            public bool Missed { get; internal set; }
            internal float nextOrderAt;
        }

        [Header("Customers")]
        [SerializeField] List<Seat> seats = new List<Seat>();
        [Tooltip("Seconds between a served drink and that customer's next order.")]
        public float newOrderDelay = 5f;

        [Header("Scene")]
        [SerializeField] ServeStation serve;
        [Tooltip("Where the player stands (MK_PlayerStart).")]
        [SerializeField] Transform playerSpot;
        [SerializeField] IceScoop iceScoop;
        [Tooltip("Pressing it puts every prop back where it started.")]
        [SerializeField] XRBaseInteractable resetButton;
        [Tooltip("The rules sign on the back wall.")]
        [SerializeField] Transform rulesSign;
        [Tooltip("The rules written on the chalkboard.")]
        [SerializeField] Transform menuBoardText;
        [Tooltip("Editor/dev shortcut: R on the keyboard resets the props too.")]
        [SerializeField] bool keyboardReset = true;

        public IReadOnlyList<SeatState> Seats => states;
        public int ServedCount { get; private set; }
        public ServeStation Station => serve;
        public Transform PlayerSpot => playerSpot;
        public IceScoop IceScoop => iceScoop;
        public Transform RulesSign => rulesSign;
        public Transform MenuBoardText => menuBoardText;
        public Transform ResetButtonObject => resetButtonObject ? resetButtonObject : resetButton ? resetButton.transform : null;

        readonly List<SeatState> states = new List<SeatState>();
        readonly List<CustomerProfile> runtimeProfiles = new List<CustomerProfile>();
        IReadOnlyList<GlassVariant> glasses = new GlassVariant[0];
        IReadOnlyList<string> garnishColors = new string[0];
        System.Random rng;
        bool ordersEnabled, resetHooked;
        Transform resetButtonObject;   // transition: the runtime-built reset button

        // ------------------------------------------------------------------ transition: scene object names
        public const string BellPropName = "PROP_ServiceBell";
        public const string ServeSpotName = "MK_ServeSpot";
        public const string PlayerSpotName = "MK_PlayerStart";
        public const string MenuBoardName = "DECO_MenuBoard";

        // ------------------------------------------------------------------ start
        void Start()
        {
            if (!serve || !resetButton || !rulesSign || !menuBoardText) BuildFromNames();
            rng = new System.Random();
            glasses = FindObjectsByType<DrinkContainer>(FindObjectsSortMode.None).Where(c => c.IsGlass && !string.IsNullOrEmpty(c.variant))
                .GroupBy(c => c.variant).Select(g => new GlassVariant(g.Key, g.First().length)).OrderBy(g => g.Name).ToList();
            garnishColors = FindObjectsByType<Garnish>(FindObjectsSortMode.None).Where(g => !string.IsNullOrEmpty(g.color))
                .Select(g => g.color).Distinct().OrderBy(c => c).ToList();

            ordersEnabled = true;
            if (!serve) { Debug.LogError($"[Moonlit] {name}: no ServeStation set, so drinks can't be served", this); ordersEnabled = false; }
            if (seats.Count == 0) { Debug.LogError($"[Moonlit] {name}: no customer seats set", this); ordersEnabled = false; }
            if (glasses.Count == 0) { Debug.LogError($"[Moonlit] {name}: no glasses in the scene, so customers can't order", this); ordersEnabled = false; }

            int i = 0;
            foreach (var seat in seats)
            {
                if (!seat.body) continue;
                var profile = seat.profile;
                if (!profile) { profile = CustomerProfile.Fallback(seat.body.name); runtimeProfiles.Add(profile); }
                var s = new SeatState { Profile = profile, Body = seat.body, TicketSpot = seat.ticketSpot };
                s.Bubble = FloatingLabel.Create(seat.body, HeadHeight(seat.body) + 0.12f, 0.28f);
                if (seat.ticketSpot) s.Ticket = FloatingLabel.Create(seat.ticketSpot, 0.06f, 0.2f);
                s.nextOrderAt = Time.time + 1.5f + i * 2.5f;   // customers order one after another
                states.Add(s);
                ShowTicket(states.Count - 1);
                i++;
            }
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

            if (!ordersEnabled) return;
            for (int i = 0; i < states.Count; i++)
            {
                var c = states[i];
                if (c.Order != null || Time.time < c.nextOrderAt) continue;
                if (!OrderGenerator.TryGenerate(rng, glasses, garnishColors, out var order)) continue;
                c.Order = order;
                c.Missed = false;
                Say(i, Pick(c.Profile.greetings), GameVisuals.Ink, 6f);
                ShowTicket(i);
            }
        }

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
            var c = states[seat];
            if (!c.Bubble) return;
            c.Bubble.SetTitle("");
            c.Bubble.Flash(text, color, seconds);
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
                var story = c.Profile.story ?? new string[0];
                string line = c.StoryIndex < story.Length ? story[c.StoryIndex++] : c.Profile.storyDone;
                Say(best, Pick(c.Profile.thanks) + "\n<size=85%>" + line + "</size>", GameVisuals.Good, 10f);
                c.Order = null;
                c.nextOrderAt = Time.time + newOrderDelay + 8f;
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

        string Pick(string[] lines) => lines == null || lines.Length == 0 ? "" : lines[rng.Next(lines.Length)];

        // ------------------------------------------------------------------ transition: finding props by name
        /// <summary>Transition: builds whichever of the serve panel, reset button and rules signs aren't in the scene yet.</summary>
        void BuildFromNames()
        {
            var bar = GameObject.Find("MoonlitBar");
            if (!bar) { Debug.LogWarning("[Moonlit] DrinkGame: no MoonlitBar in the scene"); return; }
            var plan = Plan(bar.transform);
            foreach (var w in plan.warnings) Debug.LogWarning("[Moonlit] DrinkGame: " + w);
            if (!playerSpot) playerSpot = plan.player;
            if (!iceScoop) iceScoop = bar.GetComponentInChildren<IceScoop>(true);

            if (plan.serveSpot && plan.player)
            {
                if (!serve)
                {
                    serve = gameObject.AddComponent<ServeStation>();
                    serve.Init(plan.serveSpot, plan.player, plan.bell ? plan.bell.GetComponent<XRBaseInteractable>() : null, this);
                }
                if (!resetButton)
                {
                    var reset = ResetButton.Create(transform, plan.player, plan.serveSpot);
                    if (reset) resetButtonObject = reset.transform;
                }
                if (!rulesSign) PutRulesOnBackWall(transform, bar.transform, plan.player, plan.serveSpot);
            }
            if (!menuBoardText && plan.menuBoard && plan.player) WriteMenuBoard(plan.menuBoard, plan.player);
        }

        public class SetupPlan
        {
            public Transform bell, serveSpot, player, menuBoard;
            public readonly List<string> warnings = new List<string>();
        }

        /// <summary>Transition: finds the serve spot, player spot, bell and menu board by name.</summary>
        public static SetupPlan Plan(Transform bar)
        {
            var plan = new SetupPlan();
            var all = bar.GetComponentsInChildren<Transform>(true);
            Transform Named(string n) => all.FirstOrDefault(t => t.name == n);
            Transform Need(string n)
            {
                var t = Named(n);
                if (!t) plan.warnings.Add($"missing {n}");
                return t;
            }
            plan.bell = Need(BellPropName);
            plan.serveSpot = Need(ServeSpotName);
            plan.player = Need(PlayerSpotName);
            plan.menuBoard = Named(MenuBoardName);
            return plan;
        }

        // ------------------------------------------------------------------ transition: rules text built from code
        void WriteMenuBoard(Transform board, Transform player)
        {
            if (!TryFace(board, player.position, out var f)) return;
            var root = new GameObject("How To Play").transform;
            root.SetPositionAndRotation(f.center, Quaternion.LookRotation(-f.normal, f.up));   // text and quads face local -Z

            // the chalk area inside the frame (1.1 x 0.75 board, 0.98 x 0.63 chalk in the model)
            WriteRules(root, f.width * 0.89f, f.height * 0.84f, f.halfThick);
            menuBoardText = root;
        }

        struct Face { public Vector3 center, normal, up; public float halfThick, width, height; }

        /// <summary>
        /// The front of a flat piece (a board, a disc on the wall): its centre, the side facing the viewer, which way
        /// is up, and its size. Uses the mesh's own box turned with the piece, or world bounds if the mesh was batched.
        /// </summary>
        static bool TryFace(Transform piece, Vector3 viewer, out Face f)
        {
            f = default;
            var mf = piece.GetComponent<MeshFilter>();
            bool own = mf && mf.sharedMesh && !mf.sharedMesh.name.StartsWith("Combined Mesh");
            Bounds box = own ? mf.sharedMesh.bounds : GameVisuals.WorldBounds(piece);
            Vector3 Side(Vector3 axis) => own ? piece.TransformVector(Vector3.Scale(axis, box.extents)) : Vector3.Scale(axis, box.extents);
            f.center = own ? piece.TransformPoint(box.center) : box.center;

            // the thinnest side is the face normal, the side closest to vertical is the height
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            var sides = axes.Select(a => (dir: Side(a), a)).ToArray();
            var thin = sides.OrderBy(x => x.dir.magnitude).First();
            var rest = sides.Where(x => x.a != thin.a).OrderByDescending(x => Mathf.Abs(Vector3.Dot(x.dir.normalized, Vector3.up))).ToArray();
            f.halfThick = thin.dir.magnitude; f.height = rest[0].dir.magnitude * 2f; f.width = rest[1].dir.magnitude * 2f;
            if (f.halfThick < 1e-4f || f.width < 0.1f) return false;

            f.normal = thin.dir.normalized;
            if (Vector3.Dot(f.normal, viewer - f.center) < 0) f.normal = -f.normal;   // the side facing the room
            f.up = Vector3.ProjectOnPlane(rest[0].dir, f.normal).normalized;
            if (Vector3.Dot(f.up, Vector3.up) < 0) f.up = -f.up;
            return true;
        }

        public const string RulesText =
            "<b>HOW TO MAKE A DRINK</b>\n" +
            "1. Read a ticket\n" +
            "2. Pour into <b>Stir Cup</b> or <b>Shake Mixer</b>\n" +
            "3. Same density: <b>stir</b>. Different: <b>shake</b>\n" +
            "4. <b>Mixed</b>: Shake Mixer into the glass\n" +
            "    <b>Layered</b>: Stir Cup, one tip per layer\n" +
            "5. Ice: Cold. Fruit on the rim: colour\n" +
            "6. Glass on the coaster, ring the bell\n" +
            "<color=#ff7a7a>Red button: everything back in place</color>";

        /// <summary>A dark slate with the rules on it, as big as fits, <paramref name="front"/> metres in front of <paramref name="root"/> (which faces local -Z).</summary>
        static void WriteRules(Transform root, float w, float h, float front)
        {
            var slate = GameVisuals.Primitive(PrimitiveType.Quad, root, "Slate", new Color(0.13f, 0.17f, 0.2f));
            slate.transform.localPosition = new Vector3(0, 0, -(front + 0.003f));
            slate.transform.localScale = new Vector3(w, h, 1f);

            var text = GameVisuals.Text(root, "Rules", 0.6f, TextAlignmentOptions.Left);
            text.transform.localPosition = new Vector3(0, 0, -(front + 0.006f));
            text.rectTransform.sizeDelta = new Vector2(w - w * 0.06f, h - h * 0.08f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.1f;
            text.fontSizeMax = 1f;
            text.lineSpacing = -10f;
            text.text = RulesText;
        }

        /// <summary>The round logo on the back wall behind the bar (medallion and neon glass). The rules sign takes its place.</summary>
        public static readonly string[] LogoPieces = { "DECO_Medallion", "DECO_Neon_Glass_Back", "DECO_Neon_Olive" };

        /// <summary>
        /// The wall board is too far away and too side-on to read from behind the bar, so the same rules go on a framed
        /// sign on the back wall right behind the player (about 1.1 m away), where the round logo was. The logo is
        /// hidden while playing; it's still in the bar prefab.
        /// </summary>
        void PutRulesOnBackWall(Transform parent, Transform bar, Transform player, Transform serveSpot)
        {
            var all = bar.GetComponentsInChildren<Transform>(true);
            var logo = all.FirstOrDefault(t => t.name == LogoPieces[0]);
            if (!logo || !TryFace(logo, player.position, out var f))
            {
                Debug.LogWarning("[Moonlit] DrinkGame: no " + LogoPieces[0] + " on the back wall, so no rules sign behind the bar");
                return;
            }
            Vector3 reach = serveSpot.position - player.position; reach.y = 0;
            float k = reach.magnitude / ResetButton.ModelPlayerToServe;   // follow the bar's scale
            if (k < 0.1f) k = 1f;

            const float w = 1.04f, h = 0.6f, frame = 0.03f, depth = 0.02f;
            var root = new GameObject("How To Play Sign").transform;
            root.SetParent(parent, false);
            // flat on the wall: the medallion's back sits on the wall
            Vector3 onWall = f.center - f.normal * f.halfThick + f.normal * (depth * 0.5f + 0.002f) * k;
            root.SetPositionAndRotation(onWall, Quaternion.LookRotation(-f.normal, Vector3.up));
            root.localScale = Vector3.one * k;

            var wood = GameVisuals.Primitive(PrimitiveType.Cube, root, "Frame", new Color(0.34f, 0.2f, 0.12f));
            wood.transform.localScale = new Vector3(w + frame * 2f, h + frame * 2f, depth);
            WriteRules(root, w, h, depth * 0.5f);
            rulesSign = root;

            foreach (var t in all)
                if (LogoPieces.Contains(t.name)) t.gameObject.SetActive(false);
        }
    }
}
