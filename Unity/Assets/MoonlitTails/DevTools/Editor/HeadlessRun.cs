using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// What the DevTools automation (capture, smoke test, demo video) shares before it presses Play:
    /// opening the bar scene without losing anyone's unsaved work, and the batch-mode workarounds.
    /// Nothing here saves a scene or an asset.
    /// </summary>
    public static class HeadlessRun
    {
        /// <summary>
        /// Makes sure the bar scene is the open scene. If another scene has unsaved changes, asks to save them first.
        /// Returns false if the person cancels, so the caller should stop.
        /// </summary>
        public static bool OpenBarScene(bool batch)
        {
            if (GameObject.Find("MoonlitBar") != null) return true;   // already open: play it as it is, unsaved edits included
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(MoonlitSetup.ScenePath);
            return true;
        }

        /// <summary>
        /// The XR Interaction Simulator's on-screen menu crashes in batch mode, and nothing here needs it.
        /// Only in memory: batch mode never saves the scene.
        /// </summary>
        public static void SwitchOffSimulator(string who)
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb && mb.GetType().Name.Contains("Simulator") && mb.gameObject.activeSelf)
                {
                    Debug.Log($"[Moonlit] {who}: switching off {mb.gameObject.name} for batch mode");
                    mb.gameObject.SetActive(false);
                }
        }

        /// <summary>The value after <paramref name="name"/> on the command line, or null.</summary>
        public static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>Ends a run: quits Unity with an exit code in batch mode, otherwise just stops Play.</summary>
        public static void End(bool batch, bool ok)
        {
            if (batch) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}
