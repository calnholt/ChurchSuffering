// WayStation incense smoke overlay, converted from Incense.glsl.

float4x4 MatrixTransform;
float2 ViewportSize;
float Time;
float Opacity = 0.65;

float SmokeScale = 3.2;
float WarpStrength = 2.6;
float SmokeLow = 0.30;
float SmokeHigh = 0.85;
float DepthParallax = 0.55;

float RiseSpeed = 0.055;
float ChurnSpeed = 0.040;
float DriftX = 0.010;

float3 GloomColor = float3(0.030, 0.034, 0.045);
float3 SmokeColor = float3(0.34, 0.36, 0.42);
float3 GlintColor = float3(1.00, 0.82, 0.55);

float MoteAmount = 0.1;
float MoteScale = 190.0;
float MoteDriftMin = 0.008;
float MoteDriftMax = 0.045;
float MoteFlashMin = 0.6;
float MoteFlashMax = 4.5;
float MoteFlashDepth = 0.9;

float VignetteAmount = 1.05;
float GrainAmount = 0.035;
float Exposure = 1.15;

static const int FbmOctaves = 6;
static const float Epsilon = 0.0001;

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

float SmoothStep01(float edge0, float edge1, float x)
{
    float span = max(edge1 - edge0, Epsilon);
    float t = saturate((x - edge0) / span);
    return t * t * (3.0 - 2.0 * t);
}

float Hash21(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float VNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(Hash21(i), Hash21(i + float2(1.0, 0.0)), f.x),
        lerp(Hash21(i + float2(0.0, 1.0)), Hash21(i + float2(1.0, 1.0)), f.x),
        f.y
    );
}

float2 RotateFbm(float2 p)
{
    return float2(0.80 * p.x - 0.60 * p.y, 0.60 * p.x + 0.80 * p.y);
}

float Fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;

    [unroll]
    for (int i = 0; i < FbmOctaves; i++)
    {
        v += a * VNoise(p);
        p = RotateFbm(p) * 2.0;
        a *= 0.5;
    }

    return v;
}

float SmokeField(float2 p, float t)
{
    float2 q = float2(
        Fbm(p + float2(0.0, 0.0) + 0.10 * t),
        Fbm(p + float2(5.2, 1.3) - 0.10 * t)
    );
    return Fbm(p + max(WarpStrength, 0.0) * q);
}

struct IncenseSample
{
    float3 SmokeColor;
    float3 MoteEmission;
    float Density;
};

IncenseSample RenderIncense(float2 fragCoord)
{
    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float2 uv = fragCoord / viewport;
    float aspect = viewport.x / viewport.y;
    float2 p = float2(uv.x * aspect, uv.y);
    float t = Time;

    float scale = max(SmokeScale, Epsilon);
    float parallax = max(DepthParallax, 0.0);
    float2 rise = float2(DriftX * t, -RiseSpeed * t);

    float nearSmoke = SmokeField(p * scale + rise, ChurnSpeed * t * 10.0);
    float farSmoke = SmokeField(
        p * scale * (1.0 + parallax) + rise * 0.6 + float2(13.0, 7.0),
        ChurnSpeed * t * 7.0
    );

    float density = lerp(nearSmoke, farSmoke, 0.45);
    float smokeLow = min(SmokeLow, SmokeHigh);
    float smokeHigh = max(SmokeLow, SmokeHigh);
    density = SmoothStep01(smokeLow, smokeHigh, density);

    float3 smokeColor = lerp(GloomColor, SmokeColor, density);

    float moteScale = max(MoteScale, 1.0);
    float2 mcoord = p * moteScale;
    float mcol = floor(mcoord.x);
    float driftLow = min(MoteDriftMin, MoteDriftMax);
    float driftHigh = max(MoteDriftMin, MoteDriftMax);
    float mspeed = lerp(driftLow, driftHigh, Hash21(float2(mcol, 17.0)));
    mcoord.y -= t * mspeed * moteScale;

    float2 mid = floor(mcoord);
    float mcell = Hash21(mid);
    float mote = SmoothStep01(0.985, 1.0, mcell);

    float flashLow = max(min(MoteFlashMin, MoteFlashMax), Epsilon);
    float flashHigh = max(max(MoteFlashMin, MoteFlashMax), flashLow + Epsilon);
    float frate = lerp(flashLow, flashHigh, Hash21(mid + 4.0));
    float fphase = Hash21(mid + 9.0) * 6.2831;
    float flashDepth = saturate(MoteFlashDepth);
    float flash = 1.0 - flashDepth * (0.5 + 0.5 * sin(t * frate + fphase));
    mote *= flash;

    float3 moteColor = GlintColor * mote * max(MoteAmount, 0.0) * (0.25 + density);

    float2 vv = uv - 0.5;
    float vig = 1.0 - max(VignetteAmount, 0.0) * dot(vv, vv) * 2.5;
    smokeColor *= saturate(vig);
    moteColor *= saturate(vig);

    float exposure = max(Exposure, 0.0);
    float3 smokeTone = smokeColor * exposure;
    smokeTone = smokeTone / (1.0 + smokeTone);
    float3 combinedTone = (smokeColor + moteColor) * exposure;
    combinedTone = combinedTone / (1.0 + combinedTone);
    float3 moteEmission = max(combinedTone - smokeTone, 0.0);

    float g = Hash21(floor(fragCoord) + frac(t * 7.13) * 300.0) - 0.5;
    smokeTone += g * max(GrainAmount, 0.0);

    IncenseSample sample;
    sample.SmokeColor = saturate(smokeTone);
    sample.MoteEmission = moteEmission;
    sample.Density = density;
    return sample;
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float2 uv = float2(input.TexCoord.x, 1.0 - input.TexCoord.y);
    IncenseSample sample = RenderIncense(uv * viewport);
    float opacity = saturate(Opacity) * input.Color.a;
    float alpha = opacity * sample.Density;
    float3 color = sample.SmokeColor * alpha + sample.MoteEmission * opacity;
    return float4(saturate(color), alpha);
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
