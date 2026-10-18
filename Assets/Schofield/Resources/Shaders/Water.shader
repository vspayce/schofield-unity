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

            half4 River(V i, Light sun, float3 viewDir)
            {
                float t = _Time.y;
                float2 flow = normalize(i.uv2 + 1e-4);
                float2 p = i.positionWS.xz;
                float rap = i.uv1.y;
                float speed = 1.2 + rap * 3.0;
                // two layers of chop sliding downstream at different rates
                float2 pa = p / 9 - flow * t * speed / 9, pb = p / 4.3 + float2(0.37, 0.71) - flow * t * speed * 1.4 / 4.3;
                const float e = 0.035;
                float h0 = N(pa) + 0.5 * N(pb);
                float hx = N(pa + float2(e, 0)) + 0.5 * N(pb + float2(e, 0));
                float hz = N(pa + float2(0, e)) + 0.5 * N(pb + float2(0, e));
                float k = 0.9 + rap * 1.6;
                float3 n = normalize(float3((h0 - hx) * k * 9, 1, (h0 - hz) * k * 9));
                float fres = 0.04 + 0.96 * pow(1 - saturate(dot(n, viewDir)), 5);
                float depth = i.uv1.x;
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, saturate(depth / 3.2));
                body *= 0.55 + 0.45 * saturate(dot(float3(0, 1, 0), sun.direction)) * sun.color * sun.shadowAttenuation;
                float3 r = reflect(-viewDir, n);
                half3 col = lerp(body, Sky(r), fres * 0.85);
                col += sun.color * pow(saturate(dot(r, sun.direction)), _SpecPow) * 3 * sun.shadowAttenuation;
                // foam: rapids in patches, the banks, and the shallows
                float fn = N(p / 6 - flow * t * speed / 6) * 0.6 + N(p / 2.1 - flow * t * speed / 2.1) * 0.4;
                float foam = smoothstep(0.62 - rap * 0.45, 0.8 - rap * 0.3, fn) * saturate(rap * 1.4 + 0.1);
                foam = max(foam, smoothstep(0.72, 0.95, i.uv0.y) * smoothstep(0.45, 0.7, fn));
                foam = max(foam, (1 - smoothstep(0.2, 0.9, depth)) * smoothstep(0.5, 0.7, fn));
                col = lerp(col, _FoamCol.rgb * (0.7 + 0.3 * sun.color), saturate(foam));
                float a = (0.82 + 0.18 * fres) * (1 - smoothstep(0.86, 1.0, i.uv0.y));
                return half4(col, saturate(max(a, foam) * _Opacity));
            }

            half4 Falls(V i, Light sun, float3 viewDir)
            {
                float t = _Time.y;
                float lat = i.uv0.x, fallen = i.uv0.y, phi = i.uv1.x, edge = i.uv1.y;
                float ph = i.uv2.x, w = i.uv2.y;
                // long threads of water: noise stretched hard down the sheet, racing down
                float speed = 9 + phi * 6;
                float s1 = N(float2(lat * 0.22 + ph, (fallen - t * speed) * 0.018));
                float s2 = N(float2(lat * 0.61 - ph * 3, (fallen - t * speed * 1.3) * 0.035 + 0.5));
                float s3 = N(float2(lat * 1.7 + ph, (fallen - t * speed * 1.1) * 0.09));
                float streak = s1 * 0.55 + s2 * 0.35 + s3 * 0.2;
                // the sheet breaks up as it falls
                float body = smoothstep(0.25 + phi * 0.25, 0.6 + phi * 0.15, streak);
                float a = lerp(0.9, 0.45, phi) * body + 0.15;
                a *= 1 - smoothstep(0.78, 1.0, edge);
                a *= w * _Opacity;
                float3 n = normalize(i.normalWS);
                half3 lit = _FoamCol.rgb * (0.55 + 0.6 * saturate(abs(dot(n, sun.direction))) * sun.color) + SampleSH(n) * 0.25;
                half3 col = lerp(lit * 0.82, lit * 1.08, s3);
                col = lerp(col, _Shallow.rgb * 0.7, (1 - body) * 0.35);
                return half4(col, saturate(a));
            }

            half4 Pool(V i, Light sun)
            {
                float t = _Time.y;
                float r01 = i.uv0.x, ang = i.uv0.y, rm = i.uv1.x;
                float f = N(float2(ang * 6, rm * 0.2 - t * 0.9)) * 0.6 + N(float2(ang * 13 + 0.3, rm * 0.5 - t * 1.6)) * 0.4;
                float foam = smoothstep(0.3, 0.75, f) * (1 - r01 * r01);
                half3 col = _FoamCol.rgb * (0.65 + 0.4 * sun.color);
                return half4(col, saturate(foam * 0.95 * _Opacity));
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
