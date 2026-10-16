// Billboard particles: a soft round puff (smoke, dust, blood) or a bright additive flash/streak
// (muzzle flash, tracers, sparks, glints). Shape is procedural from the UVs; colour is per vertex.
Shader "Schofield/Particle"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst", Float) = 10
        _Additive ("Additive", Float) = 0
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
            CBUFFER_START(UnityPerMaterial) float _Src, _Dst, _Additive; CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color; o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float2 c = i.uv * 2 - 1;
                float r = length(c);
                // a puff: soft falloff with a little lumpiness; a flash: hot core and glow
                half a = _Additive > 0.5 ? saturate(1 - r) * saturate(1 - r) * 1.6 : smoothstep(1.0, 0.15, r) * (0.85 + 0.15 * sin(c.x * 7 + c.y * 5));
                half4 col = half4(i.color.rgb, i.color.a * a);
                if (_Additive > 0.5) { col.rgb *= col.a; return half4(col.rgb, 1); }
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
}
