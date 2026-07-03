/* ──────────────────────────────────────────────────────────────
   PURPLE ZAPS — ShaderToy

   Electric purple lightning filaments crackling over a background image.
   Repurposed from a rainbow chaotic-zap shader:

   the rainbow output is collapsed to a single ZAP INTENSITY, then
   recolored as purple (deep-violet glow + hot lilac core)

   the zaps are composited ADDITIVELY over iChannel0, so the background
   stays fully visible in the dark gaps between bolts

   the source's delicate iterated-map constants are kept inline (marked
   "structural" — changing them collapses the pattern); only the safe,
   look-shaping dials are exposed as TUNABLES.

   Paste the whole file into shadertoy.com/new and hit Alt+Enter.

   iChannel0: bind ANY image/video for the background. Leave it empty
   and a dark indigo gradient previews in its place.

   Layout: full-screen. Zaps fill the frame; density is set by ZOOM.

   EVERY look knob lives in the TUNABLES block.
   ────────────────────────────────────────────────────────────── */

// ═══════════════════════════════════════════════════════════════
// TUNABLES
// ═══════════════════════════════════════════════════════════════
// Comments name the ENDPOINTS — which way to push, what you see.

// ── Framing / density ──────────────────────────────────────────
const float ZOOM       = 0.10;  // coord scale — LOW (0.1) = big sweeping bolts, HIGH (0.4) = many tight filaments
const float ZAP_WARP   = 1.50;  // lens warp of the filaments — low = straighter streaks, high = more curled/folded
const float ZAP_SWIRL  = 9.00;  // cross-axis twist — low = aligned streaks, high = more turbulent, web-like tangle
const float ZAP_GROWTH = 0.02;  // per-step frequency growth — low = coarser bolts, high = finer crackle (small moves, big effect)

// ── Motion (compile-time loop count + speed) ───────────────────
#define ZAP_STEPS 24  // iterations / detail — 19 = source look; lower (12) = simpler+faster, higher (26) = busier+slower
const float ZAP_SPEED = 1.00;  // animation rate — 0 = frozen, 1 = source, >1 = frantic flicker, negative = runs backward

// ── Zap brightness / what counts as a bolt ─────────────────────
const float ZAP_FLOOR = 0.55;  // dim cutoff — RAISE to keep only the brightest bolts (cleaner bg), LOWER to reveal faint haze
const float ZAP_GAIN  = 1.60;  // master zap brightness after the floor — raise for blown-out glow, lower for subtle sparks

// ── Colors ─────────────────────────────────────────────────────
const vec3  ZAP_GLOW_COLOR = vec3(0.35, 0.05, 0.70);  // outer halo hue — the deep purple of the bolt's spread
const vec3  ZAP_CORE_COLOR = vec3(0.85, 0.60, 1.00);  // hot core hue — bright lilac/white-violet at the brightest centers
const float ZAP_CORE_LO    = 0.80;  // intensity where the glow STARTS shifting toward the core color
const float ZAP_CORE_HI    = 2.00;  // intensity where it's FULLY the core color (LO/HI pair: tighten = harder hot center)

// ── Compositing over the background ────────────────────────────
const float BG_DIM = 0.40;  // how much a bolt darkens the bg beneath it (0 = pure additive, 1 = bolts fully replace bg -> punchier purple)

// ── Background fallback (only used when iChannel0 is empty) ─────
const vec3 BG_FALLBACK_TOP = vec3(0.05, 0.02, 0.12);  // top of the placeholder gradient (deep indigo)
const vec3 BG_FALLBACK_BOT = vec3(0.00, 0.00, 0.00);  // bottom of the placeholder gradient (black)

// ═══════════════════════════════════════════════════════════════
// MAIN IMAGE
// ═══════════════════════════════════════════════════════════════

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    // ── Background: iChannel0 with a procedural fallback ──
    vec2 suv = fragCoord / iResolution.xy;
    vec3 tex = texture(iChannel0, suv).rgb;
    float hasTex = step(0.01, dot(tex, vec3(1.0)));  // ~0 when nothing is bound
    vec3 placeholder = mix(BG_FALLBACK_BOT, BG_FALLBACK_TOP, suv.y);
    vec3 bg = mix(placeholder, tex, hasTex);

    // ── Zap field: chaotic iterated map (ported from the source) ──
    // Constants below with no tunable are STRUCTURAL — the map only
    // resolves into bolts at these values; nudging them breaks it.
    vec2 u = ZOOM * (2.0 * fragCoord - iResolution.xy) / iResolution.y;
    vec2 v = iResolution.xy;
    const vec4 z = vec4(1.0, 2.0, 3.0, 0.0);  // per-channel phase seed
    vec4 o = z;
    float a = 0.5;
    float t = iTime * ZAP_SPEED;

    for (int i = 1; i < ZAP_STEPS; i++) {
        float fi = float(i);
        a += ZAP_GROWTH;
        t += 1.0;  // per-step frequency march (structural)
        v = cos(t - 7.0 * u * pow(a, fi)) - 5.0 * u;
        u *= mat2(cos(fi + 0.02 * t - z.wxzw * 11.0));  // per-step rotation of the sample field
        u += tanh(40.0 * dot(u, u) * cos(1e2 * u.yx + t)) / 2e2
           + 0.2 * a * u
           + cos(4.0 / exp(dot(o, o) / 1e2) + t) / 3e2;
        // accumulate filament brightness (the (1+cos(z+t)) term is the
        // original rainbow weighting — harmless now, we take luminance below)
        o += (1.0 + cos(z + t)) / length((1.0 + fi * dot(v, v)) * sin(ZAP_WARP * u / (0.5 - dot(u, u)) - ZAP_SWIRL * u.yx + t));
    }

    // source tone-map: bright filaments -> ~2, gaps -> ~0
    o = 25.6 / (min(o, 13.0) + 164.0 / o) - dot(u, u) / 250.0;

    // ── Collapse to a single intensity, then recolor purple ──
    float zap = dot(o.rgb, vec3(1.0 / 3.0));  // scalar zap strength
    zap = max(zap - ZAP_FLOOR, 0.0) * ZAP_GAIN;  // clip dim haze, scale
    float coreMix = smoothstep(ZAP_CORE_LO, ZAP_CORE_HI, zap);
    vec3 zapCol = mix(ZAP_GLOW_COLOR, ZAP_CORE_COLOR, coreMix);

    // ── Composite over the background ──
    vec3 col = bg * (1.0 - clamp(zap * BG_DIM, 0.0, 1.0)) + zapCol * zap;
    fragColor = vec4(col, 1.0);
}
