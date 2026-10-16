// Instanced grass clumps (three crossed cards) that sway in the wind, fade out with distance and take
// the colour of the ground layer beneath. Port of the web game's grass material.
Shader "Schofield/Grass"
{
    Properties
    {
        _MainTex ("Blade", 2D) = "white" {}
        _Color ("Colour", Color) = (0.79,0.66,0.38,1)
        _Macro ("Macro noise", 2D) = "gray" {}
        _Tint1 ("Ground tint 1", Color) = (1,1,1,1)
        _Tint2 ("Ground tint 2", Color) = (1,1,1,1)
        _GreenMix ("Green mix", Vector) = (0.42,0.68,0,0)
        _Wind ("Wind", Float) = 1
        _Focus ("Focus", Vector) = (0,0,0,0)
        _Cutoff ("Cutoff", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        HLSLINCLUDE
        #include "SchofieldCommon.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_Macro); SAMPLER(sampler_Macro);
        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _Tint1, _Tint2; float4 _GreenMix, _Focus; float _Wind, _Cutoff;
        CBUFFER_END
        struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 gxz : TEXCOORD2; half gh : TEXCOORD3; half fog : TEXCOORD4; };

        float3 GrassWorld(A v, out float2 gxz, out half gh)
        {
            float3 ip = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
            float dist = length(ip.xz - _Focus.xz);
            float fade = 1.0 - smoothstep(30.0, 60.0, dist);
            float3 p = v.positionOS.xyz * fade;
            float t = _Time.y;
            float sway = sin(t * 1.7 + ip.x * 0.21 + ip.z * 0.17) * 0.5 + sin(t * 3.1 + ip.x * 0.7) * 0.2;
            p.x += sway * _Wind * 0.18 * v.positionOS.y;
            p.z += cos(t * 1.3 + ip.z * 0.2) * _Wind * 0.1 * v.positionOS.y;
            gxz = ip.xz; gh = v.positionOS.y;
            return TransformObjectToWorld(p);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            V vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o;
                o.positionWS = GrassWorld(v, o.gxz, o.gh);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(t.a - _Cutoff);
                half gm = smoothstep(_GreenMix.x, _GreenMix.y, SAMPLE_TEXTURE2D(_Macro, sampler_Macro, i.gxz / 180.0).r);
                half3 albedo = t.rgb * _Color.rgb * lerp(_Tint1.rgb, _Tint2.rgb, gm) * lerp(0.78, 1.12, saturate(i.gh));
                half3 c = SchofieldLight(i.positionWS, half3(0, 1, 0), albedo, 1, 0);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
