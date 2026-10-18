// Billboard particles: a billowing puff (smoke, dust, mist, blood) or a bright additive flash/streak
// (muzzle flash, tracers, sparks, glints). Puffs break up with noise and fade softly where they meet
// the ground (soft particles from the depth texture); colour is per vertex.
Shader "Schofield/Particle"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst", Float) = 10
        _Additive ("Additive", Float) = 0
        _Noise ("Noise", 2D) = "gray" {}
        _Soft ("Soft distance", Float) = 1.2
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend [_Src] [_Dst]
        ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial) float _Src, _Dst, _Additive, _Soft; float4 _Noise_ST; CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; float4 screen : TEXCOORD2; float3 positionWS : TEXCOORD3; };
            V vert(A v)
            {
                V o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color; o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float2 c = i.uv * 2 - 1;
                float r = length(c);
                half a;
                if (_Additive > 0.5) a = saturate(1 - r) * saturate(1 - r) * 1.6;
                else
                {
                    // billows: two octaves of noise, offset per puff by where it is in the world
                    float2 o = frac(i.positionWS.xz * 0.013 + i.positionWS.y * 0.007);
                    half n = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.uv * 0.55 + o).r * 0.65 + SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.uv * 1.3 - o * 1.7).r * 0.35;
                    a = smoothstep(1.0, 0.25, r + (0.5 - n) * 0.55);
                    // soft where it meets the ground
                    float2 suv = i.screen.xy / i.screen.w;
                    float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float self = i.screen.w;
                    a *= saturate((scene - self) / _Soft);
                }
                half4 col = half4(i.color.rgb, i.color.a * a);
                if (_Additive > 0.5) { col.rgb *= col.a; return half4(col.rgb, 1); }
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
}
