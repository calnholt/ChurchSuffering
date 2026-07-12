float4x4 MatrixTransform;
float2 CardSizePx;
float TimeSeconds;
float SheenDurationSeconds;
float RepeatDelaySeconds;
float AngleRadians;
float BandWidthNormalized;
float FeatherNormalized;
float CoreWidthNormalized;
float CornerRadiusPx;
float Intensity;
float3 GoldFringeColor;
float3 BlueFringeColor;
float3 CoreColor;
float2 Resolution;
float2 CardCenter;
float CardRotation;

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

float RoundedRectangleDistance(float2 pointPx, float2 halfSizePx, float radiusPx)
{
    float radius = clamp(radiusPx, 0.0, min(halfSizePx.x, halfSizePx.y));
    float2 distanceToCorner = abs(pointPx) - (halfSizePx - radius);
    return length(max(distanceToCorner, 0.0))
        + min(max(distanceToCorner.x, distanceToCorner.y), 0.0)
        - radius;
}

float4 SheenPixelShader(VSOutput input) : COLOR0
{
    float2 localPx = (input.TexCoord - 0.5) * CardSizePx;
    float shapeDistance = RoundedRectangleDistance(localPx, CardSizePx * 0.5, CornerRadiusPx);
    clip(-shapeDistance);

    float2 direction = normalize(float2(cos(AngleRadians), sin(AngleRadians)));
    float projected = dot(input.TexCoord - 0.5, direction);
    float projectedHalfSpan = 0.5 * (abs(direction.x) + abs(direction.y));
    float sheenDuration = max(0.001, SheenDurationSeconds);
    float repeatDelay = max(0.0, RepeatDelaySeconds);
    float cycleElapsed = fmod(max(0.0, TimeSeconds), sheenDuration + repeatDelay);
    float sweepProgress = saturate(cycleElapsed / sheenDuration);
    float sweepAlpha = cycleElapsed < sheenDuration
        ? sin(sweepProgress * 3.14159265)
        : 0.0;
    float bandCenter = lerp(-1.3 * projectedHalfSpan, 1.3 * projectedHalfSpan, sweepProgress);
    float signedDistance = projected - bandCenter;
    float distanceToBand = abs(signedDistance);

    float broadBand = 1.0 - smoothstep(
        max(0.0, BandWidthNormalized),
        max(0.001, BandWidthNormalized + FeatherNormalized),
        distanceToBand);
    float core = 1.0 - smoothstep(
        max(0.0, CoreWidthNormalized),
        max(0.001, CoreWidthNormalized + FeatherNormalized * 0.35),
        distanceToBand);
    float fringe = max(0.0, broadBand - core * 0.45);
    float leading = fringe * smoothstep(-FeatherNormalized, FeatherNormalized, signedDistance);
    float trailing = fringe * (1.0 - smoothstep(-FeatherNormalized, FeatherNormalized, signedDistance));

    float3 color = CoreColor * core
        + GoldFringeColor * leading
        + BlueFringeColor * trailing;
    float opacity = saturate((broadBand * 0.55 + core * 0.45) * sweepAlpha * Intensity);
    return float4(color, opacity) * input.Color;
}

float4 CompositeSheenPixelShader(VSOutput input) : COLOR0
{
    float4 source = tex2D(TextureSampler, input.TexCoord);
    float2 screenPosition = input.TexCoord * max(Resolution, float2(1.0, 1.0));
    float2 delta = screenPosition - CardCenter;
    float cs = cos(-CardRotation);
    float sn = sin(-CardRotation);
    float2 local = float2(cs * delta.x - sn * delta.y, sn * delta.x + cs * delta.y);
    float2 cardUV = local / max(CardSizePx, float2(1.0, 1.0)) + 0.5;
    float shapeDistance = RoundedRectangleDistance(local, CardSizePx * 0.5, CornerRadiusPx);
    float shapeMask = step(shapeDistance, 0.0);

    float2 direction = normalize(float2(cos(AngleRadians), sin(AngleRadians)));
    float projected = dot(cardUV - 0.5, direction);
    float projectedHalfSpan = 0.5 * (abs(direction.x) + abs(direction.y));
    float sheenDuration = max(0.001, SheenDurationSeconds);
    float repeatDelay = max(0.0, RepeatDelaySeconds);
    float cycleElapsed = fmod(max(0.0, TimeSeconds), sheenDuration + repeatDelay);
    float sweepProgress = saturate(cycleElapsed / sheenDuration);
    float sweepAlpha = cycleElapsed < sheenDuration ? sin(sweepProgress * 3.14159265) : 0.0;
    float bandCenter = lerp(-1.3 * projectedHalfSpan, 1.3 * projectedHalfSpan, sweepProgress);
    float signedDistance = projected - bandCenter;
    float distanceToBand = abs(signedDistance);
    float broadBand = 1.0 - smoothstep(
        max(0.0, BandWidthNormalized),
        max(0.001, BandWidthNormalized + FeatherNormalized),
        distanceToBand);
    float core = 1.0 - smoothstep(
        max(0.0, CoreWidthNormalized),
        max(0.001, CoreWidthNormalized + FeatherNormalized * 0.35),
        distanceToBand);
    float fringe = max(0.0, broadBand - core * 0.45);
    float leading = fringe * smoothstep(-FeatherNormalized, FeatherNormalized, signedDistance);
    float trailing = fringe * (1.0 - smoothstep(-FeatherNormalized, FeatherNormalized, signedDistance));
    float3 sheenColor = CoreColor * core + GoldFringeColor * leading + BlueFringeColor * trailing;
    float opacity = saturate((broadBand * 0.55 + core * 0.45) * sweepAlpha * Intensity) * shapeMask;
    return float4(saturate(source.rgb + sheenColor * opacity), max(source.a, opacity)) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SheenPixelShader();
    }
}

technique CompositeDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 CompositeSheenPixelShader();
    }
}
