Shader "NISHIKI/Water_Quiet_Clear_Final"
{
    Properties
    {
        [Header(Water Settings)]
        _WaterColor ("Water Color (水の色)", Color) = (0.1, 0.4, 0.4, 1)
        _Opacity ("Opacity (0.1〜0.2推奨)", Range(0, 1)) = 0.15

        [Header(Wave Motion)]
        _NormalMap ("Normal Map (波の形)", 2D) = "bump" {}
        _WaveSpeed ("Wave Speed (揺れの速さ)", Float) = 0.03
        _Distortion ("Distortion (水底のゆらぎ)", Range(0, 1)) = 0.05
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
            float4 _WaterColor;
            float _Opacity, _WaveSpeed, _Distortion;

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

                // 1. ゆったりとした波の重なり
                float2 uv1 = IN.uv + float2(t, t * 0.5);
                float2 uv2 = IN.uv * 0.8 - float2(t * 0.4, t * 0.2);
                float3 n1 = UnpackNormal(tex2D(_NormalMap, uv1));
                float3 n2 = UnpackNormal(tex2D(_NormalMap, uv2));
                float3 blendedNormal = normalize(n1 + n2);

                // 2. 水底の景色を「ゆらゆら」と自然に歪ませる
                float2 distortedUV = screenUV + blendedNormal.xy * _Distortion * 0.1;
                half3 background = SampleSceneColor(distortedUV);

                // 3. 透明感のある水の色をそっと乗せる
                float3 finalColor = lerp(background, _WaterColor.rgb, _Opacity);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}