Shader "Custom/URP_Koi_Pattern_Managed"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (Base Texture with Scales)", 2D) = "white" {}
        [MainColor] _BaseColor("Color Tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        
        [Header(Red Pattern)]
        _RedColor("Red Color", Color) = (0.8, 0.1, 0.1, 1)
        _RedScale("Red Scale", Float) = 1.5
        _RedDetail("Red Detail", Range(0, 1)) = 0.5
        _RedAmount("Red Amount", Range(0, 1)) = 0.5
        
        [Header(Black Pattern)]
        _BlackColor("Black Color", Color) = (0.1, 0.1, 0.1, 1)
        _BlackScale("Black Scale", Float) = 2.0
        _BlackDetail("Black Detail", Range(0, 1)) = 0.6
        _BlackAmount("Black Amount", Range(0, 1)) = 0.6

        [Header(Individual Difference)]
        _Seed("Pattern Seed", Vector) = (0,0,0,0) // C#からここをいじる

        [Header(Pattern Control)]
        _BellyLimit("Belly White Limit", Float) = 0.0
        _PatternSoftness("Pattern Softness", Range(0.01, 0.5)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD1; float2 uv : TEXCOORD0; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor; half4 _RedColor; half4 _BlackColor;
                float _Smoothness; float _RedScale; float _RedDetail; float _RedAmount;
                float _BlackScale; float _BlackDetail; float _BlackAmount;
                float _BellyLimit; float _PatternSoftness;
                float4 _Seed; // 追加
            CBUFFER_END

            float hash(float3 p) {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise(float3 p) {
                float3 i = floor(p); float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                                lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                           lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                                lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            float get_natural_noise(float3 p, float large_scale, float detail_amount) {
                float n_large = noise(p * large_scale);
                float n_small = noise(p * large_scale * 4.0);
                return saturate(n_large - (n_small * detail_amount * 0.5));
            }

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
    // 1. テクスチャをサンプリング（鱗の凹凸や眼の絵）
    float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
    
    // 2. テクスチャから「明暗（gray）」を抽出
    float gray = dot(texColor.rgb, float3(0.2126, 0.7152, 0.0722));
    
    // 影の深さを調整（眼などの暗い部分をクッキリさせる）
    float detailMask = pow(gray, 1.3); 

    // 3. お腹マスク（指定した高さより下は模様を消す）
    float bellyMask = saturate(smoothstep(_BellyLimit - 0.2, _BellyLimit + 0.2, IN.positionOS.y));

    // 4. 赤模様の計算
    float3 p_red = (IN.positionOS + _Seed.xyz) * _RedScale;
    float rNoise = get_natural_noise(p_red, 1.0, _RedDetail);
    float rMask = smoothstep(1.0 - _RedAmount, (1.0 - _RedAmount) + _PatternSoftness, rNoise) * bellyMask;

    // 5. 黒模様の計算
    float3 p_black = (IN.positionOS + _Seed.xyz + float3(10, 20, 30)) * _BlackScale;
    float bNoise = get_natural_noise(p_black, 1.0, _BlackDetail);
    float bMask = smoothstep(1.0 - _BlackAmount, (1.0 - _BlackAmount) + _PatternSoftness, bNoise) * bellyMask;

    // 6. ベース・赤・黒の「平坦な色」を混ぜる
    float3 flatColor = _BaseColor.rgb;
    flatColor = lerp(flatColor, _RedColor.rgb, rMask);
    flatColor = lerp(flatColor, _BlackColor.rgb, bMask);

    // 7. 【合成】
    // まず、平坦な色にテクスチャの影を乗算する（これで眼や溝がクッキリする）
    float3 shadowResult = flatColor * detailMask;
    
    // 次に、テクスチャの明るい部分だけを「反射光（スペキュラ）」として加算する
    // これにより、真っ黒な模様の上でも鱗のフチが光って質感が出る
    float specular = pow(gray, 8.0) * 0.15; 
    
    float3 finalRGB = shadowResult + specular;

    return half4(finalRGB, texColor.a);
}
            ENDHLSL
        }
    }
}