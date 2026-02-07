Shader "Unlit/WaterRippleSimulation"
{
    Properties {
        _MainTex ("Prev RT", 2D) = "black" {}
        _PrevTex ("2 Frames Ago RT", 2D) = "black" {}
        _Damping ("Damping", Range(0.1, 1.0)) = 0.96
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _PrevTex;
            float4 _MainTex_TexelSize;
            float _Damping;

            fixed4 frag (v2f_img i) : SV_Target {
                float2 off = _MainTex_TexelSize.xy;
                float up    = tex2D(_MainTex, i.uv + float2(0, off.y)).r;
                float down  = tex2D(_MainTex, i.uv - float2(0, off.y)).r;
                float left  = tex2D(_MainTex, i.uv - float2(off.x, 0)).r;
                float right = tex2D(_MainTex, i.uv + float2(off.x, 0)).r;

                float prev = tex2D(_PrevTex, i.uv).r;
                
                // 物理的な波の広がりを計算。ここで上下左右の平均をとる
                float ripple = (up + down + left + right) * 0.5 - prev;
                
                return saturate(ripple * _Damping - 0.001);
            }
            ENDCG
        }
    }
}