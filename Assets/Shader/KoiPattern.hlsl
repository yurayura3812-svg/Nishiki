#ifndef KOI_PATTERN_INCLUDED
#define KOI_PATTERN_INCLUDED

float KoiHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float KoiNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(KoiHash(i + float3(0,0,0)), KoiHash(i + float3(1,0,0)), f.x),
                    lerp(KoiHash(i + float3(0,1,0)), KoiHash(i + float3(1,1,0)), f.x), f.y),
               lerp(lerp(KoiHash(i + float3(0,0,1)), KoiHash(i + float3(1,0,1)), f.x),
                    lerp(KoiHash(i + float3(0,1,1)), KoiHash(i + float3(1,1,1)), f.x), f.y), f.z);
}

float KoiNaturalNoise(float3 p, float largeScale, float detailAmount)
{
    float nLarge = KoiNoise(p * largeScale);
    float nSmall = KoiNoise(p * largeScale * 4.0);
    return saturate(nLarge - (nSmall * detailAmount * 0.5));
}

// テクスチャの鱗＋手続きの紅/墨。マス目の凹凸は作らない。
float3 EvaluateKoiAlbedo(float2 uv, float3 positionOS, out float3 pigmentTint)
{
    float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    float gray = dot(texColor.rgb, float3(0.2126, 0.7152, 0.0722));
    float detailMask = saturate(pow(abs(gray), 1.3));

    float bellyMask = saturate(smoothstep(_BellyLimit - 0.2, _BellyLimit + 0.2, positionOS.y));

    float n1 = KoiNaturalNoise((positionOS + _Seed.xyz) * _Sub1Scale, 1.0, _Sub1Detail);
    float m1 = smoothstep(1.0 - _Sub1Amount, (1.0 - _Sub1Amount) + _PatternSoftness, n1) * bellyMask;

    float n2 = KoiNaturalNoise((positionOS + _Seed.xyz + float3(10.0, 20.0, 30.0)) * _Sub2Scale, 1.0, _Sub2Detail);
    float m2 = smoothstep(1.0 - _Sub2Amount, (1.0 - _Sub2Amount) + _PatternSoftness, n2) * bellyMask;

    float3 albedo = _MainColor.rgb;
    albedo = lerp(albedo, _SubColor1.rgb, m1);
    albedo = lerp(albedo, _SubColor2.rgb, m2);
    albedo *= lerp(0.55, 1.0, detailMask);
    albedo *= texColor.rgb;

    pigmentTint = pow(max(albedo, 0.001), _TintPower);
    return albedo;
}

#endif
