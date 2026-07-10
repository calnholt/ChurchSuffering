# Booster Pack Opening Animation Redesign Plan

## Document Status

- **Status:** Decision complete and ready for implementation.
- **Repository:** `Crusaders30XX`
- **Target:** `ECS/Scenes/BoosterPackOpeningDisplaySystem.cs`
- **Source mockup:** `mockups/booster-pack-opening-v2.html`
- **UI archetype:** Full-screen overlay with embedded canonical card rendering.
- **Virtual resolution:** `1920x1080`.
- **Required final verification:** `dotnet build` from the repository root.
- **Save compatibility:** No save changes or migration work.

## 1. Objective

Replace the current booster pack presentation with a phase-driven, approximately 5.14 second autoplay sequence that makes obtaining new game content feel deliberate and valuable.

The redesign must:

- Preserve the pack summon, charge, crack, rupture, and showcase structure from the HTML mockup.
- Increase anticipation and rupture impact through better motion, light, particles, shards, stage shake, and the existing post-process shockwave system.
- Remove the persistent rotating circle outlines currently visible behind the pack and rewards.
- Replace the current unclipped white-line card shine with a card-local shader sheen.
- Allow a primary click anywhere to close the overlay only after every finite reveal effect has completed.
- Keep early clicks blocked without fast-forwarding or closing.
- Keep runtime reward types and identities fully random, including duplicate kinds and duplicate items.
- Remain presentation-only. It does not roll authoritative rewards, grant content, write saves, or change progression.
- Add no new reward captions, reward names, type labels, or caption plates. Item identity remains available through the existing hover tooltips.
- Add no SFX and publish no `PlaySfxEvent` from this flow.

## 2. Current Problems To Correct

### 2.1 Persistent circles dominate the result

`DrawStageLighting` and `DrawLootPlate` both call `DrawAltarRing`. The MonoGame version draws a complete ring-mask circumference plus spokes, whereas the HTML uses low-opacity repeating conic rays. The result is several large ellipses crossing the screen and continuing to rotate after the rewards settle.

Required correction:

- Remove `DrawAltarRing` from this presentation.
- Do not render complete circumference outlines behind the pack or rewards.
- Use a fixed radial-ray mask with no circular border.
- Allow reward raybursts to expand once, then remain stationary. Only their opacity may breathe in the ready state.

### 2.2 Dynamic texture creation grows caches during animation

The current draw path keys soft radial textures by animated destination diameter. Reward plate scaling can therefore create many GPU textures during one reveal. Shockwaves create ring masks at a different size on many frames, and shards create polygon masks using random dimensions.

Required correction:

- Never create or cache a texture based on an animated frame size.
- Generate a small fixed set of normalized masks before drawing and scale them through `SpriteBatch` destination rectangles.
- Use one fixed soft radial mask, one inverted vignette mask, one rayburst mask, one particle core mask, and one shard mask.
- Remove the sprite-based visible shockwave rings and use the existing shader-backed `ShockwaveEvent` once at rupture.

### 2.3 Timeline behavior is distributed across thresholds and flags

The current implementation spreads timing across constants, direct comparisons, repeat-particle scheduling, and several boolean flags. `TotalSeconds` does not control dismissal or a ready state.

Required correction:

- Introduce one read-only timeline calculator with explicit phase durations and derived start times.
- Store the current phase, previous elapsed time, and dismissal readiness on the overlay component.
- Process phase milestones in `Update`, including every milestone crossed by a large frame step.
- Keep `Draw` limited to sampling already-authored presentation state and issuing draw calls.

### 2.4 The card reveal discards its computed rotation

`DrawLoot` computes a rotation for every reward, but `DrawCardPreview` publishes `CardRenderScaledEvent`, which forces card rotation to zero. Medal and equipment rewards rotate correctly; cards do not.

Required correction:

- Add an optional rotation to `CardRenderScaledEvent` and route it through the canonical card renderer.
- Keep default rotation at zero so every existing caller retains current behavior.
- Update scaled-card visual-effect subscribers to use the event rotation instead of assuming zero.

### 2.5 The current card shine is not clipped

The existing shine draws a thick white line over the card rectangle. It leaks beyond the card, has no material response, and does not remain aligned with card rotation.

Required correction:

- Add a dedicated card-local sheen effect rendered immediately after the canonical card body.
- Clip the effect to a rounded card rectangle in the shader.
- Rotate the sheen quad with the card.
- Use a warm white core with restrained gold and blue fringes.
- Skip the sheen cleanly when shaders are disabled or unavailable.

### 2.6 The full-screen blocker cannot dismiss the overlay

The blocker has no `UIElementEventType`. Reward preview entities also sit above it and retain their existing interaction events, so clicking a preview card can route into gameplay card handling.

Required correction:

- Add a dedicated close UI event type handled through `UIElementEventDelegateService`.
- Overwrite every preview entity's gameplay click event as soon as it is created.
- Keep all click event types set to `None` until the ready state.
- At the ready state, assign the close event to both the blocker and every preview entity.

## 3. Canonical Product Decisions

### 3.1 Presentation scope

- `ShowBoosterPackOpeningOverlayEvent` remains payload-free.
- The display system continues to generate three previews internally.
- Each slot independently rolls card, medal, or equipment.
- Duplicate kinds and duplicate item IDs are permitted.
- No reward-generation service, unlock operation, inventory mutation, or persistence is added.

### 3.2 Interaction

- The sequence begins automatically when the show event is received.
- Primary clicks before the ready state are swallowed.
- Early clicks do not fast-forward.
- Early clicks do not close the overlay.
- At `5.14s`, any primary click over the blocker or any reward closes the overlay.
- External `CloseBoosterPackOpeningOverlayEvent` events may close immediately at any time.
- Secondary click behavior remains unchanged and does not close the overlay.

### 3.3 Audio

- Do not add audio assets.
- Do not add `SfxTrack` values.
- Do not publish `PlaySfxEvent` or `StopSfxEvent` from the opening flow.

### 3.4 Background

- Retain the currently active game scene behind the overlay.
- Do not substitute the mockup's Hellrift background.
- Use blackout, bloom, flash, and vignette layers to control focus.

### 3.5 Reward information

- Do not draw reward display names.
- Do not draw uppercase reward type labels.
- Do not draw caption plates beneath rewards.
- Preserve the existing card, medal, and equipment hover tooltips after the relevant item has settled.

## 4. Files To Create

| File | Responsibility |
| --- | --- |
| `ECS/Services/BoosterPackOpeningAnimationService.cs` | Pure phase, milestone, reward-motion, shake, and sheen sampling. It must not mutate ECS state or publish events. |
| `ECS/Rendering/CardSheenOverlay.cs` | Effect parameter binding and shader-batch wrapper for a single rotated card-local sheen quad. |
| `ECS/Scenes/CardSheenDisplaySystem.cs` | Own card sheen rendering in response to `CardBaseRenderCompletedEvent`. |
| `Content/Shaders/CardSheen.fx` | Rounded-rectangle-clipped diagonal sheen pixel shader. |
| `ECS/Diagnostics/Snapshots/Fixtures/BoosterPackOpeningSnapshotFixture.cs` | Deterministic headless rendering of requested timeline samples. |
| `ECS/Diagnostics/Snapshots/Fixtures/BoosterPackOpeningSnapshotVariant.cs` | Parse `--time` and `--seed` and construct stable output slugs. |
| `tests/Crusaders30XX.Tests/BoosterPackOpeningAnimationServiceTests.cs` | Phase, milestone, reward transform, sheen, and ready-state tests. |
| `tests/Crusaders30XX.Tests/BoosterPackOpeningInputTests.cs` | UI delegate close routing and readiness contract tests. |

## 5. Files To Modify

| File or subsystem | Required change |
| --- | --- |
| `ECS/Scenes/BoosterPackOpeningDisplaySystem.cs` | Replace threshold/flag animation control, dynamic masks, rings, and line shine with the new timeline, fixed masks, raybursts, streak particles, staged input, and sheen component updates. |
| `ECS/Components/Scenes.cs` | Add the opening phase enum and component-owned particle, shard, and timeline state. |
| `ECS/Components/CardComponents.cs` | Add the `CardSheen` presentation component and `BoosterPackOpeningClose` UI event type. |
| `ECS/Events/CardEvents.cs` | Add `Rotation` to `CardRenderScaledEvent` with a zero default. |
| `ECS/Scenes/CardDisplaySystem.cs` | Render scaled cards using event rotation and restore the original transform afterward. |
| Scaled-card overlay subscribers | Pass `CardRenderScaledEvent.Rotation` through Cursed, Frozen, FrozenCard, Pledge, Scorched, Seal, Shackle, and Thorned rendering paths. |
| `ECS/Scenes/UIElementEventDelegateSystem.cs` | Publish `CloseBoosterPackOpeningOverlayEvent` for the new close UI event type. |
| `ECS/Rendering/PrimitiveTextureFactory.cs` | Add cached fixed-size radial-ray and inverted-radial mask APIs. |
| `Content/Content.mgcb` | Register `Shaders/CardSheen.fx`. |
| `Game1.cs` | Construct and register `CardSheenDisplaySystem`; do not pass it into the booster system. |
| `ECS/Diagnostics/Snapshots/DisplaySnapshotRegistry.cs` | Register the booster opening fixture. |
| `docs/display-snapshots.md` | Document booster snapshot arguments and output location. |

## 6. Target State And Data Contracts

Names in this section are normative.

### 6.1 Opening phases

Add to `ECS/Components/Scenes.cs`:

```csharp
public enum BoosterPackOpeningPhase
{
    Summon,
    Idle,
    Charge,
    Crack,
    Rupture,
    Showcase,
    Ready,
}
```

`IsOpen` remains the authoritative closed/open flag. A separate `Closed` phase is unnecessary.

### 6.2 Overlay state

Replace the one-shot trigger booleans with component-owned timeline state:

```csharp
public class BoosterPackOpeningOverlayState : IComponent
{
    public Entity Owner { get; set; }
    public bool IsOpen { get; set; }
    public bool CanDismiss { get; set; }
    public float ElapsedSeconds { get; set; }
    public float PreviousElapsedSeconds { get; set; }
    public float NextChargeParticleSeconds { get; set; }
    public BoosterPackOpeningPhase Phase { get; set; }
    public List<BoosterPackLootPreview> Loot { get; set; } = new();
    public List<BoosterPackParticleFx> Particles { get; set; } = new();
    public List<BoosterPackShardFx> Shards { get; set; } = new();
}
```

Remove:

- `RuptureTriggered`
- `RevealTriggered`
- `ChargeParticlesTriggered`
- `CrackParticlesTriggered`

Milestone crossing replaces these flags.

### 6.3 Particle presentation state

Move particle and shard state out of private system collections and into component-owned presentation records in `ECS/Components/Scenes.cs`.

`BoosterPackParticleFx` contains:

- `Vector2 Start`
- `Vector2 Delta`
- `float Width`
- `float Length`
- `Color Color`
- `float StartSeconds`
- `float DurationSeconds`
- `bool IsInward`

`BoosterPackShardFx` contains:

- `Vector2 Start`
- `Vector2 Delta`
- `float Width`
- `float Height`
- `float RotationRadians`
- `float StartSeconds`
- `float DurationSeconds`

These are presentation-only values. No other system queries them.

### 6.4 Loot preview state

Keep `BoosterPackLootPreview.Kind`, `Id`, `CardColor`, and `PreviewEntity`.

Replace kind-derived reveal delays with index-derived delays:

```text
RevealDelaySeconds = slotIndex * RevealStaggerSeconds
```

The system must set the preview entity's click events to `None` immediately after creation. It must not leave a preview card configured as `CardClicked` for even one update.

### 6.5 Card sheen component

Add a reusable presentation component to `ECS/Components/CardComponents.cs`:

```csharp
public sealed class CardSheen : IComponent
{
    public Entity Owner { get; set; }
    public bool IsActive { get; set; }
    public float Progress { get; set; }
    public float Alpha { get; set; }
}
```

The booster system writes this component on preview card entities. `CardSheenDisplaySystem` exclusively owns its visual output.

### 6.6 Scaled card rotation

Extend the existing event without breaking callers:

```csharp
public class CardRenderScaledEvent
{
    public Entity Card { get; set; }
    public Vector2 Position { get; set; }
    public float Scale { get; set; } = 1f;
    public float Alpha { get; set; } = 1f;
    public float Rotation { get; set; } = 0f;
    public Rectangle? ClipRect { get; set; }
}
```

`CardRenderScaledRotatedEvent` remains unchanged for compatibility. The booster path uses `CardRenderScaledEvent` because it needs both authored rotation and alpha.

### 6.7 Close event routing

Add:

```text
UIElementEventType.BoosterPackOpeningClose
```

`UIElementEventDelegateService` handles it only by publishing:

```text
CloseBoosterPackOpeningOverlayEvent
```

Do not check overlay state inside the delegate. The display system controls whether the event type is assigned.

## 7. Timeline Service

Create `BoosterPackOpeningAnimationService` as a static read-only calculator. It must not hold state, mutate components, create entities, publish events, or access singletons.

### 7.1 Required timing input

Define an immutable `BoosterPackOpeningTiming` value containing:

| Field | Default seconds |
| --- | ---: |
| `SummonDuration` | `0.76` |
| `IdleDuration` | `0.52` |
| `ChargeDuration` | `0.85` |
| `CrackDuration` | `0.65` |
| `RuptureDuration` | `0.76` |
| `ShowcaseDuration` | `1.60` |
| `ChargeParticleInterval` | `0.26` |
| `RevealStagger` | `0.12` |
| `RevealTravelDuration` | `0.72` |
| `SheenDelayFromReveal` | `0.52` |
| `SheenDuration` | `0.84` |

Derived starts must be calculated from the durations rather than duplicated as editable start-time values:

```text
SummonStart   = 0.00
IdleStart     = 0.76
ChargeStart   = 1.28
CrackStart    = 2.13
RuptureStart  = 2.78
ShowcaseStart = 3.54
ReadyStart    = 5.14
```

Changing a phase duration in the debug menu must shift every later phase and the ready boundary consistently.

### 7.2 Required service API

```csharp
public static BoosterPackOpeningPhase GetPhase(
    float elapsedSeconds,
    BoosterPackOpeningTiming timing);

public static float GetPhaseProgress(
    float elapsedSeconds,
    BoosterPackOpeningPhase phase,
    BoosterPackOpeningTiming timing);

public static IEnumerable<BoosterPackOpeningMilestone> GetCrossedMilestones(
    float previousSeconds,
    float currentSeconds,
    BoosterPackOpeningTiming timing);

public static BoosterPackLootAnimationSample SampleLoot(
    float elapsedSeconds,
    int slotIndex,
    Vector2 ruptureCenter,
    Vector2 finalCenter,
    BoosterPackOpeningTiming timing,
    float arcHeightPx);

public static float GetSheenProgress(
    float elapsedSeconds,
    int slotIndex,
    BoosterPackOpeningTiming timing);

public static Vector2 SampleRuptureShake(
    float elapsedSeconds,
    BoosterPackOpeningTiming timing,
    float amplitudePx);

public static bool CanDismiss(
    float elapsedSeconds,
    BoosterPackOpeningTiming timing);
```

`BoosterPackLootAnimationSample` returns:

- `Position`
- `Scale`
- `Rotation`
- `Alpha`
- `Progress`
- `IsSettled`

### 7.3 Milestones

Define milestones for:

- `ChargeStarted`
- `ChargePulse`
- `CrackStarted`
- `RuptureStarted`
- `ShowcaseStarted`
- `ReadyStarted`

`GetCrossedMilestones` must return every crossed milestone in chronological order. A frame that advances from `2.70s` to `3.70s` must process crack completion, rupture, and showcase entry exactly once.

Charge pulses are generated at `ChargeStart + n * ChargeParticleInterval` while their time is strictly less than `CrackStart`.

### 7.4 Reward travel

Each slot begins at:

```text
ShowcaseStart + slotIndex * RevealStagger
```

Use a quadratic Bezier from rupture center to slot center:

```text
control = lerp(start, end, 0.5) + (0, -RewardArcHeight)
```

Sample position with `EaseOutCubic(progress)`.

Scale keyframes:

| Normalized time | Scale |
| ---: | ---: |
| `0.00` | `0.18` |
| `0.78` | `1.08` |
| `1.00` | `1.00` |

Alpha reaches `1.0` over the first `0.18` normalized travel time.

Rotation starts according to horizontal slot position, not reward kind:

| Slot | Start rotation |
| --- | ---: |
| Left | `-14 degrees` |
| Center | `2 degrees` |
| Right | `14 degrees` |

Rotation eases to zero by the end of travel.

### 7.5 Deterministic rupture shake

Use a decaying deterministic sample rather than advancing a random generator in `Draw`:

```text
decay = 1 - normalizedProgress
x = sin(progress * TwoPi * 7) * amplitude * decay
y = sin(progress * TwoPi * 11 + 1.7) * amplitude * 0.75 * decay
```

Apply this offset only to booster overlay presentation elements. Do not write to the underlying scene's transforms.

## 8. Update Pipeline

`BoosterPackOpeningDisplaySystem.Update` becomes the sole owner of animation state changes.

For an open overlay, perform this order:

1. Store `PreviousElapsedSeconds`.
2. Advance `ElapsedSeconds` by non-negative frame delta.
3. Resolve the current phase through the timeline service.
4. Enumerate and process every crossed milestone.
5. Spawn any charge pulses crossed during the frame.
6. Update and prune component-owned particles and shards.
7. Update each preview card's `CardSheen` progress and alpha.
8. Compute settled reward hitboxes through the same loot sample used by rendering.
9. Set preview tooltip visibility after that preview has settled.
10. Set `CanDismiss` from the timeline service.
11. Update blocker and preview click event types from `CanDismiss`.

When closed:

- Disable the blocker.
- Disable the input context.
- Hide and clear preview hitboxes.
- Leave no active `CardSheen` component on a surviving entity.

### 8.1 Milestone effects

| Milestone | Update-side effect |
| --- | --- |
| `ChargeStarted` | Spawn `ChargeParticleCount` inward streaks at the exact charge start timestamp. |
| `ChargePulse` | Spawn `ChargeRepeatParticleCount` inward streaks at the pulse timestamp. |
| `CrackStarted` | Spawn `CrackParticleCount` short outward sparks. |
| `RuptureStarted` | Spawn `BurstParticleCount` streaks, spawn shards, and publish one configured `ShockwaveEvent`. |
| `ShowcaseStarted` | Spawn `ShowcaseParticleCount` outward streaks. |
| `ReadyStarted` | Enable dismissal. Do not spawn a new finite effect. |

Particle `StartSeconds` must use the milestone timestamp, not the current frame's final elapsed time. This preserves correct seeking and large-frame behavior.

### 8.2 Rupture shockwave

Publish one existing `ShockwaveEvent` at `(960,500)` with defaults:

| Parameter | Default |
| --- | ---: |
| `DurationSec` | `0.45` |
| `MaxRadiusPx` | `720` |
| `RippleWidthPx` | `36` |
| `Strength` | `0.014` |
| `ChromaticAberrationAmp` | `0.006` |
| `ChromaticAberrationFreq` | `22` |
| `ShadingIntensity` | `0.20` |

`ShockwaveDisplaySystem` already omits the effect when shaders are disabled. Do not add another fallback ring.

## 9. Open And Close Lifecycle

### 9.1 Open

`OpenOverlay` must:

1. Ensure overlay and blocker entities exist.
2. Destroy preview entities from any previous opening.
3. Clear component-owned particles and shards.
4. Load pack textures and fixed masks before the first draw.
5. Generate three fully random loot previews.
6. Assign reveal delay from slot index.
7. Immediately clear preview primary and secondary gameplay event types.
8. Add `CardSheen` only to card preview entities.
9. Reset elapsed times, phase, charge scheduling, and dismissal state.
10. Enable the overlay input context and blocker.

The debug action follows this exact path.

### 9.2 Close

`CloseOverlay` must be idempotent and:

1. Mark the state closed and not dismissible.
2. Destroy every preview entity.
3. Clear loot, particles, and shards.
4. Clear all blocker and reward hitboxes.
5. Disable the blocker and input context.
6. Leave texture resources cached for reuse until `DeleteCachesEvent`.

### 9.3 Cache deletion

On `DeleteCachesEvent`:

- Clear pack texture references.
- Clear system-owned asset references.
- Reset shader-wrapper references in `CardSheenDisplaySystem` so content can be reacquired.
- Do not dispose textures owned by `ContentManager` or shared factory caches directly.

## 10. Visual Design

### 10.1 Draw order

Back to front:

1. Full-screen blackout and phase-dependent scene bloom.
2. Floor glow and static stage rayburst.
3. Pack shadow and pack aura.
4. Intact pack frame or split pack halves.
5. Crack glow passes.
6. Rupture flash, vertical flare, and transient background beam rayburst.
7. Component-owned particle streaks and shards.
8. Reward raybursts and reward-local glows.
9. Medal, card, and equipment reward visuals.
10. `PACK OPENED` and `HOLY SPOILS` title.
11. Full-screen vignette.

The post-process shockwave is applied by the existing Game1 composite pipeline after scene rendering.

Tooltips and cursor remain later in the existing global draw order.

### 10.2 Scene wash

- Begin with blackout alpha `0.68`.
- Darken toward `0.76` through charge and crack.
- Use the rupture flash as the brightness peak rather than reducing blackout for multiple seconds.
- Settle at blackout alpha `0.70` during showcase and ready.
- Preserve a warm bloom centered on the pack/rewards.
- Keep the existing full-screen vignette at default maximum alpha `0.72`.

### 10.3 Stage light

- Keep the `760x150` floor glow at `y=830`.
- Draw a fixed 24-spoke rayburst mask stretched to `640x218` behind the pack.
- Do not rotate the stage rayburst.
- Fade the stage rayburst during rupture so it does not compete with the reward row.

### 10.4 Pack summon and idle

- Preserve the existing pack dimensions and center.
- Draw a low-alpha pack silhouette below the pack before the pack itself to approximate drop shadow without a blur pass.
- Use the summon keyframes from the timing table.
- Idle movement is limited to `14px` vertical travel and `-0.5` to `0.7` degrees rotation.

### 10.5 Charge

- Switch to `booster_2` at charge entry.
- Pulse aura opacity from `0.40` to `0.90` and scale from `0.72` to `1.04` over `0.5s` alternating cycles.
- Render inward particles as streaks aimed at the pack core.
- Increase pack shake without moving the scene background.

### 10.6 Crack

- Switch to `booster_3` at crack entry.
- Preserve the five mockup crack branches.
- Draw each branch in three passes:
  1. Wide blood-red glow.
  2. Medium gold body.
  3. Narrow warm-white core.
- Pulse intensity over `0.55s` while retaining the existing crack geometry.

### 10.7 Rupture

- Reuse the existing left and right split-pack textures.
- Preserve the `0.62s` peel duration and opposite rotations.
- Apply the deterministic stage shake for `0.58s`.
- Draw one transient 24-spoke background rayburst that scales `0.20 -> 1.08` and fully fades before showcase.
- Draw the vertical flare for `0.56s`.
- Keep the flash brief enough that the screen is never uniformly white at the ready state.
- Use the post-process shockwave for distortion; do not draw visible ring primitives.

### 10.8 Showcase and ready state

- Use three equal layout slots regardless of rolled reward kinds.
- Animate rewards from the rupture core along indexed arcs.
- Draw a local `320x320` rayburst behind each reward.
- Reward raybursts expand once with the reward and stop rotating.
- After settlement, allow only an opacity pulse between `0.14` and `0.20` and a reward float of at most `4px` over `2.6s` with slot phase offsets.
- Do not render reward names, type labels, or caption plates.
- Preserve hover tooltips once each reward is settled.

## 11. Fixed Mask Strategy

### 11.1 Primitive factory APIs

Add:

```csharp
public static Texture2D GetAntialiasedRadialBurstMask(
    GraphicsDevice device,
    int diameter,
    int spokeCount,
    float innerRadiusNormalized,
    float outerRadiusNormalized,
    float spokeFillNormalized);

public static Texture2D GetInvertedSoftRadialCircle(
    GraphicsDevice device,
    int diameter,
    float innerStop,
    float outerStop);
```

### 11.2 Rayburst generation

- Generate at a fixed `512x512` resolution.
- Use 24 spokes.
- Use four subpixel samples per pixel.
- Reject pixels inside the normalized inner radius and outside the outer radius.
- Calculate angular position within a spoke period.
- Feather spoke edges and the inner/outer radial boundaries.
- Store a white alpha mask for `SpriteBatch` tinting.
- Cache by device, diameter, spoke count, and rounded normalized parameters.

### 11.3 Reuse contract

Use destination scaling for:

- `640x218` stage rayburst.
- Up to `980x980` transient rupture rayburst.
- `320x320` reward raybursts.
- All soft ellipses and vignette destinations.

Use one normalized `32x64` shard polygon mask and scale each shard at draw time. Do not request random mask dimensions from `PrimitiveTextureFactory`.

## 12. Particle And Shard Rendering

### 12.1 Particle spawning defaults

| Group | Count |
| --- | ---: |
| Initial charge | `34` |
| Repeating charge pulse | `12` |
| Crack sparks | `24` |
| Rupture burst | `74` |
| Showcase burst | `42` |

Keep the existing color distribution:

- Approximately 66 percent gold-hot.
- Approximately 19 percent blue.
- Approximately 15 percent blood-hot.

### 12.2 Streak draw

For each active particle:

1. Sample parametric position from its authored start and delta.
2. Derive direction from delta.
3. Draw a wide low-alpha colored streak.
4. Draw a narrow warm core over it.
5. Draw a small core point at the leading edge.

Inward particles contract toward the pack. Outward particles expand from the core. Alpha fades over the final portion of duration.

### 12.3 Shards

- Spawn `34` shards at rupture by default.
- Preserve the existing fan-angle distribution.
- Use the one normalized shard mask.
- Draw a blood-dark outer pass and a smaller gold-hot highlight pass.
- Remove shards after `StartSeconds + DurationSeconds + 0.05s`.

## 13. Card Rendering Rotation

Update `CardDisplaySystem.OnCardRenderScaledEvent` to:

1. Capture original position, scale, and rotation.
2. Apply event position, uniform scale, and event rotation.
3. Render through `RenderCardWithLifecycle` using event rotation.
4. Restore all original transform values in `finally`.
5. Restore `_drawAlpha` in the existing outer `finally`.

Every direct `CardRenderScaledEvent` subscriber that currently passes `0f` rotation must instead pass `evt.Rotation`:

- `CursedDisplaySystem`
- `FrozenCardDisplaySystem`
- `FrozenDisplaySystem`
- `PledgeDisplaySystem`
- `ScorchedDisplaySystem`
- `SealDisplaySystem`
- `ShackleDisplaySystem`
- `ThornedDisplaySystem`

Existing callers that do not set rotation continue to render at zero rotation.

The booster system must pass the sampled card alpha and rotation through the event. Fix card visual-center compensation to use the current animated render scale, not the final `CardScale` constant.

## 14. Card Sheen Shader

### 14.1 System ownership

`CardSheenDisplaySystem`:

- Inherits `ECS/Core/System`.
- Is registered in `Game1` as a normal presentation system.
- Receives `EntityManager`, `GraphicsDevice`, `SpriteBatch`, `ContentManager`, and `ImageAssetService`.
- Does not receive or store `BoosterPackOpeningDisplaySystem`.
- Subscribes to `CardBaseRenderCompletedEvent` at priority `-200`, after the existing Brittle post-render priority `-100`.
- Draws only when the card has an active `CardSheen` component with progress strictly between zero and one.
- Loads the effect during `Update`, never from its render event handler.
- Does nothing when `ShaderRuntimeOptions.ShadersEnabled` is false.

### 14.2 Overlay API

`CardSheenOverlay` exposes:

```csharp
public float Progress { get; set; }
public float Alpha { get; set; }
public float AngleRadians { get; set; }
public float BandWidthNormalized { get; set; }
public float FeatherNormalized { get; set; }
public float CoreWidthNormalized { get; set; }
public float CornerRadiusPx { get; set; }
public float Intensity { get; set; }
public Vector3 GoldFringeColor { get; set; }
public Vector3 BlueFringeColor { get; set; }
public Vector3 CoreColor { get; set; }

public void Begin(SpriteBatch spriteBatch);
public void Draw(
    SpriteBatch spriteBatch,
    Texture2D pixel,
    Vector2 cardCenter,
    Vector2 cardSize,
    float cardRotation);
public void End(SpriteBatch spriteBatch);
```

### 14.3 Shader inputs

`CardSheen.fx` uses:

- `MatrixTransform`
- `CardSizePx`
- `Progress`
- `Alpha`
- `AngleRadians`
- `BandWidthNormalized`
- `FeatherNormalized`
- `CoreWidthNormalized`
- `CornerRadiusPx`
- `Intensity`
- `GoldFringeColor`
- `BlueFringeColor`
- `CoreColor`

The quad itself is rotated by `SpriteBatch`; the pixel shader operates in card-local UV space.

### 14.4 Pixel behavior

1. Convert UV into card-local pixel coordinates.
2. Evaluate a rounded-rectangle signed distance using `CardSizePx` and `CornerRadiusPx`.
3. Clip pixels outside the rounded card rectangle.
4. Project UV onto the normalized direction for `105 degrees`.
5. Map progress so the band center travels from `-130 percent` to `130 percent` of the projected card span.
6. Build a broad feathered fringe band and a narrow hot core.
7. Blend gold on the leading fringe, warm white in the core, and blue on the trailing fringe.
8. Multiply by component alpha and system intensity.

Use alpha-weighted additive color blending while preserving destination alpha.

### 14.5 SpriteBatch state safety

The event handler executes inside Game1's active scene `SpriteBatch`. It must:

1. Capture blend, sampler, depth, rasterizer, scissor, and texture-slot state.
2. End the active batch.
3. Begin the effect batch with the sheen effect and alpha-weighted additive blend.
4. Draw one card-sized rotated pixel quad.
5. End the effect batch.
6. Restore device state and resume the previous immediate SpriteBatch configuration in `finally`.

An effect load or draw failure must not leave the scene batch ended. Mark the effect unavailable for the frame and restore state before returning.

### 14.6 Sheen timing

For each card slot:

```text
sheenStart = ShowcaseStart
    + slotIndex * RevealStagger
    + SheenDelayFromReveal
```

The default duration is `0.84s`. The latest possible sheen therefore ends at:

```text
3.54 + 0.24 + 0.52 + 0.84 = 5.14s
```

This equality is required: dismissal must not enable while a finite sheen is still running.

## 15. Input And Tooltip Behavior

### 15.1 Blocker

The blocker remains a full-screen entity with:

- `Transform`
- `UIElement`
- `DontDestroyOnLoad`
- Membership in `overlay.booster-pack-opening`

Before ready:

```text
IsHidden = false
IsInteractable = true
EventType = None
SecondaryEventType = None
```

At ready:

```text
EventType = BoosterPackOpeningClose
SecondaryEventType = None
```

### 15.2 Preview entities

Immediately after preview creation:

```text
EventType = None
SecondaryEventType = None
```

Before the item settles:

- `IsHidden = true`
- `IsInteractable = false`
- `Bounds = Rectangle.Empty`

After the item settles but before ready:

- `IsHidden = false`
- `IsInteractable = true`
- Tooltip remains available.
- `EventType = None`, so clicks are swallowed.

At ready:

- Preserve tooltip behavior.
- Set `EventType = BoosterPackOpeningClose`.

Clicking a card preview must never publish `CardClicked` during this overlay.

## 16. DebugEditable Properties

All fields remain on the system that owns the output. Float scales and font scales use `Step = 0.01f`.

### 16.1 Booster Pack Opening tab

#### Timeline

| Property | Default |
| --- | ---: |
| `SummonDurationSeconds` | `0.76` |
| `IdleDurationSeconds` | `0.52` |
| `ChargeDurationSeconds` | `0.85` |
| `CrackDurationSeconds` | `0.65` |
| `RuptureDurationSeconds` | `0.76` |
| `ShowcaseDurationSeconds` | `1.60` |
| `ChargeParticleIntervalSeconds` | `0.26` |
| `RevealStaggerSeconds` | `0.12` |
| `RevealTravelSeconds` | `0.72` |
| `SheenDelaySeconds` | `0.52` |
| `SheenDurationSeconds` | `0.84` |

#### Stage and pack

| Property | Default |
| --- | ---: |
| `StageCenterX` | `960` |
| `StageCenterY` | `540` |
| `PackWidth` | `370` |
| `PackHeight` | `620` |
| `PackCenterY` | `541` |
| `PackAuraSize` | `590` |
| `PackSummonOffsetY` | `-560` |
| `PackSummonStartScale` | `0.54` |
| `PackSummonOvershootY` | `34` |
| `PackSummonOvershootScale` | `1.08` |
| `PackIdleFloatPx` | `14` |
| `PackChargeShakePx` | `5` |
| `PackCrackShakePx` | `10` |
| `RuptureShakeAmplitudePx` | `13` |
| `RuptureShakeDurationSeconds` | `0.58` |

#### Lighting

| Property | Default |
| --- | ---: |
| `BaseBlackoutAlpha` | `0.68` |
| `ChargeBlackoutAlpha` | `0.76` |
| `ShowcaseBlackoutAlpha` | `0.70` |
| `VignetteAlpha` | `0.72` |
| `FloorGlowWidth` | `760` |
| `FloorGlowHeight` | `150` |
| `FloorGlowY` | `830` |
| `StageRayWidth` | `640` |
| `StageRayHeight` | `218` |
| `RewardRaySize` | `320` |
| `RewardRayMinAlpha` | `0.14` |
| `RewardRayMaxAlpha` | `0.20` |

#### Loot layout

| Property | Default |
| --- | ---: |
| `LootSlotWidth` | `360` |
| `LootSlotHeight` | `540` |
| `LootGap` | `70` |
| `LootCenterY` | `540` |
| `RewardArcHeight` | `120` |
| `RewardIdleFloatPx` | `4` |
| `RewardIdlePeriodSeconds` | `2.6` |
| `CardScale` | `1.09` |
| `MedalSize` | `156` |
| `EquipmentIconBox` | `148` |
| `EquipmentIconScale` | `1.55` |

#### FX counts and shockwave

Keep the five existing particle count controls and `ShardCount`, then add editable streak length/width, shard highlight scale, flash alpha, beam alpha, and every shockwave parameter from section 8.2.

#### Title

Keep:

- `RewardTitleY = 62`
- `RewardKickerScale = 0.09`
- `RewardHeadlineScale = 0.58`

Do not add reward-label typography properties.

### 16.2 Card Sheen tab

| Property | Default |
| --- | ---: |
| `AngleDegrees` | `105` |
| `BandWidthNormalized` | `0.11` |
| `FeatherNormalized` | `0.06` |
| `CoreWidthNormalized` | `0.025` |
| `CornerRadiusPx` | `11` |
| `Intensity` | `0.55` |
| `GoldFringeStrength` | `0.28` |
| `BlueFringeStrength` | `0.18` |
| `CoreStrength` | `0.55` |

Timing stays on the Booster Pack Opening tab because that system writes the sheen component timeline.

## 17. Color Palette

### 17.1 Existing opening palette

| Role | CSS | MonoGame |
| --- | --- | --- |
| Blood | `#c51f33` | `new Color(197, 31, 51)` |
| Blood hot | `#ff4056` | `new Color(255, 64, 86)` |
| Gold | `#e9c755` | `new Color(233, 199, 85)` |
| Gold hot | `#fff0a4` | `new Color(255, 240, 164)` |
| Blue | `#65d1ff` | `new Color(101, 209, 255)` |
| Bone title | `#fff7cc` | `new Color(255, 247, 204)` |
| Full blackout | `#000000` | `Color.Black` multiplied by sampled alpha |

### 17.2 Sheen palette

| Role | Value |
| --- | --- |
| Leading fringe | Gold hot `#fff0a4` |
| Core | Warm white `#fffef0` |
| Trailing fringe | Blue `#65d1ff` |

The sheen must remain predominantly white. Fringe strengths stay below the core strength so it reads as reflected light rather than a rainbow overlay.

## 18. Pixel Position Reference

All coordinates use the `1920x1080` virtual screen with top-left origin.

| Element | X | Y | W | H | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| Overlay | `0` | `0` | `1920` | `1080` | Full input and draw coverage. |
| Warm scene bloom | `518` | `115` | `884` | `734` | Scales fixed radial mask. |
| Floor glow | `580` | `830` | `760` | `150` | Centered at `x=960`. |
| Stage rayburst | `640` | `475` | `640` | `218` | No border and no rotation. |
| Pack aura | `665` | `246` | `590` | `590` | Center follows pack shake. |
| Pack visual | `775` | `231` | `370` | `620` | Default settled rect. |
| Rupture core | `850` | `275` | `220` | `520` | Soft inner light. |
| Transient beam field | `470` | `-26` | `980` | `980` | Center near `(960,464)`. |
| Left reward slot | `350` | `270` | `360` | `540` | Center `(530,540)`. |
| Center reward slot | `780` | `270` | `360` | `540` | Center `(960,540)`. |
| Right reward slot | `1210` | `270` | `360` | `540` | Center `(1390,540)`. |
| Reward rayburst | item center - `160` | item center - `160` | `320` | `320` | One per reward. |
| Title region | `580` | `62` | `760` | `150` | Kicker followed by headline. |
| Vignette | `0` | `0` | `1920` | `1080` | Scaled fixed inverted mask. |

Reward visual bounds by kind:

| Kind | Final visual sizing |
| --- | --- |
| Card | Canonical `CardGeometryService` bounds at `CardScale = 1.09`. |
| Medal | `156x156`, centered in slot. |
| Equipment | `148x148` icon box with content scale `1.55`, centered in slot. |

No pixels are reserved for reward captions or labels.

## 19. Helper Method Structure

Keep `Draw` readable by using this division:

```csharp
private BoosterPackOpeningTiming BuildTiming();
private void ProcessMilestones(BoosterPackOpeningOverlayState state);
private void ProcessMilestone(
    BoosterPackOpeningOverlayState state,
    BoosterPackOpeningMilestone milestone);
private void UpdatePresentationParticles(BoosterPackOpeningOverlayState state);
private void UpdateRewardInteractions(BoosterPackOpeningOverlayState state);
private void UpdateCardSheens(BoosterPackOpeningOverlayState state);
private void SetDismissEnabled(BoosterPackOpeningOverlayState state, bool enabled);

private void EnsureRenderResources();
private void DrawSceneWash(BoosterPackOpeningOverlayState state);
private void DrawStageLighting(BoosterPackOpeningOverlayState state);
private void DrawPack(BoosterPackOpeningOverlayState state);
private void DrawCracks(Vector2 center, float rotation, float scale, float alpha);
private void DrawRuptureFx(BoosterPackOpeningOverlayState state);
private void DrawParticles(BoosterPackOpeningOverlayState state);
private void DrawShards(BoosterPackOpeningOverlayState state);
private void DrawLoot(BoosterPackOpeningOverlayState state);
private void DrawLootPlate(Vector2 center, BoosterPackLootAnimationSample sample);
private void DrawLootItem(
    BoosterPackLootPreview preview,
    BoosterPackLootAnimationSample sample);
private void DrawRewardTitle(BoosterPackOpeningOverlayState state);
private void DrawVignette();
```

Remove:

- `DrawAltarRing`
- Sprite-based `DrawShockwave`
- The current line-based card shine block
- Dynamic-size soft-radial cache keys
- Dynamic-size shard mask requests

## 20. Snapshot Fixture

### 20.1 Command contract

Add:

```bash
dotnet run -- snapshot booster-pack-opening
dotnet run -- snapshot booster-pack-opening --time 3.10 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 5.14 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 4.70 --seed 1337 no-shaders
```

Arguments:

| Argument | Default | Validation |
| --- | ---: | --- |
| `--time` | `5.14` | Finite value in `0.0..30.0`. |
| `--seed` | `1337` | Any valid signed 32-bit integer. |

Output slug:

```text
time-<seconds>-seed-<seed>
```

Store captures under:

```text
debug/snapshots/booster-pack-opening/
```

### 20.2 Deterministic setup

- Construct `BoosterPackOpeningDisplaySystem` with an internal optional seeded `Random` dependency used only by runtime random generation.
- Production `Game1` uses normal nondeterministic construction.
- The fixture supplies the requested seed.
- Add an internal `OpenForSnapshot(float elapsedSeconds)` hook that runs the normal open reset, advances timeline milestones from zero to the requested time, prunes expired FX, and samples final interaction state.
- The hook exists only to seek presentation time; it must not contain a separate render implementation.

### 20.3 Required captures

| Sample | Time |
| --- | ---: |
| Summon descent | `0.45` |
| Charge | `1.70` |
| Crack | `2.40` |
| Rupture | `3.10` |
| Reward travel | `3.95` |
| Sheen | `4.70` |
| Ready | `5.14` |
| Ready without shaders | `5.14` |

Use at least one deterministic seed that produces three cards to verify maximum card width and multiple simultaneous sheens.

## 21. Unit Tests

### 21.1 Timeline service tests

Cover:

- Exact phase at `0.00`, `0.76`, `1.28`, `2.13`, `2.78`, `3.54`, and `5.14`.
- Negative time clamps to summon start.
- Ready remains stable for elapsed values beyond `5.14`.
- Changing one phase duration shifts every later boundary and ready time.
- A large update returns every crossed milestone once and in chronological order.
- Charge pulses stop before crack.
- Reveal start is indexed by slot, not reward kind.
- Each reward finishes at its final slot center with scale `1`, rotation `0`, and alpha `1`.
- Left, center, and right rewards begin with the specified rotation signs.
- Latest sheen reaches progress `1` at `5.14`.
- `CanDismiss` is false immediately before ready and true at ready.
- Rupture shake is deterministic and returns zero after its duration.

### 21.2 Input tests

Cover:

- `BoosterPackOpeningClose` delegates to one `CloseBoosterPackOpeningOverlayEvent`.
- Event type `None` publishes no close event.
- Preview cards are configured without `CardClicked` after creation.
- Before ready, blocker and preview event types are `None`.
- At ready, blocker and all settled preview event types are `BoosterPackOpeningClose`.
- Close cleanup removes preview entities and disables the input context.

### 21.3 Card rendering regression tests

Extend existing card display tests to cover:

- `CardRenderScaledEvent` defaults to zero rotation.
- Non-zero rotation reaches `CardBaseRenderStartedEvent` and `CardBaseRenderCompletedEvent`.
- Original card transform position, scale, and rotation are restored after rendering.
- Existing alpha and clip behavior remain unchanged.

## 22. Manual Verification

### 22.1 Visual sequence

- [ ] The active scene remains visible but subdued beneath the overlay.
- [ ] No persistent ring circumference crosses the screen.
- [ ] No reward rayburst rotates after settling.
- [ ] Pack summon overshoots and settles without snapping.
- [ ] Charge particles visibly travel inward rather than appearing as shrinking dots.
- [ ] Crack branches have glow, body, and hot-core depth.
- [ ] Pack halves peel from the original seam and fully clear the reward row.
- [ ] Rupture distortion is brief and does not leave a visible ring.
- [ ] Rewards travel from the rupture core in slot order.
- [ ] Three cards fit without overlap.
- [ ] Duplicate reward kinds still reveal one after another.
- [ ] Card rotation matches medal and equipment reveal motion.
- [ ] Card sheen stays inside card corners and rotates with the card.
- [ ] No reward names, type labels, or caption plates are drawn.
- [ ] Final title and reward visuals remain readable over WayStation, Battle, and Climb backgrounds.

### 22.2 Interaction

- [ ] Clicking the backdrop before `5.14s` does nothing.
- [ ] Clicking a preview card before `5.14s` does not play the card or close the overlay.
- [ ] Hover tooltips work after each reward settles.
- [ ] Clicking the backdrop at or after `5.14s` closes the overlay.
- [ ] Clicking any card, medal, or equipment at or after `5.14s` closes the overlay.
- [ ] External close events still close immediately.
- [ ] Reopening after close creates a clean new random presentation.

### 22.3 Shader and render state

- [ ] `no-shaders` omits sheen and distortion without errors.
- [ ] Tooltips, debug overlays, and cursor still render after a sheen pass.
- [ ] Scissor state from other modal/card draws is restored.
- [ ] No SpriteBatch begin/end exception occurs when multiple card sheens render in one frame.
- [ ] A failed effect load does not break subsequent scene rendering.

### 22.4 Performance

- [ ] No texture is created from an animated diameter during `Draw`.
- [ ] After opening resources are prepared, repeated ready-state frames allocate no new primitive textures.
- [ ] Three-card sheen rendering creates no per-frame render targets.
- [ ] Particle and shard collections are pruned after their finite durations.
- [ ] `DeleteCachesEvent` leaves no stale booster-owned resource references.

## 23. Build And Verification Commands

Run in this order after implementation:

```bash
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj
dotnet build /p:SkipMonoGameContentPipeline=false
dotnet run -- snapshot booster-pack-opening --time 0.45 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 1.70 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 2.40 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 3.10 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 3.95 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 4.70 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 5.14 --seed 1337
dotnet run -- snapshot booster-pack-opening --time 5.14 --seed 1337 no-shaders
dotnet build
```

The content-pipeline build is required to compile `CardSheen.fx`. On macOS or Linux it depends on the repository's MGFXC Wine setup.

## 24. Acceptance Criteria

Implementation is complete when:

1. The overlay follows the phase and timing contract in this plan.
2. Persistent circle outlines are absent from pack and reward presentation.
3. Dynamic animation sizes do not create new cached textures per frame.
4. The rupture uses one transient post-process shockwave and sprite-based fallback visuals remain complete under `no-shaders`.
5. Every preview card uses canonical card rendering with authored reveal rotation and alpha.
6. Every preview card receives a clipped shader sheen that finishes before dismissal is enabled.
7. Early clicks are ignored and all primary click targets close the overlay in the ready state.
8. Preview cards can never route into gameplay click handling.
9. Hover tooltips remain available without adding visible reward captions.
10. Runtime preview rewards remain independently random and presentation-only.
11. Draw paths perform no timeline, ECS, input, resource-load, or cache mutation.
12. Snapshot captures cover all major phases and the no-shader path.
13. Tests and both required builds succeed.

## 25. Fidelity Notes

- The current underlying game scene intentionally replaces the mockup's fixed Hellrift image.
- The HTML altar rings and reward labels are intentionally omitted.
- CSS blur and drop-shadow filters are approximated with fixed radial masks and layered sprite passes.
- The shader sheen improves on the HTML white gradient while preserving its diagonal sweep timing and direction.
- Runtime particle and reward randomness remains nondeterministic; snapshot rendering uses an explicit seed.
- The final ready state retains a low-amplitude idle float and glow, but all finite summon, rupture, travel, and sheen effects are complete before clicks can dismiss.
