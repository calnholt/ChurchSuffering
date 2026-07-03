// Volcano embers and heat-haze background compositor, converted from VolcanoEmbers.glsl.

float4x4 MatrixTransform;
float2 ViewportSize;
float Time;
float UseSourceTexture = 1.0;

float TimeScale = 1.0;

float HazeAmp = 0.014;
float HazeScale = 3.2;
float HazeRise = 0.55;
float HazeWaveFrequency = 11.0;
float HazeWaveSpeed = 2.2;
float HazeNoiseMix = 0.65;
float HazeWaveMix = 0.45;
float HazeOctaves = 3.0;
float HazeReach = 1.10;
float HazeFloor = 0.18;

float EmberLayers = 5.0;
float ScaleFar = 16.0;
float ScaleNear = 5.0;
float SizeFar = 0.030;
float SizeNear = 0.090;
float SizeVariation = 0.60;
float DensityFar = 0.70;
float DensityNear = 0.32;
float RiseFar = 0.045;
float RiseNear = 0.150;

float EmberDrift = 0.010;
float WanderAmp = 0.040;
float WanderScale = 1.4;
float WanderSpeed = 0.20;
float SwayAmp = 0.060;
float SwayRateMin = 0.5;
float SwayRateMax = 2.4;

float TwinkleMinBrightness = 0.25;
float TwinkleRateMin = 0.8;
float TwinkleRateMax = 5.0;

float EmberCore = 0.32;
float HaloGain = 0.85;
float CoreGain = 1.30;
float EmberBloom = 0.35;

float3 CoreColor = float3(1.00, 0.92, 0.62);
float3 HotColor = float3(1.00, 0.48, 0.12);
float3 CoolColor = float3(0.75, 0.10, 0.02);

float GainFar = 0.45;
float GainNear = 1.20;
float EmberGain = 1.0;
float EmberTopDim = 0.15;
float EmberFadeLow = 0.10;
float EmberFadeHigh = 1.05;

float3 BackgroundTop = float3(0.04, 0.02, 0.05);
float3 BackgroundBottom = float3(0.55, 0.12, 0.02);
float BackgroundGlowScale = 2.0;

static const float Tau = 6.28318530718;
static const int MaxHazeOctaves = 4;
static const int MaxEmberLayers = 7;

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
    return frac((q.xx + q.yz) * q.zy);
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
    float octaveCount = clamp(HazeOctaves, 1.0, MaxHazeOctaves);

    [loop]
    for (int i = 0; i < MaxHazeOctaves; i++)
    {
        if (i < octaveCount)
        {
            value += amplitude * ValueNoise(p);
            p = RotateFbmDomain(p) * 2.0;
            amplitude *= 0.5;
        }
    }

    return value;
}

float2 HeatField(float2 p, float time)
{
    float2 q = p * max(HazeScale, 0.001) + float2(0.0, -time * HazeRise);
    float2 organic = float2(Fbm(q), Fbm(q + float2(31.4, 17.7))) - 0.5;
    float wave = sin(p.y * HazeWaveFrequency - time * HazeWaveSpeed);
    return organic * HazeNoiseMix + float2(wave, 0.0) * HazeWaveMix;
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 sceneUv = input.TexCoord;
    float2 uv = float2(sceneUv.x, 1.0 - sceneUv.y);
    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float aspect = viewport.x / viewport.y;
    float2 p = float2(uv.x * aspect, uv.y);
    float time = Time * max(TimeScale, 0.0);

    float reach = max(HazeReach, 0.001);
    float hazeMask = lerp(saturate(HazeFloor), 1.0, 1.0 - smoothstep(0.0, reach, uv.y));
    float2 offset = HeatField(p, time) * max(HazeAmp, 0.0) * hazeMask;
    float2 warpUv = uv + float2(offset.x / max(aspect, 0.001), offset.y);
    float2 sourceUv = float2(warpUv.x, 1.0 - warpUv.y);

    float3 sourceColor = tex2D(TextureSampler, sourceUv).rgb;
    float3 fallback = lerp(BackgroundTop, BackgroundBottom, pow(saturate(1.0 - uv.y), 1.5));
    fallback += BackgroundBottom * 0.35 * Fbm(warpUv * max(BackgroundGlowScale, 0.001) + float2(0.0, -time * 0.1)) * saturate(1.0 - uv.y);

    float3 color = lerp(fallback, sourceColor, saturate(UseSourceTexture));
    float3 emberColor = float3(0.0, 0.0, 0.0);
    float emberLight = 0.0;
    float layerCount = clamp(EmberLayers, 1.0, MaxEmberLayers);
    float layerDenominator = max(layerCount - 1.0, 1.0);

    [loop]
    for (int layer = 0; layer < MaxEmberLayers; layer++)
    {
        if (layer < layerCount)
        {
            float depth = layer / layerDenominator;
            float scale = lerp(max(ScaleFar, 0.001), max(ScaleNear, 0.001), depth);
            float radius = max(lerp(SizeFar, SizeNear, depth), 0.0001);
            float density = saturate(lerp(DensityFar, DensityNear, depth));
            float rise = lerp(RiseFar, RiseNear, depth);
            float gain = lerp(GainFar, GainNear, depth);

            float2 wander = float2(
                Fbm(p * max(WanderScale, 0.001) + float2(0.0, -time * WanderSpeed)) - 0.5,
                0.0
            ) * WanderAmp;
            float2 samplePosition = float2(p.x + EmberDrift * time, p.y - rise * time) + wander;
            float2 layerPosition = samplePosition * scale;
            float2 gridId = floor(layerPosition);
            float2 gridValue = frac(layerPosition) - 0.5;

            [unroll]
            for (int oy = -1; oy <= 1; oy++)
            {
                [unroll]
                for (int ox = -1; ox <= 1; ox++)
                {
                    float2 cell = gridId + float2(ox, oy);
                    float2 seed = cell + float2(layer * 41.3, layer * 19.7);
                    float densityGate = step(1.0 - density, Hash21(seed + 7.13));

                    float2 r1 = Hash22(seed);
                    float2 r2 = Hash22(seed + 11.37);
                    float2 r3 = Hash22(seed + 23.91);

                    float2 home = float2(ox, oy) + (r1 - 0.5);
                    float sizeRangeLow = max(1.0 - saturate(SizeVariation) * 0.5, 0.01);
                    float sizeRangeHigh = 1.0 + saturate(SizeVariation) * 0.5;
                    float sizeRadius = max(radius * lerp(sizeRangeLow, sizeRangeHigh, r1.x), 0.0001);
                    float swayRateMin = min(SwayRateMin, SwayRateMax);
                    float swayRateMax = max(SwayRateMin, SwayRateMax);
                    home.x += SwayAmp * sin(time * lerp(swayRateMin, swayRateMax, r2.x) + r3.x * Tau);

                    float dist = length(gridValue - home);
                    float shapeMask = 1.0 - step(sizeRadius, dist);
                    float distNorm = dist / sizeRadius;
                    float halo = smoothstep(1.0, 0.0, distNorm);
                    float core = smoothstep(max(EmberCore, 0.001), 0.0, distNorm);

                    float twinkleRateMin = min(TwinkleRateMin, TwinkleRateMax);
                    float twinkleRateMax = max(TwinkleRateMin, TwinkleRateMax);
                    float twinkle = lerp(
                        saturate(TwinkleMinBrightness),
                        1.0,
                        0.5 + 0.5 * sin(time * lerp(twinkleRateMin, twinkleRateMax, r2.y) + r3.y * Tau)
                    );

                    float3 ember = lerp(CoolColor, HotColor, Hash21(seed + 53.1));
                    ember = lerp(ember, CoreColor, core);

                    float amount = (halo * HaloGain + core * CoreGain) * twinkle * gain * densityGate * shapeMask;
                    emberColor += ember * amount * EmberGain;
                    emberLight += amount;
                }
            }
        }
    }

    float fadeLow = min(EmberFadeLow, EmberFadeHigh - 0.0001);
    float fadeHigh = max(EmberFadeHigh, fadeLow + 0.0001);
    float verticalFade = lerp(1.0, saturate(EmberTopDim), smoothstep(fadeLow, fadeHigh, uv.y));
    emberColor += CoreColor * max(EmberBloom, 0.0) * smoothstep(0.8, 2.0, emberLight);
    color += emberColor * verticalFade;

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
