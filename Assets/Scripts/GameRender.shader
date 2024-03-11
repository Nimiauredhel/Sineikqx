Shader "WeirdQix/GameRender"
{
    Properties
    {
        _GameStateMap ("Game State Map", 2D) = "white" {}
        _CellColorRamp("Cell Color Ramp", 2D) = "white" {}
        _PlayerColor ("Player Color", Color) = (1,1,1,1)
        _BorderColor ("Border Color", Color) = (1,1,1,1)
        _BossColor ("Boss Color", Color) = (1,1,1,1)
        _BorderThickness("Border Thickness", float) = 0.2
        _GridSize ("Grid Size", int) = 64
        _SunDirection("Sun Direction", vector) = (1,1,1,1)
        _SunIntensity("Sun Intensity", float) = 1.0
        
        _FillPercent("Fill Percent", Range(0.0, 1.0)) = 0.5
        
        _PlayerPosition ("Player Position", Vector) = (0.5, 0.5, 1, 1)
        _BossPosition ("Boss Position", Vector) = (0.75, 0.75, 1, 1)
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

            float4 _BossPosition;
            float4 _BossColor;

            float _FillPercent;
            
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
                float4 col;
                float2 coord = i.uv;
                float coordFrac = frac(coord);
                float coordFloor = floor(coord);

                // Determine Cell Type
                float outOfBoundsAmount = abs(coordFloor) * min(coordFrac, 1.0 - abs(coordFrac));
                float outOfBoundsDistortion = (sign(coordFloor) * _CosTime.y * 2.0 - 1.0) * (outOfBoundsAmount * (noise(coord) * 2.0 - 1.0) * 0.3 * sin(coord) + random(coord) * (outOfBoundsAmount*0.05));
                float2 valueCoord = coord + outOfBoundsDistortion * outOfBoundsAmount;
                float2 gridCoord = float2(valueCoord.x * _GridSize, valueCoord.y * _GridSize);
                float2 gridCell = floor(gridCoord);
                float cellValue = tex2D(_GameStateMap, valueCoord).a;
                col = tex2D(_CellColorRamp, float2(cellValue, cellValue));
                _SunDirection.x = 15 + (_PlayerPosition.y * 2.0 - 1.0) * 10.0;
                _SunDirection.y = 15 + (_PlayerPosition.x * 2.0 - 1.0) * 10.0;
                _SunDirection.xyz *= _SunDirection.w;
                float aberration = 1.0-cos(_Time.x)*0.1;

                // Draw Boss
                float distFromBoss = distance(coord, _BossPosition);
                float bossDarkness = _BossPosition.z/pow(10, distFromBoss);
                bossDarkness += 0.1 * noise(coord) + (0.1 * step(cellValue, 0.5) * step(0.25, cellValue));
                float bossDistNoise = noise(distFromBoss);
                float bossAmount = 1.0 - smoothstep(0.0, 0.75, 2.0*(distFromBoss+bossDistNoise));
                bossAmount *= step(0.0, bossAmount);
                bossDistNoise *= step(0.0, bossAmount);
                bossAmount *= step(cellValue, 0.75);
                _BossColor = lerp(_BossColor*0.75, _BossColor * 1.5, 1.0 - smoothstep(1.0, 0.05, distFromBoss+bossDistNoise*5.0));
                col = lerp(col, _BossColor, bossAmount);
                col = lerp(col, float4(0,0,0,0), bossDarkness);
                //col = lerp(col, _BossColor*0.8, smoothstep(0.01, 0.0, pow(distFromBoss, 3)));
                //col = lerp(col, _BossColor*1.1, step(distFromBoss, _BossPosition.w));
                
                // Draw Waves
                float globalWaveFluctuation = (_CosTime.w * 2.0 - 1.0) * 0.00001;
                float2 ditherValue = gridCell * (1.0 - cellValue) * 0.75+ random(valueCoord) * 1.2 + globalWaveFluctuation ;
                ditherValue *= 1.0 - (bossAmount*bossDistNoise*aberration);
                float2 wave = fractalSinWave(1.0 + globalWaveFluctuation, aberration*42.265*valueCoord+ditherValue, _Time.y, 1.0 + globalWaveFluctuation, 32, 1.163, 0.84).y;
                float waveNormal = wave.y * step(cellValue, 0.25);
                col = lerp(col, col*2.0, waveNormal-(bossAmount*bossDistNoise));
                
                // Draw Borders
                float2 cellFrac = frac(gridCoord-0.5);
                float2 distFromGrid = abs(cellFrac * 2.0 - 1.0);
                float nCellValue = neighbourCellValue(cellValue, valueCoord);
                _BorderColor.a *= step(0.1, nCellValue);
                float waveEffect = (wave.y * 1.5);
                col = lerp(col, _BorderColor, _BorderColor.a * (waveEffect + 1.0 - smoothstep(distFromGrid.x, 0.0, _BorderThickness)));
                col = lerp(col, _BorderColor, _BorderColor.a * (waveEffect + 1.0 - smoothstep(distFromGrid.y, 0.0, _BorderThickness)));

                // Draw Bounds & fill indicator & this is really convoluted eh
                float boundCoordNoise = 0.02*noise(coord*50.0+_Time.w*2.3)*noise(coord*12.3+_SinTime.z*5.2);
                float2 boundCoord = valueCoord - boundCoordNoise;
                float2 floorBoundCoord = floor(boundCoord+1.0);
                float fillCoord = min(frac(boundCoord.x - floorBoundCoord.x*0.01), frac(boundCoord.y - floorBoundCoord.y*0.01));
                float fillIndex = 1.0 - smoothstep(_FillPercent, _FillPercent, min(fillCoord, 1.0 - fillCoord) * 2.0);
                float boundThickness = lerp(0.0025, 0.007*_FillPercent, fillIndex);
                _BorderColor = lerp(float4(0.22, 0.22, 0.0, 1.0), float4(1.0, 1.0, boundCoordNoise+0.5*_FillPercent*length(distFromGrid)*aberration, 1.0), fillIndex);
                _BorderColor.a *= (step(boundCoord.x, 0.0) * step(0.0, boundCoord.x+boundThickness)) + (step(boundCoord.x-boundThickness, 1.0) * step(1.0, boundCoord.x))
                + (step(boundCoord.y, 0.0) * step(0.0, boundCoord.y+boundThickness)) + (step(boundCoord.y-boundThickness, 1.0) * step(1.0, boundCoord.y));
                col = lerp(col, _BorderColor, _BorderColor.a * (1.0 - smoothstep(distFromGrid.x, 0.0, _BorderThickness)));
                col = lerp(col, _BorderColor, _BorderColor.a * (1.0 - smoothstep(distFromGrid.y, 0.0, _BorderThickness)));

                // Draw Player
                float xDistFromPlayer = distance(coord.x, _PlayerPosition.x);
                float yDistFromPlayer = distance(coord.y, _PlayerPosition.y);
                float combinedDistFromPlayer = xDistFromPlayer + yDistFromPlayer;
                float lightDistanceFromPlayer = lerp(max(xDistFromPlayer, yDistFromPlayer), min(xDistFromPlayer, yDistFromPlayer), 0.75);
                lightDistanceFromPlayer += wave.y * 0.0075;
                col = lerp(col, _PlayerColor, max(0.0, _PlayerPosition.z)/lightDistanceFromPlayer);
                col = lerp(col, _PlayerColor, step(combinedDistFromPlayer, _PlayerPosition.w));
                col = lerp(col, float4(0,0,0,0), combinedDistFromPlayer*0.4);

                // Draw Noise Clouds
                //float blurDistance = abs(wave.y) * 0.05;
                //float blurAmount = 0.1;
                float cloudModifier = 0.1;
                float2 cloudCoord = float2(coord.x + _Time.y * 0.05, coord.y + _Time.y * 0.1) * 10.0;
                float2 opposingCloudCoord = float2(coord.x - (_SinTime.y * 0.3), coord.y - 0.84 - _CosTime.y * 0.15) * 5.0;
                float cloudAmount = lerp (noise(cloudCoord), noise(opposingCloudCoord), 0.5);
                //cloudAmount = lerp(cloudAmount, noise(float2(cloudCoord.x+blurDistance, cloudCoord.y)), blurAmount);
                //cloudAmount = lerp(cloudAmount, noise(float2(cloudCoord.x, cloudCoord.y+blurDistance)), blurAmount);
                //cloudAmount = lerp(cloudAmount, noise(float2(cloudCoord.x-blurDistance, cloudCoord.y)), blurAmount);
                //cloudAmount = lerp(cloudAmount, noise(float2(cloudCoord.x, cloudCoord.y-blurDistance)), blurAmount);
                cloudAmount *= step(0.5, cellValue);
                cloudAmount -= wave.y * wave.x * 0.12;
                float4 cloudCol = lerp(float4(1.0,1.0,1.0,1.0), float4(0.0, 1.0 - distFromBoss, 0.0, 1.0), step(distFromBoss*5.0-cloudAmount, 0.01));
                col = lerp(col + (wave.y * 2.0 - 1.0) * 0.005, cloudCol, cloudAmount*cloudModifier);
                
                return col;
            }
            ENDCG
        }
    }
}
