// First-open setup. This assembly depends on nothing outside the editor, so it compiles in a bare
// project and installs the packages the game needs; the game's own assemblies only compile once
// those packages are present (see the defineConstraints in Schofield.asmdef).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Schofield.Setup
{
    [InitializeOnLoad]
    static class ProjectSetup
    {
        // Added by name, so Unity picks the version that matches this editor.
        static readonly string[] Needed =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.inputsystem",
            "com.unity.ugui",
            "com.unity.cloud.gltfast",          // imports the web game's GLBs as they are
            "com.unity.meshopt.decompress",     // ...which gltfpack compressed with EXT_meshopt_compression
        };
        const string McpPackage = "com.coplaydev.unity-mcp";
        const string McpSource = "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity";

        static ListRequest list;
        static AddAndRemoveRequest add;

        static ProjectSetup()
        {
            if (SessionState.GetBool("Schofield.SetupChecked", false)) return;
            SessionState.SetBool("Schofield.SetupChecked", true);
            EditorApplication.delayCall += Check;
        }

        [MenuItem("Schofield/Setup/Install Packages", priority = 100)]
        static void Check()
        {
            list = Client.List(true, false);
            EditorApplication.update += WaitList;
        }

        static void WaitList()
        {
            if (!list.IsCompleted) return;
            EditorApplication.update -= WaitList;
            if (list.Status != StatusCode.Success) { Debug.LogWarning("[Schofield] Could not list packages: " + list.Error?.message); return; }
            var have = new HashSet<string>(list.Result.Select(p => p.name));
            var missing = Needed.Where(n => !have.Contains(n)).ToList();
            if (!have.Contains(McpPackage)) missing.Add(McpSource);
            if (missing.Count == 0) { Debug.Log("[Schofield] Packages are in place."); return; }
            Debug.Log("[Schofield] Installing: " + string.Join(", ", missing));
            add = Client.AddAndRemove(missing.ToArray(), null);
            EditorApplication.update += WaitAdd;
        }

        static void WaitAdd()
        {
            if (!add.IsCompleted) return;
            EditorApplication.update -= WaitAdd;
            if (add.Status == StatusCode.Success)
            {
                Debug.Log("[Schofield] Packages installed. Reimporting the models with glTFast.");
                AssetDatabase.ImportAsset("Assets/Schofield/Resources/Models", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            }
            else Debug.LogError("[Schofield] Package install failed: " + add.Error?.message);
        }

        [MenuItem("Schofield/Setup/Reimport Models", priority = 101)]
        static void ReimportModels() =>
            AssetDatabase.ImportAsset("Assets/Schofield/Resources/Models", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
    }

    // How the web game's files come in.
    class ImportRules : AssetPostprocessor
    {
        static bool Ours(string p) => p.Replace('\\', '/').StartsWith("Assets/Schofield/");

        void OnPreprocessTexture()
        {
            if (!Ours(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            var p = assetPath.Replace('\\', '/');
            var n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (p.Contains("/Resources/UI/"))
            {
                // HUD art: sprites, no mips, keep it crisp
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            if (p.Contains("/Resources/Textures/"))
            {
                if (n.EndsWith("_n")) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
                if (n == "cloud_noise") ti.sRGBTexture = false;   // data, not colour
                ti.wrapMode = n == "mountain_silhouette" ? TextureWrapMode.Repeat : TextureWrapMode.Repeat;
                if (n == "mountain_silhouette") ti.wrapModeV = TextureWrapMode.Clamp;
                ti.alphaIsTransparency = n == "grass_blade" || n == "mountain_silhouette" || n == "lens_dirt";
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 1024;
            }
            if (p.Contains("/Icons/"))
            {
                ti.npotScale = TextureImporterNPOTScale.None; ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 8192;
            }
        }

        void OnPreprocessAudio()
        {
            if (!Ours(assetPath)) return;
            var ai = (AudioImporter)assetImporter;
            var n = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            var s = ai.defaultSampleSettings;
            bool music = n.StartsWith("music") || n.StartsWith("sting");
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            s.quality = 0.7f;
            s.preloadAudioData = true;
            ai.defaultSampleSettings = s;
            ai.forceToMono = false;
            ai.loadInBackground = music;
        }

        // glTFast's importer: legacy Animation clips, so the game can cross-fade and layer recoil
        // straight from code by clip name (as the web game's AnimationMixer did). Set by reflection
        // so this assembly compiles before the package is installed.
        void OnPreprocessAsset()
        {
            if (!Ours(assetPath) || !assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) return;
            var imp = assetImporter;
            if (imp == null || !imp.GetType().Name.Contains("Gltf")) return;
            try { SetEnumDeep(imp, "animationMethod", "Legacy", 0); }
            catch (Exception e) { Debug.LogWarning("[Schofield] Could not set glTF animation method: " + e.Message); }
        }

        static bool SetEnumDeep(object o, string member, string value, int depth)
        {
            if (o == null || depth > 2) return false;
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            bool done = false;
            foreach (var f in o.GetType().GetFields(F))
            {
                if (string.Equals(f.Name, member, StringComparison.OrdinalIgnoreCase) && f.FieldType.IsEnum)
                {
                    f.SetValue(o, Enum.Parse(f.FieldType, value)); done = true; continue;
                }
                if (f.FieldType.IsClass && f.FieldType != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)
                    && f.FieldType.Namespace != null && f.FieldType.Namespace.StartsWith("GLTFast"))
                {
                    var v = f.GetValue(o);
                    if (v != null && SetEnumDeep(v, member, value, depth + 1)) done = true;
                }
            }
            return done;
        }
    }
}
