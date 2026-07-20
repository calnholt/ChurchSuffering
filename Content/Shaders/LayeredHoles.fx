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

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 uv = input.TexCoord;
    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float aspect = viewport.x / viewport.y;
    float2 auv = float2(uv.x * aspect, uv.y);

    float3 col = tex2D(TextureSampler, uv).rgb;

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
        float geometricMask = 1.0 - smoothstep(radius - feather, radius + feather, d);
        float extinction = smoothstep(0.0, feather, radius);
        float m = geometricMask * extinction;
        if (m <= 0.0)
        {
            continue;
        }

        float rim = geometricMask * (1.0 - geometricMask) * 4.0 * extinction;
        float2 revealUv = uv + dispUv * RevealRefract * rim;
        float3 middle = tex2D(MiddleTextureSampler, revealUv).rgb;
        float3 bottom = tex2D(BottomTextureSampler, revealUv).rgb;
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
