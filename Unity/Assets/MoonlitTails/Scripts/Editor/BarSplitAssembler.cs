using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Tools > Moonlit Tails > 6. Assemble Split Bar: puts the bar back together from the per-piece FBX files that
    /// Art/Bar/split_bar.py writes to Environment/Bar.
    ///
    /// 1. Import settings for every piece, with its materials pointed at Environment/Materials (by name).
    /// 2. One prefab per piece in Environment/Bar/Prefabs (colliders, physics and grabbing included).
    /// 3. In the open scene, every piece of the old MoonlitBar is swapped for its prefab at exactly the same place,
    ///    the bar is grouped (Architecture, BarCounters, Decor, NightSky, Stools, Props, Markers, Lights, StivModels)
    ///    and saved as Environment/Bar/MoonlitBar.prefab.
    /// 4. Every piece's world bounds and vertex count are compared before and after; if anything moved, nothing is saved.
    ///
    /// Re-running steps 1 and 2 after re-exporting from Blender updates the pieces in place (same files, same GUIDs).
    /// Command line: Unity -batchmode -nographics -projectPath Unity -executeMethod MoonlitTails.EditorTools.BarSplitAssembler.RunBatch
    /// </summary>
    public static class BarSplitAssembler
    {
        public const string Dir = MoonlitSetup.Root + "/Environment/Bar";
        const string PrefabDir = Dir + "/Prefabs";
        const string MaterialDir = MoonlitSetup.Root + "/Environment/Materials";
        static readonly string[] StivPrefixes = { "woodslab", "archdoor", "counterthingy" };

        [Serializable] class SplitFile { public string source; public string[] replaced; public SplitObject[] objects; }
        [Serializable] class SplitObject { public string name, model, group; public float[] shift; public string[] parts; public int verts, tris; }

        [MenuItem("Tools/Moonlit Tails/6. Assemble Split Bar")]
        public static void Run()
        {
            if (!EditorUtility.DisplayDialog("Assemble split bar",
                "Swaps the bar in MoonlitBar_VR.unity for the per-piece prefabs and saves the scene.\n\n" +
                "Make sure nobody else is editing the scene.", "Assemble", "Cancel")) return;
            Assemble(out _);
        }

        public static void RunBatch()
        {
            bool ok = Assemble(out string report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Only steps 1 and 2: refresh imports and prefabs after re-exporting pieces from Blender.</summary>
        [MenuItem("Tools/Moonlit Tails/7. Refresh Bar Pieces")]
        public static void RefreshPieces()
        {
            var split = Load();
            if (split == null) return;
            ConfigureImports(split);
            MakePrefabs(split);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MoonlitSetup.BarPrefab)) PromoteStivModels();
            AssetDatabase.SaveAssets();
            Debug.Log("[Moonlit] Bar pieces refreshed.");
        }

        public static void RefreshPiecesBatch()
        {
            RefreshPieces();
            EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ Stiv's models
        const string StivDir = MoonlitSetup.Root + "/Environment/StivModels";

        /// <summary>
        /// Gives every model in Environment/StivModels a prefab (StivModels/Prefabs) with colliders and the materials
        /// its copies in the bar use, then swaps copies of the bare model inside MoonlitBar.prefab for copies of that
        /// prefab, in exactly the same place. New models dropped into StivModels get the same treatment.
        /// </summary>
        public static void PromoteStivModels()
        {
            var bar = PrefabUtility.LoadPrefabContents(MoonlitSetup.BarPrefab);
            try
            {
                var models = AssetDatabase.FindAssets("t:Model", new[] { StivDir }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == StivDir).ToList();
                int swapped = 0;
                foreach (var modelPath in models)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    var copies = bar.GetComponentsInChildren<Transform>(true)
                        .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == model)
                        .ToList();

                    string prefabPath = StivDir + "/Prefabs/" + model.name + ".prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (!prefab)
                    {
                        EnsureFolder(StivDir + "/Prefabs");
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                        go.name = model.name;
                        // the materials the copies in the bar use (they all use the same ones)
                        var sample = copies.FirstOrDefault();
                        if (sample)
                        {
                            var from = sample.GetComponentsInChildren<Renderer>(true);
                            var to = go.GetComponentsInChildren<Renderer>(true);
                            for (int i = 0; i < Mathf.Min(from.Length, to.Length); i++) to[i].sharedMaterials = from[i].sharedMaterials;
                        }
                        MoonlitSetup.AddEnvironmentComponents(go, "ENV_" + model.name);
                        prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                        Object.DestroyImmediate(go);
                        Debug.Log("[Moonlit] Made " + prefabPath);
                    }

                    // swap copies of the bare model, parents before children so nested copies come along
                    foreach (var old in copies.OrderBy(t => Depth(t)))
                    {
                        if (!old) continue;
                        var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.parent);
                        piece.name = old.name;
                        piece.transform.SetSiblingIndex(old.GetSiblingIndex());
                        piece.transform.localPosition = old.localPosition;
                        piece.transform.localRotation = old.localRotation;
                        piece.transform.localScale = old.localScale;
                        piece.SetActive(old.gameObject.activeSelf);
                        // anything that was attached under the old copy (e.g. another slab) moves to the new one
                        foreach (var child in old.Cast<Transform>().Where(c => !PrefabUtility.IsPartOfPrefabAsset(c) && PrefabUtility.IsAddedGameObjectOverride(c.gameObject)).ToList())
                            child.SetParent(piece.transform, false);
                        Object.DestroyImmediate(old.gameObject);
                        swapped++;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(bar, MoonlitSetup.BarPrefab);
                Debug.Log($"[Moonlit] Stiv's models: {models.Count} prefabs, {swapped} copies in the bar swapped to them.");
            }
            finally { PrefabUtility.UnloadPrefabContents(bar); }
        }

        static int Depth(Transform t) { int d = 0; while (t.parent) { d++; t = t.parent; } return d; }

        static SplitFile Load()
        {
            string path = Dir + "/bar_split.json";
            if (!File.Exists(path)) { Debug.LogError("[Moonlit] " + path + " not found. Run Art/Bar/split_bar.py first."); return null; }
            return JsonUtility.FromJson<SplitFile>(File.ReadAllText(path));
        }

        static bool Assemble(out string report)
        {
            report = "";
            var split = Load();
            if (split == null) return false;

            var scene = EditorSceneManager.OpenScene(MoonlitSetup.ScenePath);
            var bar = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MoonlitBar");
            if (!bar) { Debug.LogError("[Moonlit] No MoonlitBar in the scene."); return false; }
            if (PrefabUtility.IsPartOfPrefabInstance(bar)) { Debug.LogError("[Moonlit] The bar is already assembled from pieces."); return false; }

            ConfigureImports(split);
            var prefabs = MakePrefabs(split);

            // ---- which scene object is which piece (copies made in Unity, like "DECO_Plant_SillL (1)", too)
            var byName = split.objects.ToDictionary(o => o.name);
            var pairs = new List<(Transform old, SplitObject o, (Bounds b, int verts, int tris) before)>();
            foreach (Transform t in bar.transform)
            {
                string key = System.Text.RegularExpressions.Regex.Replace(t.name, @" \(\d+\)$", "");
                if (byName.TryGetValue(key, out var o)) pairs.Add((t, o, Measure(t)));
            }
            var missing = split.objects.Where(o => !pairs.Any(p => p.o == o)).Select(o => o.name).ToList();

            var layout = Object.FindFirstObjectByType<BarLayout>();
            var oldRecords = layout ? layout.props.ToDictionary(r => r.prop ? r.prop.name : "", r => r.baseScale) : new Dictionary<string, Vector3>();
            if (layout) layout.props.Clear();

            Transform Group(string path)
            {
                Transform parent = bar.transform;
                foreach (var part in path.Split('/'))
                {
                    var c = parent.Find(part);
                    if (!c)
                    {
                        c = new GameObject(part).transform;
                        c.SetParent(parent, false);
                    }
                    parent = c;
                }
                return parent;
            }

            // ---- swap every piece for its prefab
            var after = new List<(string name, GameObject piece, (Bounds b, int verts, int tris) before)>();
            foreach (var (old, o, measured) in pairs)
            {
                var group = Group(o.group);
                var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[o.model], group);
                piece.name = old.name;
                after.Add((old.name, piece, measured));
                // the pivot may have moved to the piece's bottom centre: Blender (x, y, z) is Unity (-x, z, -y)
                var shift = new Vector3(-o.shift[0], o.shift[2], -o.shift[1]);
                piece.transform.localRotation = old.localRotation;
                piece.transform.localScale = old.localScale;
                piece.transform.localPosition = old.localPosition + old.localRotation * Vector3.Scale(old.localScale, shift);
                piece.SetActive(old.gameObject.activeSelf);

                if (piece.TryGetComponent<PropRespawn>(out var home))
                {
                    home.space = bar.transform;
                    home.RememberHome();
                    if (layout) layout.props.Add(new BarLayout.PropRecord
                    {
                        prop = piece.transform,
                        baseScale = oldRecords.TryGetValue(old.name, out var s) ? s : piece.transform.localScale,
                    });
                }
                Object.DestroyImmediate(old.gameObject);
            }

            // ---- markers, lights and Stiv's replacement pieces into their groups
            foreach (Transform t in bar.transform.Cast<Transform>().ToList())
            {
                if (t.name.StartsWith("MK_")) t.SetParent(Group("Markers"), true);
                else if (t.name.StartsWith("LGT_")) t.SetParent(Group("Lights"), true);
            }
            foreach (var root in scene.GetRootGameObjects())
                if (StivPrefixes.Any(p => root.name.StartsWith(p))) root.transform.SetParent(Group("StivModels"), true);

            var leftovers = bar.transform.Cast<Transform>().Where(t => !IsGroup(t.name)).Select(t => t.name).ToList();

            // ---- after: compare
            var sb = new StringBuilder("[Moonlit] Bar split check\n");
            int bad = 0, vertsDiffer = 0;
            foreach (var (name, piece, b0) in after)
            {
                var b1 = Measure(piece.transform);
                float dc = (b1.b.center - b0.b.center).magnitude, ds = (b1.b.size - b0.b.size).magnitude;
                if (dc > 0.001f || ds > 0.001f || b1.tris != b0.tris)
                {
                    sb.AppendLine($"  CHANGED {name}: centre moved {dc * 1000:F1} mm, size {ds * 1000:F1} mm, triangles {b0.tris} -> {b1.tris}");
                    bad++;
                }
                else if (b1.verts != b0.verts)
                {
                    // same triangles in the same place; only how vertices are split for normals differs (Blender version)
                    sb.AppendLine($"  note {name}: vertices {b0.verts} -> {b1.verts} (same triangles)");
                    vertsDiffer++;
                }
            }
            sb.AppendLine($"  {after.Count} pieces compared: {bad} changed, {vertsDiffer} with a different vertex split only");
            if (missing.Count > 0) sb.AppendLine("  not in the scene (skipped): " + string.Join(", ", missing));
            if (leftovers.Count > 0) sb.AppendLine("  left directly under MoonlitBar: " + string.Join(", ", leftovers));
            report = sb.ToString();

            if (bad > 0)
            {
                Debug.LogError(report + "\nNothing was saved.");
                return false;
            }
            Debug.Log(report);

            PrefabUtility.SaveAsPrefabAssetAndConnect(bar, MoonlitSetup.BarPrefab, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Moonlit] Bar assembled from pieces and saved: " + MoonlitSetup.BarPrefab);
            return true;
        }

        static bool IsGroup(string n) => new[] { "Architecture", "BarCounters", "Decor", "NightSky", "Stools", "Props", "Markers", "Lights", "StivModels" }.Contains(n);

        static (Bounds b, int verts, int tris) Measure(Transform t)
        {
            var rs = t.GetComponentsInChildren<MeshRenderer>(true);
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);
            int verts = 0, tris = 0;
            foreach (var r in rs)
            {
                b.Encapsulate(r.bounds);
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                verts += mf.sharedMesh.vertexCount;
                for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) tris += (int)mf.sharedMesh.GetIndexCount(i) / 3;
            }
            return (b, verts, tris);
        }

        // ------------------------------------------------------------------ 1. import settings + materials
        static void ConfigureImports(SplitFile split)
        {
            var materials = AssetDatabase.FindAssets("t:Material", new[] { MaterialDir })
                .Select(AssetDatabase.GUIDToAssetPath).ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => AssetDatabase.LoadAssetAtPath<Material>(p));
            var notFound = new HashSet<string>();
            foreach (var model in split.objects.Select(o => o.model).Distinct())
            {
                string path = Dir + "/" + model;
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) { Debug.LogError("[Moonlit] Missing piece " + path); continue; }
                imp.importLights = false;
                imp.importCameras = false;
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importBlendShapes = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.generateSecondaryUV = true;   // ready for baked lighting later
                imp.SaveAndReimport();

                // point every material slot at the shared bar material with the same name
                imp = (ModelImporter)AssetImporter.GetAtPath(path);
                foreach (var m in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Material>().ToList())
                {
                    if (materials.TryGetValue(m.name, out var shared)) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), shared);
                    else notFound.Add(m.name);
                }
                imp.SaveAndReimport();
            }
            if (notFound.Count > 0) Debug.LogWarning("[Moonlit] No shared material for: " + string.Join(", ", notFound));
        }

        // ------------------------------------------------------------------ 2. a prefab per piece
        static Dictionary<string, GameObject> MakePrefabs(SplitFile split)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<BarTuning>(MoonlitSetup.Root + "/BarTuning.asset");
            var result = new Dictionary<string, GameObject>();
            foreach (var model in split.objects.Select(o => o.model).Distinct())
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/" + model);
                if (!fbx) continue;
                string prefabPath = PrefabDir + "/" + Path.ChangeExtension(model, ".prefab");
                EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));

                string name = Path.GetFileNameWithoutExtension(model);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
                {
                    // update in place, so scenes keep their links and overrides
                    var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (model.StartsWith("Props/")) { if (!contents.GetComponent<Collider>()) MoonlitSetup.AddPropComponents(contents, tuning); }
                    else MoonlitSetup.AddEnvironmentComponents(contents, name);
                    result[model] = PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                    PrefabUtility.UnloadPrefabContents(contents);
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                go.name = name;
                if (model.StartsWith("Props/")) MoonlitSetup.AddPropComponents(go, tuning);
                else MoonlitSetup.AddEnvironmentComponents(go, name);
                result[model] = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);
            }
            return result;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
