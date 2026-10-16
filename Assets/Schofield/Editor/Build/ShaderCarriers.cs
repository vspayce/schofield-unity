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

        /// The animated GLBs come in with legacy clips, and the legacy Animation component samples them as garbage
        /// in player builds (the coach team and the riders flicker and fly apart). Import their clips for Mecanim;
        /// Anim plays them through a Playables mixer.
        public static void UseMecanim()
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Schofield/Resources/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb")) continue;
                var imp = AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                var so = new SerializedObject(imp);
                var m = so.FindProperty("importSettings.animationMethod");
                if (m == null) { Debug.Log($"[Mecanim] {path}: no animationMethod property"); continue; }
                Debug.Log($"[Mecanim] {path}: animationMethod was {m.intValue} ({(m.propertyType == SerializedPropertyType.Enum ? m.enumNames[m.enumValueIndex] : "int")})");
                if (m.propertyType == SerializedPropertyType.Enum)
                {
                    int idx = System.Array.IndexOf(m.enumNames, "Mecanim");
                    if (idx >= 0) m.enumValueIndex = idx;
                }
                else m.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                imp.SaveAndReimport();
            }
            ProbeModels();
        }

        /// glTFast brings these GLBs' clips in as legacy clips, and a legacy Animation component samples them
        /// as garbage in player builds (the team and the riders flicker and fly apart). Copy every clip into a
        /// Mecanim (non-legacy) clip under Resources/Anims/<model>/; Anim plays those through Playables.
        [MenuItem("Schofield/Convert Model Clips for Mecanim", priority = 23)]
        public static void ConvertClips()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Schofield/Resources/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".glb")) continue;
                var model = Path.GetFileNameWithoutExtension(path);
                var dir = $"Assets/Schofield/Resources/Anims/{model}";
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(o is AnimationClip c)) continue;
                    Directory.CreateDirectory(dir);
                    var copy = new AnimationClip { name = c.name, frameRate = c.frameRate, legacy = false, wrapMode = c.wrapMode };
                    foreach (var b in AnimationUtility.GetCurveBindings(c)) AnimationUtility.SetEditorCurve(copy, b, Linear(AnimationUtility.GetEditorCurve(c, b)));
                    foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(c)) AnimationUtility.SetObjectReferenceCurve(copy, b, AnimationUtility.GetObjectReferenceCurve(c, b));
                    var settings = AnimationUtility.GetAnimationClipSettings(copy); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(copy, settings);
                    var outPath = $"{dir}/{c.name}.anim";
                    AssetDatabase.DeleteAsset(outPath);
                    AssetDatabase.CreateAsset(copy, outPath);
                    n++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Clips] converted {n} clips for Mecanim");
        }

        /// The imported curves carry broken tangents (huge or infinite slopes on the root's position and some
        /// rotations), so between keys they evaluate to values like 1e11 and the skeleton flies apart.
        /// Rebuild each curve with straight-line tangents from its own keys (the glTF data is linear anyway).
        static AnimationCurve Linear(AnimationCurve cv)
        {
            // some keys hold garbage (the horses' root position has values like 3e12 between sane frames):
            // drop any key that is not a finite, plausible number
            var keys = System.Array.FindAll(cv.keys, k => !float.IsNaN(k.value) && !float.IsInfinity(k.value) && Mathf.Abs(k.value) < 100);
            if (keys.Length == 0) keys = new[] { new Keyframe(0, 0) };
            for (int i = 0; i < keys.Length; i++)
            {
                float slopeIn = i > 0 ? (keys[i].value - keys[i - 1].value) / Mathf.Max(1e-5f, keys[i].time - keys[i - 1].time) : 0;
                float slopeOut = i < keys.Length - 1 ? (keys[i + 1].value - keys[i].value) / Mathf.Max(1e-5f, keys[i + 1].time - keys[i].time) : 0;
                if (i == 0) slopeIn = slopeOut;
                if (i == keys.Length - 1) slopeOut = slopeIn;
                keys[i].inTangent = slopeIn; keys[i].outTangent = slopeOut;
                keys[i].weightedMode = WeightedMode.None; keys[i].inWeight = keys[i].outWeight = 1f / 3;
            }
            return new AnimationCurve(keys) { preWrapMode = cv.preWrapMode, postWrapMode = cv.postWrapMode };
        }

        public static void ProbeCurves()
        {
            var path = "Assets/Schofield/Resources/Models/horse_clevelandbay.glb";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is AnimationClip c) || c.name != "Gallop") continue;
                var conv = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Schofield/Resources/Anims/horse_clevelandbay/Gallop.anim");
                if (conv) foreach (var b in AnimationUtility.GetCurveBindings(conv)) { var cv = AnimationUtility.GetEditorCurve(conv, b); for (float t = 0; t < conv.length; t += 0.01f) { float v = cv.Evaluate(t); if (float.IsNaN(v) || Mathf.Abs(v) > 1e3) { Debug.Log($"[Curves] CONVERTED bad {b.path}.{b.propertyName}@{t:F2}={v}"); break; } } }
                int inf = 0, keys = 0, big = 0; string ex = "";
                foreach (var b in AnimationUtility.GetCurveBindings(c))
                {
                    var cv = AnimationUtility.GetEditorCurve(c, b);
                    foreach (var k in cv.keys)
                    {
                        keys++;
                        if (float.IsInfinity(k.inTangent) || float.IsInfinity(k.outTangent) || float.IsNaN(k.inTangent) || float.IsNaN(k.outTangent)) { inf++; if (ex.Length < 300) ex += $"{b.path}.{b.propertyName} t={k.time:F3} v={k.value:F3} in={k.inTangent} out={k.outTangent} w={k.weightedMode};"; }
                        if (Mathf.Abs(k.value) > 100 || Mathf.Abs(k.inTangent) > 1e4 || Mathf.Abs(k.outTangent) > 1e4) { big++; if (ex.Length < 900) ex += $" BIG {b.path}.{b.propertyName} t={k.time:F3} v={k.value} in={k.inTangent} out={k.outTangent};"; }
                    }
                    for (float t = 0; t < c.length; t += 0.01f) { float v = cv.Evaluate(t); if (float.IsNaN(v) || Mathf.Abs(v) > 1e3) { if (ex.Length < 600) ex += $" EVAL {b.path}.{b.propertyName}@{t:F2}={v};"; break; } }
                }
                Debug.Log($"[Curves] Gallop keys={keys} infTangents={inf} big={big} {ex}");
            }
        }

        public static void ProbeModels()
        {
            foreach (var n in new[] { "horse_clevelandbay", "horse", "rider" })
            {
                var path = $"Assets/Schofield/Resources/Models/{n}.glb";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var an = go ? go.GetComponentInChildren<Animation>(true) : null;
                var ar = go ? go.GetComponentInChildren<Animator>(true) : null;
                Debug.Log($"[Probe] {n} animation={(an ? an.name : "none")} animator={(ar ? ar.name + " ctrl=" + (ar.runtimeAnimatorController ? ar.runtimeAnimatorController.name : "none") : "none")}");
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip c) Debug.Log($"[Probe]   clip {c.name} legacy={c.legacy} len={c.length:F2} curves={AnimationUtility.GetCurveBindings(c).Length}");
                var imp = AssetImporter.GetAtPath(path);
                Debug.Log($"[Probe]   importer {imp?.GetType().Name}");
            }
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
