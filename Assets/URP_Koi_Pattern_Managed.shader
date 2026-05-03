Shader "Custom/URP_Koi_Pattern_Managed"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _MainColor("Main Color (Base)", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        
        [Header(Sub Color 1)]
        _SubColor1("Sub Color 1", Color) = (0.8, 0.1, 0.1, 1)
        _Sub1Scale("Sub 1 Scale", Float) = 1.5
        _Sub1Detail("Sub 1 Detail", Range(0, 1)) = 0.5
        _Sub1Amount("Sub 1 Amount", Range(0, 1)) = 0.5
        
        [Header(Sub Color 2)]
        _SubColor2("Sub Color 2", Color) = (0.1, 0.1, 0.1, 1)
        _Sub2Scale("Sub 2 Scale", Float) = 2.0
        _Sub2Detail("Sub 2 Detail", Range(0, 1)) = 0.6
        _Sub2Amount("Sub 2 Amount", Range(0, 1)) = 0.6

        _Seed("Pattern Seed", Vector) = (0,0,0,0)
        _BellyLimit("Belly White Limit", Float) = 0.0
        _PatternSoftness("Pattern Softness", Range(0.01, 0.5)) = 0.1

        [Header(Caustics)]
        _CausticsTex("Caustics Tex", 2D) = "black" {}
        _CausticsStrength("Caustics Strength", Range(0, 3)) = 0.6
        _CausticsTiling("Caustics Tiling", Float) = 0.4
        _CausticsSpeed("Caustics Speed (XY)", Vector) = (0.05, 0.04, 0, 0)
        _CausticsContrast("Caustics Contrast", Range(0.5, 4)) = 2.5
        _CausticsCutoff("Caustics Cutoff", Range(0, 0.8)) = 0.3
        _CausticsTint("Caustics Tint", Color) = (1, 1, 0.95, 1)
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
                float4 positionOS : TEXCOORD1; 
                float2 uv : TEXCOORD0; 
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CausticsTex); SAMPLER(sampler_CausticsTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _MainColor; half4 _SubColor1; half4 _SubColor2;
                float _Smoothness; float _Sub1Scale; float _Sub1Detail; float _Sub1Amount;
                float _Sub2Scale; float _Sub2Detail; float _Sub2Amount;
                float _BellyLimit; float _PatternSoftness;
                float4 _Seed;

                float4 _CausticsTex_ST;
                float _CausticsStrength;
                float _CausticsTiling;
                float4 _CausticsSpeed;
                float _CausticsContrast;
                float _CausticsCutoff;
                half4 _CausticsTint;
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
                OUT.positionOS = IN.positionOS; 
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            // --- Fragment Shader ---
            half4 frag(Varyings IN) : SV_Target {
                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float gray = dot(texColor.rgb, float3(0.2126, 0.7152, 0.0722));
                float detailMask = saturate(pow(abs(gray), 1.3));

                float3 pos = IN.positionOS.xyz;
                float bellyMask = saturate(smoothstep(_BellyLimit - 0.2, _BellyLimit + 0.2, pos.y));

                // --- Sub 1 ---
                float3 p1 = (pos + _Seed.xyz) * _Sub1Scale;
                float n1 = get_natural_noise(p1, 1.0, _Sub1Detail); 
                float m1 = smoothstep(1.0 - _Sub1Amount, (1.0 - _Sub1Amount) + _PatternSoftness, n1) * bellyMask;

                // --- Sub 2 ---
                float3 p2 = (pos + _Seed.xyz + float3(10.0, 20.0, 30.0)) * _Sub2Scale;
                float n2 = get_natural_noise(p2, 1.0, _Sub2Detail);
                float m2 = smoothstep(1.0 - _Sub2Amount, (1.0 - _Sub2Amount) + _PatternSoftness, n2) * bellyMask;

                float3 finalColor = _MainColor.rgb;
                finalColor = lerp(finalColor, _SubColor1.rgb, m1);
                finalColor = lerp(finalColor, _SubColor2.rgb, m2);

                float3 finalRGB = finalColor * detailMask;
                finalRGB += pow(detailMask, 8.0) * 0.12;

                // ---- Caustics をワールド XZ から計算して上乗せ ----
                float t = _Time.y;
                float2 cuv = IN.positionWS.xz * _CausticsTiling + _CausticsSpeed.xy * t;
                float c1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, cuv).r;
                float c2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, cuv * 1.37 - _CausticsSpeed.xy * t * 0.5).r;
                float caustics = min(c1, c2);
                caustics = saturate((caustics - _CausticsCutoff) * _CausticsContrast);
                finalRGB += _CausticsTint.rgb * caustics * _CausticsStrength;

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}