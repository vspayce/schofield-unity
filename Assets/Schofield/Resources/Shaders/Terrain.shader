// Terrain splat (port of the web game's terrain material): two ground layers mixed by macro noise,
// a dirt road along road-space UVs with ruts and a worn verge, tri-planar rock on steep slopes, snow
// above the snow line, detail normals, and a baked sun-visibility term for far ridges.
// uv0 = (|lateral| to the road, station along it), uv1.x = sun visibility.
Shader "Schofield/Terrain"
{
    Properties
    {
        _G1 ("Ground 1", 2D) = "white" {}
        _G2 ("Ground 2", 2D) = "white" {}
        _Road ("Road", 2D) = "white" {}
        _Rock ("Rock", 2D) = "white" {}
        _Snow ("Snow", 2D) = "white" {}
        _Macro ("Macro noise", 2D) = "gray" {}
        _G1N ("Ground 1 N", 2D) = "bump" {}
        _G2N ("Ground 2 N", 2D) = "bump" {}
        _RoadN ("Road N", 2D) = "bump" {}
        _RockN ("Rock N", 2D) = "bump" {}
        _Tint1 ("Tint 1", Color) = (1,1,1,1)
        _Tint2 ("Tint 2", Color) = (1,1,1,1)
        _RockTint ("Rock tint", Color) = (1,1,1,1)
        _RoadTint ("Road tint", Color) = (1,1,1,1)
        _SnowLine ("Snow line", Float) = 9999
        _RoadHalf ("Road half width", Float) = 4.2
        _RockSlope ("Rock slope", Float) = 0.3
        _GreenMix ("Green mix", Vector) = (0.42, 0.68, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "SchofieldCommon.hlsl"
        TEXTURE2D(_G1); SAMPLER(sampler_G1);
        TEXTURE2D(_G2);
        TEXTURE2D(_Road);
        TEXTURE2D(_Rock);
        TEXTURE2D(_Snow);
        TEXTURE2D(_Macro);
        TEXTURE2D(_G1N); TEXTURE2D(_G2N); TEXTURE2D(_RoadN); TEXTURE2D(_RockN);
        CBUFFER_START(UnityPerMaterial)
            half4 _Tint1, _Tint2, _RockTint, _RoadTint;
            float _SnowLine, _RoadHalf, _RockSlope;
            float4 _GreenMix;
        CBUFFER_END
        #define SMP sampler_G1

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 road : TEXCOORD0; float2 vis : TEXCOORD1; };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            struct V2F
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 road : TEXCOORD2;
                half sunVis : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            V2F vert(Attributes v)
            {
                V2F o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.road = v.road; o.sunVis = v.vis.x;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 SampleAT(TEXTURE2D_PARAM(t, s), float2 uv, half m)
            {
                // two scales, offset, blended by macro noise: kills visible tiling
                half3 a = SAMPLE_TEXTURE2D(t, s, uv).rgb;
                half3 b = SAMPLE_TEXTURE2D(t, s, uv * 0.37 + float2(0.31, 0.77)).rgb;
                return lerp(a, b, smoothstep(0.35, 0.65, m));
            }

            half2 NXY(TEXTURE2D_PARAM(t, s), float2 uv) { return UnpackNormal(SAMPLE_TEXTURE2D(t, s, uv)).xy; }

            half4 frag(V2F i) : SV_Target
            {
                float2 wxz = i.positionWS.xz;
                half m1 = SAMPLE_TEXTURE2D(_Macro, SMP, wxz / 180.0).r;
                half m2 = SAMPLE_TEXTURE2D(_Macro, SMP, wxz / 37.0 + 0.5).r;
                half3 g1 = SampleAT(TEXTURE2D_ARGS(_G1, SMP), wxz / 4.0, m2) * _Tint1.rgb;
                half3 g2 = SampleAT(TEXTURE2D_ARGS(_G2, SMP), wxz / 5.0, m2) * _Tint2.rgb;
                half gMix = smoothstep(_GreenMix.x, _GreenMix.y, m1 + (m2 - 0.5) * 0.3);
                half3 ground = lerp(g1, g2, gMix);
                ground *= 0.82 + 0.36 * m2;

                // road: along-road UV so ruts follow the road
                float ad = abs(i.road.x);
                float2 ruv = float2(i.road.x / 4.0, i.road.y / 4.0);
                half3 road = SAMPLE_TEXTURE2D(_Road, SMP, ruv).rgb * _RoadTint.rgb;
                road *= 0.9 + 0.2 * m2;
                half rut = smoothstep(0.55, 0.0, abs(ad - 0.82)) * 0.22 + smoothstep(0.5, 0.0, abs(ad - 1.95)) * 0.1;
                road *= 1.0 - rut;
                half edgeN = (m2 - 0.5) * 1.6;
                half roadW = 1.0 - smoothstep(_RoadHalf - 0.9, _RoadHalf + 1.2, ad + edgeN);
                ground = lerp(ground, lerp(ground, road, 0.55), (1.0 - smoothstep(_RoadHalf + 0.5, _RoadHalf + 3.5, ad + edgeN)) * 0.6);

                // tri-planar rock on steep slopes
                half3 n = normalize(i.normalWS);
                half slope = 1.0 - n.y;
                half3 bw = pow(abs(n), 4.0); bw /= (bw.x + bw.y + bw.z);
                float2 rwarp = (float2(m1, m2) - 0.5) * 5.0;
                float3 wp = i.positionWS;
                half3 rx = SAMPLE_TEXTURE2D(_Rock, SMP, (wp.zy + rwarp) / 11.0).rgb;
                half3 ry = SAMPLE_TEXTURE2D(_Rock, SMP, (wp.xz + rwarp) / 11.0).rgb;
                half3 rz = SAMPLE_TEXTURE2D(_Rock, SMP, (wp.xy + rwarp) / 11.0).rgb;
                half3 rock = (rx * bw.x + ry * bw.y + rz * bw.z) * _RockTint.rgb;
                half rockW = smoothstep(_RockSlope, _RockSlope + 0.14, slope + (m2 - 0.5) * 0.18 + (m1 - 0.5) * 0.25);

                half3 col = lerp(ground, rock, rockW);
                col = lerp(col, road, roadW * (1.0 - rockW * 0.8));

                half sn = smoothstep(_SnowLine - 6.0, _SnowLine + 8.0, wp.y + (m1 - 0.5) * 20.0) * smoothstep(0.55, 0.25, slope);
                sn *= 1.0 - roadW;
                half3 snow = SAMPLE_TEXTURE2D(_Snow, SMP, wxz / 6.0).rgb;
                col = lerp(col, snow, sn);

                // detail normals
                half2 ng = lerp(NXY(TEXTURE2D_ARGS(_G1N, SMP), wxz / 4.0), NXY(TEXTURE2D_ARGS(_G2N, SMP), wxz / 5.0), gMix);
                half2 nr = NXY(TEXTURE2D_ARGS(_RoadN, SMP), ruv);
                half2 nd = lerp(ng * 0.7, nr, roadW);
                half3 wn = normalize(n + half3(nd.x, 0.0, nd.y) * (1.0 - rockW) * (1.0 - sn * 0.6));
                half2 nrx = NXY(TEXTURE2D_ARGS(_RockN, SMP), (wp.zy + rwarp) / 11.0);
                half2 nrz = NXY(TEXTURE2D_ARGS(_RockN, SMP), (wp.xy + rwarp) / 11.0);
                half3 rockPert = half3(0.0, nrx.y, nrx.x) * bw.x + half3(nrz.x, nrz.y, 0.0) * bw.z;
                half3 N = normalize(wn + rockPert * rockW * 0.9);

                half3 lit = SchofieldLight(i.positionWS, N, col, i.sunVis, 0.0);
                lit = MixFog(lit, i.fog);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float4 vert(Attributes v) : SV_POSITION
            {
                return SchofieldShadowPositionCS(TransformObjectToWorld(v.positionOS.xyz), TransformObjectToWorldNormal(v.normalOS));
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
