using System.IO;
using UnityEditor;
using UnityEngine;

namespace Schofield.EditorTools
{
    /// The two materials that carry URP's Lit and Unlit shaders into player builds (the code makes its materials
    /// at run time, and Shader.Find can't find a shader no asset in the build uses).
    public static class ShaderCarriers
    {
        [MenuItem("Schofield/Make Shader Carriers", priority = 21)]
        public static void Make()
        {
            const string dir = "Assets/Schofield/Resources/Shaders";
            Directory.CreateDirectory(dir);
            foreach (var (file, shader) in new[] { ("URPLit", "Universal Render Pipeline/Lit"), ("URPUnlit", "Universal Render Pipeline/Unlit") })
            {
                var path = $"{dir}/{file}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
                AssetDatabase.CreateAsset(new Material(Shader.Find(shader)), path);
            }
            AssetDatabase.SaveAssets();
        }

        /// glTFast picks its shaders when it imports a GLB: imported before URP was set up, the models got the
        /// built-in pipeline's glTF shaders, which URP draws as nothing (only their shadows). Import them again.
        [MenuItem("Schofield/Reimport Models for URP", priority = 22)]
        public static void ReimportModels()
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Schofield/Resources/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".glb") || path.EndsWith(".gltf")) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Schofield/Resources/Models" }))
                Debug.Log("[Reimport] " + AssetDatabase.GUIDToAssetPath(guid));
            var m = AssetDatabase.LoadAllAssetsAtPath("Assets/Schofield/Resources/Models/stagecoach.glb");
            foreach (var o in m) if (o is Material mat) Debug.Log("[Reimport] stagecoach material " + mat.name + " -> " + mat.shader.name);
        }
    }
}
