// Cursed card rising shapes effect.
// Replaces the old cracks/ooze effect with simple upward-moving circles.

float4x4 MatrixTransform;
float2 iResolution;
float iTime;
float2 CARD_CENTER;
float2 CARD_SIZE;
float CARD_ROTATION = 0.0;
float CARD_RADIUS = 0.04;

float SHAPE_COUNT = 28.0;
float SHAPE_SIZE_MIN = 0.018;
float SHAPE_SIZE_MAX = 0.070;
float SHAPE_RISE_SPEED_MIN = 0.045;
float SHAPE_RISE_SPEED_MAX = 0.155;
float SHAPE_OPACITY = 0.55;
float SHAPE_EDGE_SOFTNESS = 0.16;
float SHAPE_VERTICAL_FADE = 0.14;
float3 SHAPE_COLOR = float3(0.72, 0.16, 0.96);
float EFFECT_SEED = 1.0;
float TIME_SPEED = 1.0;

#define MAX_SHAPES 48

texture Texture : register(t0);
sampler2D TextureSampler : register(s0) = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear; MagFilter = Linear; MipFilter = Linear;
    AddressU = Clamp; AddressV = Clamp;
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

float HashScalar(float value)
{
    return frac(sin(value * 12.9898 + 78.233) * 43758.5453);
}

float2 Rotate(float2 value, float angle)
{
    float cs = cos(angle);
    float sn = sin(angle);
    return float2(cs * value.x - sn * value.y, sn * value.x + cs * value.y);
}

float CardAspect()
{
    return max(CARD_SIZE.x, 1.0) / max(CARD_SIZE.y, 1.0);
}

float2 TextureUVToCardUV(float2 textureUV)
{
    float2 size = max(CARD_SIZE, float2(1.0, 1.0));
    float2 screenPosition = textureUV * max(iResolution, float2(1.0, 1.0));
    float2 local = Rotate(screenPosition - CARD_CENTER, -CARD_ROTATION);
    return float2(local.x / size.x + 0.5, 0.5 - local.y / size.y);
}

float CardSdf(float2 uv)
{
    float radius = max(CARD_RADIUS, 0.0);
    float2 halfSize = float2(0.5, 0.5) - radius;
    float2 delta = abs(uv - 0.5) - halfSize;
    return length(max(delta, 0.0)) + min(max(delta.x, delta.y), 0.0) - radius;
}

float AntialiasWidth(float value)
{
    return max(abs(ddx(value)) + abs(ddy(value)), 0.001);
}

float CardMask(float2 cardUV)
{
    float sd = CardSdf(cardUV);
    return 1.0 - smoothstep(0.0, AntialiasWidth(sd), sd);
}

float ShapeMask(float2 cardUV)
{
    float count = clamp(SHAPE_COUNT, 0.0, (float)MAX_SHAPES);
    float sizeMin = max(SHAPE_SIZE_MIN, 0.001);
    float sizeMax = max(SHAPE_SIZE_MAX, sizeMin);
    float speedMin = max(SHAPE_RISE_SPEED_MIN, 0.0);
    float speedMax = max(SHAPE_RISE_SPEED_MAX, speedMin);
    float edgeSoftness = max(SHAPE_EDGE_SOFTNESS, 0.001);
    float verticalFade = max(SHAPE_VERTICAL_FADE, 0.001);
    float time = iTime * TIME_SPEED;
    float aspect = CardAspect();
    float mask = 0.0;

    for (int i = 0; i < MAX_SHAPES; i++)
    {
        if ((float)i >= count)
        {
            continue;
        }

        float seed = (float)i + EFFECT_SEED * 19.13;
        float rndA = HashScalar(seed + 1.0);
        float rndB = HashScalar(seed + 11.0);
        float rndC = HashScalar(seed + 23.0);
        float radius = lerp(sizeMin, sizeMax, rndA);
        float speed = lerp(speedMin, speedMax, rndB);
        float travel = 1.0 + radius * 4.0;

        float2 center;
        center.x = lerp(radius, 1.0 - radius, rndC);
        center.y = -radius * 2.0 + frac(time * speed + rndB) * travel;

        float2 delta = cardUV - center;
        delta.x *= aspect;
        float dist = length(delta);
        float edge = max(radius * edgeSoftness, 0.001);
        float circle = 1.0 - smoothstep(radius - edge, radius, dist);
        float fade = smoothstep(-radius * 2.0, verticalFade, center.y) *
            (1.0 - smoothstep(1.0 - verticalFade, 1.0 + radius * 2.0, center.y));

        mask = max(mask, circle * fade);
    }

    return saturate(mask);
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 textureUV = input.TexCoord;
    float4 source = tex2D(TextureSampler, textureUV);
    float2 cardUV = TextureUVToCardUV(textureUV);
    float cardMask = CardMask(cardUV);
    float shapeMask = ShapeMask(cardUV) * cardMask * saturate(SHAPE_OPACITY);
    float3 color = lerp(source.rgb, SHAPE_COLOR, shapeMask);
    return float4(saturate(color), source.a) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
