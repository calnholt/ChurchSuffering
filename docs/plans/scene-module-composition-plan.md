# Scene-Module Composition + Unified Render List Plan

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #07 — in-process; effort/risk medium-large / medium.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** None required.

One `SceneModule` owns compose/activate/deactivate/cleanup and declares update-phase + draw position in a single ordered list.

---

## Context

**Why:** An `improve-codebase-architecture` deep-module exploration (Ousterhout lens; 2026-07-15) found the scene layer to be three shallow copies of one non-trivial pattern, plus a split-brain ordering hazard that has no compile-time or test-time guard. Crusaders30XX has three "scene systems" that each independently reimplement compose / activate / deactivate / cleanup, and every display system is enrolled in **two** hand-maintained ordered lists — one for update, one for draw — that are unrelated and free to drift.

**What prompted it:** the same exploration that produced `docs/plans/deep-module-refactor-round-2-plan.md` (RFC #1–#5). This RFC is a sibling in the `docs/plans/` series and is independently orchestrable.

**Intended outcome:** a `SceneModule` abstraction that owns the compose/activate/deactivate/clean up lifecycle **once**, and — the load-bearing part — a **single ordered render list** in which a system's update-phase membership and its draw position are declared together. Adding a display system becomes a one-line edit in one place, boundary-testable ("entering scene X registers exactly these systems and cleans exactly these entities"), instead of today's three-edit ritual with manually reconciled ordering. **Cross-reference — RFC-03 (scene navigator):** RFC-03 owns scene **transitions and lifecycle** (who publishes `PrepareSceneEvent`/`LoadSceneEvent`/`DeleteCachesEvent`/`TransitionCompleteEvent`, and when). THIS RFC owns **per-scene system composition and draw ordering** — what a scene is made of and the order it renders. The two meet at the event seam: RFC-03 fires the lifecycle events; a `SceneModule` (this RFC) subscribes and reacts. Do not duplicate the transition state machine here; a `SceneModule` is a passive recipient of lifecycle events.

## Problem

### 1. Three scenes, one copy-pasted shape (with divergence)

The same skeleton is reimplemented three times, and the three copies have already diverged in the parts that matter (activation bookkeeping, cleanup):
- **`ECS/Scenes/BattleScene/BattleSceneSystem.cs`** (1086 lines). Ctor `:191` takes `SystemManagersm` + `World world` to self-register children. Guard field `_loadedSystems` (`:32`). `AddBattleSystems()` (`:731-1043`) news up ~130 child systems as private fields (`:39-188`) then `_world.AddSystem`s each. `PrepareBattleSystems()` (`:1045-1052`) captures which systems it added via a **set-difference** against `_systemManager.GetAllSystems()` (`:1048-1050`) into `_battleSystems` (`:34`). `SetBattleSystemsActive(bool)` (`:1054-1060`) fans out over that captured list. `Draw()` (`:317-477`) `FrameProfiler.Measure`-wraps each child. `DeleteCachesEvent` handler (`:281-288`) just resets `_loadedEntities` + deactivates — no entity cleanup.
- **`ECS/Scenes/ClimbScene/ClimbSceneSystem.cs`**. Guard field `_firstLoad` (`:23`). `AddClimbSystems()` (`:90-113`), `SetClimbSystemsActive(bool)` (`:121-131`) hand-enumerates each field, `RemoveClimbSystems()` (`:115-119`), `Draw()` (`:133-142`). Ctor `:35` receives `SystemManager` but **never uses it**. Cleanup is `DeactivateClimbUiEntities()` (`:187-227`) which **hand-enumerates ~11 climb marker component types** (`:193-204`) — even though `OwnedByScene` (`ECS/Components/Scenes.cs:176-180`) already exists as the intended uniform cleanup marker and is used for exactly one entity here (`:167`).
- **`ECS/Scenes/AchievementScene/AchievementSceneSystem.cs`**. Guard field `_firstLoad` (`:24`), `AddAchievementSystems()` (`:89-126`), `SetAchievementSystemsActive(bool)` (`:128-138`), `Draw()` (`:71-87`). Its `DeleteCachesEvent` handler (`:58-61`) only calls `SetAchievementSystemsActive(false)` — it performs **no entity cleanup at all**, unlike Climb. So the three scenes answer "how do I compose, toggle, and tear down a scene?" three different ways, and the divergence (Battle set-difference vs Climb/Achievement hand-fan-out; Climb marker-enumeration vs Achievement no-cleanup) is exactly the surface a single owner would collapse.

### 2. Split-brain ordering — no compile check that the two lists agree

`Core.System` (`ECS/Core/System.cs`) has **only `Update`; there is no `Draw` on the base class** (`:23-32`). Draw is an ad-hoc per-system convention (`Draw`, `DrawHand`, `DrawBackdrop`/`DrawForeground `, `DrawAlpha`, `Draw(bool)`, `Draw(showDrawPile, showDiscardPile)`). Consequently every display system is enrolled in **two independently hand-maintained ordered lists**:
- **Update** via `_world.AddSystem(x, phase)` — phase buckets `Input /Interaction/Gameplay/Presentation` (`ECS/Core/SystemManager.cs:8-14`, dict `:22-28`, append-in-order `:40-46`), plus `AddLateSystem` (`:52-58`). Update order within a phase = call order in `AddBattleSystems` (`:896-1042`).
- **Draw** via explicit calls in `Draw()` (`BattleSceneSystem.cs:414-476`). The two orders are **unrelated** and nothing checks they agree. Concrete: `_passiveApplicationAnimationDisplaySystem` is update-registered in the `Presentation` phase (`:970`) but drawn mid-list in the flat fan-out (`:428`). Adding one display system therefore requires **three coordinated edits with correct manual ordering**: (1) field declaration (`:39-188`), (2) construct + `AddSystem` in `AddBattleSystems` (`:731-1043`), (3) a `FrameProfiler.Measure` draw call at the right position in `Draw()` (`:317-477`). Miss the third and the system updates but never renders; get its position wrong and it renders behind/in-front of the wrong layer — with no test to catch either.

### 3. `Game1.DrawScene` hard-codes the fan-out

`Game1.DrawScene` (`Game1.cs:573-670`) hard-codes each scene's draw entry behind a per-scene `switch` (`:600-640`): Battle (`:618-628`, incl. the additive-trail pass `:622-627` calling `_battleSceneSystem.DrawAdditive()`), Climb (`:632`), Achievement (`:637`), plus an inline **WayStation fan-out** of six loose systems (`:610-615`) that isn't wrapped in a scene system at all. The scene modules are constructed (`Game1.cs:215-217`) and registered (`:282-284`) by hand. A scene cannot describe its own draw list; `Game1` must know it.

### 4. The "no system-into-system" rule, skirted

`AGENTS.md:38` / `docs/coding-standards.md:37-38`: *"Never pass another `System` as a constructor parameter to a system. Systems must not hold direct references to other systems."* `BattleSceneSystem` does not literally break the ctor rule — it receives `SystemManager`/`World` (`:191`), not sibling `System`s — but it then **holds direct field references to ~130 sibling systems** (`:39-188`) and calls `.Draw()` on each. The intended-but-informal posture ("compose via the `World`/`SystemManager` handle, don't inject siblings") is real and correct; it is just unowned and unenforced. A `SceneModule` should formalize exactly this posture: it *creates* its children and holds them only inside its own render list, and never receives a sibling `System` as a ctor parameter.

### 5. Untested Scene enter/exit (systems registered exactly once, correctly toggled active, entities cleaned)

is **untested**. The only scene-adjacent tests are `BattleClimbPackageTests` (`tests/Crusaders30XX.Tests/BattleClimbPackageTests.cs`), which exercise the `internal static` helpers extracted out of `BattleSceneSystem` — `IsFirstQueuedClimbEncounter` (`BattleSceneSystem.cs:622`, test `:33`), `ApplyPendingClimbBattlePackage ` (`:633`, test `:34/48/64/83`), `ResolveBattleLocationForLoad` (`:627`, test `:97/106-107`) — and nothing about composition, toggling, or cleanup. Confirmed.

## Proposed Interface

A `SceneModule` base class owns the lifecycle once; a y; `SceneSystemEntry` list is the **single source of truth** that declares update-phase membership and draw position together. The heterogeneous draw signatures are captured as an `Action` per entry (there is no polymorphic `Draw` to lean on), so no existing system needs its method shape changed.

```csharp
namespace Crusaders30XX.ECS.Scenes { //MirrorsSystemManagerphases+ thelate bucket (SystemManager.cs:8-14, :52-58).
public enum RenderPhase { Input, Interaction, Gameplay, Presentation, Late } //ONEentry=one system.Its update-phase
membership ANDitsdraw position //(listorder) livehere, together, in one place.Killing the split-brain.public sealed
class SceneSystemEntry { public Core.System System { get; init; } //createdbythemodule; never injected as a sibling
public RenderPhase Phase { get; init; } = RenderPhase.Gameplay; public Action Draw { get; init; } // null =>
update-only (management/layout/latesystems) public Func DrawWhen { get; init; } // null => always; e.g.() =>
showDrawPile public string ProfileLabel { get; init; } //FrameProfiler.Measurelabel; defaults to Systemtype name }
public abstract class SceneModule { protected readonly World World; protected EntityManager Em => World.EntityManager;
protected SceneModule(World world) { World = world; } public abstract SceneId Scene { get; } //Buildsthechildsystems
andthe SINGLE ordered list.Calledonce, lazily.//Listorder== draw order (back-to-front).Each entryalso declares its
update phase.protected abstract IReadOnlyList Compose(); //Graphics-freeprojectionofthelist for headless assertions
(no GraphicsDevice needed): //(systemCLRtype, update phase, whether it draws).Derived fromthe same entries.public
IReadOnlyList<(Type Type, RenderPhase Phase, bool Draws)> Manifest { get; } public void Prepare();
//idempotent:composeonce, register each entryinto its phase (inactive) public void SetActive(bool);
//fan-outoverthemodule'sownentries only public void Cleanup(); //deactivate+destroyentities carrying OwnedByScene {
Scene = this.Scene } public void Draw(); //iterateentries in list order; invoke Draw where DrawWhen() is true
//Optionalbespokepasses for scenes whose rendering isn'ta flatfan-out: protected virtual void DrawBackgroundPass() { }
//Battle'sRT-compositingbracket protected virtual void DrawAdditivePass() { } //Battle'sadditivecard-trailpass} }
```

**Usage — Achievement (smallest, 8 systems), replacing `AchievementSceneSystem.cs:89-138` + `:71-87`.** Update membership and draw order foreach system are declared once, in one place, in draw order:

```csharp
public sealed class AchievementSceneModule : SceneModule { private readonlyGraphicsDevice_gd; private
readonlySpriteBatch_sb; private readonlyContentManager_content; public AchievementSceneModule(Worldw,
GraphicsDevicegd, SpriteBatchsb, ContentManagerc) : base(w) {_gd=gd; _sb=sb; _content=c; } public override SceneId
Scene => SceneId.Achievement; protected override IReadOnlyList Compose() {varbg = new
AchievementBackgroundDisplaySystem(Em, _gd, _sb, _content); var grid = new AchievementGridDisplaySystem(Em, _gd, _sb);
var desc = new AchievementDescriptionDisplaySystem(Em, _gd, _sb); var meter= new AchievementMeterDisplaySystem(Em,
_gd, _sb); var title= new AchievementTitleDisplaySystem(Em, _gd, _sb); var back = new
AchievementBackButtonDisplaySystem(Em, _gd, _sb); var boom = new AchievementExplosionSystem(Em);
//update-only:noDrawentry var conf = new AchievementConfettiDisplaySystem (Em, _gd, _sb); return new[] { new
SceneSystemEntry { System = bg, Draw = () => { if (ShaderRuntimeOptions.ShadersEn abled) bg.Draw(); } }, new
SceneSystemEntry { System = grid, Draw = grid.Draw }, new SceneSystemEntry { System = desc, Draw = desc.Draw }, new
SceneSystemEntry { System = meter, Draw = meter.Draw }, new SceneSystemEntry { System = title, Draw = title.Draw },
new SceneSystemEntry { System = back, Draw = back.Draw }, new SceneSystemEntry { System = boom }, //updates, never
drawn — explicit new SceneSystemEntry {System = conf, Draw = conf.Draw },}; } }
```

The base's `Prepare()` walks the list in order calling `World.AddSystem(entry.System, mapPhase(entry.Phase))` (or `AddLateSystem` for `Late`), guarded so a second call is a no-op (replacing `_firstLoad`/`_loadedSystems` + the `_battleSystems` set-difference `:1048-1050`). `Draw()` iterates the same list: `if (e.Draw != null && (e.DrawWhen?.Invoke() ?? true)) FrameProfiler.Measure(e.ProfileLabel ?? e.System.GetType().Name + ".Draw", e.Draw);`. `SetActive` toggles exactly the module's entries. `Cleanup()` destroys entities carrying `OwnedByScene {Scene = this.Scene }` (`Scenes.cs:176-180`), replacing `DeactivateClimbUiEntities`'s 11-marker enumeration (`ClimbSceneSystem.cs:193-204`). **What it hides:** `SystemManager` registration order and phase mapping; the `FrameProfiler.Measure` wrapping; the compose-once guard bookkeeping (`_loadedSystems`/`_firstLoad`/`_battleSystems`); the per-scene entity-marker enumeration (now `OwnedByScene`-driven); conditional-draw guards (folded into `DrawWhen`); the activation fan-out. **What it exposes:** `Prepare` / `SetActive` / `Cleanup` / `Draw` / `Scene` / `Manifest`. **Battle's non-flat rendering** stays honest: the RT-compositing background bracket (`BattleSceneSystem.cs:317-403`, `_bgRt`/`_bgTemp` ping-pong, bloodshot composite) and the additive-trail pass (`:1078-1082`) do not fit a flat entry list, so `BattleSceneModule` overrides `DrawBackgroundPass()`/`DrawAdditivePass()` with that bespoke code, while everything from `:414-476` (the ~60-call flat fan-out) becomes the single ordered list. `Draw()` runs `DrawBackgroundPass()`, then the list, then `Game1` triggers `DrawAdditivePass()` for the additive `SpriteBatch` state. **Rejected alternatives:**
1. **Data-driven render-graph** (declare passes + dependencies in data, topologically sort). Rejected: there are no branching dependencies — a scene's draw is a *total order*, not a graph. A graph adds an execution model and a serialization surface absent from this synchronous, code-composed codebase, and can't express the heterogeneous, stateful draw signatures (`DrawBackdrop` then `DrawForeground` within one system, `Draw(bool)`, conditional draws) without reflection. The `SceneSystemEntry` `Action` captures those signatures with zero churn.
2. **Give `Core.System` a virtual `Draw(SpriteBatch)` and iterate polymorphically.** Rejected: draw is genuinely heterogeneous and order-interleaved (`PayCostOverlaySystem.DrawBackdrop` renders early and `.DrawForeground` late — two calls straddling ~20 other systems, `:445`/`:468`); a uniform `Draw` would force splitting many systems and still couldn't express backdrop/foreground interleaving or the RT bracket. Keeping draw as a per-entry `Action` preserves every existing method shape.

## Dependency Strategy

In-process; no new package/assembly/network boundary. A `SceneModule` composes strictly through the `World`/`SystemManager` handle it is given: it **creates** its own child systems and holds them only inside its `SceneSystemEntry` list, registering them via `World.AddSystem`/`AddLateSystem` (`World.cs:79-90`, `SystemManager.cs:40-58`). It never receives a sibling `System` as a ctor parameter — satisfying `AGENTS.md:38` / `coding-standards.md:37-38`, and formalizing the posture `BattleSceneSystem` already takes at `:191`. Cross-system behavior stays on events/components as today; the module owns only *wiring and draw order*, not gameplay. `Game1.DrawScene` delegation: a small `SceneModuleRegistry` (map `SceneId -> SceneModule`, populated where the modules are constructed at `Game1.cs:215-217`) lets the per-scene `switch` (`Game1.cs:600-640`) collapse to `_sceneModules.Active(scene.Current)?.Draw();`. The Battle additive pass moves behind `DrawAdditivePass()` (still driven by `Game1`'s additive `SpriteBatch.Begin/End` at `:622-627`). The scene-independent global overlay fan-out (`:641-669`) is unchanged. WayStation's loose six-system fan-out (`:610-615`) is **out of scope** for the first pass and is the natural next port target once the pattern lands.

## Testing Strategy

The whole point is boundary tests, and they run **without a GraphicsDevice**, matching the existing test posture (bare `EntityManager`/`SystemManager`, xUnit, no mocking framework; `docs/plans/deep-module-refactor-round-2-plan.md:15`).
- **Ordering / split-brain guard (headless via `Manifest`):** assert each module's `Manifest` equals the expected ordered list of `(Type, RenderPhase, Draws)`; assert draw order equals the draw-enabled subsequence in list order. This is the compile/test-time check the codebase lacks today: a system update-registered but with no draw entry (or drawn but not registered) is now visible and assertable, instead of the silent `_passiveApplicationAnimationDisplaySystem` update-vs-draw divergence (`:970` vs `:428`). `Manifest` is pure metadata derived from the entries, so no live `GraphicsDevice` is required to read it.
- **Registers exactly these systems, once:** run `module.Prepare()` against a real `SystemManager`; assert `GetAllSystems()` contains exactly the manifest's systems in exactly the declared phases (phase membership checked via the phase buckets); assert idempotency — a second `Prepare()` adds nothing (locks the `_loadedSystems`/`_firstLoad` guard semantics).
- **Toggles active correctly:** `SetActive(true)`/`SetActive(false)` flip `IsActive` on exactly the module's systems and no others sharing the manager (contrast with `SystemManager.SetAllSystemsActive` `:135-145`, which hits everything).
- **Cleans exactly these entities:** seed a bare `EntityManager` with some entities carrying `OwnedByScene { Scene = X }` and some without; `Cleanup()` destroys/deactivates exactly the `X`-owned ones and leaves the rest — proving the marker-driven cleanup subsumes `DeactivateClimbUiEntities`'s hand-enumeration (`ClimbSceneSystem.cs:187-227`) and closes Achievement's missing cleanup (`AchievementSceneSystem.cs:58-61`). **Environment needs:** none beyond current xUnit — no `GraphicsDevice`, `SpriteBatch`, or `ImageAssetService`. Where a module's `Compose()` constructs graphics-bound systems, ordering assertions use `Manifest` (metadata only); registration/activation/cleanup assertions run against `SystemManager`/`EntityManager` directly. Visual parity is covered separately by the display-snapshot fixtures below.

## Implementation Recommendations

**Owns:** the compose-once lifecycle guard; register-into-phase; the activate fan-out; `OwnedByScene`-driven cleanup; and the single ordered render list where update-phase membership and draw position are declared together.

**Hides:** `SystemManager` registration order + phase mapping, `FrameProfiler.Measure` wrapping, guard bookkeeping (`_loadedSystems`/`_firstLoad`/`_battleSystems` set-difference `:1048-1050`), per-scene marker enumeration, conditional-draw guards.

**Exposes:** `Prepare` / `SetActive` / `Cleanup` / `Draw` / `Scene` / `Manifest` (+ optional `DrawBackgroundPass`/`DrawAdditivePass` overrides). **Migration (smallest first, each shippable):**
1. Add `SceneModule` + `SceneSystemEntry` + `RenderPhase` + `SceneModuleRegistry` (new files under `ECS/Scenes/`); no wiring yet.
2. **Port Achievement** (`AchievementSceneSystem.cs`, 8 systems `:89-138`) — the smallest, and the one currently *missing* entity cleanup, so it gains correctness for free. Wire its module into the registry; route `Game1.DrawScene`'s Achievement case (`:637`) through it.
3. **Port Climb** (`ClimbSceneSystem.cs:90-142`), folding `DeactivateClimbUiEntities` (`:187-227`) into `OwnedByScene`-driven `Cleanup()` (tag the ~11 marker-bearing entities with `OwnedByScene { Scene = Climb }` at creation, or make `Cleanup()` cover both during transition). Keep `DrawBackgroundOnly` (`:144-150`) as a bespoke pass override for the background-only dialogue case (`Game1.cs:591-597`).
4. **Port Battle** (`BattleSceneSystem.cs:731-1043` -> one entry list; `:317-403` -> `DrawBackgroundPass()`; `:1078-1082` -> `DrawAdditivePass()`). This is where the ~130-system list gets reconciled.
5. **Unify each scene's render list** — reconcile update-phase membership and draw position into the single declared list (the split-brain fix). Then simplify `Game1.DrawScene` (`:600-640`) to `registry.Active(scene.Current)?.Draw()`. **The split-brain fix (step 5) is the highest-value slice** — it is what removes the ability for update and draw order to silently drift and kills the three-edit ritual; the compose/lifecycle deduplication (steps 1–4) is the enabling mechanism.

**Effort:** medium-large but overwhelmingly mechanical (Battle's list is long, not hard).

**Risk:** medium — the real hazard is draw-order regressions, because unifying the two lists into one declared order means reconciling the current (divergent) update-within-phase order and draw order into a single order. This is gated by the existing headless display-snapshot fixtures in `ECS/Diagnostics/Snapshots/Fixtures/` (e.g. `AchievementSnapshotFixture`, `ClimbSnapshotFixture`, `ClimbHeaderSnapshotFixture`, `PlayerHudSnapshotFixture`, `EnemyDamageMeterSnapshotFixture`, `AssignedBlockRailSnapshotFixture `): baselines must remain pixel-identical after migration. Author each module's list to reproduce the current *draw* order exactly; treat any update-within-phase reordering as behavior-neutral only where the gameplay tests stay green.

## Critical files

- `ECS/Scenes/BattleScene/BattleSceneSystem.cs` (fields `:39-188`; ctor `:191`; `Draw` `:317-477`; `AddBattleSystems` `:731-1043`; `PrepareBattleSystems` set-difference `:1045-1052`; `SetBattleSystemsActive` `:1054-1060`; `DrawAdditive` `:1078-1082`)
- `ECS/Scenes/ClimbScene/ClimbSceneSystem.cs` (`:23`, `:90-142`; `DeactivateClimbUiEntities` `:187-227`)
- `ECS/Scenes/AchievementScene/AchievementSceneSystem.cs` (`:24`, `:71-138`)
- `ECS/Core/SystemManager.cs` (phases `:8-14`; `AddSystem` `:40-46`; `AddLateSystem` `:52-58`; `SetAllSystemsActive` `:135-145`) and `ECS/Core/System.cs` (no base `Draw`, `:23-32`)
- `Game1.cs` (`DrawScene` `:573-670`; scene-module construction `:215-217`, registration `:282-284`)
- `ECS/Components/Scenes.cs` (`OwnedByScene` `:176-180`, `SceneId` `:14-23`)
- `tests/Crusaders30XX.Tests/BattleClimbPackageTests.cs` (existing coverage boundary)
- New: `ECS/Scenes/SceneModule.cs`, `ECS/Scenes/SceneSystemEntry.cs `, `ECS/Scenes/SceneModuleRegistry.cs`

## Verification

- `dotnet build` from the repo root is clean.
- `dotnet test tests/Crusaders30XX.Tests` is green, including the new boundary tests (Manifest ordering, register-exactly-once + idempotent, toggle-active isolation, `OwnedByScene` cleanup) and the existing `BattleClimbPackageTests`.
- All display-snapshot fixtures in `ECS/Diagnostics/Snapshots/Fixtures/` produce **unchanged** baselines after migration (`--verify`); no baseline PNGs edited — draw parity is proved against the new module path.
- In-app (`dotnet run`): each ported scene (Achievement, Climb, Battle) renders identically to pre-migration; entering and exiting each scene registers its systems exactly once and leaks no entities (verify via the entity-list overlay that `OwnedByScene` entities for the departed scene are gone).

---

