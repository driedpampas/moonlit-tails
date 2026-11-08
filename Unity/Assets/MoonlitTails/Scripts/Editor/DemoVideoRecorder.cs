using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Records a progress video without a headset. It plays the bar scene, makes drinks by moving the real props,
    /// films it with a scripted camera and writes an .mp4 with captions to &lt;project&gt;/Captures/Demo.
    /// It needs ffmpeg on the PATH.
    /// Tools > Moonlit Tails > 6. Record Demo Video, or from the command line (editor closed, but a display needed):
    /// Unity -batchmode -projectPath Unity -executeMethod MoonlitTails.EditorTools.DemoVideoRecorder.RunBatch
    /// Add -demoSize 960x540 for a quick, small test render. It never saves the scene.
    /// </summary>
    [InitializeOnLoad]
    public static class DemoVideoRecorder
    {
        const string Key = "MoonlitDemoVideo";
        const string BatchKey = "MoonlitDemoVideoBatch";

        static DemoVideoRecorder()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Moonlit Tails/6. Record Demo Video")]
        public static void Run() => Start(false);

        public static void RunBatch() => Start(true);

        static void Start(bool batch)
        {
            if (GameObject.Find("MoonlitBar") == null) EditorSceneManager.OpenScene(MoonlitSetup.ScenePath);
            if (batch)
            {
                // the XR Interaction Simulator's on-screen menu crashes in batch mode (batch mode never saves the scene)
                foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (mb && mb.GetType().Name.Contains("Simulator") && mb.gameObject.activeSelf)
                        mb.gameObject.SetActive(false);
            }
            SessionState.SetBool(Key, true);
            SessionState.SetBool(BatchKey, batch);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (Object.FindFirstObjectByType<DemoDirector>() != null) return;
            if (Time.time < 1f || Object.FindFirstObjectByType<Gameplay.DrinkGame>() == null) return;

            if (started) { Debug.LogError("[Moonlit] Demo: the director went away before the video was finished"); Stop(false); return; }
            started = true;
            startedAt = EditorApplication.timeSinceStartup;
            var director = new GameObject("Demo Director").AddComponent<DemoDirector>();
            if (director == null) { Debug.LogError("[Moonlit] Demo: couldn't start the director"); Stop(false); return; }
            ParseSize(out director.width, out director.height);
            director.Done = Stop;
        }

        static bool started;
        static double startedAt;

        static void Stop(bool ok)
        {
            EditorApplication.update -= Tick;
            EditorApplication.update -= Watchdog;
            SessionState.SetBool(Key, false);
            started = false;
            if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        // a stuck script shouldn't keep the editor busy forever
        [InitializeOnLoadMethod]
        static void HookWatchdog() { EditorApplication.update -= Watchdog; EditorApplication.update += Watchdog; }

        static void Watchdog()
        {
            if (!started || EditorApplication.timeSinceStartup - startedAt < 45 * 60) return;
            Debug.LogError("[Moonlit] Demo: still not done after 45 minutes, giving up");
            Stop(false);
        }

        static void ParseSize(out int w, out int h)
        {
            w = 1920; h = 1080;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-demoSize") continue;
                var p = args[i + 1].Split('x');
                if (p.Length == 2 && int.TryParse(p[0], out int a) && int.TryParse(p[1], out int b)) { w = a; h = b; }
            }
        }
    }
}
