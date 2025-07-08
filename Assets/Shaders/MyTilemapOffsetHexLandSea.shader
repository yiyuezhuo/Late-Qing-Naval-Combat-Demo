Shader "Unlit/MyTilemapOffsetHexLandSea"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        _LandSeaTex ("Land Sea Texture", 2D) = "white" {}
        _TerrainTypeTex ("Terrain Type Texture", 2D) = "white" {}
        _TerrainTexArray ("Terrain Texture Array", 2DArray) = "white" {}

        _Width ("Width", Float) = 1
        _Height ("Height", Float) = 1
        [Toggle] _Border ("Border", Float) = 1
        _BorderPercent ("BorderPercent", Float) = 0.025
        _BorderColor ("BorderColor", Color) = (1,0,0,1)

        _WaterBeginIndex ("WaterBeginIndex", Float) = 15
        _TerrainTiling ("TerrainTiling", Float) = 1
        
        [Toggle] _ShowReferenceTexture ("Show Reference Texture", Float) = 0
        _ReferenceTexture ("Reference Texture", 2D) = "white" {}

        [Toggle] _AccurateSeaLand ("Accurate Sea Land", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
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


            sampler2D _LandSeaTex;
            sampler2D _TerrainTypeTex;
            UNITY_DECLARE_TEX2DARRAY(_TerrainTexArray);

            float _Width;
            float _Height;
            float _Border;
            float _BorderPercent;
            float4 _BorderColor;

            float _WaterBeginIndex;
            float _TerrainTiling;
            
            float _ShowReferenceTexture;
            sampler2D _ReferenceTexture;

            float _AccurateSeaLand;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float2 to_center(float2 xy)
            {
                if(fmod(xy.x, 2) >= 1)
                {
                    return floor(xy) + float2(0.5, 1.0);
                }
                else
                {
                    return floor(xy) + float2(0.5, 0.5);
                }
            }

            float2 from_center(float2 xy)
            {
                if(fmod(xy.x, 2) >= 1)
                {
                    return round(xy - float2(0.5, 1.0));
                }
                else
                {
                    return round(xy - float2(0.5, 0.5));
                }
            }

            float2 get_index(float2 uv)
            {
                float2 xy = uv * float2(_Width, _Height);
                // float2 xy_floor = floor(xy);
                // float2 xy_center = to_center(xy);

                float min_distance = 9999.0;
                float2 min_center = float2(0, 0);

                for(int dx=-1; dx<=1; dx++)
                {
                    for(int dy=-1; dy<=1; dy++)
                    {
                        float2 test_center = to_center(xy + float2(dx, dy));
                        float2 test_diff = test_center - xy;
                        test_diff = test_diff * test_diff;
                        float distance2 = test_diff.x + test_diff.y;
                        if(distance2 < min_distance)
                        {
                            min_distance = distance2;
                            min_center = test_center;
                        }
                    }
                }

                float2 index = from_center(min_center) / float2(_Width, _Height);
                // float2 index = min_center / float2(_Width, _Height);
                return index;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // https://discussions.unity.com/t/2darray-texture2darray-in-shaders/778990/2

                float2 index = get_index(i.uv);

                if(_Border)
                {
                    float dx = 1. / _Width * _BorderPercent;
                    float dy = 1. / _Height * _BorderPercent;

                    float2 index_dx = get_index(i.uv + float2(dx, 0));
                    float2 index_dy = get_index(i.uv + float2(0, dy));
                    if(index_dx.x != index.x || index_dx.y != index.y || index_dy.x != index.x || index_dy.y != index.y)
                    {
                        return _BorderColor;
                    }
                }

                if(_ShowReferenceTexture)
                {
                    return tex2D(_ReferenceTexture, i.uv);
                }

                // float2 xy_hex = from_center(min_center);

                float4 terrainIndexColor = tex2D(_TerrainTypeTex, index);
                int terrainIndex = terrainIndexColor.r * 255;
                
                if(_AccurateSeaLand)
                {
                    float is_sea = tex2D(_LandSeaTex, i.uv).b > 0.5;
                    if(is_sea && terrainIndex < _WaterBeginIndex)
                    {
                        terrainIndex = _WaterBeginIndex;
                    }
                    else if(!is_sea && terrainIndex >= _WaterBeginIndex)
                    {
                        terrainIndex = 0;
                    }
                }
                
                float2 uv_array = fmod(i.uv * _TerrainTiling, 1);
                float4 ret = UNITY_SAMPLE_TEX2DARRAY(_TerrainTexArray, float3(uv_array, terrainIndex));
                if(terrainIndex == 15)
                {
                    ret = ret * 2; // temp hack to enhance shallow water to be more visible
                }
                // ret = sqrt(ret + float4(0.1, 0.1, 0.1, 0));
                return ret;
                // return terrainIndexColor * 25;

                // sample the texture
                // fixed4 col = tex2D(_MainTex, i.uv);
                // // apply fog
                // UNITY_APPLY_FOG(i.fogCoord, col);
                // return col;
            }
            ENDCG
        }
    }
}
