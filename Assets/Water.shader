Shader "NISHIKI/Water_Wet_Rich_Contrast"
{
    Properties
    {
        [Header(Water Base)]
        _WaterColor ("Water Tint (水底の色)", Color) = (0.05, 0.1, 0.1, 1)
        _Opacity ("Opacity (底の透け具合)", Range(0, 1)) = 0.2

        [Header(Thick Gloss)]
        _ReflectColor ("Reflection Color (艶の色)", Color) = (0.9, 0.95, 1, 1)
        _ReflectIntensity ("Gloss Power (艶の強さ)", Range(0, 20)) = 8.0
        _ReflectTightness ("Gloss Sharpness (鋭さ: 100以上推奨)", Range(1, 300)) = 120.0
        _GlossSmoothness ("Gloss Smoothness (艶のなめらかさ)", Range(0, 1)) = 0.5

        [Header(Wave Motion)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Wave Bump (波の凸凹)", Range(0, 2)) = 0.8
        _WaveSpeed ("Wave Speed", Float) = 0.03
        _Distortion ("Distortion (底のゆらぎ)", Range(0, 1)) = 0.05
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            sampler2D _NormalMap;
            float4 _WaterColor, _ReflectColor;
            float _Opacity, _WaveSpeed, _Distortion, _ReflectIntensity, _ReflectTightness, _NormalStrength, _GlossSmoothness;

            Varyings vert (Attributes IN) {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float t = _Time.y * _WaveSpeed;

                // 1. 波の法線（揺れ）
                float2 uv1 = IN.uv + float2(t, t * 0.5);
                float2 uv2 = IN.uv * 0.7 - float2(t * 0.3, t * 0.4);
                float3 n1 = UnpackNormal(tex2D(_NormalMap, uv1));
                float3 n2 = UnpackNormal(tex2D(_NormalMap, uv2));
                float3 blendedNormal = normalize(n1 + n2);
                blendedNormal.xy *= _NormalStrength;
                blendedNormal = normalize(blendedNormal);

                // 2. 水底のゆらぎ
                float2 distortedUV = screenUV + blendedNormal.xy * _Distortion * 0.1;
                half3 background = SampleSceneColor(distortedUV);
                float3 waterBase = lerp(background, _WaterColor.rgb, _Opacity);

                // 3. 【厚塗りの艶ロジック】
                // 角度計算(dot)を極端に絞って、さらにsmoothstepで「パキッ」と「ヌルッ」を両立させる
                float nh = saturate(blendedNormal.z);
                float glossMask = pow(nh, _ReflectTightness);
                
                // 艶の境界をパキパキにする
                float edge = 1.0 - _GlossSmoothness;
                glossMask = smoothstep(edge * 0.1, edge, glossMask);

                // 4. 合成（加算ではなく、艶がある場所は下地を上書きする）
                float3 reflection = _ReflectColor.rgb * _ReflectIntensity;
                float3 finalColor = lerp(waterBase, reflection, glossMask);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}