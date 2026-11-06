using System.Linq;
using System.Text;
using MoonlitTails.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Tools > Moonlit Tails > 4. Check Drink Setup: lists which bar props the drink game will use and for what,
    /// and warns about anything it can't find. Changes nothing.
    /// From the command line: Unity -batchmode -quit -projectPath Unity -executeMethod MoonlitTails.EditorTools.DrinkSetupCheck.RunBatch
    /// </summary>
    public static class DrinkSetupCheck
    {
        [MenuItem("Tools/Moonlit Tails/4. Check Drink Setup")]
        public static void Run() => Check(out _);

        public static void RunBatch()
        {
            if (!SceneManagerHasBar()) EditorSceneManager.OpenScene(MoonlitSetup.ScenePath);
            bool ok = Check(out int warnings);
            EditorApplication.Exit(ok && warnings == 0 ? 0 : 1);
        }

        static bool SceneManagerHasBar() => GameObject.Find("MoonlitBar") != null;

        static bool Check(out int warnings)
        {
            warnings = 0;
            var bar = GameObject.Find("MoonlitBar");
            if (!bar) { Debug.LogError("[Moonlit] Open MoonlitBar_VR.unity first (no MoonlitBar object)."); return false; }
            var plan = DrinkGame.Plan(bar.transform);
            var sb = new StringBuilder("[Moonlit] Drink setup\n");
            sb.AppendLine($"Ingredients ({plan.sources.Count}):");
            foreach (var g in plan.sources.GroupBy(s => s.ingredient.DisplayName))
                sb.AppendLine($"  {g.Key,-16} <- {string.Join(", ", g.Select(s => s.t.name))}");
            sb.AppendLine($"Glasses ({plan.glasses.Count}):");
            foreach (var g in plan.glasses.GroupBy(s => s.type))
                sb.AppendLine($"  {g.Key,-9} {DrinkGame.GlassLengths[g.Key],-6} x{g.Count()}");
            sb.AppendLine($"Garnishes ({plan.garnishes.Count}): {string.Join(", ", plan.garnishes.Select(g => g.color).Distinct())}");
            sb.AppendLine($"Stir Cup: {Name(plan.stirCup)}   Shake Mixer: {Name(plan.shaker)}   Spoon: {Name(plan.spoon)}");
            sb.AppendLine($"Ice: {Name(plan.scoop)} + {Name(plan.iceWell)}   Bell: {Name(plan.bell)}   Serve: {Name(plan.serveSpot)}");
            sb.AppendLine($"Tickets: {plan.tickets.Count}   Menu board: {Name(plan.menuBoard)}");
            var layout = Object.FindFirstObjectByType<BarLayout>();
            sb.AppendLine($"Customers: {(layout ? string.Join(", ", layout.characters.Select(c => c.id)) : "none (no BarLayout)")}");
            foreach (var w in plan.warnings) sb.AppendLine("  WARNING: " + w);
            warnings = plan.warnings.Count;
            Debug.Log(sb.ToString());
            return true;
        }

        static string Name(Transform t) => t ? t.name : "MISSING";
    }
}
