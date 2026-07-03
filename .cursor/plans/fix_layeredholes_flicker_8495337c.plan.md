---
name: Fix LayeredHoles flicker
overview: Eliminate the per-frame layer flicker in LayeredHoles.fx by moving all per-hole lifecycle randomness (period, phase, cycle, radius, center, layer pick) to CPU-side double-precision math in LayeredHolesOverlay, uploading stable per-hole float4 uniforms to the shader.
todos:
  - id: overlay-cpu-holes
    content: Compute per-hole lifecycle data in LayeredHolesOverlay (double precision) and upload Vector4[30] uniform
    status: completed
  - id: shader-consume-array
    content: Rewrite LayeredHoles.fx loop to consume Holes[30] array; remove Hash11 and lifecycle uniforms
    status: completed
  - id: build-verify
    content: dotnet build and verify flicker is gone in Climb scene
    status: in_progress
isProject: false
---

# Fix LayeredHoles Layer-Pick Flicker

## Root cause

In [Content/Shaders/LayeredHoles.fx](Content/Shaders/LayeredHoles.fx), every per-hole random value is derived in the pixel shader via `Hash11(n) = frac(sin(n) * 43758.5453123)` with `n = fid * K + cycle`, where `cycle = floor((Time + phase) / period)`.

- The argument reaches magnitudes of 500+ (`fid * 17.7` plus a `cycle` that grows with `Time`).
- ps_3_0 `sin` range reduction (`frac(x / 2π)`) at that magnitude keeps only ~17 mantissa bits; the ×43758 multiply then amplifies last-ulp jitter (introduced when the compiler folds `floor` into arithmetic containing the per-frame `Time` term) into errors > 1.0 before the final `frac`.
- Result: once `Time`/`cycle` grow, `Hash11` output is effectively re-rolled every frame. The layer `pick` (line 226-229) is a hard 0.5 threshold, so an open hole flips between middle and bottom textures each frame. Fewer holes = fewer open holes in the unstable state, matching the observed behavior. The GLSL original has the same weakness.

## Fix: compute hole data on CPU, upload as uniform array

Follow the existing precedent of `MaskRadii[MAX_MASKS]` in [Content/Shaders/CircularMask.fx](Content/Shaders/CircularMask.fx).

### 1. `LayeredHolesOverlay` computes per-hole data ([ECS/Rendering/LayeredHolesOverlay.cs](ECS/Rendering/LayeredHolesOverlay.cs))

In `Begin`, for each hole index `i` (up to 30), replicate the shader's lifecycle math in C# `double` precision (fully stable frame-to-frame):

- `period = lerp(HolePeriodMin, HolePeriodMax, hash(i * 1.7 + 0.3))`, `phase = hash(i * 3.1 + 0.9) * period`
- `cycle = floor((Time + phase) / period)`, `local = (Time + phase) mod period`
- `openDur = period * lerp(HoleLifeMin, HoleLifeMax, hash(i * 5.3 + cycle))`; closed holes get radius 0
- envelope (`smoothstep` grow/close with `HoleOpenFrac`/`HoleCloseFrac`), `maxRadius`, radius flux `sin(Time * RadiusFluxRate + i * 2.399)`, center from `HoleMargin`/aspect (viewport is available in `Begin`), and layer pick `hash(i * 17.7 + cycle) < LayerSplit ? 1 : 0`
- `hash` is the same `frac(sin(n) * 43758.5453123)` evaluated in double precision so the visual character (staggered clocks, respawn re-rolls) is preserved exactly

Pack into `Vector4[30]`: `(centerX_aspectSpace, centerY, radius, pickMiddle01)` and upload via `SetValue(Vector4[])` along with `HoleCount`.

### 2. Simplify the shader loop ([Content/Shaders/LayeredHoles.fx](Content/Shaders/LayeredHoles.fx))

- Add `float4 Holes[30];` uniform; delete `Hash11` and the now-CPU-only lifecycle uniforms (`HolePeriodMin/Max`, `HoleLifeMin/Max`, `HoleOpenFrac/CloseFrac`, `HoleRadiusMin/Max`, `RadiusFluxAmp/Rate`, `HoleMargin`, `LayerSplit`).
- Loop body per hole: skip if `radius <= 0`; `d = distance(auv + disp, center)`; feather/`fVary`, mask `m`, rim refraction `revealUv` unchanged; `revealed = lerp(bottom, middle, Holes[i].w)`; composite. All per-pixel FBM warp/feather logic stays in the shader untouched.

### 3. Wire-up ([ECS/Scenes/ClimbScene/ClimbBackgroundDisplaySystem.cs](ECS/Scenes/ClimbScene/ClimbBackgroundDisplaySystem.cs))

- No structural change: `ConfigureOverlay` keeps setting the same `DebugEditable` tunables on the overlay; they now drive the CPU-side hole computation instead of shader uniforms. `LayerSplit` (0.5 or 1.0 from the layer plan) is consumed CPU-side for the pick.

### 4. Verify

- `dotnet build` (compiles the .fx through Wine/MGFXC) and fix any errors.
- Run the game to the Climb scene and confirm holes hold a single layer for their full lifetime, including after letting time accumulate for several minutes.

The `.glsl` prototype is left as-is; it is a ShaderToy reference and the game only consumes the `.fx`.