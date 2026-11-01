#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VRCharacters.EditorTools
{
    /// <summary>
    /// One-click setup for characters built on the shared rig (Unity Humanoid + Tail_01..07 + Ear_L/R_01..02
    /// + optional Hair_* / Coat_* physics chains).
    ///   1. Select the character FBX in the Project window.
    ///   2. Tools > VR Characters > 1. Configure Model Import   (Humanoid, blendshapes, loop/additive clips, ASTC atlas)
    ///   3. Tools > VR Characters > 2. Build Character Prefab   (URP materials, springs, colliders, blink, animator)
    /// </summary>
    public static class VRCharacterSetup
    {
        const string Menu = "Tools/VR Characters/";

        [MenuItem(Menu + "1. Configure Model Import")]
        static void ConfigureImport()
        {
            string path = SelectedModelPath(); if (path == null) return;
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.importBlendShapes = true;
            imp.importVisibility = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;   // keeps slot names (M_Hair, M_Coat, ...)
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.optimizeGameObjects = false;            // springs need the chain transforms
            imp.SaveAndReimport();

            imp = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                string n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                c.name = n;
                c.loopTime = true;
                c.loopPose = n.StartsWith("Idle");
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
                if (n.Contains("Additive"))
                {
                    c.hasAdditiveReferencePose = true;
                    c.additiveReferencePoseFrame = 0f;
                }
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();

            // atlas texture: sRGB, mipmaps, ASTC 6x6 on Android (Quest)
            foreach (var texPath in Directory.GetFiles(Path.GetDirectoryName(path), "*.png", SearchOption.AllDirectories))
            {
                var ti = AssetImporter.GetAtPath(texPath.Replace('\\', '/')) as TextureImporter;
                if (ti == null) continue;
                ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.alphaSource = TextureImporterAlphaSource.None;
                ti.anisoLevel = 2; ti.filterMode = FilterMode.Bilinear; ti.maxTextureSize = 2048;
                var android = ti.GetPlatformTextureSettings("Android");
                android.overridden = true; android.maxTextureSize = 2048; android.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
            }
            Debug.Log($"[VR Characters] Import configured for {path}");
        }

        [MenuItem(Menu + "2. Build Character Prefab")]
        static void BuildPrefab()
        {
            string path = SelectedModelPath(); if (path == null) return;
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            go.name = baseName;

            // ---------- materials (one per piece, all sharing the atlas)
            var atlas = AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.ToLower().Contains("atlas"))
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault();
            string matDir = dir + "/Materials"; if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(dir, "Materials");
            var cache = new Dictionary<string, Material>();
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = smr.sharedMaterials;
                var names = MaterialSlotNames(smr);
                for (int i = 0; i < mats.Length; i++)
                {
                    string slot = names.Count > i ? names[i] : (mats[i] != null ? mats[i].name : "M_Slot" + i);
                    if (!cache.TryGetValue(slot, out var m)) { m = MakeMaterial(slot, atlas, matDir); cache[slot] = m; }
                    mats[i] = m;
                }
                smr.sharedMaterials = mats;
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Bone4;
            }

            // ---------- spring chains (tail, ears, hair, coat) + body colliders
            var springs = go.AddComponent<SpringBoneSystem>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.EndsWith("_01")) continue;
                string label = t.name.Substring(0, t.name.Length - 3);
                var c = new SpringBoneSystem.Chain { label = label, root = t };
                if (label.StartsWith("Tail")) { c.stiffness = 0.22f; c.drag = 0.25f; c.gravity = new Vector3(0, -0.35f, 0); c.radius = 0.03f; c.windInfluence = 0.6f; }
                else if (label.StartsWith("Ear")) { c.stiffness = 0.65f; c.drag = 0.35f; c.gravity = Vector3.zero; c.radius = 0.01f; c.windInfluence = 0.3f; }
                else if (label.StartsWith("Hair")) { c.stiffness = 0.3f; c.drag = 0.25f; c.gravity = new Vector3(0, -0.6f, 0); c.radius = 0.01f; c.windInfluence = 1f; }
                else if (label.StartsWith("Coat")) { c.stiffness = 0.18f; c.drag = 0.3f; c.gravity = new Vector3(0, -0.9f, 0); c.radius = 0.02f; c.windInfluence = 0.8f; }
                else continue;
                springs.chains.Add(c);
            }
            var anim = go.GetComponent<Animator>();
            AddCollider(springs, Find(go.transform, "Hips"), SpringBoneCollider.Shape.Sphere, 0.075f, new Vector3(0, 0.02f, 0), Vector3.zero);
            AddCollider(springs, Find(go.transform, "Chest"), SpringBoneCollider.Shape.Sphere, 0.075f, Vector3.zero, Vector3.zero);
            AddCollider(springs, Find(go.transform, "Head"), SpringBoneCollider.Shape.Sphere, 0.105f, new Vector3(0, 0.1f, 0), Vector3.zero);
            foreach (var s in new[] { "Left", "Right" })
            {
                AddLimbCollider(springs, Find(go.transform, s + "UpperLeg"), 0.05f);
                AddLimbCollider(springs, Find(go.transform, s + "LowerLeg"), 0.042f);
                AddLimbCollider(springs, Find(go.transform, s + "UpperArm"), 0.04f);     // long hair hanging beside the arms
                AddLimbCollider(springs, Find(go.transform, s + "LowerArm"), 0.035f);
            }

            // ---------- blink + animator
            var blink = go.AddComponent<BlinkController>();
            blink.target = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("Blink") >= 0);
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = BuildController(path, dir, baseName, go.transform);
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            string prefabPath = dir + "/" + baseName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VR Characters] Prefab saved: {prefabPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        // -------------------------------------------------------------------------------- helpers
        static string SelectedModelPath()
        {
            var o = Selection.activeObject;
            string p = o != null ? AssetDatabase.GetAssetPath(o) : null;
            if (string.IsNullOrEmpty(p) || !(AssetImporter.GetAtPath(p) is ModelImporter))
            {
                EditorUtility.DisplayDialog("VR Characters", "Select the character's FBX in the Project window first.", "OK");
                return null;
            }
            return p;
        }

        static List<string> MaterialSlotNames(SkinnedMeshRenderer smr)
        {
            // slot names come from the FBX materials (M_Hair, M_Fur, M_Coat ...)
            return smr.sharedMaterials.Select(m => m != null ? m.name.Replace(" (Instance)", "") : "").ToList();
        }

        static Material MakeMaterial(string slot, Texture2D atlas, string matDir)
        {
            string file = matDir + "/" + slot + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(file);
            if (existing != null) return existing;
            bool eyes = slot.StartsWith("M_Eye");
            bool gold = slot.StartsWith("M_Gold");          // true metal (bells, kintsugi lines): URP/Lit, metallic
            var shader = Shader.Find(eyes ? "Universal Render Pipeline/Unlit" : gold ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find(eyes ? "Unlit/Texture" : "Mobile/Diffuse");   // built-in fallback
            var m = new Material(shader) { name = slot };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", atlas);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", atlas);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (gold)
            {
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 1f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.7f);
            }
            else if (!eyes && m.HasProperty("_Smoothness"))
            {
                float smooth = 0.15f; Color spec = new Color(0.08f, 0.08f, 0.08f);
                switch (slot)
                {
                    case "M_Hair": smooth = 0.35f; spec = new Color(0.12f, 0.12f, 0.14f); break;
                    case "M_Leather": case "M_Shoes": case "M_Bag": smooth = 0.45f; spec = new Color(0.15f, 0.15f, 0.15f); break;
                    case "M_Metal": smooth = 0.7f; spec = new Color(0.6f, 0.6f, 0.62f); break;
                }
                m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_SpecColor")) m.SetColor("_SpecColor", spec);
                m.EnableKeyword("_SPECULAR_COLOR");
            }
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, file);
            return m;
        }

        static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        static void AddCollider(SpringBoneSystem s, Transform bone, SpringBoneCollider.Shape shape, float r, Vector3 a, Vector3 b)
        {
            if (bone == null) return;
            var holder = new GameObject("SpringCollider");
            holder.transform.SetParent(bone, false);
            var c = holder.AddComponent<SpringBoneCollider>();
            c.shape = shape; c.radius = r; c.offset = a; c.endOffset = b;
            s.colliders.Add(c);
        }

        static void AddLimbCollider(SpringBoneSystem s, Transform bone, float r)
        {
            if (bone == null || bone.childCount == 0) return;
            Vector3 end = bone.GetChild(0).localPosition;
            AddCollider(s, bone, SpringBoneCollider.Shape.Capsule, r, Vector3.zero, end);
        }

        static RuntimeAnimatorController BuildController(string modelPath, string dir, string baseName, Transform root)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToList();
            string ctrlPath = dir + "/" + baseName + "_Animator.controller";
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

            // base layer: idles
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState first = null;
            foreach (var c in clips.Where(c => c.name.StartsWith("Idle")))
            {
                var st = sm.AddState(c.name); st.motion = c;
                if (first == null) first = st;
            }
            if (first != null) sm.defaultState = first;

            // additive layers for the generic tail / ear bones (work on any tail curl)
            AddAdditiveLayer(ctrl, clips.FirstOrDefault(c => c.name.StartsWith("Tail")), "Tail", root, "Tail_", dir, baseName);
            AddAdditiveLayer(ctrl, clips.FirstOrDefault(c => c.name.StartsWith("Ears")), "Ears", root, "Ear_", dir, baseName);
            return ctrl;
        }

        static void AddAdditiveLayer(AnimatorController ctrl, AnimationClip clip, string layerName, Transform root, string prefix, string dir, string baseName)
        {
            if (clip == null) return;
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            var bones = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(prefix)).ToList();
            mask.transformCount = bones.Count;
            for (int i = 0; i < bones.Count; i++)
            {
                mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(bones[i], root));
                mask.SetTransformActive(i, true);
            }
            AssetDatabase.CreateAsset(mask, $"{dir}/{baseName}_{layerName}Mask.mask");

            ctrl.AddLayer(layerName);
            var layers = ctrl.layers;
            var L = layers[layers.Length - 1];
            L.blendingMode = AnimatorLayerBlendingMode.Additive;
            L.defaultWeight = 1f;
            L.avatarMask = mask;
            var st = L.stateMachine.AddState(clip.name); st.motion = clip;
            L.stateMachine.defaultState = st;
            ctrl.layers = layers;
        }
    }
}
#endif
