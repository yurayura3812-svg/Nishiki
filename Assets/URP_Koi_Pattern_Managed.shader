Shader "Custom/URP_Koi_Pattern_Managed"
{
    // 層構造:
    //   最終色 = 粘液コート + (1 - F_coat) × (色素の拡散 + グアニン反射 × 色素フィルター)
    // 計算の本体は Assets/Shader/Koi*.hlsl

    Properties
    {
        [MainTexture] _BaseMap("Albedo (detail / pigment overlay)", 2D) = "white" {}
        _MainColor("Main Color (Base)", Color) = (1,1,1,1)

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

        [Header(Scale Structure)]
        [NoScaleOffset] _ScaleNormal("Scale Normal", 2D) = "bump" {}
        [NoScaleOffset] _ScaleID("Scale ID", 2D) = "black" {}
        _ScaleTiling("Scale Tiling", Float) = 24
        _ScaleBump("Scale Bump", Range(0, 2)) = 0.55

        [Header(Guanine Reflector)]
        [NoScaleOffset] _GinrinMask("Ginrin Mask", 2D) = "white" {}
        [NoScaleOffset] _ReflectLUT("Reflect LUT (cosTheta, disorder)", 2D) = "white" {}
        _PlateTilt("Plate Tilt (deg, hikari~0 / normal~30)", Range(0, 45)) = 30
        _GuanineF0("Guanine F0 (placeholder)", Range(0.05, 1)) = 0.58
        _GuanineRoughness("Guanine Roughness", Range(0.02, 0.6)) = 0.16
        _GuanineStrength("Guanine Strength", Range(0, 4)) = 1.4
        _GinrinStrength("Ginrin Strength", Range(0, 2)) = 0.65
        _LayerDisorder("Layer Disorder (LUT Y)", Range(0, 1)) = 0.55
        _TintPower("Pigment Tint Power", Range(0.5, 4)) = 2.0
        _GlancingTint("Glancing Silver Tint", Range(0, 1)) = 0.25

        [Header(Mucus Clearcoat)]
        _CoatF0("Coat F0", Range(0.0, 0.08)) = 0.025
        _CoatRoughness("Coat Roughness", Range(0.02, 0.5)) = 0.1
        _DiffuseWrap("Diffuse Wrap", Range(0, 0.6)) = 0.22

        [Header(Caustics (Animated Array))]
        [NoScaleOffset] _CausticsArray("Caustics Array (Texture2DArray)", 2DArray) = "" {}
        _CausticsFrameCount("Caustics Frame Count", Range(2, 64)) = 16
        _CausticsStrength("Caustics Strength", Range(0, 3)) = 0.35
        _CausticsTiling("Caustics Tiling", Float) = 0.4
        _CausticsFps("Caustics FPS", Range(1, 60)) = 16
        _CausticsTint("Caustics Tint", Color) = (1, 1, 0.95, 1)
        _CausticsContrast("Caustics Contrast", Range(0.5, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Shader/KoiCommon.hlsl"
            #include "Shader/KoiPattern.hlsl"
            #include "Shader/KoiLighting.hlsl"

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = nrmInputs.normalWS;
                OUT.tangentWS = nrmInputs.tangentWS;
                OUT.bitangentWS = nrmInputs.bitangentWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 positionWS = IN.positionWS;
                float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                float3 V = normalize(GetCameraPositionWS() - positionWS);

                float3 pigmentTint;
                float3 albedo = EvaluateKoiAlbedo(IN.uv, IN.positionOS, pigmentTint);

                float3 geomN = normalize(IN.normalWS);
                float3 T = normalize(IN.tangentWS);
                float3 B = normalize(IN.bitangentWS);
                float3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_ScaleNormal, sampler_ScaleNormal, IN.uv), _ScaleBump);
                float3 scaleN = normalize(nTS.x * T + nTS.y * B + nTS.z * geomN);
                float3 guanineN = normalize(KoiRotateAroundAxis(scaleN, T, radians(_PlateTilt)));
                float ginrin = saturate(_GinrinStrength * SAMPLE_TEXTURE2D(_GinrinMask, sampler_GinrinMask, IN.uv).r);

                float NdotV_g = saturate(dot(guanineN, V));
                float3 lut = SAMPLE_TEXTURE2D(_ReflectLUT, sampler_ReflectLUT, float2(NdotV_g, _LayerDisorder)).rgb;
                float3 guanineF0 = _GuanineF0 * lut * _GuanineStrength;
                guanineF0 *= lerp(1.0, float3(0.82, 0.93, 1.05), pow(1.0 - NdotV_g, 2.0) * _GlancingTint);

                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.positionCS = IN.positionCS;
                inputData.normalWS = scaleN;
                inputData.viewDirectionWS = V;
                inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(scaleN);

                AmbientOcclusionFactor aoFactor;
                aoFactor.indirectAmbientOcclusion = 1.0h;
                aoFactor.directAmbientOcclusion = 1.0h;

                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                mainLight.distanceAttenuation *= aoFactor.directAmbientOcclusion;

                float3 diffuse = 0;
                float3 guanine = 0;
                float3 coat = 0;
                KoiAddDirectLight(mainLight, albedo, pigmentTint, scaleN, guanineN, V, guanineF0, ginrin, diffuse, guanine, coat);

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, aoFactor);
                    KoiAddDirectLight(light, albedo, pigmentTint, scaleN, guanineN, V, guanineF0, ginrin, diffuse, guanine, coat);
                LIGHT_LOOP_END
                #endif

                float NdotV = saturate(dot(scaleN, V));
                // 縁で F を 1 にすると下の色が消えて黒い輪郭になるので上限を付ける
                float F_coat = _CoatF0 + (1.0 - _CoatF0) * pow(1.0 - NdotV, 5.0);
                F_coat = min(F_coat, 0.35);

                float3 hemi = lerp(float3(0.12, 0.16, 0.14), float3(0.40, 0.48, 0.55), saturate(scaleN.y * 0.5 + 0.5));
                float3 ambient = (SampleSH(scaleN) + hemi) * albedo;

                float3 reflCoat = reflect(-V, scaleN);
                float3 reflGua = reflect(-V, guanineN);
                if (reflCoat.y < 0.0) reflCoat.y = -reflCoat.y;
                if (reflGua.y < 0.0) reflGua.y = -reflGua.y;

                float3 coatIbl = GlossyEnvironmentReflection(reflCoat, positionWS, _CoatRoughness, 1.0h, normalizedScreenSpaceUV) * F_coat;
                float3 guaIbl = GlossyEnvironmentReflection(reflGua, positionWS, _GuanineRoughness, 1.0h, normalizedScreenSpaceUV);
                guaIbl *= guanineF0 * pigmentTint * ginrin;

                float caustics = saturate(KoiSampleCaustics(positionWS.xz * _CausticsTiling, _Time.y) * _CausticsContrast);
                diffuse += albedo * _CausticsTint.rgb * caustics * _CausticsStrength;

                float3 color = coat + coatIbl + (1.0 - F_coat) * (ambient + diffuse + guanine + guaIbl);
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 2.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
