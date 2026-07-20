/*
   Poison Card - ShaderToy

   A card covered by thick, translucent poison slime. Metaball blobs roam,
   merge, and separate while the ooze drips downward. The shared field drives
   thickness, refraction, wet highlights, and translucency as one surface.

   Paste into shadertoy.com/new. Bind iChannel0 to a card image. If no image
   is bound, a procedural card placeholder is shown.
*/

// TUNABLES
const float CARD_LEFT = 0.29;
const float CARD_RIGHT = 0.71;
const float CARD_BOTTOM = 0.16;
const float CARD_TOP = 0.88;
const float CARD_RADIUS = 0.035;

const float BLOB_FREQ = 6.0;
const float BLOB_STRETCH = 1.50;
const float BLOB_REACH_MIN = 0.55;
const float BLOB_REACH_MAX = 0.95;
const float THRESH_LO = 0.15;
const float THRESH_HI = 0.90;
const float BLOB_WOBBLE = 0.58;
const float BLOB_WOBBLE_SCALE = 0.70;
const float FLOW_SPEED = 0.10;
const float BOB_AMP = 0.42;
const float BOB_SPEED = 0.45;

const float REFRACT_AMT = 0.16;
const float ALPHA_THIN = 0.35;
const float ALPHA_THICK = 0.90;
const float ABSORB_STR = 0.90;
const vec3 ABSORB_COLOR = vec3(0.30, 0.80, 0.34);
const vec3 SLIME_SURFACE = vec3(0.55, 0.85, 0.30);
const vec3 SLIME_DEEP = vec3(0.00, 0.26, 0.08);
const vec3 LIGHT_DIR = vec3(-0.40, 0.65, 0.80);
const float AMBIENT = 0.38;
const float DIFFUSE = 0.75;
const float SPEC_POWER = 46.0;
const float SPEC_INTENSITY = 0.95;
const float RIM_POWER = 2.6;
const float RIM_INTENSITY = 0.28;
const float NORMAL_EPS = 0.006;
const float NORMAL_STRENGTH = 2.6;
const float BUMP_SCALE = 3.0;
const float BUMP_STRENGTH = 0.12;
#define FBM_OCTAVES 5
#define SEARCH_CELLS 2

// HASH / NOISE
float hash21(vec2 p) { p = fract(p * vec2(123.34, 345.45)); p += dot(p, p + 34.345); return fract(p.x * p.y); }
vec2 hash22(vec2 p) { p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3))); return fract(sin(p) * 43758.5453); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p), u = f * f * (3.0 - 2.0 * f); float a = hash21(i), b = hash21(i + vec2(1.0, 0.0)), c = hash21(i + vec2(0.0, 1.0)), d = hash21(i + vec2(1.0, 1.0)); return mix(mix(a, b, u.x), mix(c, d, u.x), u.y); }
float fbm(vec2 p) { float sum = 0.0, amp = 0.5; for (int i = 0; i < FBM_OCTAVES; i++) { sum += amp * noise(p); p = mat2(0.88, -0.48, 0.48, 0.88) * p * 2.0; amp *= 0.5; } return sum; }

// CARD / GOO
float roundedBox(vec2 p, vec2 b, float r) { vec2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
vec2 cardUV(vec2 uv) { return clamp((uv - vec2(CARD_LEFT, CARD_BOTTOM)) / vec2(CARD_RIGHT - CARD_LEFT, CARD_TOP - CARD_BOTTOM), 0.0, 1.0); }
float gooField(vec2 p) {
    vec2 gp = vec2(p.x, p.y / BLOB_STRETCH) * BLOB_FREQ;
    gp.y += iTime * FLOW_SPEED * BLOB_FREQ;
    vec2 warp = vec2(fbm(gp * BLOB_WOBBLE_SCALE + 5.0), fbm(gp * BLOB_WOBBLE_SCALE + 19.0)) - 0.5;
    gp += BLOB_WOBBLE * warp;
    vec2 cell = floor(gp); float field = 0.0;
    for (int oy = -SEARCH_CELLS; oy <= SEARCH_CELLS; oy++) for (int ox = -SEARCH_CELLS; ox <= SEARCH_CELLS; ox++) {
        vec2 id = cell + vec2(float(ox), float(oy)); vec2 h = hash22(id);
        float reach = mix(BLOB_REACH_MIN, BLOB_REACH_MAX, hash21(id + 31.7));
        vec2 c = id + 0.5 + BOB_AMP * vec2(sin(iTime * BOB_SPEED * mix(0.6, 1.4, h.x) + h.x * 6.2831), sin(iTime * BOB_SPEED * mix(0.6, 1.4, h.y) + h.y * 6.2831));
        float x = max(0.0, 1.0 - dot(gp - c, gp - c) / (reach * reach)); field += x * x * x;
    }
    return smoothstep(THRESH_LO, THRESH_HI, field) * mix(0.88, 1.12, clamp(p.y, 0.0, 1.0));
}
vec3 placeholder(vec2 u) { vec3 base = mix(vec3(0.10, 0.16, 0.18), vec3(0.32, 0.42, 0.28), u.y); float border = smoothstep(0.03, 0.0, min(min(u.x, 1.0 - u.x), min(u.y, 1.0 - u.y))); return mix(base, vec3(0.75, 0.82, 0.55), border); }
vec3 background(vec2 uv, vec2 cu, float hasTex) { return mix(placeholder(cu), texture(iChannel0, cu).rgb, hasTex); }

// MAIN IMAGE
void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = fragCoord / iResolution.xy; vec2 center = vec2((CARD_LEFT + CARD_RIGHT) * 0.5, (CARD_BOTTOM + CARD_TOP) * 0.5);
    vec2 halfSize = vec2((CARD_RIGHT - CARD_LEFT) * 0.5, (CARD_TOP - CARD_BOTTOM) * 0.5);
    float cardMask = 1.0 - smoothstep(0.0, 0.008, roundedBox(uv - center, halfSize, CARD_RADIUS));
    vec2 cu = cardUV(uv); float hasTex = step(0.01, dot(texture(iChannel0, cu).rgb, vec3(1.0)));
    vec2 p = vec2(cu.x * 1.35, cu.y); float thickness = gooField(p);
    float e = NORMAL_EPS; vec3 n = normalize(vec3((gooField(p - vec2(e, 0.0)) - gooField(p + vec2(e, 0.0))) * NORMAL_STRENGTH, (gooField(p - vec2(0.0, e)) - gooField(p + vec2(0.0, e))) * NORMAL_STRENGTH, 1.0));
    vec3 bg = background(uv, cu - n.xy * REFRACT_AMT * thickness, hasTex); float goo = smoothstep(0.0, 0.01, thickness) * cardMask;
    vec3 L = normalize(LIGHT_DIR), H = normalize(L + vec3(0.0, 0.0, 1.0)); float diff = max(dot(n, L), 0.0); float spec = pow(max(dot(n, H), 0.0), SPEC_POWER); float rim = pow(1.0 - clamp(n.z, 0.0, 1.0), RIM_POWER);
    vec3 body = mix(SLIME_SURFACE, SLIME_DEEP, thickness); body *= AMBIENT + DIFFUSE * diff; body += SLIME_SURFACE * BUMP_STRENGTH * (fbm(p * BUMP_SCALE + iTime * 0.15) - 0.5);
    vec3 trans = bg * mix(vec3(1.0), ABSORB_COLOR, thickness * ABSORB_STR); float alpha = mix(ALPHA_THIN, ALPHA_THICK, thickness) * goo;
    vec3 col = mix(bg, mix(trans, body, alpha), goo); col += vec3(1.0, 1.0, 0.95) * spec * SPEC_INTENSITY * goo; col += vec3(0.65, 1.0, 0.55) * rim * RIM_INTENSITY * goo;
    fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
