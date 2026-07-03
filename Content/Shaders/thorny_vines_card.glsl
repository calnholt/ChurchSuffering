/* ──────────────────────────────────────────────────────────────
   Thorny Vines — Two Fixed Diagonal Vines ShaderToy Version

   Simplified deterministic version:
   • No randomized layout constant.
   • Renders exactly two vines:
       1. Top-left to bottom-right
       2. Bottom-left to top-right
   • White triangular thorns for stronger thorn readability.
   • No center-opacity/readability fade; vines keep consistent opacity.

   Paste into shadertoy.com/new. Set iChannel0 to the card image (PNG/JPG).
   ────────────────────────────────────────────────────────────── */

// ═══════════════════════════════════════════════════════════════
// TUNABLES
// ═══════════════════════════════════════════════════════════════

// ── Card Region (0..1 screen space, y=0 bottom) ───────────────
const float CARD_LEFT   = 0.40;
const float CARD_RIGHT  = 0.60;
const float CARD_BOTTOM = 0.05;
const float CARD_TOP    = 0.50;
const float CARD_RADIUS = 0.04;

// ── Under-Tint ────────────────────────────────────────────────
const float CURSE_TINT_STR = 0.10;
const vec3  CURSE_TINT     = vec3(0.16, 0.27, 0.13);
const float EDGE_DARKEN    = 0.18;

// ── Vine Thickness ────────────────────────────────────────────
const float VINE_THICKNESS_A = 0.01;                  // top-left → bottom-right
const float VINE_THICKNESS_B = 0.01;                  // bottom-left → top-right
const float OUTLINE_EXTRA    = 0.00;                  // bold dark anime outline around vines
const float LINE_SOFT        = 0.0035;

// ── Two Diagonal Vines ────────────────────────────────────────
const float DIAGONAL_OPACITY = 1.0;
const float DIAGONAL_OVERSHOOT = 0.14;                 // extend slightly beyond card corners

// ── Squirm Tunables ───────────────────────────────────────────
const float SQUIRM_AMOUNT_A = 0.025;
const float SQUIRM_AMOUNT_B = 0.025;
const float SQUIRM_FREQ_A   = 7.0;
const float SQUIRM_FREQ_B   = 8.5;
const float SQUIRM_SPEED_A  = 0.18;
const float SQUIRM_SPEED_B  = 0.14;
const float SQUIRM_PHASE_B  = 2.35;

// ── Color Style ───────────────────────────────────────────────
const vec3  OUTLINE_COLOR  = vec3(0.010, 0.020, 0.010);
const vec3  VINE_DARK      = vec3(0.040, 0.095, 0.035);
const vec3  VINE_MID       = vec3(0.115, 0.210, 0.085);
const vec3  VINE_LIGHT     = vec3(0.245, 0.355, 0.160); // flat angular highlight
const float VINE_OPACITY   = 0.96;
const float VINE_SHADOW    = 0.30;

// ── Thorns ────────────────────────────────────────────────────
const int   THORNS_PER_VINE = 10;
const float THORN_LEN       = 0.050;
const float THORN_BASE      = 0.012;                   // triangular thorn base width
const vec3  THORN_WHITE     = vec3(0.940, 0.930, 0.865); // pale white thorn fill
const vec3  THORN_LIGHT     = vec3(1.000, 0.985, 0.920); // sharp white highlight

// ── Edge gripping/root texture ────────────────────────────────
const float EDGE_CREEP      = 0.075;
const float EDGE_ROOT_DENS  = 0.48;
const float EDGE_ROOT_SCALE = 35.0;

// ── Background (only seen outside the card region) ────────────
const vec3 BG_COLOR = vec3(0.045, 0.043, 0.040);

#define FBM_OCTAVES 5

const float PI = 3.14159265359;

// ═══════════════════════════════════════════════════════════════
// HASH / NOISE
// ═══════════════════════════════════════════════════════════════

float hash21(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
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

const mat2 FBM_ROT = mat2(0.80, 0.60, -0.60, 0.80);

float fbm(vec2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < FBM_OCTAVES; i++)
    {
        v += a * vnoise(p);
        p = FBM_ROT * p * 2.0;
        a *= 0.5;
    }
    return v;
}

float sat(float x) { return clamp(x, 0.0, 1.0); }

// ═══════════════════════════════════════════════════════════════
// CARD GEOMETRY / DISTANCE HELPERS
// ═══════════════════════════════════════════════════════════════

float cardSDF(vec2 uv)
{
    vec2 c = vec2((CARD_LEFT + CARD_RIGHT) * 0.5, (CARD_BOTTOM + CARD_TOP) * 0.5);
    vec2 h = vec2((CARD_RIGHT - CARD_LEFT) * 0.5, (CARD_TOP - CARD_BOTTOM) * 0.5) - CARD_RADIUS;
    vec2 d = abs(uv - c) - h;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - CARD_RADIUS;
}

vec2 cardLocal(vec2 uv)
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

vec2 aspectQ(vec2 p)
{
    p.x *= cardAspect();
    return p;
}

vec2 toAspectCentered(vec2 q)
{
    vec2 p = q - 0.5;
    p.x *= cardAspect();
    return p;
}

vec2 fromAspectCentered(vec2 p)
{
    return vec2(p.x / cardAspect(), p.y) + 0.5;
}

float sdSegmentQ(vec2 p, vec2 a, vec2 b)
{
    p = aspectQ(p);
    a = aspectQ(a);
    b = aspectQ(b);
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = sat(dot(pa, ba) / max(dot(ba, ba), 1e-5));
    return length(pa - ba * h);
}

// Tapered segment = triangular thorn. Wide at base, sharp at tip.
float taperedThornMask(vec2 p, vec2 a, vec2 b, float baseWidth, float tipWidth)
{
    vec2 pp = aspectQ(p);
    vec2 aa = aspectQ(a);
    vec2 bb = aspectQ(b);

    vec2 pa = pp - aa;
    vec2 ba = bb - aa;
    float h = sat(dot(pa, ba) / max(dot(ba, ba), 1e-5));
    float d = length(pa - ba * h);

    float w = mix(baseWidth, tipWidth, h);
    float finite = smoothstep(0.00, 0.08, h) * (1.0 - smoothstep(0.98, 1.0, h));
    return (1.0 - smoothstep(w, w + LINE_SOFT, d)) * finite;
}

// ═══════════════════════════════════════════════════════════════
// CARD COLOR SOURCE
// ═══════════════════════════════════════════════════════════════

vec3 sampleCard(vec2 uv)
{
    vec3 tex = texture(iChannel0, uv).rgb;

    // Standalone preview if no texture is bound.
    vec2 g = abs(fract(uv * 12.0) - 0.5);
    float line = smoothstep(0.45, 0.5, max(g.x, g.y));
    vec3 proc = mix(vec3(0.38, 0.23, 0.16), vec3(0.78, 0.70, 0.45), uv.y);
    proc = mix(proc, vec3(1.0), line * 0.22);

    float hasTex = step(0.01, dot(tex, vec3(1.0)));
    return mix(proc, tex, hasTex);
}

// ═══════════════════════════════════════════════════════════════
// TWO FIXED DIAGONAL VINES
// ═══════════════════════════════════════════════════════════════

float vineSquirm(float x, float amount, float freq, float speed, float phase)
{
    float t = iTime * speed;
    return amount * sin(x * freq + phase + t)
         + amount * 0.45 * sin(x * freq * 2.20 + phase * 1.71 - t * 0.80)
         + amount * 0.18 * sin(x * freq * 3.50 + phase * 0.63 + t * 1.35);
}

// diagonalSign = -1.0: top-left → bottom-right
// diagonalSign =  1.0: bottom-left → top-right
// Returns x=outline, y=stem, z=highlight/facet, w=thorn.
vec4 fixedDiagonalVine(vec2 q, float diagonalSign, float vineIndex)
{
    vec2 p = toAspectCentered(q);
    float halfAspect = cardAspect() * 0.5;

    vec2 a = vec2(-halfAspect - DIAGONAL_OVERSHOOT, -0.5 * diagonalSign - DIAGONAL_OVERSHOOT * diagonalSign);
    vec2 b = vec2( halfAspect + DIAGONAL_OVERSHOOT,  0.5 * diagonalSign + DIAGONAL_OVERSHOOT * diagonalSign);

    vec2 axis = b - a;
    float axisLen = length(axis);
    vec2 dir = axis / max(axisLen, 1e-5);
    vec2 nrm = vec2(-dir.y, dir.x);

    float along = dot(p - a, dir);
    float x01 = along / max(axisLen, 1e-5);
    float x = x01 * 2.0 - 1.0;

    float thick = mix(VINE_THICKNESS_A, VINE_THICKNESS_B, step(0.5, vineIndex));
    float amount = mix(SQUIRM_AMOUNT_A, SQUIRM_AMOUNT_B, step(0.5, vineIndex));
    float freq = mix(SQUIRM_FREQ_A, SQUIRM_FREQ_B, step(0.5, vineIndex));
    float speed = mix(SQUIRM_SPEED_A, SQUIRM_SPEED_B, step(0.5, vineIndex));
    float phase = vineIndex * SQUIRM_PHASE_B;

    float curve = vineSquirm(x, amount, freq, speed, phase);
    float d = abs(dot(p - a, nrm) - curve);

    // Only fade where the vine exits the card; no middle opacity logic.
    float endFade = smoothstep(0.00, 0.08, x01) * (1.0 - smoothstep(0.92, 1.00, x01));
    endFade = mix(0.45, 1.0, endFade);

    float outline = (1.0 - smoothstep(thick + OUTLINE_EXTRA, thick + OUTLINE_EXTRA + LINE_SOFT, d)) * endFade;
    float stem    = (1.0 - smoothstep(thick, thick + LINE_SOFT, d)) * endFade;

    float ridge = stem * (1.0 - smoothstep(0.0, max(thick * 0.42, 0.001), d));
    float facets = floor(fbm(q * 20.0 + vec2(vineIndex * 3.1, vineIndex * 1.7)) * 4.0) / 3.0;
    ridge *= 0.30 + 0.70 * facets;

    float thorn = 0.0;
    float thornOutline = 0.0;

    for (int j = 0; j < THORNS_PER_VINE; j++)
    {
        float fj = float(j);
        float thornX01 = (fj + 0.50) / float(THORNS_PER_VINE);
        float thornX = thornX01 * 2.0 - 1.0;
        float thornAlong = thornX01 * axisLen;
        float thornCurve = vineSquirm(thornX, amount, freq, speed, phase);

        // Alternate sides, with mirrored placement on the second diagonal.
        float side = mix(-1.0, 1.0, mod(fj + vineIndex, 2.0));
        float lengthWave = 0.75 + 0.25 * sin(fj * 1.73 + vineIndex * 2.17);
        float leanWave = sin(fj * 2.31 + vineIndex * 1.13);
        float len = THORN_LEN * lengthWave;

        vec2 baseP = a + dir * thornAlong + nrm * (thornCurve + side * thick * 0.44);
        vec2 tipP  = baseP + nrm * side * len + dir * (leanWave * 0.035);

        vec2 baseQ = fromAspectCentered(baseP);
        vec2 tipQ  = fromAspectCentered(tipP);

        float tEnd = smoothstep(0.00, 0.08, thornX01) * (1.0 - smoothstep(0.92, 1.00, thornX01));
        tEnd = mix(0.45, 1.0, tEnd);

        thornOutline += taperedThornMask(q, baseQ, tipQ, THORN_BASE + OUTLINE_EXTRA * 0.65, 0.003) * tEnd;
        thorn        += taperedThornMask(q, baseQ, tipQ, THORN_BASE, 0.0015) * tEnd;
    }

    return vec4(sat(outline + thornOutline), sat(stem), sat(ridge), sat(thorn)) * DIAGONAL_OPACITY;
}

vec4 diagonalVineField(vec2 q)
{
    vec4 vines = vec4(0.0);

    // Vine A: top-left to bottom-right.
    vines += fixedDiagonalVine(q, -1.0, 0.0);

    // Vine B: bottom-left to top-right.
    vines += fixedDiagonalVine(q, 1.0, 1.0);

    return clamp(vines, 0.0, 1.0);
}

// ═══════════════════════════════════════════════════════════════
// MAIN IMAGE
// ═══════════════════════════════════════════════════════════════

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / iResolution.xy;
    float aspect = iResolution.x / iResolution.y;
    vec2 ap = vec2(uv.x * aspect, uv.y);

    vec3 col = BG_COLOR;

    float sd = cardSDF(uv);
    float inCard = step(sd, 0.0);

    if (inCard > 0.5)
    {
        vec3 card = sampleCard(uv);
        vec2 q = cardLocal(uv);

        // Very light tint so the middle of the card remains clear.
        float centerClear = smoothstep(0.18, 0.34, min(min(q.x, 1.0 - q.x), min(q.y, 1.0 - q.y)));
        card = mix(card, CURSE_TINT, CURSE_TINT_STR * (1.0 - centerClear * 0.55));

        // Soft dark grip around the edge.
        float edgeAmt = smoothstep(-EDGE_CREEP, 0.0, sd);
        float rootNoise = fbm(ap * EDGE_ROOT_SCALE + vec2(iTime * 0.04, -iTime * 0.03));
        float root = edgeAmt * smoothstep(1.0 - EDGE_ROOT_DENS, 1.0, rootNoise);
        card *= 1.0 - edgeAmt * EDGE_DARKEN;
        card = mix(card, OUTLINE_COLOR, root * 0.45);

        // Exactly two fixed diagonal vines. No perimeter frame and no generated extra wraps.
        vec4 vines = diagonalVineField(q);

        float outline = vines.x;
        float stem    = vines.y;
        float hi      = vines.z;
        float thorn   = vines.w;

        card *= 1.0 - outline * VINE_SHADOW;
        card = mix(card, OUTLINE_COLOR, outline * 0.92);

        float facet = floor(fbm(q * 22.0 + iTime * 0.025) * 4.0) / 3.0;
        vec3 vineCol = mix(VINE_DARK, VINE_MID, 0.45 + 0.35 * facet);
        card = mix(card, vineCol, stem * VINE_OPACITY);
        card = mix(card, THORN_WHITE, thorn * 0.92);

        card += hi * VINE_LIGHT * 0.30;
        card += thorn * THORN_LIGHT * 0.10;

        col = card;
    }

    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
