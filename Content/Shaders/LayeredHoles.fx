// Three-layer image compositor converted from LayeredHoles.glsl.

float4x4 MatrixTransform;
float2 ViewportSize;
float Time;

int HoleCount = 30;
float4 Holes[30];

float HoleFeather = 0.045;
float FeatherVary = 0.70;
float RimWarpAmp = 0.340;
float RimWarpScale = 3.5;
float RimWarpSpeed = 0.35;
float RevealRefract = 0.35;

float RevealDarken = 0.00;

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

texture MiddleTexture : register(t1);
sampler2D MiddleTextureSampler : register(s1) = sampler_state
{
    Texture = <MiddleTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

texture BottomTexture : register(t2);
sampler2D BottomTextureSampler : register(s2) = sampler_state
{
    Texture = <BottomTexture>;
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
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

float ValueNoise(float2 x)
{
    float2 p = floor(x);
    float2 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);

    float a = Hash21(p + float2(0.0, 0.0));
    float b = Hash21(p + float2(1.0, 0.0));
    float c = Hash21(p + float2(0.0, 1.0));
    float d = Hash21(p + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float2 RotateFbmDomain(float2 p)
{
    return float2(
        0.80 * p.x - 0.60 * p.y,
        0.60 * p.x + 0.80 * p.y);
}

float Fbm(float2 p)
{
    float f = 0.0;
    float amp = 0.5;

    [unroll]
    for (int i = 0; i < 5; i++)
    {
        f += amp * ValueNoise(p);
        p = RotateFbmDomain(p) * 2.02;
        amp *= 0.5;
    }

    return f / 0.96875;
}

float2 WarpField(float2 p, float t)
{
    float a = Fbm(p + float2(0.0, 0.0) + t * 0.10);
    float b = Fbm(p + float2(5.2, 1.3) - t * 0.13);
    float2 q = float2(a, b);
    float c = Fbm(p + 4.0 * q + float2(1.7, 9.2) + t * 0.11);
    float d = Fbm(p + 4.0 * q + float2(8.3, 2.8) + t * 0.09);
    return float2(c, d) - 0.5;
}

float3 SampleLayer(sampler2D layerSampler, float2 uv, float3 placeholder)
{
    float3 tex = tex2D(layerSampler, uv).rgb;
    float hasTex = step(0.01, dot(tex, float3(1.0, 1.0, 1.0)));
    return lerp(placeholder, tex, hasTex);
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 uv = input.TexCoord;
    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float aspect = viewport.x / viewport.y;
    float2 auv = float2(uv.x * aspect, uv.y);

    float3 phTop = lerp(float3(0.85, 0.55, 0.30), float3(0.55, 0.20, 0.15), uv.y);
    float3 phMid = lerp(float3(0.10, 0.30, 0.55), float3(0.20, 0.65, 0.80), uv.x)
        * (0.75 + 0.25 * step(0.5, frac(uv.x * 8.0)) * step(0.5, frac(uv.y * 8.0)));
    float3 phBot = lerp(float3(0.10, 0.20, 0.10), float3(0.30, 0.55, 0.25), Fbm(auv * 4.0));

    float3 col = SampleLayer(TextureSampler, uv, phTop);

    float rimWarpScale = max(RimWarpScale, 0.001);
    float2 disp = WarpField(auv * rimWarpScale, Time * RimWarpSpeed) * RimWarpAmp;
    float2 dispUv = float2(disp.x / max(aspect, 0.001), disp.y);
    float fVary = Fbm(auv * rimWarpScale + 31.7);

    int clampedHoleCount = clamp(HoleCount, 0, 30);
    [loop]
    for (int i = 0; i < 30; i++)
    {
        if (i >= clampedHoleCount)
        {
            continue;
        }

        float4 hole = Holes[i];
        float2 center = hole.xy;
        float radius = hole.z;
        if (radius <= 0.0)
        {
            continue;
        }

        float d = distance(auv + disp, center);
        float feather = max(HoleFeather * (1.0 + FeatherVary * (fVary - 0.5)), 0.001);
        float m = 1.0 - smoothstep(radius - feather, radius + feather, d);
        if (m <= 0.0)
        {
            continue;
        }

        float rim = m * (1.0 - m) * 4.0;
        float2 revealUv = uv + dispUv * RevealRefract * rim;
        float3 middle = SampleLayer(MiddleTextureSampler, revealUv, phMid);
        float3 bottom = SampleLayer(BottomTextureSampler, revealUv, phBot);
        float3 revealed = lerp(bottom, middle, saturate(hole.w));
        revealed *= 1.0 - saturate(RevealDarken);

        col = lerp(col, revealed, m);
    }

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
