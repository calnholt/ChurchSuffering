/* ──────────────────────────────────────────────────────────────
   SNOWSTORM — ShaderToy

   Fullscreen realistic snowstorm with WIND SWOOSHES and real 6-point
   ice crystals up close.

   Snow rides a turbulent curl-noise WIND FLOW, so neighbouring flakes
   follow shared, curving currents — sheets of snow that swoosh across
   screen, not parallel diagonal lines.

   A fast BLOWING-SNOW SHEET layer streaks past and surges on gusts
   (the actual whoosh).

   NEAR flakes are detailed 6-fold SNOWFLAKE CRYSTALS that tumble and
   vary per-flake (polar-symmetry + lineSegment SDF). FAR flakes stay
   soft specks — at storm distance you can't resolve crystal arms, and
   it keeps the frame cheap. Split = CRYSTAL_MIN.

   Depth-of-field melts out-of-focus crystals into soft bokeh.

   Paste the whole file into shadertoy.com/new and hit Alt+Enter.

   iChannel0: OPTIONAL.
     empty -> procedural dusk winter sky (previews standalone)
     image -> snow becomes an overlay on that image

   No vignette (intentionally omitted, by request).

   Credits/technique: parallax falling-snow after Andrew Baldwin's
   "Just Snow" (ShaderToy ldsGDn); 6-point crystal symmetry from the
   user's procedural-snowflake SDF; rebuilt as a tunable control panel
   with curl-flow advection, blowing-snow sheets, flutter, twinkle, DoF.

   EVERY knob lives in the TUNABLES block. The body is prose.
   ────────────────────────────────────────────────────────────── */

#define PI 3.14159265359
#define TAU 6.28318530718

// ═══════════════════════════════════════════════════════════════
// TUNABLES
// ═══════════════════════════════════════════════════════════════
// Comments name the ENDPOINTS — which way to push, what you see.
// _FAR / _NEAR pairs interpolate by each layer's depth (0 far .. 1 near).

// ── Depth layers (the parallax) ────────────────────────────────
#define SNOW_LAYERS 6          // depth slices far->near. 4 = lighter/cheaper, 8 = denser/heavier GPU
#define NEIGHBORHOOD 1          // cells scanned per pixel: 1 => 3x3 (jittered flakes cross borders cleanly)

// ── Flake frequency per depth (cells across the screen) ────────
const float SCALE_FAR  = 24.0;  // far — high => many tiny distant specks
const float SCALE_NEAR =  5.0;  // near — low => few big foreground crystals

// ── Flake size (radius in cell units, 0..0.5) ──────────────────
const float SIZE_FAR       = 0.040;  // distant speck radius — pinpricks
const float SIZE_NEAR      = 0.190;  // foreground crystal radius — big lacy flakes
const float FLAKE_JITTER   = 0.85;   // 0 = grid-locked, 1 = scattered freely in the cell
const float FLAKE_SIZE_VAR = 0.55;   // per-flake size spread (0 = all clones, 1 = wildly mixed sizes)

// ── Density (fraction of cells holding a flake) ────────────────
const float DENSITY_FAR  = 0.95;  // far — high => dense dust of specks
const float DENSITY_NEAR = 0.45;  // near — lower keeps foreground crystals uncluttered

// ── Fall speed per depth (screen-heights/sec; near faster) ─────
const float FALL_FAR   = 0.08;  // distant fall — slow lazy drift
const float FALL_NEAR  = 0.45;  // foreground fall — quick
const float TIME_SCALE = 1.0;   // master tempo: 0.5 = slow-mo, 2.0 = frantic

// ── WIND FLOW — turbulent currents that make snow SWOOSH ────────
// This is THE knob that kills the "straight diagonal lines" look.
const float FLOW_STRENGTH  = 0.1;   // how hard currents drag the snow (0 = straight lines = novice; 0.6+ = wild swirls)
const float FLOW_SCALE     = 1.3;   // current size — low = big sweeping sheets, high = tight little eddies
const float FLOW_SCROLL_X  = 0.10;  // currents drift sideways at this speed
const float FLOW_SCROLL_Y  = 0.16;  // currents travel downward (ride the snowfall)
const float FLOW_DEPTH_MIN = 0.40;  // how much FAR layers feel the currents vs near (near = full)

// ── Prevailing wind (steady drift + surging gusts on top) ──────
const float WIND_DRIFT    = 0.14;  // steady sideways push (sign flips L/R; 0 = dead vertical)
const float WIND_GUST     = 0.40;  // gust strength — extra sideways heave
const float WIND_GUST_RATE = 0.21; // gust frequency — low = long slow heaves, high = choppy
const float WIND_PARALLAX = 1.0;   // extra wind on near flakes vs far (0 = uniform)

// ── BLOWING-SNOW SHEETS (translucent streaks whipping past = swoosh) ─
const vec3  SHEET_COL      = vec3(0.86, 0.90, 0.97);  // colour of the streaking veils
const float SHEET_BASE     = 0.05;  // always-on sheet visibility (0 = none)
const float SHEET_GUST     = 0.26;  // extra sheets surging in on gusts (the whoosh peaks)
const float SHEET_SCALE    = 1.7;   // streak field freq — low = broad veils, high = fine spray
const float SHEET_STRETCH  = 7.0;   // streak length along wind (1 = blobs, high = long ribbons)
const float SHEET_SPEED    = 1.9;   // how fast sheets whip past (raise for violent blowing)
const float SHEET_LO       = 0.42;  // ribbon threshold low ) widen gap = softer/fewer
const float SHEET_HI       = 0.86;  // ribbon threshold high ) narrow = sharper/brighter ribbons

// ── Per-flake flutter (each flake wobbles on its own clock) ────
const float SWAY_AMP       = 0.16;  // flutter width in cell units (0 = none)
const float SWAY_RATE_MIN  = 0.8;   // slowest flutter
const float SWAY_RATE_MAX  = 2.6;   // fastest flutter

// ── Twinkle (flakes catching light). Rate AND phase per-flake ──
const float TWK_MIN_BRIGHT = 0.40;  // dimmest point (0 = blinks fully off)
const float TWK_RATE_MIN   = 0.6;   // slowest twinkle
const float TWK_RATE_MAX   = 4.0;   // fastest twinkle
const float TWK_DEPTH_BIAS = 1.4;   // 1 = far & near twinkle equally; >1 = near sparkles more

// ── FOREGROUND CRYSTALS (near flakes = real 6-point snowflakes) ─
const float CRYSTAL_MIN    = 0.60;  // depth above which flakes are crystals (0 = ALL = stylised; 1 = none = all specks)
const float CRYSTAL_ARM    = 0.80;  // arm length within the flake (0.5 stubby, 0.95 long lacy arms)
const float CRYSTAL_THICK  = 0.060; // arm thickness in local space (thin lacy vs chunky)
const float CRYSTAL_SPIN_MIN = -0.5; // slowest tumble (rad/sec; sign = spin direction)
const float CRYSTAL_SPIN_MAX =  0.5; // fastest tumble
const float CRYSTAL_VARIETY  = 0.40; // per-flake arm-length randomness (0 = identical, 1 = no two alike)

// ── Depth of field (bokeh; also melts out-of-focus crystals) ───
const float DOF_FOCUS  = 0.85;  // depth that's crisp (0 = far sharp, 1 = nearest sharp). High => crystals sharp.
const float DOF_SPREAD = 0.10;  // how far out-of-focus flakes balloon into soft discs
const float EDGE_SHARP = 0.010; // edge softness AT focus — small = crisp rim
const float EDGE_SOFT  = 0.110; // edge softness FAR from focus — large = melty blob

// ── Brightness / colour ────────────────────────────────────────
const float FLAKE_GAIN   = 1.25;  // master flake brightness
const float SPARKLE_GLOW = 0.30;  // additive bloom on the brightest cores (0 = matte, hi = glinty)
const vec3  FLAKE_COL_FAR  = vec3(0.60, 0.69, 0.85);  // distant tint — cool, bluish, dim
const vec3  FLAKE_COL_NEAR = vec3(0.93, 0.97, 1.00);  // near tint — bright cold white
const float FAR_FADE     = 0.55;  // how much atmosphere dims far flakes (0 = none, 1 = far ~black)

// ── Sky (DUSK BLIZZARD default — swap for day/night) ───────────
// Daytime whiteout : TOP vec3(0.62,0.65,0.70) BOT vec3(0.86,0.88,0.92)
// Night street     : TOP vec3(0.01,0.02,0.04) BOT vec3(0.10,0.09,0.07) (+ warm FLAKE_COL_NEAR)
const vec3  SKY_TOP      = vec3(0.03, 0.05, 0.13);  // top of frame — deep slate blue
const vec3  SKY_BOT      = vec3(0.12, 0.15, 0.22);  // bottom of frame — lighter cold haze
const float SKY_GRADIENT = 1.0;   // 0 = flat, 1 = full top-dark / bottom-light

// ── Blizzard haze (drifting whiteout veil; moderate => flakes visible) ─
const vec3  HAZE_COL   = vec3(0.55, 0.60, 0.68);
const float HAZE_BASE  = 0.10;  // baseline haze (0 = clear, 0.4 = thick soup)
const float HAZE_GUST  = 0.14;  // extra haze on gusts (whiteout surges)
const float HAZE_SCALE = 2.2;   // haze blob size — low = big banks, high = wispy
const float HAZE_DRIFT = 0.06;  // haze drift speed

// ── Finishing ──────────────────────────────────────────────────
const float DITHER = 0.012;  // tiny noise to kill sky banding (0 = off). NOT a vignette.

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

float vnoise(vec2 p)
{
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
               mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x), f.y);
}

#define FBM_OCTAVES 4  // flow/sheet/haze detail (3 = soft & cheap, 6 = fine & costly)
const mat2 FBM_ROT = mat2(0.80, 0.60, -0.60, 0.80);

float fbm(vec2 p)
{
    float v = 0.0, a = 0.5;
    for (int i = 0; i < FBM_OCTAVES; i++) {
        v += a * vnoise(p);
        p = FBM_ROT * p * 2.0;
        a *= 0.5;
    }
    return v;
}

mat2 rot(float a)
{
    float c = cos(a), s = sin(a);
    return mat2(c, -s, s, c);
}

// Curl of an fbm potential -> divergence-free (swirly, mass-conserving) wind.
// Same field for every layer => neighbouring flakes share a current => sheets.
vec2 curlFlow(vec2 q, float t)
{
    vec2 sp = q * FLOW_SCALE + vec2(t * FLOW_SCROLL_X, t * FLOW_SCROLL_Y);
    float e = 0.12;
    float x1 = fbm(sp + vec2(0.0, e)), x2 = fbm(sp - vec2(0.0, e));
    float y1 = fbm(sp + vec2(e, 0.0)), y2 = fbm(sp - vec2(e, 0.0));
    return vec2(x1 - x2, -(y1 - y2)) / (2.0 * e);  // perpendicular gradient
}

// Line-segment coverage SDF (from the user's snowflake). 1 on the line, 0 off.
float lineSeg(vec2 p, vec2 a, vec2 b, float th)
{
    vec2 pa = p - a, ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    float d = length(pa - ba * h);
    return smoothstep(th, th * 0.4, d);  // falloff scales with thickness
}

// 6-fold snowflake crystal. q in flake-local space (~[-1,1]); arms reach ~armLen.
float crystalMask(vec2 q, float armLen, float th)
{
    float ang = atan(q.y, q.x);
    float r = length(q);
    float w = PI / 3.0;  // 60 deg wedge -> 6-fold symmetry
    float a = abs(mod(ang, w) - w * 0.5);  // fold + mirror into one wedge
    vec2 p = vec2(cos(a), sin(a)) * r;  // rebuild symmetric coords
    float m = 0.0;
    m += lineSeg(p, vec2(0.0), vec2(armLen, 0.0), th);  // main spine
    m += lineSeg(p, vec2(armLen * 0.28, 0.0), vec2(armLen * 0.50, armLen * 0.26), th * 0.7);  // branch 1
    m += lineSeg(p, vec2(armLen * 0.52, 0.0), vec2(armLen * 0.72, armLen * 0.20), th * 0.6);  // branch 2
    m += lineSeg(p, vec2(armLen * 0.74, 0.0), vec2(armLen * 0.90, armLen * 0.12), th * 0.5);  // tip branch
    m += smoothstep(0.18, 0.13, r);  // hex-ish core
    return clamp(m, 0.0, 1.0);
}

// ═══════════════════════════════════════════════════════════════
// MAIN IMAGE
// ═══════════════════════════════════════════════════════════════

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / iResolution.xy;
    float aspect = iResolution.x / iResolution.y;
    vec2 p = vec2(uv.x * aspect, uv.y);  // aspect-corrected -> round flakes
    float t = iTime * TIME_SCALE;

    // Surging gust shared by all layers (two desynced sines -> non-repeating heave)
    float gust = WIND_GUST * (0.6 * sin(t * WIND_GUST_RATE) + 0.4 * sin(t * WIND_GUST_RATE * 2.3 + 1.7));
    float gustNorm = 0.5 + 0.5 * sin(t * WIND_GUST_RATE * 1.3 + 0.5);  // 0..1 for sheet/haze surges

    // ── Background sky, or iChannel0 if an image is bound ──
    vec3 sky = mix(SKY_TOP, SKY_BOT, mix(0.5, uv.y, SKY_GRADIENT));
    vec3 tex = texture(iChannel0, uv).rgb;
    float hasTex = step(0.01, dot(tex, vec3(1.0)));
    vec3 col = mix(sky, tex, hasTex);

    // ── Blizzard haze (drifting fbm veil, surges on gusts) ──
    vec2 hp = p * HAZE_SCALE + vec2(t * HAZE_DRIFT + gust * 0.3, -t * 0.02);
    float hazeAmt = (HAZE_BASE + HAZE_GUST * gustNorm) * fbm(hp);
    col = mix(col, HAZE_COL, clamp(hazeAmt, 0.0, 1.0));

    // ── Turbulent wind flow: one field, reused by every layer (=> coherent sheets) ──
    vec2 flow = curlFlow(p, t) * FLOW_STRENGTH;

    // ── BLOWING-SNOW SHEETS: stretched fbm aligned to wind, scrolling fast ──
    // Rotate so travel maps to +y, compress along travel => long streaks, then scroll.
    float windAng = atan(WIND_DRIFT + gust, FALL_NEAR);
    vec2 rp = rot(-windAng) * (p + flow * 0.4) * SHEET_SCALE;
    rp.y = rp.y / SHEET_STRETCH + t * SHEET_SPEED;
    float ribbon = smoothstep(SHEET_LO, SHEET_HI, fbm(rp));
    float sheetAmt = ribbon * (SHEET_BASE + SHEET_GUST * gustNorm);
    col = mix(col, SHEET_COL, clamp(sheetAmt, 0.0, 1.0));

    // ── Snow layers: render FAR -> NEAR so near alpha-over (occludes) far ──
    for (int i = 0; i < SNOW_LAYERS; i++) {
        float depth = float(i) / float(SNOW_LAYERS - 1);  // 0 far .. 1 near
        bool isCrystal = depth >= CRYSTAL_MIN;

        float scale = mix(SCALE_FAR, SCALE_NEAR, depth);
        float radius = mix(SIZE_FAR, SIZE_NEAR, depth);
        float density = mix(DENSITY_FAR, DENSITY_NEAR, depth);
        float fall = mix(FALL_FAR, FALL_NEAR, depth);

        // Depth of field
        float blur = abs(depth - DOF_FOCUS) / max(DOF_FOCUS, 1.0 - DOF_FOCUS);  // 0..1
        float bokehR = radius + DOF_SPREAD * blur;
        float bokehDim = (radius / bokehR) * (radius / bokehR);
        float edge = min(mix(EDGE_SHARP, EDGE_SOFT, blur), bokehR * 0.95);

        // Move layer: prevailing wind + fall + the shared turbulent current
        float windScreen = WIND_DRIFT * (1.0 + WIND_PARALLAX * depth) * t + gust * (0.4 + depth * WIND_PARALLAX);
        vec2 warp = flow * mix(FLOW_DEPTH_MIN, 1.0, depth);
        vec2 sp = vec2(p.x + windScreen, p.y + fall * t) + warp;
        vec2 lp = sp * scale;
        vec2 gid = floor(lp);
        vec2 gv = fract(lp) - 0.5;

        float cover = 0.0;
        vec3 flakeCol = vec3(0.0);

        for (int oy = -NEIGHBORHOOD; oy <= NEIGHBORHOOD; oy++)
        for (int ox = -NEIGHBORHOOD; ox <= NEIGHBORHOOD; ox++) {
            vec2 cell = gid + vec2(float(ox), float(oy));
            vec2 seed = cell + vec2(float(i) * 37.2, float(i) * 17.7);
            if (step(1.0 - density, hash21(seed + 7.13)) < 0.5) continue;

            vec2 r1 = hash22(seed);           // position jitter
            vec2 r2 = hash22(seed + 11.37);   // r2.x flutter rate, r2.y twinkle rate
            vec2 r3 = hash22(seed + 23.91);   // r3.x flutter phase, r3.y twinkle phase

            // Home position (jittered) + per-flake size + flutter
            vec2 fpos = vec2(float(ox), float(oy)) + (r1 - 0.5) * FLAKE_JITTER;
            float sizeR = radius * mix(1.0 - FLAKE_SIZE_VAR * 0.5, 1.0 + FLAKE_SIZE_VAR * 0.5, r1.x);
            fpos.x += SWAY_AMP * sin(t * mix(SWAY_RATE_MIN, SWAY_RATE_MAX, r2.x) + r3.x * TAU);
            vec2 rel = gv - fpos;
            float dist = length(rel);
            if (dist > sizeR + bokehR) continue;  // cheap reject

            // Shape: real crystal up close, soft speck far away
            float shape;
            if (isCrystal) {
                float armLen = CRYSTAL_ARM * mix(1.0 - CRYSTAL_VARIETY * 0.5, 1.0 + CRYSTAL_VARIETY * 0.5, hash21(seed + 91.7));
                float spin = mix(CRYSTAL_SPIN_MIN, CRYSTAL_SPIN_MAX, hash21(seed + 57.3));
                vec2 local = rot(-(r3.x * TAU + spin * t)) * (rel / sizeR);  // tumble about flake centre
                float cm = crystalMask(local, armLen, CRYSTAL_THICK);
                float disc = smoothstep(sizeR + bokehR - radius, (sizeR + bokehR - radius) - edge, dist);
                shape = mix(cm, disc, blur);  // out-of-focus crystals melt to bokeh
            } else {
                float fR = sizeR + (bokehR - radius);
                shape = smoothstep(fR, fR - edge, dist);
            }
            if (shape <= 0.0) continue;

            // Twinkle (independent rate + phase; near varies more)
            float twkRaw = 0.5 + 0.5 * sin(t * mix(TWK_RATE_MIN, TWK_RATE_MAX, r2.y) + r3.y * TAU);
            float twk = mix(TWK_MIN_BRIGHT, 1.0, twkRaw);
            twk = clamp(1.0 - (1.0 - twk) * mix(1.0, TWK_DEPTH_BIAS, depth), 0.0, 1.0);

            float a = shape * bokehDim * twk;
            if (a > cover) {
                cover = a;
                flakeCol = mix(FLAKE_COL_FAR, FLAKE_COL_NEAR, depth);
            }
        }

        cover *= FLAKE_GAIN * mix(1.0 - FAR_FADE, 1.0, depth);
        cover = clamp(cover, 0.0, 1.0);
        col = mix(col, flakeCol, cover);  // alpha-over
        col += flakeCol * SPARKLE_GLOW * smoothstep(0.6, 1.0, cover);  // glint on bright cores
    }

    col += (hash21(fragCoord + fract(t) * 100.0) - 0.5) * DITHER;  // de-band (not a vignette)
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
