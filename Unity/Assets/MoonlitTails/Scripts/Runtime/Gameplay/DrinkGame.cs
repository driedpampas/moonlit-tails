using System.Collections.Generic;
using System.Linq;
using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// Turns the bar's props into the drink-making game when Play starts. It adds itself to any scene with a
    /// "MoonlitBar" object, so the scene file doesn't need editing (no scene merges).
    ///
    /// Which prop is which ingredient, glass size, etc. is all in the tables below. Change them freely: the props
    /// are found by name, and Tools > Moonlit Tails > 4. Check Drink Setup lists what matched.
    /// </summary>
    public class DrinkGame : MonoBehaviour
    {
        // ------------------------------------------------------------------ tables (edit these)
        /// <summary>Prop name prefix (after "PROP_") to ingredient. Each prop shows a label with its ingredient name.</summary>
        public static readonly (string prop, Ingredient ingredient)[] Sources =
        {
            // base drinks
            ("Bottle_Rum", Ingredients.Whiskey),
            ("Bottle_Vodka", Ingredients.Bourbon),
            ("Bottle_Tequila", Ingredients.Tequila),
            // bitter
            ("Fridge_Tonic", Ingredients.TonicWater),
            ("Bottle_TripleSec", Ingredients.OrangePeel),
            ("Bottle_Gin", Ingredients.Espresso),
            ("Bottle_Violet", Ingredients.BlackTea),
            // sweet
            ("Bottle_PeachSyrup", Ingredients.Syrup),
            ("Fridge_Cola", Ingredients.Cola),
            ("Fridge_Milk", Ingredients.DarkCaramel),
            ("Bottle_Grenadine", Ingredients.Grenadine),
            // sour
            ("Fridge_Soda", Ingredients.LimeJuice),
            ("Fridge_Juice", Ingredients.AppleCider),
            ("Bottle_MelonLiqueur", Ingredients.GrapefruitAcid),
            ("Bottle_BlueCuracao", Ingredients.SourMix),
        };

        /// <summary>Glass type (the word after "PROP_Glass_") to length. Short holds 3 units, Medium 4, Long 5.</summary>
        public static readonly Dictionary<string, GlassLength> GlassLengths = new Dictionary<string, GlassLength>
        {
            ["Shot"] = GlassLength.Short,
            ["Rocks"] = GlassLength.Medium,
            ["Mug"] = GlassLength.Medium,
            ["Coupe"] = GlassLength.Medium,
            ["Highball"] = GlassLength.Long,
            ["Martini"] = GlassLength.Long,
            ["Wine"] = GlassLength.Long,
        };

        public const string StirCupProp = "PROP_Blender_Jar";
        public const string ShakeMixerProp = "PROP_Shaker_Tin";
        public const string SpoonProp = "PROP_BarSpoon";
        public const string IceScoopProp = "PROP_IceScoop";
        public const string IceWell = "ENV_IceWell";
        public const string BellProp = "PROP_ServiceBell";
        public const string ServeSpot = "MK_ServeSpot";
        public const string PlayerSpot = "MK_PlayerStart";
        public const string MenuBoard = "DECO_MenuBoard";
        public static readonly string[] Tickets = { "PROP_OrderTicket_01", "PROP_OrderTicket_02", "PROP_OrderTicket_03" };

        // ------------------------------------------------------------------ boot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DrinkGame>() != null) return;
            if (GameObject.Find("MoonlitBar") == null) return;
            new GameObject("Drink Game").AddComponent<DrinkGame>();
        }

        public OrderBook Orders { get; private set; }

        void Start()
        {
            var bar = GameObject.Find("MoonlitBar");
            if (!bar) { Debug.LogWarning("[Moonlit] DrinkGame: no MoonlitBar in the scene"); return; }
            var plan = Plan(bar.transform);
            foreach (var w in plan.warnings) Debug.LogWarning("[Moonlit] DrinkGame: " + w);

            foreach (var (t, ing) in plan.sources) t.gameObject.AddComponent<IngredientSource>().Init(ing);
            foreach (var (t, type) in plan.glasses) t.gameObject.AddComponent<DrinkContainer>().Init(ContainerKind.Glass, type, GlassLengths[type]);
            if (plan.stirCup) plan.stirCup.gameObject.AddComponent<DrinkContainer>().Init(ContainerKind.StirCup, "Stir Cup", GlassLength.Long);
            if (plan.shaker) plan.shaker.gameObject.AddComponent<DrinkContainer>().Init(ContainerKind.ShakeMixer, "Shake Mixer", GlassLength.Long);
            foreach (var (t, color) in plan.garnishes) t.gameObject.AddComponent<Garnish>().Init(color);
            if (plan.spoon) plan.spoon.gameObject.AddComponent<StirTool>().Init();
            if (plan.scoop && plan.iceWell) plan.scoop.gameObject.AddComponent<IceScoop>().Init(plan.iceWell);

            var glassTypes = plan.glasses.Select(g => g.type).Distinct().Select(n => new GlassVariant(n, GlassLengths[n])).ToList();
            var colors = plan.garnishes.Select(g => g.color).Distinct().ToList();
            var layout = FindFirstObjectByType<BarLayout>();
            var people = layout ? layout.characters.Where(c => c.character).Select(c => (c.id, c.character)).ToList()
                                : new List<(string, Transform)>();

            Orders = gameObject.AddComponent<OrderBook>();
            Orders.Init(people, plan.tickets, glassTypes, colors);
            if (plan.serveSpot && plan.player)
                gameObject.AddComponent<ServeStation>().Init(plan.serveSpot, plan.player, plan.bell, Orders);
            if (plan.serveSpot && plan.player) ResetButton.Create(transform, plan.player, plan.serveSpot);
            if (plan.menuBoard && plan.player) WriteMenuBoard(plan.menuBoard, plan.player);

            Debug.Log($"[Moonlit] DrinkGame ready: {plan.sources.Count} ingredient props, {plan.glasses.Count} glasses, " +
                      $"{plan.garnishes.Count} garnishes, {people.Count} customers.");
        }

        // ------------------------------------------------------------------ finding props by name
        public class SetupPlan
        {
            public readonly List<(Transform t, Ingredient ingredient)> sources = new List<(Transform, Ingredient)>();
            public readonly List<(Transform t, string type)> glasses = new List<(Transform, string)>();
            public readonly List<(Transform t, string color)> garnishes = new List<(Transform, string)>();
            public readonly List<Transform> tickets = new List<Transform>();
            public Transform stirCup, shaker, spoon, scoop, iceWell, bell, serveSpot, player, menuBoard;
            public readonly List<string> warnings = new List<string>();
        }

        /// <summary>Works out which object gets which role, without changing anything (also used by the editor check).</summary>
        public static SetupPlan Plan(Transform bar)
        {
            var plan = new SetupPlan();
            var all = bar.GetComponentsInChildren<Transform>(true);
            Transform Named(string n) => all.FirstOrDefault(t => t.name == n);
            bool Grabbable(Transform t) => t.GetComponent<XRGrabInteractable>() && t.GetComponent<Rigidbody>();

            foreach (var (prop, ing) in Sources)
            {
                var found = all.Where(t => t.name.StartsWith("PROP_" + prop) && t.GetComponent<MeshFilter>()).ToList();
                if (found.Count == 0) plan.warnings.Add($"no prop for {ing.DisplayName} (PROP_{prop}*)");
                foreach (var t in found)
                {
                    if (Grabbable(t)) plan.sources.Add((t, ing));
                    else plan.warnings.Add($"{t.name} isn't grabbable (rebuild the scene?)");
                }
            }
            foreach (var t in all.Where(t => t.name.StartsWith("PROP_Glass_") && t.GetComponent<MeshFilter>()))
            {
                string type = t.name.Substring("PROP_Glass_".Length).Split('_')[0];
                if (!GlassLengths.ContainsKey(type)) { plan.warnings.Add($"{t.name}: no length set for glass type '{type}'"); continue; }
                if (Grabbable(t)) plan.glasses.Add((t, type));
            }
            foreach (var t in all.Where(t => t.name.StartsWith("PROP_Garnish_") && t.GetComponent<MeshFilter>()))
                if (Grabbable(t)) plan.garnishes.Add((t, t.name.Substring("PROP_Garnish_".Length).Split('_')[0]));

            Transform Need(string n, bool grab)
            {
                var t = Named(n);
                if (!t) plan.warnings.Add($"missing {n}");
                else if (grab && !Grabbable(t)) { plan.warnings.Add($"{n} isn't grabbable"); return null; }
                return t;
            }
            plan.stirCup = Need(StirCupProp, true);
            plan.shaker = Need(ShakeMixerProp, true);
            plan.spoon = Need(SpoonProp, true);
            plan.scoop = Need(IceScoopProp, true);
            plan.iceWell = Need(IceWell, false);
            plan.bell = Need(BellProp, false);
            plan.serveSpot = Need(ServeSpot, false);
            plan.player = Need(PlayerSpot, false);
            plan.menuBoard = Named(MenuBoard);
            foreach (var n in Tickets) { var t = Named(n); if (t) plan.tickets.Add(t); else plan.warnings.Add($"missing {n}"); }
            return plan;
        }

        // ------------------------------------------------------------------ how-to-play on the menu board
        /// <summary>
        /// Writes the rules on the chalkboard: flat on its face, inside the wooden frame, as big as fits. A dark panel
        /// goes under the text to hide the painted menu lines.
        /// </summary>
        static void WriteMenuBoard(Transform board, Transform player)
        {
            // the board's own box (its mesh bounds, turned with it); world bounds if the mesh was batched into another
            var mf = board.GetComponent<MeshFilter>();
            bool own = mf && mf.sharedMesh && !mf.sharedMesh.name.StartsWith("Combined Mesh");
            Bounds box = own ? mf.sharedMesh.bounds : GameVisuals.WorldBounds(board);
            Vector3 Side(Vector3 axis) => own ? board.TransformVector(Vector3.Scale(axis, box.extents)) : Vector3.Scale(axis, box.extents);
            Vector3 center = own ? board.TransformPoint(box.center) : box.center;

            // the board is a flat box: its thinnest side is the face normal, the side closest to vertical is its height
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            var sides = axes.Select(a => (dir: Side(a), a)).ToArray();
            var thin = sides.OrderBy(x => x.dir.magnitude).First();
            var rest = sides.Where(x => x.a != thin.a).OrderByDescending(x => Mathf.Abs(Vector3.Dot(x.dir.normalized, Vector3.up))).ToArray();
            float halfThick = thin.dir.magnitude, height = rest[0].dir.magnitude * 2f, width = rest[1].dir.magnitude * 2f;
            if (halfThick < 1e-4f || width < 0.1f) return;

            Vector3 normal = thin.dir.normalized;
            if (Vector3.Dot(normal, player.position - center) < 0) normal = -normal;   // the side facing the room
            Vector3 up = Vector3.ProjectOnPlane(rest[0].dir, normal).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0) up = -up;

            var root = new GameObject("How To Play").transform;
            root.SetPositionAndRotation(center, Quaternion.LookRotation(-normal, up));   // text and quads face local -Z

            // the chalk area inside the frame (1.1 x 0.75 board, 0.98 x 0.63 chalk in the model)
            float w = width * 0.89f, h = height * 0.84f;
            var slate = GameVisuals.Primitive(PrimitiveType.Quad, root, "Slate", new Color(0.13f, 0.17f, 0.2f));
            slate.transform.localPosition = new Vector3(0, 0, -(halfThick + 0.003f));
            slate.transform.localScale = new Vector3(w, h, 1f);

            var text = GameVisuals.Text(root, "Rules", 0.6f, TextAlignmentOptions.TopLeft);
            text.transform.localPosition = new Vector3(0, 0, -(halfThick + 0.006f));
            text.rectTransform.sizeDelta = new Vector2(w - 0.06f, h - 0.05f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.15f;
            text.fontSizeMax = 1f;
            text.lineSpacing = -10f;
            text.text =
                "<b>HOW TO MAKE A DRINK</b>\n" +
                "1. Read a ticket\n" +
                "2. Pour into <b>Stir Cup</b> or <b>Shake Mixer</b>\n" +
                "3. Same density: <b>stir</b>. Different: <b>shake</b>\n" +
                "4. <b>Mixed</b>: Shake Mixer into the glass\n" +
                "    <b>Layered</b>: Stir Cup, one tip per layer\n" +
                "5. Ice for Cold, fruit on the rim for colour\n" +
                "6. Glass on the coaster, ring the bell\n" +
                "<color=#ff7a7a>Red button: everything back in place</color>";
        }
    }
}
