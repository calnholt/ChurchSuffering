# Scene Navigator Plan

> A synchronous scene-navigator facade that owns scene-identity mutation, entity lifetime, and run teardown — pulling the deepest game state out of a `Draw()` "Display" system and leaving only the wipe animation behind a presentation seam.

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #03 — in-process; effort/risk medium / medium.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

Extract scene transitions from `TransitionDisplaySystem` into a synchronous `SceneNavigator` facade behind `ISceneTransitionGate`.

---

## Context

**Why:** An `improve-codebase-architecture` deep-module exploration (2026-07-15) surfaced that "change the scene" — the single most consequential state transition in the game — has **no owning module**. The actual scene switch (identity mutation + entity destruction + run teardown) is buried inside `TransitionDisplaySystem`, a `[DebugTab("Transition")]` renderer whose stated job is drawing a diagonal wipe. There is no `SceneManager`, no `SceneRouter`, no `ISceneNavigator` — the absence of that type is the signal. To answer "how does a scene change happen?" you must read a graphics system's private animation state machine, follow a `PreparationId` handshake into a second system, and reconstruct an implicit 7-event order that is split across both.

**What prompted it:** this is RFC #3 of the deep-module set (see `docs/plans/deep-module-refactor-round-2-plan.md` for the shared context, cross-cutting principles, and the `IAttackPresentationGate` precedent established by RFC #1, now shipped). Scene transition is the last god-flow hiding gameplay inside presentation.

**Intended outcome:** one deep module — `SceneNavigator` — that owns `SceneState.Current`, the `OwnedByScene`/`DontDestroyOnLoad` destruction policy, and `RunLifecycleService.EndCurrentRun` triggering, exposed behind a single synchronous entry point `NavigateTo(scene, options)`. The wipe/hold animation stays in `TransitionDisplaySystem` behind a small presentation seam (`ISceneTransitionGate`, mirroring `IAttackPresentationGate`: the navigator drives gameplay, an injected gate plays the wipe). The 4-bool `ShowTransition` event and the 7 lifecycle events collapse into that one entry point; the mislocated global scene event moves out of `CombatEvents.cs`. The common case becomes trivial: case becomes trivial: `navigator.NavigateTo(SceneId.Climb)`.

## Problem

The deepest game state in the codebase is disguised as rendering.
- **The scene switcher is a Display system.** `TransitionDisplaySystem` (`ECS/Scenes/BattleScene/TransitionDisplaySystem.cs`) is declared `[DebugTab("Transition")]` (`:14`), constructed only with `GraphicsDevice`/`SpriteBatch` (`:47`), added as an update system (`Game1.cs:300`) and drawn explicitly (`Game1.cs:666`). Its `Update` (`:70-135`) is an animation state machine (`Phase {Idle, WipeIn, Hold, WipeOut }`, `:23`) — but wired into that timeline is the entire gameplay transition.
- **Scene identity is mutated inside the renderer.** `DeleteEntities` (`:278-301`) reads `SceneState.Current` as `previous` (`:282`), then **writes `scene.Current = nextScene`** (`:284`) — the authoritative scene-identity mutation lives in a private method of a Draw system.
- **Entity lifetime is decided inside the renderer.** The same method (`:288-294`) calls `EntityManager.DestroyEntities(...)` for every entity where `OwnedByScene.Scene == previous` (`ECS/Components/Scenes.cs:176-180`), guarded by `!isReload` (`:286`, `previous == nextScene`) and `!e.HasComponent()` (`Scenes.cs:185-189`). The commented-out `DontDestroyOnReload` line (`:293`) shows the policy is still fluid — and it is being edited in a `Draw` system.
- **Run teardown is triggered inside the renderer.** `LoadTargetScene` (`:247-276`) calls `RunLifecycleService.EndCurrentRun(EntityManager)` (`:251`, `ECS/Services/RunLifecycleService.cs:12`) when `_endRunOnLoad` is set — persisting meta progress, destroying the run deck/player and `QueuedEvents`, and marking the save inactive (`RunLifecycleService.cs:14-29`). The most destructive gameplay operation in the game is a side effect of an animation phase advancing.
- **Implicit, load-bearing event order split across two systems.** The lifecycle contract lives in `ECS/Events/SceneEvents.cs` — `SceneTransitionRequested` (`:23`), `PrepareSceneEvent` (`:30`), `ScenePreparationReady` (`:36`), `SceneDeactivating` (`:42`), `SceneActivating` (`:48`), `SceneActivated` (`:55`), `LoadSceneEvent` (`:18`). The order is nowhere declared; it is emergent from two files: 1. `SceneTransitionRequested{PreparationId, From, To}` — published in `BeginWipeIn` (`:222-228`) to kick asset prep.
2. `SceneLoadingCoordinatorSystem` (`ECS/Scenes/Global/SceneLoadingCoordinatorSystem.cs`) hears it (`:49`, priority 100), queues jobs, publishes `PrepareSceneEvent` as a job (`:125`), drains on a 6 ms frame budget (`:60-104`), then publishes `ScenePreparationReady` (`:99-103`).
3. Back in the display system, `Update` blocks the swap until `IsPreparationReady()` (`:84`, `:112`) matches `PreparationId` + `TargetScene` + `Ready` (`:309-316`).
4. `LoadTargetScene` then fires, in this exact order: `SceneDeactivating` (`:259`) -> `DeleteCachesEvent` (`:260`) -> `DeleteEntities` (**`SceneState.Current` mutates here**, `:262`/`:284`) -> `SceneActivating` (`:265`) -> `LoadSceneEvent` (`:271`) -> `SceneActivated` (`:272`) -> optional `TransitionCompleteEvent` (`:275`).
- **The invariant nobody wrote down:** `SceneState.Current` flips to the target *between* `SceneDeactivating` and `LoadSceneEvent`, so the ~30 systems that subscribe to `LoadSceneEvent` (e.g. `BattleSceneSystem.cs:208`, `ClimbSceneSystem.cs:44`, `AchievementSceneSystem.cs:51`, `AppliedPassivesManagementSystem.cs:30` with `priority: 1`) see the *new* current scene, while destruction targets the *old* one. Reorder these and scenes leak or self-destruct. `SceneActivated` closes the `PreparationId` handshake (`SceneLoadingCoordinatorSystem.cs:156-159`).
- **The global scene event is mislocated in combat events.** `ShowTransition { SceneId Scene; bool SkipHold; bool SkipWipe; bool EndRunOnLoad; }` lives at `ECS/Events/CombatEvents.cs:79-85` — a comment even calls it "Fired when a battle is won" (`:78`) — yet it is the universal scene-change trigger, published from title, waystation, climb, achievement, and debug flows that have nothing to do with combat. `TransitionCompleteEvent` (`CombatEvents.cs:87`) is in the same family and equally misplaced.
- **The 4-bool + 7-event surface is the API.** Every caller hand-assembles a `ShowTransition` and hopes the two systems interpret the four booleans consistently. `SkipWipe` (`:230-239`) bypasses the animation and calls `LoadTargetScene` inline; `SkipHold` (`:82-101`) skips the black-hold; `EndRunOnLoad` (`:249`) gates teardown; `Scene == SceneId.None` (`:213`) is a debug-only visual-preview mode (`_suppressLoadScene`). Four booleans, three of them gameplay-affecting, interpreted across two systems.
- **~13 publisher sites, no facade.** Verified `new ShowTransition` producers: `Game1.cs:371` (test-fight, `SkipWipe`), `GuidedTutorialService.cs:75/167/190` (`Battle`, `SkipHold`) + `:201` (`WayStation`, `EndRunOnLoad`), `ClimbEncounterService.cs:67/158`, `RewardModalDisplaySystem.cs:94 5/959/986`, `WayStationPoiDisplaySystem.cs:13 1` (`Achievement`, `SkipHold`), `TitleMenuResumeService.cs:30/34 `, `WayStationRunSetupService.cs:24 `, `ClimbCardUpgradeDisplaySystem.cs:230`, `AchievementBackButtonDisplaySystem.cs:73`, `DebugCommandSystem.cs:190`, `GameOverOverlayDisplaySystem.cs:142` (`WayStation`, `SkipWipe`), `TestFightFlowSystem.cs:34`. Note `EnemyDefeatFlowSystem.cs:160/205/253` *stores* a `ShowTransition` in `PostVictoryAction.Transition` and publishes it later — the event is also passed around as data. **Navigability failure:** "how does a scene change happen?" has no single answer today. The honest answer is: publish a combat event, whose handler in a battle-scene Draw system runs a 4-phase animation, which on a later frame calls a private method that mutates scene identity and destroys entities and maybe ends the run, having coordinated asset readiness with a second system via a GUID handshake. That is exactly the shape a deep module is meant to collapse.

## Proposed Interface

One synchronous facade owns the gameplay swap; the wipe is a Port.

**Shape chosen: imperative facade + presentation gate**, identical in spirit to the shipped `EnemyAttackResolver`/`IAttackPresentationGate` pair (`EnemyAttackResolver.cs:16-70`).

```csharp
//ECS/Events/SceneEvents.cs—relocatedglobal scene event (was CombatEvents.cs:79) public sealed class
NavigateToSceneEvent {public SceneId To { get; init; } public SceneTransitionOptions Options {get; init; } = default;
} //ECS/Scenes/Global/SceneNavigator.cs public readonly record struct SceneTransitionOptions( bool SkipWipe = false,
//wasShowTransition.SkipWipe—immediate swap, no animation bool SkipHold = false, //wasShowTransition.SkipHold—no
black-holdbeat bool EndRunOnLoad = false); //wasShowTransition.EndRunOnLoad—tearthe rundown onswap public interface
ISceneNavigator { //Theoneentrypoint.Trivial commoncase: NavigateTo(SceneId.Climb).//Synchronouswithrespect to
GAMEPLAY: whenthe gate reports "covered", //theentireswap(identitymutation + destruction + optional run teardown +
//orderedlifecycleevents) runs atomically in onecall.Thewipe animation //thatwrapsitmayspanframes — that timing lives
entirely behindthe gate.void NavigateTo(SceneId scene, SceneTransitionOptions options = default); } //TheonePort—
mirrors IAttackPresentationGate.Presentation only.public interface ISceneTransitionGate { //Playwipe-in(+optional
hold), then invoke `onCovered` EXACTLY ONCEthe //instantthescreen is fully covered AND assetprep for `to` is Ready
//(gatepollsScenePreparationState).Thenplaywipe-outand publish //TransitionCompleteEvent{to}.SkipWipe=>invokeonCovered
synchronously, //noanimation.Scene==None => debug visual-only preview: noonCovered.void PlayTransition(SceneIdfrom,
SceneIdto, SceneTransitionOptions options, ActiononCovered); } public sealed class SceneNavigator : ISceneNavigator {
private readonlyEntityManager_entityManager; private readonlyISceneTransitionGate_gate; public
SceneNavigator(EntityManager entityManager, ISceneTransitionGategate) {_entityManager=entityManager??throw new
ArgumentNullException(nameof(entityManager)); _gate=gate??throw new ArgumentNullException(nameof(gate)); } public void
NavigateTo(SceneId scene, SceneTransitionOptions options = default) { var
sceneState=_entityManager.GetEntitiesWithComponent().FirstOrDefault()?.GetComponent(); var from = sceneState?.Current
?? SceneId.None; var preparationId = Guid.NewGuid(); //KickassetprepBEFOREthewipeso thegate has something
towaiton.EventManager.Publish(new SceneTransitionRequested {PreparationId = preparationId, From = from, To = scene });
_gate.PlayTransition(from, scene, options, onCovered: () => Swap(sceneState, from, scene, preparationId, options)); }
//Everythingbelow is today's LoadTargetScene (:247-276) + DeleteEntities (:278-301), //moved out ofthe renderer.Runs
in ONE synchronous callatthe covered point.private void Swap(SceneState sceneState, SceneIdfrom, SceneIdto, Guid
preparationId, SceneTransitionOptions options) { if (options.EndRunOnLoad)
RunLifecycleService.EndCurrentRun(_entityManager); EventManager.Publish(new SceneDeactivating { From = from, To = to
}); EventManager.Publish(new DeleteCachesEvent { Scene = to }); bool isReload = from == to; sceneState.Current = to;
//authoritativeidentitymutation_entityManager.DestroyEntities(e=>e.HasComponent() && e.GetComponent().Scene == from &&
!isReload &&!e.HasComponent()); //preservedacrossloadEventManager.Publish(newSceneActivating { PreparationId =
preparationId, From = from, To = to}); EventManager.Publish(new LoadSceneEvent { Scene = to, PreviousScene = from });
EventManager.Publish(new SceneActivated { PreparationId = preparationId, Scene = to }); } }
```

**Usage** (the common case is trivial):

```csharp
navigator.NavigateTo(SceneId.Climb); //just switch navigator.NavigateTo(SceneId.WayStation, new(EndRunOnLoad: true));
// switch + tearthe rundown navigator.NavigateTo(SceneId.Battle, new(SkipHold: true)); //tutorial, noholdbeat
```

And production glue replaces the scattered `EventManager.Publish(new ShowTransition{...})` with `EventManager.Publish(new NavigateToSceneEvent{ To = ..., Options = ...})`, subscribed once and forwarded to `navigator.NavigateTo`. **What it hides:** the load-bearing event order and its unwritten invariant (`SceneState.Current` mutates *between* `SceneDeactivating` and `LoadSceneEvent`); the reload no-op (`isReload`); the `PreparationId` handshake generation + threading; the `DontDestroyOnLoad` preservation rule; the `EndRunOnLoad` teardown gate; the `SkipWipe` fast path; and the prep-readiness poll (folded behind the gate). Callers see one verb and an options struct. **Rejected alternatives:**
- **Full retained `SceneManager` owning update + draw + a scene-object stack.** The repo has no scene-object model — scenes are just entity sets plus ~30 systems that self-subscribe to `LoadSceneEvent`, ordered by `SystemManager` phase buckets, with draw order hand-maintained in `Game1.DrawScene`/`BattleSceneSystem.Draw`. A retained manager would duplicate both and force a rewrite of every `LoadSceneEvent` subscriber. Over-engineered; rejected.
- **Thin navigator that only publishes the existing 7 events in the right order.** Leaves `SceneState.Current` mutation, entity destruction, and run teardown inside `TransitionDisplaySystem`'s `Draw` path — i.e. it fixes navigability but not the testability/ownership problem, which is the whole point. Rejected: the navigator must *own* the swap, not merely sequence events around it.

## Dependency Strategy

**In-process.** The wipe is presentation; scene-state mutation, entity destruction, and run teardown are pure gameplay — the split is the entire point, and it is drawn exactly where RFC #1 drew it.
- **Precedent — `IAttackPresentationGate` (shipped).** `EnemyAttackResolver` is a plain (non-`System`) class holding `IAttackPresentationGate` (`EnemyAttackResolver.cs:31-40`), constructed in `BattleSceneSystem.cs:799-801` with the production `GraphicsAttackPresentationGate` and injected into `EnemyAttackDisplaySystem` (`:802-807`). Its test double `ImmediateAttackPresentationGate ` (`tests/Crusaders30XX.Tests/ImmediateAttackPresentationGate.cs:10-43`) resolves every presentation step synchronously so the resolver runs headless. `SceneNavigator`/`ISceneTransitionGate` copies this shape one-for-one.
- **Production gate = the existing display system.** `TransitionDisplaySystem` implements `ISceneTransitionGate`: it keeps its animation state machine (`Update`, `:70-135`), `Draw` (`:137-209`), all `[DebugEditable]` tuning, the `StateSingleton.IsActive` transition flag (`:318-321`, `ECS/Singletons/StateSingleton.cs:7`), and the prep-readiness poll (`IsPreparationReady`, `:309-316`). It loses `LoadTargetScene`/`DeleteEntities ` (those move into `SceneNavigator.Swap`). On reaching "covered AND ready" it invokes the injected `onCovered`; on wipe-out end it publishes `TransitionCompleteEvent` (`:130`). Because `SceneNavigator` is **not** a `System`, holding the display system behind the `ISceneTransitionGate` interface does not violate "never pass another `System` as a constructor parameter to a system" (`docs/coding-standards.md:37`) — same as the resolver/gate relationship today.
- **Test gate = `ImmediateSceneTransitionGate`.** Invokes `onCovered` synchronously and returns; no animation, no readiness wait, no `GraphicsDevice`. One `EventManager.Publish` chain reproduces the full production causal sequence in a single call — exactly how `ImmediateAttackPresentationGate ` works.
- **Construction/wiring (`Game1`).** Today `_sceneLoadingCoordinatorSystem ` is built at `Game1.cs:218` and `_transitionDisplaySystem` at `Game1.cs:221`. After: build the display system (the gate) first, then `var navigator = new SceneNavigator(_world.EntityManager, _transitionDisplaySystem);`, then subscribe the relocated `NavigateToSceneEvent` once and forward to `navigator.NavigateTo`. `SceneLoadingCoordinatorSystem` is unchanged — it still listens for `SceneTransitionRequested`/`SceneActivated` and owns `ScenePreparationState`.

## Testing Strategy

The payoff: the swap becomes testable **without a `GraphicsDevice`**. Today it is unreachable headless because it lives inside a `SpriteBatch`-bound system. **New `SceneNavigatorTests`** — bare `EntityManager`, seed a `SceneState` + a mix of `OwnedByScene`/`DontDestroyOnLoad` entities, inject `ImmediateSceneTransitionGate`, call the **real** `NavigateTo`, assert observable state (canonical pattern: `EventManager.Clear()`/`EventQueue.Clear()` in ctor + `Dispose`, mirroring `EnemyAttackResolverTests.cs:15-27`):
- **Destroys the previous scene's entities.** `Current = Battle`; entities `OwnedByScene(Battle)` and `OwnedByScene(Climb)`; `NavigateTo(Climb)` -> `Battle`-owned entities destroyed, `Climb`-owned entities survive.
- **Preserves `DontDestroyOnLoad`.** A `Battle`-owned entity that also has `DontDestroyOnLoad` survives the switch away from `Battle`.
- **Reload is a no-op destroy.** `NavigateTo(Battle)` while `Current == Battle` destroys nothing (`isReload`).
- **Identity mutates to target.** After any `NavigateTo(X)`, `SceneState.Current == X`.
- **Ends the run iff `EndRunOnLoad`.** With `EndRunOnLoad: true`, the run-scoped entities `RunLifecycleService` destroys are gone (e.g. `QueuedEvents` entity, run deck/player — observable on `EntityManager`); with `false` they remain. (Guard the `AchievementManager.SaveProgress`/`SaveCache` static writes — assert on entity destruction, not disk.)
- **Lifecycle events fire in order.** Subscribe to all seven, record arrival order, assert: `SceneTransitionRequested` -> `SceneDeactivating` -> `DeleteCachesEvent` -> `SceneActivating` -> `LoadSceneEvent` -> `SceneActivated`, and that `SceneState.Current` already equals the target when `LoadSceneEvent` is observed (the load-bearing invariant).
- **`SkipWipe` and default produce identical gameplay outcomes** through the immediate gate (proves the fast path and the animated path swap the same way). **Old tests replaced:** none exist to delete — the swap is currently untestable headless so there are no unit tests headless, so there are no unit tests over `TransitionDisplaySystem`'s gameplay to remove (verified: no test file references `TransitionDisplaySystem`, `SceneState.Current` mutation, or `ShowTransition` gameplay; scene-transition coverage today is manual/debug-action only, e.g. `TransitionDisplaySystem.cs:323-350`). Callers that publish transitions in integration tests (e.g. `TestFightFlowSystemTests`, `EnemyDefeatFlowSystemTests`, `WayStationRunSetupTests`) switch from asserting on `ShowTransition` to `NavigateToSceneEvent`; their behavioral assertions are unchanged. **Environment:** no new dependencies — existing xUnit, static-bus reset, and the already-set `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. No `GraphicsDevice`/`SpriteBatch`/`ContentManager` — that is the win.

## Implementation Recommendations

**Owns:** `SceneState.Current` mutation; the `OwnedByScene`-vs-`DontDestroyOnLoad` destruction policy (incl. the reload no-op); `RunLifecycleService.EndCurrentRun` triggering; the `PreparationId` handshake generation; and the load-bearing lifecycle-event ordering + its `SceneState`-mutates-mid-sequence invariant.

**Hides:** all of the above plus the `SkipWipe` fast path and the prep-readiness gate (delegated to the seam).

**Exposes:** `NavigateTo(scene, options)`, the `ISceneTransitionGate` Port, and the relocated `NavigateToSceneEvent`. **Does NOT absorb (stays put):** the wipe/hold animation, `Draw`, `[DebugEditable]` tuning, `StateSingleton.IsActive`, and the prep-readiness poll all remain in `TransitionDisplaySystem` (now the gate); `SceneLoadingCoordinatorSystem` and its `ScenePreparationState`/`PrepareSceneEvent`/`ScenePreparationReady` half are untouched; every `LoadSceneEvent` subscriber is untouched. **Migration (sequence carefully — this touches scene identity and entity lifetime):**
1. **Introduce the navigator delegating to current behavior.** Add `SceneNavigator`/`ISceneNavigator`, `ISceneTransitionGate`, `ImmediateSceneTransitionGate`, `SceneTransitionOptions`, and the relocated `NavigateToSceneEvent`. Copy `LoadTargetScene`/`DeleteEntities ` logic verbatim into `SceneNavigator.Swap`. Wire it behind a single `NavigateToSceneEvent` subscription while `TransitionDisplaySystem` still handles `ShowTransition` — no behavior change, both paths coexist for one commit.
2. **Cut publishers over.** Mechanical rename across the ~13 producer files (see Problem) `new ShowTransition { Scene = X, SkipHold = a, SkipWipe = b, EndRunOnLoad = c }` -> `new NavigateToSceneEvent { To = X, Options = new(SkipWipe: b, SkipHold: a, EndRunOnLoad: c) }`. Retype `PostVictoryAction.Transition` in `EnemyDefeatFlowSystem.cs` (used at `:160/:205/:253`) to carry `NavigateToSceneEvent`.
3. **Absorb the gameplay.** Move `LoadTargetScene`/`DeleteEntities `/`EndCurrentRun`/lifecycle publishing out of `TransitionDisplaySystem` into `SceneNavigator.Swap`; make `TransitionDisplaySystem` implement `ISceneTransitionGate`, invoking the injected `onCovered` at the covered+ready point (replacing the inline `LoadTargetScene` calls at `:94/:116/:233`). Leave the wipe animation, `Draw`, prep-readiness poll, `StateSingleton` flag, and debug preview (`Scene == None`) in place.
4. **Delete `ShowTransition`** from `CombatEvents.cs:79-85`; relocate `TransitionCompleteEvent` (`CombatEvents.cs:87-90`) to `SceneEvents.cs` alongside `NavigateToSceneEvent` (same family — the gate publishes it on wipe-out end). **Relocate the global event:** `ShowTransition` -> `NavigateToSceneEvent` in `ECS/Events/SceneEvents.cs` (out of `CombatEvents.cs`). This alone fixes the "global scene trigger lives in combat events" smell.

**Effort:** medium.

**Risk:** medium — it moves scene-identity mutation and entity destruction, so a reorder or a dropped `DontDestroyOnLoad` check leaks or nukes entities. Mitigation: step 1 is behavior-preserving (copy, don't change); land the `SceneNavigatorTests` before step 3 so the absorb is fenced by green boundary tests; keep the gate's wipe timing byte-identical.

## Critical files

- `ECS/Scenes/BattleScene/TransitionDisplaySystem.cs` — the current scene switcher: `Update` state machine (`:70-135`), `LoadTargetScene` (`:247-276`), `DeleteEntities` (`:278-301`, mutates `SceneState.Current` `:284` + destroys `OwnedByScene` `:288-294`), `EndCurrentRun` trigger (`:251`), `BeginWipeIn` (`:211-245`). Becomes the presentation gate.
- `ECS/Scenes/Global/SceneLoadingCoordinatorSystem.cs` — asset-prep half + `PreparationId` handshake (`:49`, `:99-103`, `:106-154`, `:156-166`). Unchanged.
- `ECS/Events/CombatEvents.cs` — `ShowTransition` (`:79-85`) + `TransitionCompleteEvent` (`:87-90`) to relocate.
- `ECS/Events/SceneEvents.cs` — the 7 lifecycle events (`:18/:23/:30/:36/:42/:48/:55`); new home for `NavigateToSceneEvent`.
- `ECS/Components/Scenes.cs` — `SceneId` (`:14-23`), `SceneState` (`:25-29`), `OwnedByScene` (`:176-180`), `DontDestroyOnLoad` (`:185-189`), `ScenePreparationState` (`:39-50`).
- `ECS/Services/RunLifecycleService.cs` — `EndCurrentRun` (`:12-30`).
- `ECS/Scenes/BattleScene/EnemyAttackResolver.cs` — `IAttackPresentationGate` precedent (`:16-70`); `GraphicsAttackPresentationGate.cs` + `tests/Crusaders30XX.Tests/ImmediateAttackPresentationGate.cs` are the templates for the two gate implementations.
- `Game1.cs` — construction/wiring (`:218`, `:221`, `:300`, `:666`); publisher `:371`.
- New: `ECS/Scenes/Global/SceneNavigator.cs`, `tests/Crusaders30XX.Tests/ImmediateSceneTransitionGate.cs`, `tests/Crusaders30XX.Tests/SceneNavigatorTests.cs`.

## Verification

- `dotnet build` from the repo root is clean (per `AGENTS.md:29`).
- `dotnet test tests/Crusaders30XX.Tests` is green (xUnit, serial); existing caller tests updated from `ShowTransition` to `NavigateToSceneEvent` pass unchanged in behavior.
- New `SceneNavigatorTests` drive the **real** `NavigateTo` through `ImmediateSceneTransitionGate` and pass: destroys prev `OwnedByScene`, preserves `DontDestroyOnLoad`, reload no-op, `SceneState.Current` == target, ends run iff `EndRunOnLoad`, lifecycle events fire in order with `Current` already flipped by `LoadSceneEvent`.
- In-app (`dotnet run`): walk Battle -> WayStation -> Climb -> Achievement and back. Confirm each transition destroys exactly the departed scene's entities, preserves `DontDestroyOnLoad` entities (scene state, card geometry settings — `Game1.cs:356/360`), ends the run only on the run-ending transitions (`WayStation` with `EndRunOnLoad`, e.g. `GuidedTutorialService.cs:201`, `EnemyDefeatFlowSystem.cs:205`), the wipe/hold/`SkipWipe`/`SkipHold` visuals are unchanged, and no entities leak across scenes (spot-check `EntityManager` counts before/after a full loop).

---

