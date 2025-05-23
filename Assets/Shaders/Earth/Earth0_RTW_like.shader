Shader "Unlit/Earth0_RTW_like"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        // _MainTex ("Albedo", 2D) = "white" {}
        _HeightTex ("Height Texture", 2D) = "white" {}
        _LandColor ("Land Color", Color) = (0, 1, 0, 1)
        _SeaColor ("Sea Color", Color) = (0, 0, 1, 1)
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

            sampler2D _MainTex;
            // float4 _MainTex_ST;

            sampler2D _HeightTex;

            float4 _LandColor;
            float4 _SeaColor;

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
                float2 texCoord = longitudeLatitudeToUV(longLatRad);

                // base landscape
                // float4 col = tex2D(_MainTex, texCoord);
                float h = tex2D(_HeightTex, texCoord);
                float4 col = h > 0 ? _LandColor : _SeaColor;
                // float4 col = float4(h,h,h,1) * 60;

                return col;
            }
            ENDCG
        }
    }
    // Fallback "VertexLit"
}
