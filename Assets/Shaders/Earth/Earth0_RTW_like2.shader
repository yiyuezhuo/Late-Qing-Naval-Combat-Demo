Shader "Unlit/Earth0_RTW_like2"
{
    Properties
    {
        _HeightTex ("Height Texture", 2D) = "white" {}
        _HeightTexROI ("Height Texture ROI", 2D) = "white" {}
        _ROILatDeg0 ("ROI Latitude Deg 0", Float) = 15 // 30
        _ROILatDeg1 ("ROI Latitude Deg 1", Float) = 55 // 41
        _ROILonDeg0 ("ROI Longitude Deg 0", Float) = 105 // 116
        _ROILonDeg1 ("ROI Longitude Deg 1", Float) = 146 // 131
        _LandColor ("Land Color", Color) = (0, 1, 0, 1)
        _SeaColor ("Sea Color", Color) = (0, 0, 1, 1)
        [Toggle] _UseROI ("Use ROI", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        // Tags { "Queue"="Background" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            // #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Assets/Shaders/Shader Common/min_lib.hlsl"
            // #include "Assets/Scripts/Shader Common/GeoMath.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                // float2 uv : TEXCOORD0;
                // float3 normal : NORMAL;
            };

            struct v2f
            {
                // float2 uv : TEXCOORD0;
                // UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 objPos : TEXCOORD0;
                // float3 worldNormal : NORMAL;
            };

            // sampler2D _MainTex;
            // float4 _MainTex_ST;

            sampler2D _HeightTex;
            sampler2D _HeightTexROI;

            float _ROILatDeg0;
            float _ROILatDeg1;
            float _ROILonDeg0;
            float _ROILonDeg1;

            float4 _LandColor;
            float4 _SeaColor;

            float _UseROI;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // o.worldNormal = UnityObjectToWorldNormal(v.normal);

                // o.objPos = o.vertex;
                o.objPos = v.vertex;

                // o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 spherePos = normalize(i.objPos);
                
                // float2 texCoord = pointToUV(spherePos);
                float2 longLatRad = pointToLongitudeLatitude(spherePos);
                float2 longLatDeg = longLatRad * 180 / PI;
                
                // float h;
                float4 col;
                // if(longLatDeg.x > _ROILatDeg0 && longLatDeg.x < _ROILatDeg1 && longLatDeg.y > _ROILonDeg0 && longLatDeg.y < _ROILonDeg1)
                if(_UseROI && longLatDeg.x > _ROILonDeg0 && longLatDeg.x < _ROILonDeg1 && longLatDeg.y > _ROILatDeg0 && longLatDeg.y < _ROILatDeg1)
                {
                    float longitudeDeg = longLatDeg[0]; // range [-PI, PI]
                    float latitudeDeg = longLatDeg[1]; // range [-PI/2, PI/2]
                    
                    float u = (longitudeDeg - _ROILonDeg0) / (_ROILonDeg1 - _ROILonDeg0);
                    float v = (latitudeDeg - _ROILatDeg0) / (_ROILatDeg1 - _ROILatDeg0);
                    float2 texCoord = float2(u, v);
                    float h = tex2D(_HeightTexROI, texCoord);
                    col = h > 0 ? _LandColor : _SeaColor;
                    // col = float4(0,1,0,1);
                }
                else
                {
                    float2 texCoord = longitudeLatitudeToUV(longLatRad);
                    // col = float4(1,0,0,1);
                    float h = tex2D(_HeightTex, texCoord);
                    col = h > 0 ? _LandColor : _SeaColor;
                    // h = 0;
                }

                // float h = tex2D(_HeightTex, texCoord);
                // float4 col = h > 0 ? _LandColor : _SeaColor;

                return col;
            }
            ENDCG
        }
    }
    // Fallback "VertexLit"
}
