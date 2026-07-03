/* ──────────────────────────────────────────────────────────────
   VOLCANO EMBERS + HEAT HAZE — ShaderToy

   A fullscreen ATMOSPHERE overlay for a volcano / lava level:

   HEAT HAZE — warps the background (iChannel0) with a smooth,
   upward-travelling shimmer (hot air rises). Built from low-freq
   value-noise + a vertical wave, so it WOBBLES the image without ever
   looking grainy. Strongest near the ground, fading upward.

   FLOATING EMBERS — glowing orange specks that drift and rise across
   the whole frame, each with its own size, twinkle, sway and colour
   temperature. (This is the "sparks" particle system from the source
   fire shader, lifted out and rebuilt as a standalone, fully-tunable
   rising-ember field.)

   The fire / smoke / flame body of the source shader are intentionally
   NOT included — only the floating particles + the heat distortion.

   Paste the whole file into shadertoy.com/new and hit Alt+Enter.

   iChannel0: your volcano background image/video (set Filter = mipmap
   or linear, Wrap = clamp). Leave it empty and the shader previews on
   a procedural lava-glow gradient.

   Layout assumption: "down" = bottom of screen (uv.y = 0) is the heat
   source (lava/ground). Embers rise from there toward the top.

   EVERY knob lives in the TUNABLES block. The body is prose.
   ────────────────────────────────────────────────────────────── */

#define PI 3.14159265359
#define TAU 6.28318530718

// ═══════════════════════════════════════════════════════════════
// TUNABLES
// ═══════════════════════════════════════════════════════════════
// Comments name the ENDPOINTS — which way to push, what you'll see.

// ── Master ──────────────────────────────────────────────────────
const float TIME_SCALE = 1.0;  // global tempo: 0.5 = slow-mo lazy heat, 2.0 = frantic

// ════════════════════ HEAT HAZE (the warp) ════════════════════
// Distorts iChannel0. Kept SMOOTH on purpose — all displacement comes
// from low-frequency noise + a wave, never per-pixel randomness, so it
// shimmers without grain. If it ever looks noisy: lower HAZE_SCALE or
// HAZE_OCTAVES, never raise them.
const float HAZE_AMP       = 0.014;  // warp strength (uv units). 0 = no distortion, 0.04 = strong mirage. KEEP SMALL.
const float HAZE_SCALE     = 3.2;    // noise cell size. LOW = big slow heat blobs, HIGH = tight ripples (raising toward grain — be careful)
const float HAZE_RISE      = 0.55;   // how fast the heat field scrolls UPWARD (hot air rising). 0 = static shimmer
const float HAZE_WAVE_FREQ = 11.0;   // horizontal-shimmer wave: bands up the screen. LOW = broad waves, HIGH = many thin ripples
const float HAZE_WAVE_SPD  = 2.2;    // how fast that wave climbs the screen
const float HAZE_NOISE_MIX = 0.65;   // share of warp from organic noise (turbulent, irregular heat)
const float HAZE_WAVE_MIX  = 0.45;   // share of warp from the coherent vertical wave (classic mirage shimmer)
#define HAZE_OCTAVES 3  // haze noise detail. 2 = glassy smooth, 4 = more turbulent (still smooth). DO NOT go high — grain.

// Vertical reach: heat is strongest at the lava/ground and fades up.
const float HAZE_REACH = 1.10;  // how far up the screen distortion reaches (uv.y; 0.4 = only low band, 1+ = whole frame)
const float HAZE_FLOOR = 0.18;  // leftover shimmer at the very top (0 = dead calm up high, 1 = uniform everywhere)

// ════════════════════ FLOATING EMBERS ════════════════════
// Layered rising-particle field. _FAR / _NEAR pairs interpolate by each
// layer's depth (0 = far/distant .. 1 = near/foreground).

// ── Depth layers (parallax) ─────────────────────────────────────
#define EMBER_LAYERS 5  // depth slices far->near. 3 = sparse/cheap, 7 = thick swarm/heavier GPU
#define NEIGHBORHOOD 1   // cells scanned per pixel: 1 => 3x3 (lets jittered/swaying embers cross cell borders cleanly)

// ── Ember frequency per depth (cells across the screen) ─────────
const float SCALE_FAR  = 16.0;  // far — high => many tiny distant sparks
const float SCALE_NEAR =  5.0;  // near — low => few big foreground embers

// ── Ember size (radius in cell units, 0..0.5) ───────────────────
const float SIZE_FAR  = 0.030;  // distant spark radius — pinpricks
const float SIZE_NEAR = 0.090;  // foreground ember radius — fat glowing motes
const float SIZE_VAR  = 0.60;   // per-ember size spread (0 = all identical, 1 = wildly mixed)

// ── Density (fraction of cells that actually hold an ember) ─────
const float DENSITY_FAR  = 0.70;  // far — high => dense dust of sparks
const float DENSITY_NEAR = 0.32;  // near — lower keeps big foreground embers uncluttered

// ── Rise speed per depth (screen-heights/sec; near rises faster) ─
const float RISE_FAR  = 0.045;  // distant rise — slow lazy float
const float RISE_NEAR = 0.150;  // foreground rise — quicker ascent

// ── Sideways motion ─────────────────────────────────────────────
const float EMBER_DRIFT  = 0.010;  // steady breeze pushing embers sideways (sign flips L/R; 0 = straight up)
const float WANDER_AMP   = 0.040;  // thermal meander: whole columns wander (0 = ramrod vertical, hi = drunken weave)
const float WANDER_SCALE = 1.4;    // meander size — low = broad sweeps, high = jittery wiggle
const float WANDER_SPEED = 0.20;   // how fast the meander field evolves

// ── Per-ember sway (each ember wobbles on its own clock) ────────
const float SWAY_AMP      = 0.060;  // horizontal wobble width in cell units (0 = none)
const float SWAY_RATE_MIN   = 0.5;    // slowest wobble (embers that drift lazily)
const float SWAY_RATE_MAX   = 2.4;    // fastest wobble (embers that jitter)

// ── Twinkle (embers flaring & guttering). Rate AND phase per-ember ─
const float TWK_MIN_BRIGHT = 0.25;  // dimmest point of a flicker (0 = winks fully out, 1 = steady glow)
const float TWK_RATE_MIN   = 0.8;   // slowest flicker
const float TWK_RATE_MAX   = 5.0;   // fastest flicker (rapid sputter)

// ── Ember shape (soft glow + hot core) ──────────────────────────
const float EMBER_CORE  = 0.32;  // core size as fraction of radius — small = pinpoint white-hot center, large = uniform blob
const float HALO_GAIN   = 0.85;  // brightness of the soft outer glow
const float CORE_GAIN   = 1.30;  // extra punch on the hot center
const float EMBER_BLOOM = 0.35;  // additive overspill from the brightest cores (0 = matte, hi = glinty bloom)

// ── Ember colour (hot core -> cooling edge), with per-ember temp ─
const vec3 COL_CORE = vec3(1.00, 0.92, 0.62);  // white-hot center
const vec3 COL_HOT  = vec3(1.00, 0.48, 0.12);  // fresh orange ember (high temp roll)
const vec3 COL_COOL = vec3(0.75, 0.10, 0.02);  // cooling deep-red ember (low temp roll)

// ── Brightness over depth + height ──────────────────────────────
const float GAIN_FAR     = 0.45;  // distant embers dimmed by atmosphere
const float GAIN_NEAR    = 1.20;  // foreground embers full strength
const float EMBER_GAIN   = 1.0;   // master ember brightness
const float EMBER_TOP_DIM = 0.15;  // brightness up high (embers cool as they rise; 1 = no fade, 0 = dark by the top)
const float EMBER_FADE_LO = 0.10;  // uv.y where cooling starts (below this = full bright birth zone)
const float EMBER_FADE_HI = 1.05;  // uv.y where cooling reaches EMBER_TOP_DIM

// ════════════════════ PROCEDURAL FALLBACK BG ════════════════════
// Only shown when iChannel0 is empty, so the shader previews standalone.
const vec3  BG_TOP        = vec3(0.04, 0.02, 0.05);  // top of frame — dark smoky rock
const vec3  BG_BOT        = vec3(0.55, 0.12, 0.02);  // bottom of frame — lava glow
const float BG_GLOW_SCALE = 2.0;  // size of the mottled glow blobs in the fallback

// ═══════════════════════════════════════════════════════════════
// NOISE / HELPERS
// ═══════════════════════════════════════════════════════════════

float hash21(vec2 p)  // vec2 -> float [0,1)
{
    vec3 q = fract(vec3(p.xyx) * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return fract((q.x + q.y) * q.z);
}

vec2 hash22(vec2 p)  // vec2 -> vec2 [0,1)
{
    vec3 q = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return fract((q.xx + q.yz) * q.zy);
}

float vnoise(vec2 p)  // smooth value noise
{
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);  // smootherstep -> no faceting
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
               mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x), f.y);
}

const mat2 FBM_ROT = mat2(0.80, 0.60, -0.60, 0.80);

float fbm(vec2 p)
{
    float v = 0.0, a = 0.5;
    for (int i = 0; i < HAZE_OCTAVES; i++) {
        v += a * vnoise(p);
        p = FBM_ROT * p * 2.0;
        a *= 0.5;
    }
    return v;
}

// Smooth heat-haze displacement field (uv-space offset, pre-mask).
// p is aspect-corrected. Pure low-freq noise + a climbing wave => the
// shimmer is spatially smooth at any pixel density: never grainy.
vec2 heatField(vec2 p, float t)
{
    vec2 q = p * HAZE_SCALE + vec2(0.0, -t * HAZE_RISE);  // field scrolls UP (rising heat)
    vec2 organic = vec2(fbm(q), fbm(q + vec2(31.4, 17.7))) - 0.5;
    float wave = sin(p.y * HAZE_WAVE_FREQ - t * HAZE_WAVE_SPD);  // mirage band climbing the screen
    return organic * HAZE_NOISE_MIX + vec2(wave, 0.0) * HAZE_WAVE_MIX;
}

// ═══════════════════════════════════════════════════════════════
// MAIN IMAGE
// ═══════════════════════════════════════════════════════════════

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / iResolution.xy;
    float aspect = iResolution.x / iResolution.y;
    vec2 p = vec2(uv.x * aspect, uv.y);  // aspect-corrected -> round embers & heat cells
    float t = iTime * TIME_SCALE;

    // ── HEAT HAZE: warp the sampling UV, strongest low, fading up ──
    float hMask = mix(HAZE_FLOOR, 1.0, 1.0 - smoothstep(0.0, HAZE_REACH, uv.y));
    vec2 offset = heatField(p, t) * HAZE_AMP * hMask;
    vec2 warpUv = uv + vec2(offset.x / aspect, offset.y);  // /aspect keeps x-warp in true screen units

    // ── Background: warped iChannel0, or procedural lava fallback ──
    vec3 tex = texture(iChannel0, warpUv).rgb;
    float hasTex = step(0.01, dot(tex, vec3(1.0)));  // ~0 when no texture bound
    vec3 fbg = mix(BG_TOP, BG_BOT, pow(1.0 - uv.y, 1.5));  // gradient: glow at bottom
    fbg += BG_BOT * 0.35 * fbm(warpUv * BG_GLOW_SCALE + vec2(0.0, -t * 0.1)) * (1.0 - uv.y);  // soft mottled glow
    vec3 col = mix(fbg, tex, hasTex);

    // ── FLOATING EMBERS: layered rising particle field (additive) ──
    vec3 emberCol = vec3(0.0);   // ember light only (kept off the bg until faded)
    float emberLight = 0.0;      // accumulated brightness, for bloom

    for (int i = 0; i < EMBER_LAYERS; i++) {
        float depth = float(i) / float(EMBER_LAYERS - 1);  // 0 far .. 1 near
        float scale = mix(SCALE_FAR, SCALE_NEAR, depth);
        float radius = mix(SIZE_FAR, SIZE_NEAR, depth);
        float density = mix(DENSITY_FAR, DENSITY_NEAR, depth);
        float rise = mix(RISE_FAR, RISE_NEAR, depth);
        float gain = mix(GAIN_FAR, GAIN_NEAR, depth);

        // Move the layer: rise (up the screen), steady drift, thermal wander.
        vec2 wander = vec2(fbm(p * WANDER_SCALE + vec2(0.0, -t * WANDER_SPEED)) - 0.5, 0.0) * WANDER_AMP;
        vec2 sp = vec2(p.x + EMBER_DRIFT * t, p.y - rise * t) + wander;  // -rise*t => content moves UP
        vec2 lp = sp * scale;
        vec2 gid = floor(lp);
        vec2 gv = fract(lp) - 0.5;

        for (int oy = -NEIGHBORHOOD; oy <= NEIGHBORHOOD; oy++)
        for (int ox = -NEIGHBORHOOD; ox <= NEIGHBORHOOD; ox++) {
            vec2 cell = gid + vec2(float(ox), float(oy));
            vec2 seed = cell + vec2(float(i) * 41.3, float(i) * 19.7);  // unique per layer
            if (step(1.0 - density, hash21(seed + 7.13)) < 0.5) continue;  // density gate

            vec2 r1 = hash22(seed);           // position jitter
            vec2 r2 = hash22(seed + 11.37);   // r2.x sway rate, r2.y twinkle rate
            vec2 r3 = hash22(seed + 23.91);   // r3.x sway phase, r3.y twinkle phase

            // Home (jittered) + per-ember size + own-clock horizontal sway.
            vec2 home = vec2(float(ox), float(oy)) + (r1 - 0.5);
            float sizeR = radius * mix(1.0 - SIZE_VAR * 0.5, 1.0 + SIZE_VAR * 0.5, r1.x);
            home.x += SWAY_AMP * sin(t * mix(SWAY_RATE_MIN, SWAY_RATE_MAX, r2.x) + r3.x * TAU);
            float dist = length(gv - home);
            if (dist > sizeR) continue;  // cheap reject

            // Shape: soft halo to the rim, tight hot core in the middle.
            float dn = dist / sizeR;  // 0 center .. 1 rim
            float halo = smoothstep(1.0, 0.0, dn);
            float core = smoothstep(EMBER_CORE, 0.0, dn);

            // Twinkle: independent rate + phase so embers never sync up.
            float twk = mix(TWK_MIN_BRIGHT, 1.0, 0.5 + 0.5 * sin(t * mix(TWK_RATE_MIN, TWK_RATE_MAX, r2.y) + r3.y * TAU));

            // Per-ember colour temperature, then whiten toward the core.
            vec3 ec = mix(COL_COOL, COL_HOT, hash21(seed + 53.1));
            ec = mix(ec, COL_CORE, core);

            float a = (halo * HALO_GAIN + core * CORE_GAIN) * twk * gain;
            emberCol += ec * a * EMBER_GAIN;
            emberLight += a;
        }
    }

    // Cooling-with-height fade (embers dim as they rise), then add to bg.
    // Applied ONLY to ember light — the background is never darkened.
    float vfade = mix(1.0, EMBER_TOP_DIM, smoothstep(EMBER_FADE_LO, EMBER_FADE_HI, uv.y));
    emberCol += COL_CORE * EMBER_BLOOM * smoothstep(0.8, 2.0, emberLight);  // bloom on hottest cores
    col += emberCol * vfade;

    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
