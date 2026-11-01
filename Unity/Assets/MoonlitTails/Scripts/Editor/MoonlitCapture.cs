using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Desk check without a headset: plays the VR scene, renders what a standing player sees in every direction
    /// (front, down at the counter, left, right, behind, up) plus a few close-ups, then throws a glass and resets the props.
    /// Images and a report go to &lt;project&gt;/Captures. Tools > Moonlit Tails > 3. Capture Player Views.
    /// </summary>
    [InitializeOnLoad]
    public static class MoonlitCapture
    {
        const string ActiveKey = "MoonlitCapture.Active";
        const string ExitKey = "MoonlitCapture.Exit";
        public static float EyeHeight = 1.6f;

        static double startTime = -1;
        static int phase;
        static Dictionary<Rigidbody, Vector3> startPositions;
        static Rigidbody thrown;
        static Vector3 thrownFrom;
        static readonly StringBuilder report = new StringBuilder();

        static MoonlitCapture()
        {
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Moonlit Tails/3. Capture Player Views")]
        public static void RunFromMenu() => Begin(false);

        /// <summary>Command line: Unity.exe -projectPath ... -executeMethod MoonlitTails.EditorTools.MoonlitCapture.RunAndExit</summary>
        public static void RunAndExit() => Begin(true);

        static void Begin(bool exitAfter)
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, exitAfter);
            SetXRStartup(false);   // no headset on the desk: don't let OpenXR try to start
            EditorSceneManager.OpenScene(MoonlitSetup.ScenePath);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (startTime < 0) { startTime = EditorApplication.timeSinceStartup; phase = 0; report.Clear(); RecordStart(); return; }
            double t = Time.time;   // game time, so the checks don't depend on how fast this PC renders
            if (EditorApplication.timeSinceStartup - startTime > 180) { report.AppendLine("TIMEOUT"); Finish(); return; }
            try
            {
                if (phase == 0 && t > 4)
                {
                    report.AppendLine($"Play mode ran {Time.frameCount} frames, game time {Time.time:F1} s");
                    foreach (var c in UnityEngine.Object.FindFirstObjectByType<BarLayout>().characters)
                        if (c.seated) report.AppendLine($"{c.id}: measured seated hip height {c.seatedHipHeight:F3} (per metre)");
                    HandGrabReport();
                    SettleReport(); Shoot(); ThrowGlass(); phase = 1;
                }
                else if (phase == 1 && t > 7) { ThrowReport(); ResetAll(); phase = 2; }
                else if (phase == 2 && t > 8) { ResetReport(); Finish(); }
            }
            catch (Exception e) { Debug.LogException(e); report.AppendLine("EXCEPTION " + e.Message); Finish(); }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            string dir = CaptureDir();
            File.WriteAllText(Path.Combine(dir, "report.txt"), report.ToString());
            Debug.Log("[MoonlitCapture] done\n" + report);
            startTime = -1;
            bool exit = SessionState.GetBool(ExitKey, false);
            EditorApplication.isPlaying = false;
            SetXRStartup(true);
            if (exit) EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ views
        static void Shoot()
        {
            var layout = UnityEngine.Object.FindFirstObjectByType<BarLayout>();
            Transform origin = layout.xrOrigin;
            Vector3 eye = origin.position + Vector3.up * EyeHeight;
            Quaternion facing = Quaternion.Euler(0, origin.eulerAngles.y, 0);

            var go = new GameObject("CaptureCam");
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f; cam.fieldOfView = 90f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.09f, 0.2f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // (name, yaw, pitch) relative to the way the player faces; +pitch looks down
            var views = new (string, float, float)[]
            {
                ("01_front", 0, 8), ("02_front_counter", 0, 40), ("03_hands_down", 0, 65),
                ("04_left_glasses", -70, 35), ("05_right_bottles", 70, 35),
                ("06_left", -90, 5), ("07_right", 90, 5),
                ("08_behind_backbar", 180, 10), ("09_behind_counter", 180, 45), ("10_up_ceiling", 0, -60),
                ("11_front_left", -40, 12), ("12_front_right", 40, 12),
            };
            foreach (var (name, yaw, pitch) in views)
            {
                go.transform.SetPositionAndRotation(eye, facing * Quaternion.Euler(pitch, yaw, 0));
                Save(cam, name, 1600, 1200);
            }

            // close-ups of each character from the player's eye, narrower lens
            cam.fieldOfView = 45f;
            foreach (var c in layout.characters)
            {
                if (!c.character) continue;
                var anim = c.character.GetComponent<Animator>();
                var head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                Vector3 target = head ? head.position : c.character.position + Vector3.up * 0.7f;
                go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - Vector3.up * 0.25f - eye));
                Save(cam, "20_char_" + c.id, 1600, 1200);
                report.AppendLine($"{c.id}: head at {target.y:F2} m (player eye {eye.y:F2} m), {Vector3.Distance(eye, target):F2} m away");
            }

            // overview from the back corner of the room
            cam.fieldOfView = 75f;
            var bar = layout.environment;
            var serve = bar.GetComponentsInChildren<Transform>(true).First(t => t.name == "MK_ServeSpot");
            Vector3 fwd = (serve.position - origin.position); fwd.y = 0; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 overviewPos = origin.position - fwd * 1.2f + right * 3.6f + Vector3.up * 3.0f;
            go.transform.SetPositionAndRotation(overviewPos, Quaternion.LookRotation(origin.position + fwd * 2.5f - right * 0.5f - overviewPos));
            Save(cam, "30_overview", 1600, 1000);
            UnityEngine.Object.Destroy(go);
        }

        static void Save(Camera cam, string name, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(CaptureDir(), name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        // ------------------------------------------------------------------ physics checks
        static IEnumerable<Rigidbody> Props() =>
            UnityEngine.Object.FindObjectsByType<PropRespawn>(FindObjectsSortMode.None).Select(p => p.GetComponent<Rigidbody>());

        static void RecordStart() => startPositions = Props().ToDictionary(r => r, r => r.position);

        static void SettleReport()
        {
            var moved = startPositions.Where(kv => kv.Key && Vector3.Distance(kv.Key.position, kv.Value) > 0.05f)
                .Select(kv => $"{kv.Key.name} ({Vector3.Distance(kv.Key.position, kv.Value):F2} m)").ToList();
            report.AppendLine($"Settle: {startPositions.Count} props, {moved.Count} moved more than 5 cm in the first {Time.time:F1} s" + (moved.Count > 0 ? ": " + string.Join(", ", moved) : ""));
        }

        static void ThrowGlass()
        {
            thrown = Props().FirstOrDefault(r => r.name.Contains("Glass_Rocks_01")) ?? Props().First();
            var layout = UnityEngine.Object.FindFirstObjectByType<BarLayout>();
            thrownFrom = thrown.position;
            // a gentle toss from above the counter toward the back bar
            thrown.position = layout.xrOrigin.position + Vector3.up * 1.3f;
            thrown.linearVelocity = -layout.xrOrigin.forward * 3f + Vector3.up * 1.5f;
            thrown.angularVelocity = new Vector3(4, 1, 2);
        }

        static void ThrowReport()
        {
            var layout = UnityEngine.Object.FindFirstObjectByType<BarLayout>();
            float floorY = layout.xrOrigin.position.y;
            report.AppendLine($"Throw: {thrown.name} ended at height {thrown.position.y - floorY:F2} m above the player floor, " +
                              $"{Vector3.Distance(thrown.position, layout.xrOrigin.position):F2} m from the player, speed now {thrown.linearVelocity.magnitude:F2} m/s");
        }

        static void ResetAll()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<PropRespawn>(FindObjectsSortMode.None)) p.SendHome();
        }

        static void ResetReport()
        {
            report.AppendLine($"Reset: {thrown.name} is {Vector3.Distance(thrown.position, thrownFrom):F3} m from its home spot");
        }

        // feeds HandGrab a fake hand movement (no headset here) to check the throw speed it would give
        static void HandGrabReport()
        {
            var grabs = Resources.FindObjectsOfTypeAll<HandGrab>().Where(h => h.gameObject.scene.IsValid()).ToList();
            report.AppendLine($"HandGrab components: {grabs.Count} ({string.Join(", ", grabs.Select(g => g.handedness + " on " + g.name))})");
            var hg = grabs.FirstOrDefault();
            if (hg == null) return;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var history = (List<(float t, Vector3 p, Quaternion r)>)typeof(HandGrab).GetField("history", flags).GetValue(hg);
            history.Clear();
            // 90 Hz samples: hand speeds up to 3 m/s forward, then slows to 1 m/s while the fingers open
            float t = 0f; Vector3 p = Vector3.zero;
            float[] speeds = { 0.5f, 1f, 1.8f, 2.5f, 3f, 3f, 2.6f, 2f, 1.4f, 1f };
            foreach (var s in speeds) { t += 1f / 90f; p += Vector3.forward * s / 90f; history.Add((t, p, Quaternion.Euler(0, t * 180f, 0))); }
            var args = new object[] { Vector3.zero, Vector3.zero };
            typeof(HandGrab).GetMethod("HandVelocity", flags).Invoke(hg, args);
            var v = (Vector3)args[0]; var w = (Vector3)args[1];
            report.AppendLine($"Throw speed from a hand peaking at 3.0 m/s and slowing to 1.0 m/s at release: {v.magnitude:F2} m/s (spin {w.magnitude * Mathf.Rad2Deg:F0} deg/s, hand turned 180 deg/s)");
            history.Clear();
        }

        // ------------------------------------------------------------------ helpers
        static string CaptureDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        static void SetXRStartup(bool on)
        {
            UnityEditor.EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget per);
            var s = per ? per.SettingsForBuildTarget(BuildTargetGroup.Standalone) : null;   // editor Play mode uses the PC settings
            if (s == null) return;
            s.InitManagerOnStart = on;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
        }
    }
}
