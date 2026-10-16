// Shared bits for Schofield's hand-written URP shaders: simple lighting (sun with shadows + SH
// ambient + a few point lights for muzzle flashes) and the shadow-caster position.
#ifndef SCHOFIELD_COMMON_INCLUDED
#define SCHOFIELD_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// sun (shadowed) + sky ambient + additional point lights, for a rough dielectric
half3 SchofieldLight(float3 positionWS, half3 normalWS, half3 albedo, half sunVis, half wrap)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light sun = GetMainLight(shadowCoord);
    half ndl = saturate((dot(normalWS, sun.direction) + wrap) / (1 + wrap));
    half3 col = albedo * sun.color * ndl * sun.shadowAttenuation * sunVis;
    col += albedo * SampleSH(normalWS);
    #if defined(_ADDITIONAL_LIGHTS)
    uint count = GetAdditionalLightsCount();
    for (uint i = 0u; i < count; ++i)
    {
        Light l = GetAdditionalLight(i, positionWS);
        col += albedo * l.color * l.distanceAttenuation * saturate(dot(normalWS, l.direction) + 0.3);
    }
    #endif
    return col;
}

float3 _LightDirection;
float3 _LightPosition;

float4 SchofieldShadowPositionCS(float3 positionWS, float3 normalWS)
{
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDir = normalize(_LightPosition - positionWS);
    #else
    float3 lightDir = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
    #if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    return positionCS;
}

#endif
