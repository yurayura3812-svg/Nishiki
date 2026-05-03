Shader "KoiPond/Water"
{
    // Unity 6 / Forward+ 対応版
    // Depth Texture が Transparent から読めない問題を回避するため、
    // 深度差は頂点シェーダーで計算した「水面のY」と「カメラ→水面距離の比率」から疑似的に算出。
    // 屈折は _CameraOpaqueTexture を直接読む。
    Properties
    {
        [Header(Color)]
        _ShallowColor("Shallow Water Color", Color) = (0.45, 0.75, 0.65, 0.4)
        _DeepColor("Deep Water Color", Color) = (0.05, 0.18, 0.22, 0.95)
        _ColorBlendDistance("Color Blend Distance (view-based)", Float) = 8.0
        _ColorBlendPower("Color Blend Power", Range(0.1, 5)) = 1.5

        [Header(Surface Normals)]
        _NormalMapA("Normal Map A", 2D) = "bump" {}
        _NormalMapB("Normal Map B", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 0.6
        _NormalSpeedA("Normal Scroll Speed A", Vector) = (0.02, 0.015, 0, 0)
        _NormalSpeedB("Normal Scroll Speed B", Vector) = (-0.015, 0.025, 0, 0)
        _NormalTiling("Normal Tiling", Float) = 0.5

        [Header(Waves Gerstner)]
        _WaveA("Wave A (dirX, dirZ, steepness, wavelength)", Vector) = (1, 0.3, 0.25, 6)
        _WaveB("Wave B (dirX, dirZ, steepness, wavelength)", Vector) = (0.4, 0.9, 0.20, 4)
        _WaveC("Wave C (dirX, dirZ, steepness, wavelength)", Vector) = (-0.7, 0.5, 0.15, 2.5)
        _WaveSpeed("Wave Speed", Float) = 1.0

        [Header(Ripple Interaction)]
        _RippleTex("Ripple RT (R=height G=normalX B=normalZ)", 2D) = "black" {}
        _RippleAreaCenter("Ripple Area Center XZ", Vector) = (0, 0, 0, 0)
        _RippleAreaSize("Ripple Area Size (world units)", Float) = 20.0
        _RippleHeightStrength("Ripple Vertex Strength", Range(0, 2)) = 0.4
        _RippleNormalStrength("Ripple Normal Strength", Range(0, 4)) = 1.5

        [Header(Reflection Refraction)]
        _RefractionStrength("Refraction Strength", Range(0, 0.2)) = 0.04
        _ReflectionStrength("Reflection Strength", Range(0, 1)) = 0.6
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 4.0
        _FresnelBias("Fresnel Bias", Range(0, 0.5)) = 0.04

        [Header(Specular)]
        _SunColor("Sun Color", Color) = (1, 0.95, 0.85, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.92
        _SpecularStrength("Specular Strength", Range(0, 4)) = 1.5

        [Header(Caustics on Surface)]
        _CausticsTex("Caustics Tex (greyscale)", 2D) = "black" {}
        _CausticsStrength("Caustics Strength", Range(0, 2)) = 0.6
        _CausticsTiling("Caustics Tiling", Float) = 0.3
        _CausticsSpeed("Caustics Speed", Vector) = (0.03, 0.04, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Opaque Texture を直接宣言（depth は使わない）
            TEXTURE2D(_CameraOpaqueTexture);  SAMPLER(sampler_CameraOpaqueTexture);

            // 屈折専用 RT (RefractionCameraSetup.cs から渡される)
            // 中身: メインカメラより広い視野で撮影されたシーン
            // _RefractionTexParams.xy = メイン視野が RT 内で占める割合(=1/viewExpansion)
            TEXTURE2D(_RefractionTex);        SAMPLER(sampler_RefractionTex);
            float4 _RefractionTexParams;

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _ColorBlendDistance;
                float  _ColorBlendPower;

                float4 _NormalMapA_ST;
                float4 _NormalMapB_ST;
                float  _NormalStrength;
                float4 _NormalSpeedA;
                float4 _NormalSpeedB;
                float  _NormalTiling;

                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;
                float  _WaveSpeed;

                float4 _RippleAreaCenter;
                float  _RippleAreaSize;
                float  _RippleHeightStrength;
                float  _RippleNormalStrength;

                float  _RefractionStrength;
                float  _ReflectionStrength;
                float  _FresnelPower;
                float  _FresnelBias;

                float4 _SunColor;
                float  _Smoothness;
                float  _SpecularStrength;

                float4 _CausticsTex_ST;
                float  _CausticsStrength;
                float  _CausticsTiling;
                float4 _CausticsSpeed;
            CBUFFER_END

            TEXTURE2D(_NormalMapA);    SAMPLER(sampler_NormalMapA);
            TEXTURE2D(_NormalMapB);    SAMPLER(sampler_NormalMapB);
            TEXTURE2D(_RippleTex);     SAMPLER(sampler_RippleTex);
            TEXTURE2D(_CausticsTex);   SAMPLER(sampler_CausticsTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float4 screenPos   : TEXCOORD4;
                float2 uv          : TEXCOORD5;
                float  fogCoord    : TEXCOORD6;
                float  viewDist    : TEXCOORD7; // カメラから水面までの距離(深さ感の代用)
            };

            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 bitangent, float t)
            {
                float steepness = wave.z;
                float wavelength = max(wave.w, 0.001);
                float k = 2.0 * 3.14159265 / wavelength;
                float c = sqrt(9.8 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, p.xz) - c * t * _WaveSpeed);
                float a = steepness / k;

                float sinF = sin(f);
                float cosF = cos(f);

                tangent += float3(
                    -d.x * d.x * (steepness * sinF),
                     d.x * (steepness * cosF),
                    -d.x * d.y * (steepness * sinF));
                bitangent += float3(
                    -d.x * d.y * (steepness * sinF),
                     d.y * (steepness * cosF),
                    -d.y * d.y * (steepness * sinF));

                return float3(
                    d.x * (a * cosF),
                    a * sinF,
                    d.y * (a * cosF));
            }

            float3 SampleRipple(float3 worldPos)
            {
                float2 local = (worldPos.xz - _RippleAreaCenter.xz) / _RippleAreaSize + 0.5;
                if (any(local < 0) || any(local > 1))
                    return float3(0, 0, 0);
                float3 r = SAMPLE_TEXTURE2D_LOD(_RippleTex, sampler_RippleTex, local, 0).rgb;
                return float3(r.r, r.g * 2 - 1, r.b * 2 - 1);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float t = _Time.y;

                // 先に波紋を取得して、Gerstner波の強度を抑制する係数を作る
                // 波紋が強い場所ほど Gerstner 波を弱める（表面張力的な近似）
                // 勾配ではなく高さ R のみで判定（勾配は浮動小数点誤差で常に微小値が出るため）
                float3 ripple = SampleRipple(posWS);
                float rippleActivity = saturate(abs(ripple.x) * 6.0);
                float gerstnerScale = 1.0 - rippleActivity * 0.85; // 最大85%抑制

                float3 tangent   = float3(1, 0, 0);
                float3 bitangent = float3(0, 0, 1);
                float3 offset = 0;
                offset += GerstnerWave(_WaveA, posWS, tangent, bitangent, t);
                offset += GerstnerWave(_WaveB, posWS, tangent, bitangent, t);
                offset += GerstnerWave(_WaveC, posWS, tangent, bitangent, t);
                offset *= gerstnerScale;

                posWS += offset;

                posWS.y += ripple.x * _RippleHeightStrength;

                float3 normalFromWaves = normalize(cross(bitangent, tangent));

                OUT.positionWS  = posWS;
                OUT.positionCS  = TransformWorldToHClip(posWS);
                OUT.normalWS    = normalFromWaves;

                float3 wTangent = TransformObjectToWorldDir(IN.tangentOS.xyz);
                float3 wNormal  = normalFromWaves;
                float3 wBitan   = cross(wNormal, wTangent) * IN.tangentOS.w;
                OUT.tangentWS   = wTangent;
                OUT.bitangentWS = wBitan;

                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.uv = IN.uv;
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);

                // 視線距離 (カメラ→水面表面)
                OUT.viewDist = length(GetCameraPositionWS() - posWS);
                return OUT;
            }

            float3 SampleAnimatedNormal(float3 worldPos, float t)
            {
                float2 baseUV = worldPos.xz * _NormalTiling;
                float2 uvA = baseUV * _NormalMapA_ST.xy + _NormalMapA_ST.zw + _NormalSpeedA.xy * t;
                float2 uvB = baseUV * _NormalMapB_ST.xy + _NormalMapB_ST.zw + _NormalSpeedB.xy * t;

                float3 nA = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMapA, sampler_NormalMapA, uvA));
                float3 nB = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMapB, sampler_NormalMapB, uvB));

                float3 n = normalize(float3(nA.xy + nB.xy, nA.z * nB.z));
                n.xy *= _NormalStrength;
                return normalize(n);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // 波紋を先に読み取る
                float3 ripple = SampleRipple(IN.positionWS);
                float rippleActivity = saturate(abs(ripple.x) * 6.0);

                // ===== 表面法線 =====
                // Normal Map のさざ波は波紋がある場所では弱める（表面張力的な近似）
                float3 nTS = SampleAnimatedNormal(IN.positionWS, t);
                nTS.xy *= (1.0 - rippleActivity * 0.85);
                nTS = normalize(nTS);

                float3x3 TBN = float3x3(normalize(IN.tangentWS), normalize(IN.bitangentWS), normalize(IN.normalWS));
                float3 normalWS = normalize(mul(nTS, TBN));

                normalWS = normalize(normalWS + float3(ripple.y, 0, ripple.z) * _RippleNormalStrength);

                // ===== ベクトル =====
                float3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);
                Light mainLight  = GetMainLight();
                float3 lightDir  = normalize(mainLight.direction);

                // ===== 視線距離ベースの疑似「水深」 =====
                // カメラから遠い部分ほど水中での光路が長い→深く見える、という近似。
                // 真上から見下ろす視点で、水塊の深さに比例した効果になる。
                float depthFactor = saturate(IN.viewDist / _ColorBlendDistance);
                depthFactor = pow(depthFactor, _ColorBlendPower);

                float4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);

                // ===== 屈折 (専用RTから読む。RTはメインカメラより広い視野で撮影されている) =====
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float2 refrOffset = normalWS.xz * _RefractionStrength;

                // メイン視野は RT 内の中央 _RefractionTexParams.xy の領域に映る。
                // screenUV(0..1) を RT 内のメイン視野領域へマッピング:
                //   rt_uv = 0.5 + (screenUV - 0.5) * scale
                // ここで scale = _RefractionTexParams.xy = 1/viewExpansion
                // 屈折オフセットは RT 上での相対距離なので scale を掛けない（オフセット量はそのまま）
                float2 baseUV = 0.5 + (screenUV - 0.5) * _RefractionTexParams.xy;
                float2 refrUV = baseUV + refrOffset * _RefractionTexParams.xy;

                // RT は広く撮影しているので、メインカメラの画面端でも RT の中央付近にある
                // → 屈折オフセットを足しても RT の端を超える可能性が極めて低い
                refrUV = saturate(refrUV);

                float3 refractedScene = SAMPLE_TEXTURE2D(_RefractionTex, sampler_RefractionTex, refrUV).rgb;

                // 水色とブレンド (waterColor.a で水色の濃さを調整)
                float3 throughWater = lerp(refractedScene, waterColor.rgb, waterColor.a * (0.3 + depthFactor * 0.7));

                // ===== Caustics =====
                // 水底の Caustics は URP Decal Projector で別途投影しているのでここでは何もしない。
                // (旧: 水面に直接 Caustics を加算していたが、水底ではなく水面に光って見える問題があったため除去)

                // ===== 反射 =====
                float fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                float3 reflDir = reflect(-viewDirWS, normalWS);

                // 上から見下ろす視点だと反射方向が真下(=水中)を向いてしまい
                // Skybox(空)が映らないので、reflDir を上半球に折り返す。
                // これで真上から見ても水面に空(HDRI)が映るようになる。
                if (reflDir.y < 0.0)
                    reflDir.y = -reflDir.y;

                half3 reflProbe = GlossyEnvironmentReflection(reflDir, IN.positionWS, 1 - _Smoothness, 1.0);

                float3 H = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, H));
                float roughness = max(1.0 - _Smoothness, 0.02);
                float a2 = roughness * roughness;
                float d  = (NdotH * NdotH) * (a2 - 1.0) + 1.0;
                float specGGX = a2 / (3.14159 * d * d);
                float3 specular = specGGX * _SunColor.rgb * mainLight.color * _SpecularStrength;

                // ===== 合成 =====
                float3 col = lerp(throughWater, reflProbe, fresnel * _ReflectionStrength);
                col += specular;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                    float shadow = MainLightRealtimeShadow(shadowCoord);
                    col *= lerp(0.7, 1.0, shadow);
                #endif

                col = MixFog(col, IN.fogCoord);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
