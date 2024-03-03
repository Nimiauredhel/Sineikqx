Shader "WeirdQix/GameRender"
{
    Properties
    {
        _GameStateMap ("Game State Map", 2D) = "white" {}
        _CellColorRamp("Cell Color Ramp", 2D) = "white" {}
        _PlayerColor ("Player Color", Color) = (1,1,1,1)
        _GridColor ("Grid Color", Color) = (1,1,1,1)
        _GridSize ("Grid Size", int) = 64
        
        _PlayerPosition ("Player Position", Vector) = (0.5, 0.5, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

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

            sampler2D _GameStateMap;
            float4 _GameStateMap_ST;
            sampler2D _CellColorRamp;
            float4 _CellColorRamp_ST;
            
            float4 _PlayerColor;
            float4 _PlayerPosition;

            float4 _GridColor;
            int _GridSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _GameStateMap);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;

                //Draw Cell
                float2 coord = i.uv;
                float2 gridCell = float2(floor(coord.x * _GridSize), floor(coord.y * _GridSize));
                float4 cellValue = tex2D(_GameStateMap, gridCell/_GridSize);
                col = tex2D(_CellColorRamp, float2(cellValue.r, cellValue.r));
                
                // Draw Grid
                fixed xDistFromGrid = frac(i.uv.x * _GridSize);
                fixed yDistFromGrid = frac(i.uv.y * _GridSize);
                col = lerp(col, _GridColor, _GridColor.a * step(xDistFromGrid, 0.2));
                col = lerp(col, _GridColor, _GridColor.a * step(yDistFromGrid, 0.2));

                // Draw Player
                fixed xDistFromPlayer = distance(i.uv.x, _PlayerPosition.x);
                fixed yDistFromPlayer = distance(i.uv.y, _PlayerPosition.y);
                fixed combinedDistFromPlayer = xDistFromPlayer + yDistFromPlayer;
                fixed lightDistanceFromPlayer = lerp(max(xDistFromPlayer, yDistFromPlayer), min(xDistFromPlayer, yDistFromPlayer), 0.75);
                col = lerp(col, _PlayerColor, max(0.0, _PlayerPosition.z)/lightDistanceFromPlayer);
                //col = lerp(col, _PlayerColor, step(combinedDistFromPlayer, _PlayerPosition.w));
                col = lerp(col, float4(0,0,0,0), combinedDistFromPlayer*0.001);
                
                return col;
            }
            ENDCG
        }
    }
}
