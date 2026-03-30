Shader "Unlit/Earth0_RTW_like2"
{
    Properties
    {
        _HeightTex ("Height Texture", 2D) = "white" {}
        _HeightTexROI ("Height Texture ROI", 2D) = "white" {}
        _ShoreFieldTexROI ("Shore Field Texture ROI", 2D) = "black" {}
        _ROILatDeg0 ("ROI Latitude Deg 0", Float) = 15 // 30
        _ROILatDeg1 ("ROI Latitude Deg 1", Float) = 55 // 41
        _ROILonDeg0 ("ROI Longitude Deg 0", Float) = 105 // 116
        _ROILonDeg1 ("ROI Longitude Deg 1", Float) = 146 // 131
        _LandColor ("Land Color", Color) = (0, 1, 0, 1)
        _SeaColor ("Sea Color", Color) = (0, 0, 1, 1)

        _LandColorDark ("Land Color (Dark)", Color) = (0.0588, 0.1568, 0.1843)
        _SeaColorDark ("Sea Color (Dark)", Color) = (0.0235, 0.0274, 0.0431, 1)

        _SeaTex ("Sea Texture", 2D) = "white" {}
        _SeaTexScale ("Sea Texture Scale", Float) = 1

        [Toggle] _UseROI ("Use ROI", Float) = 1
        [Toggle] _UseDark ("Use Dark", Float) = 0
        [Toggle] _UseSeaTex ("Use Sea Texture", Float) = 1
        [Toggle] _ShowShoreDistance ("Show Shore Distance", Float) = 0
        [Toggle] _ShowShoreGradient ("Show Shore Gradient", Float) = 0
        [Toggle] _UseSunLight ("Use Sun Light", Float) = 1
        _SunDirObj ("Sun Direction (Object Space)", Vector) = (0, 1, 0, 0)
        _NightBrightness ("Night Brightness", Range(0,1)) = 0.35
        _TerminatorSoftness ("Terminator Softness", Range(0,1)) = 0.08
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
            sampler2D _ShoreFieldTexROI;

            float _ROILatDeg0;
            float _ROILatDeg1;
            float _ROILonDeg0;
            float _ROILonDeg1;

            float4 _LandColor;
            float4 _SeaColor;

            float4 _LandColorDark;
            float4 _SeaColorDark;

            sampler2D _SeaTex;
            float _SeaTexScale;

            float _UseROI;
            float _UseDark;
            float _UseSeaTex;
            float _ShowShoreDistance;
            float _ShowShoreGradient;
            float _UseSunLight;
            float4 _SunDirObj;
            float _NightBrightness;
            float _TerminatorSoftness;



            float4 getColorLight(float h) // JTS-like
            {
                return h > 0 ? (1-sqrt(h)*3.5) * _LandColor : _SeaColor;
            }

            float4 getColorDark(float h) // Google Map dark theme like
            {
                return h > 0 ? (1-sqrt(h)*3.5) * _LandColorDark : _SeaColorDark;
            }

            float4 getLandColor(float h)
            {
                return (_UseDark ? _LandColorDark : _LandColor) * (1-sqrt(h)*3.5);
            }

            float4 getSeaColor(float h, float2 longLatDeg)
            {
                if(_UseSeaTex)
                {
                    // return tex2D(_SeaTex, longLatDeg * _SeaTexScale);
                    return tex2D(_SeaTex, longLatDeg * _SeaTexScale) * 2; // x2 is the temp hack to enhance shallow water to be more visible
                }
                return _UseDark ? _SeaColorDark : _SeaColor;
            }

            float4 getColor(float h, float2 longLatDeg)
            {
                // return _UseDark ? getColorDark(h) : getColorLight(h);
                return h > 0 ? getLandColor(h) : getSeaColor(h, longLatDeg);
            }

            float3 applyShoreFieldOverlay(float3 baseRgb, float2 texCoord)
            {
                float overlayCount = 0;
                float3 overlayColor = 0;
                float4 shore = tex2D(_ShoreFieldTexROI, texCoord);

                if(_ShowShoreDistance > 0.5)
                {
                    // float distance01 = saturate(shore.r);
                    float distance01 = 20 * shore.r;
                    float nearShore = 1 - distance01;
                    // float3 distanceColor = lerp(float3(0.05, 0.2, 0.8), float3(1.0, 0.35, 0.0), nearShore);
                    float3 distanceColor = lerp(float3(0.0, 0.0, 0.0), float3(1.0, 1, 1), nearShore);
                    overlayColor += distanceColor;
                    overlayCount += 1;
                }

                if(_ShowShoreGradient > 0.5)
                {
                    overlayColor += float3(shore.g, shore.b, 0.5);
                    overlayCount += 1;
                }

                if(overlayCount <= 0)
                {
                    return baseRgb;
                }

                return lerp(baseRgb, overlayColor / overlayCount, 0.85);
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
                float2 longLatDeg = longLatRad * 180 / PI;
                
                // float h;
                float4 col;
                // if(longLatDeg.x > _ROILatDeg0 && longLatDeg.x < _ROILatDeg1 && longLatDeg.y > _ROILonDeg0 && longLatDeg.y < _ROILonDeg1)
                bool inROI = _UseROI && longLatDeg.x > _ROILonDeg0 && longLatDeg.x < _ROILonDeg1 && longLatDeg.y > _ROILatDeg0 && longLatDeg.y < _ROILatDeg1;
                bool showShoreOverlay = _ShowShoreDistance > 0.5 || _ShowShoreGradient > 0.5;
                if(inROI)
                {
                    float longitudeDeg = longLatDeg[0]; // range [-PI, PI]
                    float latitudeDeg = longLatDeg[1]; // range [-PI/2, PI/2]
                    
                    float u = (longitudeDeg - _ROILonDeg0) / (_ROILonDeg1 - _ROILonDeg0);
                    float v = (latitudeDeg - _ROILatDeg0) / (_ROILatDeg1 - _ROILatDeg0);
                    
                    float2 texCoord = float2(u, v);
                    float h = tex2D(_HeightTexROI, texCoord);
                    col = getColor(h, longLatDeg);
                    if(showShoreOverlay)
                    {
                        col.rgb = applyShoreFieldOverlay(col.rgb, texCoord);
                    }
                }
                else
                {
                    float2 texCoord = longitudeLatitudeToUV(longLatRad);
                    float h = tex2D(_HeightTex, texCoord);
                    col = getColor(h, longLatDeg);
                }

                if(_UseSunLight)
                {
                    float3 sunDirObj = normalize(_SunDirObj.xyz);
                    float ndotl = dot(spherePos, sunDirObj);
                    float dayFactor = smoothstep(-_TerminatorSoftness, _TerminatorSoftness, ndotl);
                    float lightFactor = lerp(_NightBrightness, 1.0, dayFactor);
                    col.rgb *= lightFactor;
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
