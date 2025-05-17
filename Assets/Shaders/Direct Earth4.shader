Shader "Unlit/Direct Earth4"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        // _MainTex ("Albedo", 2D) = "white" {}
        _HeightTex ("Height Texture", 2D) = "white" {}
        _FrozenLevel ("Frozen Level", Range(0, 1)) = 0.0
        _MinExtent ("Min Extent", Range(0, 90)) = 70.0 // North Latitude 70 deg ~ North Latitude 90 Deg
        _MaxExtent ("Max Extent", Range(0, 90)) = 50.0 // North Latitude 60 deg ~ North Latitude 90 Deg
        _CaveLow ("Cave Low", Range(-180, 180)) = -57
        _CaveHigh ("Cave High", Range(-180, 180)) = 53
        _CaveCoef ("Cave Coef", Range(0, 1)) = 0.5
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
            float _FrozenLevel;
            float _MinExtent;
            float _MaxExtent;
            float _CaveLow;
            float _CaveHigh;
            float _CaveCoef;

            float getSeaIce(float lat, float lon)
            {
                float latThreshold = lerp(_MinExtent, _MaxExtent, _FrozenLevel);

                float coef = step(_CaveLow, lon) * step(lon, _CaveHigh) * sin((lon - _CaveLow) / (_CaveHigh - _CaveLow) * 3.1415) * _CaveCoef;
                latThreshold = 90 - (90 - latThreshold) * (1 - coef);

                float s = (lat - latThreshold) / ( 90 - latThreshold);
                float s2 = max(0, min(1, s * 2));
                return s2;
            }

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
                float4 col = tex2D(_MainTex, texCoord);

                // Frozen Blend

                // TODO: Handle Height

                float2 longLatDeg = longLatRad * 180 / PI;

                float h = tex2D(_HeightTex, texCoord); // height (m) / 65535 (u16 processing)
                float heightMask = 1 - sign(h);

                float seaIce = getSeaIce(longLatDeg.y, longLatDeg.x);
                float seaIceSouth = getSeaIce(longLatDeg.y - 0.02, longLatDeg.x);

                if(seaIce == 1 && seaIceSouth < 1)
                    // return fixed4(1.0, 1.0, 1.0, 1);
                    return fixed4(0.0, 1.0, 0.0, 1);

                float s2 = seaIce / 2 * heightMask;
                
                // float latThreshold = lerp(_MinExtent, _MaxExtent, _FrozenLevel);
                // float s = (longLatDeg.y - latThreshold) / ( 90 - latThreshold);
                // float s2 = max(0, min(1, s * 2)) / 2 * heightMask;
                col = lerp(col, fixed4(1.0, 1.0, 1.0, 1), s2);
                
                // float t = tex2D(_HeightTex, texCoord) * 100;
                // col = fixed4(t, t, t, 1);

                // sample the texture
                // fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                // UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    // Fallback "VertexLit"
}
