Shader "Unlit/DrawWaterHeight"
{
    Properties
    {
        _MainTex ("Previous RT", 2D) = "black" {}
        _DrawPos ("Draw Position", Vector) = (-1,-1,0,0)
        _BrushSize ("Brush Size", Float) = 0.05
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _DrawPos;
            float _BrushSize;

            fixed4 frag (v2f_img i) : SV_Target
            {
                // UVが0-1の範囲外なら描かない（シマシマ対策）
                if (i.uv.x < 0 || i.uv.x > 1 || i.uv.y < 0 || i.uv.y > 1) return 0;

                float4 col = tex2D(_MainTex, i.uv);
                float d = distance(i.uv, _DrawPos.xy);
                float draw = smoothstep(_BrushSize, _BrushSize * 0.5, d);
                
                // 範囲外の変な座標(-1など)が送られてきたら無視
                if (_DrawPos.x < 0 || _DrawPos.x > 1) draw = 0;

                return saturate(col + draw * 0.1) * 0.98;
            }
            ENDCG
        }
    }
}