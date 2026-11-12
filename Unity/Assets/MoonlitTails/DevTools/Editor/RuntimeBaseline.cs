using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MoonlitTails.Gameplay;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Measures our own scripts in one Play session: garbage (GC bytes) and time per frame, read from the Profiler,
    /// while idle, with a glass on the coaster, and while pouring. Run it before and after performance work.
    /// Writes Unity/Captures/runtime-baseline.json (git-ignored) and logs a summary. Never saves a scene or an asset.
    /// Tools > Moonlit Tails > Automation > Record Runtime Baseline, or from the command line:
    /// Unity -batchmode -projectPath Unity -executeMethod MoonlitTails.EditorTools.RuntimeBaseline.RunBatch
    /// </summary>
    [InitializeOnLoad]
    public static class RuntimeBaseline
    {
        const string Key = "MoonlitRuntimeBaseline";
        const string BatchKey = "MoonlitRuntimeBaselineBatch";
        const float WindowSeconds = 3f;
        const int MeasureFps = 72;   // the Quest 2's default refresh rate; also keeps a window inside the Profiler's history

        const string DeadlineKey = "MoonlitRuntimeBaselineDeadline";
        const string ProfileEditorKey = "MoonlitRuntimeBaselineProfileEditor";
        const string ProfilerEnabledKey = "MoonlitRuntimeBaselineProfilerEnabled";
        const double SettleTimeoutSeconds = 60;

        static RuntimeBaseline()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
            // Play stopped by hand mid-run: drop the flag and put the Profiler back, so the next Play is a normal one
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            if (gcInFrame.Valid) gcInFrame.Dispose();
            RestoreProfiler();
            Debug.LogWarning("[Moonlit] Runtime baseline: Play stopped before it finished; nothing was written.");
        }

        static void RestoreProfiler()
        {
            ProfilerDriver.enabled = SessionState.GetBool(ProfilerEnabledKey, false);
            ProfilerDriver.profileEditor = SessionState.GetBool(ProfileEditorKey, false);
        }

        [MenuItem("Tools/Moonlit Tails/Automation/Record Runtime Baseline")]
        public static void Run() => Start(false);

        public static void RunBatch() => Start(true);

        static void Start(bool batch)
        {
            if (!HeadlessRun.OpenBarScene(batch)) return;
            if (batch) HeadlessRun.SwitchOffSimulator("Runtime baseline");
            PlayModeXR.SkipNextPlay();
            SessionState.SetBool(Key, true);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetBool(ProfilerEnabledKey, ProfilerDriver.enabled);      // restored when the run ends
            SessionState.SetBool(ProfileEditorKey, ProfilerDriver.profileEditor);
            SessionState.SetFloat(DeadlineKey, (float)(EditorApplication.timeSinceStartup + SettleTimeoutSeconds));
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        // ------------------------------------------------------------------ the run
        enum Phase { Settle, Idle, Coaster, Pouring, Done }
        static Phase phase;
        static float phaseEnd;
        static int windowFirstFrame;
        static int previousFps = int.MinValue;   // MinValue = not changed yet
        static bool finishing;
        static readonly List<string> windows = new List<string>();
        static readonly StringBuilder summary = new StringBuilder();
        static ProfilerRecorder gcInFrame;
        static long gcTotal; static int gcFrames;

        // pouring
        static IngredientSource bottle;
        static DrinkContainer cup;
        static Quaternion bottleUpright;
        static float nextTilt; static bool tipped;

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try { Step(); }
            catch (Exception e) { Debug.LogException(e); summary.AppendLine("FAILED: " + e.Message); Finish(false); }
        }

        static void Step()
        {
            if (gcInFrame.Valid && gcInFrame.LastValue > 0) { gcTotal += gcInFrame.LastValue; gcFrames++; }
            switch (phase)
            {
                case Phase.Settle:
                    // the floating labels exist by now
                    if (Time.time < 3f || UnityEngine.Object.FindFirstObjectByType<DrinkGame>() == null)
                    {
                        if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, float.MaxValue))
                            throw new TimeoutException($"the drink game didn't start within {SettleTimeoutSeconds} s");
                        return;
                    }
                    previousFps = Application.targetFrameRate;
                    Application.targetFrameRate = MeasureFps;   // play mode only; not saved
                    ProfilerDriver.ClearAllFrames();
                    ProfilerDriver.profileEditor = false;
                    ProfilerDriver.enabled = true;
                    gcInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                    BeginWindow(Phase.Idle);
                    return;

                case Phase.Idle:
                    if (Time.time < phaseEnd) return;
                    EndWindow("idle");
                    var glass = DrinkContainer.All.First(c => c.IsGlass && c.variant == "Highball");
                    Hold(glass.transform);
                    var station = UnityEngine.Object.FindFirstObjectByType<ServeStation>();
                    station.TrySnap(glass);
                    BeginWindow(Phase.Coaster);
                    return;

                case Phase.Coaster:
                    if (Time.time < phaseEnd) return;
                    EndWindow("glass on the coaster");
                    cup = DrinkContainer.All.First(c => c.kind == ContainerKind.StirCup);
                    bottle = UnityEngine.Object.FindObjectsByType<IngredientSource>(FindObjectsSortMode.None).First();
                    Hold(bottle.transform);
                    bottleUpright = bottle.transform.rotation;
                    tipped = false; nextTilt = Time.time;
                    BeginWindow(Phase.Pouring);
                    return;

                case Phase.Pouring:
                    // tip, pour (0.5 s), stand it up to re-arm, repeat
                    if (Time.time >= nextTilt)
                    {
                        if (tipped) bottle.transform.rotation = bottleUpright;
                        else TipOver(bottle, cup);
                        tipped = !tipped;
                        nextTilt = Time.time + (tipped ? 0.7f : 0.2f);
                        Physics.SyncTransforms();
                    }
                    if (Time.time < phaseEnd) return;
                    EndWindow("pouring");
                    Finish(true);
                    return;
            }
        }

        static void BeginWindow(Phase next)
        {
            phase = next;
            phaseEnd = Time.time + WindowSeconds;
            windowFirstFrame = ProfilerDriver.lastFrameIndex + 1;
            gcTotal = 0; gcFrames = 0;
        }

        static void EndWindow(string name)
        {
            int last = ProfilerDriver.lastFrameIndex;
            int first = Mathf.Max(windowFirstFrame, ProfilerDriver.firstFrameIndex);
            var perType = new Dictionary<string, (double gc, double ms)>();
            int frames = 0;
            for (int f = first; f <= last; f++)
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                                                                         HierarchyFrameDataView.columnGcMemory, false);
                if (view == null || !view.valid) continue;
                frames++;
                Collect(view, view.GetRootItemID(), perType);
            }
            double frameGc = gcFrames > 0 ? (double)gcTotal / gcFrames : 0;
            var rows = perType.OrderByDescending(p => p.Value.gc).ThenByDescending(p => p.Value.ms).ToList();
            double ourGc = frames > 0 ? rows.Sum(r => r.Value.gc) / frames : 0;
            double ourMs = frames > 0 ? rows.Sum(r => r.Value.ms) / frames : 0;

            summary.AppendLine($"{name}: {frames} profiled frames; our scripts {ourGc:F0} B/frame GC, {ourMs:F3} ms/frame; " +
                               $"whole frame incl. editor ~{frameGc:F0} B/frame GC (approximate: sampled once per editor update)");
            foreach (var r in rows.Where(r => r.Value.gc > 0 || r.Value.ms / Math.Max(frames, 1) >= 0.001))
                summary.AppendLine($"    {r.Key,-18} {r.Value.gc / Math.Max(frames, 1),8:F0} B/frame  {r.Value.ms / Math.Max(frames, 1),7:F3} ms/frame");

            var sb = new StringBuilder();
            sb.Append($"{{\"state\":{Str(name)},\"profiledFrames\":{frames},\"ourGcBytesPerFrame\":{Num(ourGc)},\"ourMsPerFrame\":{Num(ourMs)},")
              .Append($"\"wholeFrameGcBytesPerFrameInclEditorApprox\":{Num(frameGc)},\"scripts\":[");
            sb.Append(string.Join(",", rows.Select(r =>
                $"{{\"type\":{Str(r.Key)},\"gcBytesPerFrame\":{Num(r.Value.gc / Math.Max(frames, 1))},\"msPerFrame\":{Num(r.Value.ms / Math.Max(frames, 1))}}}")));
            sb.Append("]}");
            windows.Add(sb.ToString());
        }

        static readonly string[] OurAssemblies = { "MoonlitTails.Runtime.dll!", "VRCharacters.Runtime.dll!" };

        /// <summary>
        /// The script type of a Profiler sample from one of our assemblies, or null. Script samples are named like
        /// "MoonlitTails.Runtime.dll!MoonlitTails.Gameplay::ServeStation.Update() [Invoke]".
        /// </summary>
        static string OurScript(string sample)
        {
            if (!OurAssemblies.Any(sample.StartsWith)) return null;
            int sep = sample.IndexOf("::", StringComparison.Ordinal);
            if (sep < 0) return null;
            string rest = sample.Substring(sep + 2);
            int dot = rest.IndexOf('.');
            return dot > 0 ? rest.Substring(0, dot) : rest;
        }

        /// <summary>Adds up the samples of our own scripts, per script type.</summary>
        static void Collect(HierarchyFrameDataView view, int id, Dictionary<string, (double gc, double ms)> perType)
        {
            var children = new List<int>();
            view.GetItemChildren(id, children);
            foreach (var c in children)
            {
                string type = OurScript(view.GetItemName(c));
                if (type != null)
                {
                    perType.TryGetValue(type, out var acc);
                    acc.gc += view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnGcMemory);
                    acc.ms += view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
                    perType[type] = acc;
                    continue;   // its children are already counted in its totals
                }
                Collect(view, c, perType);
            }
        }

        static void Finish(bool ok)
        {
            if (finishing) return;   // a throw inside Finish must not re-enter it from Tick's catch
            finishing = true;
            try
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(Key, false);
                if (gcInFrame.Valid) gcInFrame.Dispose();
                RestoreProfiler();
                if (previousFps != int.MinValue) Application.targetFrameRate = previousFps;

                // a failed run never replaces a good baseline
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, ok ? "runtime-baseline.json" : "runtime-baseline.failed.json");
                File.WriteAllText(path, "{\n\"performance\":[\n" + string.Join(",\n", windows) + "\n]\n}\n");
                if (ok) Debug.Log($"[Moonlit] Runtime baseline -> {path}\n{summary}");
                else Debug.LogError($"[Moonlit] Runtime baseline FAILED (partial data in {path})\n{summary}");
            }
            catch (Exception e)
            {
                ok = false;
                Debug.LogException(e);
            }
            finally
            {
                phase = Phase.Settle; windows.Clear(); summary.Clear();
                previousFps = int.MinValue; finishing = false;
                HeadlessRun.End(SessionState.GetBool(BatchKey, false), ok);
            }
        }

        // ------------------------------------------------------------------ JSON
        static string Num(double d) => d.ToString("0.#####", CultureInfo.InvariantCulture);

        static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.Append($"\\u{(int)c:x4}"); else sb.Append(c); break;
                }
            return sb.Append('"').ToString();
        }

        // ------------------------------------------------------------------ moving props (play mode only)
        static void Hold(Transform t)
        {
            var rb = t.GetComponent<Rigidbody>();
            if (rb) { if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
        }

        static void TipOver(IngredientSource source, DrinkContainer into)
        {
            var spout = source.GetComponent<Pourer>();
            source.transform.rotation = Quaternion.AngleAxis(130f, Vector3.right) * bottleUpright;
            source.transform.position += into.OpeningWorld + Vector3.up * 0.12f - spout.SpoutWorld;
        }
    }
}
