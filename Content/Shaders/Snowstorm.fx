// Tundra snowstorm background compositor, converted from Snowstorm.glsl.

float4x4 MatrixTransform;
float2 ViewportSize;
float Time;
float UseSourceTexture = 1.0;

float TimeScale = 1.0;

float SnowLayers = 6.0;
float ScaleFar = 24.0;
float ScaleNear = 5.0;
float SizeFar = 0.040;
float SizeNear = 0.190;
float FlakeJitter = 0.85;
float FlakeSizeVariation = 0.55;
float DensityFar = 0.95;
float DensityNear = 0.45;
float FallFar = 0.08;
float FallNear = 0.45;

float FlowStrength = 0.10;
float FlowScale = 1.3;
float FlowScrollX = 0.10;
float FlowScrollY = 0.16;
float FlowDepthMin = 0.40;
float WindDrift = 0.14;
float WindGust = 0.40;
float WindGustRate = 0.21;
float WindParallax = 1.0;

float3 SheetColor = float3(0.86, 0.90, 0.97);
float SheetBase = 0.05;
float SheetGust = 0.26;
float SheetScale = 1.7;
float SheetStretch = 7.0;
float SheetSpeed = 1.9;
float SheetLow = 0.42;
float SheetHigh = 0.86;

float SwayAmp = 0.16;
float SwayRateMin = 0.8;
float SwayRateMax = 2.6;
float TwinkleMinBrightness = 0.40;
float TwinkleRateMin = 0.6;
float TwinkleRateMax = 4.0;
float TwinkleDepthBias = 1.4;

float CrystalMin = 0.60;
float CrystalArm = 0.80;
float CrystalThick = 0.060;
float CrystalSpinMin = -0.5;
float CrystalSpinMax = 0.5;
float CrystalVariety = 0.40;

float DofFocus = 0.85;
float DofSpread = 0.10;
float EdgeSharp = 0.010;
float EdgeSoft = 0.110;

float FlakeGain = 1.25;
float SparkleGlow = 0.30;
float3 FlakeColorFar = float3(0.60, 0.69, 0.85);
float3 FlakeColorNear = float3(0.93, 0.97, 1.00);
float FarFade = 0.55;

float3 SkyTop = float3(0.03, 0.05, 0.13);
float3 SkyBottom = float3(0.12, 0.15, 0.22);
float SkyGradient = 1.0;

float3 HazeColor = float3(0.55, 0.60, 0.68);
float HazeBase = 0.10;
float HazeGust = 0.14;
float HazeScale = 2.2;
float HazeDrift = 0.06;

float Dither = 0.012;

static const float Pi = 3.14159265359;
static const float Tau = 6.28318530718;
static const int MaxSnowLayers = 6;
static const int MaxFbmOctaves = 4;

texture Texture : register(t0);
sampler2D TextureSampler : register(s0) = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput SpriteVertexShader(VSInput input)
{
    VSOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

float Hash21(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float2 Hash22(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac(float2(q.x + q.y, q.x + q.z) * float2(q.z, q.y));
}

float ValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 blend = frac(p);
    blend = blend * blend * (3.0 - 2.0 * blend);

    return lerp(
        lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), blend.x),
        lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + float2(1.0, 1.0)), blend.x),
        blend.y
    );
}

float2 RotateFbmDomain(float2 p)
{
    return float2(
        0.80 * p.x - 0.60 * p.y,
        0.60 * p.x + 0.80 * p.y
    );
}

float Fbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;

    [unroll]
    for (int i = 0; i < MaxFbmOctaves; i++)
    {
        value += amplitude * ValueNoise(p);
        p = RotateFbmDomain(p) * 2.0;
        amplitude *= 0.5;
    }

    return value;
}

float2 Rotate(float2 p, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

float PositiveMod(float x, float y)
{
    return x - y * floor(x / max(y, 0.0001));
}

float2 CurlFlow(float2 q, float time)
{
    float2 samplePosition = q * max(FlowScale, 0.001) + float2(time * FlowScrollX, time * FlowScrollY);
    float e = 0.12;
    float x1 = Fbm(samplePosition + float2(0.0, e));
    float x2 = Fbm(samplePosition - float2(0.0, e));
    float y1 = Fbm(samplePosition + float2(e, 0.0));
    float y2 = Fbm(samplePosition - float2(e, 0.0));
    return float2(x1 - x2, -(y1 - y2)) / (2.0 * e);
}

float LineSegment(float2 p, float2 a, float2 b, float thickness)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.00001));
    float d = length(pa - ba * h);
    float outer = max(thickness, 0.0001);
    float inner = outer * 0.4;
    return 1.0 - smoothstep(inner, outer, d);
}

float CrystalMask(float2 q, float armLength, float thickness)
{
    float angle = atan2(q.y, q.x);
    float radius = length(q);
    float wedge = Pi / 3.0;
    float foldedAngle = abs(PositiveMod(angle, wedge) - wedge * 0.5);
    float2 p = float2(cos(foldedAngle), sin(foldedAngle)) * radius;

    float mask = 0.0;
    mask += LineSegment(p, float2(0.0, 0.0), float2(armLength, 0.0), thickness);
    mask += LineSegment(p, float2(armLength * 0.28, 0.0), float2(armLength * 0.50, armLength * 0.26), thickness * 0.7);
    mask += LineSegment(p, float2(armLength * 0.52, 0.0), float2(armLength * 0.72, armLength * 0.20), thickness * 0.6);
    mask += LineSegment(p, float2(armLength * 0.74, 0.0), float2(armLength * 0.90, armLength * 0.12), thickness * 0.5);
    mask += 1.0 - smoothstep(0.13, 0.18, radius);
    return saturate(mask);
}

float SoftDisc(float innerRadius, float outerRadius, float distance)
{
    return 1.0 - smoothstep(innerRadius, max(outerRadius, innerRadius + 0.0001), distance);
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 sceneUv = input.TexCoord;
    float2 uv = float2(sceneUv.x, 1.0 - sceneUv.y);
    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float aspect = viewport.x / viewport.y;
    float2 p = float2(uv.x * aspect, uv.y);
    float time = Time * max(TimeScale, 0.0);

    float gust = WindGust * (
        0.6 * sin(time * WindGustRate) +
        0.4 * sin(time * WindGustRate * 2.3 + 1.7)
    );
    float gustNorm = 0.5 + 0.5 * sin(time * WindGustRate * 1.3 + 0.5);

    float skyBlend = lerp(0.5, uv.y, saturate(SkyGradient));
    float3 sky = lerp(SkyBottom, SkyTop, skyBlend);
    float3 sourceColor = tex2D(TextureSampler, sceneUv).rgb;
    float3 color = lerp(sky, sourceColor, saturate(UseSourceTexture));

    float2 hazePosition = p * max(HazeScale, 0.001) + float2(time * HazeDrift + gust * 0.3, -time * 0.02);
    float hazeAmount = (max(HazeBase, 0.0) + max(HazeGust, 0.0) * gustNorm) * Fbm(hazePosition);
    color = lerp(color, HazeColor, saturate(hazeAmount));

    float2 flow = CurlFlow(p, time) * max(FlowStrength, 0.0);

    float windAngle = atan2(WindDrift + gust, max(FallNear, 0.0001));
    float2 ribbonPosition = Rotate(p + flow * 0.4, -windAngle) * max(SheetScale, 0.001);
    ribbonPosition.y = ribbonPosition.y / max(SheetStretch, 0.001) + time * SheetSpeed;
    float sheetLow = min(SheetLow, SheetHigh);
    float sheetHigh = max(SheetLow, SheetHigh);
    sheetHigh = max(sheetHigh, sheetLow + 0.0001);
    float ribbon = smoothstep(sheetLow, sheetHigh, Fbm(ribbonPosition));
    float sheetAmount = ribbon * (max(SheetBase, 0.0) + max(SheetGust, 0.0) * gustNorm);
    color = lerp(color, SheetColor, saturate(sheetAmount));

    float layerCount = clamp(SnowLayers, 1.0, MaxSnowLayers);
    float layerDenominator = max(layerCount - 1.0, 1.0);

    [loop]
    for (int layer = 0; layer < MaxSnowLayers; layer++)
    {
        if ((float)layer < layerCount)
        {
            float depth = (float)layer / layerDenominator;
            bool isCrystal = depth >= CrystalMin;

            float scale = lerp(max(ScaleFar, 0.001), max(ScaleNear, 0.001), depth);
            float radius = max(lerp(SizeFar, SizeNear, depth), 0.0001);
            float density = saturate(lerp(DensityFar, DensityNear, depth));
            float fall = lerp(FallFar, FallNear, depth);

            float focus = saturate(DofFocus);
            float blur = abs(depth - focus) / max(max(focus, 1.0 - focus), 0.0001);
            float bokehRadius = radius + max(DofSpread, 0.0) * blur;
            bokehRadius = max(bokehRadius, 0.0001);
            float bokehDim = (radius / bokehRadius) * (radius / bokehRadius);
            float edge = min(lerp(max(EdgeSharp, 0.0001), max(EdgeSoft, 0.0001), blur), bokehRadius * 0.95);

            float windScreen = WindDrift * (1.0 + WindParallax * depth) * time + gust * (0.4 + depth * WindParallax);
            float2 warp = flow * lerp(saturate(FlowDepthMin), 1.0, depth);
            float2 samplePosition = float2(p.x + windScreen, p.y + fall * time) + warp;
            float2 layerPosition = samplePosition * scale;
            float2 gridId = floor(layerPosition);
            float2 gridValue = frac(layerPosition) - 0.5;

            float cover = 0.0;
            float3 flakeColor = float3(0.0, 0.0, 0.0);

            [unroll]
            for (int oy = -1; oy <= 1; oy++)
            {
                [unroll]
                for (int ox = -1; ox <= 1; ox++)
                {
                    float2 cell = gridId + float2(ox, oy);
                    float2 seed = cell + float2(layer * 37.2, layer * 17.7);
                    float densityGate = step(1.0 - density, Hash21(seed + 7.13));

                    if (densityGate > 0.5)
                    {
                        float2 r1 = Hash22(seed);
                        float2 r2 = Hash22(seed + 11.37);
                        float2 r3 = Hash22(seed + 23.91);

                        float2 flakePosition = float2(ox, oy) + (r1 - 0.5) * saturate(FlakeJitter);
                        float variation = saturate(FlakeSizeVariation);
                        float sizeRadius = radius * lerp(max(1.0 - variation * 0.5, 0.01), 1.0 + variation * 0.5, r1.x);
                        float swayRateMin = min(SwayRateMin, SwayRateMax);
                        float swayRateMax = max(SwayRateMin, SwayRateMax);
                        flakePosition.x += max(SwayAmp, 0.0) * sin(time * lerp(max(swayRateMin, 0.0), max(swayRateMax, 0.0), r2.x) + r3.x * Tau);

                        float2 relative = gridValue - flakePosition;
                        float dist = length(relative);

                        if (dist <= sizeRadius + bokehRadius)
                        {
                            float shape;
                            if (isCrystal)
                            {
                                float crystalVariation = saturate(CrystalVariety);
                                float armLength = max(CrystalArm, 0.001) * lerp(
                                    max(1.0 - crystalVariation * 0.5, 0.01),
                                    1.0 + crystalVariation * 0.5,
                                    Hash21(seed + 91.7)
                                );
                                float spin = lerp(CrystalSpinMin, CrystalSpinMax, Hash21(seed + 57.3));
                                float2 local = Rotate(relative / max(sizeRadius, 0.0001), -(r3.x * Tau + spin * time));
                                float crystal = CrystalMask(local, armLength, max(CrystalThick, 0.0001));
                                float outer = sizeRadius + bokehRadius - radius;
                                float disc = SoftDisc(outer - edge, outer, dist);
                                shape = lerp(crystal, disc, saturate(blur));
                            }
                            else
                            {
                                float flakeRadius = sizeRadius + (bokehRadius - radius);
                                shape = SoftDisc(flakeRadius - edge, flakeRadius, dist);
                            }

                            if (shape > 0.0)
                            {
                                float twinkleRateMin = min(TwinkleRateMin, TwinkleRateMax);
                                float twinkleRateMax = max(TwinkleRateMin, TwinkleRateMax);
                                float twinkleRaw = 0.5 + 0.5 * sin(
                                    time * lerp(max(twinkleRateMin, 0.0), max(twinkleRateMax, 0.0), r2.y) +
                                    r3.y * Tau
                                );
                                float twinkle = lerp(saturate(TwinkleMinBrightness), 1.0, twinkleRaw);
                                twinkle = saturate(1.0 - (1.0 - twinkle) * lerp(1.0, max(TwinkleDepthBias, 0.0), depth));

                                float alpha = shape * bokehDim * twinkle;
                                if (alpha > cover)
                                {
                                    cover = alpha;
                                    flakeColor = lerp(FlakeColorFar, FlakeColorNear, depth);
                                }
                            }
                        }
                    }
                }
            }

            cover *= max(FlakeGain, 0.0) * lerp(1.0 - saturate(FarFade), 1.0, depth);
            cover = saturate(cover);
            color = lerp(color, flakeColor, cover);
            color += flakeColor * max(SparkleGlow, 0.0) * smoothstep(0.6, 1.0, cover);
        }
    }

    float2 fragCoord = float2(sceneUv.x * viewport.x, uv.y * viewport.y);
    color += (Hash21(fragCoord + frac(time) * 100.0) - 0.5) * max(Dither, 0.0);
    return float4(saturate(color), 1.0) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
