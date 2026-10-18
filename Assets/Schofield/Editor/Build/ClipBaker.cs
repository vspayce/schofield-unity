// Rebuilds the characters' Mecanim clips (Resources/Anims/<model>/<clip>.anim) from Builds/anims.json,
// which Tools/anims.mjs writes by decoding the GLBs with three.js and its meshopt decoder — the same
// path the web game plays them through. glTFast mis-decodes these meshopt-compressed animation
// channels (root positions of 1e12, scrambled rotations), which is what sank the horses into the
// ground and made every rig flicker. Each clip is resampled at 30 fps, already in Unity's handedness,
// with quaternions kept in one hemisphere.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Schofield.EditorTools
{
    public static class ClipBaker
    {
        static readonly HashSet<string> Once = new HashSet<string> { "Fall", "Rear", "FallOff", "DieStanding", "Shoot", "SeatShoot", "ShootL" };

        [System.Serializable] class Track { public string path, prop; public float[] values; }
        [System.Serializable] class Clip { public float duration, fps; public int frames; public Track[] tracks; }

        [MenuItem("Schofield/Rebuild Character Clips (from Builds/anims.json)", priority = 24)]
        public static void Rebuild()
        {
            var file = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds/anims.json");
            if (!File.Exists(file)) { Debug.LogError("[Clips] run Tools/anims.mjs first: " + file); return; }
            // JsonUtility can't read nested dictionaries: walk the two outer levels by hand
            var json = File.ReadAllText(file);
            int n = 0;
            foreach (var (model, modelJson) in Objects(json))
            {
                // three.js sanitises node names (drops . [ ] : / and turns spaces into _): map each of its paths
                // back to the real one in the imported model
                var map = new Dictionary<string, string>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Schofield/Resources/Models/{model}.glb");
                if (prefab)
                    foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == prefab.transform) continue;
                        string real = AnimationUtility.CalculateTransformPath(t, prefab.transform);
                        var parts = real.Split('/');
                        for (int i = 0; i < parts.Length; i++) parts[i] = Sanitize(parts[i]);
                        map[string.Join("/", parts)] = real;
                    }
                int missing = 0;
                foreach (var (clipName, clipJson) in Objects(modelJson))
                {
                    var c = JsonUtility.FromJson<Clip>(clipJson);
                    if (c.tracks == null || c.tracks.Length == 0) continue;
                    var clip = new AnimationClip { name = clipName, frameRate = c.fps, legacy = false };
                    foreach (var t in c.tracks)
                    {
                        if (map.Count > 0) { if (map.TryGetValue(t.path, out var real)) t.path = real; else if (map.ContainsValue(t.path) == false) { missing++; continue; } }
                        int size = t.prop == "quaternion" ? 4 : 3;
                        string[] props = t.prop == "quaternion" ? new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" }
                            : t.prop == "position" ? new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" }
                            : new[] { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
                        int frames = t.values.Length / size;
                        for (int k = 0; k < size; k++)
                        {
                            var keys = new Keyframe[frames];
                            for (int i = 0; i < frames; i++) keys[i] = new Keyframe(Mathf.Min(c.duration, i / c.fps), t.values[i * size + k]);
                            for (int i = 0; i < frames; i++)
                            {
                                float sIn = i > 0 ? (keys[i].value - keys[i - 1].value) / Mathf.Max(1e-5f, keys[i].time - keys[i - 1].time) : 0;
                                float sOut = i < frames - 1 ? (keys[i + 1].value - keys[i].value) / Mathf.Max(1e-5f, keys[i + 1].time - keys[i].time) : sIn;
                                if (i == 0) sIn = sOut;
                                keys[i].inTangent = sIn; keys[i].outTangent = sOut;
                            }
                            clip.SetCurve(t.path, typeof(Transform), props[k], new AnimationCurve(keys));
                        }
                    }
                    clip.EnsureQuaternionContinuity();
                    var st = AnimationUtility.GetAnimationClipSettings(clip);
                    st.loopTime = !Once.Contains(clipName);
                    AnimationUtility.SetAnimationClipSettings(clip, st);
                    var dir = $"Assets/Schofield/Resources/Anims/{model}";
                    Directory.CreateDirectory(dir);
                    var outPath = $"{dir}/{clipName}.anim";
                    AssetDatabase.DeleteAsset(outPath);
                    AssetDatabase.CreateAsset(clip, outPath);
                    n++;
                }
                if (missing > 0) Debug.LogWarning($"[Clips] {model}: {missing} tracks had no matching node");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Clips] rebuilt {n} clips from three.js-decoded animation");
        }

        static string Sanitize(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in name) { if (char.IsWhiteSpace(ch)) sb.Append('_'); else if ("[].:/".IndexOf(ch) < 0) sb.Append(ch); }
            return sb.ToString();
        }

        /// the top-level members of a JSON object, as (key, raw value)
        static IEnumerable<(string, string)> Objects(string json)
        {
            int i = json.IndexOf('{') + 1;
            while (i < json.Length)
            {
                int k0 = json.IndexOf('"', i); if (k0 < 0) yield break;
                int k1 = json.IndexOf('"', k0 + 1);
                string key = json.Substring(k0 + 1, k1 - k0 - 1);
                int v0 = json.IndexOf('{', k1);
                int depth = 0, v1 = v0;
                for (; v1 < json.Length; v1++) { if (json[v1] == '{') depth++; else if (json[v1] == '}') { depth--; if (depth == 0) break; } }
                yield return (key, json.Substring(v0, v1 - v0 + 1));
                i = v1 + 1;
                while (i < json.Length && (json[i] == ',' || char.IsWhiteSpace(json[i]))) i++;
                if (i < json.Length && json[i] == '}') yield break;
            }
        }
    }
}
