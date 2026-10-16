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
    }
}
