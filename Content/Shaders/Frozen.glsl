/* ──────────────────────────────────────────────────────────────
   Frozen Card — ShaderToy

   Encases a card in a clear ice cube plus rising "cold breath" mist.
   Card stays READABLE: ice refracts/tints, never hides content.

   Paste into shadertoy.com/new. Set iChannel0 to the card image (PNG/JPG).
   A procedural placeholder shows if no texture is bound, so you can preview
   standalone. All tunables at the top.

   LAYOUT NOTE: The "card" is assumed to fill the lower portion of the view,
   with empty headroom above it for breath to rise into. Adjust CARD_TOP /
   CARD_BOTTOM / CARD_LEFT / CARD_RIGHT to match where your card sits.
   ────────────────────────────────────────────────────────────── */

// ═══════════════════════════════════════════════════════════════
// TUNABLES
// ═══════════════════════════════════════════════════════════════

// ── Card Region (0..1 screen space, y=0 bottom) ───────────────
// Defines the rectangle the ice covers. Breath rises above CARD_TOP.
const float CARD_LEFT   = 0.40;  // left edge of card
const float CARD_RIGHT  = 0.60;  // right edge of card
const float CARD_BOTTOM = 0.05;  // bottom edge of card
const float CARD_TOP    = 0.5;   // top edge of card (lower = more breath headroom)
const float CARD_RADIUS = 0.04;  // rounded-corner radius of the ice block

// ── Ice Clarity / Readability ─────────────────────────────────
const float ICE_TINT_STR  = 0.42;                    // blue tint over card (0 = invisible, 1 = opaque blue). KEEP LOW for readability
const vec3  ICE_TINT      = vec3(0.62, 0.80, 0.95);  // color of the ice tint (cyan-blue)
const float ICE_BRIGHTEN  = 0.06;                    // adds glassy lift to card (0 = none, raises = washed out)

// ── Refraction (the "looking through ice" warble) ────────────
const float REFRACT_AMT   = 0.0001;  // how much card content bends (0 = flat glass, high = unreadable). Keep small
const float REFRACT_SCALE = 20.0;    // size of refraction lumps (low = big waves, high = fine ripples)
const float REFRACT_SPEED = 0.15;    // slow internal shimmer speed

// ── Frost (cloudy edges) ──────────────────────────────────────
const float FROST_EDGE    = 0.1;                     // thickness of frosty border creeping in from card edge
const float FROST_DENSITY = 0.55;                    // how opaque/white the frost gets (0 = clear edge, 1 = solid white rim)
const float FROST_SCALE   = 2.0;                     // crystalline frost grain size (high = finer crystals)
const vec3  FROST_COLOR   = vec3(0.88, 0.94, 1.0);   // near-white frost color

// ── Surface Sparkle / Specular ────────────────────────────────
const float SPARKLE_AMT   = 0.45;   // brightness of glinting ice facets (0 = none)
const float SPARKLE_SCALE = 990.0;  // density of sparkle points (high = more tiny glints)
const float SPARKLE_SIZE  = 0.12;   // radius of each glint (low = pinpoint stars, high = soft blooms)
const float SPARKLE_SPEED = 1.5;    // twinkle rate

// ── Internal Cracks (3D beveled grooves, not flat lines) ────
const float CRACK_AMT     = 1.0;                     // master crack visibility (0 = none, scales whole effect)
const float CRACK_SCALE   = 10.0;                    // crack network size (low = few big cracks, high = web)
const vec2  CRACK_SEED    = vec2(3.0, 3.0);          // pattern seed ONLY — change to reshuffle which cracks/shards appear
const float CRACK_SHARP   = 43.0;                    // crack thinness (high = hairline, low = wide soft veins)
const float CRACK_DEPTH   = 2.2;                     // bevel steepness / how "carved" cracks feel (0 = flat, high = deep groove)
const float CRACK_LIGHT   = 0.35;                    // bright lit lip on the light-facing side of each crack
const float CRACK_SHADE   = 0.28;                    // dark shadow on the far side of each crack (gives relief)
const float CRACK_AO      = 0.80;                    // darkening inside the groove itself (depth / occlusion)
const vec3  CRACK_DEEP_TINT = vec3(0.30, 0.55, 0.80); // deep-ice blue tint in the bottom of grooves
const vec3  CRACK_LIGHT_DIR = vec3(-0.6, 0.7, 0.5);   // direction light hits the ice (x,y in-plane, z out toward viewer)

// ── Ice Facets (each cracked section = its own tilted ice plane) ──
// Voronoi shards get a random plane angle so each section reflects and
// refracts the card at a different tilt, like a shattered ice cube.
const float FACET_TILT    = 0.35;   // how differently each shard is angled (0 = uniform glass, high = chaotic panes)
const float FACET_REFRACT = 0.018;  // extra card displacement per shard from its tilt
const float FACET_REFLECT = 0.30;   // brightness shift per shard as its angle catches/misses the light
const float FACET_WARBLE  = 0.6;    // how much the smooth noise warble bends the shared surface normal

// ── Cold Breath (mist rising above the card) ──────────────────
const float BREATH_STR         = 0.85;               // overall breath opacity (0 = off)
const float BREATH_OFFSET      = -0.1;               // vertical gap between card top and mist start (negative = overlap card)
const float BREATH_HEIGHT      = 0.422;              // how far above the mist start the breath reaches (in screen units)
const float BREATH_WIDTH       = 1.0;                // breath source width vs card (1.0 = full card width)
const float BREATH_SPREAD      = 0.45;               // extra widening as mist rises (0 = straight column, high = fans out)
const float BREATH_EDGE_SOFT   = 0.35;               // side fade softness
const float BREATH_RISE        = 0.03;               // upward drift speed of mist
const float BREATH_SCALE       = 5.5;                // mist puff size (low = big clouds, high = wispy)
const float BREATH_SWIRL       = 1.8;                // curl/turbulence strength (0 = straight rising fog, high = churning vortices)
const float BREATH_SWIRL_SPEED = 1.6;                // how fast the swirls rotate/evolve over time
const float BREATH_PUFF        = 0.01;               // pulsing in/out cadence (0 = steady stream, high = visible breaths)
const vec3  BREATH_COLOR       = vec3(0.90, 0.95, 1.0); // breath fog color (cool white)

// ── Background (only seen outside the card region) ────────────
const vec3 BG_COLOR = vec3(0.04, 0.06, 0.10);  // dark cold backdrop behind everything

// ── Detail ────────────────────────────────────────────────────
#define FBM_OCTAVES 5  // noise detail for frost/breath (3 = soft, 7 = busy)

// ═══════════════════════════════════════════════════════════════
// HASH / NOISE
// ═══════════════════════════════════════════════════════════════

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
    float v = 0.0, a = 0.5;
    for (int i = 0; i < FBM_OCTAVES; i++)
    {
        v += a * vnoise(p);
        p = FBM_ROT * p * 2.0;
        a *= 0.5;
    }
    return v;
}

// Voronoi-style cell distance. Returns x = nearest-point distance,
// y = second-nearest (edge proximity).
vec2 voronoi(vec2 p)
{
    vec2 ip = floor(p);
    vec2 fp = fract(p);
    float d1 = 8.0, d2 = 8.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            vec2 g = vec2(float(x), float(y));
            vec2 o = hash22(ip + g);
            vec2 r = g + o - fp;
            float d = dot(r, r);
            if (d < d1)
            {
                d2 = d1;
                d1 = d;
            }
            else if (d < d2)
            {
                d2 = d;
            }
        }
    return vec2(sqrt(d1), sqrt(d2));
}

// Crack groove profile at ice-space point p.
// 0 = flat ice, toward 1 = center of a fracture line.
float crackProfile(vec2 p)
{
    vec2 v = voronoi(p * CRACK_SCALE + CRACK_SEED);
    float edge = v.y - v.x;
    return 1.0 - smoothstep(0.0, 1.0 / CRACK_SHARP, edge);
}

// Like voronoi(), but also returns the integer coordinate of the nearest cell.
vec2 voronoiId(vec2 p, out vec2 id)
{
    vec2 ip = floor(p);
    vec2 fp = fract(p);
    float d1 = 8.0, d2 = 8.0;
    vec2 bid = ip;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            vec2 g = vec2(float(x), float(y));
            vec2 o = hash22(ip + g);
            vec2 r = g + o - fp;
            float d = dot(r, r);
            if (d < d1)
            {
                d2 = d1;
                d1 = d;
                bid = ip + g;
            }
            else if (d < d2)
            {
                d2 = d;
            }
        }
    id = bid;
    return vec2(sqrt(d1), sqrt(d2));
}

// ═══════════════════════════════════════════════════════════════
// CARD GEOMETRY
// ═══════════════════════════════════════════════════════════════

// Signed distance to the rounded card rectangle (negative = inside).
float cardSDF(vec2 uv)
{
    vec2 c = vec2((CARD_LEFT + CARD_RIGHT) * 0.5, (CARD_BOTTOM + CARD_TOP) * 0.5);
    vec2 h = vec2((CARD_RIGHT - CARD_LEFT) * 0.5, (CARD_TOP - CARD_BOTTOM) * 0.5) - CARD_RADIUS;
    vec2 d = abs(uv - c) - h;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - CARD_RADIUS;
}

// ═══════════════════════════════════════════════════════════════
// CARD COLOR SOURCE
// ═══════════════════════════════════════════════════════════════

// Sample the card image; fall back to a procedural pattern if no
// texture is bound (so the shader previews standalone).
vec3 sampleCard(vec2 uv)
{
    vec3 tex = texture(iChannel0, uv).rgb;
    vec2 g = abs(fract(uv * 12.0) - 0.5);
    float line = smoothstep(0.45, 0.5, max(g.x, g.y));
    vec3 proc = mix(vec3(0.20, 0.35, 0.55), vec3(0.85, 0.80, 0.55), uv.y);
    proc = mix(proc, vec3(1.0), line * 0.25);
    float hasTex = step(0.01, dot(tex, vec3(1.0)));
    return mix(proc, tex, hasTex);
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

    // ── ICE BLOCK over the card ──────────────────────────────
    float sd = cardSDF(uv);
    float inIce = step(sd, 0.0);
    float edgeAmt = smoothstep(-FROST_EDGE, 0.0, sd);

    if (inIce > 0.5)
    {
        // One shared ice surface normal: cracks, facet tilt, and warble
        // all feed a single normal that drives refraction and lighting.

        vec2 facetId;
        vec2 vc = voronoiId(ap * CRACK_SCALE + CRACK_SEED, facetId);
        float cC = 1.0 - smoothstep(0.0, 1.0 / CRACK_SHARP, vc.y - vc.x);
        vec2 facetTilt = (hash22(facetId) - 0.5) * 2.0 * FACET_TILT;

        float eps = 1.5 / iResolution.y;
        float gC = crackProfile(ap);
        float gX = crackProfile(ap + vec2(eps, 0.0));
        float gY = crackProfile(ap + vec2(0.0, eps));
        vec2 grad = vec2(gX - gC, gY - gC) / eps;

        vec2 warp = vec2(
            fbm(ap * REFRACT_SCALE + iTime * REFRACT_SPEED),
            fbm(ap * REFRACT_SCALE + 17.0 - iTime * REFRACT_SPEED)
        ) - 0.5;

        vec2 tilt = facetTilt + warp * FACET_WARBLE + grad * CRACK_DEPTH * CRACK_AMT;
        vec3 n = normalize(vec3(tilt, 1.0));

        vec2 cardUV = uv + n.xy * REFRACT_AMT + facetTilt * FACET_REFRACT;
        vec3 card = sampleCard(cardUV);

        card = mix(card, ICE_TINT, ICE_TINT_STR);
        card += ICE_BRIGHTEN;

        float frostN = fbm(ap * FROST_SCALE);
        float frost = edgeAmt * FROST_DENSITY * (0.5 + 0.5 * frostN);
        card = mix(card, FROST_COLOR, clamp(frost, 0.0, 1.0));

        vec3 L = normalize(CRACK_LIGHT_DIR);
        float diff = dot(n, L);

        card += diff * FACET_REFLECT * (1.0 - cC);
        card = mix(card, CRACK_DEEP_TINT, cC * CRACK_AO);
        card += max(diff, 0.0) * cC * CRACK_LIGHT;
        card -= max(-diff, 0.0) * cC * CRACK_SHADE;

        float spec = pow(max(diff, 0.0), 24.0) * cC;
        card += spec * CRACK_LIGHT * 1.5;

        vec2 sv = voronoi(ap * SPARKLE_SCALE);
        float glint = pow(1.0 - smoothstep(0.0, SPARKLE_SIZE, sv.x), 6.0);
        glint *= 0.5 + 0.5 * sin(iTime * SPARKLE_SPEED + hash21(floor(ap * SPARKLE_SCALE)) * 6.28);
        card += glint * SPARKLE_AMT;

        col = card;
    }

    // ── COLD BREATH rising above the card ────────────────────
    float cardCX = (CARD_LEFT + CARD_RIGHT) * 0.5;
    float cardW = (CARD_RIGHT - CARD_LEFT);
    float breathBase = CARD_TOP + BREATH_OFFSET;

    if (uv.y > breathBase)
    {
        float yAbove = uv.y - breathBase;
        float yNorm = yAbove / BREATH_HEIGHT;

        float halfW = (cardW * 0.5 * BREATH_WIDTH) * (1.0 + yNorm * BREATH_SPREAD);
        float xDist = abs(uv.x - cardCX) / max(halfW, 1e-3);
        float xFall = 1.0 - smoothstep(1.0 - BREATH_EDGE_SOFT, 1.0, xDist);

        float yFall = (1.0 - smoothstep(0.0, 1.0, yNorm)) * smoothstep(0.0, 0.15, yNorm);

        vec2 mp = vec2(ap.x, uv.y) * BREATH_SCALE;
        mp.y -= iTime * BREATH_RISE * BREATH_SCALE * 10.0;

        vec2 sp = mp * 0.6 + vec2(0.0, iTime * BREATH_SWIRL_SPEED);
        float fa = fbm(sp);
        float fb = fbm(sp + vec2(4.7, 1.3));
        vec2 curl = vec2(fb - 0.5, 0.5 - fa);
        mp += curl * BREATH_SWIRL * (0.3 + yNorm);

        float mist = fbm(mp + fbm(mp * 0.5 + iTime * 0.05));
        mist = smoothstep(0.35, 0.9, mist);

        float pulse = 1.0 - BREATH_PUFF * (0.5 + 0.5 * sin(iTime * 1.6));
        float breath = mist * xFall * yFall * BREATH_STR * pulse;
        col = mix(col, BREATH_COLOR, clamp(breath, 0.0, 1.0));
    }

    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
