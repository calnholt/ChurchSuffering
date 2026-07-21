---
name: shadertoy-shader-creation
description: Create ShaderToy-style GLSL prototype shaders for ChurchSuffering under `Content/Shaders`, with constants/tunables extracted to the top of the file. Use when the user asks for a new `.glsl` shader concept, a Shadertoy/ShaderToy-style prototype, or a source shader similar to `Content/Shaders/Brittle.glsl`, `DesertStorm.glsl`, or `DrippingBlood.glsl`. Do not use for MonoGame `.fx` conversion or runtime integration; use `monogame-shader-display` for that.
---

# ShaderToy Shader Creation

Create standalone GLSL prototypes that are easy to preview in shadertoy.com and later convert into MonoGame `.fx` effects.

## Scope

Use this skill for source `.glsl` prototype files only. If the user asks to apply the shader in-game, create components, create overlays, register `Content.mgcb`, or compile a MonoGame effect, use `monogame-shader-display` instead or after this skill.

Prefer adding a new file under `Content/Shaders/<Name>.glsl`. Do not edit runtime `.fx` files unless explicitly requested.

## Inspect First

Read:

- `AGENTS.md`
- The closest existing `.glsl` prototypes in `Content/Shaders/`
- Any named reference shader, especially `Content/Shaders/Brittle.glsl` when the user asks for a card effect
- Existing `.fx` only when you need to understand eventual runtime constraints

Check for unrelated work with `git status --short` before editing. Preserve unrelated changes.

## File Structure

Use the project prototype style:

1. Header comment with the effect name, purpose, ShaderToy preview instructions, and channel usage.
2. `TUNABLES` section at the top.
3. All visual constants, implementation constants, loop counts, color values, hash seeds, dimensions, and thresholds at the top.
4. Helper sections such as `HASH / NOISE`, `CARD GEOMETRY`, `SMOKE FIELD`, or domain-specific equivalents.
5. `MAIN IMAGE` section with `void mainImage(out vec4 fragColor, in vec2 fragCoord)`.

Keep text ASCII. Avoid typographic separators, arrows, bullets, and em dashes inside shader comments.

## Constants Policy

Extract constants aggressively. The top section should include:

- Card or scene region values
- Speeds, scales, thresholds, powers, densities, opacity, colors
- Hash/noise constants and fixed math constants
- Compile-time loop counts with `#define`
- Placeholder texture constants if the shader can preview without a channel

Do not hide magic numbers in helpers unless they are GLSL literals like `0.0`, `1.0`, or obvious vector construction values. Prefer readable constant names over clever formulas.

## ShaderToy Conventions

Use ShaderToy globals directly:

- `iResolution`
- `iTime`
- `iChannel0`, `iChannel1`, etc. when needed

Use GLSL names:

- `vec*`
- `mat*`
- `fract`
- `mix`
- `mod`
- `texture`

Do not write HLSL or MonoGame effect boilerplate in a `.glsl` prototype.

## Card Effects

When the effect is intended to apply to a card like Brittle:

- Include a preview card rectangle with `CARD_LEFT`, `CARD_RIGHT`, `CARD_BOTTOM`, `CARD_TOP`, and `CARD_RADIUS`.
- Use a rounded-card SDF so the effect can cover the card and optionally extend past its bounds.
- Map `iChannel0` onto the card with a `cardUV` helper.
- Include a procedural placeholder card when no texture is bound.
- Keep the shader conceptually card-local so it can later become a capture-based post-process like `Brittle.fx`.
- If the desired effect must fully hide card identity, ensure the card interior is fully replaced or composited to opacity 1.

For effects that escape the card boundary, drive outside coverage from distance to the card SDF and a noise/wisp field. Avoid clipping all motion to the card unless the user asked for a contained effect.

## Visual Design

Translate the user request into concrete controllable layers. For example, smoke can be:

- A dense interior mask that guarantees occlusion
- Domain-warped FBM for body volume
- Swirl motion around the card center
- Thin ribbon or tendril masks outside the SDF
- Grain, vignette, edge boost, and color grading as separate tunables

Prefer layered fields with clear names over one opaque formula. Put comments at major sections, not on every line.

## Validation

Run:

```bash
git diff --check
```

If only `.glsl` prototypes changed, `dotnet build` is not required because `.glsl` files are not compiled by `Content.mgcb`. If the task also changed `.fx`, C#, content registration, or an approved implementation plan, run `dotnet build` from the repo root.

Finish by reviewing:

```bash
git status --short
```

Report any skipped validation and any unrelated dirty files.
