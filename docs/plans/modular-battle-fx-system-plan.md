# Unified Modular Battle FX System Implementation Plan

## Document Status

- **Status:** Revised design, ready for implementation.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **Primary scene:** `SceneId.Battle`.
- **Mockup:** `mockups/card-fx-modular-animation-system-v1.html`
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required. Recipes live on runtime object definitions and do not require save migration.

This plan replaces the previous additive/legacy-fallback design. There is no separate "legacy vs modular" branch for the object-owned battle FX covered by this plan.

---

## 1. Objective

Create a unified composable battle visual effects system for object-owned battle presentation:

- Cards
- Equipment activations
- Medal activations
- Enemy attacks

Each object may define a code-owned visual effect recipe. Runtime systems translate those recipes into cosmetic animation requests. Display systems own animation state and rendering. Gameplay systems only publish events or enqueue queue wrappers. Visual effects must never mutate gameplay state directly.

The current card `"Attack"` and `"Buff"` animations are migrated into this modular system in v1:

- The current player attack lunge becomes an `ActorLunge` module.
- The current buff squash/stretch becomes an `ActorSquashStretch` module.
- Existing attack and buff card constructors stop setting `CardBase.Animation` and instead set `VisualEffectRecipe`.

Enemy attack lunge/impact sequencing also becomes modular in v1:

- Enemy attacks with no custom recipe use a default `EnemyAttackLunge()` recipe.
- Enemy attacks with custom recipes include any lunge, impact, SFX, and visual modules in that recipe.
- The modular coordinator publishes the authoritative enemy impact signal.

Equipment and medals are not forced to invent default visuals:

- Equipment or medals with `ActivationEffectRecipe == null` keep their current gameplay behavior and do not queue a visual wait.
- Equipment or medals with a recipe use the same modular request path as all other recipe-driven object FX.

Unrelated battle visuals are out of scope and remain independent unless this plan explicitly touches them. This includes damage splash from `ModifyHpEvent`, passive displays, poison/recoil/intimidate displays, background shaders, defeat pixel bursts, tutorial UI, and other HUD/scene presentation systems.

The system also adds debug menu playback lists:

- `Card Modular Effects`
- `Equipment Modular Effects`
- `Medal Modular Effects`
- `Enemy Attack Modular Effects`

Debug playback uses the same rendering path as real modular effects, is marked `IsPreview = true`, and must not update gameplay state.

---

## 2. Canonical Product Decisions

### 2.1 Unified rollout

- There is no legacy animation fallback for card attack/buff or enemy attack lunge.
- All existing card constructors with `Animation = "Attack"` or `Animation = "Buff"` are migrated to recipes in v1.
- `CardBase.Animation` is removed from the target design.
- Existing card play code no longer interprets `"Attack"` or `"Buff"`.
- Enemy attacks without custom recipes use `VisualEffectPresets.EnemyAttackLunge()`.
- Equipment and medals remain recipe-optional; no recipe means no visual activation wait.

### 2.2 Object-owned recipes

- Recipes are defined in code on object classes, not JSON.
- Add recipe properties to base object classes.
- Reusable presets live in a central static preset class.
- Object constructors choose a preset and may adjust intensity, particle multiplier, timing profile, target intent, modules, or SFX.
- Recipes must be immutable or defensively cloned when requested so one object cannot mutate a shared preset for another object.

### 2.3 Presentation-only ownership

- Display systems own animation state.
- Services remain read-only helpers/calculators.
- Gameplay systems do not hold direct references to display systems.
- Cross-system behavior goes through `EventManager`, `EventQueue`, or `EventQueueBridge`.
- Draw functions do not mutate gameplay state.
- Modular FX systems may write presentation-only components, but not gameplay components such as HP, AP, passives, card zones, or save data.

### 2.4 Deterministic queue behavior

- Card gameplay remains immediate after validation and cost payment, matching current `CardPlaySystem` behavior.
- Card visual queue events serialize presentation only; they must not delay `CardBase.OnPlay`, HP/resource/passive mutation, AP spend, tracking, or card movement.
- Recipe-enabled card visuals enqueue a modular request and wait for modular completion in the presentation queue.
- Enemy attacks still resolve damage/effects at the authoritative impact signal. The modular coordinator publishes that impact signal for every enemy attack recipe.
- Recipe-enabled equipment and medal activations use trigger-queued activation wrappers. Gameplay activation and modular visual start happen in the same queue step, then the trigger queue waits for modular visual completion.
- Debug playback must not enqueue gameplay rule events.

### 2.5 Mirroring

- Recipes are authored in canonical player-to-enemy orientation.
- Runtime mirroring flips horizontal direction automatically when the target is on the opposite side.
- Objects must not define separate left-facing and right-facing recipes.

### 2.6 Debug playback

- Debug playback uses the same rendering path as real modular effects.
- Debug playback marks requests with `IsPreview = true`.
- Debug playback may publish visual events only.
- Debug playback must not call:
  - `CardBase.OnPlay`
  - `EquipmentBase.OnActivate`
  - `MedalBase.Activate`
  - `EnemyAttackBase.OnAttackHit`
  - `EnemyAttackBase.OnAttackReveal`
  - `EnemyAttackBase.OnBlocksConfirmed`
  - HP/passive/resource mutation events
  - tracking events
  - save writes
  - event queue rule resolution for gameplay

---

## 3. Existing Context To Refactor

### 3.1 Current animation paths

- `CardPlaySystem` currently checks `CardBase.Animation`.
  - `"Attack"` enqueues `QueuedStartPlayerAttackAnimation` then `QueuedWaitPlayerImpactEvent`.
  - `"Buff"` enqueues `QueuedStartBuffAnimation(true)` then `QueuedWaitBuffComplete(true)`.
- Current card gameplay is immediate after validation and cost payment.
- `PlayerAnimationSystem` currently owns player lunge, buff/debuff scale, and damage flash through `PlayerAnimationState`.
- `EnemyDisplaySystem` currently listens for `StartEnemyAttackAnimation`, moves the enemy portrait, plays SFX, and publishes `EnemyAttackImpactNow`.
- `EnemyAttackDisplaySystem` currently enqueues enemy attack animation and wait events during block confirmation.
- `SplashEffectAnimationDisplaySystem` reacts to `ModifyHpEvent` and `ApplyPassiveEvent`; it remains independent and is not a card animation fallback.
- `ShockwaveDisplaySystem` and `RectangularShockwaveDisplaySystem` already support shader-backed shockwaves through events.

### 3.2 Target refactor shape

- Remove card string animation interpretation from `CardPlaySystem`.
- Replace player attack and buff start/wait queued events with modular visual request/wait events.
- Replace enemy attack start/wait fallback branching with recipe resolution; default enemy lunge is a recipe.
- Refactor `PlayerAnimationSystem` into a modular actor presentation owner, or replace it with a new system that owns the same presentation outputs.
- Use one actor presentation component for lunge, squash/stretch, damage flash compatibility, and any future actor-only presentation transforms.
- Preserve independent damage/passive splash displays.

---

## 4. Non-Goals

- Do not add JSON loaders for recipes.
- Do not persist recipes to saves.
- Do not add gameplay effects to visual systems.
- Do not make services mutate components, publish events, or change singleton state.
- Do not pass one system instance into another system constructor.
- Do not use `MouseState` or `GamePad`; debug menu continues through existing input services.
- Do not introduce a new scene-level UI framework for debug playback.
- Do not require shaders for the feature to work. Shader-backed modules should degrade by simply omitting shader distortion when shaders are disabled.
- Do not convert unrelated battle visuals into this system in v1.

---

## 5. Target Data Contracts

Names are normative unless an existing namespace conflict requires a mechanical equivalent.

Create:

```text
ECS/Data/VisualEffects/VisualEffectModels.cs
```

Recommended namespace:

```csharp
namespace Crusaders30XX.ECS.Data.VisualEffects;
```

### 5.1 Enums

```csharp
public enum VisualEffectModule
{
    ActorLunge,
    ActorSquashStretch,
    WhiteWash,
    RedVignette,
    Shockwave,
    SlashBand,
    SmokeScreen,
    SwordArc,
    CrossSlash,
    ClawSlash,
    Bite,
    RockBlast,
    HammerArc,
    CrossBloom,
    Ring,
    Halo,
    Beam,
    Rays,
    Shards,
    Debris,
    SmokeBlobs,
    Cracks,
    HitFlash,
    Shake,
    PunchZoom,
    HitStop
}
```

`ActorLunge` reproduces the current player/enemy portrait lunge behavior. `ActorSquashStretch` reproduces the current buff scale keyframes. `ClawSlash`, `Bite`, and `RockBlast` are general visual primitives and must not force target selection by themselves.

```csharp
public enum VisualEffectTimingProfile
{
    PlayerAttack,
    PlayerBuff,
    EnemyAttackLunge,
    SnapImpact,
    HeavyImpact,
    HolyRise,
    RitualPulse,
    DefensiveLock,
    FlickerChaos
}
```

```csharp
public enum VisualEffectTargetRole
{
    Enemy,
    Player,
    Self,
    Opponent
}
```

```csharp
public enum VisualEffectSourceKind
{
    Card,
    Equipment,
    Medal,
    EnemyAttack,
    Debug
}
```

### 5.2 Recipe model

```csharp
public sealed class VisualEffectRecipe
{
    public string Id { get; init; } = string.Empty;
    public VisualEffectTimingProfile Timing { get; init; } = VisualEffectTimingProfile.SnapImpact;
    public VisualEffectTargetRole TargetRole { get; init; } = VisualEffectTargetRole.Enemy;
    public float Intensity { get; init; } = 1f;
    public float ParticleMultiplier { get; init; } = 1f;
    public IReadOnlyList<VisualEffectModule> Modules { get; init; } = Array.Empty<VisualEffectModule>();
    public SfxTrack StartSfx { get; init; } = SfxTrack.None;
    public SfxTrack ImpactSfx { get; init; } = SfxTrack.None;
    public float StartSfxVolume { get; init; } = 0.5f;
    public float ImpactSfxVolume { get; init; } = 0.5f;
    public float StartSfxPitch { get; init; } = 0f;
    public float ImpactSfxPitch { get; init; } = 0f;
}
```

Implement helper methods without mutating shared instances:

```csharp
public VisualEffectRecipe Clone();
public VisualEffectRecipe WithIntensity(float intensity);
public VisualEffectRecipe WithParticleMultiplier(float particleMultiplier);
public VisualEffectRecipe WithTarget(VisualEffectTargetRole targetRole);
public VisualEffectRecipe WithTiming(VisualEffectTimingProfile timing);
public VisualEffectRecipe WithModules(params VisualEffectModule[] modules);
public VisualEffectRecipe WithStartSfx(SfxTrack track, float volume = 0.5f, float pitch = 0f);
public VisualEffectRecipe WithImpactSfx(SfxTrack track, float volume = 0.5f, float pitch = 0f);
```

Use immutable copies internally. Prefer a private array or `ImmutableArray<VisualEffectModule>` exposed as `IReadOnlyList<VisualEffectModule>`. Normalize duplicate modules away while preserving first occurrence order.

### 5.3 Timing profile model

```csharp
public readonly struct VisualEffectTiming
{
    public float DurationSeconds { get; init; }
    public float ImpactTimeSeconds { get; init; }
    public float HitStopStartSeconds { get; init; }
    public float HitStopDurationSeconds { get; init; }
}
```

Create:

```csharp
public static class VisualEffectTimingProfileResolver
{
    public static VisualEffectTiming Resolve(VisualEffectTimingProfile profile);
}
```

Defaults:

| Profile | Duration | Impact | Hit-stop start | Hit-stop duration |
| --- | ---: | ---: | ---: | ---: |
| `PlayerAttack` | `0.20f` | `0.20f` | `0.0f` | `0.0f` |
| `PlayerBuff` | `0.96f` | `0.36f` | `0.0f` | `0.0f` |
| `EnemyAttackLunge` | `0.20f` | `0.20f` | `0.0f` | `0.0f` |
| `SnapImpact` | `0.56f` | `0.18f` | `0.13f` | `0.08f` |
| `HeavyImpact` | `0.84f` | `0.26f` | `0.20f` | `0.145f` |
| `HolyRise` | `1.10f` | `0.36f` | `0.0f` | `0.0f` |
| `RitualPulse` | `0.98f` | `0.30f` | `0.0f` | `0.0f` |
| `DefensiveLock` | `0.72f` | `0.22f` | `0.0f` | `0.0f` |
| `FlickerChaos` | `0.86f` | `0.18f` | `0.14f` | `0.08f` |

Clamp impact to `0..DurationSeconds`.

### 5.4 Presets

Create:

```text
ECS/Data/VisualEffects/VisualEffectPresets.cs
```

Add static factory methods:

```csharp
public static VisualEffectRecipe PlayerAttack();
public static VisualEffectRecipe PlayerBuff();
public static VisualEffectRecipe EnemyAttackLunge();
public static VisualEffectRecipe LightSlash();
public static VisualEffectRecipe HeavyHammer();
public static VisualEffectRecipe HolyStrike();
public static VisualEffectRecipe HolySupport();
public static VisualEffectRecipe DefensiveGuard();
public static VisualEffectRecipe BloodRitual();
public static VisualEffectRecipe EnemySlash();
public static VisualEffectRecipe EnemyHeavyImpact();
public static VisualEffectRecipe EnemyClawSlash();
public static VisualEffectRecipe EnemyBite();
public static VisualEffectRecipe EnemyRockBlast();
```

Each method returns a fresh recipe instance.

Preset defaults:

| Preset | Timing | Target | Modules | SFX |
| --- | --- | --- | --- | --- |
| `PlayerAttack` | `PlayerAttack` | `Enemy` | `ActorLunge` | start `SwordAttack` |
| `PlayerBuff` | `PlayerBuff` | `Player` | `ActorSquashStretch` | start `Prayer` |
| `EnemyAttackLunge` | `EnemyAttackLunge` | `Player` | `ActorLunge` | impact `SwordImpact` |
| `LightSlash` | `SnapImpact` | `Enemy` | `ActorLunge`, `SwordArc`, `HitFlash`, `Debris` | start `SwordAttack` |
| `HeavyHammer` | `HeavyImpact` | `Enemy` | `ActorLunge`, `HammerArc`, `Ring`, `Debris`, `Cracks`, `HitFlash`, `Shockwave`, `Shake`, `PunchZoom`, `HitStop` | start `SwordAttack`, impact `SwordImpact` |
| `HolyStrike` | `HolyRise` | `Enemy` | `ActorLunge`, `CrossBloom`, `Beam`, `Rays`, `Ring`, `WhiteWash`, `HitFlash` | start `Prayer`, impact `SwordImpact` |
| `HolySupport` | `HolyRise` | `Player` | `ActorSquashStretch`, `CrossBloom`, `Halo`, `Beam`, `Rays`, `WhiteWash` | start `Prayer` |
| `DefensiveGuard` | `DefensiveLock` | `Player` | `Ring`, `Halo`, `WhiteWash`, `PunchZoom` | start `GainAegis` |
| `BloodRitual` | `RitualPulse` | `Self` | `RedVignette`, `Ring`, `SmokeBlobs`, `Rays` | none |
| `EnemySlash` | `SnapImpact` | `Player` | `ActorLunge`, `CrossSlash`, `SlashBand`, `HitFlash`, `Shake` | impact `SwordImpact` |
| `EnemyHeavyImpact` | `HeavyImpact` | `Player` | `ActorLunge`, `Ring`, `Debris`, `Cracks`, `HitFlash`, `Shockwave`, `Shake`, `HitStop` | impact `SwordImpact` |
| `EnemyClawSlash` | `SnapImpact` | `Player` | `ActorLunge`, `ClawSlash`, `HitFlash`, `Debris`, `SlashBand`, `Shake` | impact `SwordImpact` |
| `EnemyBite` | `HeavyImpact` | `Player` | `ActorLunge`, `Bite`, `HitFlash`, `RedVignette`, `Shake`, `HitStop` | impact `SwordImpact` |
| `EnemyRockBlast` | `HeavyImpact` | `Player` | `ActorLunge`, `RockBlast`, `Ring`, `Debris`, `SmokeBlobs`, `HitFlash`, `Shockwave`, `Shake`, `PunchZoom`, `HitStop` | impact `SwordImpact` |

### 5.5 Base object properties

Add imports rather than fully qualified names.

In `CardBase`:

```csharp
public VisualEffectRecipe VisualEffectRecipe { get; protected set; }
```

Remove `CardBase.Animation` from the target design and migrate every existing constructor assignment.

In `EquipmentBase`:

```csharp
public VisualEffectRecipe ActivationEffectRecipe { get; protected set; }
```

In `MedalBase`:

```csharp
public VisualEffectRecipe ActivationEffectRecipe { get; protected set; }
```

In `EnemyAttackBase`:

```csharp
public VisualEffectRecipe AttackEffectRecipe { get; protected set; }
```

Cards and enemy attacks must always resolve to a recipe at runtime. Equipment and medal recipe properties are nullable by convention.

---

## 6. Event Contracts

Create or extend:

```text
ECS/Events/VisualEffectEvents.cs
```

Keep existing defeat pixel burst events intact.

Add:

```csharp
public sealed class VisualEffectRequested
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public VisualEffectRecipe Recipe { get; init; }
    public Entity Source { get; init; }
    public Entity Target { get; init; }
    public VisualEffectSourceKind SourceKind { get; init; }
    public string SourceId { get; init; } = string.Empty;
    public string ContextId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsPreview { get; init; }
}
```

```csharp
public sealed class VisualEffectImpactReached
{
    public Guid RequestId { get; init; }
    public bool IsPreview { get; init; }
}
```

```csharp
public sealed class VisualEffectCompleted
{
    public Guid RequestId { get; init; }
    public bool IsPreview { get; init; }
}
```

Rules:

- `Recipe` must be cloned before being stored by any display/coordinator system.
- Factory methods clone recipes before assigning them to requests.
- The coordinator clones again when creating active effects so it can bake global debug multipliers into the active recipe.
- `VisualEffectRequested` is immutable after construction.
- Accepted `ActiveVisualEffect` instances require non-null concrete source and target anchors. No fallback screen anchors are allowed.
- `ContextId` is used by real enemy attack requests. The coordinator publishes `EnemyAttackImpactNow { ContextId }` at modular impact for accepted non-preview enemy attack requests.
- Completion events are visual timing events only. They do not imply gameplay success.
- Preview completion events must not be consumed by gameplay queue waits unless the wait was created for the same request ID.
- If the coordinator rejects a preview request, it logs quietly and publishes no lifecycle events.
- If the coordinator rejects a non-preview request that has already been published, it logs loudly and immediately publishes `VisualEffectImpactReached` and `VisualEffectCompleted` for that request ID. If the rejected request is an enemy attack with a non-empty `ContextId`, it also publishes `EnemyAttackImpactNow` for that context.

---

## 7. Queue Contracts

Create:

```text
ECS/Scenes/BattleScene/QueuedStartVisualEffect.cs
ECS/Scenes/BattleScene/QueuedWaitVisualEffectImpact.cs
ECS/Scenes/BattleScene/QueuedWaitVisualEffectComplete.cs
ECS/Scenes/BattleScene/QueuedActivateEquipmentWithVisual.cs
ECS/Scenes/BattleScene/QueuedActivateMedalWithVisual.cs
```

Do not create card legacy wrappers. Do not create enemy legacy wrappers.

### 7.1 Start wrapper

`QueuedStartVisualEffect`:

- Constructor accepts a fully built `VisualEffectRequested`.
- Publishes the request in `StartResolving`.
- Marks itself complete immediately.
- Stores the `RequestId` in `Payload`.

### 7.2 Impact wait wrapper

`QueuedWaitVisualEffectImpact`:

- Constructor accepts `Guid requestId`.
- Subscribes to `VisualEffectImpactReached`.
- Completes only when `RequestId` matches.
- Unsubscribes on completion.

### 7.3 Completion wait wrapper

`QueuedWaitVisualEffectComplete`:

- Constructor accepts `Guid requestId`.
- Subscribes to `VisualEffectCompleted`.
- Completes only when `RequestId` matches.
- Unsubscribes on completion.

Do not complete based only on `IsPreview` or source kind.

No modular visual wait has timeout behavior. Waits ignore non-matching request IDs and remain waiting until the matching event is published. Existing `EventQueue.Clear()` has no cancellation/dispose hook, so waits unsubscribe on normal completion only; document this as an existing queue limitation rather than extending queue lifecycle in this plan.

### 7.4 Equipment and medal activation wrappers

Recipe-enabled equipment and medal activations enqueue trigger events instead of running directly.

`QueuedActivateEquipmentWithVisual`:

- Runs existing equipment activation gameplay inside `StartResolving`.
- Exact order: `OnActivate`, publish `EquipmentAbilityTriggered`, build/publish `VisualEffectRequested`, then wait for modular completion.
- If visual request creation fails, log and complete immediately after gameplay activation.
- Validation happens before enqueue only; the queued action assumes activation remains valid.

`QueuedActivateMedalWithVisual`:

- Runs existing medal activation gameplay inside `StartResolving`.
- Exact order: publish `MedalTriggered`, call `medal.Activate()`, build/publish `VisualEffectRequested`, then wait for modular completion.
- If visual request creation fails, log and complete immediately after gameplay activation.
- Do not add generic medal validation beyond the existing event path that emits `MedalActivateEvent`.

---

## 8. Runtime Request Resolution

Create:

```text
ECS/Services/VisualEffectRequestFactory.cs
```

This service must be read-only. It may inspect entities and object data but must not publish events, enqueue events, mutate components, or change singleton state.

Recommended signatures:

```csharp
public static VisualEffectRequested? ForCard(
    EntityManager entityManager,
    Entity cardEntity,
    VisualEffectRecipe recipe,
    bool isPreview = false);
```

```csharp
public static VisualEffectRequested? ForEquipment(
    EntityManager entityManager,
    Entity equipmentEntity,
    VisualEffectRecipe recipe,
    bool isPreview = false);
```

```csharp
public static VisualEffectRequested? ForMedal(
    EntityManager entityManager,
    Entity medalEntity,
    VisualEffectRecipe recipe,
    bool isPreview = false);
```

```csharp
public static VisualEffectRequested? ForEnemyAttack(
    EntityManager entityManager,
    Entity enemyEntity,
    EnemyAttackBase attack,
    VisualEffectRecipe recipe,
    string contextId,
    bool isPreview = false);
```

```csharp
public static VisualEffectRequested? ForDebugPreview(
    EntityManager entityManager,
    VisualEffectSourceKind sourceKind,
    string sourceId,
    string displayName,
    VisualEffectRecipe recipe);
```

Target resolution:

- `VisualEffectTargetRole.Enemy`: prefer the named `"Enemy"` entity when active and it has an `Enemy` component, then fall back to first active `Enemy`.
- `VisualEffectTargetRole.Player`: first `Player`.
- `VisualEffectTargetRole.Self`: player actor for card/equipment/medal requests; enemy actor for enemy attack requests.
- `VisualEffectTargetRole.Opponent`: infer ownership from `VisualEffectSourceKind`. `EnemyAttack` means opponent is player; `Card`, `Equipment`, and `Medal` mean opponent is enemy.

Source resolution:

- Cards use the card entity as source when it has a valid `Transform`; otherwise use the player actor.
- Equipment and medals use the player actor as source, not the UI item entity.
- Enemy attacks use the enemy actor as source.
- Debug card/equipment/medal previews use player source and recipe target.
- Debug enemy attack previews use enemy source and player target.

Real gameplay factory methods return `null` if required source or target resolution fails. Callers must not publish a null request or enqueue waits for it.

---

## 9. Gameplay Integration

### 9.1 Card play

Modify `CardPlaySystem` only at the existing animation enqueue point.

New behavior:

1. Read `card.VisualEffectRecipe`.
2. If the recipe is non-null:
   - Build a `VisualEffectRequested` through `VisualEffectRequestFactory.ForCard`.
   - If request creation succeeds, enqueue `QueuedStartVisualEffect` and `QueuedWaitVisualEffectComplete`.
   - Continue immediately to `OnPlay`; do not wait inside `CardPlaySystem`.
   - If request creation fails, log and continue immediately to `OnPlay`.
3. If the recipe is null, enqueue no card visual and continue immediately to `OnPlay`.

Remove all card play checks for `card.Animation == "Attack"` and `card.Animation == "Buff"`.

All existing cards that currently set `Animation = "Attack"` or `Animation = "Buff"` must be migrated to recipes so player-facing behavior is preserved without fallback.

### 9.2 Equipment activation

Modify `EquipmentManagerSystem.OnEquipmentActivate` after all existing validation succeeds:

- If `ActivationEffectRecipe == null`, keep current direct behavior unchanged.
- If `ActivationEffectRecipe != null`, enqueue `QueuedActivateEquipmentWithVisual` on the trigger queue and return.
- The queued wrapper runs `equipment.Equipment.OnActivate`, publishes `EquipmentAbilityTriggered`, publishes modular visual request, and waits for modular completion.
- Validation happens only before enqueue.
- Do not decrement uses from visual code.
- Existing equipment use accounting remains wherever the equipment object or manager currently performs it.
- If visual request creation fails inside the queued wrapper, gameplay activation still runs and the wrapper completes immediately.

### 9.3 Medal activation

Modify `MedalManagerSystem.OnMedalActivate`:

1. Resolve the equipped medal.
2. If `ActivationEffectRecipe == null`, keep the current `EventQueueBridge.EnqueueTriggerAction` behavior unchanged.
3. If `ActivationEffectRecipe != null`, enqueue `QueuedActivateMedalWithVisual` on the trigger queue.
4. The queued wrapper publishes `MedalTriggered`, calls `medal.Activate()`, publishes modular visual request, and waits for modular completion.

Do not add generic medal validation beyond the existing `MedalActivateEvent` path. If visual request creation fails inside the queued wrapper, gameplay activation still runs and the wrapper completes immediately.

### 9.4 Enemy attack

Modify the enemy attack animation start path where `QueuedStartEnemyAttackAnimation(ctx)` and `QueuedWaitImpactEvent(ctx)` are currently enqueued.

New behavior:

1. Resolve current planned attack definition for `ctx`.
2. Resolve recipe:
   - Use `attack.AttackEffectRecipe` when non-null.
   - Otherwise use `VisualEffectPresets.EnemyAttackLunge()`.
3. Build request with source enemy, target player, source kind `EnemyAttack`, source ID attack ID, and `ContextId = ctx`.
4. Enqueue `QueuedStartVisualEffect`.
5. Enqueue `QueuedWaitVisualEffectImpact`.
6. Preserve `QueuedAdvanceToNextPlannedAttackEvent` and surrounding attack resolution ordering.

The modular coordinator publishes the single authoritative `EnemyAttackImpactNow { ContextId = ctx }` at modular impact. Do not publish enemy attack impact from `EnemyDisplaySystem`.

If modular request creation fails before publishing, log loudly and enqueue an emergency default enemy lunge request if possible. If no request can be created because required battle actors are missing, publish the required impact signal through the same anti-deadlock path used by coordinator rejection.

---

## 10. Coordinator System

Create:

```text
ECS/Scenes/BattleScene/ModularEffectCoordinatorSystem.cs
```

Attributes:

```csharp
[DebugTab("Modular Effect Coordinator")]
```

System responsibilities:

- Subscribe to `VisualEffectRequested`.
- Clone and validate recipes.
- Resolve source and target anchors.
- Reject requests whose concrete source/target anchors cannot be resolved.
- Create `ActiveVisualEffect` entities.
- Publish recipe start SFX when accepting a request.
- Tick elapsed time.
- Publish recipe impact SFX when elapsed crosses impact time.
- Publish `VisualEffectImpactReached` once when elapsed crosses impact time.
- Publish `VisualEffectCompleted` once when elapsed reaches duration.
- For accepted non-preview enemy attack requests with non-empty `ContextId`, publish `EnemyAttackImpactNow` once at modular impact.
- Remove completed effects.
- Publish shockwave events at impact for requests with `VisualEffectModule.Shockwave`.
- Expose active effect snapshots to display systems through ECS components, not direct system references.

Create:

```text
ECS/Components/VisualEffectComponents.cs
```

```csharp
public sealed class ActiveVisualEffect : IComponent
{
    public Guid RequestId { get; set; }
    public VisualEffectRecipe Recipe { get; set; }
    public VisualEffectTiming Timing { get; set; }
    public Entity Source { get; set; }
    public Entity Target { get; set; }
    public Vector2 SourceAnchor { get; set; }
    public Vector2 TargetAnchor { get; set; }
    public Vector2 ImpactAnchor { get; set; }
    public int DirectionSign { get; set; } = 1;
    public float ElapsedSeconds { get; set; }
    public bool ImpactPublished { get; set; }
    public bool IsPreview { get; set; }
    public VisualEffectSourceKind SourceKind { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string ContextId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
```

Anchor resolution:

- Prefer `PortraitInfo.LastDrawCenter` when present and non-zero.
- Fall back to `Transform.Position`.
- For card source, use card `Transform.Position`.
- Do not use fallback screen-space anchors for preview or gameplay.
- Active effect anchors are captured once at creation time and are not updated every frame.
- Impact anchor defaults to target anchor.

Mirroring:

```csharp
DirectionSign = TargetAnchor.X >= SourceAnchor.X ? 1 : -1;
```

Cleanup:

- Remove all `ActiveVisualEffect` entities on `LoadSceneEvent`.
- Remove all active preview effects when leaving battle scene.
- Subscribe to `DeleteCachesEvent` only if the coordinator owns caches.

Debug editables:

| Property | Default | Notes |
| --- | ---: | --- |
| `GlobalIntensityMultiplier` | `1.0f` | `Step = 0.01f` |
| `GlobalParticleMultiplier` | `1.0f` | `Step = 0.01f` |
| `MaxConcurrentEffects` | `16` | Integer |

Capacity behavior:

- Preview requests may be rejected when at capacity.
- If a real request arrives at capacity, evict the oldest preview if one exists.
- Do not drop real gameplay requests because queues may be waiting on their lifecycle events.

---

## 11. Actor Presentation

Refactor current `PlayerAnimationSystem`/`PlayerAnimationState` into a unified actor presentation owner.

Create or replace with:

```csharp
public sealed class ActorPresentationState : IComponent
{
    public Vector2 DrawOffset { get; set; } = Vector2.Zero;
    public Vector2 ScaleMultiplier { get; set; } = Vector2.One;
    public Color TintColor { get; set; } = Color.White;
}
```

Target behavior:

- `ActorLunge` writes `DrawOffset` for the recipe source actor.
- `ActorSquashStretch` writes `ScaleMultiplier` for the recipe target actor.
- Existing damage flash behavior may remain in the same actor presentation owner so `ModifyHpEvent` tint still works.
- `PlayerDisplaySystem`, `EnemyDisplaySystem`, and overlays that currently read `PlayerAnimationState` are updated to read `ActorPresentationState`.
- No system other than the actor presentation owner writes actor presentation offsets/scales/tints.

`ActorLunge` defaults:

- Player-to-enemy offset matches current player attack feel: lunge toward target by roughly `36px`, duration `0.20s`.
- Enemy-to-player offset matches current enemy lunge feel: lunge toward target by roughly `36px`, duration `0.20s`.
- Direction is derived from source and target anchors.
- Impact occurs at the end for baseline lunge recipes.

`ActorSquashStretch` defaults:

- Reproduce the current buff keyframes:
  - `(1.25, 0.75)` for `0.288s`
  - `(0.75, 1.25)` for `0.096s`
  - `(1.15, 0.85)` for `0.096s`
  - `(0.95, 1.05)` for `0.144s`
  - `(1.05, 0.95)` for `0.096s`
  - `(1.0, 1.0)` for `0.240s`

Remove `StartPlayerAttackAnimation`, `PlayerAttackImpactNow`, `StartBuffAnimation`, `BuffAnimationComplete`, and their queue wrappers from the target implementation. Update callers to use modular visual requests and waits instead of adapters.

---

## 12. Display Systems

Use multiple display systems so each owns its own animation/rendering logic.

### 12.1 Screen treatment display

Create:

```text
ECS/Scenes/BattleScene/ModularEffectScreenDisplaySystem.cs
```

Attributes:

```csharp
[DebugTab("Modular FX Screen")]
```

Draws modules:

- `WhiteWash`
- `RedVignette`
- `SlashBand`
- `SmokeScreen`
- `Shake`
- `PunchZoom`
- `HitStop`

Implementation notes:

- Fullscreen wash/vignette/smoke use `_pixel` strips, cached radial textures, or simple transparent overlays.
- Shake and punch write a battle presentation transform that affects player and enemy portrait drawing only.
- Do not mutate actor `Transform.Position`.
- Hit-stop freezes only modular effect visual sampling for its duration; it must not pause game simulation.

Create component:

```csharp
public sealed class BattlePresentationTransform : IComponent
{
    public Vector2 Offset { get; set; } = Vector2.Zero;
    public Vector2 Scale { get; set; } = Vector2.One;
}
```

`ModularEffectScreenDisplaySystem` owns this component in v1.

### 12.2 Primitive impact display

Create:

```text
ECS/Scenes/BattleScene/ModularEffectPrimitiveDisplaySystem.cs
```

Attributes:

```csharp
[DebugTab("Modular FX Primitives")]
```

Draws modules:

- `SwordArc`
- `CrossSlash`
- `ClawSlash`
- `Bite`
- `RockBlast`
- `HammerArc`
- `CrossBloom`
- `Ring`
- `Halo`
- `Beam`
- `Rays`
- `Cracks`
- `HitFlash`

All positions are derived from `ActiveVisualEffect` anchors and `DirectionSign`.

### 12.3 Particle display

Create:

```text
ECS/Scenes/BattleScene/ModularEffectParticleDisplaySystem.cs
```

Attributes:

```csharp
[DebugTab("Modular FX Particles")]
```

Draws modules:

- `Shards`
- `Debris`
- `SmokeBlobs`

Particle state is owned by this system. Detect newly created `ActiveVisualEffect` components by `RequestId` and spawn particles once. Do not subscribe directly to `VisualEffectRequested`.

Base counts:

| Module | Base count |
| --- | ---: |
| `Shards` | `11` |
| `Debris` | `12` |
| `SmokeBlobs` | `7` |

### 12.4 Shockwave dispatch

Do not create a duplicate shockwave renderer. The coordinator publishes existing `ShockwaveEvent` for circular impact shockwaves. If shaders are disabled, existing shockwave systems no-op. Other modules still render.

---

## 13. Draw Order And Registration

In `BattleSceneSystem`:

1. Instantiate and register the coordinator, actor presentation system, screen display, primitive display, particle display, and debug provider systems.
2. Register modular update systems in `SystemUpdatePhase.Presentation` before existing display systems that read their output.
3. Coordinator advances elapsed and publishes impact/completion before display systems read active effects for the current frame.
4. Draw order:
   - battle background
   - player and enemy portraits
   - pixel burst / actor defeat effects
   - modular primitive and particle actor-space FX
   - modular screen treatment overlays
   - active character indicator / enemy intent / battle HUD
   - hand/cards/equipment/medals
   - modal foregrounds and debug menu

Practical placement in current draw method:

- Draw primitive/particle systems after `EnemyDisplaySystem.Draw` and `PixelBurstDisplaySystem.Draw`.
- Draw screen overlays after actor-space FX and before HUD resource displays.
- Do not draw modular FX over pay-cost modal foregrounds or debug menu.

---

## 14. Debug Annotation And Menu Extension

### 14.1 New annotation

Extend `ECS/Diagnostics/DebugAttributes.cs`.

Add:

```csharp
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class DebugActionListAttribute : Attribute
{
    public string DisplayName { get; }
    public int Order { get; set; }

    public DebugActionListAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}
```

Add:

```csharp
public sealed class DebugNamedAction
{
    public string Label { get; init; } = string.Empty;
    public Action Invoke { get; init; }
    public bool IsEnabled { get; init; } = true;
}
```

Allowed method signatures:

```csharp
IEnumerable<DebugNamedAction> MethodName()
IReadOnlyList<DebugNamedAction> MethodName()
List<DebugNamedAction> MethodName()
```

### 14.2 Debug providers

Create four systems:

```text
ECS/Scenes/BattleScene/Debug/CardModularEffectsDebugSystem.cs
ECS/Scenes/BattleScene/Debug/EquipmentModularEffectsDebugSystem.cs
ECS/Scenes/BattleScene/Debug/MedalModularEffectsDebugSystem.cs
ECS/Scenes/BattleScene/Debug/EnemyAttackModularEffectsDebugSystem.cs
```

Each system:

- Has a `[DebugTab]` with the exact tab name.
- Has one `[DebugActionList("Play Effects")]` method.
- Uses the corresponding factory to enumerate object definitions.
- Includes only objects with non-null recipes, except enemy attacks should resolve null recipes to `EnemyAttackLunge()` for preview.
- Creates actions that publish `VisualEffectRequestFactory.ForDebugPreview(...)`.
- Does not enqueue gameplay events.
- Does not call object gameplay callbacks.

Debug label format:

```text
Name [id]
```

---

## 15. Initial Recipe Wiring

### 15.1 Cards

Migrate every current card constructor assignment:

- `Animation = "Attack"` becomes `VisualEffectRecipe = VisualEffectPresets.PlayerAttack()` unless a richer preset is chosen.
- `Animation = "Buff"` becomes `VisualEffectRecipe = VisualEffectPresets.PlayerBuff()` unless a richer preset is chosen.

Richer v1 card recipes:

| Card | Recipe |
| --- | --- |
| `ForgeStrike` | `VisualEffectPresets.HeavyHammer()` |
| `Sword` | `VisualEffectPresets.LightSlash()` |
| `Strike` | `VisualEffectPresets.LightSlash()` |
| `Smite` | `VisualEffectPresets.HolyStrike()` |
| `Hammer` | `VisualEffectPresets.HeavyHammer().WithIntensity(0.9f)` |
| `DivineProtection` | `VisualEffectPresets.DefensiveGuard()` |
| `DowseWithHolyWater` | `VisualEffectPresets.HolySupport()` |

Cards not listed above still receive baseline `PlayerAttack()` or `PlayerBuff()` when they currently use those animations.

### 15.2 Equipment

Wire one equipment activation recipe:

| Equipment | Recipe |
| --- | --- |
| `HelmOfSeeing` or `PurgingBracers` | `VisualEffectPresets.DefensiveGuard()` |

Equipment without a recipe keeps direct activation behavior and no visual wait.

### 15.3 Medals

Wire one medal activation recipe:

| Medal | Recipe |
| --- | --- |
| `StLuke` or `StMichael` | `VisualEffectPresets.HolySupport()` |

Medals without a recipe keep current trigger behavior and no visual wait.

### 15.4 Enemy attacks

Wire representative enemy attack recipes:

| Enemy attack | Recipe |
| --- | --- |
| `BoneStrike` or `TrainingStrike` | `VisualEffectPresets.EnemySlash()` |
| `ScorchingClaw` or `FrozenClaw` | `VisualEffectPresets.EnemyClawSlash()` |
| `VelvetFangs` or `RazorMaw` | `VisualEffectPresets.EnemyBite()` |
| `SandBlast`, `SandPound`, or `StoneBarrage` | `VisualEffectPresets.EnemyRockBlast()` |

Every enemy attack not listed above uses `VisualEffectPresets.EnemyAttackLunge()` at runtime.

---

## 16. Implementation Sequence

### Step 1 - Data models and presets

- Add visual effect enums, recipe model, timing model, timing resolver, SFX fields, clone/with helpers, and presets.
- Add tests for clone, presets, SFX defaults, and timing profiles.

### Step 2 - Base object recipe properties and card migration

- Add recipe properties to cards, equipment, medals, and enemy attacks.
- Remove `CardBase.Animation` from the target design.
- Migrate every `Animation = "Attack"` and `Animation = "Buff"` constructor assignment to recipes.

### Step 3 - Events and queue wrappers

- Add modular effect events.
- Add generic start, impact wait, and completion wait wrappers.
- Add trigger-queued equipment/medal activation wrappers.
- Do not add card/enemy legacy wrappers.

### Step 4 - Request factory

- Implement read-only request creation.
- Resolve player/enemy/source/target.
- Clone recipes.
- For enemy attacks, centralize default recipe resolution to `EnemyAttackLunge()` when `AttackEffectRecipe` is null.

### Step 5 - Coordinator and active component

- Subscribe to requests.
- Spawn active effect entities.
- Publish start/impact SFX.
- Tick elapsed time.
- Publish impact and completion once.
- Publish `EnemyAttackImpactNow` at modular impact for accepted real enemy attack requests.
- Emergency-complete rejected real requests.
- Dispatch shockwave at impact.

### Step 6 - Actor presentation refactor

- Replace current player/enemy lunge and buff event handling with recipe-driven `ActorLunge` and `ActorSquashStretch`.
- Update player/enemy display systems and dependent overlays to read the unified actor presentation component.
- Preserve damage flash compatibility without using attack/buff legacy events.

### Step 7 - Display systems

- Implement screen, primitive, and particle display systems.
- Include `ClawSlash`, `Bite`, and `RockBlast`.
- Add `BattlePresentationTransform` ownership in the screen display system.
- Apply intensity, particle multiplier, and direction sign.

### Step 8 - Gameplay integration

- Update `CardPlaySystem` to use recipe requests only.
- Update `EquipmentManagerSystem` and `MedalManagerSystem` for recipe-enabled activation wrappers.
- Update `EnemyAttackDisplaySystem` to enqueue default/custom modular enemy attack requests instead of legacy enemy animation events.
- Remove or stop using obsolete attack/buff/enemy legacy queued events.

### Step 9 - Debug annotation and debug providers

- Add `DebugActionListAttribute` and `DebugNamedAction`.
- Extend debug menu reflection and rendering.
- Add four provider systems and register them in battle.
- Ensure debug preview publishes only `VisualEffectRequested` with `IsPreview = true`.

### Step 10 - Battle registration and draw order

- Instantiate systems.
- Add modular systems to the world in the correct update phase.
- Add draw calls in the required order.
- Ensure no direct system references are passed into other systems.

---

## 17. Rendering Details By Module

All module values are starting defaults. Add `[DebugEditable]` for hardcoded tuning values on display systems.

| Module | Behavior |
| --- | --- |
| `ActorLunge` | Moves source actor portrait visually toward target and back; does not mutate actor `Transform.Position`. |
| `ActorSquashStretch` | Applies current buff scale keyframes to target actor presentation scale. |
| `WhiteWash` | Radial bright wash centered on impact; fades in early and expands/fades out. |
| `RedVignette` | Fullscreen dark red vignette pulse; multiply-like approximation with alpha overlay. |
| `SlashBand` | Wide diagonal band crossing impact direction; mirrored with direction sign. |
| `SmokeScreen` | Several soft dark radial patches around impact; fade and drift upward. |
| `Shake` | Portrait-only draw offset sampled from keyframes; visual only. |
| `PunchZoom` | Portrait-only visual scale pulse around screen center; visual only. |
| `HitStop` | Hold modular FX sample time briefly; do not pause game update. |
| `SwordArc` | Fast tapered slash from source side through impact. |
| `CrossSlash` | Two delayed slash bars crossing at impact. |
| `ClawSlash` | Three fast parallel red-white slash streaks at impact. |
| `Bite` | Closing top/bottom fang impression with red impact ring. |
| `RockBlast` | Circular stone burst with chunk fragments and dusty impact center. |
| `HammerArc` | Dark hammer silhouette rotating into target. |
| `CrossBloom` | Expanding glowing cross at source or target based on recipe target. |
| `Ring` | Expanding circular ring at impact. |
| `Halo` | Ellipse above friendly target, rising and fading. |
| `Beam` | Vertical light beam centered on target. |
| `Rays` | Radial rays rotating and fading. |
| `Cracks` | Jagged red line segments radiating from impact. |
| `HitFlash` | Short white radial flash at impact. |
| `Shards` | Bright jagged particles scattering away from impact. |
| `Debris` | Dark rectangular/polygon particles scattering away from impact. |
| `SmokeBlobs` | Soft circles drifting upward from impact. |

---

## 18. Testing Plan

### 18.1 Unit tests

Add tests under:

```text
tests/Crusaders30XX.Tests/
```

Required scenarios:

- Presets return fresh instances.
- `WithIntensity` does not mutate original recipe.
- `WithParticleMultiplier` does not mutate original recipe.
- Duplicate recipe modules are normalized away.
- Timing profiles resolve expected durations and impact times.
- Recipe SFX defaults are correct for `PlayerAttack`, `PlayerBuff`, and `EnemyAttackLunge`.
- Request factory resolves player/enemy targets correctly.
- Request factory resolves default enemy attack recipes when `AttackEffectRecipe` is null.
- Debug preview request sets `IsPreview = true`.
- Queue waits ignore non-matching request IDs.
- Queue waits complete on matching impact/completion.
- Queue waits do not have timeout behavior.
- Coordinator publishes impact exactly once.
- Coordinator publishes completion exactly once.
- Coordinator publishes `EnemyAttackImpactNow` at modular impact for accepted real enemy attack requests.
- Coordinator emergency-completes rejected non-preview requests.
- Coordinator quietly rejects invalid preview requests without lifecycle events.
- Direction sign is `1` for player-to-enemy and `-1` for enemy-to-player.
- `ActorLunge` writes actor presentation state only.
- `ActorSquashStretch` reproduces current buff keyframe duration and final scale.
- Debug action list reflection ignores invalid signatures.
- Debug action list invokes enabled actions only.

### 18.2 Gameplay path tests

- Every card formerly using `Animation = "Attack"` or `"Buff"` has a non-null `VisualEffectRecipe`.
- Card play no longer checks `CardBase.Animation`.
- Attack recipe card runs `OnPlay` immediately while queuing modular FX.
- Buff recipe card runs `OnPlay` immediately while queuing modular FX.
- Recipe-enabled card with failed modular request creation still runs gameplay immediately.
- Equipment activation with recipe enqueues a trigger activation, calls `OnActivate` exactly once, publishes `EquipmentAbilityTriggered`, starts modular FX, and waits for completion.
- Equipment activation without recipe keeps current direct behavior.
- Medal activation with recipe enqueues a trigger activation, publishes `MedalTriggered`, calls `Activate` exactly once, starts modular FX, and waits for completion.
- Medal activation without recipe keeps current trigger behavior.
- Enemy attack with custom recipe resolves damage from modular impact only.
- Enemy attack without custom recipe uses `EnemyAttackLunge()` and resolves damage from modular impact.
- Debug preview of card/equipment/medal/enemy attack does not call gameplay callbacks.

### 18.3 Manual verification

Run:

```bash
dotnet run -- test-fight hammer skeleton hard
```

Verify:

- Existing attack cards still show a player lunge through modular FX.
- Existing buff cards still show squash/stretch through modular FX.
- `Forge Strike`, `Sword`, `Strike`, and `Smite` show richer modular effects when available.
- Enemy attacks always animate through modular requests, including attacks with no custom recipe.
- Recipe-enabled card gameplay state changes immediately when played, even while visuals continue.
- Recipe-enabled equipment/medal activations stall later trigger queue work until modular visual completion.
- Debug menu has the four modular effect tabs.
- Debug buttons play effects without changing HP, AP, cards, equipment uses, medal counters, passives, or battle phase when battle actors exist.
- Shader disabled mode still shows non-shader modules:

```bash
dotnet run -- no-shaders
```

### 18.4 Final verification

After implementation:

```bash
dotnet build
```

Fix compile errors before handoff.

---

## 19. Acceptance Criteria

The feature is complete when:

- Existing card attack and buff animations are represented by modular recipes.
- `CardBase.Animation` and card play string animation branching are removed from the target behavior.
- All current attack/buff cards have recipes.
- Real gameplay requests modular effects for recipe-enabled cards, equipment, medals, and enemy attacks.
- Enemy attacks without custom recipes use the default modular enemy lunge recipe.
- The modular coordinator is the only path that publishes enemy attack impact for enemy attack visuals.
- Recipe-enabled card gameplay remains immediate.
- Recipe-enabled equipment and medal activations are trigger-queued and wait for modular completion after gameplay activation.
- Equipment and medals without recipes keep existing behavior and do not wait for visuals.
- Effects mirror correctly between player-to-enemy and enemy-to-player.
- Debug menu exposes object-type effect tabs with play buttons.
- Debug playback is purely cosmetic and does not mutate game state.
- All new systems follow ECS event/component ownership rules.
- `dotnet build` succeeds.

---

## 20. Implementation Guardrails

- Use imports, not fully qualified names.
- Add `[DebugTab]` to systems with draw/debug controls.
- Add as many useful `[DebugEditable]` values as practical for animation tuning.
- Float text/display scales use `Step = 0.01f`.
- Draw functions must not update gameplay state.
- Do not read or write `ParallaxLayer` internals.
- Do not use `MouseState` or `GamePad`.
- Do not pass another system as a constructor parameter.
- Services must remain read-only.
- Preview effects must stay visually faithful to the real effect path.
- Keep all debug and display strings ASCII-only.

---

## 21. Likely File Map

| File | Action |
| --- | --- |
| `ECS/Data/VisualEffects/VisualEffectModels.cs` | Create models, enums, timing resolver, SFX fields. |
| `ECS/Data/VisualEffects/VisualEffectPresets.cs` | Create reusable recipe presets, including baseline attack/buff/enemy lunge. |
| `ECS/Components/VisualEffectComponents.cs` | Create `ActiveVisualEffect` and actor presentation components. |
| `ECS/Events/VisualEffectEvents.cs` | Add modular effect events without removing pixel burst events. |
| `ECS/Services/VisualEffectRequestFactory.cs` | Create read-only request builder. |
| `ECS/Scenes/BattleScene/ModularEffectCoordinatorSystem.cs` | Create request coordinator and lifecycle owner. |
| `ECS/Scenes/BattleScene/ModularEffectActorPresentationSystem.cs` | Own actor lunge, squash/stretch, and presentation state. |
| `ECS/Scenes/BattleScene/ModularEffectScreenDisplaySystem.cs` | Create screen overlay renderer. |
| `ECS/Scenes/BattleScene/ModularEffectPrimitiveDisplaySystem.cs` | Create primitive renderer. |
| `ECS/Scenes/BattleScene/ModularEffectParticleDisplaySystem.cs` | Create particle renderer. |
| `ECS/Scenes/BattleScene/QueuedStartVisualEffect.cs` | Create queue start wrapper. |
| `ECS/Scenes/BattleScene/QueuedWaitVisualEffectImpact.cs` | Create impact wait wrapper. |
| `ECS/Scenes/BattleScene/QueuedWaitVisualEffectComplete.cs` | Create completion wait wrapper. |
| `ECS/Scenes/BattleScene/QueuedActivateEquipmentWithVisual.cs` | Trigger-queued recipe-enabled equipment activation. |
| `ECS/Scenes/BattleScene/QueuedActivateMedalWithVisual.cs` | Trigger-queued recipe-enabled medal activation. |
| `ECS/Diagnostics/DebugAttributes.cs` | Add list-backed debug action annotation. |
| `ECS/Scenes/BattleScene/DebugMenuSystem.cs` | Render list-backed debug actions. |
| `ECS/Scenes/BattleScene/Debug/*ModularEffectsDebugSystem.cs` | Add four debug provider systems. |
| `ECS/Objects/Cards/CardBase.cs` | Add recipe property and remove `Animation`. |
| `ECS/Objects/Equipment/EquipmentBase.cs` | Add activation recipe property. |
| `ECS/Objects/Medals/MedalBase.cs` | Add activation recipe property. |
| `ECS/Objects/Enemies/EnemyAttackBase.cs` | Add attack recipe property. |
| `ECS/Objects/Cards/*.cs` | Migrate every `Animation = "Attack"` and `"Buff"` assignment to recipes. |
| `ECS/Objects/EnemyAttacks/*.cs` | Add representative custom enemy attack recipes. |
| `ECS/Scenes/BattleScene/CardPlaySystem.cs` | Use modular recipe request only. |
| `ECS/Scenes/BattleScene/EquipmentManagerSystem.cs` | Enqueue recipe-enabled activation wrapper after validation. |
| `ECS/Scenes/BattleScene/MedalManagerSystem.cs` | Enqueue recipe-enabled activation wrapper. |
| `ECS/Scenes/BattleScene/EnemyAttackDisplaySystem.cs` | Use modular queue path for all enemy attacks. |
| `ECS/Scenes/BattleScene/EnemyDisplaySystem.cs` | Stop publishing attack impact from display lunge path. |
| `ECS/Scenes/BattleScene/PlayerDisplaySystem.cs` | Read unified actor presentation state. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs` | Register and draw modular systems. |

---

## 22. Open Technical Notes

- If primitive drawing requires cached polygons, add methods to `PrimitiveTextureFactory` and clear caches on `DeleteCachesEvent`.
- If debug list rendering becomes too large for `DebugMenuSystem`, extract internal helper methods inside the same system rather than creating a separate UI framework.
- If `HitStop` conflicts with queue timing, keep queue timing based on unpaused elapsed seconds and freeze only visual sampling.
- `EventQueue.Clear()` can drop an active wait without an unsubscribe hook. Modular waits follow existing queue wrapper style and unsubscribe on normal completion only. A future queue cancellation hook could clean this up, but do not add it in this plan.
