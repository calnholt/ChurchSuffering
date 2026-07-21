# Medal/Equipment Presentation Gate Plan

> Smallest of the set. Behavior-preserving; a directly-copyable clone of the already-shipped `IAttackPresentationGate` applied to the two activation flows that never got the seam.

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `ChurchSuffering`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #06 — local-substitutable; effort/risk small / low.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

Extend the shipped `IAttackPresentationGate` pattern to medal and equipment activation for headless-testable gameplay.

---

## Context

**Why:** an `improve-codebase-architecture` deep-module exploration (2026-07-15) looking for small interfaces hiding large implementations. The enemy-attack path already solved exactly this problem — "gameplay resolution welded to visual-effect timing behind the static bus" — by extracting `EnemyAttackResolver` + the `IAttackPresentationGate` port (`ECS/Scenes/BattleScene/EnemyAttackResolver.cs:11-70`), with a production `GraphicsAttackPresentationGate` (`GraphicsAttackPresentationGate.cs:18`) and a test `ImmediateAttackPresentationGate ` (`tests/ChurchSuffering.Tests/ImmediateAttackPresentationGate.cs:10`). Two sibling flows — medal activation and equipment activation — have the identical welding and got **no** such seam.

**What prompted it:** medal/equipment "activate with visual" is implemented as monolithic queued events (`QueuedActivateMedalWithVisual.cs`, `QueuedActivateEquipmentWithVisual.cs`) that inline both sides of the static `EventManager` bus — they *do the gameplay* and then *publish + subscribe-and-wait for the visual* in one `StartResolving`. There is no way to activate a recipe-bearing medal/equipment in a test without standing up the full visual pipeline.

**Intended outcome:** extend the shipped `IAttackPresentationGate` pattern to medals and equipment. One presentation-gate port; a production gate that builds the real `QueuedStartVisualEffect` + `QueuedWaitVisualEffectComplete ` steps; an `Immediate*` test double that skips the visual wait so activation *gameplay* is testable headless. Gameplay ("activate the medal/equipment") becomes separable from presentation ("play + wait for the effect").

## Problem

"Activate one medal/equipment with its visual" fuses gameplay and presentation inside a single queued event, both sides hard-coded onto the static bus.
- **Gameplay and presentation are one method.** `QueuedActivateMedalWithVisual.StartResolving` (`ECS/Scenes/BattleScene/QueuedActivateMedalWithVisual.cs:29-62`) publishes `MedalTriggered` (:39) and calls `medal.Activate()` (:40) — the gameplay — then immediately `new`s a request via `VisualEffectRequestFactory.ForMedal` (:42-45), subscribes a `VisualEffectCompleted` handler keyed to the request GUID (`EventManager.Subscribe(_handler)`, :59), publishes the request (:60), and parks itself in `Waiting` (:61) until `OnCompleted` matches `evt.RequestId` and flips to `Complete` (:66-71). Equipment is byte-for-byte the same shape: `equipment.OnActivate(...)` + `MarkUsed()` (`QueuedActivateEquipmentWithVisual.cs:39-40`), `EquipmentAbilityTriggered` (:41), factory (:43-46), subscribe (:60) / publish (:61) / wait (:62) / GUID-matched `OnCompleted` (:67-72).
- **Both sides of the static bus are inlined.** The step is producer *and* consumer: it publishes `VisualEffectRequested` and subscribes `VisualEffectCompleted` on the process-global `EventManager`. There is no seam between "apply the effect" and "play the animation."
- **Progress depends on an unrelated system ticking.** The gating `VisualEffectCompleted` is produced *only* by `ModularEffectCoordinatorSystem.PublishCompletion` (`ECS/Scenes/BattleScene/ModularEffectCoordinatorSystem.cs:265-273`, publishing `VisualEffectCompleted` at :268) as it ticks `ActiveVisualEffect` entities (impact side at `PublishImpact:167`, publishing `VisualEffectImpactReached:205`). So "did the medal fire?" is inseparable from "did the coordinator construct, run, and finish the effect?" — the queued step never leaves `Waiting` without the whole pipeline live.
- **Zero tests on the visual path.** Grep of `tests/` for `QueuedActivateMedal`, `QueuedActivateEquipment` → **zero** files. `VisualEffectCompleted` appears in tests only in `EnemyPhaseFlowSystemTests.cs:1 22,125` (a different flow). `MedalManagerSystem` is never constructed in any test. `EquipmentManagerSystem` *is* constructed (`AbilityEquipmentTests.cs:67,102,1 18,132,148`; `EquipmentDisplaySystemTests.cs: 166,353,377`) — but every one of those tests publishes `EquipmentActivateEvent` and asserts **synchronously**, with a bare `EntityManager` (`AbilityEquipmentTests.cs:163-175`), no coordinator, and no `PumpEventQueue()`. That path is reachable *only* through the recipe==null synchronous branch (`EquipmentManagerSystem.cs:52-54`); the recipe branch that enqueues `QueuedActivateEquipmentWithVisual` (`:49`) is entirely untested, because it would park in `Waiting` forever without the coordinator.
- **Contrast — the attack path already has the seam.** `EnemyAttackResolver.ResolveCurrentAttack` enqueues gameplay steps and delegates every presentation-blocking step to the injected `IAttackPresentationGate` (`EnemyAttackResolver.cs:50-68`). Its production gate builds `QueuedStartVisualEffect` + `QueuedWaitVisualEffectImpact` (`GraphicsAttackPresentationGate.cs:47-48`); its immediate double publishes the completion synchronously (`ImmediateAttackPresentationGate.cs:38-40`). The medal/equipment flows are the same problem left unfixed.

## Proposed Interface

One port, one method. The port hides "play a visual request and block until it finishes"; the caller keeps the gameplay and the (headless-safe, source-specific) request construction.

```csharp
namespace ChurchSuffering.ECS.Systems {public interface IActivationPresentationGate {
//Prod:[QueuedStartVisualEffect(request), QueuedWaitVisualEffectComplete(request.RequestId)] //Test:empty—gameplay
already applied bythe gameplay-onlystep; nothing towaiton.IReadOnlyList BuildPresentationSteps(VisualEffectRequested
request); } //Gameplay-onlyqueuedstep:theoldQueuedActivateMedalWithVisual minus
request/subscribe/publish/wait.publicsealed class QueuedActivateMedal : EventQueue.IQueuedEvent {
//StartResolving():resolveEquippedMedal; if null -> Complete; //EventManager.Publish(newMedalTriggered{...});
medal.Activate(); State = Complete;} //QueuedActivateEquipment:equipment.OnActivate(em, e); equipment.MarkUsed();
//EventManager.Publish(newEquipmentAbilityTriggered{...}); State = Complete; } //Productiongate—mirrors
GraphicsAttackPresentationGate.BuildImpactSteps:47-48, //butwaits for Completed (medals/equipment) instead of
ImpactReached (attacks). public sealed class GraphicsActivationPresentationGate : IActivationPresentationGate {public
IReadOnlyList BuildPresentationSteps(VisualEffectRequested request) { if (request == null) return Array.Empty();
//preservesthe:46-55null-request log return new EventQueue.IQueuedEvent[] { new QueuedStartVisualEffect(request),
//publishesVisualEffectRequested(:25) new QueuedWaitVisualEffectComplete(request.RequestId)
//subscribesVisualEffectCompleted(:27,:32-37)}; } } //Testdouble—even simpler than ImmediateAttackPresentationGate: no
completion tofake, //becausethegameplay-onlystepalready applied the effect synchronously onpump.internal sealed class
ImmediateActivationPresentationGate : IActivationPresentationGate {public IReadOnlyList
BuildPresentationSteps(VisualEffectRequested request) => Array.Empty(); }
```

**Before/after — `MedalManagerSystem.cs:42-46`:**

```csharp
//BEFORE—onequeuedstep welds gameplay + build + publish + subscribe + wait if (medal.ActivationEffectRecipe != null)
{EventQueue.EnqueueTrigger(new QueuedActivateMedalWithVisual(EntityManager, e.MedalEntity)); //:44return; }
//AFTER—gameplay is onestep; presentation is the injected gate's steps if (medal.ActivationEffectRecipe != null)
{EventQueue.EnqueueTrigger(new QueuedActivateMedal(EntityManager, e.MedalEntity)); //MedalTriggered+medal.Activate()
var request = VisualEffectRequestFactory.ForMedal(EntityManager, e.MedalEntity, medal.ActivationEffectRecipe); foreach
(varstep in _presentationGate.BuildPresentationSteps(request)) EventQueue.EnqueueTrigger(step); return; }
```

`EquipmentManagerSystem.cs:47-51` migrates identically (`QueuedActivateEquipment` + `VisualEffectRequestFactory.ForEquipment`). Steps stay `EnqueueTrigger` (not `EnqueueRule`) to preserve today's queue semantics (`EventQueue.cs:36`). The gameplay step runs when the queue reaches it; the gate's steps play the visual immediately after. Request construction is headless-safe (pure entity queries + a POCO; `VisualEffectRequestFactory.ForMedal:88-99`, `ForEquipment:75-86`) so it stays with the caller regardless of gate. **What it hides:** the two-step play-then-wait shape; the GUID-matched `VisualEffectCompleted` subscribe/unsubscribe dance (`QueuedActivateMedalWithVisual.cs:59,66-71`); the fact that completion is gated on `ModularEffectCoordinatorSystem` ticking `ActiveVisualEffect` entities (`:265-273`); the null-request fallback + its `RequestCreationFailed` log (`QueuedActivateMedalWithVisual.cs:46-55`). Gameplay side (`MedalTriggered`/`EquipmentAbilityTriggered`, `Activate`/`OnActivate`+`MarkUsed`) is left exactly as-is in the gameplay-only step. **One gate vs two — decision: ONE gate.** The only differences between the medal and equipment flows are gameplay (`medal.Activate()` vs `equipment.OnActivate()`+`MarkUsed()`) and which factory builds the request (`ForMedal` vs `ForEquipment`) — both of which live on the caller side. The presentation-pipeline concern that actually varies between prod and test — "publish the request and block on `VisualEffectCompleted`, or skip it" — is *identical* for both. One port, one method, one immediate double. **Rejected alternatives.** (1) *Gate owns request construction via two methods* (`BuildMedalPresentation(em, medalEntity, recipe)` / `BuildEquipmentPresentation(...)`, mirroring the attack gate's internal `VisualEffectRequestFactory` call at `GraphicsAttackPresentationGate.cs:38`) — doubles the surface and duplicates the null-guard for zero payoff, since the factory call is a source-specific one-liner and building a `VisualEffectRequested` POCO hangs nothing headless (it is not the load-bearing presentation dependency). (2) *Two separate ports* (`IMedalPresentationGate` + `IEquipmentPresentationGate`) — byte-identical bodies; splits one seam into two for no behavioral reason.

## Dependency Strategy

Local-substitutable in-process presentation subsystem — the exact category the attack gate already proves substitutable. No new assembly/package/network boundary.
- **Production `GraphicsActivationPresentationGate`** (new file beside `GraphicsAttackPresentationGate.cs` in `ECS/Scenes/BattleScene/`): builds `QueuedStartVisualEffect` (`QueuedStartVisualEffect.cs:25` publishes the request) + `QueuedWaitVisualEffectComplete ` (`QueuedWaitVisualEffectComplete.cs:27,32-37` subscribes and GUID-matches `VisualEffectCompleted`). Byte-for-byte the same wiring the current `QueuedActivate*WithVisual` does inline — only the ownership moves.
- **Test `ImmediateActivationPresentationGate`** (new file beside `tests/ChurchSuffering.Tests/ImmediateAttackPresentationGate.cs`): returns no presentation steps. Because `medal.Activate()` / `equipment.OnActivate()` now live in a standalone `QueuedActivate*` step and `EventManager.Publish` is synchronous, one `PumpEventQueue()` (`EnemyAttackFlowTests.cs:195-201`) applies the gameplay with **no** coordinator and **no** `VisualEffectCompleted` needed. This is simpler than the attack immediate double, which must publish `EnemyAttackImpactNow` to drive downstream damage (`ImmediateAttackPresentationGate.cs:38-40`); here the gameplay is fully upstream of the gate.
- **Construction/injection point:** `BattleSceneSystem` — the same file/method that already builds the attack resolver + `new GraphicsAttackPresentationGate()` (`BattleSceneSystem.cs:799-801`) and constructs the two managers (`_equipmentManagerSystem`/`_medalManagerSystem` at `:845-846`). Build one `GraphicsActivationPresentationGate` and pass it to both manager ctors. A gate is a plain injected port, not a `System`, so this respects the AGENTS.md rule against passing a `System` as a ctor param.

## Testing Strategy

New `MedalActivationTests` / `EquipmentActivationTests` (bare `EntityManager`, `new` the real manager with `ImmediateActivationPresentationGate`, publish the activate event, `PumpEventQueue()`, assert gameplay state — the canonical headless pattern):
- Activate a **recipe-bearing** medal (`ActivationEffectRecipe != null`, the previously-untestable branch): assert `medal.Activate()`'s gameplay effect applied and `MedalTriggered` fired — **no** `ModularEffectCoordinatorSystem`, **no** `VisualEffectRequested`/`VisualEffectCompleted` plumbing.
- Activate a recipe-bearing equipment: assert `OnActivate` effect applied, `IsUsed` true (`EquippedEquipment.Equipment.IsUsed`, cf. `AbilityEquipmentTests.cs:72`), `EquipmentAbilityTriggered` fired.
- Guard the eligibility gates that stay in the manager (`EquipmentManagerSystem.cs:29, 37-46`): frozen input / unavailable / `!CanActivate()` → no activation, no steps enqueued.
- Optional ordering guard: with the immediate gate the queue drains to idle (`EventQueue.IsIdle`, `EventQueue.cs:27`) after one pump with no coordinator — proving the gate is the only thing that was blocking headless runs. This removes another reason tests need the full visual pipeline: the recipe==null equipments (`kunai_sheath`, `sanctified_circlet`, `bulwark_plate`, `sunderstep_treads`, `oathbreaker_coif`) are testable today only because they dodge the visual branch; the immediate gate makes recipe-bearing medals/equipment first-class headless-testable for the first time. Keep the existing recipe==null tests unchanged — they still exercise the synchronous branch (`EquipmentManagerSystem.cs:52-54`) and prove behavior preservation.

## Implementation Recommendations

- **Owns:** the `BuildPresentationSteps` port; the production gate's play+wait step list; the immediate double.
- **Hides:** the play-then-wait pair, the `VisualEffectCompleted` GUID subscribe/unsubscribe, the coordinator-tick dependency, the null-request log/fallback.
- **Exposes:** `BuildPresentationSteps(VisualEffectRequested)` + two slim gameplay-only queued steps (`QueuedActivateMedal`, `QueuedActivateEquipment`).
- **Migration (behavior-preserving):** (1) add `IActivationPresentationGate` + `GraphicsActivationPresentationGate` beside the attack files; (2) split `QueuedActivateMedalWithVisual`/ `QueuedActivateEquipmentWithVisual` into gameplay-only `QueuedActivateMedal`/`QueuedActivateEquipment` (delete the request/subscribe/publish/wait tail at `:42-71` / `:43-72`) and move the `VisualEffectRequestFactory` call + `BuildPresentationSteps` loop into the managers' recipe branches; keep the production gate byte-equivalent to the deleted inline code (including the `RequestCreationFailed` log on null request); (3) inject one `GraphicsActivationPresentationGate` into `MedalManagerSystem`/`EquipmentManagerSystem` ctors at `BattleSceneSystem.cs:845-846`; (4) add `ImmediateActivationPresentationGate` + the new tests. The `Queued*` primitives (`QueuedStartVisualEffect`, `QueuedWaitVisualEffectComplete `) and the `ModularEffectCoordinatorSystem` are reused untouched.
- **Effort:** small — one port + one production gate + one test double + two slimmed queued steps + two-line ctor injection in two managers.

**Risk:** low — behavior-preserving, and the precedent (`IAttackPresentationGate`) already shipped and is directly copyable.

## Critical files

- `ECS/Scenes/BattleScene/QueuedActivateMedalWithVisual.cs` (`:29-71` — split into gameplay `QueuedActivateMedal` + gate steps)
- `ECS/Scenes/BattleScene/QueuedActivateEquipmentWithVisual.cs` (`:29-72` — same split)
- `ECS/Scenes/BattleScene/MedalManagerSystem.cs` (`:44` enqueue site; inject gate)
- `ECS/Scenes/BattleScene/EquipmentManagerSystem.cs` (`:49` enqueue site; inject gate)
- `ECS/Scenes/BattleScene/EnemyAttackResolver.cs` + `GraphicsAttackPresentationGate.cs` + `tests/ChurchSuffering.Tests/ImmediateAttackPresentationGate.cs` (the template to copy)
- `ECS/Scenes/BattleScene/QueuedStartVisualEffect.cs`, `QueuedWaitVisualEffectComplete.cs` (reused primitives)
- `ECS/Services/VisualEffectRequestFactory.cs` (`ForMedal:88`, `ForEquipment:75`)
- `ECS/Scenes/BattleScene/BattleSceneSystem.cs` (`:799-801` gate precedent, `:845-846` manager construction/injection)

## Verification

- `dotnet build` from repo root clean.
- `dotnet test tests/ChurchSuffering.Tests` green; existing recipe==null equipment tests (`AbilityEquipmentTests`, `EquipmentDisplaySystemTests`) pass unchanged.
- New `MedalActivationTests`/`EquipmentActivationTests` drive the **real** managers through `ImmediateActivationPresentationGate`, `PumpEventQueue()`, and assert the gameplay effect applied synchronously — with no `ModularEffectCoordinatorSystem` and no `VisualEffectCompleted` plumbing (proving activation is now separable from presentation).
- In-app (`dotnet run`): activate a recipe-bearing medal and a recipe-bearing equipment during a battle — confirm the visual effect still plays and the gameplay effect still lands (i.e. the production gate is behavior-equivalent to the deleted inline path).

---

