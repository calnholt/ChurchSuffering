// Thorned card vine effect.
// Converted from Content/Shaders/thorny_vines_card.glsl for MonoGame SpriteBatch.

float4x4 MatrixTransform;
float2 iResolution;
float iTime;
float2 CARD_CENTER;
float2 CARD_SIZE;
float CARD_ROTATION = 0.0;

// Card region in effect space, y=0 at bottom.
float CARD_LEFT = 0.40;
float CARD_RIGHT = 0.60;
float CARD_BOTTOM = 0.05;
float CARD_TOP = 0.50;
float CARD_RADIUS = 0.04;

float CURSE_TINT_STR = 0.10;
float3 CURSE_TINT = float3(0.16, 0.27, 0.13);
float EDGE_DARKEN = 0.18;

float VINE_THICKNESS_A = 0.01;
float VINE_THICKNESS_B = 0.01;
float OUTLINE_EXTRA = 0.00;
float LINE_SOFT = 0.0035;
float DIAGONAL_OPACITY = 1.0;
float DIAGONAL_OVERSHOOT = 0.14;

float SQUIRM_AMOUNT_A = 0.025;
float SQUIRM_AMOUNT_B = 0.025;
float SQUIRM_FREQ_A = 7.0;
float SQUIRM_FREQ_B = 8.5;
float SQUIRM_SPEED_A = 0.18;
float SQUIRM_SPEED_B = 0.14;
float SQUIRM_PHASE_B = 2.35;

float3 OUTLINE_COLOR = float3(0.010, 0.020, 0.010);
float3 VINE_DARK = float3(0.040, 0.095, 0.035);
float3 VINE_MID = float3(0.115, 0.210, 0.085);
float3 VINE_LIGHT = float3(0.245, 0.355, 0.160);
float VINE_OPACITY = 0.96;
float VINE_SHADOW = 0.30;

float THORNS_PER_VINE = 10.0;
float THORN_LEN = 0.050;
float THORN_BASE = 0.012;
float3 THORN_WHITE = float3(0.940, 0.930, 0.865);
float3 THORN_LIGHT = float3(1.000, 0.985, 0.920);

float EDGE_CREEP = 0.075;
float EDGE_ROOT_DENS = 0.48;
float EDGE_ROOT_SCALE = 35.0;
float TIME_SPEED = 1.0;

#define FBM_OCTAVES 5
#define MAX_THORNS_PER_VINE 16

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

float CardAspect()
{
    return max(CARD_SIZE.x, 1.0) / max(CARD_SIZE.y, 1.0);
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

float2 CardLocal(float2 uv)
{
    return float2(
        (uv.x - CARD_LEFT) / max(CARD_RIGHT - CARD_LEFT, 0.0001),
        (uv.y - CARD_BOTTOM) / max(CARD_TOP - CARD_BOTTOM, 0.0001));
}

float2 EffectAspectPosition(float2 effectUV)
{
    float2 q = CardLocal(effectUV);
    return float2(q.x * CardAspect(), q.y);
}

float2 AspectQ(float2 p)
{
    p.x *= CardAspect();
    return p;
}

float2 ToAspectCentered(float2 q)
{
    float2 p = q - 0.5;
    p.x *= CardAspect();
    return p;
}

float2 FromAspectCentered(float2 p)
{
    return float2(p.x / max(CardAspect(), 0.0001), p.y) + 0.5;
}

float TaperedThornMask(float2 p, float2 a, float2 b, float baseWidth, float tipWidth)
{
    float2 pp = AspectQ(p);
    float2 aa = AspectQ(a);
    float2 bb = AspectQ(b);

    float2 pa = pp - aa;
    float2 ba = bb - aa;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.00001));
    float d = length(pa - ba * h);

    float w = lerp(baseWidth, tipWidth, h);
    float finite = smoothstep(0.00, 0.08, h) * (1.0 - smoothstep(0.98, 1.0, h));
    return (1.0 - smoothstep(w, w + max(LINE_SOFT, 0.00001), d)) * finite;
}

float VineSquirm(float x, float amount, float freq, float speed, float phase)
{
    float t = iTime * TIME_SPEED * speed;
    return amount * sin(x * freq + phase + t)
        + amount * 0.45 * sin(x * freq * 2.20 + phase * 1.71 - t * 0.80)
        + amount * 0.18 * sin(x * freq * 3.50 + phase * 0.63 + t * 1.35);
}

float4 FixedDiagonalVine(float2 q, float diagonalSign, float vineIndex)
{
    float2 p = ToAspectCentered(q);
    float halfAspect = CardAspect() * 0.5;
    float overshoot = max(DIAGONAL_OVERSHOOT, 0.0);

    float2 a = float2(-halfAspect - overshoot, -0.5 * diagonalSign - overshoot * diagonalSign);
    float2 b = float2(halfAspect + overshoot, 0.5 * diagonalSign + overshoot * diagonalSign);

    float2 axis = b - a;
    float axisLen = length(axis);
    float2 dir = axis / max(axisLen, 0.00001);
    float2 nrm = float2(-dir.y, dir.x);

    float along = dot(p - a, dir);
    float x01 = along / max(axisLen, 0.00001);
    float x = x01 * 2.0 - 1.0;

    float second = step(0.5, vineIndex);
    float thick = lerp(VINE_THICKNESS_A, VINE_THICKNESS_B, second);
    float amount = lerp(SQUIRM_AMOUNT_A, SQUIRM_AMOUNT_B, second);
    float freq = lerp(SQUIRM_FREQ_A, SQUIRM_FREQ_B, second);
    float speed = lerp(SQUIRM_SPEED_A, SQUIRM_SPEED_B, second);
    float phase = vineIndex * SQUIRM_PHASE_B;

    thick = max(thick, 0.0001);
    amount = max(amount, 0.0);
    freq = max(freq, 0.0001);

    float curve = VineSquirm(x, amount, freq, speed, phase);
    float d = abs(dot(p - a, nrm) - curve);

    float endFade = smoothstep(0.00, 0.08, x01) * (1.0 - smoothstep(0.92, 1.00, x01));
    endFade = lerp(0.45, 1.0, endFade);

    float lineSoft = max(LINE_SOFT, 0.00001);
    float outline = (1.0 - smoothstep(thick + OUTLINE_EXTRA, thick + OUTLINE_EXTRA + lineSoft, d)) * endFade;
    float stem = (1.0 - smoothstep(thick, thick + lineSoft, d)) * endFade;

    float ridge = stem * (1.0 - smoothstep(0.0, max(thick * 0.42, 0.001), d));
    float facets = floor(Fbm(q * 20.0 + float2(vineIndex * 3.1, vineIndex * 1.7)) * 4.0) / 3.0;
    ridge *= 0.30 + 0.70 * facets;

    float thorn = 0.0;
    float thornOutline = 0.0;
    float thornCount = clamp(floor(THORNS_PER_VINE + 0.5), 0.0, (float)MAX_THORNS_PER_VINE);

    for (int j = 0; j < MAX_THORNS_PER_VINE; j++)
    {
        if ((float)j >= thornCount) continue;

        float fj = (float)j;
        float thornX01 = (fj + 0.50) / max(thornCount, 1.0);
        float thornX = thornX01 * 2.0 - 1.0;
        float thornAlong = thornX01 * axisLen;
        float thornCurve = VineSquirm(thornX, amount, freq, speed, phase);

        float side = lerp(-1.0, 1.0, fmod(fj + vineIndex, 2.0));
        float lengthWave = 0.75 + 0.25 * sin(fj * 1.73 + vineIndex * 2.17);
        float leanWave = sin(fj * 2.31 + vineIndex * 1.13);
        float len = max(THORN_LEN, 0.0) * lengthWave;

        float2 baseP = a + dir * thornAlong + nrm * (thornCurve + side * thick * 0.44);
        float2 tipP = baseP + nrm * side * len + dir * (leanWave * 0.035);

        float2 baseQ = FromAspectCentered(baseP);
        float2 tipQ = FromAspectCentered(tipP);

        float tEnd = smoothstep(0.00, 0.08, thornX01) * (1.0 - smoothstep(0.92, 1.00, thornX01));
        tEnd = lerp(0.45, 1.0, tEnd);

        thornOutline += TaperedThornMask(q, baseQ, tipQ, max(THORN_BASE, 0.0) + OUTLINE_EXTRA * 0.65, 0.003) * tEnd;
        thorn += TaperedThornMask(q, baseQ, tipQ, max(THORN_BASE, 0.0), 0.0015) * tEnd;
    }

    return saturate(float4(saturate(outline + thornOutline), saturate(stem), saturate(ridge), saturate(thorn)) * DIAGONAL_OPACITY);
}

float4 DiagonalVineField(float2 q)
{
    float4 vines = float4(0.0, 0.0, 0.0, 0.0);
    vines += FixedDiagonalVine(q, -1.0, 0.0);
    vines += FixedDiagonalVine(q, 1.0, 1.0);
    return saturate(vines);
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 textureUV = input.TexCoord;
    float4 source = tex2Dlod(TextureSampler, float4(textureUV, 0.0, 0.0));
    float2 uv = TextureUVToEffectUV(textureUV);
    float sd = CardSdf(uv);

    if (sd > 0.0)
    {
        return source * input.Color;
    }

    float2 q = saturate(CardLocal(uv));
    float2 ap = EffectAspectPosition(uv);
    float3 color = source.rgb;

    float centerClear = smoothstep(0.18, 0.34, min(min(q.x, 1.0 - q.x), min(q.y, 1.0 - q.y)));
    color = lerp(color, CURSE_TINT, saturate(CURSE_TINT_STR) * (1.0 - centerClear * 0.55));

    float edgeAmt = smoothstep(-max(EDGE_CREEP, 0.0001), 0.0, sd);
    float rootNoise = Fbm(ap * max(EDGE_ROOT_SCALE, 0.0001) + float2(iTime * TIME_SPEED * 0.04, -iTime * TIME_SPEED * 0.03));
    float root = edgeAmt * smoothstep(1.0 - saturate(EDGE_ROOT_DENS), 1.0, rootNoise);
    color *= 1.0 - edgeAmt * max(EDGE_DARKEN, 0.0);
    color = lerp(color, OUTLINE_COLOR, root * 0.45);

    float4 vines = DiagonalVineField(q);
    float outline = vines.x;
    float stem = vines.y;
    float hi = vines.z;
    float thorn = vines.w;

    color *= 1.0 - outline * max(VINE_SHADOW, 0.0);
    color = lerp(color, OUTLINE_COLOR, outline * 0.92);

    float facet = floor(Fbm(q * 22.0 + iTime * TIME_SPEED * 0.025) * 4.0) / 3.0;
    float3 vineColor = lerp(VINE_DARK, VINE_MID, 0.45 + 0.35 * facet);
    color = lerp(color, vineColor, stem * saturate(VINE_OPACITY));
    color = lerp(color, THORN_WHITE, thorn * 0.92);

    color += hi * VINE_LIGHT * 0.30;
    color += thorn * THORN_LIGHT * 0.10;

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
