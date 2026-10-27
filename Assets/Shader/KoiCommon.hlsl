#ifndef KOI_COMMON_INCLUDED
#define KOI_COMMON_INCLUDED

#ifndef LIGHT_LOOP_BEGIN
    #define LIGHT_LOOP_BEGIN(lightCount) for (uint lightIndex = 0u; lightIndex < lightCount; ++lightIndex) {
    #define LIGHT_LOOP_END }
#endif

TEXTURE2D(_BaseMap);             SAMPLER(sampler_BaseMap);
TEXTURE2D(_ScaleNormal);         SAMPLER(sampler_ScaleNormal);
TEXTURE2D(_ScaleID);             SAMPLER(sampler_ScaleID);
TEXTURE2D(_GinrinMask);          SAMPLER(sampler_GinrinMask);
TEXTURE2D(_ReflectLUT);          SAMPLER(sampler_ReflectLUT);
TEXTURE2D_ARRAY(_CausticsArray); SAMPLER(sampler_CausticsArray);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _MainColor; half4 _SubColor1; half4 _SubColor2;
    float _Sub1Scale; float _Sub1Detail; float _Sub1Amount;
    float _Sub2Scale; float _Sub2Detail; float _Sub2Amount;
    float _BellyLimit; float _PatternSoftness;
    float4 _Seed;

    float _ScaleTiling;
    float _ScaleBump;

    float _PlateTilt;
    float _GuanineF0;
    float _GuanineRoughness;
    float _GuanineStrength;
    float _GinrinStrength;
    float _LayerDisorder;
    float _TintPower;
    float _GlancingTint;

    float _CoatF0;
    float _CoatRoughness;
    float _DiffuseWrap;

    float _CausticsFrameCount;
    float _CausticsStrength;
    float _CausticsTiling;
    float _CausticsFps;
    half4 _CausticsTint;
    float _CausticsContrast;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS  : SV_POSITION;
    float2 uv          : TEXCOORD0;
    float3 positionWS  : TEXCOORD1;
    float3 normalWS    : TEXCOORD2;
    float3 tangentWS   : TEXCOORD3;
    float3 bitangentWS : TEXCOORD4;
    float3 positionOS  : TEXCOORD5;
    float  fogFactor   : TEXCOORD6;
};

float3 KoiRotateAroundAxis(float3 v, float3 axis, float angleRad)
{
    float s, c;
    sincos(angleRad, s, c);
    return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

#endif
