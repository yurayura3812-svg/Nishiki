Shader "NISHIKI/Water_Ultimate"
{
    Properties
    {
        [Header(Appearance)]
        _WaterColor ("Water Color", Color) = (0.2, 0.4, 0.5, 0.6)
        _ReflectionColor ("Reflection Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Glossiness", Range(0, 1)) = 0.9
        
        [Header(Distortion)]
        _DistortionMap ("Normal Map", 2D) = "bump" {}
        _DistortionStrength ("Distortion Strength", Range(0, 0.1)) = 0.02
        _WaveSpeed ("Wave Speed", Float) = 0.05

        [Header(Ripple Settings)]
        _RippleCenter ("Ripple Center", Vector) = (0,0,0,0)
        _RippleTime ("Ripple Time", Float) = 1.0 
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline"}
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
            };

            sampler2D _DistortionMap;
            float4 _WaterColor;
            float4 _ReflectionColor;
            float _DistortionStrength;
            float _WaveSpeed;
            float _Glossiness;
            float4 _RippleCenter;
            float _RippleTime;

            Varyings vert (Attributes IN) {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(OUT.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target {
                // --- 1. 動的な波紋 (Ripple) ---
                float dist = distance(IN.positionWS.xz, _RippleCenter.xz);
                float ripple = sin(dist * 60.0 - _RippleTime * 20.0); // 波を細かく修正
                float rippleMask = saturate(1.0 - dist * 3.0) * saturate(1.0 - _RippleTime);
                float rippleEffect = ripple * rippleMask * 0.05;

                // --- 2. 波のゆらぎ ---
                float2 timeUV = IN.uv + _Time.y * _WaveSpeed;
                float2 distortion = tex2D(_DistortionMap, timeUV).rg * 2.0 - 1.0;
                float2 finalDistortion = distortion * _DistortionStrength + rippleEffect;

                // --- 3. 屈折（背景の歪み） ---
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                screenUV += finalDistortion;
                half3 background = SampleSceneColor(screenUV);

                // --- 4. 水の色と合成 ---
                half3 waterBase = lerp(background, _WaterColor.rgb, _WaterColor.a);

                // --- 5. フレネル反射 ---
                float3 viewDir = normalize(IN.viewDirWS);
                float3 normal = normalize(IN.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 4.0);
                half3 finalRGB = lerp(waterBase, _ReflectionColor.rgb, fresnel * _Glossiness);

                // --- 6. 鏡面反射（リファレンス再現：太い光の輪） ---
                float specularBase = max(0, finalDistortion.r + finalDistortion.g);
                float specular = pow(specularBase, 8.0) * _Glossiness;
                float edgeLight = smoothstep(0.4, 0.5, specularBase) * 0.5;
                finalRGB += (specular + edgeLight) * _ReflectionColor.rgb;

                // --- 7. 水面のキラキラ (Sparkle) ---
                float sparkle = pow(max(0, distortion.g), 100.0) * 10.0;
                finalRGB += sparkle * _ReflectionColor.rgb * _Glossiness;

                return half4(finalRGB, 1.0); // ここが抜けていました
            }
            ENDHLSL
        }
    }
}