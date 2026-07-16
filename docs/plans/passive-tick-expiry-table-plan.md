# Passive Tick/Expiry Definition Table Plan

> Sixth candidate from the deep-module exploration; RFC #05 in the Round 2 series ([`deep-module-refactor-round-2-plan.md`](deep-module-refactor-round-2-plan.md)). Sequence **after** the phase-flow work (larger blast radius; the Stun branch re-enqueues a phase sequence). Medium-large effort.

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #05 — in-process; effort/risk medium-large / medium.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

Replace phase-keyed passive dispatch and five hardcoded scope `HashSet`s with a per-passive `PassiveRegistry` definition table.

---

## Context

**Why:** `ECS/Scenes/BattleScene/AppliedPassivesManagementSystem.cs` (587 lines) is the single god-file that ticks, decrements, expires, and applies every one of the 41 `AppliedPassiveType` values (`ECS/Components/CardComponents.cs:1184-1227`). It is keyed on battle phase (`OnChangeBattlePhase`, `:84`), and it decides *when a passive expires* in **five hardcoded `HashSet`** located ~300 lines away from where the passive's *effect* is written. Adding or re-scoping a passive means editing a switch branch **and** finding the right one of five far-away sets — the two halves of one fact live in different regions of the same file, and three enum values are in **none** of the sets.

**What prompted it:** the `improve-codebase-architecture` deep-module exploration (2026-07-15), extending the five RFCs in `docs/plans/deep-module-refactor-round-2-plan.md`. The pure passive *math* is already extracted and headless-tested (`ECS/Scenes/BattleScene/AppliedPassivesService.cs`, `CardStatModifierService.cs`); what remains shallow is the **sequencing/expiry classification** and the fleet of near-empty per-passive shell systems that exist only because there is no "on event X, for passive Y, do Z" registration point.

**Intended outcome:** a per-passive **definition table** where each passive declares its scope, its trigger, and its effect as co-located metadata; the five hardcoded HashSets become a projection over that metadata; the genuinely-shell reactive systems collapse into table entries; and two tiny accessors kill the two most-copied idioms in the battle layer. The pure math stays exactly where it is. Passive-authoring ergonomics improve (one entry per passive, not one switch-branch + one-of-five sets + maybe a new one-handler system).

## Problem

`AppliedPassivesManagementSystem` is a 587-line phase-keyed dispatcher (`OnChangeBattlePhase`, `:84-129`) whose per-passive knowledge is smeared across three unrelated regions: **1. Effects live in phase-branch god-methods.**
- `ApplyStartOfTurnPassives` (`:224-255`) — Inferno→Burn (`:229-246`, note the composite: Inferno feeds Burn stacks and the two are summed in one branch), Webbing→Slow (`:247-254`).
- `ApplyStartOfPreBlockPassives` (`:257-317`) — Stun (`:262-293`), Aggression (`:295-307`), Power (`:308-316`).
- `ApplyEndOfTurnPassives` (`:483-492`) — CarpeDiem only.
- Plus one-off special-cases scattered as private methods: `ApplySubZeroFreeze` (`:131-144`), `ExpireGuardAtEnemyStart` (`:183-194`), `RemoveOneScarAtStartOfBattle` (`:196-207`), `RemoveOneFearAtBattleEnd` (`:215-223`), `EnemyShieldsMaintenance` (`:173-181`), `OnCardPlayed`/SwordIntoShield (`:36-61`), and the threshold-replacement `ResolveFrostbiteThresholds` (`:368-419`) reached from a `switch (e.Type)` inside `OnApplyPassive` (`:350-360`). **2. Duration-scope is 5 hardcoded sets, far from the passive, non-exhaustive and overlapping.** `GetTurnPassives` (`:513`, 7 types), `GetTurnPassivesToDecrement` (`:527`, 3 types), `GetBattlePassives` (`:537`, 22 types), `GetRunLongPassives` (`:565`, 6 types), `GetQuestPassives` (`:578`, 2 types) classify the enum. Consumed by expiry (`RemoveTurnPassives`, `:494-511`) **and** four other call sites that need scope for reasons unrelated to ticking:
- `OnLoadScene` keep-set (`:157-159`) — run-long ∪ quest survive a scene load.
- run-long sync back to persistent state at `OnApplyPassive:362`, `OnRemovePassive:442`, `OnUpdatePassive:465` (each guards `GetRunLongPassives().Contains(e.Type)` then calls `RunScopedStateService.SyncRunLongPassivesFromPlayer`, `ECS/Services/RunScopedStateService.cs:43`). Smells verified against the enum:
- **Overlap:** `Slow` is in both `GetTurnPassivesToDecrement` (`:532`) and `GetBattlePassives` (`:546`); `Poison` is in both `GetTurnPassivesToDecrement` (`:533`) and `GetRunLongPassives` (`:574`). Membership is not a partition — which set "wins" is implicit in call order.
- **Unclassified:** `Shield`, `Intellect`, and `Frozen` are in **none** of the five sets. `Shield` survives only via the `EnemyShieldsMaintenance` special-case (`:173`); the other two have no scope at all.
- **Duplicated-fact:** `Aggression` is a Turn passive (`:518`) **and** is explicitly removed by a `TimerScheduler` in the PreBlock branch (`:303-306`); its lifetime is stated twice. **3. The Stun branch embeds phase orchestration.** `ApplyStartOfPreBlockPassives`, when the last planned attack is consumed by Stun, inline-clears the queue and re-enqueues the entire `EnemyEnd → PlayerStart → Action` sequence (`:277-289`). This is the same hardcoded-phase-sequence-enqueue pattern that `EnemyPhaseFlowSystem.ContinueFlow` already owns for `EnemyStart → PreBlock → Block` (`EnemyPhaseFlowSystem.cs:213-221`) — a passive effect is reaching into phase-flow's job. **4. Copy-pasted read idioms (verified).**
- `...Passives.TryGetValue(AppliedPassiveType.X, out int stacks)` + `stacks <= 0` guard: **42 occurrences across 26 files** (e.g. `AppliedPassivesManagementSystem.cs` ×12, `CardStatModifierService.cs` ×4, `EnemyIntentPipsSystem.cs` ×2, plus one-offs in card/enemy objects and tests). There is no `entity.Passive(type)` / `passives.Get(type)` accessor. - "get the Player entity": `GetEntitiesWithComponent().FirstOrDefault()` **88 occurrences across 56 files**; the shell systems use the sibling variant `EntityManager.GetEntity("Player")` /`GetEntity("Enemy")` (`BleedManagementSystem.cs:46`, `AnathemaManagementSystem.cs: 32`, `VigorManagementSystem.cs:36`). `GetComponentHelper` (`ECS/Services/GetComponentHelper.cs`) already hosts `GetAppliedPassives`/`GetCourage `/`GetHandOfCards` but has **no** `GetPlayer`/`GetEnemy`. **5. Near-empty reactive shell systems exist only for lack of a registration point.** Each is a single handler = one guarded stack read + one publish:
- `AnathemaManagementSystem` (`:28-50`) — `OnPledgeAdded` → read Anathema on enemy → one `ModifyHpRequestEvent`.
- `VigorManagementSystem` (`:25-47`) — `OnCardPlayed` → `VigorService.GetWaivedPipCount` → one `ApplyPassiveEvent{Vigor,-consumed}`.
- `BleedManagementSystem` (`:44-78`) — `OnConfirmBlocksRequested` → per-qualifying-color HP loss + Bleed decrement; the pure `GetQualifyingSameColorCount(progress)` static (`:34-42`) is separately unit-tested. *Corrections to the exploration notes (verified by reading):*
- The pure math lives at **`ECS/Scenes/BattleScene/AppliedPassivesService.cs`** (namespace `Crusaders30XX.ECS.Systems`), **not** `ECS/Services/`.
- **`RecoilManagementSystem` is not a pure shell** — 5 subscriptions (`ApplyRecoilEvent`/`CardBlockedEvent`/`AttackResolved`/`BeginDefeatPresentationEvent`/`EnemyPhaseResetEvent`, `:23-27`) managing a per-card `Recoil` component lifecycle with random selection. Borderline; **do not** fold wholesale.
- **`PlunderManagementSystem` is not a shell** — a 404-line stateful animation/deck system (pending-card state, `EventQueueBridge` snatch/discard animations, HP-gauge tracking, `Plundered`/`PlunderSnatchFlight` component lifecycles). **Exclude** from this refactor entirely; its only shell-like touch is the `AppliedPassiveType.Plunder` presence-check at `PlunderManagementSystem.cs:53 `. Net: "what scope does passive X have, when does it tick, and what does it do" is one fact split across a switch branch, one of five far-away sets, and (sometimes) a dedicated system. Passive math is deep and tested; passive **sequencing/classification** is the shallow part.

## Proposed Interface

A per-passive `PassiveDefinition` record plus a `PassiveRegistry` static table. The god-file's phase branches become **iterate the registry filtered by trigger, run each effect**; the five HashSets become **projections over `Scope`**. The effect delegate is executed *by* the management system (a `System`), so publishing/enqueueing events from it stays compliant with "services are read-only, systems write" (`docs/coding-standards.md:14`, `AGENTS.md:41`).

```csharp
namespace Crusaders30XX.ECS.Systems { //Replacesthe5hardcoded HashSets.Exactly one scopeper passive.public enum
PassiveScope { Turn, //GetTurnPassives—removedwholesale at ownerturn end TurnDecrement,
//GetTurnPassivesToDecrement—lose1 + PassiveTriggeredat turnend Battle, //GetBattlePassives—persistswithina battle
/bossphaseRunLong, //GetRunLongPassives—persistsacross battles (RunScopedStatesync) Quest,
//GetQuestPassives—persistswithina quest Unscoped //Shield/Intellect/Frozen—noauto-expiry; owned elsewhere }
//Whichphase/eventtickstheeffect.Maps 1:1 tothegod-file's dispatch points.public enum PassiveTrigger { None,
StartOfBattle, //OnChangeBattlePhaseSubPhase.StartBattleStartOfTurn,
//ApplyStartOfTurnPassives(EnemyStart/PlayerStart) StartOfPreBlock, //ApplyStartOfPreBlockPassives(SubPhase.PreBlock)
EndOfTurn, //ApplyEndOfTurnPassives(SubPhase.PlayerEnd) BattleEnd, //OnEnemyKilledOnApplyThreshold, //OnApplyPassive
switch (Frostbite) OnCardPlayed, //OnCardPlayed(SwordIntoShield) + Vigor consume OnConfirmBlocks,
//BleedManagementSystemOnPledge//AnathemaManagementSystem}
//HandedtoeacheffectbyAppliedPassivesManagementSystem.Carries the write channel //thesystemalreadyuses
(EnqueueTriggerAction /EventManager.Publish) so effects stay //authoredthewaythegod-file'sbranches are today.public
readonly struct PassiveTickContext { public EntityManager Entities {get; init; } public Entity Owner { get; init; }
public int Stacks { get; init; } public Action Enqueue { get; init; }
//EventQueueBridge.EnqueueTriggerAction//EventManager.Publish is static; effects callit directly, as the branches
donow.} public sealed record PassiveDefinition { public AppliedPassiveType Type { get; init;} public PassiveScope
Scope { get; init; } public PassiveTrigger Trigger {get; init; } = PassiveTrigger.None; public int Order { get; init;
} = 0; //intra-triggertie-break(Infernobefore Burn) public int? ThresholdEvery { get; init; }
//Frostbite:fireperNstacks (TooltipTextService.FrostbiteThreshold) public Action Effect { get; init; } // null =
data-only (pure scope classification) } public static class PassiveRegistry { public static PassiveDefinition
Get(AppliedPassiveTypetype); //nevernull; Unscoped/None default public static IReadOnlyList
WithTrigger(PassiveTriggert); //pre-sortedbyOrder public static IReadOnlySet WithScope(PassiveScopes);
//replacesthe5Get*Passives() } }
```

Accessors that kill the two idioms (added to the existing `GetComponentHelper` + one extension class, no new namespaces):

```csharp
namespace Crusaders30XX.ECS.Services {public static partial class GetComponentHelper { public static Entity
GetPlayer(EntityManagerem) => em.GetEntitiesWithComponent().FirstOrDefault(); //killsthe88xidiom public static Entity
GetEnemy(EntityManagerem) => em.GetEntitiesWithComponent().FirstOrDefault(); } public static class PassiveAccessor {
//0whenthecomponentorkey is absent — collapses "TryGetValue + stacks<=0" (42x). public static int Passive(this Entity
owner, AppliedPassiveTypetype) => owner?.GetComponent()?.Passives is { } p && p.TryGetValue(type, out int n) ? n : 0;
public static bool HasPassive(this Entity owner, AppliedPassiveTypetype) => owner.Passive(type) > 0; } }
```

**Usage — register one passive (Burn), and the two call sites that read it:**

```csharp
//TABLE(co-located:scope+ trigger + effect in one place, nextto Inferno) PassiveRegistry.Register(new
PassiveDefinition { Type = AppliedPassiveType.Inferno, Scope = PassiveScope.Battle, Trigger =
PassiveTrigger.StartOfTurn, Order = 0, Effect = ctx => ctx.Enqueue("Passive.Inferno", () => { EventManager.Publish(new
ApplyPassiveEvent { Target = ctx.Owner, Type = AppliedPassiveType.Burn, Delta = ctx.Stacks });
EventManager.Publish(new PassiveTriggered { Owner = ctx.Owner, Type = AppliedPassiveType.Inferno }); },
AppliedPassivesManagementSystem.Duration), }); PassiveRegistry.Register(new PassiveDefinition { Type =
AppliedPassiveType.Burn, Scope = PassiveScope.Battle, Trigger = PassiveTrigger.StartOfTurn, Order = 1,
//runsafterInferno'sApplyPassive Effect = ctx => ctx.Enqueue("Passive.Burn", () => {EventManager.Publish(new
ModifyHpRequestEvent { Source = ctx.Owner, Target = ctx.Owner, Delta = -ctx.Stacks, DamageType = ModifyTypeEnum.Effect
}); EventManager.Publish(new PassiveTriggered { Owner = ctx.Owner, Type = AppliedPassiveType.Burn }); },
AppliedPassivesManagementSystem.Duration), }); //DISPATCH(wholeApplyStartOfTurnPassivescollapses tothis, for any
trigger) foreach (vardef in PassiveRegistry.WithTrigger(PassiveTrigger.StartOfTurn)) { int stacks =
owner.Passive(def.Type); if (stacks <= 0) continue; def.Effect(new PassiveTickContext { Entities = EntityManager,
Owner = owner, Stacks = stacks, Enqueue = EventQueueBridge.EnqueueTriggerAction }); } //EXPIRY(RemoveTurnPassives) —
nomorehand-written sets foreach (vart in PassiveRegistry.WithScope(PassiveScope.Turn)) { /*RemovePassive*/} foreach
(vart in PassiveRegistry.WithScope(PassiveScope.TurnDecrement)) { /*UpdatePassive-1+PassiveTriggered */}
//SCOPECONSUMERSelsewherereadthe same projection: var keep = PassiveRegistry.WithScope(PassiveScope.RunLong)
//OnLoadScenekeep-set.Concat(PassiveRegistry.WithScope(PassiveScope.Quest)); if (PassiveRegistry.Get(e.Type).Scope ==
PassiveScope.RunLong) RunScopedStateService.SyncRunLongPassivesFromPlayer(e.Target);
```

**What it hides / fixes:**
- The 5 non-exhaustive, overlapping HashSets → one `Scope` per passive (a partition; `Slow`/`Poison` overlaps are resolved by choosing the dominant scope; `Shield`/`Intellect`/`Frozen` become explicit `Unscoped`).
- Which phase/event ticks a passive, and the intra-phase ordering invariant (Inferno-before-Burn) → `Trigger` + `Order`, instead of top-to-bottom method position.
- The `Aggression` double-statement → its Turn scope drives expiry; the PreBlock branch stops re-removing it.
- The 42x/88x read idioms → `owner.Passive(type)` and `GetComponentHelper.GetPlayer(em)`.
- Frostbite's threshold branch → declarative `ThresholdEvery` (`OnApplyThreshold`), keeping `ResolveFrostbiteThresholds`' replacement-effect body as the effect. **Rejected alternatives:**
1. **Full data-driven JSON/`Content` table.** Passive effects publish typed events and call `EventQueueBridge`/`TimerScheduler` — not expressible as data without a mini-interpreter. Rejected: reintroduces the shallowness one level down (a "passive effect DSL") and breaks C#-native authoring. The table stays **code** (C# object initializers), data only for scope/trigger/order.
2. **Per-passive `IPassiveBehavior` registry (one class per passive).** Correct OO shape, but 41 near-empty classes is *more* ceremony than 41 record literals, and it re-creates the shell-system fan-out this RFC is removing. Rejected in favor of a flat record table; a passive that genuinely needs a class (none today) can supply an `Effect` delegate that forwards to one.

## Dependency Strategy

**In-process**, single assembly, no new boundary. `PassiveDefinition`/`PassiveRegistry`/`PassiveTickContext` live beside `AppliedPassivesManagementSystem` in `ECS/Scenes/BattleScene/`. The pure math stays put and untouched: `AppliedPassivesService` (`GetGuardAbsorption:19`, `GetPassiveDelta:47`, `GetGalvanizeBonus:13`, `GetPreviewAttackDamage:28`) and `CardStatModifierService` continue to compute damage/absorption; the registry effects *call* nothing new — they publish the same events the branches publish today. Effects that need read-only math still call the services; effects that need writes go through `EventManager`/`EventQueueBridge`, exactly as the god-file already does. **Registration/discovery:** `PassiveRegistry` is a static class with a static ctor that registers all 41 definitions once (mirrors how the existing `Get*Passives()` are static and allocation-cheap-per-call — the registry allocates once). No reflection, no `Content` load, no per-frame cost; `WithTrigger`/`WithScope` return cached, pre-sorted collections. `AppliedPassivesManagementSystem` keeps its existing `EventManager.Subscribe` wiring in its ctor (`:25-33`) and its `public static float Duration` (`:22`); only the *bodies* of the phase branches change to iterate the registry. Because the registry is static and pure-of-side-effects at construction, tests can assert over it with zero ECS setup.

## Testing Strategy

The current passive tests (all confirmed present in `tests/Crusaders30XX.Tests/`) publish a trigger event and assert component deltas: `FearSlowPassiveTests` (publishes `ChangeBattlePhaseEvent{PlayerEnd}` → Slow -1; `EnemyKilledEvent` → Fear -1), `ScarHandlingTests`, `CarpeDiemTests`, `BleedManagementSystemTests` (pure `GetQualifyingSameColorCount`), `SubZeroTests`, `FrostbiteReplacementEffectTests`, `AppliedPassivesServiceGalvanizeTests`, `AppliedPassivesServicePreviewTests`, `RecoilManagementSystemTests`, `VigorManagementSystemTests`. Pattern: bare `EntityManager`, `new AppliedPassivesManagementSystem(em)` (+ the relevant shell system), publish, assert `AppliedPassives.Passives`, reset with `EventManager.Clear()` in ctor/`Dispose`. **Add data-level boundary tests (`PassiveRegistryTests`, no ECS, no systems):**
- **Scope projection equivalence:** `PassiveRegistry.WithScope(PassiveScope.Turn)` equals the frozen old `GetTurnPassives()` set (and the same for the other four) — locks behavior preservation before deleting the HashSets. Assert the union of all scopes covers all 41 enum values (catches the current `Shield`/`Intellect`/`Frozen` gap) and that scopes are disjoint (catches the `Slow`/`Poison` overlap decision).
- **Trigger membership + order as data:** `WithTrigger(StartOfPreBlock)` is exactly `[Stun, Aggression, Power]`; `WithTrigger(StartOfTurn)` places `Inferno` before `Burn` (guards the composite-ordering invariant that is implicit in method position today); `WithTrigger(EndOfTurn)` is `[CarpeDiem]`.
- **Every non-`Unscoped` passive has a defined `Scope`; every effect-bearing trigger has a non-null `Effect`.** **Keep unchanged (the math — this RFC does not touch it):** `AppliedPassivesServiceGalvanizeTests`, `AppliedPassivesServicePreviewTests`, `FrostbiteReplacementEffectTests` (the replacement body is reused as Frostbite's effect), `BleedManagementSystem.GetQualifyingSameColorCount` unit tests (the pure helper moves to a service/stays static but keeps its signature). **Collapse the shell-system tests into trigger tests:** `VigorManagementSystemTests` and `AnathemaManagementSystem`/`Bleed` behavior become assertions on the `OnCardPlayed`/`OnPledge`/`OnConfirmBlocks` registry entries driven through `AppliedPassivesManagementSystem` (publish `CardPlayedEvent`/`PledgeAddedEvent`/`ConfirmBlocksRequested`, assert deltas). Delete the standalone shell-system fixtures for the folded systems; **retain** `RecoilManagementSystemTests` (Recoil is not folded). **Env:** none beyond current xUnit; `[assembly: CollectionBehavior(DisableTestParallelization=true)]` still required (static `EventManager`/`EventQueue`, unchanged by this RFC).

## Implementation Recommendations

**Owns:** the per-passive scope/trigger/order/effect metadata (`PassiveRegistry`); the dispatch loop (iterate-by-trigger, run effect) inside `AppliedPassivesManagementSystem`; the scope projection consumed by expiry, `OnLoadScene` keep-set, and run-long sync.

**Hides:** the 5 HashSets, the phase-branch switch structure, intra-phase ordering, the `Aggression` double-lifetime, the Frostbite threshold branch, the read idioms.

**Exposes:** `PassiveRegistry.Get/WithTrigger/WithScope`, `GetComponentHelper.GetPlayer/GetEnemy`, `Entity.Passive(type)`. **Does NOT own (stays put):** all passive *math* (`AppliedPassivesService`, `CardStatModifierService`, `VigorService.GetWaivedPipCount`, `BleedManagementSystem.GetQualifyingSameColorCount`); the `OnApplyPassive`/`OnRemovePassive`/`OnUpdatePassive` mutation handlers (`:319-469`); `RecoilManagementSystem` and `PlunderManagementSystem` (both real stateful systems); `RunScopedStateService`. **Migration (incremental, behavior-preserving; each step independently shippable):**
1. **Add accessors first** (`GetPlayer`/`GetEnemy`, `Entity.Passive`) and mechanically replace the 42/88 call sites. Pure cleanup, no table dependency; de-risks every later step. Grep-guard the two patterns to zero (excepting the accessor bodies themselves).
2. **Add `PassiveRegistry` populated from the current sets**, with `WithScope` returning identical membership; migrate `GetTurnPassives`/`GetTurnPassivesToDecrement`/`GetBattlePassives `/`GetRunLongPassives`/`GetQuestPassives` to thin `WithScope(...)` forwarders (keep the public static signatures during transition — external callers exist). Land `PassiveRegistryTests` scope-equivalence. Resolve `Slow`/`Poison` scope (dominant scope + document) and classify `Shield`/`Intellect`/`Frozen` as `Unscoped`.
3. **Fold trigger effects one branch at a time**, replacing each phase-branch body with the dispatch loop: `EndOfTurn` (CarpeDiem, smallest) → `StartOfTurn` (Inferno/Burn/Webbing, exercises `Order`) → `StartOfPreBlock` (Aggression/Power; **defer Stun**) → the special-cases (`SubZero`, `Guard` expiry, `Scar`/`Fear`/`Shield` maintenance) → Frostbite `OnApplyThreshold`.
4. **Fold the shell systems** into `OnCardPlayed`/`OnPledge`/`OnConfirmBlocks` trigger entries: Vigor, Anathema, Bleed. Delete each system + its `world.AddSystem` registration in `Game1.cs` only after its trigger test is green. **Do not** fold Recoil or Plunder.
5. **Extract the Stun phase re-enqueue last, coordinated with phase-flow.** The Stun effect (`:277-289`) must stop inline-clearing the queue and hardcoding `EnemyEnd → PlayerStart → Action`; route it through a phase-flow-owned next-phase helper co-located with `EnemyPhaseFlowSystem.ContinueFlow` (`EnemyPhaseFlowSystem.cs:213-221`), which already owns the canonical "enqueue the next phase sequence" logic. The Stun effect publishes an intent ("enemy attack skipped, advance") and phase-flow enqueues the sequence — the passive stops owning phase orchestration.

**Effort:** medium-large (587-line file restructured + ~3 systems removed + ~130 call-site edits).

**Risk:** medium — larger blast radius than the other RFCs because scope classification is read by non-tick consumers (`OnLoadScene`, run-long sync) and because the Stun branch touches phase-flow. **Sequence this after the phase-flow work** so step 5 lands against a stable next-phase helper; steps 1-4 are safe to start immediately and independently. **Authoring ergonomics (preserved/improved):** adding a passive today = add enum value + write a switch branch + add to one of five sets (+ maybe a new one-handler system). After: add enum value + one `PassiveRegistry.Register(...)` literal declaring scope, trigger, and effect together. The `create-card`/`create-medal` authoring flows that reference passives by enum are unaffected; the effect delegate keeps the exact `EventManager.Publish` / `EnqueueTriggerAction` idioms authors already use, so nothing about *writing* an effect changes — only *where* it lives.

## Critical files

- `ECS/Scenes/BattleScene/AppliedPassivesManagementSystem.cs` (god-file: dispatch `:84`, tick methods `:224/:257/:483/:494`, HashSets `:513-585`, special-cases `:131/:183/:196/:215/:368`).
- `ECS/Components/CardComponents.cs` (`AppliedPassives` `:1178`, `AppliedPassiveType` `:1184-1227`, `SubPhase` `:953`).
- `ECS/Scenes/BattleScene/AppliedPassivesService.cs` + `CardStatModifierService.cs` (pure math — unchanged).
- `ECS/Services/GetComponentHelper.cs` (host for `GetPlayer`/`GetEnemy`), `ECS/Services/RunScopedStateService.cs` (run-long sync consumer).
- Shell systems to fold: `AnathemaManagementSystem.cs`, `VigorManagementSystem.cs`, `BleedManagementSystem.cs` (+ `VigorService.cs`). Exclude: `RecoilManagementSystem.cs`, `PlunderManagementSystem.cs`.
- `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs` (`ContinueFlow:213-221` — next-phase helper for the Stun extraction).
- Tests: `tests/Crusaders30XX.Tests/{FearSlowPassiveTests,ScarHandlingTests, CarpeDiemTests,BleedManagementSystemTests,SubZeroTests,FrostbiteReplacementEffectTests,AppliedP assivesServiceGalvanizeTests,AppliedPassivesServicePreviewTests,RecoilManagementSystemTests,VigorManagementSystemTests}.cs`.

## Verification

- `dotnet build` from repo root is clean (per `AGENTS.md:29`).
- `dotnet test tests/Crusaders30XX.Tests` green: the existing passive tests pass **unchanged** through steps 1-4 (behavior-preserving), then the shell-system fixtures for folded systems are deleted and replaced by `PassiveRegistryTests` scope/trigger data tests + the folded trigger tests. The math tests (`AppliedPassivesService*`, `FrostbiteReplacementEffect`, `Bleed` helper) pass throughout.
- Grep-guard: `Passives\.TryGetValue\(AppliedPassiveType\.` and `GetEntitiesWithComponent\(\)\.FirstOrDefault\(\)` return zero outside the new accessors (currently 42 and 88 hits).
- In-app (`dotnet run -- new`): apply **Burn**, **Stun**, **Frostbite**, **Bleed**, and **Vigor** and confirm identical tick/expiry timing across a full turn cycle — Burn ticks at owner `StartOfTurn`; a Stun that consumes the last planned attack still advances `EnemyEnd → PlayerStart → Action` (now via phase-flow); Frostbite fires at its 3-stack threshold; Bleed drains on same-color block confirm; Vigor is consumed on a discard-cost non-weapon play — and that turn-scoped passives (Aggression/Sharpen/Might/Galvani ze/CarpeDiem) clear at turn end while battle/run-long/quest passives persist exactly as before. - --

