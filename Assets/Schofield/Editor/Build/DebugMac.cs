using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Schofield.EditorTools
{
    /// A Mac build with IL2CPP and a development player (Player.log, stack traces), to chase what the phone does.
    public static class DebugMac
    {
        public static void Build()
        {
            ShaderCarriers.Make();
            var nb = UnityEditor.Build.NamedBuildTarget.Standalone;
            var was = PlayerSettings.GetScriptingBackend(nb);
            PlayerSettings.SetScriptingBackend(nb, ScriptingImplementation.Mono2x);
            var scenes = new[] { "Assets/Schofield/Scenes/Main.unity" };
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, target = BuildTarget.StandaloneOSX,
                locationPathName = Path.GetFullPath("Builds/macDebug/Schofield.app"), options = BuildOptions.Development });
            PlayerSettings.SetScriptingBackend(nb, was);
            Debug.Log("[DebugMac] " + r.summary.result + " " + r.summary.totalErrors);
        }
    }
}
