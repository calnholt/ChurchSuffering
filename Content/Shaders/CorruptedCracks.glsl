/*
   Corrupted Cracks Card Overlay - ShaderToy

   A card in the process of corruption: fractures split across the face,
   but their openings are filled with oozing purple energy, pulsing bubbles,
   and faint violet mist contained inside the card boundary.

   Paste into shadertoy.com/new. Set iChannel0 to the card image (PNG/JPG).
   Leave iChannel0 empty to preview with the procedural placeholder card.

   Channel usage:
   iChannel0: optional card image
*/

// ==============================================================
// TUNABLES
// ==============================================================

// Card region, 0..1 screen space, y=0 bottom.
const float CARD_LEFT   = 0.36;
const float CARD_RIGHT  = 0.64;
const float CARD_BOTTOM = 0.12;
const float CARD_TOP    = 0.88;
const float CARD_RADIUS = 0.035;

// Distribution input.
// Change this value to reroll where cracks, ooze pockets, bubbles, and sparks appear.
const float EFFECT_SEED = 1.0;

// Card corruption.
const vec3  BG_COLOR              = vec3(0.018, 0.014, 0.026);
const vec3  CARD_SHADOW_TINT      = vec3(0.080, 0.035, 0.125);
const vec3  CARD_SICKLY_TINT      = vec3(0.180, 0.055, 0.270);
const float CARD_DESATURATION     = 0.40;
const float CARD_TINT_STRENGTH    = 0.34;
const float CARD_EDGE_DARKEN      = 0.34;
const float CARD_CENTER_PRESERVE  = 0.62;

// Crack source.
const float PRIMARY_CRACK_SCALE   = 5.4;
const float SECONDARY_CRACK_SCALE = 10.5;
const float HAIRLINE_CRACK_SCALE  = 18.0;
const vec2  PRIMARY_CRACK_SEED    = vec2(4.70, 9.20);
const vec2  SECONDARY_CRACK_SEED  = vec2(12.30, 2.90);
const vec2  HAIRLINE_CRACK_SEED   = vec2(23.10, 17.40);
const float PRIMARY_CRACK_WIDTH   = 0.105;
const float SECONDARY_CRACK_WIDTH = 0.068;
const float HAIRLINE_CRACK_WIDTH  = 0.028;
const float CRACK_BRANCH_CUTOFF   = 0.42;
const float CRACK_CORE_WIDTH      = 0.42;
const float CRACK_RIM_WIDTH       = 0.76;
const float CRACK_DARKEN          = 0.58;
const float CRACK_EDGE_RELIEF     = 0.12;
const float CRACK_FLICKER_SPEED   = 3.40;
const float CRACK_FLICKER_DEPTH   = 0.20;

// Oozing purple energy.
const vec3  CORE_PURPLE           = vec3(0.98, 0.22, 1.00);
const vec3  INNER_PURPLE          = vec3(0.55, 0.08, 0.92);
const vec3  OUTER_PURPLE          = vec3(0.20, 0.04, 0.42);
const vec3  OOZE_DARK_PURPLE      = vec3(0.055, 0.010, 0.105);
const float CORE_BRIGHTNESS       = 1.26;
const float RIM_BRIGHTNESS        = 0.62;
const float HALO_BRIGHTNESS       = 0.34;
const float HALO_WIDTH            = 0.52;
const float OOZE_GLOB_SCALE       = 8.5;
const float OOZE_DETAIL_SCALE     = 23.0;
const float OOZE_SWELL_AMOUNT     = 0.38;
const float OOZE_SWIRL_STRENGTH   = 0.18;
const float OOZE_FLOW_SPEED       = 0.16;
const float OOZE_BODY_LOW         = 0.20;
const float OOZE_BODY_HIGH        = 0.78;
const float OOZE_CORE_LOW         = 0.64;
const float OOZE_CORE_HIGH        = 1.08;
const float OOZE_SURFACE_SHINE    = 0.52;
const float OOZE_EDGE_SHADOW      = 0.36;
const float ARCANE_SPARK_AMOUNT   = 0.18;
const float ARCANE_SPARK_SPEED    = 2.10;
const float ARCANE_DOT_SCALE      = 74.0;
const float ARCANE_DOT_SHARPNESS  = 0.985;

// Bubbling corruption pockets.
const float BUBBLE_AMOUNT         = 0.90;
const float BUBBLE_SCALE          = 14.0;
const float BUBBLE_SPEED          = 0.42;
const float BUBBLE_SIZE_MIN       = 0.055;
const float BUBBLE_SIZE_MAX       = 0.135;
const float BUBBLE_RIM_WIDTH      = 0.035;
const float BUBBLE_CORE_FADE      = 0.42;
const float BUBBLE_HIGHLIGHT      = 0.58;
const vec3  BUBBLE_RIM_COLOR      = vec3(1.00, 0.35, 1.00);

// Mist from the fractures.
const float MIST_INTENSITY        = 0.52;
const float MIST_SOURCE_WIDTH     = 0.34;
const float MIST_SCALE            = 5.50;
const float MIST_DETAIL_SCALE     = 13.0;
const float MIST_RISE_SPEED       = 0.055;
const float MIST_SIDE_DRIFT       = 0.020;
const float MIST_SWIRL_STRENGTH   = 1.45;
const float MIST_SWIRL_SPEED      = 0.70;
const float MIST_SOFT_LOW         = 0.32;
const float MIST_SOFT_HIGH        = 0.82;
const float MIST_INSIDE_FADE      = 0.62;
const vec3  MIST_COLOR_LOW        = vec3(0.20, 0.05, 0.34);
const vec3  MIST_COLOR_HIGH       = vec3(0.62, 0.16, 0.95);

// Soft internal liquid currents.
const float CURRENT_SCALE         = 11.0;
const float CURRENT_SPEED         = 0.18;
const float CURRENT_OPACITY       = 0.26;
const float CURRENT_WIDTH         = 0.18;
const float CURRENT_PULSE_SPEED   = 1.05;

// Finishing.
const float EDGE_AA               = 0.0025;
const float VIGNETTE_STRENGTH     = 0.42;
const float GRAIN_AMOUNT          = 0.025;
const float EXPOSURE              = 1.08;

// Implementation constants.
#define FBM_OCTAVES 5

const float TWO_PI                = 6.28318530718;
const float LACUNARITY            = 2.0;
const float PERSISTENCE           = 0.5;
const float SEED_X_MULT           = 17.23;
const float SEED_Y_MULT           = -41.11;
const float SEED_SALT_X_MULT      = 31.70;
const float SEED_SALT_Y_MULT      = 19.90;
const mat2  FBM_ROT               = mat2(0.80, 0.60, -0.60, 0.80);

// ==============================================================
// HASH / NOISE
// ==============================================================

float hash21(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

vec2 hash22(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}

vec2 seedOffset(float salt)
{
    return vec2(
        EFFECT_SEED * SEED_X_MULT + salt * SEED_SALT_X_MULT,
        EFFECT_SEED * SEED_Y_MULT + salt * SEED_SALT_Y_MULT);
}

float vnoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
        mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x),
        f.y);
}

float fbm(vec2 p)
{
    float value = 0.0;
    float amp = 0.5;
    for (int i = 0; i < FBM_OCTAVES; i++)
    {
        value += amp * vnoise(p);
        p = FBM_ROT * p * LACUNARITY;
        amp *= PERSISTENCE;
    }
    return value;
}

// ==============================================================
// CARD GEOMETRY
// ==============================================================

float cardSDF(vec2 uv)
{
    vec2 center = vec2((CARD_LEFT + CARD_RIGHT) * 0.5, (CARD_BOTTOM + CARD_TOP) * 0.5);
    vec2 halfSize = vec2((CARD_RIGHT - CARD_LEFT) * 0.5, (CARD_TOP - CARD_BOTTOM) * 0.5) - CARD_RADIUS;
    vec2 d = abs(uv - center) - halfSize;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - CARD_RADIUS;
}

vec2 cardUV(vec2 uv)
{
    return vec2(
        (uv.x - CARD_LEFT) / (CARD_RIGHT - CARD_LEFT),
        (uv.y - CARD_BOTTOM) / (CARD_TOP - CARD_BOTTOM));
}

float cardAspect()
{
    return ((CARD_RIGHT - CARD_LEFT) * iResolution.x) /
           ((CARD_TOP - CARD_BOTTOM) * iResolution.y);
}

vec2 cardSpace(vec2 q)
{
    vec2 p = q - 0.5;
    p.x *= cardAspect();
    return p;
}

// ==============================================================
// CARD COLOR SOURCE
// ==============================================================

vec3 sampleCard(vec2 q)
{
    vec3 tex = texture(iChannel0, q).rgb;

    vec2 grid = abs(fract(q * 10.0) - 0.5);
    float line = smoothstep(0.46, 0.50, max(grid.x, grid.y));
    vec3 paper = mix(vec3(0.22, 0.18, 0.24), vec3(0.62, 0.54, 0.70), q.y);
    vec3 frame = mix(vec3(0.12, 0.08, 0.16), vec3(0.34, 0.25, 0.42), q.y);
    float inner = smoothstep(0.03, 0.09, min(min(q.x, 1.0 - q.x), min(q.y, 1.0 - q.y)));
    vec3 proc = mix(frame, paper, inner);
    proc = mix(proc, vec3(0.92, 0.86, 1.00), line * 0.13);

    float hasTex = step(0.01, dot(tex, vec3(1.0)));
    return mix(proc, tex, hasTex);
}

// ==============================================================
// CRACK NETWORK
// ==============================================================

vec2 voronoiEdge(vec2 p, out vec2 cellId)
{
    vec2 ip = floor(p);
    vec2 fp = fract(p);
    float d1 = 8.0;
    float d2 = 8.0;
    cellId = ip;

    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            vec2 g = vec2(float(x), float(y));
            vec2 offset = hash22(ip + g);
            vec2 r = g + offset - fp;
            float d = dot(r, r);
            if (d < d1)
            {
                d2 = d1;
                d1 = d;
                cellId = ip + g;
            }
            else if (d < d2)
            {
                d2 = d;
            }
        }

    return vec2(sqrt(d1), sqrt(d2));
}

float crackLayer(vec2 p, float scale, vec2 seed, float width, float branchCutoff, out vec2 id)
{
    vec2 edge = voronoiEdge(p * scale + seed, id);
    float edgeDistance = edge.y - edge.x;
    float branch = step(branchCutoff, hash21(id + seed));
    float line = 1.0 - smoothstep(0.0, width, edgeDistance);
    return line * branch;
}

float crackNetwork(vec2 q, out float core, out float rim, out vec2 primaryId)
{
    vec2 p = cardSpace(q);
    vec2 idA;
    vec2 idB;
    vec2 idC;

    float primary = crackLayer(
        p,
        PRIMARY_CRACK_SCALE,
        PRIMARY_CRACK_SEED + seedOffset(1.0),
        PRIMARY_CRACK_WIDTH,
        CRACK_BRANCH_CUTOFF,
        idA);

    float secondary = crackLayer(
        p + vec2(
            fbm(p * 2.0 + seedOffset(2.0)),
            fbm(p * 2.0 + seedOffset(3.0))) * 0.08,
        SECONDARY_CRACK_SCALE,
        SECONDARY_CRACK_SEED + seedOffset(4.0),
        SECONDARY_CRACK_WIDTH,
        CRACK_BRANCH_CUTOFF + 0.13,
        idB);

    float hair = crackLayer(
        p,
        HAIRLINE_CRACK_SCALE,
        HAIRLINE_CRACK_SEED + seedOffset(5.0),
        HAIRLINE_CRACK_WIDTH,
        CRACK_BRANCH_CUTOFF + 0.24,
        idC);

    float network = clamp(primary + secondary * 0.42 + hair * 0.16, 0.0, 1.0);
    core = smoothstep(CRACK_CORE_WIDTH, 1.0, network);
    rim = smoothstep(CRACK_RIM_WIDTH, 1.0, network);
    primaryId = idA;
    return network;
}

float oozeField(vec2 q, float crackMask, out float oozeCore, out float oozeRim, out float oozeShine)
{
    vec2 p = cardSpace(q);
    float t = iTime * OOZE_FLOW_SPEED;
    vec2 warp = vec2(
        fbm(p * OOZE_GLOB_SCALE + vec2(t, -t * 0.7) + seedOffset(6.0)),
        fbm(p * OOZE_GLOB_SCALE + vec2(8.4 - t * 0.6, 3.1 + t) + seedOffset(7.0)));
    vec2 warped = p + (warp - 0.5) * OOZE_SWIRL_STRENGTH;

    float swell = fbm(warped * OOZE_GLOB_SCALE + vec2(t * 1.7, -t) + seedOffset(8.0));
    float detail = fbm(warped * OOZE_DETAIL_SCALE + vec2(-t * 3.0, t * 2.2) + seedOffset(9.0));
    float source = smoothstep(0.015, 0.22, crackMask + swell * 0.13);
    float liquid = crackMask + swell * OOZE_SWELL_AMOUNT + detail * 0.12;

    float body = smoothstep(OOZE_BODY_LOW, OOZE_BODY_HIGH, liquid) * source;
    oozeCore = smoothstep(OOZE_CORE_LOW, OOZE_CORE_HIGH, liquid) * body;
    oozeRim = (smoothstep(0.18, 0.55, body) - smoothstep(0.72, 1.0, body)) * body;
    oozeShine = smoothstep(0.68, 0.94, detail) * body * (1.0 - oozeCore * 0.45);
    return body;
}

float liquidCurrent(vec2 q, float oozeMask)
{
    vec2 p = cardSpace(q);
    float t = iTime * CURRENT_SPEED;
    float flowA = fbm(p * CURRENT_SCALE + vec2(t * 2.0, -t) + seedOffset(10.0));
    float flowB = fbm(p * CURRENT_SCALE * 1.7 + vec2(5.7 - t, 2.4 + t * 1.6) + seedOffset(11.0));
    float current = 1.0 - smoothstep(CURRENT_WIDTH, CURRENT_WIDTH * 2.3, abs(flowA - flowB));
    float pulse = 0.60 + 0.40 * sin(iTime * CURRENT_PULSE_SPEED + fbm(p * 6.0 + seedOffset(12.0)) * TWO_PI);
    return current * oozeMask * pulse * CURRENT_OPACITY;
}

float bubbleField(vec2 q, float oozeMask, out float bubbleCore, out float bubbleHighlight)
{
    vec2 p = cardSpace(q) * BUBBLE_SCALE + seedOffset(13.0);
    vec2 ip = floor(p);
    vec2 fp = fract(p);
    float rim = 0.0;
    bubbleCore = 0.0;
    bubbleHighlight = 0.0;

    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            vec2 cell = ip + vec2(float(x), float(y));
            vec2 rnd = hash22(cell);
            float life = fract(iTime * BUBBLE_SPEED + hash21(cell + seedOffset(14.0)));
            float wobble = sin(iTime * (0.8 + rnd.x) + rnd.y * TWO_PI + EFFECT_SEED) * 0.09;
            vec2 center = vec2(float(x), float(y)) + rnd - fp + vec2(wobble, life * 0.28);
            float radius = mix(BUBBLE_SIZE_MIN, BUBBLE_SIZE_MAX, hash21(cell + seedOffset(15.0)));
            radius *= 0.70 + 0.30 * sin(life * TWO_PI);

            float d = length(center);
            float localCore = 1.0 - smoothstep(radius * BUBBLE_CORE_FADE, radius, d);
            float localRim = 1.0 - smoothstep(BUBBLE_RIM_WIDTH, BUBBLE_RIM_WIDTH * 2.1, abs(d - radius));
            float localHighlight = 1.0 - smoothstep(radius * 0.10, radius * 0.42, length(center + vec2(radius * 0.30, -radius * 0.35)));

            float localOoze = smoothstep(0.18, 0.72, oozeMask);
            float spawn = smoothstep(0.28, 1.0, hash21(cell + seedOffset(16.0)));
            rim = max(rim, localRim * localOoze * spawn);
            bubbleCore = max(bubbleCore, localCore * localOoze * spawn);
            bubbleHighlight = max(bubbleHighlight, localHighlight * localOoze * spawn);
        }

    return rim * BUBBLE_AMOUNT;
}

float arcaneSparkle(vec2 q, vec2 crackId, float crackMask)
{
    vec2 p = q * ARCANE_DOT_SCALE + seedOffset(17.0);
    vec2 cell = floor(p);
    vec2 f = fract(p) - 0.5;
    float cellRand = hash21(cell + crackId * 1.31 + seedOffset(18.0));
    float sparse = smoothstep(ARCANE_DOT_SHARPNESS, 1.0, cellRand);
    float dotShape = 1.0 - smoothstep(0.03, 0.22, length(f));
    float twinkle = 0.45 + 0.55 * sin(iTime * ARCANE_SPARK_SPEED + cellRand * TWO_PI);
    return sparse * dotShape * twinkle * crackMask * ARCANE_SPARK_AMOUNT;
}

// ==============================================================
// MIST FIELD
// ==============================================================

float smokeField(vec2 p)
{
    float t = iTime;
    vec2 flow = vec2(MIST_SIDE_DRIFT * t, -MIST_RISE_SPEED * t);
    vec2 base = p * MIST_SCALE + flow;
    vec2 warp = vec2(
        fbm(base + vec2(0.0, t * MIST_SWIRL_SPEED) + seedOffset(19.0)),
        fbm(base + vec2(6.2, -t * MIST_SWIRL_SPEED) + seedOffset(20.0)));

    float body = fbm(base + warp * MIST_SWIRL_STRENGTH + seedOffset(21.0));
    float detail = fbm(base * MIST_DETAIL_SCALE + warp + seedOffset(22.0));
    return smoothstep(MIST_SOFT_LOW, MIST_SOFT_HIGH, mix(body, detail, 0.24));
}

float mistFromCracks(vec2 uv, float sd)
{
    vec2 q = cardUV(uv);
    vec2 sourceQ = clamp(q, 0.0, 1.0);

    float core;
    float rim;
    vec2 id;
    float sourceCrack = crackNetwork(sourceQ, core, rim, id);
    float oozeCore;
    float oozeRim;
    float oozeShine;
    float sourceOoze = oozeField(sourceQ, sourceCrack, oozeCore, oozeRim, oozeShine);
    float source = smoothstep(0.0, MIST_SOURCE_WIDTH, sourceOoze);

    vec2 p = cardSpace(sourceQ);
    float smoke = smokeField(p + vec2(sourceOoze * 0.23, oozeRim * 0.17));
    float insideMask = 1.0 - smoothstep(-MIST_INSIDE_FADE, 0.0, sd);

    return source * smoke * insideMask * MIST_INTENSITY;
}

// ==============================================================
// MAIN IMAGE
// ==============================================================

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / iResolution.xy;
    float sd = cardSDF(uv);
    float aa = max(EDGE_AA, 1.5 / iResolution.y);
    float cardMask = 1.0 - smoothstep(-aa, aa, sd);

    vec3 col = BG_COLOR;

    if (cardMask > 0.0)
    {
        vec2 q = clamp(cardUV(uv), 0.0, 1.0);
        vec3 card = sampleCard(q);
        float luma = dot(card, vec3(0.299, 0.587, 0.114));
        card = mix(card, vec3(luma), CARD_DESATURATION);

        vec2 centered = q - 0.5;
        float centerDist = length(vec2(centered.x * cardAspect(), centered.y));
        float edgeDark = 1.0 - smoothstep(0.0, CARD_CENTER_PRESERVE, centerDist);
        vec3 tint = mix(CARD_SHADOW_TINT, CARD_SICKLY_TINT, edgeDark);
        card = mix(card, card * tint + tint * 0.48, CARD_TINT_STRENGTH);
        card *= 1.0 - CARD_EDGE_DARKEN * (1.0 - edgeDark);

        float core;
        float rim;
        vec2 crackId;
        float cracks = crackNetwork(q, core, rim, crackId);
        float oozeCore;
        float oozeRim;
        float oozeShine;
        float ooze = oozeField(q, cracks, oozeCore, oozeRim, oozeShine);
        float bubbleCore;
        float bubbleHighlight;
        float bubbleRim = bubbleField(q, ooze, bubbleCore, bubbleHighlight);

        float flickerSeed = hash21(crackId + floor(iTime * 0.75) + seedOffset(23.0));
        float flicker = 1.0 - CRACK_FLICKER_DEPTH +
                        CRACK_FLICKER_DEPTH * sin(iTime * CRACK_FLICKER_SPEED + flickerSeed * TWO_PI);

        float halo = smoothstep(0.0, HALO_WIDTH, ooze) * (1.0 - oozeCore * 0.35);
        float relief = abs(ooze - fbm(cardSpace(q) * 10.0 + seedOffset(24.0)));
        float current = liquidCurrent(q, ooze);
        float spark = arcaneSparkle(q, crackId, oozeCore);

        card = mix(card, OOZE_DARK_PURPLE, ooze * OOZE_EDGE_SHADOW);
        card *= 1.0 - CRACK_DARKEN * oozeCore;
        card += OUTER_PURPLE * halo * HALO_BRIGHTNESS;
        card += INNER_PURPLE * ooze * RIM_BRIGHTNESS * flicker;
        card += CORE_PURPLE * oozeCore * CORE_BRIGHTNESS * flicker;
        card += CORE_PURPLE * current;
        card += CORE_PURPLE * spark;
        card += BUBBLE_RIM_COLOR * bubbleRim * flicker;
        card += INNER_PURPLE * bubbleCore * 0.25;
        card += vec3(1.0, 0.72, 1.0) * bubbleHighlight * BUBBLE_HIGHLIGHT;
        card += vec3(OOZE_SURFACE_SHINE) * oozeShine;
        card -= vec3(CRACK_EDGE_RELIEF) * relief * oozeRim * 0.08;

        col = mix(col, card, cardMask);
    }

    float mist = mistFromCracks(uv, sd) * cardMask;
    vec3 mistColor = mix(MIST_COLOR_LOW, MIST_COLOR_HIGH, mist);
    col += mistColor * mist;

    vec2 vignetteUv = uv - 0.5;
    col *= 1.0 - VIGNETTE_STRENGTH * dot(vignetteUv, vignetteUv);

    float grain = hash21(floor(fragCoord) + fract(iTime * 5.71) * 147.0 + seedOffset(25.0)) - 0.5;
    col += grain * GRAIN_AMOUNT;

    col *= EXPOSURE;
    col = col / (1.0 + col);

    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
