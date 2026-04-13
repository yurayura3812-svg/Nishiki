<<<<<<< HEAD
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
=======
Shader "Custom/URPWater_NoEdgeStretch"
{
    Properties
    {
        [Header(Base Visual)]
        _MainTex("Caustics Texture", 2D) = "white" {}
        _BaseColor("Water Color & Alpha", Color) = (0, 0.5, 0.7, 0.4)
        
        [Header(Refraction Control)]
        _RefractionStrength("Refraction Strength", Range(0, 0.05)) = 0.02
        _DistortSpeed("Distort Speed", Float) = 1.0
        _DistortScale("Distort Scale", Float) = 10.0

        [Header(Wave Settings)]
        _WaveAmp("Wave Height", Float) = 0.05
        _WaveFreq("Wave Speed", Float) = 1.0
        _WaveScale("Wave Density", Float) = 0.05
>>>>>>> 5769b59 (水のシェーダー仮作成)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };

<<<<<<< HEAD
            sampler2D _NormalMap;
            float4 _WaterColor;
            float _Opacity, _WaveSpeed, _Distortion;
=======
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _BaseColor;
            float _WaveAmp, _WaveFreq, _WaveScale, _RefractionStrength, _DistortSpeed, _DistortScale;
>>>>>>> 5769b59 (水のシェーダー仮作成)

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float wave = sin(worldPos.x * _WaveScale + _Time.y * _WaveFreq) * _WaveAmp;
                wave += cos(worldPos.z * (_WaveScale * 1.5) - _Time.y * (_WaveFreq * 0.8)) * _WaveAmp * 0.5;
                
                worldPos.y += wave;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 screenPos = input.screenPos;
                float2 baseUV = screenPos.xy / screenPos.w;

<<<<<<< HEAD
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
=======
                // --- 端の伸び防止ロジック ---
                // 画面端（0.0や1.0に近い場所）ほど 0 になるマスクを作成
                float edgeMask = saturate(baseUV.x * 20.0) * saturate((1.0 - baseUV.x) * 20.0) *
                                 saturate(baseUV.y * 20.0) * saturate((1.0 - baseUV.y) * 20.0);

                // 歪みの計算
                float time = _Time.y * _DistortSpeed;
                float2 noise = float2(sin(input.uv.x * _DistortScale + time), cos(input.uv.y * _DistortScale + time));
                
                // 画面端では歪みを edgeMask で打ち消す
                float2 distortedUV = baseUV + (noise * _RefractionStrength * edgeMask);

                // 歪んだ背景を取得
                half3 koiBackground = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, distortedUV).rgb;

                // 水面のテクスチャ重ね
                float2 causticsUV = input.uv + float2(_Time.y * 0.02, _Time.y * 0.01);
                half caustics = tex2D(_MainTex, causticsUV).r;

                half3 finalColor = lerp(koiBackground, caustics * _BaseColor.rgb * 1.5, caustics * 0.2);
                finalColor = lerp(koiBackground, finalColor * _BaseColor.rgb, _BaseColor.a);
>>>>>>> 5769b59 (水のシェーダー仮作成)

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}