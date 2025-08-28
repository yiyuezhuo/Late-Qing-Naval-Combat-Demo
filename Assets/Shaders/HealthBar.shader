// LLM Generated

Shader "Custom/HealthBar"
{
    Properties
    {
        _FillAmount ("Fill Amount", Range(0,1)) = 1.0
        _BackgroundColor ("Background Color", Color) = (0.3,0.3,0.3,1)
        _HealthColor ("Health Color", Color) = (0,1,0,1)
        _LowHealthColor ("Low Health Color", Color) = (1,0,0,1)
        _BorderColor ("Border Color", Color) = (0,0,0,1)
        _BorderWidth ("Border Width", Range(0,0.1)) = 0.02
    }
    
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            float _FillAmount;
            float4 _BackgroundColor;
            float4 _HealthColor;
            float4 _LowHealthColor;
            float4 _BorderColor;
            float _BorderWidth;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Border Detection
                if (i.uv.x < _BorderWidth || i.uv.x > 1 - _BorderWidth || 
                    i.uv.y < _BorderWidth || i.uv.y > 1 - _BorderWidth)
                {
                    return _BorderColor;
                }
                
                // Health Bar Fill
                if (i.uv.x < _FillAmount)
                {
                    float4 healthColor = lerp(_LowHealthColor, _HealthColor, _FillAmount);
                    return healthColor;
                }
                else
                {
                    return _BackgroundColor;
                }
            }
            ENDCG
        }
    }
}