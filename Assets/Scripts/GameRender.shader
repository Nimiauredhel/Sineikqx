Shader "WeirdQix/GameRender"
{
    Properties
    {
        _GameStateMap ("Game State Map", 2D) = "white" {}
        _CellColorRamp("Cell Color Ramp", 2D) = "white" {}
        _PlayerColor ("Player Color", Color) = (1,1,1,1)
        _GridColor ("Grid Color", Color) = (1,1,1,1)
        _GridSize ("Grid Size", int) = 64
        _SunDirection("Sun Direction", vector) = (1,1,1,1)
        _SunIntensity("Sun Intensity", float) = 1.0
        
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

            float4 _SunDirection;
            float _SunIntensity;
            
            sampler2D _GameStateMap;
            float4 _GameStateMap_ST;
            sampler2D _CellColorRamp;
            float4 _CellColorRamp_ST;
            
            float4 _PlayerColor;
            float4 _PlayerPosition;

            float4 _GridColor;
            int _GridSize;

            float random (float2 st)
            {
                return frac(sin(dot(st.xy,
                float2(12.9898,78.233)))*
                43758.5453123);
            }

            // 2D Noise based on Morgan McGuire @morgan3d
            // https://www.shadertoy.com/view/4dS3Wd
            float noise (in float2 st)
            {
                float2 i = floor(st);
                float2 f = frac(st);

                // Four corners in 2D of a tile
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));

                // Smooth Interpolation

                // Cubic Hermine Curve.  Same as SmoothStep()
                float2 u = f*f*(3.0-2.0*f);
                // u = smoothstep(0.,1.,f);

                // Mix 4 coorners percentages
                return lerp(a, b, u.x) +
                (c - a)* u.y * (1.0 - u.x) +
                (d - b) * u.x * u.y;
            }
            
            float2 sinWaveDiffuse(float cosOfSin, float2 direction)
            {
                float2 a = cosOfSin * direction;
                float4 tangent = float4(1.0, 0.0, a);
                float4 binormal = float4(0.0, 1.0, a);
                float3 normal = cross(tangent, binormal);
                float diffuse = dot(normal, _SunDirection);
                
                return diffuse * _SunIntensity;
            }
            
            float sinWave(float amplitude, float2 position, float2 direction, float time, float frequency)
            {
                return amplitude * sin(dot(direction, position)*(2/frequency)+time*frequency);
            }

            float cosWave(float amplitude, float2 position, float2 direction, float time, float frequency)
            {
                return amplitude * cos(dot(direction, position)*(2/frequency)+time*frequency);
            }

            float2 fractalSinWave(float amplitude, float2 position, float time, float frequency, int octaves, float lacunarity, float gain)
            {
                float euler = 2.71828;
                float2 heightAndDiffuse = float2(0.0, 0.0);
                float2 dirMod = float2(0.0, 0.0)
                + (sin(_Time.y*0.1) * 2.0 - 1.0) * 0.01
                + (cos(_Time.y*0.15) * 2.0 - 1.0) * 0.01;
                
                for(int i = 0; i < octaves; i++)
                {
                    float2 direction = float2(random(3456.324+i*3.765) * 2.0 - 1.0, random(7566.7634+i*7.342) * 2.0 - 1.0);
                    direction += dirMod * (1.0 + 0.1 * (i+1));
                    float height = sinWave(amplitude, position, direction, time, frequency);
                    height = pow(euler, height);
                    
                    float diffuse = sinWaveDiffuse(cos(height), direction);
                    heightAndDiffuse += float2(height, diffuse);
                    frequency *= lacunarity;
                    amplitude *= gain;
                }

                return clamp(0.0, 1.0, heightAndDiffuse);
            }

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

                _SunDirection.xy = length(_PlayerPosition * 2.0 - 1.0) * _SunDirection.w;
                float aberration = 1.0-cos(_Time.x)*0.1;
                coord *= aberration;
                float2 wave = fractalSinWave(1.0, aberration*42.265*coord+gridCell*0.75, _Time.y, 1.0, 32, 1.18, 0.82).y;
                
                wave.y *= step(cellValue, 0.25);
                col = lerp(col, col*2.0, wave.y);
                
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
                col = lerp(col, _PlayerColor, step(combinedDistFromPlayer, _PlayerPosition.w));
                col = lerp(col, float4(0,0,0,0), combinedDistFromPlayer*0.4);
                
                return col;
            }
            ENDCG
        }
    }
}
