// Frozen card ice and cold-breath effect
// Converted from Shadertoy GLSL to HLSL for MonoGame SpriteBatch.

float4x4 MatrixTransform;
float2 iResolution; // viewport width/height in pixels
float iTime;        // seconds
float2 CARD_CENTER; // rendered card center in top-left screen coordinates
float2 CARD_SIZE;   // rendered card width/height in pixels
float CARD_ROTATION = 0.0;

// Card region (0..1 shader space, y=0 bottom)
float CARD_LEFT = 0.40;
float CARD_RIGHT = 0.60;
float CARD_BOTTOM = 0.05;
float CARD_TOP = 0.50;
float CARD_RADIUS = 0.04;

// Ice clarity
float ICE_TINT_STR = 0.42;
float3 ICE_TINT = float3(0.62, 0.80, 0.95);
float ICE_BRIGHTEN = 0.06;

// Refraction
float REFRACT_AMT = 0.0001;
float REFRACT_SCALE = 20.0;
float REFRACT_SPEED = 0.15;

// Frost
float FROST_EDGE = 0.10;
float FROST_DENSITY = 0.55;
float FROST_SCALE = 2.0;
float3 FROST_COLOR = float3(0.88, 0.94, 1.0);

// Surface sparkle
float SPARKLE_AMT = 0.45;
float SPARKLE_SCALE = 990.0;
float SPARKLE_SIZE = 0.12;
float SPARKLE_SPEED = 1.5;

// Internal cracks
float CRACK_AMT = 1.0;
float CRACK_SCALE = 10.0;
float2 CRACK_SEED = float2(3.0, 3.0);
float CRACK_SHARP = 43.0;
float CRACK_DEPTH = 2.2;
float CRACK_LIGHT = 0.35;
float CRACK_SHADE = 0.28;
float CRACK_AO = 0.80;
float3 CRACK_DEEP_TINT = float3(0.30, 0.55, 0.80);
float3 CRACK_LIGHT_DIR = float3(-0.6, 0.7, 0.5);

// Ice facets
float FACET_TILT = 0.35;
float FACET_REFRACT = 0.018;
float FACET_REFLECT = 0.30;
float FACET_WARBLE = 0.6;

// Cold breath
float BREATH_STR = 0.85;
float BREATH_OFFSET = -0.10;
float BREATH_HEIGHT = 0.422;
float BREATH_WIDTH = 1.0;
float BREATH_SPREAD = 0.45;
float BREATH_EDGE_SOFT = 0.35;
float BREATH_RISE = 0.03;
float BREATH_SCALE = 5.5;
float BREATH_SWIRL = 1.8;
float BREATH_SWIRL_SPEED = 1.6;
float BREATH_PUFF = 0.01;
float3 BREATH_COLOR = float3(0.90, 0.95, 1.0);

// Background outside the card region
float3 BG_COLOR = float3(0.04, 0.06, 0.10);

#define FBM_OCTAVES 5

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

float Hash21(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 Hash22(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(Hash21(i), Hash21(i + float2(1.0, 0.0)), f.x),
        lerp(Hash21(i + float2(0.0, 1.0)), Hash21(i + float2(1.0, 1.0)), f.x),
        f.y);
}

float Fbm(float2 p)
{
    const float2x2 rotation = float2x2(0.80, 0.60, -0.60, 0.80);
    float value = 0.0;
    float amplitude = 0.5;

    for (int i = 0; i < FBM_OCTAVES; i++)
    {
        value += amplitude * ValueNoise(p);
        p = mul(rotation, p) * 2.0;
        amplitude *= 0.5;
    }

    return value;
}

// Returns nearest and second-nearest Voronoi point distances.
float2 Voronoi(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    float nearest = 8.0;
    float secondNearest = 8.0;

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 gridOffset = float2((float)x, (float)y);
            float2 pointOffset = Hash22(ip + gridOffset);
            float2 delta = gridOffset + pointOffset - fp;
            float distanceSquared = dot(delta, delta);

            if (distanceSquared < nearest)
            {
                secondNearest = nearest;
                nearest = distanceSquared;
            }
            else if (distanceSquared < secondNearest)
            {
                secondNearest = distanceSquared;
            }
        }
    }

    return sqrt(float2(nearest, secondNearest));
}

float CrackProfile(float2 p)
{
    float crackSharp = max(CRACK_SHARP, 0.0001);
    float2 distances = Voronoi(p * CRACK_SCALE + CRACK_SEED);
    float edge = distances.y - distances.x;
    return 1.0 - smoothstep(0.0, 1.0 / crackSharp, edge);
}

// Returns Voronoi distances and writes the nearest cell coordinate.
float2 VoronoiId(float2 p, out float2 id)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    float nearest = 8.0;
    float secondNearest = 8.0;
    float2 nearestId = ip;

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 gridOffset = float2((float)x, (float)y);
            float2 pointOffset = Hash22(ip + gridOffset);
            float2 delta = gridOffset + pointOffset - fp;
            float distanceSquared = dot(delta, delta);

            if (distanceSquared < nearest)
            {
                secondNearest = nearest;
                nearest = distanceSquared;
                nearestId = ip + gridOffset;
            }
            else if (distanceSquared < secondNearest)
            {
                secondNearest = distanceSquared;
            }
        }
    }

    id = nearestId;
    return sqrt(float2(nearest, secondNearest));
}

float CardSdf(float2 uv)
{
    float radius = max(CARD_RADIUS, 0.0);
    float2 center = float2((CARD_LEFT + CARD_RIGHT) * 0.5, (CARD_BOTTOM + CARD_TOP) * 0.5);
    float2 halfSize = float2((CARD_RIGHT - CARD_LEFT) * 0.5, (CARD_TOP - CARD_BOTTOM) * 0.5) - radius;
    float2 delta = abs(uv - center) - halfSize;
    return length(max(delta, 0.0)) + min(max(delta.x, delta.y), 0.0) - radius;
}

float2 Rotate(float2 value, float angle)
{
    float cs = cos(angle);
    float sn = sin(angle);
    return float2(cs * value.x - sn * value.y, sn * value.x + cs * value.y);
}

float2 TextureUVToEffectUV(float2 textureUV)
{
    float2 size = max(CARD_SIZE, float2(1.0, 1.0));
    float2 screenPosition = textureUV * max(iResolution, float2(1.0, 1.0));
    float2 local = Rotate(screenPosition - CARD_CENTER, -CARD_ROTATION);
    float2 normalized = float2(local.x / size.x + 0.5, 0.5 - local.y / size.y);
    return float2(
        lerp(CARD_LEFT, CARD_RIGHT, normalized.x),
        lerp(CARD_BOTTOM, CARD_TOP, normalized.y));
}

float2 EffectUVToTextureUV(float2 effectUV)
{
    float2 size = max(CARD_SIZE, float2(1.0, 1.0));
    float cardWidth = max(CARD_RIGHT - CARD_LEFT, 0.0001);
    float cardHeight = max(CARD_TOP - CARD_BOTTOM, 0.0001);
    float2 normalized = float2(
        (effectUV.x - CARD_LEFT) / cardWidth,
        (effectUV.y - CARD_BOTTOM) / cardHeight);
    float2 local = float2(
        (normalized.x - 0.5) * size.x,
        (0.5 - normalized.y) * size.y);
    float2 screenPosition = CARD_CENTER + Rotate(local, CARD_ROTATION);
    return screenPosition / max(iResolution, float2(1.0, 1.0));
}

float2 EffectAspectPosition(float2 effectUV)
{
    float cardWidth = max(CARD_RIGHT - CARD_LEFT, 0.0001);
    float cardHeight = max(CARD_TOP - CARD_BOTTOM, 0.0001);
    float2 normalized = float2(
        (effectUV.x - CARD_LEFT) / cardWidth,
        (effectUV.y - CARD_BOTTOM) / cardHeight);
    float2 size = max(CARD_SIZE, float2(1.0, 1.0));
    return float2(normalized.x * size.x / size.y, normalized.y);
}

float3 SampleCard(float2 effectUV)
{
    return tex2Dlod(TextureSampler, float4(EffectUVToTextureUV(effectUV), 0.0, 0.0)).rgb;
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 resolution = max(iResolution, float2(1.0, 1.0));
    float2 textureUV = input.TexCoord;
    float2 uv = TextureUVToEffectUV(textureUV);
    float2 aspectPosition = EffectAspectPosition(uv);
    float4 source = tex2Dlod(TextureSampler, float4(textureUV, 0.0, 0.0));
    float3 color = source.rgb;
    float outputAlpha = source.a;

    // Ice block over the card.
    float signedDistance = CardSdf(uv);
    float inIce = step(signedDistance, 0.0);
    float frostEdge = max(FROST_EDGE, 0.0001);
    float edgeAmount = smoothstep(-frostEdge, 0.0, signedDistance);

    if (inIce > 0.5)
    {
        float2 facetId;
        float crackScale = max(CRACK_SCALE, 0.0001);
        float crackSharp = max(CRACK_SHARP, 0.0001);
        float2 cellDistances = VoronoiId(aspectPosition * crackScale + CRACK_SEED, facetId);
        float crackCenter = 1.0 - smoothstep(0.0, 1.0 / crackSharp, cellDistances.y - cellDistances.x);
        float2 facetTilt = (Hash22(facetId) - 0.5) * 2.0 * FACET_TILT;

        float epsilon = 1.5 / resolution.y;
        float centerGroove = CrackProfile(aspectPosition);
        float xGroove = CrackProfile(aspectPosition + float2(epsilon, 0.0));
        float yGroove = CrackProfile(aspectPosition + float2(0.0, epsilon));
        float2 gradient = float2(xGroove - centerGroove, yGroove - centerGroove) / max(epsilon, 0.0001);

        float refractScale = max(REFRACT_SCALE, 0.0001);
        float2 warp = float2(
            Fbm(aspectPosition * refractScale + iTime * REFRACT_SPEED),
            Fbm(aspectPosition * refractScale + 17.0 - iTime * REFRACT_SPEED)) - 0.5;

        float2 tilt = facetTilt + warp * FACET_WARBLE + gradient * CRACK_DEPTH * CRACK_AMT;
        float3 normal = normalize(float3(tilt, 1.0));
        float2 cardUV = uv + normal.xy * REFRACT_AMT + facetTilt * FACET_REFRACT;
        float3 card = SampleCard(cardUV);

        card = lerp(card, ICE_TINT, saturate(ICE_TINT_STR));
        card += ICE_BRIGHTEN;

        float frostNoise = Fbm(aspectPosition * max(FROST_SCALE, 0.0001));
        float frost = edgeAmount * FROST_DENSITY * (0.5 + 0.5 * frostNoise);
        card = lerp(card, FROST_COLOR, saturate(frost));

        float3 lightDirection = normalize(CRACK_LIGHT_DIR + float3(0.0, 0.0, 0.0001));
        float diffuse = dot(normal, lightDirection);
        card += diffuse * FACET_REFLECT * (1.0 - crackCenter);
        card = lerp(card, CRACK_DEEP_TINT, saturate(crackCenter * CRACK_AO));
        card += max(diffuse, 0.0) * crackCenter * CRACK_LIGHT;
        card -= max(-diffuse, 0.0) * crackCenter * CRACK_SHADE;

        float specular = pow(max(diffuse, 0.0), 24.0) * crackCenter;
        card += specular * CRACK_LIGHT * 1.5;

        float sparkleScale = max(SPARKLE_SCALE, 0.0001);
        float2 sparkleDistances = Voronoi(aspectPosition * sparkleScale);
        float sparkleSize = max(SPARKLE_SIZE, 0.0001);
        float glint = pow(1.0 - smoothstep(0.0, sparkleSize, sparkleDistances.x), 6.0);
        glint *= 0.5 + 0.5 * sin(iTime * SPARKLE_SPEED + Hash21(floor(aspectPosition * sparkleScale)) * 6.28);
        card += glint * SPARKLE_AMT;

        color = card;
    }

    // Cold breath rising above the card.
    float cardCenterX = (CARD_LEFT + CARD_RIGHT) * 0.5;
    float cardWidth = CARD_RIGHT - CARD_LEFT;
    float breathBase = CARD_TOP + BREATH_OFFSET;

    if (uv.y > breathBase)
    {
        float yAbove = uv.y - breathBase;
        float yNormalized = yAbove / max(BREATH_HEIGHT, 0.0001);
        float halfWidth = (cardWidth * 0.5 * BREATH_WIDTH) * (1.0 + yNormalized * BREATH_SPREAD);
        float xDistance = abs(uv.x - cardCenterX) / max(abs(halfWidth), 0.001);
        float edgeSoftness = saturate(BREATH_EDGE_SOFT);
        float xFalloff = 1.0 - smoothstep(1.0 - edgeSoftness, 1.0, xDistance);
        float yFalloff = (1.0 - smoothstep(0.0, 1.0, yNormalized)) * smoothstep(0.0, 0.15, yNormalized);

        float breathScale = max(BREATH_SCALE, 0.0001);
        float2 mistPosition = float2(aspectPosition.x, uv.y) * breathScale;
        mistPosition.y -= iTime * BREATH_RISE * breathScale * 10.0;

        float2 swirlPosition = mistPosition * 0.6 + float2(0.0, iTime * BREATH_SWIRL_SPEED);
        float noiseA = Fbm(swirlPosition);
        float noiseB = Fbm(swirlPosition + float2(4.7, 1.3));
        float2 curl = float2(noiseB - 0.5, 0.5 - noiseA);
        mistPosition += curl * BREATH_SWIRL * (0.3 + yNormalized);

        float mist = Fbm(mistPosition + Fbm(mistPosition * 0.5 + iTime * 0.05));
        mist = smoothstep(0.35, 0.9, mist);

        float pulse = 1.0 - BREATH_PUFF * (0.5 + 0.5 * sin(iTime * 1.6));
        float breath = mist * xFalloff * yFalloff * BREATH_STR * pulse;
        color = lerp(color, BREATH_COLOR, saturate(breath));
        outputAlpha = max(outputAlpha, saturate(breath));
    }

    color *= outputAlpha;
    return float4(saturate(color), outputAlpha) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
