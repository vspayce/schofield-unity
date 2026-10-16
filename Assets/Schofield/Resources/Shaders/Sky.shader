// Sky dome as a skybox: gradient + sun glow in the haze + two planar cloud layers + sun disc.
// Port of the web game's world/sky.js shader.
Shader "Schofield/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.25,0.43,0.66,1)
        _Horizon ("Horizon", Color) = (0.95,0.76,0.55,1)
        _Ground ("Ground", Color) = (0.54,0.42,0.27,1)
        _SunGlow ("Sun glow", Color) = (1,0.7,0.42,1)
        _SunColor ("Sun colour", Color) = (1,0.79,0.54,1)
        _SunDir ("Sun direction", Vector) = (0,0.2,1,0)
        _Clouds ("Clouds", Float) = 0.45
        _Cloud ("Cloud noise", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Cloud); SAMPLER(sampler_Cloud);
            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _Ground, _SunGlow, _SunColor;
                float4 _SunDir;
                float _Clouds;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            V vert(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 sunDir = normalize(_SunDir.xyz);
                float sd = max(dot(d, sunDir), 0.0);
                float t = _Time.y;
                half3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(smoothstep(-0.02, 0.6, h), 0.5));
                col += _SunGlow.rgb * 0.15 * (1.0 - saturate(h));
                col = lerp(col, _Ground.rgb, smoothstep(0.0, -0.08, h));
                col += _SunGlow.rgb * (pow(sd, 6.0) * 0.55 + pow(sd, 40.0) * 0.6) * (1.0 - smoothstep(0.1, 0.8, h) * 0.5);
                if (h > 0.0)
                {
                    float2 cp = d.xz / (h + 0.12);
                    half c1 = SAMPLE_TEXTURE2D(_Cloud, sampler_Cloud, cp * 0.18 + float2(t * 0.0015, 0.0)).r;
                    half c2 = SAMPLE_TEXTURE2D(_Cloud, sampler_Cloud, cp * 0.45 + float2(t * 0.003, 0.2)).r;
                    half c = smoothstep(1.0 - _Clouds, 1.15 - _Clouds * 0.55, c1 * 0.7 + c2 * 0.35);
                    c *= smoothstep(0.0, 0.18, h);
                    half3 lit = lerp(_Horizon.rgb, _SunColor.rgb, 0.3) * lerp(0.75, 1.1, smoothstep(0.0, 0.35, h)) + _SunGlow.rgb * pow(sd, 4.0) * 0.9;
                    half3 shade = lerp(_Zenith.rgb, _Horizon.rgb, 0.45) * 0.6;
                    half3 cc = lerp(shade, lit, smoothstep(0.2, 1.0, c2 + sd * 0.4));
                    col = lerp(col, cc, c * 0.9);
                }
                col += _SunColor.rgb * smoothstep(0.9993, 0.9997, sd) * 8.0;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
