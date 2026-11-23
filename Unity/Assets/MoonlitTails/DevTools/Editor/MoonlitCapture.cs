using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MoonlitTails.Gameplay;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Desk check without a headset: plays the VR scene, renders what a standing player sees in every direction
    /// (front, down at the counter, left, right, behind, up) plus a few close-ups, then throws a glass and resets the props.
    /// Images and a report go to &lt;project&gt;/Captures. Tools > Moonlit Tails > Automation > Capture Player Views.
    /// It never saves the scene, and XR is skipped for this one Play session only (in memory, see PlayModeXR).
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

        [MenuItem("Tools/Moonlit Tails/Automation/Capture Player Views")]
        public static void RunFromMenu() => Begin(false);

        /// <summary>Command line: Unity.exe -projectPath ... -executeMethod MoonlitTails.EditorTools.MoonlitCapture.RunAndExit</summary>
        public static void RunAndExit() => Begin(true);

        static void Begin(bool exitAfter)
        {
            if (!HeadlessRun.OpenBarScene(exitAfter)) return;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, exitAfter);
            PlayModeXR.SkipNextPlay();   // no headset on the desk: don't let OpenXR try to start
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
            string text = report.ToString();
            HeadlessRun.End(SessionState.GetBool(ExitKey, false), !text.Contains("TIMEOUT") && !text.Contains("EXCEPTION"));
        }

        // ------------------------------------------------------------------ views
        static void Shoot()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<DrinkGame>();
            Transform origin = Origin();
            Vector3 eye = origin.position + Vector3.up * EyeHeight;
            Quaternion facing = Quaternion.Euler(0, origin.eulerAngles.y, 0);

            var go = new GameObject("CaptureCam");
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f; cam.fieldOfView = 90f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.09f, 0.2f);

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
            foreach (var c in game.Seats)
            {
                if (!c.Body) continue;
                var anim = c.Body.GetComponent<Animator>();
                var head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                Vector3 target = head ? head.position : c.Body.position + Vector3.up * 0.7f;
                go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - Vector3.up * 0.25f - eye));
                Save(cam, "20_char_" + c.Body.name, 1600, 1200);
                report.AppendLine($"{c.Body.name} ({c.DisplayName}): head at {target.y:F2} m (player eye {eye.y:F2} m), {Vector3.Distance(eye, target):F2} m away");
            }

            // overview from the back corner of the room
            cam.fieldOfView = 75f;
            var serve = game.Station.ServeSpot;
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
            var rocks = DrinkContainer.All.FirstOrDefault(c => c.IsGlass && c.variant == "Rocks");
            thrown = rocks ? rocks.GetComponent<Rigidbody>() : Props().First();
            var origin = Origin();
            thrownFrom = thrown.position;
            // a gentle toss from above the counter toward the back bar
            thrown.position = origin.position + Vector3.up * 1.3f;
            thrown.linearVelocity = -origin.forward * 3f + Vector3.up * 1.5f;
            thrown.angularVelocity = new Vector3(4, 1, 2);
        }

        static void ThrowReport()
        {
            var origin = Origin();
            float floorY = origin.position.y;
            report.AppendLine($"Throw: {thrown.name} ended at height {thrown.position.y - floorY:F2} m above the player floor, " +
                              $"{Vector3.Distance(thrown.position, origin.position):F2} m from the player, speed now {thrown.linearVelocity.magnitude:F2} m/s");
        }

        static void ResetAll()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<PropRespawn>(FindObjectsSortMode.None)) p.SendHome();
        }

        static void ResetReport()
        {
            report.AppendLine($"Reset: {thrown.name} is {Vector3.Distance(thrown.position, thrownFrom):F3} m from its home spot");
        }

        // ------------------------------------------------------------------ helpers
        static Transform Origin() => UnityEngine.Object.FindFirstObjectByType<XROrigin>().transform;

        static string CaptureDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
