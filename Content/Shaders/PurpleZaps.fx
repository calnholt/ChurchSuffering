// Purple lightning background compositor, converted from PurpleZaps.glsl.

float4x4 MatrixTransform;
float2 ViewportSize;
float Time;
float UseSourceTexture = 1.0;

float Zoom = 0.10;
float ZapWarp = 1.50;
float ZapSwirl = 9.00;
float ZapGrowth = 0.02;
float ZapSpeed = 1.00;
float ZapFloor = 0.55;
float ZapGain = 1.60;
float3 ZapGlowColor = float3(0.35, 0.05, 0.70);
float3 ZapCoreColor = float3(0.85, 0.60, 1.00);
float ZapCoreLow = 0.80;
float ZapCoreHigh = 2.00;
float BackgroundDim = 0.40;
float3 BackgroundFallbackTop = float3(0.05, 0.02, 0.12);
float3 BackgroundFallbackBottom = float3(0.00, 0.00, 0.00);

static const int ZapSteps = 24;

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

float SafeSignedDenominator(float value, float minimumAbs)
{
    float magnitude = max(abs(value), minimumAbs);
    return value < 0.0 ? -magnitude : magnitude;
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 sceneUv = input.TexCoord;
    float2 uv = float2(sceneUv.x, 1.0 - sceneUv.y);
    float2 viewport = max(ViewportSize, float2(1.0, 1.0));
    float2 fragCoord = uv * viewport;

    float3 sourceColor = tex2D(TextureSampler, sceneUv).rgb;
    float3 placeholder = lerp(BackgroundFallbackBottom, BackgroundFallbackTop, uv.y);
    float3 bg = lerp(placeholder, sourceColor, saturate(UseSourceTexture));

    float2 u = max(Zoom, 0.001) * (2.0 * fragCoord - viewport) / max(viewport.y, 1.0);
    float2 v = viewport;
    float4 z = float4(1.0, 2.0, 3.0, 0.0);
    float4 o = z;
    float a = 0.5;
    float t = Time * ZapSpeed;

    [loop]
    for (int i = 1; i < ZapSteps; i++)
    {
        float fi = (float)i;
        a += ZapGrowth;
        t += 1.0;

        v = cos(t - 7.0 * u * pow(max(a, 0.001), fi)) - 5.0 * u;

        float4 rotationValues = cos(fi + 0.02 * t - z.wxzw * 11.0);
        float2x2 rotation = float2x2(
            rotationValues.x,
            rotationValues.y,
            rotationValues.z,
            rotationValues.w
        );
        u = mul(u, rotation);

        float warpDenominator = SafeSignedDenominator(0.5 - dot(u, u), 0.001);
        float2 warpArg = max(ZapWarp, 0.001) * u / warpDenominator - ZapSwirl * u.yx + t;
        float filamentDenominator = max(length((1.0 + fi * dot(v, v)) * sin(warpArg)), 0.001);

        u += tanh(40.0 * dot(u, u) * cos(100.0 * u.yx + t)) / 200.0
            + 0.2 * a * u
            + cos(4.0 / exp(dot(o, o) / 100.0) + t) / 300.0;

        o += (1.0 + cos(z + t)) / filamentDenominator;
    }

    o = 25.6 / (min(o, 13.0) + 164.0 / max(o, 0.001)) - dot(u, u) / 250.0;

    float zap = dot(o.rgb, float3(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0));
    zap = max(zap - ZapFloor, 0.0) * max(ZapGain, 0.0);

    float coreLow = min(ZapCoreLow, ZapCoreHigh - 0.0001);
    float coreHigh = max(ZapCoreHigh, coreLow + 0.0001);
    float coreMix = smoothstep(coreLow, coreHigh, zap);
    float3 zapColor = lerp(ZapGlowColor, ZapCoreColor, coreMix);

    float3 color = bg * (1.0 - saturate(zap * max(BackgroundDim, 0.0))) + zapColor * zap;
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
