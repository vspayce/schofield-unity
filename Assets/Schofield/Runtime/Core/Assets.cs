// Loading the web game's art from Resources (GLBs imported by glTFast, JPG/PNG textures, MP3s), with
// graceful fallbacks so the game still runs if something is missing.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public static class Assets
    {
        static readonly Dictionary<string, Object> cache = new Dictionary<string, Object>();

        static T Load<T>(string path) where T : Object
        {
            if (cache.TryGetValue(path, out var o)) return o as T;
            var r = Resources.Load<T>(path);
            if (!r) Debug.LogWarning("[Schofield] missing resource " + path);
            cache[path] = r;
            return r;
        }

        public static Shader Shader(string name)
        {
            var s = Load<Shader>("Shaders/" + name);
            return s ? s : Urp(false);
        }

        /// URP's Lit or Unlit shader. Shader.Find only sees shaders the build already includes, so these come from two
        /// materials in Resources/Shaders (URPLit, URPUnlit) that carry them into every player build.
        public static UnityEngine.Shader Urp(bool lit)
        {
            var m = Load<Material>(lit ? "Shaders/URPLit" : "Shaders/URPUnlit");
            if (m && m.shader) return m.shader;
            return UnityEngine.Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
        }

        static Texture2D flatNormal;
        public static Texture2D Tex(string name, bool normal = false)
        {
            if (string.IsNullOrEmpty(name)) return normal ? FlatNormal() : Texture2D.whiteTexture;
            var t = Load<Texture2D>("Textures/" + name);
            if (t) return t;
            return normal ? FlatNormal() : Texture2D.whiteTexture;
        }
        static Texture2D FlatNormal()
        {
            if (flatNormal) return flatNormal;
            flatNormal = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var c = new Color(0.5f, 0.5f, 1f, 0.5f);
            flatNormal.SetPixels(new[] { c, c, c, c }); flatNormal.Apply();
            return flatNormal;
        }

        /// the imported GLB's prefab (glTFast's main asset), or null
        public static GameObject Model(string name) => Load<GameObject>("Models/" + name);
        public static AudioClip Clip(string name) => Load<AudioClip>("Audio/" + name);
        public static Sprite Sprite(string name) => Load<Sprite>("UI/" + name);
        public static Font Font(string name) => Load<Font>("Fonts/" + name);

        public static Transform Find(Transform root, string name)
        {
            if (!root) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = Find(root.GetChild(i), name);
                if (r) return r;
            }
            return null;
        }

        /// gltfpack moves meshes onto unnamed children of their named nodes; glTFast names those
        /// itself, so a renderer's "real" name is its own or its parent's
        public static string MeshName(Renderer r)
        {
            var n = r.gameObject.name;
            if (!string.IsNullOrEmpty(n) && !n.StartsWith("Node") && !n.StartsWith("mesh_") && !n.StartsWith("Mesh") && !n.StartsWith("Primitive")) return n;
            return r.transform.parent ? r.transform.parent.name : n;
        }

        // ---- material colour (glTFast's shader graph calls it baseColorFactor) ----
        public static readonly int BaseColorFactor = UnityEngine.Shader.PropertyToID("baseColorFactor");
        public static readonly int BaseColor = UnityEngine.Shader.PropertyToID("_BaseColor");
        public static readonly int ColorId = UnityEngine.Shader.PropertyToID("_Color");

        public static Color GetColor(Material m)
        {
            if (m.HasProperty(BaseColorFactor)) return m.GetColor(BaseColorFactor);
            if (m.HasProperty(BaseColor)) return m.GetColor(BaseColor);
            if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
            return Color.white;
        }
        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorFactor)) m.SetColor(BaseColorFactor, c);
            if (m.HasProperty(BaseColor)) m.SetColor(BaseColor, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }
        /// multiply a renderer's materials by a tint (cloned, so the shared asset stays as it was)
        public static void Tint(Renderer r, Color mul)
        {
            var mats = r.materials;
            foreach (var m in mats) { var c = GetColor(m); SetColor(m, new Color(c.r * mul.r, c.g * mul.g, c.b * mul.b, c.a)); }
            r.materials = mats;
        }

        public static Material Lit(Color c, float smooth = 0.1f, float metal = 0)
        {
            var m = new Material(Urp(true));
            m.SetColor(BaseColor, c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            return m;
        }
    }
}
