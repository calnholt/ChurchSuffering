# Enemy Attack Resolution → `EnemyAttackResolver` Plan

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #1 — land **second**, after RFC #3 (static `EntityManager` removal).
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required. Start fresh with `dotnet run -- new` when validating.

This plan extracts "resolve one enemy attack" into a deep module `EnemyAttackResolver` — a plain class (no `GraphicsDevice`) owning the multi-step sequence behind one method. Presentation/timing is a Port (`IAttackPresentationGate`). Biggest testability win among the five Deep-Module refactors.

**Does NOT absorb (this pass):** damage math + lambda choreography in `AttackResolutionSystem`/`EnemyDamageManagerSystem`/`HpManagementSystem`; the static buses (candidate RFC #6).

---

## 1. Objective

Own the definition and sequencing of "resolve one enemy attack" (phase entry, ordered step list, presentation-gate seam) behind a small interface testable without `GraphicsDevice`/`SpriteBatch`.

**Chosen shape:** Imperative facade. Rejected: explicit state-machine object (duplicates `IQueuedEvent`); coroutine/yield (execution model absent from this synchronous static-bus codebase).

---

## 2. Problem

"Resolve one enemy attack" is a multi-frame saga across ~15 collaborators, ~10 event types, and **two static buses**. No object owns it; the sequencing lives inside a `GraphicsDevice`-bound renderer.

### 2.1 Trigger + pipeline in a graphics `DisplaySystem`

`EnemyAttackDisplaySystem.ExecuteConfirm` (`ECS/Scenes/BattleScene/EnemyAttackDisplaySystem.cs:401-437`) is the only assembly point: publishes `ChangeBattlePhaseEvent{EnemyAttack}` (`:406`) then enqueues:

1. `QueuedDiscardAssignedBlocksEvent` (`:407`)
2. `QueuedResolveAttackEvent` (`:408`)
3. `QueuedWaitAbsorbEvent` (`:409`)
4. N× `QueuedStartVisualEffect` (`:418-421`)
5. `QueuedWaitVisualEffectImpact` (`:422`)
6. `QueuedAdvanceToNextPlannedAttackEvent` (`:436`)

With an emergency `QueuedPublish` fallback (`:432-434`). Constructed only with `GraphicsDevice`/`SpriteBatch` (`BattleSceneSystem.cs:799`) → **orchestration cannot be instantiated headless**.

### 2.2 Shallow steps, deep hidden coupling

`QueuedResolveAttackEvent.cs:24` publishes `ResolveAttack`. `AttackResolutionSystem.OnResolveAttack` (`AttackResolutionSystem.cs:31`) evaluates the block condition, publishes `ApplyEffect{Damage}`, then **subscribes two anonymous runtime lambdas** (`:74-112`) to `ResolvingEnemyDamageEvent`/`EnemyDamageAppliedEvent`. Control flow lives in closures keyed by a captured `attackSequence` — invisible to a queue reader.

**Correction:** `HpManagementSystem` does **not** publish `EnemyDamageAppliedEvent`. `EnemyDamageAppliedEvent` originates in `EnemyDamageManagerSystem.OnImpactNow:112`, computed by bracketing the synchronous `ModifyHpRequestEvent` with an `hpBefore`/`hp.Current` diff. The "damage landed" signal that drives `AttackResolved` comes from the damage manager, not the HP system.

### 2.3 Port is broader than "visual impact"

Two other steps also block on presentation-only signals and would hang a headless run:

- `QueuedWaitAbsorbEvent` waits for `EnemyAbsorbComplete` (published only by absorb tween, `EnemyAttackDisplaySystem.cs:702`).
- Animated `QueuedDiscardAssignedBlocksEvent` (`:39-46`) publishes `DebugCommandEvent{"AnimateAssignedBlocksToDiscard"}` and stalls; headless equivalent `ResolveImmediately` (`:80-114`) exists only on the non-animate branch.

Gameplay-timing authority in production is `ModularEffectCoordinatorSystem.PublishImpact:167` (fires `EnemyAttackImpactNow` `:175` + `VisualEffectImpactReached` `:205`).

### 2.4 Integration risk

1. **The test lies** — `EnemyAttackFlowTests.ConfirmAndResolveCurrentAttack` (`:176-199`) reimplements the pipeline, skipping discard, absorb, and impact wait; never runs `ExecuteConfirm`. Same copy-paste in `EnemyDamageThresholdTests.cs:180`, `FallenShepherdAttackTests.cs:367`, `FrostEaterTests.cs:123`.
2. Order is load-bearing but implicit (`ResolveAttack` must precede impact so `_pendingDamage` accumulates before `EnemyAttackImpactNow` consumes it).
3. `EnemyPhaseFlowSystem.OnEnemyPhaseLethal:56` calls `EventQueue.Clear()` mid-saga.

---

## 3. Proposed Interface

```csharp
public interface IEnemyAttackResolver
{
    // Assembles and enqueues the full pipeline.
    // Completion is observable via existing AttackResolved / phase-change events
    // (and EventQueue.IsIdle) — no new completion event.
    void ResolveCurrentAttack(int attackSequence);
}

public interface IAttackPresentationGate // the one Port
{
    EventQueue.IQueuedEvent CreateDiscardStep(EntityManager em);   // prod: animate; test: immediate
    EventQueue.IQueuedEvent CreateAbsorbWait();                    // prod: real tween; test: no-op complete
    // MUST guarantee EnemyAttackImpactNow is published at impact.
    IReadOnlyList<EventQueue.IQueuedEvent> BuildImpactSteps(EntityManager em, Entity enemy, EnemyAttackBase attack);
}

public sealed class EnemyAttackResolver : IEnemyAttackResolver
{
    public EnemyAttackResolver(EntityManager em, IAttackPresentationGate gate);

    public void ResolveCurrentAttack(int attackSequence)
    {
        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyAttack, Previous = SubPhase.Block });
        EventQueue.EnqueueRule(_gate.CreateDiscardStep(_em));
        EventQueue.EnqueueRule(new QueuedResolveAttackEvent());
        EventQueue.EnqueueRule(_gate.CreateAbsorbWait());
        EnemyAttackFlowService.TryGetCurrentEnemyAttack(_em, out var enemy, out _, out var planned);
        foreach (var step in _gate.BuildImpactSteps(_em, enemy, planned?.AttackDefinition))
            EventQueue.EnqueueRule(step);
        EventQueue.EnqueueRule(new QueuedAdvanceToNextPlannedAttackEvent(_em));
    }
}
```

Caller (`ExecuteConfirm`) collapses to `_attackResolver.ResolveCurrentAttack(attackSequence);` after confirm-gating bookkeeping.

### 3.1 What the resolver hides

- Six-step ordering and load-bearing invariants.
- Emergency-impact fallback.
- Visual-request construction/selection (`VisualEffectRequestFactory.ForEnemyAttackSequence` + `SingleOrDefault(DrivesGameplayImpact)`, currently `:410-423`).
- Which steps are gameplay vs presentation-gated.
- That damage, on-hit hooks, and `AttackResolved` all fall out of publishing `EnemyAttackImpactNow` after `ResolveAttack`.

`AttackResolutionSystem` lambda dance is left intact — the resolver owns *sequencing*, not damage math.

---

## 4. Dependency Strategy

In-process except presentation/timing = Port `IAttackPresentationGate` (folds all three graphics-only stalls: discard, absorb, impact).

### 4.1 Production `GraphicsAttackPresentationGate`

Built in `BattleSceneSystem` beside the display system:

- `CreateDiscardStep` → `QueuedDiscardAssignedBlocksEvent(em)` (keeps animate branch)
- `CreateAbsorbWait` → `QueuedWaitAbsorbEvent()`
- `BuildImpactSteps` → exact logic at `EnemyAttackDisplaySystem.cs:410-435`

### 4.2 Test `ImmediateAttackPresentationGate`

- Discard → calls `QueuedDiscardAssignedBlocksEvent.ResolveImmediately(...)` synchronously
- Absorb → already-`Complete` no-op
- Impact → one step publishing `EnemyAttackImpactNow` on `StartResolving`

Because `EventManager.Publish` is synchronous, one pump reproduces production's causal chain:
`ResolveAttack` → `ApplyEffect` → `EnemyAttackImpactNow` → `ResolvingEnemyDamageEvent` → `ModifyHpRequestEvent` → HP → `EnemyDamageAppliedEvent` → `onApplied` → `AttackResolved`.

---

## 5. Testing Strategy

### 5.1 New `EnemyAttackResolverTests`

Bare `EntityManager`, `new` the real management systems, inject `ImmediateAttackPresentationGate`, call the **real** `ResolveCurrentAttack`, `PumpEventQueue()`, assert component state:

1. Unblocked → HP down by `def.Damage`, `AttackResolved` once.
2. Fully blocked → HP unchanged, `OnAttackHit` not invoked, `WasConditionMet` correct.
3. Threshold effect fires exactly when assigned block < `BlockRequiredToPreventEffect` and final damage > 0 (`AttackResolutionSystem.cs:95-102`).
4. Two-attack turn → after first, `intent.Planned` drops to 1, `ActiveAttackSequence==2`; after second, phase lands `PlayerTurn/Action` (migrate assertions from `EnemyAttackFlowTests.cs:58-79`).
5. Discard side effects (blocks cleared, `CardBlockedEvent`/`OnBlock` fired via immediate discard).
6. Ordering guard locking the resolve-before-impact invariant.

### 5.2 Delete/replace

- `EnemyAttackFlowTests.ConfirmAndResolveCurrentAttack` (`:176-199`) — route `[Fact]`s through the resolver.
- Copy-paste helpers: `EnemyDamageThresholdTests.ResolveAttack:180`, `FallenShepherdAttackTests.ResolveThresholdAttack:367`, `FrostEaterTests.cs:123-124` (share `ImmediateAttackPresentationGate`, or keep only where a test deliberately isolates the damage subsystem below the resolver).

### 5.3 Environment

None beyond current xUnit — no `GraphicsDevice`/`SpriteBatch`/`ImageAssetService`.

---

## 6. Implementation Steps

1. Add `IEnemyAttackResolver`, `IAttackPresentationGate`, `EnemyAttackResolver`, `GraphicsAttackPresentationGate`, `ImmediateAttackPresentationGate`.
2. Move `EnemyAttackDisplaySystem.ExecuteConfirm` logic (`:406-436`) into `EnemyAttackResolver.ResolveCurrentAttack`.
3. Implement production gate byte-for-byte equivalent to current display-system behavior.
4. Inject resolver into `EnemyAttackDisplaySystem`; shrink `ExecuteConfirm`.
5. Construct production gate + resolver in `BattleSceneSystem.cs:799`.
6. Add `EnemyAttackResolverTests`; migrate/delete reimplemented test helpers.
7. Manual smoke: full enemy attack turn — blocks resolve, damage lands on visual impact, multi-attack turns advance, mid-attack boss phase transition (`EnemyPhaseFlowSystem`) still clears cleanly.
8. Run `dotnet build` and `dotnet test tests/Crusaders30XX.Tests`.

**Effort:** medium. **Risk:** medium-low (move of `:406-436` into one class + three adapter methods; no gameplay math changes). Watch `EnemyPhaseFlowSystem:56` `EventQueue.Clear()` — the facade holds no cross-frame state.

`Queued*` classes + all four subscriber systems are reused unchanged.

---

## 7. Critical Files

- `ECS/Scenes/BattleScene/EnemyAttackDisplaySystem.cs` (ExecuteConfirm `:401-437`)
- `ECS/Scenes/BattleScene/AttackResolutionSystem.cs` (`:31`)
- `ECS/Scenes/BattleScene/EnemyDamageManagerSystem.cs` (OnImpactNow `:43`)
- `ECS/Scenes/BattleScene/QueuedDiscardAssignedBlocksEvent.cs` (ResolveImmediately `:80`)
- `tests/Crusaders30XX.Tests/EnemyAttackFlowTests.cs` (`:176-199`)

---

## 8. Verification Checklist

- [ ] `dotnet build` succeeds from repo root.
- [ ] New `EnemyAttackResolverTests` drive real `ResolveCurrentAttack` through `ImmediateAttackPresentationGate` and pass.
- [ ] Reimplemented `ConfirmAndResolveCurrentAttack` helpers deleted.
- [ ] `dotnet test tests/Crusaders30XX.Tests` green.
- [ ] In-app: enemy attack turn — blocks, damage on impact, multi-attack advance, lethal phase clear.
