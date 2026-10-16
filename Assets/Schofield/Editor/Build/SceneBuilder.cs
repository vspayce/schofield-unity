// Makes the URP pipeline asset and the one game scene (a camera and the Game component: everything
// else is built at runtime from the route), and sets the player up for phones, Apple TV and the Mac.
// Runs by itself the first time the project compiles with its packages in place.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Schofield.EditorTools
{
    [InitializeOnLoad]
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Schofield/Scenes/Main.unity";
        const string Gen = "Assets/Schofield/Generated";

        static SceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ScenePath))) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Debug.Log("[Schofield] First open: building the game scene.");
                Build();
            };
        }

        [MenuItem("Schofield/Build Game Scene", priority = 1)]
        public static void Build()
        {
            Directory.CreateDirectory(Gen); Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EnsurePipeline();
            BuildTools.ConfigurePlayer();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 5000; cam.fieldOfView = 60;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            new GameObject("Game").AddComponent<Game>();
            RenderSettings.skybox = null;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Schofield] Scene ready: " + ScenePath + ". Press Play.");
        }

        public static UniversalRenderPipelineAsset EnsurePipeline()
        {
            string path = $"{Gen}/Schofield_URP.asset", rpath = $"{Gen}/Schofield_Renderer.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!asset)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rpath);
                var ppd = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                if (ppd) rd.postProcessData = ppd;
                EditorUtility.SetDirty(rd);
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.supportsHDR = true;
            asset.msaaSampleCount = 1;
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = false;
            asset.shadowDistance = 140f;
            asset.shadowCascadeCount = 2;
            var so = new SerializedObject(asset);
            void Set(string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; }
            Set("m_MainLightShadowmapResolution", 2048);
            Set("m_AdditionalLightsRenderingMode", 1);    // per pixel (the muzzle flash)
            Set("m_AdditionalLightsPerObjectLimit", 4);
            Set("m_SoftShadowsSupported", 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            // forward rendering: the hand-written shaders loop over additional lights the forward way
            var rdObj = so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
            if (rdObj)
            {
                var rso = new SerializedObject(rdObj);
                var rm = rso.FindProperty("m_RenderingMode"); if (rm != null) rm.intValue = 0;
                rso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rdObj);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            int level = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
            QualitySettings.SetQualityLevel(level, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
