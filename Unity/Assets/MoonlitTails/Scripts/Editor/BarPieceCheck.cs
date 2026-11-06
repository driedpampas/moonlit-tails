using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Tools > Moonlit Tails > 8. Check Bar Pieces: checks that every object in the bar is where it belongs and set up
    /// right. Changes nothing.
    /// - every mesh in the bar belongs to a piece prefab (no loose meshes)
    /// - every material is one of the shared bar materials (no missing or default ones)
    /// - room pieces have colliders, props have collider + rigidbody + grab + respawn
    /// - no missing scripts, no duplicate names
    /// Command line: Unity -batchmode -nographics -projectPath Unity -executeMethod MoonlitTails.EditorTools.BarPieceCheck.RunBatch
    /// </summary>
    public static class BarPieceCheck
    {
        [MenuItem("Tools/Moonlit Tails/8. Check Bar Pieces")]
        public static void Run() => Check(out _);

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(MoonlitSetup.ScenePath);
            Check(out int problems);
            EditorApplication.Exit(problems == 0 ? 0 : 1);
        }

        static readonly string[] Groups = { "Architecture", "BarCounters", "Decor", "NightSky", "Stools", "Props", "Markers", "Lights", "StivModels" };
        static readonly string[] FixedProps = { "PROP_TapHandle", "PROP_DispenserLever", "PROP_OrderTicket" };
        static readonly string[] NoCollider = { "StringLights", "Neon", "Pendant", "Lantern", "WallStars", "Sconces", "Noren", "IceCubes", "Picture", "Medallion", "Straw", "SKY_" };

        public static bool Check(out int problems)
        {
            var bar = GameObject.Find("MoonlitBar");
            var issues = new List<string>();
            var sb = new StringBuilder("[Moonlit] Bar piece check\n");
            if (!bar) { Debug.LogError("[Moonlit] No MoonlitBar in the open scene."); problems = 1; return false; }

            var barAsset = PrefabUtility.GetCorrespondingObjectFromSource(bar);
            sb.AppendLine($"MoonlitBar is a prefab instance: {(barAsset ? AssetDatabase.GetAssetPath(barAsset) : "NO")}");
            if (!barAsset) issues.Add("MoonlitBar isn't an instance of MoonlitBar.prefab");

            // ---- what's directly in each group
            var pieces = new List<GameObject>();
            int markers = 0, lights = 0, stiv = 0;
            var pieceAssets = new HashSet<string>();
            foreach (Transform child in bar.transform)
            {
                if (!Groups.Contains(child.name)) { issues.Add($"unexpected object directly under MoonlitBar: {child.name}"); continue; }
                foreach (var t in Members(child))
                {
                    var go = t.gameObject;
                    if (child.name == "Markers")
                    {
                        markers++;
                        if (go.GetComponents<Component>().Length > 1 || t.childCount > 0) issues.Add($"marker {go.name} has components or children");
                        continue;
                    }
                    if (child.name == "Lights")
                    {
                        lights++;
                        if (!go.GetComponent<Light>()) issues.Add($"{go.name} in Lights has no Light");
                        continue;
                    }
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) { issues.Add($"{child.name}/{go.name} is not a prefab instance"); continue; }
                    string asset = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    if (child.name == "StivModels") stiv++; else pieces.Add(go);
                    pieceAssets.Add(asset);
                    if (!asset.EndsWith(".prefab")) issues.Add($"{go.name} is an instance of a model ({asset}), not of a piece prefab");
                }
            }
            sb.AppendLine($"Pieces: {pieces.Count} (from {pieceAssets.Count} prefab files), Stiv's models: {stiv}, markers: {markers}, lights: {lights}");

            // ---- every renderer in the bar must belong to a prefab instance, with real materials
            var shared = new HashSet<Material>(AssetDatabase.FindAssets("t:Material", new[] { MoonlitSetup.Root + "/Environment/Materials" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))));
            int renderers = 0;
            foreach (var r in bar.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer) continue;
                renderers++;
                if (!PrefabUtility.IsPartOfPrefabInstance(r) || PrefabUtility.IsAddedComponentOverride(r))
                    issues.Add($"loose mesh (not in a piece prefab): {Path(r.transform, bar.transform)}");
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) { issues.Add($"missing material on {Path(r.transform, bar.transform)}"); continue; }
                    bool stivMat = Path(r.transform, bar.transform).StartsWith("StivModels/");
                    if (!stivMat && !shared.Contains(m)) issues.Add($"{Path(r.transform, bar.transform)} uses '{m.name}' ({AssetDatabase.GetAssetPath(m)}), not a shared bar material");
                }
            }

            // ---- physics and grabbing
            int props = 0, fixedProps = 0;
            foreach (var p in pieces)
            {
                string n = p.name;
                bool isProp = n.StartsWith("PROP_");
                if (isProp && FixedProps.Any(n.StartsWith))
                {
                    fixedProps++;
                    if (!p.GetComponent<Collider>()) issues.Add($"{n}: no collider");
                    continue;
                }
                if (isProp)
                {
                    props++;
                    var col = p.GetComponent<MeshCollider>();
                    if (!col || !col.convex) issues.Add($"{n}: needs a convex collider");
                    if (!p.GetComponent<Rigidbody>()) issues.Add($"{n}: no rigidbody");
                    if (!p.GetComponent<XRGrabInteractable>()) issues.Add($"{n}: not grabbable");
                    var home = p.GetComponent<PropRespawn>();
                    if (!home) issues.Add($"{n}: no PropRespawn");
                    else if (home.space != bar.transform) issues.Add($"{n}: PropRespawn space isn't the bar");
                    continue;
                }
                bool wantCollider = !NoCollider.Any(n.Contains);
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>(true))
                {
                    bool has = mf.GetComponent<MeshCollider>();
                    if (wantCollider && !has) issues.Add($"{Path(mf.transform, bar.transform)}: no collider");
                    if (!wantCollider && has) issues.Add($"{Path(mf.transform, bar.transform)}: has a collider but shouldn't");
                }
            }
            sb.AppendLine($"Grabbable props: {props}, fixed props (taps, levers, tickets): {fixedProps}, renderers: {renderers}");

            // ---- BarLayout knows every prop, missing scripts, names
            var layout = Object.FindFirstObjectByType<BarLayout>();
            if (layout)
            {
                var listed = new HashSet<Transform>(layout.props.Where(r => r.prop).Select(r => r.prop));
                foreach (var p in pieces.Where(p => p.GetComponent<PropRespawn>()))
                    if (!listed.Contains(p.transform)) issues.Add($"{p.name}: not in BarLayout's prop list (won't scale)");
                if (layout.props.Any(r => !r.prop)) issues.Add("BarLayout has empty prop entries");
                if (layout.environment != bar.transform) issues.Add("BarLayout.environment isn't the bar");
            }
            foreach (var t in bar.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) issues.Add($"missing script on {Path(t, bar.transform)}");
            foreach (var g in pieces.GroupBy(p => p.name).Where(g => g.Count() > 1))
                issues.Add($"{g.Count()} pieces called {g.Key}");

            // ---- overrides the scene keeps on top of the prefab (they're fine, but good to know)
            var mods = PrefabUtility.GetPropertyModifications(bar) ?? new PropertyModification[0];
            var added = PrefabUtility.GetAddedGameObjects(bar);
            var removed = PrefabUtility.GetRemovedGameObjects(bar);
            sb.AppendLine($"Scene overrides on the bar prefab: {mods.Length} property changes, {added.Count} added objects, {removed.Count} removed objects");

            foreach (var i in issues) sb.AppendLine("  PROBLEM: " + i);
            sb.AppendLine(issues.Count == 0 ? "No problems." : $"{issues.Count} problems.");
            problems = issues.Count;
            if (problems == 0) Debug.Log(sb.ToString()); else Debug.LogError(sb.ToString());
            return problems == 0;
        }

        /// <summary>The objects in a group: direct children, and the children of sub-groups (Props/Glasses).</summary>
        static IEnumerable<Transform> Members(Transform group)
        {
            foreach (Transform c in group)
            {
                bool subGroup = !PrefabUtility.IsAnyPrefabInstanceRoot(c.gameObject) && !c.GetComponent<Renderer>() && !c.GetComponent<Light>()
                                && !c.name.StartsWith("MK_") && c.childCount > 0;
                if (subGroup) foreach (Transform cc in c) yield return cc;
                else yield return c;
            }
        }

        static string Path(Transform t, Transform root)
        {
            var s = t.name;
            while (t.parent && t.parent != root) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
