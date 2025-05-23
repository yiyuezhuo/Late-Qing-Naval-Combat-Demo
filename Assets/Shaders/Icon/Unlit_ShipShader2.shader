Shader "Unlit/Unlit_ShipShader2"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _BorderColor ("Border Color", Color) = (1,0,0,1)  //
        _BorderSize ("Border Size", Range(0.01, 0.1)) = 0.05
        _CornerSize ("Corner Size", Range(0.1, 0.3)) = 0.2
        _ShowBorder ("Show Border", Float) = 0 // 0 = Off, 1 = Half, 2 - Full 
    }
    SubShader
    {
        // Tags { "RenderType"="Opaque" }
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha  
            ZWrite Off  
            Cull Off   

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            fixed4 _MainColor;
            fixed4 _BorderColor;
            float _BorderSize;
            float _CornerSize;
            float _ShowBorder;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * _MainColor;

                float2 border = step(i.uv, _BorderSize) + step(1.0 - _BorderSize, i.uv);
                float isBorder = max(border.x, border.y);

                // float isCorner = min(border.x, border.y);

                float2 border2 = step(i.uv, _CornerSize) + step(1.0 - _CornerSize, i.uv);
                float isCorner = min(isBorder, min(border2.x, border2.y));
                
                float showFullBorder = max(0, _ShowBorder - 1);
                float showCorder = min(1, _ShowBorder);

                col = lerp(col, _BorderColor, showFullBorder * isBorder);
                col = lerp(col, _BorderColor, showCorder * isCorner);

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
