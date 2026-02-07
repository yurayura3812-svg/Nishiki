Shader "NISHIKI/Water_Final_Bump"
{
    Properties
    {
        [Header(Appearance)]
        _WaterColor ("Water Color", Color) = (0.2, 0.4, 0.5, 0.6)
        _ReflectionColor ("Reflection Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Glossiness", Range(0, 1)) = 0.9
        
        [Header(Distortion and Bump)]
        _DistortionMap ("Normal Map", 2D) = "bump" {}
        _DistortionStrength ("Distortion Strength", Range(0, 0.1)) = 0.02
        _NormalStrength ("Normal Strength", Range(0, 10)) = 1.0 // これを追加
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
            float _NormalStrength;
            float _WaveSpeed;
            float _Glossiness;
            float4 _RippleCenter;
            float _RippleTime;

            Varyings vert (Attributes IN) {
            Varyings OUT;
            float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

            // キューブの上面（ローカル座標のYが高い部分）だけをうねらせる
            if (IN.positionOS.y > 0.0) {
                float wave = sin(posWS.x * 2.0 + _Time.y * _WaveSpeed * 10.0) 
                        * cos(posWS.z * 2.5 + _Time.y * _WaveSpeed * 8.0);
                posWS.y += wave * _DistortionStrength * 5.0; 
            }

            OUT.positionWS = posWS;
            OUT.positionCS = TransformWorldToHClip(posWS);
            OUT.uv = IN.uv;
            OUT.screenPos = ComputeScreenPos(OUT.positionCS);
            OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
            OUT.viewDirWS = GetWorldSpaceViewDir(posWS);
            return OUT;
        }

            half4 frag (Varyings IN) : SV_Target {
                // 1. 波紋
                float dist = distance(IN.positionWS.xz, _RippleCenter.xz);
                float ripple = sin(dist * 60.0 - _RippleTime * 20.0);
                float rippleMask = saturate(1.0 - dist * 3.0) * saturate(1.0 - _RippleTime);
                float rippleEffect = ripple * rippleMask * 0.05;

                // --- 2. 波のゆらぎ（2方向合成で単調さを破壊） ---
                float2 speed1 = float2(_WaveSpeed, _WaveSpeed * 0.8);
                float2 speed2 = float2(-_WaveSpeed * 0.7, _WaveSpeed * 1.2);

                // タイリングを変えるのがコツ（一方は細かく、一方は大きく）
                float2 uv1 = IN.uv * 1.0 + _Time.y * speed1;
                float2 uv2 = IN.uv * 1.5 + _Time.y * speed2;

                float2 distortion1 = tex2D(_DistortionMap, uv1).rg * 2.0 - 1.0;
                float2 distortion2 = tex2D(_DistortionMap, uv2).rg * 2.0 - 1.0;

                // 合成することで、一定方向への流れが打ち消し合い、不規則なゆらぎになる
                float2 finalDistortion = (distortion1 + distortion2) * _DistortionStrength + rippleEffect;

                // 3. 視線と法線（Normal Strengthを反映）
                float3 viewDir = normalize(IN.viewDirWS);
                // ここで NormalStrength を掛けて、物理的な凹凸を強調する
                float3 bump = float3(finalDistortion.x, 0, finalDistortion.y) * _NormalStrength;
                float3 normal = normalize(IN.normalWS + bump);
                
                // 4. 屈折背景
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w + (finalDistortion * 0.5);
                half3 background = SampleSceneColor(screenUV);

                // 5. 環境反射 (艶)
                float3 reflectDir = reflect(-viewDir, normal);
                half3 envReflection = GlossyEnvironmentReflection(reflectDir, 1.0 - _Glossiness, 1.0);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 5.0);

                // 6. ライティング（影とハイライト）
                float3 lightDir = _MainLightPosition.xyz;
                float ndotl = saturate(dot(normal, lightDir)); // 波の斜面への光の当たり具合
                
                // 7. 合成
                half3 waterBase = lerp(background, _WaterColor.rgb, _WaterColor.a);
                // 陰影を乗算して凹凸をはっきりさせる
                waterBase *= (0.7 + ndotl * 0.3); 
                
                half3 finalRGB = lerp(waterBase, envReflection * _ReflectionColor.rgb, fresnel * _Glossiness);

                // 鏡面ハイライト（キラッとした芯）
                float3 halfDir = normalize(viewDir + lightDir);
                float spec = pow(max(0, dot(normal, halfDir)), 256.0 * _Glossiness) * _Glossiness;
                finalRGB += spec * _MainLightColor.rgb * _ReflectionColor.rgb * _NormalStrength;
                
                // コースティクスの簡易実装
                float2 causticUV = screenUV * 2.0 + _Time.y * _WaveSpeed;
                half caustic = tex2D(_DistortionMap, causticUV).r; // ノーマルマップを再利用
                caustic = pow(caustic, 10.0) * 2.0; // 鋭い網目にする
                background += caustic * _ReflectionColor.rgb * 0.5; // 砂利を光らせる
                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}