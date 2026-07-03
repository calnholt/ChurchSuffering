// Scorched card fire effect.
// Converted from Content/Shaders/scorched_card.glsl for MonoGame SpriteBatch.

float4x4 MatrixTransform;
float2 iResolution;
float iTime;
float2 CARD_CENTER;
float2 CARD_SIZE;
float CARD_ROTATION = 0.0;

// Card region in effect space, y=0 at bottom.
float CARD_LEFT = 0.38;
float CARD_RIGHT = 0.62;
float CARD_BOTTOM = 0.18;
float CARD_TOP = 0.82;
float CARD_RADIUS = 0.05;

float FIRE_REACH = 0.13;
float FIRE_INNER = 0.01;
float FLAME_SHAPE = 0.30;
float FLAME_SHARP = 7.0;
float FLAME_THRESH = 0.0;
float HEAT_FADE = 0.45;
float FIRE_SCALE = 7.5;
float FIRE_RISE = 1.7;
float FIRE_EVOLVE = 1.1;
float FIRE_TURB = 0.45;
float FIRE_LEAN_OUT = 1.2;
float FIRE_FUEL = 1.0;
float TOP_BIAS = 0.55;
float FIRE_BRIGHTNESS = 1.35;
float3 FIRE_TINT = float3(1.0, 1.0, 1.0);

float EMBER_STR = 1.3;
float EMBER_REACH = 0.11;
float EMBER_GRID = 22.0;
float EMBER_SIZE = 0.09;
float3 EMBER_COLOR = float3(1.0, 0.45, 0.10);

float CARD_SCORCH = 0.0;
float CARD_GLOW = 0.30;
float TIME_SPEED = 0.6;

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

float3 Mod289(float3 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float4 Mod289(float4 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float4 Permute(float4 x)
{
    return Mod289(((x * 34.0) + 1.0) * x);
}

float Snoise(float3 v)
{
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
    const float4 D = float4(0.0, 0.5, 1.0, 2.0);

    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - D.yyy;

    i = Mod289(i);
    float4 p = Permute(Permute(Permute(
        i.z + float4(0.0, i1.z, i2.z, 1.0))
        + i.y + float4(0.0, i1.y, i2.y, 1.0))
        + i.x + float4(0.0, i1.x, i2.x, 1.0));

    float n_ = 0.142857142857;
    float3 ns = n_ * D.wyz - D.xzx;

    float4 j = p - 49.0 * floor(p * ns.z * ns.z);
    float4 x_ = floor(j * ns.z);
    float4 y_ = floor(j - 7.0 * x_);
    float4 x = x_ * ns.x + ns.yyyy;
    float4 y = y_ * ns.x + ns.yyyy;
    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, float4(0.0, 0.0, 0.0, 0.0));

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 p0 = float3(a0.xy, h.x);
    float3 p1 = float3(a0.zw, h.y);
    float3 p2 = float3(a1.xy, h.z);
    float3 p3 = float3(a1.zw, h.w);

    float4 norm = rsqrt(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
    p0 *= norm.x;
    p1 *= norm.y;
    p2 *= norm.z;
    p3 *= norm.w;

    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    return 42.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
}

float Prng(float2 seed)
{
    seed = frac(seed * float2(5.3983, 5.4427));
    seed += dot(seed.yx, seed.xy + float2(21.5351, 14.3137));
    return frac(seed.x * seed.y * 95.4337);
}

float NoiseStack(float3 pos, int octaves, float falloff)
{
    float noise = Snoise(pos);
    float off = 1.0;
    if (octaves > 1)
    {
        pos *= 2.0;
        off *= falloff;
        noise = (1.0 - off) * noise + off * Snoise(pos);
    }
    if (octaves > 2)
    {
        pos *= 2.0;
        off *= falloff;
        noise = (1.0 - off) * noise + off * Snoise(pos);
    }
    if (octaves > 3)
    {
        pos *= 2.0;
        off *= falloff;
        noise = (1.0 - off) * noise + off * Snoise(pos);
    }
    return (1.0 + noise) / 2.0;
}

float2 NoiseStackUV(float3 pos, int octaves, float falloff)
{
    float a = NoiseStack(pos, octaves, falloff);
    float b = NoiseStack(pos + float3(3984.293, 423.21, 5235.19), octaves, falloff);
    return float2(a, b);
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
    const float PI = 3.1415926535897932384626433832795;
    float2 textureUV = input.TexCoord;
    float4 source = tex2Dlod(TextureSampler, float4(textureUV, 0.0, 0.0));
    float2 uv = TextureUVToEffectUV(textureUV);
    float2 ap = EffectAspectPosition(uv);
    float time = iTime * TIME_SPEED;
    float3 color = source.rgb;

    float sd = CardSdf(uv);
    float eps = max(CARD_TOP - CARD_BOTTOM, 0.0001) * 1.5 / max(CARD_SIZE.y, 1.0);
    float gx = CardSdf(uv + float2(eps, 0.0)) - sd;
    float gy = CardSdf(uv + float2(0.0, eps)) - sd;
    float2 nrm = normalize(float2(gx, gy) + 0.000001);

    float topFac = 0.5 + 0.5 * nrm.y;
    float fuel = FIRE_FUEL * lerp(1.0 - TOP_BIAS, 1.0, topFac);
    float tRaw = sd / (FIRE_REACH * max(fuel, 0.05));
    float reach = saturate(2.0 - tRaw);
    float t = saturate(tRaw);
    float band = 1.0 - smoothstep(0.0, max(FIRE_INNER, 0.0001), -sd);

    float3 npos = float3(ap * FIRE_SCALE + nrm * t * FIRE_LEAN_OUT, 0.0);
    npos.y -= time * FIRE_RISE;
    npos.z += time * FIRE_EVOLVE;

    float3 dpos = float3(ap * FIRE_SCALE * 1.2, 0.0) +
        time * float3(0.04, -FIRE_RISE * 0.45, FIRE_EVOLVE * 1.2);
    float2 warp = (NoiseStackUV(dpos, 2, 0.4) - 0.5) * 2.0;
    float noise = saturate(NoiseStack(npos + FIRE_TURB * float3(warp, 0.0), 3, 0.4));

    float k = max(FLAME_SHAPE * fuel, 0.0001);
    float flames = pow(t, k) * pow(noise, k);
    float lit = reach * pow(saturate(1.0 - flames * flames * flames), max(FLAME_SHARP, 0.0001));
    lit *= band;
    if (FLAME_THRESH > 0.0)
    {
        lit = saturate((lit - FLAME_THRESH) / max(1.0 - FLAME_THRESH, 0.001));
    }

    float f = lit * (1.0 - HEAT_FADE * t);
    float fff = f * f * f;
    float3 fire = FIRE_BRIGHTNESS * FIRE_TINT * float3(f, fff, fff * fff);

    float3 embers = float3(0.0, 0.0, 0.0);
    float emberReach = max(EMBER_REACH, 0.0001);
    float emberZone = smoothstep(emberReach, 0.0, abs(sd));
    emberZone *= smoothstep(CARD_BOTTOM - 0.01, CARD_BOTTOM + 0.02, uv.y);

    if (EMBER_STR > 0.0 && emberZone > 0.001)
    {
        float2 fragCoord = textureUV * max(iResolution, float2(1.0, 1.0));
        float emberGrid = max(EMBER_GRID, 0.0001);
        float2 sc = fragCoord - float2(0.0, 190.0 * time);
        sc -= 30.0 * (NoiseStackUV(0.01 * float3(sc, 30.0 * iTime), 1, 0.4) - 0.5);
        if (fmod(sc.y / emberGrid, 2.0) < 1.0)
        {
            sc.x += 0.5 * emberGrid;
        }

        float2 gi = floor(sc / emberGrid);
        float rnd = Prng(gi);
        float life = saturate(10.0 * (1.0 - saturate((gi.y + 190.0 * time / emberGrid) / (24.0 - 20.0 * rnd))));

        if (life > 0.0)
        {
            float sz = EMBER_SIZE * rnd * (0.3 + 0.7 * emberZone);
            float rad = 999.0 * rnd * 2.0 * PI + 2.0 * iTime;
            float2 off = (0.5 - sz) * emberGrid * float2(sin(rad), cos(rad));
            float2 m = fmod(sc + off, emberGrid) - 0.5 * emberGrid;
            float g = max(0.0, 1.0 - length(m) / (sz * emberGrid + 0.0001));
            embers = life * g * emberZone * EMBER_STR * EMBER_COLOR;
        }
    }

    if (sd < 0.0)
    {
        float edge = 1.0 - smoothstep(0.0, max(FIRE_INNER, 0.0001), -sd);
        float3 card = SampleCard(uv);
        card *= 1.0 - CARD_SCORCH * edge;
        card += CARD_GLOW * edge * float3(1.0, 0.45, 0.12);
        color = card;
    }

    color = max(color, fire);
    color += embers;

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
