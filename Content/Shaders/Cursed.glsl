/* ──────────────────────────────────────────────────────────────
   Cursed Rising Circles — ShaderToy

   A riff on purple_rising_circles.glsl. The heavy volumetric fog is
   gone — this is just purple orbs rising up a dark void, restyled as
   "cursed energy": writhing, unstable balls of malevolent light that
   crawl up the card from below.

   The distortion is deliberately NON-GRAINY. Two smooth layers, no
   high-frequency static:
   1. A slow domain-warp ripples the whole field (space itself bends,
      like heat-haze off the energy).
   2. Each orb's edge writhes via polar sine lobes — flame/tentacle
      tendrils that breathe and curl as it rises.

   Both are driven by LOW-frequency smooth noise / sines, so you get
   liquid, cursed motion instead of TV snow. (Push the *_FREQ knobs
   too high and you reintroduce grain — the comments flag where that
   line is.)

   Each orb also flickers on its own clock (unstable energy), trails
   a faint comet tail below it, and glows with a magenta core inside
   a deep-violet aura.

   Paste into shadertoy.com/new and hit Alt+Enter.
   iChannel0: not used — previews standalone on its own dark void.

   Layout: screen normalised to uv in [-1,1] on Y; X is aspect-corrected
   so orbs stay round (X range ~ +/-1.78 on 16:9).

   All tunables live in the TUNABLES block. The body is prose.
   ────────────────────────────────────────────────────────────── */

// ═══════════════════════════════════════════════════════════════
// TUNABLES
// ═══════════════════════════════════════════════════════════════
// Comments name the ENDPOINTS — which way to push, what you see.

// ── Cursed palette ─────────────────────────────────────────────
const vec3 VOID_BOTTOM  = vec3(0.05, 0.01, 0.10);  // void colour low — warmer black-violet near the floor
const vec3 VOID_TOP     = vec3(0.015, 0.00, 0.04); // void colour high — near-black indigo up top (raise for less depth)
const vec3 ENERGY_CORE  = vec3(0.70, 0.18, 0.95);  // orb CORE hue — hot magenta-violet; raise for more searing centres
const vec3 ENERGY_AURA  = vec3(0.28, 0.03, 0.55);  // orb AURA hue — deep cursed violet halo; lower G/R for colder, B-heavy dread
const vec3 HAZE_COLOR   = vec3(0.18, 0.02, 0.34);  // faint rising background haze tint (only shows if BG_HAZE_GAIN > 0)

// ── The void backdrop ──────────────────────────────────────────
const float BG_HAZE_GAIN  = 0.18;  // strength of the smooth rising haze (0 = pure void, higher = misty energy field). KEEP LOW so orbs pop
const float BG_HAZE_FREQ  = 1.40;  // haze scale — low = big slow swells, high = busier. Push too high = grainy, defeats the point
const float BG_HAZE_DRIFT = 0.10;  // how fast the haze creeps UP (0 = frozen, higher = faster rise)
const float VIGNETTE      = 0.35;  // corner darkening (0 = flat, higher = tighter spotlight on centre, more claustrophobic)

// ── Orb population ─────────────────────────────────────────────
#define ORB_COUNT 50                   // number of cursed orbs (more = denser swarm; raises GPU cost linearly)
const float ORB_SIZE_MIN = 0.025;      // smallest orb radius (uv units) — distant motes
const float ORB_SIZE_MAX = 0.090;      // largest orb radius — also the brightness reference (biggest = brightest)

// ── Orb BOUNDS — vertical travel (where they rise in / vanish) ──
const float ORB_BAND_BOTTOM = -1.35;   // orbs enter at this uv.y (set below screen so they rise in)
const float ORB_BAND_TOP    =  1.35;   // orbs exit at this uv.y (set above screen so they rise out)
const float ORB_BAND_FADE   =  0.45;   // fade-in/out distance at each bound so orbs don't pop on/off

// ── Orb BOUNDS — horizontal spread (where they appear across X) ─
const float ORB_SPREAD_LEFT  = -1.80;  // leftmost uv.x an orb can occupy
const float ORB_SPREAD_RIGHT =  1.80;  // rightmost uv.x an orb can occupy (narrow the pair = a tight cursed column)

// ── Orb motion ─────────────────────────────────────────────────
const float ORB_RISE_MIN      = 0.05;  // slowest riser (screen-bands/sec) — lazy, oppressive crawl
const float ORB_RISE_MAX      = 0.16;  // fastest riser — quicker climbers (gap min->max = how varied the speeds read)
const float ORB_SWAY_AMP      = 0.07;  // horizontal wobble width as it rises (0 = dead-straight columns)
const float ORB_SWAY_RATE_MIN = 0.35;  // slowest sway
const float ORB_SWAY_RATE_MAX = 1.10;  // fastest sway (min!=max so orbs never drift in lockstep)

// ── Cursed distortion 1 — GLOBAL field warp (NON-GRAINY) ───────
// Bends the whole sampling space so the orbs ripple together, like the
// air around them is corrupted. Smooth low-freq noise = liquid, not grain.
const float WARP_AMP   = 0.10;  // how far space bends (0 = rigid, higher = soupy, melting field). Too high = orbs smear into mush
const float WARP_FREQ  = 1.30;  // warp scale — LOW = big lazy undulation, HIGH = tight ripples. >3.0 starts to look grainy; stay low
const float WARP_DRIFT = 0.18;  // how fast the warp itself churns/rises (0 = static bend, higher = active writhing)

// ── Cursed distortion 2 — per-orb TENDRIL edges (NON-GRAINY) ───
// Each orb's outline is pushed in/out by smooth angular sine lobes, so
// it writhes like a knot of energy instead of being a clean disc.
const float EDGE_AMP       = 0.28;  // tendril depth (0 = perfect circle, higher = deeper spikes/arms). >0.6 can pinch the orb apart
const float EDGE_LOBES     = 5.0;   // number of arms/tendrils around the rim (low = blobby lobes, high = spiky star)
const float EDGE_DETAIL    = 1.45;  // 2nd-harmonic strength — adds asymmetry/curl so arms aren't a tidy flower (0 = smooth lobes)
const float EDGE_RATE_MIN  = 0.6;   // slowest edge-writhe speed
const float EDGE_RATE_MAX  = 1.8;   // fastest edge-writhe (min!=max so every orb squirms on its own clock)

// ── Orb glow, aura & tail ──────────────────────────────────────
const float CORE_SOFT  = 0.40;  // core edge blur as a FRACTION of radius (0 = hard disc, 1 = no solid core, all haze)
const float AURA_REACH = 0.55;  // halo spread as a fraction of radius — low = tight rim-light, high = wide radiating dread
const float AURA_GAIN  = 0.90;  // halo brightness vs the core (0 = bare discs, higher = drenched in glow)
const float ORB_TRAIL  = 1.55;  // downward comet tail: 1.0 = round halo, <1 stretches the glow below into a rising streak

// ── Instability (the "cursed" pulse) ──────────────────────────
const float FLICKER_DEPTH    = 0.25;  // per-orb brightness flicker (0 = steady, 1 = guttering on/off like a dying ember)
const float FLICKER_RATE_MIN = 1.5;   // slowest flicker
const float FLICKER_RATE_MAX = 6.0;   // fastest flicker (min!=max so the swarm shimmers chaotically, not in unison)
const float PULSE_DEPTH      = 0.12;  // global "breathing" of all energy (0 = constant, higher = the whole curse swells)
const float PULSE_RATE       = 1.80;  // breathing speed

// ── Detail ────────────────────────────────────────────────────
#define FBM_OCTAVES 4  // noise detail for warp/haze (low octave count keeps motion soft, not grainy)

// ═══════════════════════════════════════════════════════════════
// NOISE / HELPERS
// ═══════════════════════════════════════════════════════════════

// vec2 -> float in [0,1). Cheap deterministic randomness.
float hash21(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

// Consecutive integer seeds (0,1,2,...) -> very different values, for per-orb props.
float hashScalar(float s)
{
    return fract(sin(s * 12.9898 + 78.233) * 43758.5453);
}

// Smooth value noise — smootherstep interpolation keeps it non-grainy.
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

// Few-octave fbm — low octave count on purpose so the warp/haze stay soft.
float fbm(vec2 p)
{
    float v = 0.0, a = 0.5;
    for (int i = 0; i < FBM_OCTAVES; i++)
    {
        v += a * vnoise(p);
        p = FBM_ROT * p * 2.0;
        a *= 0.5;
    }
    return v;
}

// Smooth domain-warp of a coordinate — this is the cursed "space bends" layer.
// Returns the warped uv; the warp field itself drifts upward over time.
vec2 cursedWarp(vec2 uv)
{
    float t = iTime * WARP_DRIFT;
    vec2 q = vec2(
        fbm(uv * WARP_FREQ + vec2(0.0, -t)),
        fbm(uv * WARP_FREQ + vec2(7.3, 2.1) - t));
    return uv + (q - 0.5) * 2.0 * WARP_AMP; // (q-0.5)*2 -> [-1,1] push
}

// Dark void backdrop with a faint, smooth rising haze and a vignette.
vec3 voidBackdrop(vec2 uv)
{
    float g = smoothstep(-1.0, 1.0, uv.y); // 0 at floor -> 1 up top
    vec3 col = mix(VOID_BOTTOM, VOID_TOP, g);

    // smooth haze, drifting UP (subtract from y to move features upward)
    float t = iTime * BG_HAZE_DRIFT;
    float h = fbm(uv * BG_HAZE_FREQ + vec2(0.0, -t));
    h = smoothstep(0.45, 0.95, h); // tighten into wisps
    col += HAZE_COLOR * h * BG_HAZE_GAIN;
    col *= 1.0 - VIGNETTE * dot(uv, uv) * 0.4; // radial corner darkening
    return col;
}

// ═══════════════════════════════════════════════════════════════
// MAIN IMAGE
// ═══════════════════════════════════════════════════════════════

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy * 2.0 - 1.0; // -> [-1,1]
    uv.x *= iResolution.x / iResolution.y;               // aspect-correct -> round orbs

    // ── The void ──
    vec3 color = voidBackdrop(uv);

    // ── Bend space (global non-grainy warp). Orbs are sampled in warped uv. ──
    vec2 wuv = cursedWarp(uv);

    // ── Rising cursed orbs ──
    vec3 energy = vec3(0.0);
    for (int i = 0; i < ORB_COUNT; i++)
    {
        float index = float(i) / float(ORB_COUNT);
        float rnd  = hashScalar(float(i) + 1.0);   // size / rise seed
        float rndB = hashScalar(float(i) + 101.0); // flicker seed
        float rndC = hashScalar(float(i) + 211.0); // edge-writhe seed
        float phase  = rnd * 6.28318;
        float radius = mix(ORB_SIZE_MIN, ORB_SIZE_MAX, rnd);
        float speed  = mix(ORB_RISE_MIN, ORB_RISE_MAX, rnd);

        // vertical travel: bottom -> top, wrapping; index staggers the start
        float yT = fract(iTime * speed + index);
        vec2 pos;
        pos.y = mix(ORB_BAND_BOTTOM, ORB_BAND_TOP, yT);

        // horizontal slot across the spread, plus a per-orb sway
        float xr = 0.5 + 0.5 * sin(index * rnd * 1000.0);
        pos.x = mix(ORB_SPREAD_LEFT, ORB_SPREAD_RIGHT, xr);
        float swayRate = mix(ORB_SWAY_RATE_MIN, ORB_SWAY_RATE_MAX, rndB);
        pos.x += ORB_SWAY_AMP * sin(iTime * swayRate + phase);

        // vector from orb centre (in warped space)
        vec2 q = wuv - pos;
        float ang = atan(q.y, q.x);
        float dCore = length(q);

        // comet tail: stretch the halo downward (below the orb) as it rises
        vec2 qt = q;
        qt.y = (qt.y < 0.0) ? qt.y * ORB_TRAIL : qt.y;
        float dAura = length(qt);

        // tendril edge — smooth polar sine lobes writhe the outline
        float eRate = mix(EDGE_RATE_MIN, EDGE_RATE_MAX, rndC);
        float wob = sin(ang * EDGE_LOBES + iTime * eRate + phase)
                  + EDGE_DETAIL * sin(ang * EDGE_LOBES * 2.0 - iTime * eRate * 1.27 + phase * 2.1);
        wob /= (1.0 + EDGE_DETAIL); // renormalise to ~[-1,1]
        float rmod = radius * (1.0 + EDGE_AMP * wob);

        // solid-ish writhing core
        float blur = max(radius * CORE_SOFT, 1e-4);
        float core = smoothstep(rmod, rmod - blur, dCore);

        // radiating halo (exponential falloff, with the stretched tail)
        float aura = exp(-dAura / (radius * AURA_REACH));

        // per-orb instability flicker (range [1-depth, 1])
        float flickRate = mix(FLICKER_RATE_MIN, FLICKER_RATE_MAX, rndB);
        float flick = 1.0 - FLICKER_DEPTH * (0.5 + 0.5 * sin(iTime * flickRate + phase * 3.0));

        // fade in at the bottom bound, out at the top (no pop on wrap)
        float edgeFade = smoothstep(ORB_BAND_BOTTOM, ORB_BAND_BOTTOM + ORB_BAND_FADE, pos.y)
                       * smoothstep(ORB_BAND_TOP, ORB_BAND_TOP - ORB_BAND_FADE, pos.y);

        float weight = radius / ORB_SIZE_MAX; // bigger orbs read brighter
        vec3 orbCol = mix(ENERGY_AURA, ENERGY_CORE, core); // violet halo -> magenta core
        energy += orbCol * (aura * AURA_GAIN + core) * flick * edgeFade * weight;
    }

    // global cursed "breathing" of all the energy at once
    float pulse = 1.0 + PULSE_DEPTH * sin(iTime * PULSE_RATE);
    color += energy * pulse;

    fragColor = vec4(color, 1.0);
}
