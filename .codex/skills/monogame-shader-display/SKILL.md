---
name: monogame-shader-display
description: Convert GLSL or ShaderToy-style shaders into MonoGame `.fx` effects and integrate them into ChurchSuffering through an overlay wrapper and display system. Use when adding a procedural scene background, full-screen shader pass, post-process effect, or other shader-backed `*DisplaySystem`, including requests to convert files under `Content/Shaders`, create an overlay, register an effect in `Content.mgcb`, or insert a shader into a scene draw pipeline.
---

# MonoGame Shader Display

Implement shader-backed display systems end to end while preserving scene ownership, SpriteBatch state, runtime shader-disable behavior, and project conventions.

## 1. Inspect Before Editing

Read:

- `AGENTS.md`
- The source shader
- The target scene/display system and its draw coordinator
- `Content/Content.mgcb`
- One analogous `.fx` shader
- Its overlay wrapper under `ECS/Rendering` or `ECS/Factories`
- Its display system and registration/draw-order integration

Determine:

- Whether the effect generates pixels procedurally or processes an existing texture
- Which system currently owns the destination background or source render target
- The exact draw position relative to backgrounds, titles, UI, and global overlays
- The fallback required when `ShaderRuntimeOptions.ShadersEnabled` is false or effect loading fails
- Whether the effect is scene-local, globally registered, or owned by a parent scene system

Do not let two systems own the same output. If an existing renderer would cover the shader with an opaque fill, transfer background ownership to the shader display system instead of counteracting that fill later.

## 2. Convert the Shader

Create `Content/Shaders/<Name>.fx` using the repository's SpriteBatch-compatible pattern:

- Declare `MatrixTransform` and a viewport/resolution uniform.
- Include the SpriteBatch vertex input/output structures and transform vertex shader.
- Include `Texture` and `TextureSampler`, even for a procedural pass drawn from a white pixel.
- Compile a `SpriteDrawing` technique with `vs_3_0` and `ps_3_0`.
- Translate GLSL types and functions: `vec*` to `float*`, `fract` to `frac`, `mix` to `lerp`, and `mod` to `fmod`.
- Replace ShaderToy globals such as `iTime` and `iResolution` with explicit effect parameters.
- Account for coordinate origin differences. SpriteBatch UV Y starts at the top; invert Y when preserving a bottom-left-origin ShaderToy effect.
- Use fixed compile-time maximum loop bounds for shader model 3.0. Gate runtime-adjustable counts inside those loops.
- Guard divisions, radii, speeds, and periods with small positive minimums.
- Preserve the source shader's visual defaults unless the user requests redesign.

Expose practical tunables as effect parameters rather than hardcoded constants. Keep only true implementation limits, such as maximum loop counts, compile-time constants.

Register the new `.fx` file in `Content/Content.mgcb` with `EffectImporter`, `EffectProcessor`, and the existing debug-mode convention.

## 3. Create the Overlay

Add `ECS/Rendering/<Name>Overlay.cs` unless the nearest established pattern belongs in `ECS/Factories`.

The overlay must:

- Own the `Effect` reference and report availability.
- Expose strongly typed properties for shader parameters.
- Cache any required white pixel or source texture.
- Set `CurrentTechnique` to `SpriteDrawing`.
- Set the orthographic projection, viewport/resolution, time, textures, and tunables in `Begin`.
- Start its own SpriteBatch with blend and sampler states appropriate to the effect.
- Draw either a full-viewport white pixel for procedural output or the supplied source texture for post-processing.
- End only the SpriteBatch it begins.

Use null-safe parameter lookup so optimized-out parameters do not crash effect binding.

Choose blending intentionally:

- Use `BlendState.Opaque` for a complete background or full scene replacement.
- Use `BlendState.AlphaBlend` for transparent overlays.
- Follow the existing compositor pattern for render-target post-processing.

## 4. Create the Display System

Add the system beside the target scene, named `<Name>DisplaySystem`.

Required behavior:

- Inherit from `Core.System`.
- Add `[DebugTab("<Readable Name>")]`.
- Add `DebugEditable` properties for all useful visual controls; expose colors by channel when the debug editor cannot edit vectors directly.
- Use `Step = 0.01f` for ordinary float controls, with finer steps only where normalized shader values require them.
- Restrict update and draw behavior to the intended `SceneId` or active effect state.
- Advance animation time in `Update` or `UpdateEntity`, never in `Draw`.
- Reset scene-local animation time when leaving the scene when re-entry should restart the effect.
- Lazily load the effect through `ContentManager`.
- Catch effect-load failures, log once with `LoggingService`, and use the defined fallback.
- Honor `ShaderRuntimeOptions.ShadersEnabled`.
- Clamp counts and normalize paired min/max values before assigning overlay properties.

For a system that temporarily takes over SpriteBatch:

1. Capture the current blend, sampler, depth-stencil, and rasterizer states.
2. End the caller's SpriteBatch.
3. Run the overlay's `Begin`, `Draw`, and `End`.
4. Restart SpriteBatch with the captured states.

Draw must render only. Do not create entities, change scene state, or advance timers there.

## 5. Integrate Ownership and Ordering

Wire the system into the same lifecycle as the target scene:

- Construct it with the required `EntityManager`, `GraphicsDevice`, `SpriteBatch`, and `ContentManager`.
- Register it with `World` if it needs updates.
- Call its `Draw` from the scene's established draw coordinator.
- Add `FrameProfiler.Measure` using the system's exact draw name.
- Place procedural backgrounds before scene text and UI.
- Place transparent overlays after the content they cover.
- Place post-process systems in the existing render-target composition chain.

If the new system owns the background, remove the old background draw and its obsolete fields/resources from the previous owner. Preserve that system's unrelated text, input, layout, or transition responsibilities.

## 6. Validate

Run from the repository root:

```bash
dotnet build
dotnet test tests/ChurchSuffering.Tests/ChurchSuffering.Tests.csproj --no-restore
```

The build must compile the `.fx` file through MGFXC and compile C#. Fix all new errors before handoff; report unrelated pre-existing warnings separately.

Run short shader-enabled and `no-shaders` launches when the environment supports the game window. Confirm from process status and logs that:

- The effect loads without a logged failure.
- The game remains alive through multiple update/draw frames.
- The fallback path does not throw.

Terminate smoke-test processes cleanly. Do not leave game or compiler sessions running.

Finish with `git diff --check` and review `git status --short`. Preserve unrelated user changes and source `.glsl` files.
