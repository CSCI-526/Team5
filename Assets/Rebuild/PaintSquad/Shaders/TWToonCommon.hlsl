#ifndef TW_TOON_COMMON_INCLUDED
#define TW_TOON_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    half4 _ShadeColor;
    half _ShadeThreshold;
    half _ShadeSoftness;
    half4 _RimColor;
    half _RimStrength;
    half _RimPower;
    half4 _OutlineColor;
    half _OutlineWidth;
    half _Emission;
    half _ShadowStrength;
    half _Ambient;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    half   fog        : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ToonVert(Attributes i)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);
    o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
    o.normalWS = TransformObjectToWorldNormal(i.normalOS);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.fog = ComputeFogFactor(o.positionCS.z);
    return o;
}

half4 ToonFrag(Varyings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    Light light = GetMainLight(shadowCoord);
    float3 N = normalize(i.normalWS);
    float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

    // Soft two band (wrapped) lighting.
    half ndl = dot(N, light.direction);
    half band = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
    half shadow = lerp(1.0h, light.shadowAttenuation, _ShadowStrength);
    half lit = band * shadow;
    half3 diffuse = lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
    half3 ambient = SampleSH(N) * _Ambient;
    half3 col = _BaseColor.rgb * (diffuse * light.color + ambient);

    // Rim highlight for readability at tiny on screen sizes.
    half rim = pow(saturate(1.0h - dot(N, V)), _RimPower) * _RimStrength;
    col += _RimColor.rgb * rim;

    col = lerp(col, _BaseColor.rgb * (1.0h + _Emission), saturate(_Emission));
    col = MixFog(col, i.fog);
    return half4(col, 1);
}

// ---------- Shadow caster ----------
float3 _LightDirection;
float3 _LightPosition;

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;
};

ShadowVaryings ToonShadowVert(Attributes i)
{
    ShadowVaryings o;
    UNITY_SETUP_INSTANCE_ID(i);
    float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
    float3 nWS = TransformObjectToWorldNormal(i.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDir = normalize(_LightPosition - posWS);
#else
    float3 lightDir = _LightDirection;
#endif
    float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
#if UNITY_REVERSED_Z
    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
#else
    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
#endif
    o.positionCS = cs;
    return o;
}

half4 ToonShadowFrag(ShadowVaryings i) : SV_Target { return 0; }

// ---------- Depth only ----------
ShadowVaryings ToonDepthVert(Attributes i)
{
    ShadowVaryings o;
    UNITY_SETUP_INSTANCE_ID(i);
    o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
    return o;
}

half4 ToonDepthFrag(ShadowVaryings i) : SV_Target { return 0; }

// ---------- Outline (inverted hull, constant pixel width) ----------
ShadowVaryings OutlineVert(Attributes i)
{
    ShadowVaryings o;
    UNITY_SETUP_INSTANCE_ID(i);
    float4 cs = TransformObjectToHClip(i.positionOS.xyz);
    float3 nWS = TransformObjectToWorldNormal(i.normalOS);
    float2 nCS = mul((float3x3)GetWorldToHClipMatrix(), nWS).xy;
    float len = max(length(nCS), 1e-5);
    float2 offset = (nCS / len) * (_OutlineWidth * 2.0 / _ScreenParams.xy) * cs.w;
    cs.xy += offset;
    o.positionCS = cs;
    return o;
}

half4 OutlineFrag(ShadowVaryings i) : SV_Target { return half4(_OutlineColor.rgb, 1); }

#endif
