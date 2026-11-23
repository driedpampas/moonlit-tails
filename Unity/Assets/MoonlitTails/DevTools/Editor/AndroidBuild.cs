using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// Builds the Quest APK from the scenes in Build Settings. Command line:
    /// Unity -batchmode -quit -projectPath Unity -executeMethod MoonlitTails.EditorTools.AndroidBuild.Build
    ///   [-apkPath out.apk] [-jdk dir] [-sdk dir] [-ndk dir]
    /// Saves no scene or asset. The JDK/SDK/NDK paths only go into the editor's per-user preferences.
    /// </summary>
    public static class AndroidBuild
    {
        [MenuItem("Tools/Moonlit Tails/Automation/Build Android APK")]
        public static void Build()
        {
            bool batch = Application.isBatchMode;
            string apk = HeadlessRun.Arg("-apkPath") ?? "Captures/Build/MoonlitTails.apk";
            string jdk = HeadlessRun.Arg("-jdk"), sdk = HeadlessRun.Arg("-sdk"), ndk = HeadlessRun.Arg("-ndk");
            if (jdk != null) AndroidExternalToolsSettings.jdkRootPath = jdk;
            if (sdk != null) AndroidExternalToolsSettings.sdkRootPath = sdk;
            if (ndk != null) AndroidExternalToolsSettings.ndkRootPath = ndk;

            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(apk)));

            // XR Management adds its settings to the preloaded assets while building. If a build is interrupted they
            // stay in ProjectSettings.asset, so put the list back afterwards whatever happens.
            var preloaded = PlayerSettings.GetPreloadedAssets();
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = apk,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                PlayerSettings.SetPreloadedAssets(preloaded);
            }
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[Moonlit] Android build {report.summary.result}: {apk} ({report.summary.totalSize / 1048576} MB, {report.summary.totalErrors} errors)");
            if (batch) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
