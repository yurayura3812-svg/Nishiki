Shader "Custom/URP_Koi_Pattern_Natural"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (Base Texture with Scales)", 2D) = "white" {}
        [MainColor] _BaseColor("Color Tint (White base)", Color) = (1,1,1,1)
        
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        
        [Header(Red Pattern)]
        _RedColor("Red Color", Color) = (0.8, 0.1, 0.1, 1)
        _RedScale("Red Scale (Large)", Float) = 1.5
        _RedDetail("Red Detail (Small noise)", Range(0, 1)) = 0.5
        _RedAmount("Red Amount", Range(0, 1)) = 0.5
        
        [Header(Black Pattern)]
        _BlackColor("Black Color", Color) = (0.1, 0.1, 0.1, 1)
        _BlackScale("Black Scale (Large)", Float) = 2.0
        _BlackDetail("Black Detail (Small noise)", Range(0, 1)) = 0.6
        _BlackAmount("Black Amount", Range(0, 1)) = 0.6

        [Header(Pattern Control)]
        _BellyLimit("Belly White Limit (Height)", Float) = 0.0
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

            struct Attributes
            {
                float4 positionOS : POSITION; // Object Space Position
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD1; 
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RedColor;
                half4 _BlackColor;
                float _Smoothness;
                float _RedScale;
                float _RedDetail;
                float _RedAmount;
                float _BlackScale;
                float _BlackDetail;
                float _BlackAmount;
                float _BellyLimit;
                float _PatternSoftness;
            CBUFFER_END

            float hash(float3 p) {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // 3D Value Noise
            float noise(float3 p) {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                                lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                           lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                                lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            // 大小2つのノイズを混ぜて自然な模様を作る関数
            float get_natural_noise(float3 p, float large_scale, float detail_amount) {
                float n_large = noise(p * large_scale); // 大きな塊
                float n_small = noise(p * large_scale * 4.0); // 細かい掠れ
                // 大きなノイズをベースに、小さなノイズを少し混ぜる
                return saturate(n_large - (n_small * detail_amount * 0.5));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz; // Object空間座標をfragに渡す
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. ベーステクスチャ取得 (画像4の鱗がある画像)
                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float3 originalRGB = texColor.rgb;
                float3 finalRGB = originalRGB;

                // 2. お腹側を白くするマスク (ObjectのY座標ベース)
                float bellyMask = smoothstep(_BellyLimit - 0.1, _BellyLimit + 0.1, IN.positionOS.y);
                bellyMask = saturate(bellyMask);

                // --- 模様の生成 (改良された複合ノイズを使用) ---
                
                // 赤い模様
                float rN = get_natural_noise(IN.positionOS, _RedScale, _RedDetail);
                float rMask = smoothstep(1.0 - _RedAmount, (1.0 - _RedAmount) + _PatternSoftness, rN);
                rMask *= bellyMask; // お腹側を制限

                // 黒い模様 (位置を大きくずらす)
                float3 p_black = IN.positionOS + float3(10.5, 20.2, 30.7);
                float bN = get_natural_noise(p_black, _BlackScale, _BlackDetail);
                float bMask = smoothstep(1.0 - _BlackAmount, (1.0 - _BlackAmount) + _PatternSoftness, bN);
                bMask *= bellyMask;

                // --- 鱗の質感を残す合成 (乗算ベース) ---
                
                // 赤い模様:
                float3 redPatterned = originalRGB * _RedColor.rgb * 2.0; // 明るさ補正用の2.0
                finalRGB = lerp(finalRGB, redPatterned, rMask); // マスクの場所だけ適用

                // 黒い模様:
                float3 blackPatterned = originalRGB * _BlackColor.rgb;
                finalRGB = lerp(finalRGB, blackPatterned, bMask);

                return half4(finalRGB, texColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}