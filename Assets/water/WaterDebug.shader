Shader "KoiPond/WaterDebug"
{
    // デバッグ用：水面メッシュに割り当てて、屈折テクスチャの読み取り結果を可視化する
    // 結果の見方：
    //   - シーンの色がそのまま水面に映る → Opaque Texture が正常に読めている（シェーダー問題）
    //   - 真っ黒・真っ白になる → Opaque Texture が読めていない（パイプライン問題）
    //   - 紫色（マゼンタ）になる → コンパイルエラー
    Properties { }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DebugOpaqueRead"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                
                // 画面を 4 分割して、各領域で別の情報を表示
                half4 result = half4(0, 0, 0, 1);

                if (screenUV.x < 0.5 && screenUV.y > 0.5)
                {
                    // 左上: Opaque Texture をそのまま表示
                    float2 uv = (screenUV - float2(0, 0.5)) * float2(2, 2);
                    result.rgb = SampleSceneColor(uv);
                }
                else if (screenUV.x >= 0.5 && screenUV.y > 0.5)
                {
                    // 右上: Depth Texture を可視化（白いほど近い）
                    float2 uv = (screenUV - float2(0.5, 0.5)) * float2(2, 2);
                    float d = SampleSceneDepth(uv);
                    float lin = Linear01Depth(d, _ZBufferParams);
                    result.rgb = half3(lin, lin, lin);
                }
                else if (screenUV.x < 0.5 && screenUV.y <= 0.5)
                {
                    // 左下: 純粋な赤 (シェーダーが描画されているか確認用)
                    result.rgb = half3(1, 0, 0);
                }
                else
                {
                    // 右下: スクリーン座標を可視化
                    result.rgb = half3(screenUV.x, screenUV.y, 0);
                }

                return result;
            }
            ENDHLSL
        }
    }
}
