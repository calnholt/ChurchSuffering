# WayStation Run-Setup as Component State Plan

> **Superseded:** The difficulty-based design in this draft is replaced by `docs/plans/waystation-penance-v2-plan.md`. Do not implement both models.

> RFC-08 of the deep-module refactor series. The smallest, cleanest standalone win — one file added, one static deleted, ~10 read/write sites repointed. Behavior-preserving. De-risks parallel tests by removing one process-global.

## Document Status

- **Status:** Superseded by Waystation Penance V2.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #08 — in-process; effort/risk small / low.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

Move `WayStationRunSetupSingleton` selection state onto a `RunSetup` component owned by the modal system.

---

## Context

**Why:** `improve-codebase-architecture` deep-module exploration (2026-07-15) flagged `WayStationRunSetupSingleton` as a `public static` mutable slot that carries UI-modal selection state into battle balance. It is the cheapest fix in the series: no new event, no new port, no rendering, no algorithm — just move two enum fields off a process-global onto a component the owning system writes.

**What prompted it:** the singleton is written by a UI modal system and by a test-fight service, then read at *battle-initialization* time — player max HP (`RunPlayerService`) and enemy HP scaling (`EntityFactory`) — far from where it was set. That is exactly the "public static snapshot that other code must query" the coding standards forbid.

**Intended outcome:** delete the singleton; the WayStation modal system writes a `RunSetup` component on a persistent entity; departure and battle-init read that component off the `World`. No hidden global remains; each `World`/test owns its own selection state.

## Problem

`WayStationRunSetupSingleton` (`ECS/Singletons/WayStationRunSetupSingleton.cs:17`) is a `static class` holding process-global class  holding process global mutable selection:

```csharp
public static StartingWeapon SelectedWeapon { get; set; } = StartingWeapon.Sword; //:19 public static RunDifficulty
SelectedDifficulty{ get; set; } = RunDifficulty.Easy; //:20 public static int PlayerMaxHp => /*Easy25, Normal 22, Hard
20 */; //:22-28 public static float EnemyHealthModifier => /*Easy0.8, Normal 0.9, Hard 1.0 */; //:30-36 public static
string WeaponId => /*sword|dagger| hammer */; //:38-44
```

**A UI modal sets battle balance through a hidden global.**
- **Writers:** `WayStationClimbSettingsModalSystem` — `SelectWeapon` (`:678`), `SelectDifficulty` (`:688`), `NormalizeSelection` (`:695`, `:702`), fast-depart reset (`:154-155`); `TestFightSetupService.ApplyRunDi fficulty` (`:160-166`); `WayStationSnapshotFixture` (`:103-104`, `:118`, `:127-128`); `WayStationRunSetupTests` (`:55`, `:72`, `:89`, `:97`, `:109`, `:119`, `:127`, `:192`, `:208`, `:232`, `:253`).
- **Readers, at battle-init distance from any writer:**
- `WayStationRunSetupService.Depart` — `WeaponId` (`:19`), `SelectedDifficulty` (`:21`); `GetSelectedTemperanceId` reads `SelectedWeapon` (`:50`); `ApplySelectedPlayerHp` reads `PlayerMaxHp` (`:43`).
- `EntityFactory.ApplyWayStationEnemyHealthModifier` reads `EnemyHealthModifier` (`:520`), called during enemy creation at `EntityFactory.cs:430` — **enemy HP scaling depends on a static last written by a modal button.**
- `RunPlayerService.EnsureRunPlayer` → `ApplySelectedPlayerHp` (`:21`) sets the player's `HP.Max` from `PlayerMaxHp`; invoked from `BattleSceneSystem.cs:497`.
- `TestFightSetupService` reads `PlayerMaxHp` (`:40`, `:144`); modal highlight `IsSelected` reads both fields (`:667`, `:672`); `:282` reads `SelectedWeapon`. **Contradicts the documented rule.** `docs/coding-standards.md:13`:

> Keep systems self-contained. Encode state on components that the owning system writes, not public static snapshots that other code must query. And the adjacent services rule (`:14`) is already broken by `TestFightSetupService.ApplyRunDi fficulty` (`:160-166`), which *writes* singleton state from a service:

> Services are read-only helpers/calculators. They must not mutate ECS components, publish/enqueue events, or change singleton state. Route game-state writes through systems via events. **Order-coupled shared-global tests.** `WayStationRunSetupTests` mutates the static in one path and depends on `finally` blocks to reset it (`:72`, `:97`, `:119`, `:127`) so the next `[Theory]` case isn't poisoned. The same static is shared with `WayStationSnapshotFixture` (`:103-128`) and `TestFightSetupService` (`:160`). This shared global is one of the reasons the suite is pinned serial (`tests/Crusaders30XX.Tests/TestAssembly.cs:3` — `DisableTestParallelization = true`): a parallel run would let two cases race on `SelectedDifficulty`.

## Proposed Interface

A `RunSetup` component (data container) written by the modal system, read off the `World` by departure and battle-init. The difficulty/weapon → balance mapping moves verbatim onto the component as pure computed properties (identical bodies to the deleted singleton, now instance state).

```csharp
//ECS/Components/RunSetup.cs—livesona DontDestroyOnLoad "RunSetup" entityso it survives WayStation -> Battle public
class RunSetup : IComponent { public Entity Owner { get; set; } public StartingWeapon SelectedWeapon {get; set; } =
StartingWeapon.Sword; public RunDifficulty SelectedDifficulty { get; set; } = RunDifficulty.Easy; public int
PlayerMaxHp => SelectedDifficulty switch { RunDifficulty.Easy => 25, RunDifficulty.Normal => 22, RunDifficulty.Hard =>
20, _=>22}; public float EnemyHealthModifier => SelectedDifficulty switch {RunDifficulty.Easy => 0.8f,
RunDifficulty.Normal => 0.9f, RunDifficulty.Hard => 1.0f, _=>0.9f}; public string WeaponId => SelectedWeapon switch
{StartingWeapon.Sword => "sword", StartingWeapon.Dagger => "dagger", StartingWeapon.Hammer => "hammer", _=>"sword"}; }
//Smallensure/readhelper, mirroring the existing RunPlayerService.EnsureRunPlayer
/RunDeckService.EnsureRunDeckpattern.//Reader for all consumers; creator only whereno writerhas runyet (test-fight
/directDepart). public static class RunSetupService { public static RunSetup GetRunSetup(EntityManagerem);
//ensurestheDontDestroyOnLoad"RunSetup" entity + component }
```

- **Writer (owning system):** `WayStationClimbSettingsModalSystem` gets `RunSetupService.GetRunSetup(EntityManager)` and sets `.SelectedWeapon` / `.SelectedDifficulty` in `SelectWeapon`/`SelectDifficulty`/ `NormalizeSelection`/fast-depart. `IsSelected` reads the same component. The `static` helper methods (`:665-704`) become instance methods (they need the `EntityManager`), or take the resolved `RunSetup`.
- **Readers:** `WayStationRunSetupService.Depart(World)`, `RunPlayerService.EnsureRunPlayer(World)`, and `EntityFactory` (enemy scaling) each resolve the component from the `World`/`EntityManager` they already hold. `TestFightSetupService` writes the component via the same helper instead of the static. **Before/after — `WayStationRunSetupService.Depart` (`:11-25`, `:38-46`, `:48-51`):**

```csharp
//BEFORE—readsthree properties offa process-global static public static void Depart(World world) { if (world == null)
return; RunDeckService.DestroyRunDeck(world.EntityManager); RunPlayerService.DestroyRunPlayer (world.EntityManager);
SaveCache.StartWayStationClimbAttempt(); SaveCache.ConfigurePrimaryRunSetup(WayStationRunSetupSingleton.WeaponId,
GetSelectedTemperanceId(),
//readsWayStationRunSetupSingleton.SelectedWeaponWayStationRunSetupSingleton.SelectedDifficulty);
PrepareRunEntities(world); EventManager.Publish(new ShowTransition { Scene = SceneId.Climb, SkipHold = true }); }
public static void ApplySelectedPlayerHp(Entity player) { var hp = player?.GetComponent(); if (hp == null) return;
hp.Max = WayStationRunSetupSingleton.PlayerMaxHp; //hiddenglobalhp.UnscarredMax=hp.Max; hp.Current = hp.Max; }
```

```csharp
//AFTER—readstheRunSetup component offthe World; no static anywhere public static void Depart(World world) { if (world
== null) return; var setup = RunSetupService.GetRunSetup(world.EntityManager);
RunDeckService.DestroyRunDeck(world.EntityManager); RunPlayerService.DestroyRunPlayer (world.EntityManager);
SaveCache.StartWayStationClimbAttempt(); SaveCache.ConfigurePrimaryRunSetup( setup.WeaponId,
StartingDeckGeneratorService.Get DefaultTemperanceId(setup.SelectedWeapon), setup.SelectedDifficulty);
PrepareRunEntities(world); EventManager.Publish(new ShowTransition { Scene = SceneId.Climb, SkipHold = true }); }
public static void ApplySelectedPlayerHp(Entity player, RunSetup setup) //callerpassestheresolvedcomponent {varhp =
player?.GetComponent(); if (hp == null || setup == null) return; hp.Max = setup.PlayerMaxHp; hp.UnscarredMax = hp.Max;
hp.Current = hp.Max; }
```

And enemy scaling — `EntityFactory.ApplyWayStationEnemyHealthModifier` (`:517-526`) takes the modifier from the caller (which holds the `EntityManager`) instead of reading the static:

```csharp
//BEFORE:floatmodifier= WayStationRunSetupSingleton.EnemyHealthModifier; //:520//AFTER:passed in — float modifier =
RunSetupService.GetRunSetup(entityManager).EnemyHealthModifier; (resolved at :430 callsite) private static void
ApplyWayStationEnemyHealthModifier(EnemyBasedef, float modifier) {...}
```

**What it removes:** the `WayStationRunSetupSingleton` static class in full — the only hidden global on this path. Selection state becomes `World`-scoped: two worlds (a real second run, two parallel test cases) can never clobber each other's difficulty/weapon. **Rejected alternative — keep the singleton but make it `[ThreadStatic]`/`AsyncLocal` per test.** Isolates test cases but re-hides the world reference as ambient spooky state, still contradicts `coding-standards.md:13` (it's still a static, not component state the owning system writes), and does nothing for the "which run?" question in-app. A component keyed to the `World` fixes both isolation and ownership with one type. (Also rejected: thread `weapon`/`difficulty` as parameters through the whole enemy-creation path — `EnemyHealthModifier` is read deep inside `EntityFactory`, far more churn than a single component read.)

## Dependency Strategy

In-process; no new boundary, no event, no assembly. The `RunSetup` component is written exclusively by the owning `WayStationClimbSettingsModalSystem` (and, for the test-fight path, via `RunSetupService.GetRunSetup` mirroring existing `Ensure*` service helpers). It is read by:
- **departure** — `WayStationRunSetupService.Depart` / `RunPlayerService.EnsureRunPlayer`;
- **battle init** — `EntityFactory` enemy HP scaling (`:430`/`:517`) and player `HP.Max` (`RunPlayerService.EnsureRunPlayer:21`). The component lives on a `DontDestroyOnLoad` "RunSetup" entity (same persistence mechanism `TestFightSetupService` uses for `Deck`/`QueuedEvents`, `:26-27`/`:49-50`) so the selection survives the WayStation → Climb → Battle transition.

## Testing Strategy

- **Tests set the component, not a static.** `WayStationRunSetupTests` replaces `WayStationRunSetupSingleton.SelectedDifficulty = X` with `RunSetupService.GetRunSetup(world.EntityManager).SelectedDifficulty = X` (and weapon likewise). Because each test builds its own `World`, the selection is `World`-scoped — no shared global, so the `finally` resets (`:72`, `:97`, `:119`, `:127`) are **deleted**.
- **Order-independent / parallel-safe.** Removing this shared static eliminates one of the cross-test coupling points behind `TestAssembly.cs:3`. (Full parallelization still blocks on the static `EventManager`/`EventQueue`; this RFC removes one blocker and does not by itself flip `DisableTestParallelization`.)
- **`Difficulty_maps_to_player_hp_and_enemy_health_modifier` (`:183-196`)** becomes a pure test on a `new RunSetup { SelectedDifficulty = X }`'s `PlayerMaxHp`/`EnemyHealthModifier` — zero global mutation, no reset needed.
- **`Enemy_factory_scales_health_for_selected_run_difficulty` (`:198-220`)** and the diagnostic HP test (`:239-305`) set `RunSetup` on their `world` and assert the same expected HP values; behavior is unchanged.
- **`WayStationSnapshotFixture` (`:103-128`)** writes the fixture's `World` `RunSetup` component instead of the static; baselines are unchanged (same selection → same render).
- **Old static-mutating test setup is replaced, not layered** — no test should reference `WayStationRunSetupSingleton` after migration (grep-guard: zero hits).

## Implementation Recommendations

**Owns:** the run's weapon/difficulty selection as `World`-scoped state, plus the pure difficulty/weapon → HP/modifier/id derivations (moved intact from the singleton).

**Hides:** nothing new — it *removes* hidden global state; the derivation tables live with the two fields they depend on.

**Exposes:** `RunSetup` component fields + computed getters, and `RunSetupService.GetRunSetup(EntityManager)` as the single read/ensure entry point. **Migration (mechanical, behavior-preserving):**
1. Add `ECS/Components/RunSetup.cs` (fields + computed getters copied from the singleton) and `RunSetupService.GetRunSetup` (ensure DontDestroyOnLoad entity).
2. Repoint `WayStationClimbSettingsModalSystem` writers/readers (`:154-155`, `:282`, `:667-704`) to the component; the `static` selection helpers become instance methods (they now need `EntityManager`).
3. Read at `Depart` (`:19-21`, `:50`) and `ApplySelectedPlayerHp` (`:43`); thread `RunSetup` from `RunPlayerService.EnsureRunPlayer` (`:21`).
4. Read at battle init: pass `EnemyHealthModifier` into `EntityFactory.ApplyWayStationEnemyHealthModifier` from the `:430` call site.
5. Repoint `TestFightSetupService` (`:40`, `:144`, `:160-166`) to write/read the component.
6. Update `WayStationSnapshotFixture` (`:103-128`) and `WayStationRunSetupTests` to set the component; delete the `finally` static-resets.
7. **Delete `ECS/Singletons/WayStationRunSetupSingleton.cs`** (keep the `StartingWeapon`/`RunDifficulty` enums — relocate them to the component file or a shared enums file; they're referenced widely).

**Effort:** small (1 file added + 1 helper; ~10 sites repointed; 1 static deleted).

**Risk:** low — behavior-preserving; the derivation math is copied verbatim and the default (`Sword`/`Easy`) matches the singleton's initializers. **Relation to a broader run-session RFC:** run lifecycle currently lives in `RunLifecycleService` (`ECS/Services/RunLifecycleService.cs`), a stateless service — there is no `RunSession`/`RunState` *component* today. If a future RFC introduces a persistent run-session component, `SelectedWeapon`/`SelectedDifficulty` are natural fields on it and `RunSetup` should fold in there. This RFC does not depend on that: it is the minimal, self-contained fix and ships alone.

## Critical files

- `ECS/Singletons/WayStationRunSetupSingleton.cs` (deleted; enums relocated)
- `ECS/Scenes/WayStationScene/WayStationClimbSettingsModalSystem.cs` (owning writer: `:154-155`, `:282`, `:665-704`)
- `ECS/Services/WayStationRunSetupService.cs` (`Depart:11`, `ApplySelectedPlayerHp:38`, `GetSelectedTemperanceId:48`)
- `ECS/Services/RunPlayerService.cs ` (`EnsureRunPlayer:10`, `:21`)
- `ECS/Factories/EntityFactory.cs` (enemy scaling `:430`, `:517-526`)
- `ECS/Services/TestFightSetupService.cs` (`:40`, `:144`, `:160-166`)
- `ECS/Diagnostics/Snapshots/Fixtures/WayStationSnapshotFixture.cs` (`:103-128`)
- `tests/Crusaders30XX.Tests/WayStationRunSetupTests.cs`
- `docs/coding-standards.md` (`:13`, `:14`)

## Verification

- `dotnet build` from repo root — clean.
- `dotnet test tests/Crusaders30XX.Tests` — green; `WayStationRunSetupTests` pass with the component set on each test's `World` and **no** static mutation or `finally` reset (grep-guard: zero references to `WayStationRunSetupSingleton` in `ECS/` and `tests/`).
- `WayStationSnapshotFixture` baselines unchanged (`--verify`).
- In-app (`dotnet run`): open the WayStation, pick a weapon + difficulty in the climb-settings modal, depart, and confirm the resulting battle has the correct player `HP.Max` (Easy 25 / Normal 22 / Hard 20) and correctly scaled enemy HP (Easy 0.8 / Normal 0.9 / Hard 1.0). Then a test-fight run (`TestFightSetupService`) applies the same HP/modifier through the component.
