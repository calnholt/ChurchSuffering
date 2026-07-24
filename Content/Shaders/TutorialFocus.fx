// Soft gothic tutorial focus overlay.
// Keeps highlighted UI clear while feathering and texturing the surrounding dim.

float4x4 MatrixTransform;
float2 ViewportSize;
float Time;

static const int MAX_CUTOUTS = 16;
float4 CutoutRects[MAX_CUTOUTS]; // center xy, half-size zw in logical pixels
float CutoutRotations[MAX_CUTOUTS];
int CutoutCount = 0;

float CutoutPadding = 8.0;
float CutoutCornerRadius = 18.0;
float CutoutFeather = 20.0;
float OverlayAlpha = 0.705882;

float RimWidth = 14.0;
float RimAlpha = 0.16;
float3 RimColor = float3(0.78, 0.62, 0.36);

float GrainStrength = 0.05;
float GrainScale = 0.04;
float GrainDriftSpeed = 2.0;
float BreathSpeed = 0.15;
float BreathAmount = 0.12;

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
    float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float ValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    local = local * local * (3.0 - 2.0 * local);

    return lerp(
        lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), local.x),
        lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + float2(1.0, 1.0)), local.x),
        local.y);
}

float RoundedRectangleDistance(float2 positionPx, float4 packedRect, float rotation)
{
    float2 halfSize = max(packedRect.zw + max(CutoutPadding, 0.0), float2(1.0, 1.0));
    float radius = min(max(CutoutCornerRadius, 0.0), min(halfSize.x, halfSize.y));
    float2 delta = positionPx - packedRect.xy;
    float cosine = cos(rotation);
    float sine = sin(rotation);
    float2 local = float2(
        delta.x * cosine + delta.y * sine,
        -delta.x * sine + delta.y * cosine);
    float2 q = abs(local) - (halfSize - radius);
    return length(max(q, float2(0.0, 0.0))) + min(max(q.x, q.y), 0.0) - radius;
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 screenPx = input.TexCoord * max(ViewportSize, float2(1.0, 1.0));
    float nearestDistance = 1000000.0;

    [unroll]
    for (int i = 0; i < MAX_CUTOUTS; i++)
    {
        if (i < CutoutCount)
        {
            nearestDistance = min(
                nearestDistance,
                RoundedRectangleDistance(screenPx, CutoutRects[i], CutoutRotations[i]));
        }
    }

    float hasCutout = CutoutCount > 0 ? 1.0 : 0.0;
    float feather = max(CutoutFeather, 0.001);
    float outside = lerp(1.0, smoothstep(0.0, feather, nearestDistance), hasCutout);

    float safeGrainScale = max(GrainScale, 0.001);
    float2 drift = float2(0.73, -0.41) * Time * max(GrainDriftSpeed, 0.0);
    float grain = ValueNoise((screenPx + drift) * safeGrainScale);
    grain = grain * 2.0 - 1.0;
    float grainMultiplier = 1.0 + grain * max(GrainStrength, 0.0);

    float phase = Time * max(BreathSpeed, 0.0) * 6.28318530718;
    float breath = 1.0 + sin(phase) * max(BreathAmount, 0.0);

    float rimWidth = max(RimWidth, 0.001);
    float rim = hasCutout * step(0.0, nearestDistance) *
        (1.0 - smoothstep(0.0, rimWidth, nearestDistance));
    float rimOpacity = saturate(RimAlpha) * rim * max(breath, 0.0);
    float dimOpacity = saturate(OverlayAlpha) * outside * grainMultiplier;
    float finalAlpha = saturate(dimOpacity + rimOpacity);

    float textureAlpha = tex2D(TextureSampler, input.TexCoord).a;
    float tintAlpha = textureAlpha * input.Color.a;

    // MonoGame's AlphaBlend expects premultiplied color.
    float3 finalColor = saturate(RimColor) * rimOpacity * tintAlpha * input.Color.rgb;
    return float4(finalColor, finalAlpha * tintAlpha);
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
