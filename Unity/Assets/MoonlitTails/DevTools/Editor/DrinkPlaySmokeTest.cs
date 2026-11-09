using System;
using System.Collections.Generic;
using System.Linq;
using MoonlitTails.Drinks;
using MoonlitTails.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Plays the bar scene and makes drinks by moving the real props around (no headset needed): pours, a wrong and a
    /// right mix, stirring with the spoon, a mixed and a layered glass, ice, garnish, adjustments and serving.
    /// Tools > Moonlit Tails > Automation > Play Smoke Test, or from the command line:
    /// Unity -batchmode -projectPath Unity -executeMethod MoonlitTails.EditorTools.DrinkPlaySmokeTest.RunBatch
    /// (exit code 0 = everything passed). It never saves the scene: the props it moves go back when Play stops.
    /// </summary>
    [InitializeOnLoad]
    public static class DrinkPlaySmokeTest
    {
        const string Key = "MoonlitDrinkSmoke";
        const string BatchKey = "MoonlitDrinkSmokeBatch";

        static DrinkPlaySmokeTest()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Moonlit Tails/Automation/Play Smoke Test")]
        public static void Run() => Start(false);

        public static void RunBatch() => Start(true);

        static void Start(bool batch)
        {
            if (!HeadlessRun.OpenBarScene(batch)) return;
            if (batch) HeadlessRun.SwitchOffSimulator("Smoke test");
            SessionState.SetBool(Key, true);
            SessionState.SetBool(BatchKey, batch);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        // ------------------------------------------------------------------ a tiny script of timed steps
        static List<(float wait, string name, Action act)> steps;
        static int index;
        static float nextAt = -1f;
        static readonly List<string> failures = new List<string>();
        static int passes;

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (steps == null)
            {
                if (Time.time < 2f || UnityEngine.Object.FindFirstObjectByType<DrinkGame>() == null) return;
                steps = BuildSteps();
                index = 0;
                nextAt = Time.time;
            }
            if (Time.time < nextAt) return;
            if (index >= steps.Count) { Finish(); return; }
            var s = steps[index++];
            try { s.act(); }
            catch (Exception e) { failures.Add($"{s.name}: threw {e.GetType().Name}: {e.Message}"); Debug.LogException(e); }
            nextAt = Time.time + (index < steps.Count ? steps[index].wait : 0.1f);
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            string result = $"[Moonlit] Smoke test: {passes} passed, {failures.Count} failed" +
                            (failures.Count > 0 ? "\n  " + string.Join("\n  ", failures) : "");
            if (failures.Count > 0) Debug.LogError(result); else Debug.Log(result);
            bool batch = SessionState.GetBool(BatchKey, false);
            steps = null;
            HeadlessRun.End(batch, failures.Count == 0);
        }

        static void Expect(bool ok, string what)
        {
            if (ok) { passes++; Debug.Log("[Moonlit] PASS " + what); }
            else { failures.Add(what); Debug.LogError("[Moonlit] FAIL " + what); }
        }

        // ------------------------------------------------------------------ helpers
        static DrinkContainer Vessel(ContainerKind k) => DrinkContainer.All.First(c => c.kind == k);
        static DrinkContainer GlassOf(string variant) => DrinkContainer.All.First(c => c.IsGlass && c.variant == variant);
        static IngredientSource Source(Ingredient i) => UnityEngine.Object.FindObjectsByType<IngredientSource>(FindObjectsSortMode.None).First(s => s.Ingredient == i);

        static readonly Dictionary<Transform, Quaternion> upright = new Dictionary<Transform, Quaternion>();

        static void Hold(Component c)
        {
            var rb = c.GetComponent<Rigidbody>();
            if (rb) { if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
            if (!upright.ContainsKey(c.transform)) upright[c.transform] = c.transform.rotation;
        }

        static void Place(Component c, Vector3 pos)
        {
            Hold(c);
            c.transform.rotation = upright[c.transform];
            c.transform.position = pos;
            Physics.SyncTransforms();
        }

        /// <summary>Tip a prop so its spout is just above a container's mouth.</summary>
        static void TipOver(Component c, DrinkContainer into)
        {
            Hold(c);
            var spout = c.GetComponent<PourSpout>();
            c.transform.rotation = Quaternion.AngleAxis(130f, Vector3.right) * upright[c.transform];
            Vector3 target = into.OpeningWorld + Vector3.up * 0.12f;
            c.transform.position += target - spout.SpoutWorld;
            Physics.SyncTransforms();
        }

        static void TipOverNothing(Component c, Vector3 where)
        {
            Hold(c);
            var spout = c.GetComponent<PourSpout>();
            c.transform.rotation = Quaternion.AngleAxis(130f, Vector3.right) * upright[c.transform];
            c.transform.position += where - spout.SpoutWorld;
            Physics.SyncTransforms();
        }

        static Transform Find(string n) => GameObject.Find("MoonlitBar").GetComponentsInChildren<Transform>(true).First(t => t.name == n);

        // ------------------------------------------------------------------ the script
        static List<(float, string, Action)> BuildSteps()
        {
            var serve = Find(DrinkGame.ServeSpot).position;
            Vector3 right = Vector3.right * 0.35f, up = Vector3.up * 0.25f;
            Vector3 work = serve + up;   // a work area floating above the counter, out of the way
            var shaker = Vessel(ContainerKind.ShakeMixer);
            var cup = Vessel(ContainerKind.StirCup);
            var highball = GlassOf("Highball");
            var rocks = GlassOf("Rocks");
            var game = UnityEngine.Object.FindFirstObjectByType<DrinkGame>();
            var station = UnityEngine.Object.FindFirstObjectByType<ServeStation>();
            var whiskey = Source(Ingredients.Whiskey);
            var cola = Source(Ingredients.Cola);
            var tonic = Source(Ingredients.TonicWater);
            var away = work + Vector3.up * 1.5f;

            return new List<(float, string, Action)>
            {
                (0f, "set up", () =>
                {
                    Place(shaker, work);
                    Place(cup, work + right);
                    Place(highball, work - right);
                    Place(rocks, work - right * 2f);
                    Expect(shaker.IsEmpty && cup.IsEmpty && highball.IsEmpty, "containers start empty");
                    Expect(game.Orders.Customers.Count == 3, "three customers");
                }),
                (0.2f, "pour whiskey", () => TipOver(whiskey, shaker)),
                (0.9f, "check whiskey", () =>
                {
                    Expect(shaker.PartCount == 1 && shaker.Units == 1, "one tip pours 1 unit into the shaker");
                    Place(whiskey, away);
                }),
                (0.2f, "pour whiskey again", () => TipOver(whiskey, shaker)),
                (0.9f, "check self-mix", () =>
                {
                    Expect(shaker.PartCount == 1, "the same ingredient can't go in twice");
                    Place(whiskey, away);
                }),
                (0.2f, "pour cola", () => TipOver(cola, shaker)),
                (0.9f, "wrong mix", () =>
                {
                    Expect(shaker.PartCount == 2, "cola makes two unmixed parts");
                    Place(cola, away + right);
                    shaker.TryMix(MixMethod.Stir);
                    Expect(shaker.PartCount == 2, "stirring a shake-only pair does nothing");
                    shaker.TryMix(MixMethod.Shake);
                    Expect(shaker.PartCount == 1 && shaker.Units == 2, "shaking combines them (2 units)");
                }),
                (0.2f, "shaker into highball", () => TipOver(shaker, highball)),
                (0.9f, "check mixed glass", () =>
                {
                    Expect(!highball.IsEmpty && highball.Method == PrepMethod.Mixed && highball.Units == 2, "the Shake Mixer pours a whole mixed drink");
                    Expect(highball.FinalStats == new Taste(2, 2, 0), "whiskey + cola = (2, 2, 0)");
                    Expect(shaker.IsEmpty, "the shaker is empty after pouring");
                    Place(shaker, work);
                }),
                (0.2f, "fill ice scoop", () =>
                {
                    var scoop = UnityEngine.Object.FindFirstObjectByType<IceScoop>();
                    var well = GameVisuals.WorldBounds(Find(DrinkGame.IceWell)).center;
                    Hold(scoop);
                    scoop.transform.position += well - scoop.GetComponent<Renderer>().bounds.center;
                }),
                (0.3f, "scoop to glass", () =>
                {
                    var scoop = UnityEngine.Object.FindFirstObjectByType<IceScoop>();
                    scoop.transform.position += highball.OpeningWorld + Vector3.up * 0.02f - scoop.GetComponent<Renderer>().bounds.center;
                }),
                (0.3f, "check ice, garnish", () =>
                {
                    Expect(highball.Cold, "the ice scoop makes the drink Cold");
                    var scoop = UnityEngine.Object.FindFirstObjectByType<IceScoop>();
                    Place(scoop, away - right);
                    var lime = UnityEngine.Object.FindObjectsByType<Garnish>(FindObjectsSortMode.None).First(g => g.color == "Lime");
                    highball.AttachGarnish(lime);
                    Expect(highball.Garnish == lime, "a lime garnish sits on the rim");
                }),
                (0.2f, "serve", () =>
                {
                    Place(highball, serve + Vector3.up * 0.01f);
                    var c = game.Orders.Customers[0];
                    c.order = new DrinkOrder { Target = new Taste(2, 2, 0), Method = PrepMethod.Mixed, GlassVariant = "Highball", Cold = true, Color = "Lime", Hint = "test" };
                }),
                (0.3f, "ring", () =>
                {
                    int before = game.Orders.Served;
                    station.Serve();
                    Expect(game.Orders.Served == before + 1, "a correct drink is accepted");
                    Expect(highball.IsEmpty && !highball.Cold && highball.Garnish == null, "the served glass is emptied and sent home");
                    Expect(game.Orders.Customers[0].order == null && game.Orders.Customers[0].storyIndex == 1, "the customer tells the next story line");
                }),
                (0.2f, "stir cup: whiskey", () => { Place(whiskey, away); TipOver(whiskey, cup); }),
                (0.9f, "stir cup: tonic", () => { Place(whiskey, away); TipOver(tonic, cup); }),
                (0.9f, "spoon", () =>
                {
                    Place(tonic, away + right);
                    Expect(cup.PartCount == 2, "whiskey and tonic in the stir cup");
                    Hold(UnityEngine.Object.FindFirstObjectByType<StirTool>());
                }),
            }
            .Concat(SpoonCircles(cup))
            .Concat(new List<(float, string, Action)>
            {
                (0.1f, "check stir", () =>
                {
                    Expect(cup.PartCount == 1 && cup.Units == 2, "swirling the spoon in the cup stirs it");
                    var spoon = UnityEngine.Object.FindFirstObjectByType<StirTool>();
                    Place(spoon, away + right * 2f);
                }),
                (0.2f, "layer 1", () => TipOver(cup, rocks)),
                (0.9f, "layer 2", () => { Place(cup, work + right); }),
                (0.2f, "layer 2 pour", () => TipOver(cup, rocks)),
                (0.9f, "check layers", () =>
                {
                    Place(cup, work + right);
                    Expect(rocks.Method == PrepMethod.Layered && rocks.Units == 2, "the Stir Cup pours one layer per tip");
                    Expect(rocks.FinalStats == new Taste(3, 0, 0), "two layers of (2,0,0) score ceil(1.5 * 2) = 3");
                    Expect(cup.IsEmpty, "the stir cup empties after its 2 units");
                    Expect(rocks.TryAdjust(new Taste(0, 1, 0), out _) && rocks.TryAdjust(new Taste(0, 0, 1), out _), "a layered drink gets 2 adjustment points");
                    Expect(!rocks.TryAdjust(new Taste(1, 0, 0), out _), "a third point is refused");
                    Expect(rocks.FinalStats == new Taste(3, 1, 1), "adjusted stats");
                }),
                (0.1f, "wrong serve", () =>
                {
                    Place(rocks, serve + Vector3.up * 0.01f);
                    foreach (var c in game.Orders.Customers)
                        if (c.order == null) c.order = new DrinkOrder { Target = new Taste(9, 9, 9), Method = PrepMethod.Mixed, GlassVariant = "Wine", Hint = "test" };
                }),
                (0.3f, "ring wrong", () =>
                {
                    int before = game.Orders.Served;
                    station.Serve();
                    Expect(game.Orders.Served == before, "a wrong drink isn't accepted");
                    Expect(game.Orders.Customers.Any(c => c.missed), "the customer's ticket now shows a tip");
                    Expect(!rocks.IsEmpty, "a refused drink stays in the glass");
                }),
                (0.1f, "dump", () => TipOverNothing(rocks, serve + Vector3.up * 0.6f + right * 2f)),
                (1.6f, "check dump", () => Expect(rocks.IsEmpty, "tipping a glass out over nothing empties it")),
                (0.1f, "raw into glass", () => { Place(rocks, work - right * 2f); TipOver(tonic, rocks); }),
                (0.9f, "check raw", () => Expect(rocks.IsEmpty, "raw ingredients can't go straight into a glass")),
                (0.1f, "coaster snap", () =>
                {
                    var snap = station.Snap;
                    Hold(rocks);
                    rocks.transform.rotation = Quaternion.AngleAxis(35f, Vector3.forward) * upright[rocks.transform];
                    rocks.transform.position = serve + new Vector3(0.07f, 0.12f, -0.04f);
                    Physics.SyncTransforms();
                    Expect(snap.InReach(rocks), "a tilted glass let go just above the coaster is close enough to snap");
                    Expect(snap.Snap(rocks), "it snaps onto the coaster");
                    Vector3 off = rocks.CenterWorld - serve; float lift = rocks.BottomWorld.y - serve.y; off.y = 0;
                    Expect(Vector3.Angle(rocks.UpWorld, Vector3.up) < 1f, "the snapped glass stands upright");
                    Expect(off.magnitude < 0.005f && Mathf.Abs(lift) < 0.005f, "the snapped glass stands in the middle of the coaster");
                    Expect(!snap.Snap(highball), "a second glass doesn't snap onto a taken coaster");
                    Place(highball, serve + new Vector3(0.5f, 0.12f, 0f));
                    Expect(!snap.InReach(highball), "a glass let go farther away doesn't snap");
                }),
                (0.2f, "reset button", () =>
                {
                    var button = UnityEngine.Object.FindObjectsByType<PokeButton>(FindObjectsSortMode.None)
                        .FirstOrDefault(b => b.transform.parent && b.transform.parent.name == ResetButton.Name);
                    Expect(button != null, "the reset button is on the counter");
                    Place(whiskey, away);
                    Place(cup, away + right);
                    button?.Press();
                    bool AtHome(Component c) { var h = c.GetComponent<PropRespawn>(); return h && Vector3.Distance(c.transform.position, h.HomePosition) < 0.001f; }
                    Expect(AtHome(whiskey) && AtHome(cup) && AtHome(rocks) && AtHome(highball), "pressing it puts the props back where they started");
                }),
                (0.2f, "check reset", () => Expect(station.Snap.Glass == null, "a glass sent home leaves the coaster")),
            }).ToList();
        }

        static IEnumerable<(float, string, Action)> SpoonCircles(DrinkContainer cup)
        {
            for (int i = 0; i < 40; i++)
            {
                int k = i;
                yield return (0.03f, "swirl", () =>
                {
                    var spoon = UnityEngine.Object.FindFirstObjectByType<StirTool>();
                    var t = spoon.transform;
                    // stand the spoon up in the cup, then move it in a circle
                    var b = GameVisuals.LocalBounds(spoon.gameObject);
                    Vector3 e = b.extents;
                    Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
                    t.rotation = Quaternion.FromToRotation(t.TransformDirection(axis), Vector3.up) * t.rotation;
                    float a = k * 0.6f;
                    Vector3 c = cup.OpeningWorld + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * cup.OpeningRadiusWorld * 0.5f;
                    Vector3 low = t.TransformPoint(b.center - axis * GameVisuals.Extent(b, axis) * 0.9f);
                    t.position += (c - Vector3.up * 0.03f) - low;
                    Physics.SyncTransforms();
                });
            }
        }
    }
}
