Shader "KoiPond/PondBottom"
{
    // 水底用シェーダー。URP/Lit の最低限の機能を実装しつつ、
    // 動的にUVが歪むCausticsを上乗せする。
    //
    // 機能:
    //   - Base Map (Albedo)
    //   - メインライトの拡散光 (Lambert)
    //   - 環境光 (Spherical Harmonics)
    //   - 影の受け取り
    //   - Caustics with UV wobble (うねりあり)
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.4
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 1.0

        [Header(Caustics (Animated Array))]
        [NoScaleOffset] _CausticsArray("Caustics Array (Texture2DArray)", 2DArray) = "" {}
        _CausticsFrameCount("Caustics Frame Count", Range(2, 64)) = 16
        _CausticsStrength("Caustics Strength", Range(0, 3)) = 1.0
        _CausticsTiling("Caustics Tiling", Float) = 0.4
        _CausticsFps("Caustics FPS", Range(1, 60)) = 16
        _CausticsTint("Caustics Tint", Color) = (1, 1, 0.95, 1)
        _CausticsContrast("Caustics Contrast", Range(0.5, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D_ARRAY(_CausticsArray); SAMPLER(sampler_CausticsArray);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Smoothness;
                float _AmbientStrength;

                float _CausticsFrameCount;
                float _CausticsStrength;
                float _CausticsTiling;
                float _CausticsFps;
                half4 _CausticsTint;
                float _CausticsContrast;
            CBUFFER_END

            // ============================================================
            //   Animated Caustics (Texture2DArray)
            //   各フレームが個別のテクスチャとして配列に格納されているので、
            //   フレーム間の bleed が発生しない (構造上の利点)
            //   各フレームは Repeat タイリング可能
            // ============================================================
            float SampleCausticsArray(float2 worldUV, float t)
            {
                int frameCount = (int)_CausticsFrameCount;

                float frameFloat = t * _CausticsFps;
                int frame0 = ((int)floor(frameFloat)) % frameCount;
                int frame1 = (frame0 + 1) % frameCount;
                float blend = frac(frameFloat);

                // worldUV はそのまま (Repeat タイリング)
                float c0 = SAMPLE_TEXTURE2D_ARRAY(_CausticsArray, sampler_CausticsArray, worldUV, frame0).r;
                float c1 = SAMPLE_TEXTURE2D_ARRAY(_CausticsArray, sampler_CausticsArray, worldUV, frame1).r;

                return lerp(c0, c1, blend);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS  = vp.positionCS;
                OUT.positionWS  = vp.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord    = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ===== Base color =====
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                float3 albedo = baseTex.rgb;

                // ===== Lighting =====
                float3 normalWS = normalize(IN.normalWS);

                // メインライト（影付き）
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 mainDiffuse = albedo * mainLight.color * NdotL * mainLight.shadowAttenuation;

                // 環境光（Spherical Harmonics）
                float3 ambient = SampleSH(normalWS) * albedo * _AmbientStrength;

                float3 finalRGB = mainDiffuse + ambient;

                // ===== Caustics (Texture2DArray アニメーション) =====
                // 各フレームが独立してタイリングされるので、フレーム間の継ぎ目が出ない
                float t = _Time.y;
                float2 worldUV = IN.positionWS.xz * _CausticsTiling;
                float caustics = SampleCausticsArray(worldUV, t);

                // コントラスト調整
                caustics = saturate(caustics * _CausticsContrast);

                // Caustics は加算で乗せる（光なので）
                finalRGB += _CausticsTint.rgb * caustics * _CausticsStrength * mainLight.shadowAttenuation;

                // フォグ
                finalRGB = MixFog(finalRGB, IN.fogCoord);

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        // 影投影パス（鯉などの影が水底に落ちるように）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(ShadowAttributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings OUT;
                OUT.positionCS = GetShadowPositionHClip(input);
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
