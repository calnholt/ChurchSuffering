/* ──────────────────────────────────────────────────────────────
   Layered Holes — ShaderToy

   Three-layer image compositor with growing/closing circular "holes".

   Top layer (iChannel0) covers the screen. Holes open up across it;
   each hole reveals EITHER the middle layer (iChannel1) or the bottom
   layer (iChannel2) — assigned per hole, re-rolled every time the hole
   respawns.

   Each hole has a life: radius eases up from 0 to a max, wobbles while
   open, then eases back to 0 and stays closed for a bit before
   respawning in a new spot with a new layer pick. Many holes run at once
   on staggered clocks.

   The hole PERIMETER is the feature: it's domain-warped (wobbly,
   organic), softly feathered, and the feather width itself varies around
   the rim. All three of those live in the FEATHER / RIM block — tune
   there first.

   Paste into shadertoy.com.

   Bind:
     iChannel0 = top image (the surface you punch holes in)
     iChannel1 = middle image (revealed by some holes)
     iChannel2 = bottom image (revealed by the others)

   ────────────────────────────────────────────────────────────── */

// ═══════════════════════════════════════════════════════════════════════
// TUNABLES
// ═══════════════════════════════════════════════════════════════════════

#define NUM_HOLES 30  // how many hole-slots run at once (more = busier)

// ── Hole lifecycle (timing) ──
const float HOLE_PERIOD_MIN = 10.0;  // shortest full cycle sec (open+closed) — fast turnover
const float HOLE_PERIOD_MAX = 20.0;  // longest full cycle sec — slow, lazy turnover
const float HOLE_LIFE_MIN   = 0.45;  // min fraction of the cycle the hole is OPEN
const float HOLE_LIFE_MAX   = 0.75;  // max open fraction (remainder = closed gap before respawn)
const float HOLE_OPEN_FRAC  = 0.25;  // grow-in portion of the open time (0->full). bigger = slower bloom
const float HOLE_CLOSE_FRAC = 0.30;  // shrink-out portion. bigger = slower close

// ── Hole size ──
const float HOLE_R_MIN       = 0.10;  // smallest max-radius (1.0 = screen height). tiny peepholes
const float HOLE_R_MAX       = 0.5;   // largest max-radius. big windows
const float RADIUS_FLUX_AMP  = 0.12;  // radius wobble while open (0 = steady disc, 0.3 = breathing)
const float RADIUS_FLUX_RATE = 2.2;   // wobble speed (low = slow swell, high = jittery)
const float HOLE_MARGIN      = 0.02;  // keep centers this far from screen edges

// ── FEATHER / RIM <- the important block ──
const float HOLE_FEATHER   = 0.045;  // base soft-edge width (0 = hard cut, 0.12 = very smoky)
const float FEATHER_VARY   = 0.70;   // how much the feather width varies AROUND the rim
                                       // 0 = uniform softness, 1 = some arcs crisp, some hazy
const float RIM_WARP_AMP   = 0.340;  // perimeter distortion — pushes the edge in/out (0 = perfect circle)
const float RIM_WARP_SCALE = 3.5;    // distortion frequency (low = big lobes, high = fine crinkle)
const float RIM_WARP_SPEED = 0.35;   // how fast the wobble crawls (0 = frozen warp)
const float REVEAL_REFRACT = 0.35;   // sideways smear of the revealed layer AT the rim only
                                       // (0 = clean reveal, high = glassy refracted lip)

// ── Layer assignment ──
const float LAYER_SPLIT = 0.50;  // < this fraction of holes reveal MIDDLE (iCh1), rest reveal BOTTOM (iCh2)
                                  // 0.0 = all show bottom, 1.0 = all show middle

// ── Reveal look ──
const float REVEAL_DARKEN = 0.00;  // darken revealed layers (0 = none, 0.5 = sunk in shadow)

// ═══════════════════════════════════════════════════════════════════════
// NOISE / HELPERS
// ═══════════════════════════════════════════════════════════════════════

float hash11(float n) { return fract(sin(n) * 43758.5453123); }

float hash21(vec2 p)
{
    p = fract(p * vec2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return fract(p.x * p.y);
}

// value noise
float vnoise(vec2 x)
{
    vec2 p = floor(x);
    vec2 f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(p + vec2(0.0, 0.0));
    float b = hash21(p + vec2(1.0, 0.0));
    float c = hash21(p + vec2(0.0, 1.0));
    float d = hash21(p + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

const mat2 FBM_MTX = mat2(0.80, 0.60, -0.60, 0.80);

float fbm(vec2 p)
{
    float f = 0.0, amp = 0.5;
    for (int i = 0; i < 5; i++) {
        f += amp * vnoise(p);
        p = FBM_MTX * p * 2.02;
        amp *= 0.5;
    }
    return f / 0.96875;
}

// Domain-warped 2D displacement field — the organic rim wobble.
// Returns a centered vector (~ -0.5..0.5 per axis) that pushes the edge around.
vec2 warpField(vec2 p, float t)
{
    float a = fbm(p + vec2(0.0, 0.0) + t * 0.10);
    float b = fbm(p + vec2(5.2, 1.3) - t * 0.13);
    vec2 q = vec2(a, b);
    float c = fbm(p + 4.0 * q + vec2(1.7, 9.2) + t * 0.11);
    float d = fbm(p + 4.0 * q + vec2(8.3, 2.8) + t * 0.09);
    return vec2(c, d) - 0.5;
}

// ═══════════════════════════════════════════════════════════════════════
// MAIN IMAGE
// ═══════════════════════════════════════════════════════════════════════

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / iResolution.xy;  // 0..1, for texture fetches
    float aspect = iResolution.x / iResolution.y;
    vec2 auv = vec2(uv.x * aspect, uv.y);  // aspect-corrected, for round circles + noise

    vec3 layer0 = texture(iChannel0, uv).rgb;
    // (middle/bottom sampled per-hole below, possibly with refracted uv)
    vec3 col = layer0;

    // global rim-warp displacement, computed once and shared by every hole
    vec2 disp = warpField(auv * RIM_WARP_SCALE, iTime * RIM_WARP_SPEED) * RIM_WARP_AMP;
    vec2 dispUv = vec2(disp.x / aspect, disp.y);  // same displacement back in uv-space
    float fVary = fbm(auv * RIM_WARP_SCALE + 31.7);  // 0..1 field that varies feather around the rim

    for (int i = 0; i < NUM_HOLES; i++) {
        float fid = float(i);

        // lifecycle clock
        float period = mix(HOLE_PERIOD_MIN, HOLE_PERIOD_MAX, hash11(fid * 1.7 + 0.3));
        float phase = hash11(fid * 3.1 + 0.9) * period;
        float cycle = floor((iTime + phase) / period);  // which respawn we're on
        float local = mod(iTime + phase, period);
        float openDur = period * mix(HOLE_LIFE_MIN, HOLE_LIFE_MAX, hash11(fid * 5.3 + cycle));
        if (local > openDur) continue;  // hole is closed this part of the cycle

        float t = local / openDur;  // 0..1 open progress
        float grow = smoothstep(0.0, HOLE_OPEN_FRAC, t);
        float close = 1.0 - smoothstep(1.0 - HOLE_CLOSE_FRAC, 1.0, t);
        float env = grow * close;  // 0 at birth/death, 1 mid-life

        float maxR = mix(HOLE_R_MIN, HOLE_R_MAX, hash11(fid * 7.7 + cycle));
        float flux = 1.0 + RADIUS_FLUX_AMP * sin(iTime * RADIUS_FLUX_RATE + fid * 2.399);
        float radius = maxR * env * flux;
        if (radius <= 0.0) continue;

        // position (re-rolled each respawn), in aspect-corrected space
        float cx = mix(HOLE_MARGIN, aspect - HOLE_MARGIN, hash11(fid * 11.1 + cycle));
        float cy = mix(HOLE_MARGIN, 1.0 - HOLE_MARGIN, hash11(fid * 13.3 + cycle));
        vec2 center = vec2(cx, cy);

        // warped distance -> wobbly, distorted perimeter
        float d = distance(auv + disp, center);

        // feather width varies around the rim (some arcs crisp, some hazy)
        float fw = max(HOLE_FEATHER * (1.0 + FEATHER_VARY * (fVary - 0.5)), 1e-3);

        // reveal mask: 1 inside the hole, feathered to 0 across the rim band
        float m = 1.0 - smoothstep(radius - fw, radius + fw, d);
        if (m <= 0.0) continue;

        // refraction smear that peaks exactly at the feathered edge
        float rim = m * (1.0 - m) * 4.0;  // 0 in center & outside, 1 at edge
        vec2 ruv = uv + dispUv * REVEAL_REFRACT * rim;

        // per-hole layer pick (middle vs bottom), re-rolled each respawn
        float pick = hash11(fid * 17.7 + cycle);
        vec3 revealed = (pick < LAYER_SPLIT)
            ? texture(iChannel1, ruv).rgb
            : texture(iChannel2, ruv).rgb;
        revealed *= (1.0 - REVEAL_DARKEN);

        col = mix(col, revealed, m);
    }

    fragColor = vec4(col, 1.0);
}
