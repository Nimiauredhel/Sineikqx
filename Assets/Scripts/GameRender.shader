Shader "WeirdQix/GameRender"
{
    Properties
    {
        _GameStateMap ("Game State Map", 2D) = "white" {}
        _CellColorRamp("Cell Color Ramp", 2D) = "white" {}
        _PlayerColor ("Player Color", Color) = (1,1,1,1)
        _BorderColor ("Border Color", Color) = (1,1,1,1)
        _BorderThickness("Border Thickness", float) = 0.2
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

            float4 _BorderColor;
            float _BorderThickness;
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

            float neighbourCellValue(float cellValue, float2 coord)
            {
                float testDist = 0.002;
                float nValue = -1.0;
                float tempValue = -1.0;
                float difference = 0.0;

                //right
                tempValue = tex2D(_GameStateMap, float2(coord.x+testDist, coord.y)).a;
                difference = abs(cellValue-tempValue);
                nValue = max(nValue, difference);
                //up
                tempValue = tex2D(_GameStateMap, float2(coord.x, coord.y+testDist)).a;
                difference = abs(cellValue-tempValue);
                nValue = max(nValue, difference);
                //left
                tempValue = tex2D(_GameStateMap, float2(coord.x-testDist, coord.y)).a;
                difference = abs(cellValue-tempValue);
                nValue = max(nValue, difference);
                //down
                tempValue = tex2D(_GameStateMap, float2(coord.x, coord.y-testDist)).a;
                difference = abs(cellValue-tempValue);
                nValue = max(nValue, difference);

                return nValue;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;
                float2 coord = i.uv;

                // Determine Cell Type
                float2 gridCoord = float2(coord.x * _GridSize, coord.y * _GridSize);
                float2 gridCell = floor(gridCoord);
                float cellValue = tex2D(_GameStateMap, coord).a;
                col = tex2D(_CellColorRamp, float2(cellValue, cellValue));
                _SunDirection.xy = length(_PlayerPosition * 2.0 - 1.0) * _SunDirection.w;
                float aberration = 1.0-cos(_Time.x)*0.1;

                // Draw Waves
                float2 ditherValue = gridCell * (1.0 - cellValue) * 0.75;
                float globalWaveFluctuation = (_CosTime.w * 2.0 - 1.0) * 0.00001;
                float2 wave = fractalSinWave(1.0 + globalWaveFluctuation, aberration*42.265*coord+ditherValue, _Time.y, 1.0 + globalWaveFluctuation, 32, 1.163, 0.84).y;
                wave.y *= step(cellValue, 0.25);
                col = lerp(col, col*2.0, wave.y);
                
                // Draw Borders
                float2 cellFrac = frac(gridCoord-0.5);
                float2 distFromGrid = abs(cellFrac * 2.0 - 1.0);
                float nCellValue = neighbourCellValue(cellValue, coord);
                _BorderColor.a *= step(0.1, nCellValue);
                col = lerp(col, _BorderColor, _BorderColor.a * (1.0 - smoothstep(distFromGrid.x, 0.0, _BorderThickness)));
                col = lerp(col, _BorderColor, _BorderColor.a * (1.0 - smoothstep(distFromGrid.y, 0.0, _BorderThickness)));

                // Draw Bounds
                float boundCoordNoise = 0.005*noise(i.uv*50.0+_Time.w*2.3)*noise(i.uv*12.3+_SinTime.z*5.2);
                float2 boundCoord = i.uv - boundCoordNoise;
                float boundThickness = 0.0025;
                _BorderColor = float4(0.22, 0.22, 0.0, 1.0);
                _BorderColor.a *= (step(boundCoord.x, 0.0) * step(0.0, boundCoord.x+boundThickness)) + (step(boundCoord.x-boundThickness, 1.0) * step(1.0, boundCoord.x))
                + (step(boundCoord.y, 0.0) * step(0.0, boundCoord.y+boundThickness)) + (step(boundCoord.y-boundThickness, 1.0) * step(1.0, boundCoord.y));
                col = lerp(col, _BorderColor, _BorderColor.a * (1.0 - smoothstep(distFromGrid.x, 0.0, _BorderThickness)));
                col = lerp(col, _BorderColor, _BorderColor.a * (1.0 - smoothstep(distFromGrid.y, 0.0, _BorderThickness)));

                // Draw Player
                fixed xDistFromPlayer = distance(i.uv.x, _PlayerPosition.x);
                fixed yDistFromPlayer = distance(i.uv.y, _PlayerPosition.y);
                fixed combinedDistFromPlayer = xDistFromPlayer + yDistFromPlayer;
                fixed lightDistanceFromPlayer = lerp(max(xDistFromPlayer, yDistFromPlayer), min(xDistFromPlayer, yDistFromPlayer), 0.75);
                col = lerp(col, _PlayerColor, max(0.0, _PlayerPosition.z)/lightDistanceFromPlayer);
                col = lerp(col, _PlayerColor, step(combinedDistFromPlayer, _PlayerPosition.w));
                col = lerp(col, float4(0,0,0,0), combinedDistFromPlayer*0.4);

                // Draw Noise Clouds
                float blurDistance = 0.2;
                float blurAmount = 0.2;
                float cloudModifier = 0.2;
                float2 cloudCoord = coord * 5.0 + _Time.x;
                float cloudAmount = noise(cloudCoord);
                cloudAmount = lerp(cloudAmount, noise(float2(cloudCoord.x+blurDistance, cloudCoord.y)), blurAmount);
                cloudAmount = lerp(cloudAmount, noise(float2(cloudCoord.x, cloudCoord.y+blurDistance)), blurAmount);
                cloudAmount = lerp(cloudAmount, noise(float2(cloudCoord.x-blurDistance, cloudCoord.y)), blurAmount);
                cloudAmount = lerp(cloudAmount, noise(float2(cloudCoord.x, cloudCoord.y-blurDistance)), blurAmount);
                cloudAmount *= step(0.5, cellValue);
                col = lerp(col, float4(1.0,1.0,1.0,1.0), cloudAmount*cloudModifier);
                
                return col;
            }
            ENDCG
        }
    }
}
