Shader "NISHIKI/Water_Real_Pond_NaturalFlow_Fixed"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow Color", Color) = (0.35,0.6,0.55,1)
        _DeepColor ("Deep Color", Color) = (0.02,0.12,0.15,1)

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
                float t2 = _Time.y * (_WaveSpeed * 0.73);
                float t3 = _Time.y * (_WaveSpeed * 1.21);

                float wave1 = sin(dot(originalWS.xz, float2(1.3,0.7)) + t1);
                float wave2 = sin(dot(originalWS.xz, float2(0.8,1.9)) - t2);
                float wave3 = sin(dot(originalWS.xz, float2(2.2,1.1)) + t3);

                float wave = (wave1 * 0.5 + wave2 * 0.3 + wave3 * 0.2);

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

                float t1 = _Time.y * (_WaveSpeed * 0.8);
                float t2 = _Time.y * (_WaveSpeed * 1.35);

                float2 uv1 = IN.uv + flowDir * t1 * 0.2;
                float2 uv2 = IN.uv * 1.7 - flowDir * t2 * 0.15;

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

                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);

                float edgeMask = smoothstep(0.02, 0.1, screenUV.y) *
                 smoothstep(0.02, 0.1, 1.0 - screenUV.y);

                float refractionAmount = depthFactor * _RefractionStrength * edgeMask;

                float2 distortedUV = screenUV + blendedNormal.xz * refractionAmount;

                half3 background = SampleSceneColor(distortedUV);
                float3 waterBase = lerp(background, waterColor, 0.65);

                float3 reflectDir = reflect(-viewDir, normal);
                half3 reflection = GlossyEnvironmentReflection(reflectDir, 1.0 - _Glossiness, 1.0);

                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                float3 finalColor = lerp(waterBase, reflection * _ReflectionColor.rgb, fresnel);

                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfDir = normalize(viewDir + lightDir);

                float spec = pow(saturate(dot(normal, halfDir)), 96.0) * _Glossiness;
                finalColor += spec * mainLight.color * 0.5;

                return half4(finalColor, 1.0);

            }

            ENDHLSL
        }
    }
}
