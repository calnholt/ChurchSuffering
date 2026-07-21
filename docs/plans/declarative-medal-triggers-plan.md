# Declarative Medal Triggers Plan

> Deep-module refactor (Ousterhout). Deepen `MedalBase` behind a small declarative trigger interface so the manager layer — not 28 hand-written per-medal `Initialize`/`Dispose` pairs — owns subscribe/unsubscribe/activate. Mirrors the equipment model that already exists in the same codebase. **In-process. Documentation only; implemented later.**

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `ChurchSuffering`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #04 — in-process; effort/risk medium / low.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

Replace 28× hand-wired medal `Subscribe`/`Unsubscribe` pairs with declarative trigger registration owned by `MedalBase`.

---

## Context

**Why:** During the `improve-codebase-architecture` deep-module exploration (2026-07-15) the medal layer surfaced as the widest instance of the "shallow module reimplemented N times" smell called out in the parent RFC set (`docs/plans/deep-module-refactor-round-2-plan.md`). Every one of the 28 medals under `ECS/Objects/Medals/` hand-wires its own `EventManager.Subscribe<...>` inside `Initialize` and must mirror an `EventManager.Unsubscribe<...>` inside `Dispose`. The *identical* concern is already solved declaratively one folder over: equipment declares behavior as delegates and `EquipmentManagerSystem` owns the whole lifecycle. Medals never got the same treatment.

**What prompted it:** the exploration flagged that (a) the subscribe/dispatch/unsubscribe triple is reimplemented 22 times with only the event type and predicate varying; (b) a single forgotten `Unsubscribe` leaks a subscription onto the process-static `EventManager` (`ECS/Core/EventManager.cs:10`, `_eventHandlers` is `static`), which outlives the battle and double-fires next battle; and (c) that leak surface is essentially untested — explicit dispose/unsubscribe assertions exist for only 3 of 28 medals.

**Intended outcome:** medals declare *when they fire* (`{ eventtype, condition, cadence }`) and *what they do* (`Activate()`), and nothing else. A single owner subscribes on medal init and unsubscribes on dispose, symmetric by construction, so the leak/double-fire class is defined out of existence. One-file authoring is preserved — no new cross-cutting interface an author must learn, and the common medal (one phase-triggered effect) is ~3 lines. The three pull-based provider interfaces keep working untouched.

## Problem

"React to a game event, check a condition, activate once" is reimplemented across 28 authored medal files. **22 of 28** hand-wire `EventManager.Subscribe` in `Initialize(EntityManager, Entity)` and mirror `EventManager.Unsubscribe` in `Dispose()`; the other 6 (`StGeorge`, `StChristopher`, `StLawrence`, `StOlaf`, `StNicholas`, `StThomasAquinas`) subscribe to nothing. **13** subscribe `ChangeBattlePhaseEvent` specifically. Each medal repeats the same control flow: trigger handler evaluates a condition → `MedalBase.EmitActivateEvent()` (`ECS/Objects/Medals/MedalBase.cs:23`) → publishes `MedalActivateEvent` → `MedalManagerSystem.OnMedalActivate` (`ECS/Scenes/BattleScene/MedalManagerSystem.cs:34`) → recipe/visual path (`:42-45`) or `MedalTriggered` + `medal.Activate()` after a delay (`:47-51`). Representative today (all file-relative lines):
- `StSebastian.cs` — `Initialize` subscribes `EnemyKilledEvent` (`:22`); `OnEnemyKilled` checks `hp.Current == 1` then `EmitActivateEvent()` (`:32`); `Dispose` unsubscribes (`:46`).
- `StMonica.cs` — subscribes `TriggerTemperance` (`:21`); `EmitActivateEvent()` (`:27`); unsubscribes (`:37`).
- `StElijah.cs` — subscribes `PledgeAddedEvent` **and** `ChangeBattlePhaseEvent` (`:23-24`); `OnAcquire` sets `CurrentCount = MaxCount` (`:27-30`); `OnChangeBattlePhaseEvent` resets the counter on `SubPhase.StartBattle` (`:32-36`); `OnPledgeAdded` guards `CurrentCount`, zeroes it, `EmitActivateEvent()` (`:38-44`); `Dispose` unsubscribes both (`:58-59`). Four structural problems: 1. **28× reimplemented subscribe/dispatch/unsubscribe vs equipment's declarative model.** `EquipmentBase` (`ECS/Objects/Equipment/EquipmentBase.cs`) declares behavior as two delegate properties — `OnActivate` (`Action`, `:57`) and `CanActivate` (`Func`, `:58`) — set in the subclass ctor (e.g. `HeartforgeCuirass.cs:22-30` sets `OnActivate` and nothing else). `EquipmentManagerSystem` (`ECS/Scenes/BattleScene/EquipmentManagerSystem.cs`) owns the *entire* lifecycle behind one `EventManager.Subscribe` in its ctor (`:16`): availability (`IsAvailable`/`IsUsed`, `:37`), the `CanActivate()` guard (`:42`), the recipe/visual branch (`:47-49`), `OnActivate(...)` invocation (`:52`), `MarkUsed()` (`:53`), and publishing `EquipmentAbilityTriggered` (`:54`). **The asymmetry:** equipment has *no per-instance subscriptions* — one uniform trigger (a player-driven `EquipmentActivateEvent`) covers all equipment, so the manager subscribes once, globally. Medals are *passively* triggered by many different game events (one bespoke event set per medal), so today each medal subscribes for itself — and must remember to unsubscribe for itself. Equipment's manager owns lifecycle because there is no per-instance subscription to own; the medal layer has exactly that per-instance subscription and no owner for it.
2. **Latent subscription-leak / double-fire bug class.** `EventManager` is static; `_eventHandlers` (`EventManager.cs:34`) outlives any battle. Medal teardown runs through `EquippedMedal.Dispose()` (`ECS/Components/CardComponents.cs:1073-1076`), which calls `Medal?.Dispose()`. A medal whose `Dispose` omits (or mis-types) one `Unsubscribe` leaks that handler onto the static bus. Next battle a fresh instance subscribes again, so the event now has **two** live handlers — the orphaned one plus the new one — and the effect double-fires. `Unsubscribe` matches by delegate-reference equality (`EventManager.cs:67`, `ph.Handler.Equals(handler)`), so the symmetry is entirely dependent on each author pairing the exact same handler in two hand-written methods. A repo sweep finds all 28 currently symmetric, but the guarantee is per-file discipline, not construction — one typo reintroduces it, and it is invisible in the file that has it.
3. **26/28 subscribe/unsubscribe lifecycles effectively untested.** Explicit dispose-then-assert-no-refire tests exist for only **3** medals — `StPeter` (`tests/ChurchSuffering.Tests/MedalCounterTests.cs:199`), `StLazarus` (`:445`), `StAnthonyOfPadua` (`:1409`). `MedalCounterTests` exercises the *subscribe* path of ~16 medals implicitly (Initialize → publish → assert `CurrentCount`/activation), but never asserts that `Dispose` removed the handler or that a second battle does not double-fire. **8 medals have zero behavioral coverage at all**: `StLuke`, `StSebastian`, `StNicholas`, `StMichael`, `StLouieIX`, `StJoanOfArc`, `StFrancisDeSales`, `StAugustine`. The leak class in problem 2 is precisely the thing no test guards.
4. **Control flow buried in per-medal closures.** The "when" lives inside private handler methods (`OnEnemyKilled`, `OnPledgeAdded`, `OnChangeBattlePhaseEvent`, …). Whether a medal is once-per-battle, every-Nth, or every-time is knowable only by reading its handler bodies and its `OnAcquire`/`CurrentCount` plumbing. For the 6 once-per-battle medals this is *three* correlated pieces (`OnAcquire` seed, a `ChangeBattlePhaseEvent(StartBattle)` reset handler, and a `CurrentCount` guard) hand-maintained per file — see `StElijah.cs:27-44`.

## Proposed Interface

Medals declare triggers; a manager-owned binder subscribes/unsubscribes them. `MedalBase.Initialize`/`Dispose` become **sealed** template methods — authors can no longer write `Subscribe`/`Unsubscribe`, so the leak class is gone by construction.

```csharp
namespace ChurchSuffering.ECS.Objects.Medals { //AuthorstouchONLY:ctor (Id/Name/Text), ConfigureTriggers (when),
Activate (effect). public abstract class MedalBase : IDisposable { public string Id { get; set; } public string Name {
get; set; } = ""; public string Text { get; set; } = ""; public EntityManager EntityManager { get; set; } public
Entity MedalEntity { get; set; } public VisualEffectRecipe ActivationEffectRecipe { get; protected set; } public int
CurrentCount { get; set; } public int MaxCount { get; set; } public bool Activated { get; set; } private
IReadOnlyList_bindings; //THEdeclarativehook.Default:no triggers (provider-only /OnAcquire-onlymedals). protected
virtual void ConfigureTriggers(IMedalTriggerRegistry triggers) { } //SEALEDtemplate—owns subscribe.Noper-medal
Subscribe survives.public void Initialize(EntityManager entityManager, Entity medalEntity) {EntityManager =
entityManager; MedalEntity = medalEntity; var registry = new MedalTriggerRegistry(this);
//manager-sidebinderConfigureTriggers(registry); _bindings=registry.Build(); foreach (varb in _bindings)
b.Subscribe(); } //SEALEDtemplate—owns unsubscribe.Symmetric to Initialize by construction.public void Dispose() { if
(_bindings==null) return; foreach (varb in _bindings) b.Unsubscribe(); _bindings=null; } protected void
EmitActivateEvent() => EventManager.Publish(new MedalActivateEvent { MedalEntity = MedalEntity }); public virtual void
Activate() { } //theeffect(unchangedauthoring surface) public virtual void OnAcquire() { } } //Fluent, event-typed,
one-file — no new namespace for authors to learn.public interface IMedalTriggerRegistry { //SubscribetoTEvent;
firewhen 'when' is true (null == always).priority passthrough for the //rareorderedmedal(StHomobonususes priority:1
today).Returns a builder for cadence.IMedalTriggerBuilder On(Funcwhen = null, int priority = 0) where TEvent : class;
} public interface IMedalTriggerBuilder {IMedalTriggerBuilder OncePerBattle(); //auto-wirestheStartBattlereset — no
OnAcquire/CurrentCountcodeIMedalTriggerBuilderEveryNth(intn); //fireoneveryNth match (counter medals)
IMedalTriggerBuilder Do(Action effect); //OPTIONALper-triggereffect; default = EitAtit Et() } }
```

**Wh t EmitActivateEvent() } } **What `MedalManagerSystem` / the binder owns** (hidden from authors): the generic `EventManager.Subscribe` / `Unsubscribe` mechanics (`EventManager.cs:42/61`); the delegate-reference symmetry that makes unsubscribe exact; the once-per-battle reset (the binder emits a *second* `MedalTriggerBinding` that resets the cadence counter on `SubPhase.StartBattle`, subscribed/unsubscribed alongside the primary binding so the reset never leaks either); every-Nth counting; priority passthrough; and the existing dispatch tail it already owns — `MedalActivateEvent` → recipe/visual (`QueuedActivateMedalWithVisual`, `MedalManagerSystem.cs:42-45`) or delayed `MedalTriggered` + `medal.Activate()` (`:47-51`). The activation half of `MedalManagerSystem` is unchanged; this RFC adds the subscription half it never had. The binding itself captures the concrete event type once, so subscribe and unsubscribe use the *same* delegate instance — the exact requirement `EventManager.Unsubscribe` imposes (`:67`):

```csharp
internal sealed class MedalTriggerBinding : IMedalTriggerBinding where TEvent: class { private readonlyAction_handler;
//oneinstance, used for both calls private readonly int _priority; public MedalTriggerBinding(Action handler, int
priority) {_handler=handler; _priority=priority; } public void Subscribe() =>EventManager.Subscribe(_handler,
_priority); public void Unsubscribe() =>EventManager.Unsubscribe(_handler); }
```

### Before / after — `StSebastian`

Before (`ECS/Objects/Medals/StSebastian.cs`, 49 lines — Initialize+Subscribe, private handler, Activate, Dispose+Unsubscribe):

```csharp
public override void Initialize(EntityManager entityManager, Entity medalEntity) {EntityManager = entityManager;
MedalEntity = medalEntity; EventManager.Subscribe(OnEnemyKilled); } private void OnEnemyKilled(EnemyKilledEventevt) {
var player = EntityManager.GetEntitiesWithComponent().FirstOrDefault(e => e.HasComponent()); if (player == null)
return; var hp = player.GetComponent(); if (hp == null || hp.Current != 1) return; EmitActivateEvent(); } public
override void Activate() => EventManager.Publish(new IncreaseMaxHpEvent { Target = EntityManager.GetEntity("Player"),
Delta = 1 }); public override void Dispose() => EventManager.Unsubscribe(OnEnemyKilled);
```

After — no `Initialize`, no `Dispose`, no `Subscribe`/`Unsubscribe`, no private handler:

```csharp
protected override void ConfigureTriggers(IMedalTriggerRegistry triggers)
=>triggers.On(_=>EntityManager.GetEntitiesWithComponent() .FirstOrDefault(e =>
e.HasComponent())?.GetComponent()?.Current == 1); public override void Activate() => EventManager.Publish(new
IncreaseMaxHpEvent { Target = EntityManager.GetEntity("Player"), Delta = 1 });
```

The common medal collapses to one trigger line. `StMonica` after: `triggers.On(e => e.Owner != null);` — literally three lines counting the ctor and `Activate`. `StElijah` after drops its entire once-per-battle apparatus (`OnAcquire`, the `ChangeBattlePhaseEvent` handler, and the `CurrentCount` guard, `StElijah.cs:23-44`) to one line: `triggers.On(e => e.Card?.GetComponent() != null).OncePerBattle();`. Counter medals (`StPeter` every 3 black blocks, `StLouieIX` every 3rd `StartPlayerTurn`, `StLazarus` every 2 mills, `StBenedict` every 3rd pledge, `StHomobonus` every 3rd reward) become `On(pred).EveryNth(n)`; `StHomobonus` adds `priority: 1` in the `On<>` call.

### How the provider interfaces coexist (untouched)

The three orthogonal provider interfaces are **pull-based**, not event-subscription-based, so they are entirely outside this refactor and keep working unchanged:
- `IAlternateCardPlayProvider` (`StGeorge`) — consumed by `AlternateCardPlayService.GetProfile` iterating `GetEntitiesWithComponent()` and testing `equipped.Medal is IAlternateCardPlayProvider` (`ECS/Scenes/BattleScene/AlternateCardPlayService.cs:52-62`).
- `ICardStatModifierProvider` (`StChristopher`, `StLawrence`) — `CardStatModifierService.cs:276-282`, same query shape.
- `IReplacementEffectProvider` (`StOlaf`) — `ReplacementEffectSystem.cs:30-36`, same. None of these read the `EventManager` subscription set; they query equipped medals synchronously. These four medals already subscribe to zero events, so their `ConfigureTriggers` is the default no-op — they simply stop overriding `Initialize` to set `EntityManager`/`MedalEntity` (the sealed base now does it) and keep their interface method verbatim. A medal may both declare triggers *and* implement a provider interface; the two paths never interact.

### Rejected alternatives

1. **Keep manual (status quo).** Rejected: leaves 22 reimplementations, preserves the leak class as a per-file discipline problem, and keeps once-per-battle as three correlated hand-maintained pieces.
2. **Attribute-driven** (`[MedalTrigger(typeof(PledgeAddedEvent))]` + startup reflection). Rejected: conditions and cadence are *code*, not data — attributes cannot express `hp.Current == 1` or `EveryNth` without inventing a DSL; adds a reflection scan; and there is no attribute-driven precedent in the repo, whereas equipment already establishes the delegate idiom.
3. **Bare delegate-list mirroring equipment's single `OnActivate`** (a `List<(Type, Func, Action)>` the author fills in the ctor). Rejected: loses compile-time event typing (author down-casts `object` to the event), and cannot auto-wire the `OncePerBattle` `StartBattle` reset. The fluent `On(...)` builder keeps the payload strongly typed and folds cadence into the binder — chosen.

## Dependency Strategy

**Category: in-process.** No new package/assembly/network boundary. `MedalActivateEvent` / `MedalTriggered` / the recipe queue stay exactly as they are. **Ownership of subscribe/unsubscribe (leak-proof by construction).** Medals are `Initialize`d at their equip/acquire sites (`EntityFactory.cs:132`, `RunMedalService.cs:54`, `BoosterPackOpeningDisplaySystem.cs:792`) and `Dispose`d via `EquippedMedal.Dispose()` (`CardComponents.cs:1073-1076`) on teardown. Those are the real lifecycle hooks, so the sealed `MedalBase.Initialize`/`Dispose` template pair is where the manager-owned binder is driven — `Initialize` subscribes exactly the set `ConfigureTriggers` declared; `Dispose` unsubscribes exactly that same set (same delegate instances). Because both methods are sealed and there is no per-medal subscription code, it is impossible to subscribe without a matching unsubscribe or to mismatch the delegate — the leak/double-fire class is defined out of existence. `MedalManagerSystem` remains the activation owner and is the natural host for the binder if a battle-scoped (rather than base-invoked) binder is preferred later; the interface above does not change either way. **Authoring ergonomics preserved.** The new types live in the existing `ChurchSuffering.ECS.Objects.Medals` namespace (proposed file `ECS/Objects/Medals/MedalTriggers.cs`), so a medal author still writes one file, imports nothing new, and touches three familiar surfaces (ctor, `ConfigureTriggers`, `Activate`). No system reference is passed to a medal (respects `AGENTS.md:38`); effects still route through events, not service writes (respects `AGENTS.md:41`, `docs/coding-standards.md:14`).

## Testing Strategy

**New — one parameterized boundary test across ALL 28 medals**, seeded from `MedalFactory.GetAllMedals()` (`ECS/Factories/MedalFactory.cs:58-63`) so every future medal is auto-enrolled. Following the repo harness (bare `new EntityManager()`, `new` the management systems, `EventManager.Clear()` between cases; serial per `TestAssembly.cs:3`). For each medal id:
- **Subscribe/unsubscribe symmetry (leak check, fully generic).** Record `EventManager.GetEventHandlerCount()` (`EventManager.cs:106`); `Initialize`; assert the count rose by the medal's declared binding count; `Dispose`; assert the count returned to baseline. This is the assertion that catches the leak class for all 28 without per-medal knowledge.
- **No double-fire across battles.** `Initialize` → `Dispose` → `Initialize` a fresh instance → publish the medal's declared trigger event with a condition-satisfying world → assert the medal activates exactly **once** (count `MedalActivateEvent`/`MedalTriggered`). Under the old model a missing `Unsubscribe` fires twice here; under the new model the sealed teardown makes twice impossible.
- **Fires on event Y when condition Z, once.** A data-driven `(medalId, triggerEvent, worldSetup, expectedActivations)` table, sourced from the medal's declared triggers, drives the positive case and (where cheap) a negative case; `OncePerBattle` medals also get a "fires once, then a `StartBattle` phase change re-arms it" row exercising the binder's auto-reset. **Kept unchanged (must pass as-is) — the provider-interface and flow tests:** `StGeorgeMedalTests` (`tests/ChurchSuffering.Tests/StGeorgeMedalTests.cs`, alternate-play provider end-to-end), `FrostbiteReplacementEffectTests` (`StOlaf`), `CardStatModifierServiceTests` (`StChristopher`/`StLawrence`), and the `MedalFactory_includes_*` registry assertions. These exercise the pull-based provider paths this refactor does not touch. **Replaced:** the ad-hoc per-medal manual-wiring assertions in `MedalCounterTests` — specifically the three explicit dispose/unsubscribe tests (`:199`, `:445`, `:1409`) — collapse into the generic parameterized symmetry/no-double-fire test above, which now covers all 28 instead of 3. The implicit subscribe-path counter assertions (`CurrentCount`/`EveryNth` behavior) stay, retargeted at the declared triggers where they add medal-specific value.

## Implementation Recommendations

**Owns:** the binder (`MedalTriggerRegistry` + `MedalTriggerBinding`) — generic subscribe/unsubscribe, delegate-reference symmetry, `OncePerBattle` `StartBattle` auto-reset, `EveryNth` counting, priority passthrough; plus the existing `MedalActivateEvent` dispatch tail already in `MedalManagerSystem`.

**Hides:** that a trigger is an `EventManager` subscription at all; the once-per-battle three-piece apparatus; the delegate-equality requirement of `Unsubscribe`; the recipe/visual-vs-delayed activation branch.

**Exposes:** `ConfigureTriggers(IMedalTriggerRegistry)` + the fluent `On().OncePerBattle()/.EveryNth(n)/.Do(effect)` builder; unchanged `Activate()`. **Migration (additive, then mechanical, in batches):**
1. Add `MedalTriggers.cs` (registry/builder/binding) and convert `MedalBase.Initialize`/`Dispose` to the sealed template + `ConfigureTriggers` hook. No medal changes yet; the default no-op `ConfigureTriggers` keeps every medal compiling. Provider-only medals immediately drop their `Initialize` override.
2. Migrate the 22 event-subscribing medals in batches (grouped by shape: StartBattle-fire, once-per-battle, every-Nth, plain-condition). Each medal loses `Initialize`/`Dispose`/`Subscribe`/` Unsubscribe`/private handler and gains one `ConfigureTriggers`; `Activate` is untouched. Provider interfaces stay verbatim.
3. Land the parameterized boundary test; delete the superseded manual-wiring assertions.

**Effort: medium** — 28 authored files plus one new file and the `MedalBase` template; each medal edit is small and mechanical (the hard design work is in the binder).

**Risk: low but content-authoring sensitive.** The refactor is behavior-preserving (same events, same activation tail) and the new generic test proves the leak class gone. The real risk is regressing the authoring flow: **update the `create-medal` skill** (`.claude/skills/create-medal/SKILL.md`) — its template and rules currently instruct authors to hand-wire `EventManager.Subscribe` in `Initialize` and mirror `Unsubscribe` in `Dispose`; those steps must be replaced with the `ConfigureTriggers` pattern, and its reference medal (`StElijah`) re-pointed at the post-migration form. (Note: that skill's factory-registration section is *already* stale versus the enum-based `MedalId` + `GameIdExtensions.ToKey` + single `MedalFactory.MedalConstructors` reality in `ECS/Data/Ids/GameIds.cs:80-110,341-372` and `ECS/Factories/MedalFactory.cs:11-42`; fix it in the same pass.) Do not change save-file behavior (`AGENTS.md:37`).

## Critical files

- `ECS/Objects/Medals/MedalBase.cs ` — `EmitActivateEvent` (`:23`), `Activate` (`:27`), `Initialize`/`Dispose` (`:21`, `:41`); gains the sealed template + `ConfigureTriggers`.
- `ECS/Objects/Medals/MedalTriggers.cs` — **new**: `IMedalTriggerRegistry` / `IMedalTriggerBuilder` / `MedalTriggerRegistry` / `MedalTriggerBinding`.
- `ECS/Scenes/BattleScene/MedalManagerSystem.cs` — activation owner (`OnMedalActivate` `:34`); unchanged behavior, natural binder host.
- `ECS/Objects/Equipment/EquipmentBase.cs` (`OnActivate`/`CanActivate` `:57-58`) + `ECS/Scenes/BattleScene/EquipmentManagerSystem.cs` (`:16`, `:37-54`) — the declarative model being mirrored.
- `ECS/Core/EventManager.cs` — `Subscribe`/`Unsubscribe` (`:42`/`:61`), delegate-equality removal (`:67`), `GetEventHandlerCount` (`:106`, used by the leak test).
- `ECS/Components/CardComponents.cs:1073-1076` (`EquippedMedal.Dispose` → `Medal?.Dispose()`) and equip sites `ECS/Factories/EntityFactory.cs:132 `, `ECS/Services/RunMedalService.cs:54`, `ECS/Scenes/BoosterPackOpeningDisplaySystem.cs:792`.
- Provider path (must keep working): `ECS/Scenes/BattleScene/AlternateCardPlayService.cs:52-62`, `CardStatModifierService.cs:276-282`, `ReplacementEffectSystem.cs:30-36`; medals `StGeorge.cs`, `StChristopher.cs`, `StLawrence.cs`, `StOlaf.cs`.
- The 28 authored medals under `ECS/Objects/Medals/` (before/after exemplars: `StSebastian.cs`, `StMonica.cs`, `StElijah.cs`).
- Tests: `tests/ChurchSuffering.Tests/MedalCounterTests.cs`, `StGeorgeMedalTests.cs`, `FrostbiteReplacementEffectTests.cs`, `CardStatModifierServiceTests.cs`, `TestAssembly.cs:3`.
- Authoring: `.claude/skills/create-medal/SKILL.md` (must be updated).

## Verification

- `dotnet build` from repo root is clean (`AGENTS.md:29`).
- `dotnet test tests/ChurchSuffering.Tests` is green (xUnit, serial per `TestAssembly.cs:3`).
- The new parameterized medal-lifecycle test passes for all 28: for every medal, `GetEventHandlerCount()` returns to baseline after `Dispose`, and re-`Initialize` + publish the declared trigger activates **once** (no double-fire). Confirm it would have caught a deliberately reintroduced missing-unsubscribe (spike a mismatch, watch the count-not-restored / double-activation assertion fail, revert).
- The kept provider tests pass unchanged: `StGeorgeMedalTests`, `FrostbiteReplacementEffectTests`, `CardStatModifierServiceTests`.
- In-app (`dotnet run -- new`): trigger several medals across **two consecutive battles** and confirm no double-fire (the leak bug) — e.g. equip `StSebastian`/`StMonica`/`StElijah `, resolve battle 1 to trigger each, start battle 2, trigger again, and verify each fires exactly once per battle. Confirm `StGeorge` alternate block-as-attack play still works (its provider path is untouched).

---

