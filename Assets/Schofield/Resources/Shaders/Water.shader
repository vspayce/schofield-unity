// Thunder Gorge water, one shader for three surfaces (_Kind): 0 the river ribbon, 1 the falls curtain,
// 2 the churning pool at its foot. Procedural chop and streaks from the cloud noise texture, the sky
// colours for reflections, the sun for glints, foam where the water runs fast. After world/water.js.
Shader "Schofield/Water"
{
    Properties
    {
        _Noise ("Noise", 2D) = "gray" {}
        _Kind ("Kind", Float) = 0
        _Shallow ("Shallow", Color) = (0.53,0.66,0.58,1)
        _Deep ("Deep", Color) = (0.04,0.16,0.18,1)
        _FoamCol ("Foam", Color) = (0.93,0.96,0.96,1)
        _Zenith ("Zenith", Color) = (0.24,0.42,0.61,1)
        _Horizon ("Horizon", Color) = (0.79,0.84,0.86,1)
        _SpecPow ("Spec power", Float) = 300
        _Opacity ("Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial)
                float4 _Noise_ST; float _Kind, _SpecPow, _Opacity;
                half4 _Shallow, _Deep, _FoamCol, _Zenith, _Horizon;
            CBUFFER_END

            // river: uv0 = (s, |edge|), uv1 = (depth over bed, rapids), uv2 = flow direction (x, z)
            // falls: uv0 = (lateral m, fallen m), uv1 = (phi, |edge|), uv2 = (shell phase, shell weight)
            // pool:  uv0 = (r01, angle01), uv1 = (radius m, 0)
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct V
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                float2 uv0 : TEXCOORD2; float2 uv1 : TEXCOORD3; float2 uv2 : TEXCOORD4; half fog : TEXCOORD5;
            };

            V vert(A v)
            {
                V o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv0 = v.uv0; o.uv1 = v.uv1; o.uv2 = v.uv2;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float N(float2 p) { return SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p).r; }

            half3 Sky(float3 r)
            {
                return lerp(_Horizon.rgb, _Zenith.rgb, saturate(r.y * 1.6));
            }

            // a sharpened noise "line" pattern: thin bright threads where the noise crosses mid-grey
            float Threads(float2 p) { float n = N(p); return 1 - saturate(abs(n - 0.5) * 7); }

            half3 Ambient(float3 n) { return max(SampleSH(n), half3(0.05, 0.06, 0.07)); }

            float shadowLerp(Light sun) { return sun.shadowAttenuation; }

            half4 River(V i, Light sun, float3 viewDir)
            {
                float t = _Time.y;
                float2 flow = normalize(i.uv2 + 1e-4);
                float2 perp = float2(-flow.y, flow.x);
                float2 p = i.positionWS.xz;
                float along = dot(p, flow), across = dot(p, perp);
                float rap = i.uv1.y;
                float speed = 1.6 + rap * 3.5;
                float sl = shadowLerp(sun);
                // chop: soft, stretched down the current, two layers at different rates
                float2 pa = float2(across / 7, (along - t * speed) / 10), pb = float2(across / 3.1 + 0.37, (along - t * speed * 1.35) / 4.5);
                const float e = 0.03;
                float h0 = N(pa) + 0.5 * N(pb);
                float hx = N(pa + float2(e, 0)) + 0.5 * N(pb + float2(e, 0));
                float hz = N(pa + float2(0, e)) + 0.5 * N(pb + float2(0, e));
                float k = 0.45 + rap * 1.1;
                // fine chop aliases into stripes far off: let it calm down with distance
                float dcam = distance(GetCameraPositionWS(), i.positionWS);
                k *= lerp(1, 0.2, saturate(dcam / 140));
                float2 g = (h0 - float2(hx, hz)) * k * 9;
                float3 n = normalize(float3(g.x * perp.x + g.y * flow.x, 1, g.x * perp.y + g.y * flow.y));
                float fres = 0.03 + 0.97 * pow(1 - saturate(dot(n, viewDir)), 5);
                float depth = i.uv1.x;
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, saturate(depth / 2.6));
                body *= Ambient(float3(0, 1, 0)) * 0.9 + sun.color * saturate(sun.direction.y) * 0.35 * sl;
                float3 r = reflect(-viewDir, n);
                half3 col = lerp(body, Sky(r), fres * 0.7);
                col += sun.color * pow(saturate(dot(r, sun.direction)), _SpecPow) * 2.5 * sl;
                // white water: long streaks riding the current, gathering where it runs fast and along the banks
                // stretched noise (not iso-lines) so the foam comes in torn, uneven streaks
                float st1 = N(float2(across / 1.7, (along - t * speed * 1.2) / 16));
                float st2 = N(float2(across / 0.8 + 3.1, (along - t * speed * 1.5) / 6.5));
                float streak = st1 * 0.65 + st2 * 0.35;
                float patch = N(float2(across / 14, (along - t * speed * 0.6) / 30));
                float foam = smoothstep(0.66 - rap * 0.3, 0.86 - rap * 0.25, streak * 0.6 + patch * 0.4 + rap * 0.18);
                foam *= lerp(1, 0.6, saturate(dcam / 200));
                foam = max(foam, smoothstep(0.8, 0.98, i.uv0.y) * smoothstep(0.4, 0.7, streak));
                foam = max(foam, (1 - smoothstep(0.15, 0.7, depth)) * smoothstep(0.5, 0.75, streak) * 0.6);
                foam = saturate(foam * (0.75 + rap * 0.5));
                half3 white = _FoamCol.rgb * (Ambient(float3(0, 1, 0)) * 0.8 + sun.color * saturate(sun.direction.y) * 0.55 * sl);
                col = lerp(col, white, foam);
                float a = (0.86 + 0.14 * fres) * (1 - smoothstep(0.86, 1.0, i.uv0.y));
                return half4(col, saturate(max(a, foam) * _Opacity));
            }

            // The curtain. Water leaves the lip as a smooth, glassy tongue (dark, reflecting the sky, threaded with
            // highlights), accelerates under gravity — the texture scrolls in fall-time, so the threads stretch out
            // as it speeds up — and tears into aerated white water that breaks into falling clumps near the foot.
            half4 Falls(V i, Light sun, float3 viewDir)
            {
                float t = _Time.y;
                float lat = i.uv0.x, fallen = i.uv0.y, phi = i.uv1.x, edge = i.uv1.y;
                float ph = i.uv2.x, w = i.uv2.y;
                float sl = shadowLerp(sun);
                // fall-time since the lip (s): sqrt(2h/g); upstream of the lip it's slow surface flow
                float tf = fallen > 0 ? sqrt(2 * fallen / 9.8) : fallen * 0.25;
                float v = tf - t * 0.55;
                float th1 = Threads(float2(lat * 0.35 + ph, v * 1.6));
                float th2 = Threads(float2(lat * 0.9 - ph * 3, v * 2.6 + 0.5));
                float th3 = N(float2(lat * 2.2 + ph, v * 4.5));
                float clumps = N(float2(lat * 0.45 + ph * 2, v * 1.2 + 0.3));
                // the flow isn't even across the lip: heavier tongues and thinner gaps between them
                float lobe = N(float2(lat * 0.09 + ph * 0.5, 0.37)) * 0.7 + N(float2(lat * 0.23 - ph, 0.81)) * 0.3;
                // aeration: none at the lip, mostly white by a third of the way down
                float aer = saturate(smoothstep(0.0, 0.32, phi) * 0.85 + th2 * 0.25 * phi + clumps * 0.25 * phi);
                float3 n = normalize(i.normalWS);
                float3 nf = dot(n, viewDir) < 0 ? -n : n;
                float ndl = saturate(dot(nf, sun.direction) * 0.5 + 0.5);            // wrapped: thick water scatters light
                half3 amb = Ambient(nf);
                // glassy tongue
                float fres = 0.08 + 0.92 * pow(1 - saturate(dot(nf, viewDir)), 4);
                half3 glass = lerp(_Deep.rgb * 1.6 + _Shallow.rgb * 0.25, Sky(reflect(-viewDir, nf)), fres * 0.8);
                glass += sun.color * (th1 * 0.35 + th3 * 0.1) * (0.4 + 0.6 * sl);
                // aerated white, self-shadowed in the folds
                half3 white = _FoamCol.rgb * (amb * 1.05 + sun.color * ndl * 0.75 * (0.35 + 0.65 * sl));
                white *= 0.78 + 0.3 * th3 + 0.12 * th1;
                half3 col = lerp(glass, white, aer);
                // coverage: solid near the top, tearing into clumps and threads lower down
                float solid = 1 - smoothstep(0.35, 1.0, phi) * (1 - smoothstep(0.35, 0.75, clumps * 0.7 + th3 * 0.4));
                float a = lerp(0.97, 0.9, aer) * saturate(solid + th1 * 0.3);
                a *= saturate(0.55 + lobe * 0.9 - phi * (1 - lobe) * 0.5);
                aer = saturate(aer + (lobe - 0.5) * 0.3);
                col = lerp(glass, white, aer) * (0.88 + lobe * 0.22);
                a *= 1 - smoothstep(0.72, 1.0, edge + (th3 - 0.5) * 0.25);
                a *= w * _Opacity;
                // fade into the pool and where it meets rock
                return half4(col, saturate(a));
            }

            // the plunge pool: a boil of white at the centre, rings of foam spreading out and thinning
            half4 Pool(V i, Light sun)
            {
                float t = _Time.y;
                float r01 = i.uv0.x, ang = i.uv0.y, rm = i.uv1.x;
                float sl = shadowLerp(sun);
                float boil = N(float2(ang * 5 + t * 0.05, rm * 0.25 - t * 1.4)) * 0.55 + N(float2(ang * 11 + 0.3, rm * 0.6 - t * 2.1)) * 0.45;
                float rings = smoothstep(0.55, 0.8, N(float2(ang * 4 + rm * 0.05, rm * 0.12 - t * 0.35)));
                float foam = saturate(smoothstep(0.3, 0.7, boil) * (1 - r01 * 0.8) + rings * (1 - r01) * 0.6);
                foam *= 1 - smoothstep(0.6, 1.0, r01);
                half3 col = _FoamCol.rgb * (Ambient(float3(0, 1, 0)) * 0.95 + sun.color * saturate(sun.direction.y) * 0.6 * sl);
                return half4(col, saturate(foam * 0.97 * _Opacity));
            }

            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(sc);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half4 c = _Kind < 0.5 ? River(i, sun, viewDir) : _Kind < 1.5 ? Falls(i, sun, viewDir) : Pool(i, sun);
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
