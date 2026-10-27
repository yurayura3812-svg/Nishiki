#ifndef KOI_LIGHTING_INCLUDED
#define KOI_LIGHTING_INCLUDED

float KoiD_GGX(float NdotH, float roughness)
{
    float a = max(roughness * roughness, 1e-4);
    float a2 = a * a;
    float d = (NdotH * a2 - NdotH) * NdotH + 1.0;
    return a2 / max(PI * d * d, 1e-7);
}

float KoiV_SmithGGX(float NdotV, float NdotL, float roughness)
{
    float a = max(roughness * roughness, 1e-4);
    float gv = NdotL * (NdotV * (1.0 - a) + a);
    float gl = NdotV * (NdotL * (1.0 - a) + a);
    return 0.5 / max(gv + gl, 1e-5);
}

float3 KoiF_Schlick(float3 F0, float VdotH)
{
    float w = pow(saturate(1.0 - VdotH), 5.0);
    return F0 + (1.0 - F0) * w;
}

float3 KoiSpecularGGX(float3 N, float3 V, float3 L, float3 lightCol, float3 F0, float roughness)
{
    float3 H = normalize(V + L);
    float NdotH = saturate(dot(N, H));
    float NdotV = saturate(dot(N, V));
    float VdotH = saturate(dot(V, H));
    float D = KoiD_GGX(NdotH, roughness);
    float Vis = KoiV_SmithGGX(NdotV, saturate(dot(N, L)), roughness);
    float3 F = KoiF_Schlick(F0, VdotH);
    return D * Vis * F * saturate(dot(N, L)) * lightCol;
}

float KoiSampleCaustics(float2 worldUV, float t)
{
    int frameCount = max((int)_CausticsFrameCount, 2);
    float frameFloat = t * _CausticsFps;
    int frame0 = ((int)floor(frameFloat)) % frameCount;
    int frame1 = (frame0 + 1) % frameCount;
    float blend = frac(frameFloat);
    float c0 = SAMPLE_TEXTURE2D_ARRAY(_CausticsArray, sampler_CausticsArray, worldUV, frame0).r;
    float c1 = SAMPLE_TEXTURE2D_ARRAY(_CausticsArray, sampler_CausticsArray, worldUV, frame1).r;
    return lerp(c0, c1, blend);
}

void KoiAddDirectLight(
    Light light,
    float3 albedo,
    float3 pigmentTint,
    float3 scaleN,
    float3 guanineN,
    float3 V,
    float3 guanineF0,
    float ginrin,
    inout float3 diffuse,
    inout float3 guanine,
    inout float3 coat)
{
    float wrap = _DiffuseWrap;
    // セルフシャドウを真っ黒まで落とさない（真上視点で輪郭がアニメ線になるのを防ぐ）
    float shadow = lerp(0.55, 1.0, light.shadowAttenuation);
    float3 lc = light.color * (light.distanceAttenuation * shadow);
    float ndl = saturate((dot(scaleN, light.direction) + wrap) / (1.0 + wrap));
    diffuse += albedo * ndl * lc;
    guanine += KoiSpecularGGX(guanineN, V, light.direction, lc, guanineF0, _GuanineRoughness) * pigmentTint * ginrin;
    coat += KoiSpecularGGX(scaleN, V, light.direction, lc, _CoatF0.xxx, _CoatRoughness);
}

#endif
