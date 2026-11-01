using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Sets the project up for Quest hand tracking and (re)builds the VR test scene.
    ///   Tools > Moonlit Tails > 1. Configure Project   (URP, OpenXR + hand tracking, XRI samples, character prefabs)
    ///   Tools > Moonlit Tails > 2. Rebuild VR Scene     (bar + XR hands rig + 3 characters + grabbable props)
    /// Sizes are NOT set here: they live in BarTuning.asset and are applied by BarLayout.
    /// </summary>
    public static class MoonlitSetup
    {
        public const string Root = "Assets/MoonlitTails";
        public const string ScenePath = Root + "/Scenes/MoonlitBar_VR.unity";
        const string TuningPath = Root + "/BarTuning.asset";
        const string BarFbx = Root + "/Environment/MoonlitBar.fbx";
        const string SitFbx = Root + "/Animations/Sitting Idle.fbx";
        static readonly string[] CharacterIds = { "Kintsu", "Character02", "Character03" };

        // ============================================================================ 1. project
        [MenuItem("Tools/Moonlit Tails/1. Configure Project")]
        public static void ConfigureProject()
        {
            SetupURP();
            SetupXR();
            ImportSamples();
            ConfigureBarImport();
            ConfigureSittingClip();
            foreach (var id in CharacterIds) SetupCharacter(id);
            AssetDatabase.SaveAssets();
            Debug.Log("[Moonlit] Project configured.");
        }

        static void SetupURP()
        {
            string dir = Root + "/Settings";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(dir + "/MoonlitURP.asset");
            if (asset == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                data.renderingMode = RenderingMode.ForwardPlus;          // the bar has lots of small lights
                AssetDatabase.CreateAsset(data, dir + "/MoonlitURP_Renderer.asset");
                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, dir + "/MoonlitURP.asset");
            }
            asset.msaaSampleCount = 4;
            asset.supportsHDR = true;
            asset.shadowDistance = 12f;
            asset.renderScale = 1f;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // the old ProBuilder block-out materials, so that scene doesn't turn pink
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m.shader == lit) continue;
                Color c = m.color; m.shader = lit; m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m);
            }
        }

        static void SetupXR()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                EnsureFolder("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            foreach (var g in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                if (!perTarget.HasSettingsForBuildTarget(g)) perTarget.CreateDefaultSettingsForBuildTarget(g);
                if (!perTarget.HasManagerSettingsForBuildTarget(g)) perTarget.CreateDefaultManagerSettingsForBuildTarget(g);
                var gs = perTarget.SettingsForBuildTarget(g);
                gs.InitManagerOnStart = true;
                XRPackageMetadataStore.AssignLoader(gs.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", g);
                EditorUtility.SetDirty(gs);
            }
            AssetDatabase.SaveAssets();

            // OpenXR features: hand tracking + pinch/aim + hand and Touch controller profiles; Quest support on Android
            foreach (var g in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                FeatureHelpers.RefreshFeatures(g);
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
                if (settings == null) { Debug.LogError("[Moonlit] No OpenXR settings for " + g); continue; }
                var wanted = new HashSet<string> { "HandTracking", "MetaHandTrackingAim", "HandInteractionProfile", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile" };
                if (g == BuildTargetGroup.Android) wanted.Add("MetaQuestFeature");
                foreach (var f in settings.GetFeatures())
                {
                    if (!wanted.Contains(f.GetType().Name)) continue;
                    f.enabled = true;
                    EditorUtility.SetDirty(f);
                }
                Debug.Log($"[Moonlit] OpenXR {g}: " + string.Join(", ", settings.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name)));
                EditorUtility.SetDirty(settings);
            }

            // keep Play mode running when the editor window loses focus (it does as soon as the headset takes over)
            PlayerSettings.runInBackground = true;

            // Quest standalone build basics (only matters if you build an APK instead of using Link)
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;
        }

        static void ImportSamples()
        {
            ImportSample("com.unity.xr.interaction.toolkit", "Starter Assets");
            ImportSample("com.unity.xr.hands", "HandVisualizer");
            ImportSample("com.unity.xr.interaction.toolkit", "Hands Interaction Demo");
        }

        static void ImportSample(string package, string name)
        {
            string key = name.Replace(" ", "").ToLowerInvariant();
            foreach (var s in Sample.FindByPackage(package, null))
            {
                if (!s.displayName.Replace(" ", "").ToLowerInvariant().Contains(key)) continue;
                if (!s.isImported) s.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow);
                Debug.Log($"[Moonlit] Sample '{s.displayName}' at {s.importPath}");
                return;
            }
            Debug.LogError($"[Moonlit] Sample not found: {package} / {name}");
        }

        static void ConfigureBarImport()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(BarFbx);
            imp.importLights = true;                 // light positions come from Blender, intensities are set in BuildLights
            imp.importCameras = false;
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.generateSecondaryUV = true;          // ready for baked lighting later (needed for Quest performance)
            imp.SaveAndReimport();
        }

        static void ConfigureSittingClip()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(SitFbx);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();
            imp = (ModelImporter)AssetImporter.GetAtPath(SitFbx);
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = "Sitting_Idle";
                c.loopTime = true; c.loopPose = true;
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;   // bake root into pose
                c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        static void SetupCharacter(string id)
        {
            string dir = $"{Root}/Characters/{id}";
            string fbx = $"{dir}/{id}.fbx";
            if (!File.Exists($"{dir}/{id}.prefab"))
            {
                // the shared character pipeline's own one-click tools (Tools > VR Characters > 1 / 2)
                var tool = typeof(VRCharacters.EditorTools.VRCharacterSetup);
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(fbx);
                tool.GetMethod("ConfigureImport", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(fbx);
                tool.GetMethod("BuildPrefab", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            }
            // toon look so customers always read clearly (see MoonlitToon.shader)
            var toon = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/MoonlitToon.shader");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { dir + "/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m.shader == toon) continue;
                var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                m.shader = toon;
                if (tex) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
                if (m.name.StartsWith("M_Eye")) { m.SetFloat("_OutlineWidth", 0f); m.SetFloat("_MinBrightness", 1f); m.SetFloat("_RimStrength", 0f); }
                EditorUtility.SetDirty(m);
            }
        }

        // ============================================================================ 2. scene
        [MenuItem("Tools/Moonlit Tails/2. Rebuild VR Scene")]
        public static void BuildScene()
        {
            PlayerSettings.runInBackground = true;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // load (or create) the tuning asset after NewScene, which unloads unreferenced assets
            var tuning = AssetDatabase.LoadAssetAtPath<BarTuning>(TuningPath);
            if (tuning == null) { tuning = ScriptableObject.CreateInstance<BarTuning>(); AssetDatabase.CreateAsset(tuning, TuningPath); }

            var bar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BarFbx));
            PrefabUtility.UnpackPrefabInstance(bar, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            bar.name = "MoonlitBar";
            bar.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bar.transform.localScale = Vector3.one;

            var layout = new GameObject("BarLayout (sizes live in BarTuning.asset)").AddComponent<BarLayout>();
            layout.tuning = tuning;
            layout.environment = bar.transform;
            Transform Marker(string n) => bar.GetComponentsInChildren<Transform>(true).First(t => t.name == n);

            ApplyBarMaterials(bar);
            SetupBarObjects(bar, layout, tuning);
            BuildLights(bar, Marker("MK_PlayerStart"), Marker("MK_ServeSpot"), Marker("MK_CustomerSpot_C"));

            layout.playerSpot = Marker("MK_PlayerStart");
            layout.playerFacing = Marker("MK_ServeSpot");
            layout.xrOrigin = CreateXRRig();

            var sit = AssetDatabase.LoadAllAssetsAtPath(SitFbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            AddCharacter(layout, tuning, "Kintsu", Marker("MK_CustomerSpot_C"), Marker("MK_PlayerStart"), null);       // standing, ordering
            AddCharacter(layout, tuning, "Character03", Marker("MK_StoolSeat_R"), Marker("MK_PlayerStart"), sit);      // on a stool at the bar
            AddCharacter(layout, tuning, "Character02", Marker("MK_ChairSeat_L1"), Marker("MK_Table_L"), sit);         // at a table in the lounge

            BuildAtmosphere(layout.xrOrigin, Marker("MK_PlayerStart"), Marker("MK_ServeSpot"));
            EditorUtility.SetDirty(tuning);
            layout.Apply();

            EnsureFolder(Root + "/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Moonlit] Scene saved: " + ScenePath);
        }

        // ---------------------------------------------------------------------------- bar materials
        // Colours come from MoonlitBar_materials.json (written by build_bar.py), so Unity matches Blender exactly.
        static void ApplyBarMaterials(GameObject bar)
        {
            string json = File.ReadAllText(Root + "/Environment/MoonlitBar_materials.json");
            string dir = Root + "/Environment/Materials"; EnsureFolder(dir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var simple = Shader.Find("Universal Render Pipeline/Simple Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var mats = new Dictionary<string, Material>();
            var rx = new Regex("\"(M_[^\"]+)\"\\s*:\\s*\\{\\s*\"hex\"\\s*:\\s*\"(#[0-9a-fA-F]{6})\",\\s*\"roughness\"\\s*:\\s*([\\d.]+),\\s*\"metallic\"\\s*:\\s*([\\d.]+),\\s*\"emission\"\\s*:\\s*([\\d.]+),\\s*\"alpha\"\\s*:\\s*([\\d.]+)");
            float F(Match m, int i) => float.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
            foreach (Match m in rx.Matches(json))
            {
                string name = m.Groups[1].Value;
                ColorUtility.TryParseHtmlString(m.Groups[2].Value, out var col);
                float rough = F(m, 3), metal = F(m, 4), emit = F(m, 5), alpha = F(m, 6);
                bool sky = name.Contains("Sky") || name.Contains("Hills") || name.Contains("Moon") || name.Contains("Emit_Star") || name.Contains("HouseWindow");
                bool transparent = alpha < 0.99f;
                var shader = sky ? unlit : (metal > 0.5f || transparent) ? lit : simple;

                string path = $"{dir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.enableInstancing = true;
                if (sky)
                {
                    mat.SetColor("_BaseColor", col * (emit >= 3f ? 1.6f : 1f));
                }
                else
                {
                    mat.SetColor("_BaseColor", new Color(col.r, col.g, col.b, alpha));
                    mat.SetFloat("_Smoothness", shader == lit ? 1f - rough : 0f);
                    if (shader == lit) mat.SetFloat("_Metallic", metal);
                    if (emit > 0f)
                    {
                        mat.EnableKeyword("_EMISSION");
                        // neon stays coloured instead of blowing out to white; bulbs glow a bit more
                        mat.SetColor("_EmissionColor", col * (name.Contains("Neon") ? 1.4f : name.Contains("WarmBulb") ? 2.2f : 1.2f));
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    }
                    if (transparent) MakeTransparent(mat);
                }
                EditorUtility.SetDirty(mat);
                mats[name] = mat;
            }
            foreach (var r in bar.GetComponentsInChildren<MeshRenderer>(true))
            {
                var sm = r.sharedMaterials;
                for (int i = 0; i < sm.Length; i++)
                    if (sm[i] != null && mats.TryGetValue(sm[i].name, out var m)) sm[i] = m;
                r.sharedMaterials = sm;
            }
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_AlphaClip", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        // ---------------------------------------------------------------------------- colliders, physics, grabbing
        // Props that are part of a machine (levers, tap handles) or pinned (order tickets) stay put for now.
        static readonly string[] FixedProps = { "PROP_TapHandle", "PROP_DispenserLever", "PROP_OrderTicket" };
        // Decor that should not block hands or thrown props.
        static readonly string[] NoCollider = { "StringLights", "Neon", "Pendant", "Lantern", "WallStars", "Sconces", "Noren", "IceCubes", "Picture", "Medallion", "Straw" };

        static void SetupBarObjects(GameObject bar, BarLayout layout, BarTuning tuning)
        {
            foreach (var t in bar.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject; string n = go.name;
                bool hasMesh = go.TryGetComponent<MeshFilter>(out _);
                if (n.StartsWith("PROP_") && hasMesh)
                {
                    if (FixedProps.Any(n.StartsWith)) { go.AddComponent<MeshCollider>().convex = true; continue; }
                    var col = go.AddComponent<MeshCollider>(); col.convex = true;
                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = n.Contains("Bottle_") ? 0.8f : n.Contains("Fridge_") ? 0.45f : n.Contains("Glass_") ? 0.25f
                            : n.Contains("Garnish_") ? 0.02f : n.Contains("Blender") ? 0.6f : n.Contains("Shaker") ? 0.35f : 0.2f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // thrown things shouldn't tunnel through walls
                    var grab = go.AddComponent<XRGrabInteractable>();
                    grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;  // collides with counters while held
                    grab.throwOnDetach = true;
                    grab.useDynamicAttach = true;                                          // held where your hand touched it
                    grab.throwVelocityScale = tuning.throwStrength;
                    var home = go.AddComponent<PropRespawn>(); home.space = bar.transform; home.RememberHome();
                    layout.props.Add(new BarLayout.PropRecord { prop = t, baseScale = t.localScale });
                }
                else if (n.StartsWith("ENV_") || n.StartsWith("DECO_") || n.StartsWith("SKY_"))
                {
                    // no Batching Static yet: BarLayout rescales the bar live while we tune sizes
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                    if (n.StartsWith("SKY_")) { if (go.TryGetComponent<MeshRenderer>(out var r)) r.shadowCastingMode = ShadowCastingMode.Off; continue; }
                    if (hasMesh && !NoCollider.Any(n.Contains)) go.AddComponent<MeshCollider>();
                }
            }
        }

        // ---------------------------------------------------------------------------- lights
        static void BuildLights(GameObject bar, Transform player, Transform serve, Transform customer)
        {
            var lights = bar.GetComponentsInChildren<Light>(true).ToList();
            Debug.Log($"[Moonlit] {lights.Count} lights imported from the FBX");
            foreach (var l in lights)
            {
                string n = l.gameObject.name;
                if (n.Contains("Moonlight")) { Object.DestroyImmediate(l.gameObject); continue; }   // replaced by the directional moon
                l.shadows = LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                if (!n.Contains("Neon")) l.color = Color.Lerp(l.color, Color.white, 0.35f);   // warm, but wood should still look like wood
                if (n.Contains("Pendant"))
                {
                    l.type = LightType.Spot; l.spotAngle = 120f; l.innerSpotAngle = 50f; l.range = 4f; l.intensity = 1.8f;
                    l.transform.rotation = Quaternion.LookRotation(Vector3.down, player.forward);
                    l.shadows = LightShadows.Soft;     // only the three pendants cast shadows (Quest budget)
                }
                else if (n.Contains("CustomerKey"))
                {
                    l.type = LightType.Spot; l.spotAngle = 80f; l.innerSpotAngle = 40f; l.range = 4f; l.intensity = 1.5f;
                    l.transform.rotation = Quaternion.LookRotation(customer.position + Vector3.up - l.transform.position);
                }
                else
                {
                    l.type = LightType.Point;
                    (l.intensity, l.range) = n.Contains("BarKey") ? (1.1f, 4.5f) : n.Contains("LoungeFill") ? (1.2f, 6f)
                        : n.Contains("Lantern") ? (1.6f, 4f) : n.Contains("Neon") ? (1f, 2.5f) : n.Contains("Shelf") ? (0.8f, 2f)
                        : n.Contains("Sconce") ? (0.6f, 2f) : n.Contains("Vestibule") ? (1f, 3f) : (0.8f, 2.5f);
                }
            }
        }

        static void BuildAtmosphere(Transform xrOrigin, Transform player, Transform serve)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.37f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.33f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.15f, 0.13f);

            // soft blue moonlight through the windows; also the toon shader's "main light"
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional; moon.color = new Color(0.62f, 0.7f, 1f); moon.intensity = 0.35f; moon.shadows = LightShadows.None;
            Vector3 inward = player.position - serve.position; inward.y = 0;
            moon.transform.rotation = Quaternion.LookRotation(inward.normalized) * Quaternion.Euler(35f, 0f, 0f);
            RenderSettings.sun = moon;

            // bloom so neon and bulbs glow (turn the Volume off if the Quest build needs the frame time)
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/Settings/BarPostProcess.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, Root + "/Settings/BarPostProcess.asset");
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1f); bloom.intensity.Override(0.6f); bloom.scatter.Override(0.6f);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            var vol = new GameObject("PostProcess (Bloom)").AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = profile;

            var cam = xrOrigin.GetComponentInChildren<Camera>(true);
            if (cam)
            {
                cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.09f, 0.2f);
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
        }

        // ---------------------------------------------------------------------------- XR rig
        static Transform CreateXRRig()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Samples" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith("XR Origin")).ToList();
            string pick = paths.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Contains("Hands")) ?? paths.FirstOrDefault();
            Debug.Log("[Moonlit] XR rigs: " + string.Join(" | ", paths) + "  ->  " + pick);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(pick));
            var origin = rig.GetComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;   // real head height, so the counter height is real too

            // stationary game: the player stays behind the bar, so switch movement off
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                if (t.name == "Locomotion" || t.name.StartsWith("Teleport")) t.gameObject.SetActive(false);

            // natural grabbing on tracked hands: wrap the hand around a prop (or pinch small things); see HandGrab
            var tuning = AssetDatabase.LoadAssetAtPath<BarTuning>(TuningPath);
            foreach (var nf in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true))
            {
                bool onHand = false;
                for (var t = nf.transform; t != null; t = t.parent)
                {
                    if (t.name.Contains("Controller")) { onHand = false; break; }
                    if (t.name.Contains("Hand")) onHand = true;
                }
                if (!onHand) continue;
                var grab = nf.gameObject.AddComponent<HandGrab>();
                grab.tuning = tuning; grab.interactor = nf; grab.origin = origin;
                grab.handedness = nf.handedness == UnityEngine.XR.Interaction.Toolkit.Interactors.InteractorHandedness.Right
                    ? UnityEngine.XR.Hands.Handedness.Right : UnityEngine.XR.Hands.Handedness.Left;
                Debug.Log($"[Moonlit] HandGrab on '{nf.name}' ({grab.handedness})");
            }

            if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            return rig.transform;
        }

        // ---------------------------------------------------------------------------- characters
        static void AddCharacter(BarLayout layout, BarTuning tuning, string id, Transform spot, Transform face, AnimationClip sitClip)
        {
            string dir = $"{Root}/Characters/{id}";
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{id}.prefab"));
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var anim = go.GetComponent<Animator>();
            anim.applyRootMotion = false;
            bool seated = sitClip != null;

            AnimationClip pose = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.StartsWith("Idle"));
            if (seated)
            {
                // same controller (idle + tail/ear layers), with the idle swapped for the Mixamo sitting idle
                string ovPath = $"{dir}/{id}_Sitting.overrideController";
                var ov = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ovPath);
                if (ov == null) { ov = new AnimatorOverrideController(anim.runtimeAnimatorController); AssetDatabase.CreateAsset(ov, ovPath); }
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                ov.GetOverrides(pairs);
                for (int i = 0; i < pairs.Count; i++)
                    if (pairs[i].Key.name.StartsWith("Idle")) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, sitClip);
                ov.ApplyOverrides(pairs);
                EditorUtility.SetDirty(ov);
                anim.runtimeAnimatorController = ov;
                pose = sitClip;
            }

            // show the real pose in the editor too, and measure how high the hips are when sitting
            float hips = SamplePose(anim, pose, 0.5f);

            var body = go.AddComponent<CapsuleCollider>();   // so thrown props bounce off customers
            body.radius = 0.2f;
            body.height = seated ? 0.65f : 0.95f;
            body.center = seated ? new Vector3(0, hips + 0.2f, 0) : new Vector3(0, 0.5f, 0);

            layout.characters.Add(new BarLayout.CharacterSlot
            {
                id = id, character = go.transform, spot = spot, faceTowards = face,
                seated = seated, seatedHipHeight = seated ? hips : 0f,
            });
            tuning.Character(id);
            Debug.Log($"[Moonlit] {id}: {(seated ? "seated" : "standing")}, hips at {hips:F3} m");
        }

        static float SamplePose(Animator anim, AnimationClip clip, float time)
        {
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (clip == null || hips == null) return 0f;
            float before = hips.position.y - anim.transform.position.y;
            // sample the clip the way the Animation window does, then keep the bone transforms so the saved scene
            // already shows the pose (in Play mode the Animator takes over anyway)
            var root = anim.transform;
            Vector3 rootPos = root.localPosition; Quaternion rootRot = root.localRotation;
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(anim.gameObject, clip, time);
            AnimationMode.EndSampling();
            var pose = root.GetComponentsInChildren<Transform>(true).Where(t => t != root)
                .Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToList();
            AnimationMode.StopAnimationMode();
            foreach (var (t, p, r, s) in pose) { t.localPosition = p; t.localRotation = r; t.localScale = s; }
            root.localPosition = rootPos; root.localRotation = rootRot;
            float after = hips.position.y - root.position.y;
            Debug.Log($"[Moonlit] {anim.name} pose '{clip.name}': hips {before:F3} -> {after:F3} m");
            return after;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
