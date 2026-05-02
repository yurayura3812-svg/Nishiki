Shader "KoiPond/RippleSim"
{
    // GPU wave-equation propagator. Used as a Blit material on a RenderTexture pair (ping-pong).
    // Channels:
    //   R  = current height
    //   G  = previous height
    //   BA = packed gradient (0..1, decoded as (BA*2-1)) for fast normal lookup
    //
    // Standalone shader - no SRP / Common.hlsl dependency, runs on any URP version.
    Properties
    {
        _MainTex("Previous State", 2D) = "black" {}
        _Damping("Damping", Range(0.9, 0.999)) = 0.985
        _Speed("Propagation Speed", Range(0.1, 0.5)) = 0.35
        _TexelSize("Texel Size (1/res)", Float) = 0.001953125 // 1/512
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        // ========================================================
        // Pass 0: Propagate wave equation
        // ========================================================
        Pass
        {
            Name "Propagate"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Damping;
            float _Speed;
            float _TexelSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float dx = _TexelSize;

                float4 c  = tex2D(_MainTex, uv);
                float  cu = tex2D(_MainTex, uv + float2(0,  dx)).r;
                float  cd = tex2D(_MainTex, uv + float2(0, -dx)).r;
                float  cl = tex2D(_MainTex, uv + float2(-dx, 0)).r;
                float  cr = tex2D(_MainTex, uv + float2( dx, 0)).r;

                // 離散波動方程式: h_new = 2*h - h_prev + c^2 * laplacian(h)
                float lap = (cu + cd + cl + cr) * 0.5 - c.r * 2.0;
                float prev = c.g;
                float curr = c.r;

                float next = (curr * 2.0 - prev) + lap * _Speed;
                next *= _Damping;

                // 法線計算用の勾配
                float gx = (cr - cl) * 0.5;
                float gz = (cu - cd) * 0.5;

                // -1..1 -> 0..1
                float2 grad = float2(gx, gz) * 0.5 + 0.5;

                return float4(next, curr, grad.x, grad.y);
            }
            ENDCG
        }

        // ========================================================
        // Pass 1: Add impulses (additive into R channel)
        // ========================================================
        Pass
        {
            Name "AddImpulse"
            Blend One One
            ColorMask R

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _ImpulsePos;    // xy = uv 0..1, z = radius (uv space), w = strength
            float4 _ImpulsePos1;
            float4 _ImpulsePos2;
            float4 _ImpulsePos3;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Splat(float2 uv, float4 p)
            {
                if (p.w == 0) return 0;
                float d = distance(uv, p.xy);
                float falloff = 1.0 - smoothstep(0, p.z, d);
                return falloff * falloff * p.w;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float h = 0;
                h += Splat(uv, _ImpulsePos);
                h += Splat(uv, _ImpulsePos1);
                h += Splat(uv, _ImpulsePos2);
                h += Splat(uv, _ImpulsePos3);
                return float4(h, 0, 0, 0);
            }
            ENDCG
        }
    }
    FallBack Off
}
