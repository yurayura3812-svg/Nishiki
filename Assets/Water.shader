Shader "NISHIKI/Water_Real_Pond_Final_Fixed"
{
    Properties
    {
        [Header(Color Settings)]
        _ShallowColor ("Shallow Color (浅瀬の色)", Color) = (0.35, 0.6, 0.55, 1)
        _DeepColor ("Deep Color (深所の色)", Color) = (0.02, 0.12, 0.15, 1)
        _AbsorptionTint ("Absorption Tint (底の染まり色)", Color) = (0.85, 0.95, 1.0, 1)
        _WaterOpacity ("Water Opacity (水の色の濃さ)", Range(0, 1)) = 0.5

        [Header(Reflection)]
        _ReflectionColor ("Reflection Color", Color) = (1,1,1,1)
        _Glossiness ("Glossiness", Range(0,1)) = 0.65
        _FresnelPower ("Fresnel Power", Range(1,8)) = 4

        [Header(Wave)]
        _WaveHeight ("Wave Height", Range(0,0.3)) = 0.06
        _WaveSpeed ("Wave Speed", Float) = 0.6
        _FlowDirection ("Flow Direction", Vector) = (0.6,0.0,0.3,0)

        [Header(Normal)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,3)) = 1.2

        [Header(Refraction)]
        _RefractionStrength ("Refraction Strength", Range(0,0.1)) = 0.03

        [Header(Depth)]
        _DepthMultiplier ("Depth Multiplier", Range(0.1,5)) = 1.8
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
            };

            sampler2D _NormalMap;
            float4 _ShallowColor;
            float4 _DeepColor;
            float4 _AbsorptionTint;
            float _WaterOpacity;
            float4 _ReflectionColor;
            float _Glossiness;
            float _FresnelPower;
            float _WaveHeight;
            float _WaveSpeed;
            float4 _FlowDirection;
            float _NormalStrength;
            float _RefractionStrength;
            float _DepthMultiplier;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 originalWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 posWS = originalWS;

                float t1 = _Time.y * _WaveSpeed;
                float wave1 = sin(dot(originalWS.xz, float2(1.3,0.7)) + t1);
                float wave2 = sin(dot(originalWS.xz, float2(0.8,1.9)) - t1 * 0.73);
                float wave = (wave1 * 0.6 + wave2 * 0.4);

                posWS.y = originalWS.y + wave * _WaveHeight;

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.viewDirWS = GetWorldSpaceViewDir(posWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(IN.viewDirWS);
                float2 flowDir = normalize(_FlowDirection.xz);

                float t = _Time.y * _WaveSpeed;
                float2 uv1 = IN.uv + flowDir * t * 0.1;
                float2 uv2 = IN.uv * 1.7 - flowDir * t * 0.15;

                float3 n1 = UnpackNormal(tex2D(_NormalMap, uv1));
                float3 n2 = UnpackNormal(tex2D(_NormalMap, uv2));
                float3 blendedNormal = normalize(n1 + n2);
                float3 normal = normalize(IN.normalWS + blendedNormal * _NormalStrength);

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceDepth = LinearEyeDepth(IN.positionCS.z, _ZBufferParams);

                float depthDiff = saturate(sceneDepth - surfaceDepth);
                float depthFactor = saturate(depthDiff * _DepthMultiplier);

                depthFactor = max(0.2, depthFactor); // どんなに浅くても20%は染まった状態にする

                // --- 屈折と二重防止エッジマスク ---
                float edgeMask = smoothstep(0.02, 0.1, screenUV.x) * smoothstep(0.02, 0.1, 1.0 - screenUV.x) *
                                 smoothstep(0.02, 0.1, screenUV.y) * smoothstep(0.02, 0.1, 1.0 - screenUV.y);
                float boundaryMask = smoothstep(0.01, 0.05, depthDiff); // 0.05の幅で徐々に歪ませる
                float refractionAmount = depthFactor * _RefractionStrength * edgeMask * boundaryMask;
                float2 distortedUV = screenUV + blendedNormal.xz * refractionAmount;

                // --- 色の合成 ---
                half3 background = SampleSceneColor(distortedUV);
                
                // 【重要】色計算用の深さをここで作る（下駄を履かせる）
                float colorDepth = saturate(depthFactor + 0.5); 

                // 1. 背景（鯉）を染める計算にも colorDepth を使う！
                // これで浅瀬でも「真っ白な背景」を拾わなくなります
                float3 absorbed = background * lerp(float3(1,1,1), _AbsorptionTint.rgb, colorDepth);
                
                // 2. 水そのものの色
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, colorDepth);
                
                // 3. 最終的な合成（ここも colorDepth を使う）
                float3 waterBase = lerp(absorbed, waterColor, colorDepth * _WaterOpacity);
                // --- 反射 ---
                float3 reflectDir = reflect(-viewDir, normal);
                half3 reflection = GlossyEnvironmentReflection(reflectDir, 1.0 - _Glossiness, 1.0);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                float3 finalColor = lerp(waterBase, reflection * _ReflectionColor.rgb, fresnel);

                // --- スペキュラ ---
                Light mainLight = GetMainLight();
                float3 halfDir = normalize(viewDir + mainLight.direction);
                float spec = pow(saturate(dot(normal, halfDir)), 96.0) * _Glossiness;
                finalColor += spec * mainLight.color * 0.5;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}