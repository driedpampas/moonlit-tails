#if UNITY_EDITOR_LINUX
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Lets you press Play in the Linux editor without a headset.
    ///
    /// OpenXR has no editor runtime on Linux, so XR startup fails on Play. While this is on (the default), XR startup
    /// is turned off for the length of each Play session, and the scene's XR Interaction Simulator drives the rig
    /// with mouse and keyboard instead. The project setting goes back to what it was when Play stops, and it is never
    /// saved, so the XR asset in git and the Quest build don't change.
    ///
    /// Tools > Moonlit Tails > Linux: Skip OpenXR in Play Mode switches it off, e.g. to try a Linux OpenXR runtime
    /// such as Monado. The choice is stored per machine.
    /// </summary>
    [InitializeOnLoad]
    static class LinuxPlayModeXR
    {
        const string MenuPath = "Tools/Moonlit Tails/Linux: Skip OpenXR in Play Mode";
        const string PrefKey = "MoonlitTails.LinuxSkipOpenXR";
        const string RestoreKey = "MoonlitTails.LinuxSkipOpenXR.Restore";   // survives the domain reload into Play

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static LinuxPlayModeXR()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            if (!SessionState.GetBool(RestoreKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                SetStartup(false);                // just reloaded into Play: make sure it's still off
            else
                Restore();                        // Play ended without us seeing it (e.g. a script reload): put it back
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && Enabled && GetSettings() is { InitManagerOnStart: true })
            {
                SetStartup(false);
                SessionState.SetBool(RestoreKey, true);
                Debug.Log("[Moonlit] Linux editor: OpenXR is skipped for this Play session; the XR Interaction Simulator " +
                          "drives the rig. Switch this off under " + MenuPath.Replace("/", " > ") + ".");
            }
            // restore only once Play has fully ended: XR Management reads the same flag while exiting Play
            else if (state == PlayModeStateChange.EnteredEditMode)
                Restore();
        }

        static void Restore()
        {
            if (!SessionState.GetBool(RestoreKey, false)) return;
            SetStartup(true);
            SessionState.EraseBool(RestoreKey);
        }

        static XRGeneralSettings GetSettings()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget per);
            return per ? per.SettingsForBuildTarget(BuildTargetGroup.Standalone) : null;   // editor Play mode uses the PC settings
        }

        // in memory only, not marked dirty: the asset on disk keeps XR startup on
        static void SetStartup(bool on)
        {
            var s = GetSettings();
            if (s) s.InitManagerOnStart = on;
        }

        [MenuItem(MenuPath, priority = 200)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
#endif
