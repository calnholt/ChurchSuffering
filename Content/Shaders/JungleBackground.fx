// Jungle falling leaves background compositor, converted from jungle_background.glsl.

float4x4 MatrixTransform;
float2 ViewportSize;
float Time;

float LeafCount = 150.0;
float FieldOverfill = 1.15;
float TimeScale = 1.0;

float FallBase = 0.05;
float FallParallax = 0.9;
float WindDrift = -0.05;
float WindParallax = 1.0;
float WindGust = 0.01;
float WindGustRate = 0.02;

float2 SpinRate = float2(0.20, 0.30);
float SpinDesync = 1.0;
float SwayAmp = 0.35;
float SwayTilt = 0.45;
float SwayRateMin = 0.5;
float SwayRateMax = 1.6;

float LeafRadiusMin = 0.3;
float LeafRadiusVariation = 0.8;
float3 LeafColorDark = float3(0.05, 0.22, 0.03);
float3 LeafColorLight = float3(0.45, 0.80, 0.18);
float LeafHueJitter = 0.12;
float LeafBrightness = 1.0;
float FarFade = 0.40;

float CameraDistance = 15.0;
float CameraFov = 1.5;
float LeafZBack = 8.0;

float BackgroundBrightness = 1.0;
float3 BackgroundSkyLow = float3(0.20, 0.26, 0.30);
float3 BackgroundSkyHigh = float3(0.45, 0.52, 0.55);

static const float Pi = 3.14159265359;
static const float Tau = 6.28318530718;
static const int MaxLeafCount = 150;

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

float2 Rotate(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

float Hash21(float2 p)
{
    p = frac(p * float2(452.127, 932.618));
    p += dot(p, p + 123.23);
    return frac(p.x * p.y);
}

float Noise(float2 p)
{
    float2 q = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(Hash21(q + float2(0.0, 0.0)), Hash21(q + float2(1.0, 0.0)), f.x),
        lerp(Hash21(q + float2(0.0, 1.0)), Hash21(q + float2(1.0, 1.0)), f.x),
        f.y
    );
}

float Fbm(float2 p)
{
    float f = 0.0;
    f += 0.5 * Noise(p);
    f += 0.25 * Noise(p * 2.0);
    f += 0.125 * Noise(p * 4.0);
    f += 0.0625 * Noise(p * 8.0);
    return f;
}

float SdLeave(float2 p)
{
    float d = length(p * float2(1.0, 0.9) - float2(0.0, 0.02)) - 0.2 + 0.9 * abs(p.x);
    p.x = abs(p.x);

    float2 q = p;
    q -= float2(0.0, -0.2 + 32.0 * pow(p.x, 5.0));
    q = Rotate(q, Pi * 0.3 + 0.1 * Fbm(16.0 * p));
    q -= float2(0.0, 0.17);
    d = min(d, length(q) - 0.17 + 2.0 * abs(q.x));

    q = p;
    q -= float2(-0.07, -0.1 + 6.0 * pow(p.x, 3.0));
    q = Rotate(q, Pi * 0.7 + 0.1 * Fbm(24.0 * p));
    q -= float2(0.0, 0.13);
    d = min(d, length(q) - 0.13 + 2.5 * abs(q.x));

    q = p;
    float h = clamp(q.y, -0.377, 0.0);
    q -= float2(0.0, h);
    d = min(d + 0.2 * pow(Fbm(24.0 * p), 3.0), length(q) + 0.02 * h);
    return d;
}

float2 SphIntersect(float3 ro, float3 rd, float3 ce, float ra)
{
    float3 oc = ro - ce;
    float b = dot(oc, rd);
    float c = dot(oc, oc) - ra * ra;
    float h = b * b - c;
    if (h < 0.0) return float2(-1.0, -1.0);
    h = sqrt(h);
    return float2(-b - h, -b + h);
}

float2 Polar(float3 p)
{
    return float2(atan2(p.x, p.z), p.y);
}

float LeaveSphere(float3 ro, float3 rd, float3 ce, float2 an, float ra)
{
    float2 t = SphIntersect(ro, rd, ce, ra);
    if (t.y < 0.0) return 0.0;

    float3 oc = ro - ce;
    float3 p = oc + rd * t.x;
    float3 q = oc + rd * t.y;

    p.xz = Rotate(p.xz, an.x);
    q.xz = Rotate(q.xz, an.x);
    p.yz = Rotate(p.yz, an.y);
    q.yz = Rotate(q.yz, an.y);

    float diameter = max(ra * 2.0, 0.0001);
    float2 ratio = float2(ra, 1.0) / diameter;
    float2 pf = Polar(p) * ratio;
    float2 pb = Polar(q) * ratio;
    float lf = step(SdLeave(pf), 0.0);
    float lb = step(SdLeave(pb), 0.0);
    return lf * (1.0 - lb) + lb;
}

float Hash1(inout float n)
{
    n += 1.0;
    return frac(sin(n) * 2348.3241);
}

float2 Hash2(inout float n)
{
    n += 1.0;
    return frac(sin(n) * float2(2348.3241, 4591.5392));
}

float3 Hash3(inout float n)
{
    n += 1.0;
    return frac(sin(n) * float3(2348.3241, 4591.5392, 3412.4231));
}

float3 Render(float3 ro, float3 rd, float3 bg)
{
    float3 col = bg;
    float t = Time * max(TimeScale, 0.0);
    float gustRate = max(abs(WindGustRate), 0.0001);
    float gust = WindGust * (0.6 * sin(t * gustRate) + 0.4 * sin(t * gustRate * 2.3 + 1.7));

    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float aspect = viewport.x / viewport.y;
    float halfV = (max(CameraDistance, 0.001) + max(LeafZBack, 0.001)) * 0.5 / max(CameraFov, 0.001);
    float2 field = float2(halfV * aspect, halfV) * 2.0 * max(FieldOverfill, 0.001);
    float2 an = t * SpinRate;
    float count = clamp(LeafCount, 1.0, MaxLeafCount);
    float denominator = max(count - 1.0, 1.0);
    float swayRateLow = max(min(SwayRateMin, SwayRateMax), 0.0001);
    float swayRateHigh = max(max(SwayRateMin, SwayRateMax), swayRateLow + 0.0001);

    [loop]
    for (int leaf = 0; leaf < MaxLeafCount; leaf++)
    {
        if (leaf < count)
        {
            float depth = leaf / denominator;
            float n = depth + 1.0;
            float2 rnd2 = Hash2(n);
            float par = 1.0 + WindParallax * depth;
            float2 move = float2(
                WindDrift * par + gust * (0.3 + depth),
                FallBase * (1.0 + FallParallax * depth)
            ) * t;
            float2 p = (0.5 - frac(rnd2 + move)) * field;

            an += (Hash1(n) - 0.5) * SpinDesync;

            float3 rnd = Hash3(n);
            float3 mat = lerp(LeafColorDark, LeafColorLight, rnd.x);
            mat.r += (rnd.y - 0.5) * LeafHueJitter;
            mat.g += (rnd.z - 0.5) * LeafHueJitter * 0.5;
            mat = clamp(mat * max(LeafBrightness, 0.0), 0.0, 1.0);
            mat *= lerp(1.0 - saturate(FarFade), 1.0, depth);

            float ra = max(LeafRadiusMin + LeafRadiusVariation * Hash1(n), 0.001);
            float swayRate = lerp(swayRateLow, swayRateHigh, Hash1(n));
            float swayPhase = t * swayRate + Hash1(n) * Tau;
            p.x += SwayAmp * sin(swayPhase);
            float2 anLeaf = an + float2(SwayTilt * sin(swayPhase + Pi * 0.5), 0.0);

            float3 ce = float3(p, max(LeafZBack, 0.001) * (1.0 - depth));
            float alpha = LeaveSphere(ro, rd, ce, Pi * anLeaf, ra);
            col = lerp(col, mat, alpha);
        }
    }

    return col;
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float2 uv = float2(input.TexCoord.x, 1.0 - input.TexCoord.y);
    float2 pc = (uv * viewport - 0.5 * viewport) / viewport.y;
    float3 ro = float3(0.0, 0.0, -max(CameraDistance, 0.001));
    float3 rd = normalize(float3(pc * max(CameraFov, 0.001), 1.0));

    float3 tex = tex2D(TextureSampler, input.TexCoord).rgb;
    float hasTex = step(0.01, dot(tex, float3(1.0, 1.0, 1.0)));
    float3 sky = lerp(BackgroundSkyLow, BackgroundSkyHigh, uv.y);
    float3 bg = lerp(sky, tex, hasTex) * max(BackgroundBrightness, 0.0);

    float3 col = Render(ro, rd, bg);
    return float4(saturate(col), 1.0) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
