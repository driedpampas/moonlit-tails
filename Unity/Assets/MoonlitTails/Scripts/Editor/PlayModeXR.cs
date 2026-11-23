using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Turns XR startup off for one Play session, in memory only. The XR asset on disk (in git, used by the Quest
    /// build) never changes, and the setting comes back by itself when Play stops.
    ///
    /// Used for:
    /// - Linux: OpenXR has no editor runtime there, so XR startup is skipped on every Play and the scene's XR
    ///   Interaction Simulator drives the rig with mouse and keyboard instead. Switch it off under
    ///   Tools > Moonlit Tails > Linux: Skip OpenXR in Play Mode, e.g. to try Monado. The choice is stored per machine.
    /// - the desk checks in DevTools, which call <see cref="SkipNextPlay"/> before they press Play.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeXR
    {
        const string RequestKey = "MoonlitTails.SkipOpenXR.Request";
        const string RestoreKey = "MoonlitTails.SkipOpenXR.Restore";   // survives the domain reload into Play

        static PlayModeXR()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            if (!SessionState.GetBool(RestoreKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                SetStartup(false);                // just reloaded into Play: make sure it's still off
            else
                Restore();                        // Play ended without us seeing it (e.g. a script reload): put it back
        }

        /// <summary>Skip XR startup for the next Play session only.</summary>
        public static void SkipNextPlay() => SessionState.SetBool(RequestKey, true);

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                bool requested = SessionState.GetBool(RequestKey, false);
                SessionState.EraseBool(RequestKey);
                if ((requested || AlwaysSkip) && GetSettings() is { InitManagerOnStart: true })
                {
                    SetStartup(false);
                    SessionState.SetBool(RestoreKey, true);
                    if (!requested)
                        Debug.Log("[Moonlit] Linux editor: OpenXR is skipped for this Play session; the XR Interaction Simulator " +
                                  "drives the rig. Switch this off under " + LinuxMenu.Replace("/", " > ") + ".");
                }
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

        // ------------------------------------------------------------------ Linux: skip on every Play
        const string LinuxMenu = "Tools/Moonlit Tails/Linux: Skip OpenXR in Play Mode";
        const string LinuxPrefKey = "MoonlitTails.LinuxSkipOpenXR";

#if UNITY_EDITOR_LINUX
        static bool AlwaysSkip
        {
            get => EditorPrefs.GetBool(LinuxPrefKey, true);
            set => EditorPrefs.SetBool(LinuxPrefKey, value);
        }

        [MenuItem(LinuxMenu, priority = 200)]
        static void Toggle() => AlwaysSkip = !AlwaysSkip;

        [MenuItem(LinuxMenu, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(LinuxMenu, AlwaysSkip);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
#else
        static bool AlwaysSkip => false;
#endif
    }
}
