// Player settings, and the Apple builds: a Mac app to play at once, and Xcode projects for iPhone/iPad
// and Apple TV (archive those in Xcode and upload to TestFlight). Adapted from The Black Bird's.
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Schofield.EditorTools
{
    public static class BuildTools
    {
        public const string BundleId = "com.vspayce.schofield";
        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        static string BuildsDir => Path.Combine(ProjectRoot, "Builds");

        [MenuItem("Schofield/Configure Player Settings", priority = 20)]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "vspayce";
            PlayerSettings.productName = "Schofield";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            // phones, landscape only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;   // edge swipes don't steal the aim
            foreach (var nb in new[] { UnityEditor.Build.NamedBuildTarget.iOS, UnityEditor.Build.NamedBuildTarget.tvOS, UnityEditor.Build.NamedBuildTarget.Standalone })
                PlayerSettings.SetApplicationIdentifier(nb, BundleId);
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0") PlayerSettings.bundleVersion = "0.1";
            Debug.Log("[Schofield] Player settings configured.");
        }

        static bool OnTarget(BuildTargetGroup group, BuildTarget target, string what)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target) return true;
            Debug.LogWarning($"[Schofield] Switching the editor to {target} (this takes a while). Run \"{what}\" again when it finishes.");
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            return false;
        }

        static void Apple(BuildTarget target, BuildTargetGroup group, string path, string what)
        {
            ConfigurePlayer();
            if (!BuildPipeline.IsBuildTargetSupported(group, target)) { Debug.LogError($"[Schofield] {what}: install the {target} module in Unity Hub first."); return; }
            if (!OnTarget(group, target, what)) return;
            if (!File.Exists(Path.Combine(ProjectRoot, SceneBuilder.ScenePath))) SceneBuilder.Build();
            var id = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.SetApplicationIdentifier(id, BundleId);
            if (target != BuildTarget.StandaloneOSX) PlayerSettings.SetScriptingBackend(id, ScriptingImplementation.IL2CPP);
            if (target == BuildTarget.tvOS) { PlayerSettings.tvOS.requireExtendedGameController = true; PlayerSettings.tvOS.targetOSVersionString = "17.0"; }   // played with a controller
            if (target == BuildTarget.iOS) PlayerSettings.iOS.targetOSVersionString = "16.0";
            AppleRelease.Prepare(target);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath }, locationPathName = path, target = target, targetGroup = group, options = BuildOptions.None,
            });
            var s = report.summary;
            if (s.result == BuildResult.Succeeded) Debug.Log($"[Schofield] {what}: {path} ({s.totalTime.TotalSeconds:0}s)");
            else Debug.LogError($"[Schofield] {what} {s.result}: {s.totalErrors} errors. See the console.");
        }

        [MenuItem("Schofield/Apple/Build Mac App", priority = 40)]
        public static void BuildMac() => Apple(BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone, Path.Combine(BuildsDir, "macOS", "Schofield.app"), "Mac app");

        [MenuItem("Schofield/Apple/Build Xcode Project for Apple TV", priority = 41)]
        public static void BuildTV() => Apple(BuildTarget.tvOS, BuildTargetGroup.tvOS, Path.Combine(BuildsDir, "tvOS"), "Apple TV Xcode project");

        [MenuItem("Schofield/Apple/Build Xcode Project for iPhone and iPad", priority = 42)]
        public static void BuildIOS() => Apple(BuildTarget.iOS, BuildTargetGroup.iOS, Path.Combine(BuildsDir, "iOS"), "iPhone/iPad Xcode project");
    }
}
