/* ──────────────────────────────────────────────────────────────
   Burning Card — ShaderToy

   Sets a card's BORDER on fire. Flames hug the card edge — hottest
   right on the border line, licking outward (and leaning upward like
   real fire) before fading to wisps + embers.

   Fire engine is the Ashima simplex-noise flame from the original
   "fire" shader, but the screen-space height gradient is replaced by
   a SIGNED-DISTANCE band around a rounded card rect (same region trick
   as frozen_card.glsl). So instead of a wall of fire at screen bottom,
   you get a ring of fire around the card.

   Paste into shadertoy.com/new. Set iChannel0 to the card image (PNG/JPG).
   Leave empty = standalone (a dark procedural card face is drawn so you
   see something).

   LAYOUT NOTE: Move CARD_LEFT/RIGHT/BOTTOM/TOP to put the rectangle
   where your card sits (0..1 screen space, y=0 at bottom).
   All tunables at the top.
   ────────────────────────────────────────────────────────────── */

// ═══════════════════════════════════════════════════════════════
// TUNABLES
// ═══════════════════════════════════════════════════════════════

// ── Card Region (0..1 screen space, y=0 bottom) ───────────────
// The rounded rectangle the fire wraps around.
const float CARD_LEFT   = 0.38;  // left edge of card
const float CARD_RIGHT  = 0.62;  // right edge of card
const float CARD_BOTTOM = 0.18;  // bottom edge of card
const float CARD_TOP    = 0.82;  // top edge of card
const float CARD_RADIUS = 0.05;  // rounded-corner radius

// ── Fire Band (where flames live, in screen units) ────────────
const float FIRE_REACH = 0.13;  // how far flames lick OUTWARD past the border (low = tight, high = tall tongues)
const float FIRE_INNER = 0.01;  // how far fire bleeds INWARD over the card edge. 0 = stops at border

// ── Flame Shape (natural soft tongues — the Ashima "fire" curve) ──────
// The turbulent noise is shaped by the original fire formula:
// flames = pow(t, SHAPE) * pow(noise, SHAPE); lit = (1 - flames^3)^SHARP
// This gives soft, naturally-tapering licks (hottest at the border, fading to
// wispy tips) instead of a hard-thresholded neon rim.
const float FLAME_SHAPE  = 0.30;  // tongue length/softness (low = tall soft licks, high = short stubby flames)
const float FLAME_SHARP  = 7.0;   // falloff contrast along a tongue (orig fire = 8; low = washy glow, high = crisp licks)
const float FLAME_THRESH = 0.0;   // optional tonguing floor: 0 = full natural fire, raise for sparse separated licks
const float HEAT_FADE    = 0.45;  // extra cooling to red along a tongue (0 = let the color ramp redden on its own)

// ── Fire Motion (authentic rising churn — global UP scroll) ───
const float FIRE_SCALE     = 7.5;  // flame detail size (low = big lazy tongues, high = fine fast flicker)
const float FIRE_RISE      = 1.7;  // upward scroll speed — the core "flames rising" motion
const float FIRE_EVOLVE    = 1.1;  // turbulent churn rate (how fast shapes morph in place)
const float FIRE_TURB      = 0.45; // domain-warp strength — the lateral lick & flicker (0 = smooth, high = chaotic)
const float FIRE_LEAN_OUT  = 1.2;  // outward bend so tongues angle away from the card as they rise

// ── Per-edge Fuel ─────────────────────────────────────────────
const float FIRE_FUEL = 1.0;   // master hotness (low = thin starved flames, high = fat roaring fire)
const float TOP_BIAS  = 0.55;  // taller flames on top edge vs bottom (0 = even ring, 1 = bottom barely burns)

// ── Fire Color ────────────────────────────────────────────────
const float FIRE_BRIGHTNESS = 1.35;                    // overall flame intensity (lower = less neon-rim bloom)
const vec3  FIRE_TINT       = vec3(1.0, 1.0, 1.0);     // multiply on the flame ramp. (1,1,1)=natural orange.
                                                       // try (0.6,0.8,1.6) for blue/gas flame, (1.2,0.7,1.0) for magenta

// ── Embers / Sparks (the flying particles — emphasized) ───────
const float EMBER_STR   = 1.3;                       // ember brightness (0 = no sparks)
const float EMBER_REACH = 0.11;                      // fat band around the border where embers are born (bigger = more particles)
const float EMBER_GRID  = 22.0;                      // spacing of ember cells (low = few big embers, high = dense fine sparks)
const float EMBER_SIZE  = 0.09;                      // ember dot radius (low = pinpoint, high = soft glowing blobs)
const vec3  EMBER_COLOR = vec3(1.0, 0.45, 0.10);     // ember color (warm orange)

// ── Card Face / Background ────────────────────────────────────
const float CARD_SCORCH = 0.0;                         // how dark the card edge gets, charred by the fire (0 = clean card)
const float CARD_GLOW   = 0.30;                        // warm flame light cast onto the card face near the border (lower = less rim glow)
const vec3  BG_COLOR    = vec3(0.02, 0.02, 0.03);      // backdrop behind everything (dark = fire pops)
const float TIME_SPEED  = 0.6;                         // global fire time multiplier (slows/speeds the whole effect)

// ═══════════════════════════════════════════════════════════════
// NOISE CORE (Ashima simplex — keep as-is)
// ═══════════════════════════════════════════════════════════════
// Array & textureless GLSL simplex noise. Ian McEwan, Ashima Arts. MIT.
// https://github.com/ashima/webgl-noise

vec3 mod289(vec3 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec4 mod289(vec4 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec4 permute(vec4 x)
{
    return mod289(((x * 34.0) + 1.0) * x);
}

vec4 taylorInvSqrt(vec4 r)
{
    return 1.79284291400159 - 0.85373472095314 * r;
}

float snoise(vec3 v)
{
    const vec2 C = vec2(1.0 / 6.0, 1.0 / 3.0);
    const vec4 D = vec4(0.0, 0.5, 1.0, 2.0);

    vec3 i = floor(v + dot(v, C.yyy));
    vec3 x0 = v - i + dot(i, C.xxx);

    vec3 g = step(x0.yzx, x0.xyz);
    vec3 l = 1.0 - g;
    vec3 i1 = min(g.xyz, l.zxy);
    vec3 i2 = max(g.xyz, l.zxy);

    vec3 x1 = x0 - i1 + C.xxx;
    vec3 x2 = x0 - i2 + C.yyy;
    vec3 x3 = x0 - D.yyy;

    i = mod289(i);
    vec4 p = permute(permute(permute(
        i.z + vec4(0.0, i1.z, i2.z, 1.0))
        + i.y + vec4(0.0, i1.y, i2.y, 1.0))
        + i.x + vec4(0.0, i1.x, i2.x, 1.0));

    float n_ = 0.142857142857;
    vec3 ns = n_ * D.wyz - D.xzx;

    vec4 j = p - 49.0 * floor(p * ns.z * ns.z);
    vec4 x_ = floor(j * ns.z);
    vec4 y_ = floor(j - 7.0 * x_);
    vec4 x = x_ * ns.x + ns.yyyy;
    vec4 y = y_ * ns.x + ns.yyyy;
    vec4 h = 1.0 - abs(x) - abs(y);

    vec4 b0 = vec4(x.xy, y.xy);
    vec4 b1 = vec4(x.zw, y.zw);

    vec4 s0 = floor(b0) * 2.0 + 1.0;
    vec4 s1 = floor(b1) * 2.0 + 1.0;
    vec4 sh = -step(h, vec4(0.0));

    vec4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    vec4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    vec3 p0 = vec3(a0.xy, h.x);
    vec3 p1 = vec3(a0.zw, h.y);
    vec3 p2 = vec3(a1.xy, h.z);
    vec3 p3 = vec3(a1.zw, h.w);

    vec4 norm = inversesqrt(vec4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
    p0 *= norm.x;
    p1 *= norm.y;
    p2 *= norm.z;
    p3 *= norm.w;

    vec4 m = max(0.6 - vec4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    return 42.0 * dot(m * m, vec4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
}

// Cheap PRNG for ember placement. From https://www.shadertoy.com/view/4djSRW
float prng(in vec2 seed)
{
    seed = fract(seed * vec2(5.3983, 5.4427));
    seed += dot(seed.yx, seed.xy + vec2(21.5351, 14.3137));
    return fract(seed.x * seed.y * 95.4337);
}

const float PI = 3.1415926535897932384626433832795;

// Layered simplex (octave stack), remapped to 0..1.
float noiseStack(vec3 pos, int octaves, float falloff)
{
    float noise = snoise(pos);
    float off = 1.0;
    if (octaves > 1)
    {
        pos *= 2.0;
        off *= falloff;
        noise = (1.0 - off) * noise + off * snoise(pos);
    }
    if (octaves > 2)
    {
        pos *= 2.0;
        off *= falloff;
        noise = (1.0 - off) * noise + off * snoise(pos);
    }
    if (octaves > 3)
    {
        pos *= 2.0;
        off *= falloff;
        noise = (1.0 - off) * noise + off * snoise(pos);
    }
    return (1.0 + noise) / 2.0;
}

// Two decorrelated noise stacks -> a 2D displacement vector.
vec2 noiseStackUV(vec3 pos, int octaves, float falloff)
{
    float a = noiseStack(pos, octaves, falloff);
    float b = noiseStack(pos + vec3(3984.293, 423.21, 5235.19), octaves, falloff);
    return vec2(a, b);
}

// ═══════════════════════════════════════════════════════════════
// SIGNED DISTANCE: CARD RECTANGLE
// ═══════════════════════════════════════════════════════════════

// Signed distance to the rounded card rect (negative = inside the card).
float cardSDF(vec2 uv)
{
    vec2 c = vec2((CARD_LEFT + CARD_RIGHT) * 0.5, (CARD_BOTTOM + CARD_TOP) * 0.5);
    vec2 h = vec2((CARD_RIGHT - CARD_LEFT) * 0.5, (CARD_TOP - CARD_BOTTOM) * 0.5) - CARD_RADIUS;
    vec2 d = abs(uv - c) - h;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - CARD_RADIUS;
}

// ═══════════════════════════════════════════════════════════════
// MAIN IMAGE
// ═══════════════════════════════════════════════════════════════

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / iResolution.xy;
    float aspect = iResolution.x / iResolution.y;
    vec2 ap = vec2(uv.x * aspect, uv.y);  // aspect-corrected so flames aren't stretched
    float time = iTime * TIME_SPEED;

    // ── Card field: signed distance + outward normal ──────────
    float sd = cardSDF(uv);  // <0 inside card, 0 on border, >0 outside
    float eps = 1.5 / iResolution.y;  // ~1px step for the gradient
    float gx = cardSDF(uv + vec2(eps, 0.0)) - sd;
    float gy = cardSDF(uv + vec2(0.0, eps)) - sd;
    vec2 nrm = normalize(vec2(gx, gy) + 1e-6);  // points OUTWARD from the card edge

    // ── FLAME FIELD ───────────────────────────────────────────
    // The SDF distance plays the role screen-height played in the original
    // Ashima "fire" shader: t = 0 at the border (flame base) -> 1 at the reach
    // (flame tips). Per-edge fuel makes the top edge burn taller than the bottom.
    float topFac = 0.5 + 0.5 * nrm.y;  // 1 on top edge, 0 on bottom
    float fuel = FIRE_FUEL * mix(1.0 - TOP_BIAS, 1.0, topFac);
    float tRaw = sd / (FIRE_REACH * max(fuel, 0.05));  // 0 at border, grows outward
    float reach = clamp(2.0 - tRaw, 0.0, 1.0);         // tips fade out past the reach (no hard cut)
    float t = clamp(tRaw, 0.0, 1.0);                   // 0 at base -> 1 at tip
    float band = 1.0 - smoothstep(0.0, FIRE_INNER, -sd);  // fade fire bleeding inside the card

    // Rising sample frame: the turbulent field is read in a FIXED screen frame
    // and scrolled straight UP, so flames rise. The outward normal bends tongues
    // away from the card as they climb (no per-pixel rotation -> corners can't
    // streak into starbursts).
    vec3 npos = vec3(ap * FIRE_SCALE + nrm * t * FIRE_LEAN_OUT, 0.0);
    npos.y -= time * FIRE_RISE;   // scroll up -> flames rise
    npos.z += time * FIRE_EVOLVE; // churn in place

    // Domain-warp displacement with its OWN multi-axis time evolution — this is
    // the lateral licking + flicker the static version had lost. The warp field
    // itself rises (y) and churns (z), so tongues wobble and break up naturally.
    vec3 dpos = vec3(ap * FIRE_SCALE * 1.2, 0.0) + time * vec3(0.04, -FIRE_RISE * 0.45, FIRE_EVOLVE * 1.2);
    vec2 warp = (noiseStackUV(dpos, 2, 0.4) - 0.5) * 2.0;  // ~-1..1
    float noise = clamp(noiseStack(npos + FIRE_TURB * vec3(warp, 0.0), 3, 0.4), 0.0, 1.0);

    // Flame shaping — the original fire's soft natural curve:
    // flames = pow(t, k) * pow(noise, k); lit = (1 - flames^3)^SHARP
    // Full-hot at the base (t=0), tapering to wispy tips where noise wins out.
    float k = FLAME_SHAPE * fuel;
    float flames = pow(t, k) * pow(noise, k);
    float lit = reach * pow(clamp(1.0 - flames * flames * flames, 0.0, 1.0), FLAME_SHARP);
    lit *= band;
    if (FLAME_THRESH > 0.0)  // optional: carve sparser separated licks
        lit = clamp((lit - FLAME_THRESH) / max(1.0 - FLAME_THRESH, 1e-3), 0.0, 1.0);

    // Blackbody ramp: white-hot core -> orange -> deep red at the tips. The channel
    // curve reddens naturally as `f` drops; HEAT_FADE adds extra tip cooling.
    float f = lit * (1.0 - HEAT_FADE * t);
    float fff = f * f * f;
    vec3 fire = FIRE_BRIGHTNESS * FIRE_TINT * vec3(f, fff, fff * fff);

    // ── EMBERS (rising sparks born at the border) ─────────────
    vec3 embers = vec3(0.0);
    float emberZone = smoothstep(EMBER_REACH, 0.0, abs(sd));  // strongest right on the border
    // Embers rise — never spawn in the empty gap BELOW the card's bottom edge.
    emberZone *= smoothstep(CARD_BOTTOM - 0.01, CARD_BOTTOM + 0.02, uv.y);

    if (EMBER_STR > 0.0 && emberZone > 0.001)
    {
        vec2 sc = fragCoord - vec2(0.0, 190.0 * time);  // drift upward over time
        sc -= 30.0 * (noiseStackUV(0.01 * vec3(sc, 30.0 * iTime), 1, 0.4) - 0.5);
        if (mod(sc.y / EMBER_GRID, 2.0) < 1.0)
            sc.x += 0.5 * EMBER_GRID;

        vec2 gi = floor(sc / EMBER_GRID);
        float rnd = prng(gi);
        float life = clamp(10.0 * (1.0 - clamp((gi.y + 190.0 * time / EMBER_GRID) / (24.0 - 20.0 * rnd), 0.0, 1.0)), 0.0, 1.0);

        if (life > 0.0)
        {
            float sz = EMBER_SIZE * rnd * (0.3 + 0.7 * emberZone);
            float rad = 999.0 * rnd * 2.0 * PI + 2.0 * iTime;
            vec2 off = (0.5 - sz) * EMBER_GRID * vec2(sin(rad), cos(rad));
            vec2 m = mod(sc + off, EMBER_GRID) - 0.5 * EMBER_GRID;
            float g = max(0.0, 1.0 - length(m) / (sz * EMBER_GRID + 1e-4));
            embers = life * g * emberZone * EMBER_STR * EMBER_COLOR;
        }
    }

    // ── COMPOSE: background / card face / fire / embers ───────
    vec3 col = BG_COLOR;

    if (sd < 0.0)
    {
        // Inside the card: show the bound texture, or a procedural fallback.
        vec3 tex = texture(iChannel0, uv).rgb;
        float hasTex = step(0.01, dot(tex, vec3(1.0)));
        vec2 cl = (uv - vec2(CARD_LEFT, CARD_BOTTOM)) / vec2(CARD_RIGHT - CARD_LEFT, CARD_TOP - CARD_BOTTOM);
        vec3 fallbk = mix(vec3(0.10, 0.11, 0.14), vec3(0.16, 0.17, 0.20), cl.y)
            + 0.03 * step(0.5, fract(cl.y * 6.0));  // faint banding so it's not flat
        vec3 card = mix(fallbk, tex, hasTex);

        // Char the edge dark + cast warm flame light onto the face near the border.
        float edge = 1.0 - smoothstep(0.0, FIRE_INNER, -sd);  // 1 at edge -> 0 inside
        card *= 1.0 - CARD_SCORCH * edge;
        card += CARD_GLOW * edge * vec3(1.0, 0.45, 0.12);
        col = card;
    }

    // Fire and embers add over whatever is behind them.
    col = max(col, fire);
    col += embers;

    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
