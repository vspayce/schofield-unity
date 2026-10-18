// Post-processing: the route's colour grade (ACES, exposure, contrast, saturation, warm gain, cool
// shadows, white balance), a filmic finish (bloom with lens dirt, sun flares, a touch of chromatic
// fringe, grain, vignette, camera motion blur when the coach is flying), a tighter vignette through
// the scope, the Dead Eye look (gold tintype sepia, burnt edges, lens split) and the red edges when you're
// hit. "High" graphics also turns on ambient occlusion and SMAA; "fast" keeps phones cool.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Schofield
{
    public class Post
    {
        readonly Volume grade, deadeye, hit, scope;
        readonly GameObject root;
        public float deadeyeTarget, hitWeight, scopeTarget;

        /// 1 = high (default on Apple TV, the Mac and phones with 6 GB+), 0 = fast
        public static int Quality
        {
            get => PlayerPrefs.GetInt("schofield.quality", !Application.isMobilePlatform || Application.platform == RuntimePlatform.tvOS || SystemInfo.systemMemorySize >= 5500 ? 1 : 0);
            set { PlayerPrefs.SetInt("schofield.quality", value); PlayerPrefs.Save(); }
        }

        public Post(Transform parent)
        {
            root = new GameObject("Post");
            root.transform.SetParent(parent, false);
            var dirt = Assets.Tex("lens_dirt");
            grade = Make("Grade", 0, p =>
            {
                p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                p.Add<ColorAdjustments>(true);
                p.Add<WhiteBalance>(true);
                p.Add<ShadowsMidtonesHighlights>(true);
                var v = p.Add<Vignette>(true); v.intensity.Override(0.3f); v.smoothness.Override(0.45f); v.rounded.Override(false);
                var g = p.Add<FilmGrain>(true); g.type.Override(FilmGrainLookup.Thin1); g.intensity.Override(0.22f); g.response.Override(0.8f);
                var b = p.Add<Bloom>(true); b.threshold.Override(0.9f); b.intensity.Override(0.55f); b.scatter.Override(0.68f); b.highQualityFiltering.Override(false);
                b.dirtTexture.Override(dirt); b.dirtIntensity.Override(1.6f); b.tint.Override(new Color(1, 0.94f, 0.86f));
                var ca = p.Add<ChromaticAberration>(true); ca.intensity.Override(0.06f);
                var lf = p.Add<ScreenSpaceLensFlare>(true); lf.intensity.Override(0.35f); lf.firstFlareIntensity.Override(0.6f); lf.secondaryFlareIntensity.Override(0.4f);
                lf.warpedFlareIntensity.Override(0.3f); lf.streaksIntensity.Override(0.25f); lf.streaksLength.Override(0.4f); lf.tintColor.Override(new Color(1, 0.85f, 0.65f));
                var mb = p.Add<MotionBlur>(true); mb.mode.Override(MotionBlurMode.CameraOnly); mb.quality.Override(MotionBlurQuality.Low); mb.intensity.Override(0); mb.clamp.Override(0.04f);
            });
            scope = Make("Scope", 5, p =>
            {
                var v = p.Add<Vignette>(true); v.intensity.Override(0.5f); v.smoothness.Override(0.3f);
            });
            scope.weight = 0;
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
            ApplyQuality();
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

        /// the graphics setting: AO, anti-aliasing, motion blur, flares, shadow reach
        public void ApplyQuality()
        {
            bool high = Quality == 1;
            var cam = Camera.main;
            if (cam)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.antialiasing = high ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
                data.antialiasingQuality = AntialiasingQuality.Medium;
            }
            var p = grade.sharedProfile;
            if (p.TryGet(out MotionBlur mb)) mb.active = high;
            if (p.TryGet(out ScreenSpaceLensFlare lf)) lf.active = high;
            if (p.TryGet(out Bloom b)) b.highQualityFiltering.Override(high && !Application.isMobilePlatform);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            {
                asset.shadowDistance = high ? 180 : 120;
                asset.shadowCascadeCount = high ? 4 : 2;
                var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f?.GetValue(asset) is ScriptableRendererData[] list)
                    foreach (var rd in list) if (rd) foreach (var feat in rd.rendererFeatures) if (feat is ScreenSpaceAmbientOcclusion) feat.SetActive(high);
            }
        }

        public void SetRoute(LightDef L)
        {
            var p = grade.sharedProfile;
            if (p.TryGet(out ColorAdjustments ca))
            {
                // the web game's Narkowicz ACES had no 0.6 pre-scale, so it sat ~2/3 stop brighter than URP's
                ca.postExposure.Override(Mathf.Log(L.exposure, 2) + 0.6f);
                ca.contrast.Override((L.gradeContrast - 1) * 70f + 4);
                ca.saturation.Override((L.gradeSat - 1) * 100f + 4);
                ca.colorFilter.Override(new Color(L.gradeGain.x + L.gradeLift.x * 2, L.gradeGain.y + L.gradeLift.y * 2, L.gradeGain.z + L.gradeLift.z * 2));
            }
            // warm low suns, cooler high ones
            if (p.TryGet(out WhiteBalance wb)) wb.temperature.Override(Mathf.Lerp(12, -2, Mathf.InverseLerp(4, 25, L.sunElev)));
            if (p.TryGet(out ShadowsMidtonesHighlights smh))
                smh.shadows.Override(new Vector4(L.tintShadow.x, L.tintShadow.y, L.tintShadow.z, 0));
        }

        /// speed: the coach's speed over cruise, 0..1 — blur only when it's really flying
        public void Update(float rdt, float speedK = 0)
        {
            deadeye.weight = MathX.Damp(deadeye.weight, deadeyeTarget, 8, rdt);
            hit.weight = Mathf.Clamp01(hitWeight);
            scope.weight = MathX.Damp(scope.weight, scopeTarget, 10, rdt);
            if (grade.sharedProfile.TryGet(out MotionBlur mb)) mb.intensity.Override(Mathf.Clamp01(speedK) * 0.35f);
        }

        public void Dispose() { if (root) Object.Destroy(root); }
    }
}
