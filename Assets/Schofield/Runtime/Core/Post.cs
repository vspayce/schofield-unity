// Post-processing: the route's colour grade (ACES, exposure, contrast, saturation, warm gain, cool
// shadows, vignette, grain, bloom), the Dead Eye look (gold tintype sepia, burnt edges, lens split)
// and the red edges when you're hit. Replaces the web game's final grade pass.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Schofield
{
    public class Post
    {
        readonly Volume grade, deadeye, hit;
        readonly GameObject root;
        public float deadeyeTarget, hitWeight;

        public Post(Transform parent)
        {
            root = new GameObject("Post");
            root.transform.SetParent(parent, false);
            grade = Make("Grade", 0, p =>
            {
                p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                p.Add<ColorAdjustments>(true);
                p.Add<ShadowsMidtonesHighlights>(true);
                var v = p.Add<Vignette>(true); v.intensity.Override(0.33f); v.smoothness.Override(0.42f);
                var g = p.Add<FilmGrain>(true); g.type.Override(FilmGrainLookup.Thin1); g.intensity.Override(0.28f); g.response.Override(0.8f);
                var b = p.Add<Bloom>(true); b.threshold.Override(0.95f); b.intensity.Override(0.4f); b.scatter.Override(0.6f); b.highQualityFiltering.Override(false);
            });
            deadeye = Make("DeadEye", 10, p =>
            {
                var ca = p.Add<ColorAdjustments>(true); ca.saturation.Override(-78f); ca.contrast.Override(12f); ca.colorFilter.Override(new Color(1.12f, 1.0f, 0.68f));
                var v = p.Add<Vignette>(true); v.intensity.Override(0.52f); v.smoothness.Override(0.5f); v.color.Override(new Color(0.22f, 0.15f, 0.06f));
                p.Add<ChromaticAberration>(true).intensity.Override(0.35f);
                p.Add<LensDistortion>(true).intensity.Override(-0.14f);
            });
            deadeye.weight = 0;
            hit = Make("Hit", 20, p =>
            {
                var v = p.Add<Vignette>(true); v.intensity.Override(0.55f); v.smoothness.Override(0.65f); v.color.Override(new Color(0.55f, 0.02f, 0.02f));
            });
            hit.weight = 0;
        }

        Volume Make(string name, int priority, System.Action<VolumeProfile> fill)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = priority;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            fill(p);
            vol.sharedProfile = p;
            return vol;
        }

        public void SetRoute(LightDef L)
        {
            var p = grade.sharedProfile;
            if (p.TryGet(out ColorAdjustments ca))
            {
                // the web game's Narkowicz ACES had no 0.6 pre-scale, so it sat ~2/3 stop brighter than URP's
                ca.postExposure.Override(Mathf.Log(L.exposure, 2) + 0.6f);
                ca.contrast.Override((L.gradeContrast - 1) * 70f);
                ca.saturation.Override((L.gradeSat - 1) * 100f);
                ca.colorFilter.Override(new Color(L.gradeGain.x + L.gradeLift.x * 2, L.gradeGain.y + L.gradeLift.y * 2, L.gradeGain.z + L.gradeLift.z * 2));
            }
            if (p.TryGet(out ShadowsMidtonesHighlights smh))
                smh.shadows.Override(new Vector4(L.tintShadow.x, L.tintShadow.y, L.tintShadow.z, 0));
        }

        public void Update(float rdt)
        {
            deadeye.weight = MathX.Damp(deadeye.weight, deadeyeTarget, 8, rdt);
            hit.weight = Mathf.Clamp01(hitWeight);
        }

        public void Dispose() { if (root) Object.Destroy(root); }
    }
}
