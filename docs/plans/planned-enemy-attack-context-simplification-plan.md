# Planned Enemy Attack Context Simplification Plan

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **Primary scene:** `SceneId.Battle`.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required. Battle attack context is transient runtime state. Start fresh with `dotnet run -- new` when validating the change.

This plan removes the GUID-style `PlannedAttack.ContextId` plumbing from planned enemy attacks. The replacement model treats `AttackIntent.Planned[0]` as the only active enemy attack and scopes block assignment, progress, resolution, damage, and presentation around that current attack.

The goal is not to remove every property named `ContextId` in the codebase. Several unrelated systems use context IDs for input overlays, card movement, climb events, diagnostics, and other non-enemy-attack flows. This refactor targets only planned enemy attack identity and logic.

---

## 1. Objective

Simplify enemy attack flow by removing per-planned-attack string context IDs and replacing them with current-attack ownership.

The current implementation creates a unique `ContextId` for every current and next-turn planned enemy attack. That ID is then passed through:

- `PlannedAttack`
- `EnemyAttackProgress`
- `AssignedBlockCard`
- block assignment events
- confirm gating
- queued enemy attack rule events
- enemy damage events
- enemy attack display state
- ambush timer state
- modular visual effect enemy impact callbacks

The ID has a real reason in the current code: it distinguishes one planned attack from another after events and animations have crossed asynchronous queue boundaries. The design is still broader than necessary because the battle rules only allow one enemy attack to be blocked and resolved at a time. Future planned attacks are previews until they become `AttackIntent.Planned[0]`.

The target implementation keeps the valid sequencing behavior while removing the GUID string as a public identity for enemy attack resolution.

---

## 2. Canonical Product Decisions

### 2.1 Current attack is authoritative

- `AttackIntent.Planned[0]` is the active enemy attack.
- Only the active enemy attack can receive block assignments.
- Only the active enemy attack can be confirmed.
- Only the active enemy attack can resolve damage or hit effects.
- All other entries in `AttackIntent.Planned` are queued future attacks or preview data.

### 2.2 Planned attack identity

- Remove `PlannedAttack.ContextId`.
- Do not replace it with another string ID.
- Repeated identical `AttackId` values remain valid because list position, not attack ID, controls active resolution.
- Advancing an attack always removes the first planned attack, not a searched context.

### 2.3 Progress ownership

- Enemy attack progress is active-attack state, not one entity per planned attack.
- Maintain at most one `EnemyAttackProgress` per active enemy.
- Reset or recreate progress when the active planned attack changes.
- Do not pre-create progress for future planned attacks.
- Do not retain old progress after an attack advances.

### 2.4 Block ownership

- Assigned block cards belong to the active enemy attack.
- `AssignedBlockCard` no longer needs a planned attack context ID.
- Block assignment events no longer need to carry a planned attack context ID.
- Returning, discarding, and display systems operate on currently assigned block cards.

### 2.5 Queued attack flow

Keep the current rule order:

1. Discard assigned block cards.
2. Resolve the current attack.
3. Wait for enemy attack absorb presentation.
4. Start the enemy attack visual effect.
5. Wait for visual effect impact.
6. Apply enemy attack impact damage.
7. Advance to the next planned attack.

The queue must not search by context. Each queued wrapper resolves against the current attack snapshot it was created for or the current active attack at execution time, as specified below.

### 2.6 Visual effect identity

- Keep `VisualEffectRequested.RequestId`; it is the correct identity for waiting on a specific visual effect.
- Remove enemy-attack `ContextId` from `VisualEffectRequested` usage.
- The modular effect coordinator still publishes the authoritative enemy attack impact signal for accepted real enemy attack visual requests.
- Enemy attack impact no longer carries a planned attack context ID.

### 2.7 Other context IDs remain

Do not remove or rename context IDs outside this planned enemy attack flow. Examples that remain out of scope:

- input context IDs
- card move context IDs unrelated to assigned block ownership
- climb event resolution context IDs
- diagnostic overlay context IDs
- visual preview request IDs

---

## 3. Current System Findings

### 3.1 Where context is created

`EnemyIntentPlanningSystem.AddPlanned` currently creates a GUID string for every planned attack, including next-turn preview attacks. It stores the value on `PlannedAttack.ContextId` and publishes it through `IntentPlanned`.

This means future attacks receive runtime IDs before they are active. That is the root cause of much of the context plumbing.

### 3.2 Where context is required today

The current code uses planned attack context to:

- find the planned attack to resolve in `AttackResolutionSystem`
- find or create progress in `EnemyAttackProgressManagementSystem`
- count assigned blockers in `EnemyAttackConfirmAvailabilityService`
- bind cards/equipment to a specific attack in block assignment systems
- wait for assigned-block discard flights in `QueuedDiscardAssignedBlocksEvent`
- wait for absorb completion in `QueuedWaitAbsorbEvent`
- apply pending enemy damage in `EnemyDamageManagerSystem`
- publish enemy attack impact from `ModularEffectCoordinatorSystem`
- reset display and ambush state when the active attack changes
- show and satisfy must-block requirements

Each of these uses can be expressed as "the current attack" because block and resolution phases never handle two active enemy attacks at once.

### 3.3 Why `AttackId` is not enough

Enemies can produce multiple planned attacks in one turn, and repeated `AttackId` values are possible through random selection with replacement. The refactor must not key state by `AttackId`.

The replacement is current list position plus an internal sequence marker for presentation reset, not attack ID.

### 3.4 Why VFX `RequestId` is enough for visual waits

Visual effect queue waits already use `VisualEffectRequested.RequestId`. The planned attack context is not needed to know which effect reached impact. It is only used to tell gameplay which attack is active at the moment of impact.

After this refactor, gameplay reads the active attack and active progress directly when impact occurs.

---

## 4. Target Runtime Model

### 4.1 PlannedAttack

`PlannedAttack` keeps only attack data:

```csharp
public class PlannedAttack
{
    public EnemyAttackId AttackId;
    public int ResolveStep;
    public bool WasBlocked;
    public bool IsAmbush;
    public EnemyAttackBase AttackDefinition;
}
```

No string context or replacement global ID is added.

### 4.2 EnemyAttackProgress

`EnemyAttackProgress` becomes the current attack progress snapshot for an enemy:

```csharp
public class EnemyAttackProgress : IComponent
{
    public Entity Owner { get; set; }
    public Entity Enemy { get; set; }
    public EnemyAttackId AttackId { get; set; }
    public int AttackSequence { get; set; }

    // Existing counters and derived damage fields remain.
}
```

`AttackSequence` is an internal monotonic integer used only to detect that the active planned attack changed. It is not a public event identity and is not passed through gameplay events as a context key.

### 4.3 Attack sequence ownership

Add active-attack sequence state to the enemy or attack intent component. The recommended shape is to keep it on `AttackIntent`:

```csharp
public class AttackIntent : IComponent
{
    public Entity Owner { get; set; }
    public List<PlannedAttack> Planned { get; set; } = new();
    public int ActiveAttackSequence { get; set; }
}
```

Increment `ActiveAttackSequence` whenever the active planned attack changes:

- after planning fills an empty current intent
- after next-turn intent is promoted to current
- after `QueuedAdvanceToNextPlannedAttackEvent` removes `Planned[0]` and another planned attack remains
- after enemy phase reset clears and replans attack intent

Do not increment it every frame.

### 4.4 Enemy attack flow helper

Create one helper service for current enemy attack lookup. It must be read-only.

Recommended service:

```text
ECS/Services/EnemyAttackFlowService.cs
```

Required methods:

- `TryGetCurrentEnemyAttack(EntityManager, out Entity enemy, out AttackIntent intent, out PlannedAttack planned)`
- `TryGetCurrentProgress(EntityManager, out EnemyAttackProgress progress)`
- `GetOrCreateCurrentProgress(EntityManager, Entity enemy, AttackIntent intent, PlannedAttack planned)`
- `ResetCurrentProgress(EntityManager, Entity enemy, AttackIntent intent, PlannedAttack planned)`
- `HasCurrentAttack(EntityManager)`

The service may create or destroy progress only if it is explicitly named as a progress management helper. If the existing service boundary is kept strictly read-only, put create/destroy behavior in `EnemyAttackProgressManagementSystem` and keep lookup helpers read-only. Do not make a generic service mutate unrelated ECS state.

### 4.5 EnemyAttackImpactNow

Change enemy attack impact events from context-specific to current-attack-specific:

```csharp
public class EnemyAttackImpactNow
{
}
```

All subscribers resolve the current attack and current progress when they receive the event.

### 4.6 Confirm pending state

Replace `PhaseState.PendingBlockConfirmContextId` with a boolean:

```csharp
public bool PendingBlockConfirm { get; set; }
```

The pending confirm only applies to the current attack. If the active attack sequence changes, clear it.

---

## 5. Implementation Plan

### 5.1 Update data contracts

- Remove `ContextId` from `PlannedAttack`.
- Remove `ContextId` from enemy attack progress and block-assignment-only components where it only represents planned enemy attack identity.
- Add `AttackIntent.ActiveAttackSequence`.
- Add `EnemyAttackProgress.AttackSequence`.
- Replace `PhaseState.PendingBlockConfirmContextId` with `PendingBlockConfirm`.
- Remove planned attack context from combat events:
  - `IntentPlanned`
  - `ResolveAttack`
  - `AttackResolved`
  - `EnemyAbsorbComplete`
  - `EnemyAttackImpactNow`
  - `ResolvingEnemyDamageEvent`
  - `EnemyDamageAppliedEvent`
  - `TriggerEnemyAttackDisplayEvent`
  - `ShowStunnedOverlay`
- Remove planned attack context from block assignment events:
  - `BlockAssignmentAdded`
  - `BlockAssignmentRemoved`
- Keep context fields on generic card movement events unless every caller in that event family is migrated in the same change.

### 5.2 Update planning

- In `EnemyIntentPlanningSystem.AddPlanned`, stop creating `Guid.NewGuid().ToString("N")`.
- Stop storing context on planned attacks.
- Publish `IntentPlanned` with attack ID, step, and telegraph text only.
- Set or increment `AttackIntent.ActiveAttackSequence` when the first current planned attack is created or promoted.
- Do not assign sequence values to next-turn preview attacks.
- Keep `ResolveStep` for display and debugging if it is still useful.

### 5.3 Update current attack progress management

- Refactor `EnemyAttackProgressManagementSystem` to maintain only the active progress entity for each enemy with current `AttackIntent`.
- On update or on phase change to `Block` or `EnemyAttack`, compare:
  - current enemy
  - current planned attack
  - `AttackIntent.ActiveAttackSequence`
  - progress `AttackSequence`
- If progress is missing or sequence differs, destroy stale progress for that enemy and create a fresh progress row for the current attack.
- Recompute progress from live assigned block cards and passive snapshots.
- Remove logic that pre-creates progress for every `intent.Planned` entry.
- Remove cleanup that compares progress context IDs against all planned attacks.

### 5.4 Update block assignment

- In `HandBlockInteractionSystem`, assign hand cards only when a current planned attack exists.
- In `EquipmentBlockInteractionSystem`, assign equipment only when a current planned attack exists.
- Stop writing planned attack context into `AssignedBlockCard`.
- Continue storing block amount, color, equipment metadata, animation phase, assigned timestamp, and return target.
- Publish `BlockAssignmentAdded` without context.
- On unassign, publish `BlockAssignmentRemoved` without context.
- In `AssignedBlockCardsDisplaySystem`, update filtering, hotkey assignment, previous-card removal, and layout to consider all assigned block cards for the current attack.
- Preserve the current behavior that one newest assigned block card receives the unassign hotkey.

### 5.5 Update assigned-block discard

- Refactor `QueuedDiscardAssignedBlocksEvent` to no longer take a context string.
- When it starts, publish a specific event for assigned-block discard animation instead of a generic debug command if one does not already exist.
- Wait for all active assigned-block discard flights associated with assigned block cards.
- Do not wait on unrelated card movement animations.
- Remove context matching from `CardToDiscardFlight` only if that field is exclusively used for enemy assigned block discard. If generic card movement still needs context, leave the field and stop depending on it for enemy attack sequencing.

### 5.6 Update confirm flow

- Refactor `EnemyAttackConfirmAvailabilityService` to accept no context parameter.
- Confirm availability reads:
  - battle input freeze state
  - current phase
  - current planned attack
  - active assigned blocker count
  - any assigned block animation state
  - tutorial action allowance
  - confirmed active attack sequence
- In `EnemyAttackDisplaySystem`, replace `_confirmedForContext` with `_confirmedAttackSequence`.
- Replace `_pendingConfirmContextId` with `_pendingConfirmSequence` or a boolean plus the active attack sequence.
- `QueueConfirm` sets `PhaseState.PendingBlockConfirm = true`.
- `TryResolvePendingConfirm` resolves only when the pending sequence still equals the current `AttackIntent.ActiveAttackSequence`.
- `ExecuteConfirm` enqueues the context-free rule sequence.

### 5.7 Update attack resolution

- Refactor `QueuedResolveAttackEvent` to no longer take a context.
- Refactor `AttackResolutionSystem.OnResolveAttack` to resolve the current planned attack from `AttackIntent.Planned[0]`.
- Read the current progress directly.
- Store `WasBlocked` on the current planned attack.
- Subscribe to context-free `EnemyDamageAppliedEvent`.
- While subscribed, guard by the captured active attack sequence so a later attack impact cannot trigger old callbacks.
- Publish `AttackResolved` without context when the captured sequence finishes.
- Unsubscribe handlers after the matching damage-applied event.

### 5.8 Update damage application

- Refactor `EnemyDamageManagerSystem.OnImpactNow` to use current progress.
- Keep pending damage accumulation behavior.
- Publish `ResolvingEnemyDamageEvent` and `EnemyDamageAppliedEvent` without planned attack context.
- Make subscribers that need attack matching compare captured active attack sequence, not context.
- Preserve current damage order:
  - base pending damage
  - resolving event for conditional extra effects
  - extra pending damage
  - assigned block
  - Aegis unless ignored
  - HP modification
  - damage applied event

### 5.9 Update visual effect impact bridge

- Refactor `VisualEffectRequestFactory.ForEnemyAttack` to no longer accept context.
- Refactor `VisualEffectRequested` and `ActiveVisualEffect` only if their context fields are exclusively for enemy attack gameplay. If other visual systems use the field, leave the property but pass `string.Empty` for enemy attacks and stop reading it in enemy attack impact logic.
- In `ModularEffectCoordinatorSystem.PublishImpact`, publish context-free `EnemyAttackImpactNow` for non-preview enemy attack effects.
- In rejected gameplay request handling, publish context-free `EnemyAttackImpactNow` for non-preview enemy attack requests.
- Keep `QueuedWaitVisualEffectImpact(request.RequestId)` unchanged.

### 5.10 Update enemy attack display and phase trigger

- Refactor `PhaseChangeEventSystem` to compare `AttackIntent.ActiveAttackSequence` instead of `_lastSeenContextId`.
- Refactor `TriggerEnemyAttackDisplayEvent` to carry no context.
- In `EnemyAttackDisplaySystem`, use active attack sequence for:
  - detecting a new attack
  - clearing pending confirm
  - resetting impact animation
  - replaying debug impact for the current attack
  - shockwave origin calculation
- Keep banner rendering based on the current planned attack.
- Keep `EnemyAttackBannerAnchor` behavior unchanged.

### 5.11 Update ambush and must-block systems

- In `AmbushState`, replace context tracking with active attack sequence tracking.
- In `AmbushDisplaySystem`, start, stop, and expire ambush state for the current attack sequence.
- Refactor `AmbushTimerExpired` to carry attack sequence or no payload. Use sequence if the timer can expire after a later attack becomes active.
- In `MustBeBlockedSystem`, track active attack sequence instead of context.
- Auto-assignment on ambush timer expiration must verify the captured sequence still matches the current attack before assigning cards.
- Must-block fulfillment reads current progress directly.

### 5.12 Update passive and special-effect systems

Update all enemy attack systems that currently resolve progress by context:

- `BrittleManagementSystem`
- `BleedManagementSystem`
- `ShackleManagementSystem`
- `PlayerHudHealthDisplaySystem`
- `HPDisplaySystem`
- `DamagePredictionService`
- `EnemyDamageMeterDisplaySystem`
- `CanPlayCardHighlightSystem`
- `DiscardSpecificCardHighlightSystem`
- `MarkedForSpecificDiscardSystem`
- `AppliedPassivesManagementSystem`

For each system:

- If it is handling the current enemy attack, read current attack and current progress from the helper.
- If it is handling generic card movement or non-attack behavior, preserve its existing non-attack context semantics.
- Do not create static shared snapshots for current attack state.
- Do not inject systems into other systems.

### 5.13 Update advance and reset behavior

- Refactor `QueuedAdvanceToNextPlannedAttackEvent` to remove `intent.Planned[0]`.
- When another planned attack remains:
  - increment `AttackIntent.ActiveAttackSequence`
  - clear pending confirm
  - clear assigned block state
  - clear active progress so the progress system recreates it for the new current attack
  - enqueue `PreBlock` then `Block`
- When no planned attack remains:
  - clear pending confirm
  - clear progress
  - enqueue `EnemyEnd`, `PlayerStart`, and `Action`
- In `EnemyPhaseResetService`, clear current and next attack lists and reset active attack sequence.
- In `BattleTransientStateCleanupService`, clear any attack-sequence transient state.

### 5.14 Remove obsolete helpers

- Remove `GetComponentHelper.GetContextId`.
- Replace `GetComponentHelper.GetPlannedAttack` with a current attack helper or update it to call `EnemyAttackFlowService`.
- Remove context-based find helpers in `EnemyAttackDisplaySystem`, `EnemyAttackProgressManagementSystem`, and `EnemyAttackConfirmAvailabilityService`.
- Remove logging fields that only print planned attack context IDs.

---

## 6. Required Tests

### 6.1 Unit tests

Update or add tests for:

- normal attack can confirm with zero blockers
- must-block-at-least and must-block-exactly requirements count active assigned blockers
- returning blockers do not satisfy confirm requirements
- animating blockers allow confirm request but delay confirm resolution
- the same active attack cannot be confirmed twice
- advancing removes the first planned attack
- advancing to another attack increments active attack sequence
- repeated same `AttackId` attacks resolve separately
- progress resets between two attacks in one enemy turn
- assigned block cards from the first attack do not affect the second attack
- enemy attack damage uses current progress at impact
- on-hit and threshold effects fire only for the matching active attack sequence
- rejected enemy attack VFX still publishes impact and resolves damage
- ambush timer expiration only auto-assigns blocks for the active attack sequence

### 6.2 Integration-style flow tests

Add a focused enemy phase flow test that creates a two-attack turn:

1. Plan two attacks.
2. Enter `Block`.
3. Assign block to attack one.
4. Confirm attack one.
5. Resolve impact.
6. Advance to attack two.
7. Assert attack two has fresh progress and no inherited assigned block.
8. Confirm attack two.
9. Resolve impact.
10. Assert the enemy turn advances to player action.

Add a repeated-attack test where both planned attacks have the same `AttackId`. The test must prove that the second attack is not treated as already confirmed or already resolved.

### 6.3 Snapshot and manual verification

After implementation and `dotnet build`, run at least one manual or snapshot fight flow that includes multiple enemy attacks in one turn:

```bash
dotnet run -- test-fight hammer ogre hard
```

Verify:

- first attack banner appears
- block assignment works
- confirm button appears and hides correctly
- damage resolves at visual impact
- second attack banner appears after the first attack
- second attack starts with no stale block/progress state
- player action phase resumes after the final attack

If `ogre hard` is not deterministic enough for multi-attack coverage, use a debug fixture or temporary local test setup during implementation and remove it before final handoff.

---

## 7. Known Test Environment Issue

During research, this command was attempted:

```bash
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj --filter "EnemyAttackConfirmAvailabilityServiceTests|EnemyAttackProgressOverrideServiceTests|EnemyDamageThresholdTests|FallenShepherdAttackTests"
```

The test project did not compile because of existing unrelated API drift in tests, including typed ID conversions, removed `EnemyDifficulty`, stale `EnemyDefeatFlowSystem` constructor parameters, and missing `PlayerAnimationState` references.

Implementation must still run `dotnet build` from the repo root. Enemy attack tests changed by this refactor should be made compile-correct as part of the change, but this plan does not require repairing every unrelated stale test file.

---

## 8. Acceptance Criteria

- No `PlannedAttack.ContextId` remains.
- Enemy attack resolution no longer searches planned attacks by string context.
- Current attack progress is scoped to the active attack only.
- Multiple planned attacks in one enemy turn still resolve in order.
- Repeated same `AttackId` attacks in one turn resolve as separate attacks.
- Assigned blocks never leak from one planned attack to the next.
- Enemy damage still applies at modular visual impact.
- Confirm gating still waits for block assignment animations.
- Ambush auto-confirm and must-block auto-assignment still target only the active attack.
- `dotnet build` succeeds from the repository root.

---

## 9. Implementation Order

1. Update data contracts and event contracts.
2. Add current attack lookup/progress helper behavior.
3. Refactor progress management to one active progress row.
4. Refactor block assignment and assigned block display.
5. Refactor confirm gating and enemy attack display.
6. Refactor queued attack events.
7. Refactor attack resolution and damage impact.
8. Refactor modular VFX enemy impact bridge.
9. Refactor ambush, must-block, passive, prediction, and HUD readers.
10. Refactor attack advance and reset cleanup.
11. Remove obsolete context helpers and context logs.
12. Update focused tests.
13. Run `dotnet build` and fix compile errors.

Do not split this change into a partial compatibility layer where both context-based and current-attack-based enemy attack resolution paths coexist. The final runtime should have one enemy attack identity model.
