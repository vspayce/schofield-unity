// Sky (skybox material), far ridge silhouette rings, the sun with a shadow that follows the coach,
// sky ambient and fog. Ported from world/sky.js.
using UnityEngine;
using UnityEngine.Rendering;

namespace Schofield
{
    public class Sky
    {
        readonly Material skyMat;
        readonly GameObject rings;
        public readonly Light sun;
        readonly LightDef L;

        public static Vector3 SunDirection(LightDef L)
        {
            float phi = (90 - L.sunElev) * Mathf.Deg2Rad, theta = L.sunAzim * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Cos(theta)).normalized;
        }

        public Sky(Route route, Transform parent)
        {
            L = route.def.light;
            var sunDir = SunDirection(L);
            skyMat = new Material(Assets.Shader("Sky")) { name = "Sky" };
            skyMat.SetColor("_Zenith", L.zenith); skyMat.SetColor("_Horizon", L.horizon); skyMat.SetColor("_Ground", L.ground);
            skyMat.SetColor("_SunGlow", L.sunGlow); skyMat.SetColor("_SunColor", L.sunColor);
            skyMat.SetVector("_SunDir", sunDir); skyMat.SetFloat("_Clouds", L.clouds);
            skyMat.SetTexture("_Cloud", Assets.Tex("cloud_noise"));
            RenderSettings.skybox = skyMat;

            // three.js lights carry a 1/π in their Lambert term that URP doesn't
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = L.sunColor;
            sun.intensity = L.sunIntensity / Mathf.PI * 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.shadowBias = 0.4f; sun.shadowNormalBias = 0.6f;
            sun.transform.rotation = Quaternion.LookRotation(-sunDir);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // shade fill: the routes all run on low suns, so shadowed faces live on this — keep it generous enough
            // that a cliff in shadow still reads as rock, not a black hole (AO takes some back)
            float amb = Mathf.Max(0.9f, (L.hemiIntensity * 0.62f + L.env * 0.5f) * 1.3f);
            RenderSettings.ambientSkyColor = L.hemiSky * amb * 1.1f;
            RenderSettings.ambientEquatorColor = Color.Lerp(L.hemiSky, L.hemiGround, 0.5f) * amb;
            RenderSettings.ambientGroundColor = L.hemiGround * amb * 0.9f;
            RenderSettings.ambientIntensity = 1;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = L.env;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = L.fog;
            RenderSettings.fogDensity = L.fogDensity;
            DynamicGI.UpdateEnvironment();

            // far ridge rings, two layers of atmospheric perspective
            rings = new GameObject("Ridges");
            rings.transform.SetParent(parent, false);
            var sil = Assets.Tex("mountain_silhouette");
            (float r, float h, float off, float mix, int rep)[] layers = route.biome == "mountain"
                ? new[] { (2400f, 520f, 0f, 0.55f, 3), (1700f, 300f, 0.37f, 0.3f, 4) }
                : route.biome == "plains"
                    ? new[] { (2600f, 200f, 0f, 0.6f, 3), (1900f, 90f, 0.5f, 0.4f, 5) }
                    : new[] { (2500f, 260f, 0.2f, 0.55f, 3), (1800f, 140f, 0.7f, 0.35f, 4) };
            foreach (var ly in layers)
            {
                var col = Color.Lerp(L.fog, L.hemiGround * 0.8f, 1 - ly.mix);
                var m = new Material(Assets.Shader("Unlit"));
                m.SetTexture("_MainTex", sil); m.SetColor("_Color", col);
                m.SetTextureScale("_MainTex", new Vector2(ly.rep, 1)); m.SetTextureOffset("_MainTex", new Vector2(ly.off, 0));
                m.SetFloat("_Cutoff", 0.02f); m.SetFloat("_Cull", (float)CullMode.Off);
                var go = new GameObject("Ring");
                go.transform.SetParent(rings.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = RingMesh(ly.r, ly.h, 96);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            }
        }

        static Mesh RingMesh(float r, float h, int seg)
        {
            var v = new Vector3[(seg + 1) * 2]; var uv = new Vector2[v.Length]; var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                float x = Mathf.Sin(a) * r, z = Mathf.Cos(a) * r;
                v[i * 2] = new Vector3(x, -25, z); v[i * 2 + 1] = new Vector3(x, h - 25, z);
                uv[i * 2] = new Vector2(i / (float)seg, 0); uv[i * 2 + 1] = new Vector2(i / (float)seg, 1);
                if (i < seg)
                {
                    int k = i * 6, b = i * 2;
                    tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2; tri[k + 3] = b + 2; tri[k + 4] = b + 1; tri[k + 5] = b + 3;
                }
            }
            var m = new Mesh { vertices = v, uv = uv, triangles = tri };
            m.bounds = new Bounds(Vector3.zero, Vector3.one * r * 2.2f);
            return m;
        }

        public void Update(Camera cam)
        {
            var p = cam.transform.position;
            rings.transform.position = new Vector3(p.x, p.y - 30, p.z);
        }

        public void Dispose()
        {
            if (rings) Object.Destroy(rings);
            if (sun) Object.Destroy(sun.gameObject);
            Object.Destroy(skyMat);
        }
    }
}
