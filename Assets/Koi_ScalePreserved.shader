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
        _Seed("Pattern Seed", Vector) = (0,0,0,0)

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

            struct Attributes { 
                float4 positionOS : POSITION; 
                float2 uv : TEXCOORD0; 
            };

            struct Varyings { 
                float4 positionCS : SV_POSITION; 
                float4 positionOS : TEXCOORD1; // 精度安定のためfloat4へ
                float2 uv : TEXCOORD0; 
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor; half4 _RedColor; half4 _BlackColor;
                float _Smoothness; float _RedScale; float _RedDetail; float _RedAmount;
                float _BlackScale; float _BlackDetail; float _BlackAmount;
                float _BellyLimit; float _PatternSoftness;
                float4 _Seed;
            CBUFFER_END

            // --- Noise Functions ---
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

            // --- Vertex Shader ---
            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS; // 座標をフラグメントへ渡す
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            // --- Fragment Shader ---
            half4 frag(Varyings IN) : SV_Target {
                // 1. テクスチャサンプリング
                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                
                // 【重要】元のテクスチャの色を無視し、輝度（影）だけを抽出
                // これにより、テクスチャの端にある「赤み」などが混ざらなくなります
                float gray = dot(texColor.rgb, float3(0.2126, 0.7152, 0.0722));
                float detailMask = saturate(pow(gray, 1.3));

                // 2. 座標を安定させて取得
                float3 pos = IN.positionOS.xyz;

                // 3. お腹マスク
                float bellyMask = saturate(smoothstep(_BellyLimit - 0.2, _BellyLimit + 0.2, pos.y));

                // 4. 赤模様の計算
                float3 p_red = (pos + _Seed.xyz) * _RedScale;
                float rNoise = get_natural_noise(p_red, 1.0, _RedDetail);
                float rMask = smoothstep(1.0 - _RedAmount, (1.0 - _RedAmount) + _PatternSoftness, rNoise) * bellyMask;

                // 5. 黒模様の計算
                float3 p_black = (pos + _Seed.xyz + float3(10.0, 20.0, 30.0)) * _BlackScale;
                float bNoise = get_natural_noise(p_black, 1.0, _BlackDetail);
                float bMask = smoothstep(1.0 - _BlackAmount, (1.0 - _BlackAmount) + _PatternSoftness, bNoise) * bellyMask;

                // 6. 色の合成
                // 地肌の色（_BaseColor）からスタートし、模様を上書きしていく
                float3 flatColor = _BaseColor.rgb;
                flatColor = lerp(flatColor, _RedColor.rgb, rMask);
                flatColor = lerp(flatColor, _BlackColor.rgb, bMask);

                // 7. 最終出力
                // 合成した色にテクスチャの影（鱗の溝など）を掛ける
                float3 finalRGB = flatColor * detailMask;
                
                // ヌラヌラとした反射光（スペキュラ）を少し足す
                finalRGB += pow(detailMask, 8.0) * 0.12; 

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}