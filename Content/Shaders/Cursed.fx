// Cursed card haunted-orb effect.
// Purple bubbles rise through the card with smoky bodies, broken runes,
// writhing rims, and restrained spectral trails.

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

float SegmentDistance(float2 position, float2 segmentStart, float2 segmentEnd)
{
    float2 segment = segmentEnd - segmentStart;
    float segmentLengthSquared = max(dot(segment, segment), 0.0001);
    float along = saturate(dot(position - segmentStart, segment) / segmentLengthSquared);
    return length(position - (segmentStart + segment * along));
}

float StrokeMask(float distanceToStroke, float thickness, float antialias)
{
    return 1.0 - smoothstep(thickness, thickness + antialias, distanceToStroke);
}

float BrokenRuneMask(float2 orbLocal, float seed, float radius, float time, float visibility)
{
    float direction = HashScalar(seed + 47.0) < 0.5 ? -1.0 : 1.0;
    float runeSpeed = lerp(0.10, 0.24, HashScalar(seed + 59.0));
    float runeAngle = direction * time * runeSpeed + HashScalar(seed + 71.0) * 6.2831853;
    float2 runePosition = Rotate(orbLocal / max(radius, 0.001), runeAngle);
    float angle = atan2(runePosition.y, runePosition.x);
    float antialias = 0.025;

    // The outer inscription is intentionally incomplete rather than a clean ring.
    float ringDistance = abs(length(runePosition) - 0.50);
    float ring = StrokeMask(ringDistance, 0.045, antialias);
    float fragmentPattern = abs(sin(angle * 3.0 + seed * 1.73));
    ring *= smoothstep(0.28, 0.48, fragmentPattern);

    // Three angular strokes create a compact, readable occult sigil.
    float leftStroke = StrokeMask(
        SegmentDistance(runePosition, float2(-0.30, -0.18), float2(0.0, 0.30)),
        0.045,
        antialias);
    float rightStroke = StrokeMask(
        SegmentDistance(runePosition, float2(0.0, 0.30), float2(0.30, -0.18)),
        0.045,
        antialias);
    float crossStroke = StrokeMask(
        SegmentDistance(runePosition, float2(-0.21, -0.01), float2(0.21, -0.01)),
        0.040,
        antialias);

    // Deterministically omit one stroke on some bubbles so the runes feel broken.
    leftStroke *= step(0.18, HashScalar(seed + 83.0));
    rightStroke *= step(0.28, HashScalar(seed + 97.0));
    crossStroke *= step(0.38, HashScalar(seed + 109.0));

    float glyph = max(ring, max(leftStroke, max(rightStroke, crossStroke)));
    float flicker = 0.78 + 0.22 * sin(time * lerp(1.2, 2.8, HashScalar(seed + 127.0)) + seed);
    return glyph * visibility * flicker;
}

float3 ApplyHauntedOrbs(float2 cardUV, float3 sourceColor)
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
    float bodyMask = 0.0;
    float smokeMask = 0.0;
    float rimMask = 0.0;
    float auraMask = 0.0;
    float runeMask = 0.0;

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
        float rndD = HashScalar(seed + 37.0);
        float radius = lerp(sizeMin, sizeMax, rndA);
        float speed = lerp(speedMin, speedMax, rndB);
        float travel = 1.0 + radius * 4.0;

        float2 center;
        center.x = lerp(radius, 1.0 - radius, rndC);
        center.y = -radius * 2.0 + frac(time * speed + rndB) * travel;

        float2 delta = cardUV - center;
        delta.x *= aspect;
        float dist = length(delta);
        float angle = atan2(delta.y, delta.x);

        // Low-frequency angular motion keeps the silhouette spectral without noise.
        float writhe = sin(angle * 4.0 + time * lerp(0.65, 1.35, rndC) + seed);
        writhe += 0.45 * sin(angle * 7.0 - time * lerp(0.35, 0.80, rndD) + seed * 2.1);
        writhe /= 1.45;
        float writhingRadius = radius * (1.0 + 0.09 * writhe);
        float edge = max(radius * edgeSoftness, 0.001);
        float body = 1.0 - smoothstep(writhingRadius - edge, writhingRadius, dist);
        float innerBody = 1.0 - smoothstep(writhingRadius - edge * 2.4, writhingRadius - edge * 0.35, dist);
        float rim = saturate(body - innerBody);

        // Stretch the halo below the orb to imply upward motion.
        float2 trailDelta = delta;
        if (trailDelta.y < 0.0)
        {
            trailDelta.y /= 1.65;
        }
        float trailDistance = length(trailDelta);
        float aura = exp(-max(trailDistance - writhingRadius * 0.72, 0.0) /
            max(radius * 0.34, 0.001));
        aura *= 1.0 - body * 0.72;

        // Broad moving bands provide texture inside the translucent shell.
        float2 normalizedDelta = delta / max(radius, 0.001);
        float smokeWave = sin(normalizedDelta.x * 2.2 + normalizedDelta.y * 1.4 + time * 0.72 + seed);
        smokeWave += 0.55 * sin(normalizedDelta.x * -1.3 + normalizedDelta.y * 2.7 - time * 0.46 + seed * 1.9);
        float smoke = saturate(0.5 + smokeWave / 3.1) * body;

        float fade = smoothstep(-radius * 2.0, verticalFade, center.y) *
            (1.0 - smoothstep(1.0 - verticalFade, 1.0 + radius * 2.0, center.y));

        // Only bubbles large enough to resolve cleanly receive an inscription.
        float runeStart = lerp(sizeMin, sizeMax, 0.42);
        float runeFull = lerp(sizeMin, sizeMax, 0.68);
        float runeVisibility = smoothstep(runeStart, max(runeFull, runeStart + 0.001), radius);
        float rune = BrokenRuneMask(delta, seed, radius, time, runeVisibility) * innerBody;

        bodyMask = max(bodyMask, body * fade);
        smokeMask = max(smokeMask, smoke * fade);
        rimMask = max(rimMask, rim * fade);
        auraMask = max(auraMask, aura * fade);
        runeMask = max(runeMask, rune * fade);
    }

    float opacity = saturate(SHAPE_OPACITY);
    float3 spectralBody = sourceColor * lerp(0.62, 0.48, smokeMask) + SHAPE_COLOR * 0.12;
    float bodyBlend = bodyMask * opacity * lerp(0.46, 0.66, smokeMask);
    float3 color = lerp(sourceColor, spectralBody, bodyBlend);

    color += SHAPE_COLOR * auraMask * opacity * 0.12;
    color += SHAPE_COLOR * rimMask * opacity * 0.42;
    color += lerp(SHAPE_COLOR, float3(0.92, 0.54, 1.0), 0.42) * runeMask * opacity * 0.72;
    return saturate(color);
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 textureUV = input.TexCoord;
    float4 source = tex2D(TextureSampler, textureUV);
    float2 cardUV = TextureUVToCardUV(textureUV);
    float cardMask = CardMask(cardUV);
    float3 hauntedColor = ApplyHauntedOrbs(cardUV, source.rgb);
    float3 color = lerp(source.rgb, hauntedColor, cardMask);
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
