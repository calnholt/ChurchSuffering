# ECS-050 cutover rehearsal

Status: cutover complete. The original rehearsal findings remain below as the historical checklist
that drove the integration. ECS-046 passed, the root event/runtime conflicts were closed, and
`Game1.cs` now constructs only `DataOrientedGameRuntime` plus external MonoGame adapters.

Current cutover evidence:

- One generated-registry `World`, 250-route `EventRuntime`, and 23-system scheduler are owned by the root.
- Static scenes, scene-preparation events, combat presentation, stable-ID test fights, and all 46
  registered snapshot variants materialize `Transform + Sprite` entities without a legacy world.
- Hardware is captured only by `MonoGamePlayerInputAdapter` and translated at the host boundary.
- `Game1` consumes read-only render packets and drains host command/audio/shader requests after update.
- Focused ECS-050 integration/authoring/host tests pass, project build passes, and fresh/test-fight
  launches reach the game loop without instantiating the legacy ECS.

The exact-visual parity work tracked by ECS-052 remains: production texture/sound/shader bindings,
text packets, and fixture-specific snapshot authoring must replace the current semantic development
textures before unchanged visual baselines can pass.

Executable evidence is in
`tests/Crusaders30XX.Tests/DataOriented/Gameplay/Integration/Ecs050CutoverRehearsalTests.cs`.
Those tests record the current gaps; replace their gap assertions with positive composition gates
when the root host exists.

## Current host flow that must be preserved

| Host stage | Current responsibility | Cutover requirement |
| --- | --- | --- |
| `Initialize` | Initialize logging/telemetry, calculate display metrics, construct the legacy `World`. | Construct one data-oriented root composition. The composition owns one `World`, one `EventRuntime`, and one `SystemScheduler`; no legacy world may be constructed. |
| Early `LoadContent` | Create `SpriteBatch`, GPU profiler, rasterizer, image/font resources, achievement state, and the initial `SceneState`. | Create external GPU/audio/content adapters, validate generated catalogs, create the generated component registry/world, and run `GlobalUiWorldBootstrap.Create` exactly once. |
| System construction | Construct global display/input/gameplay systems and parent scene systems. Parent scene systems dynamically add a much larger set of child systems. | Instantiate all operational data-oriented systems once. Register global systems and scene-group systems with the scheduler; do not dynamically add systems during scene transitions. |
| System registration | Register legacy phases in insertion order, plus position tween and parallax as late systems. | Register explicit descriptors, build the dependency graph once, and fail before the first frame on duplicate IDs, missing dependencies, cycles, or unordered access conflicts. |
| Launch setup | Initialize snapshot host, optionally prepare test-fight battle, optionally prepare card-list profile. | Give each launch mode a new-world authoring adapter. None may accept or instantiate legacy `World`, `Entity`, or behavior objects. |
| `RunUpdate` | Tick logging, expose window activity, update legacy Input/Interaction/Gameplay/Presentation phases, then late systems, then profile the active scene. | Capture one hardware input snapshot, route it before the Input phase, run the scheduler once, synchronize `scheduler.ActiveScene` from the unique scene component for the next frame, consume external audio/GPU requests, and profile the same scene. |
| `DrawScene` | Draw a scene-specific branch, global overlays, optional additive battle pass, then cursor/trail. A background-only climb-dialogue branch suppresses normal scene and foreground draws. | Consume reusable render packets only. Preserve the scene branch, overlay order, background-only dialogue behavior, additive pass boundaries, cursor suppression rules, and SpriteBatch state transitions. Draw must not mutate ECS state. |
| Post-processing | Composite poison, circular shockwave, and rectangular shockwave in that order when shaders are enabled, invoke the snapshot hook, then letterbox to the backbuffer. | Consume shader request/packet buffers with identical pass order and target ping-pong behavior. Preserve the snapshot hook before presentation and the established final presentation behavior until characterization approves any change. |
| `UnloadContent` | Export telemetry, optionally write the performance report, flush logging, stop GPU profiling, and dispose image/render-target pools. | Extract final diagnostics before disposing external adapters and GPU resources. ECS buffers/entities need deterministic teardown, but gameplay systems must not own or dispose MonoGame resources. |

The scheduler phase order (`Input`, `Interaction`, `Rules`, `Gameplay`, `Presentation`,
`LatePresentation`, `RenderExtraction`) already models the desired update order. Rendering remains a
separate host draw step after extraction.

## Required root composition shape

ECS-050 needs one coordinator-owned composition object, created atomically before the first frame.
Its minimum ownership is:

1. The registry-created `World` and the unique globals returned by `GlobalUiWorldBootstrap`.
2. Stable resource stores and catalog/resource validation results.
3. One instance of every domain event hub: global/UI, card, combat, effects, meta-game, and
   presentation.
4. Host-facing consumers for input submission, window/application commands, scene asset
   preparation, cache invalidation, audio, GPU effects, telemetry, diagnostics, and persistence.
5. One combined `EventRoutingEndpoint`, then one `EventRuntime`.
6. One `SystemScheduler` using that same runtime.
7. An explicit registration array containing operational systems only.
8. Reusable presentation request queues, render-packet buffers, and draw consumers.

Construction order is contractual: create hubs and consumers, build all routes, create the single
runtime, create the scheduler (which attaches that runtime), bootstrap globals, construct systems,
register systems, build the scheduler, and only then materialize the initial scene. A combat session
must be created after the root runtime exists and must receive its already-routed combat hub.

## Blocking event-composition gap

`World.AttachEventRuntime` rejects a second runtime. `SystemScheduler` also attaches the runtime it
is given. The current domain APIs cannot therefore be called one after another:

- Cards expose an `EventRoutingEndpoint` through `CardGameplayComposition.Routes`.
- `CombatEventHub.Attach(World)` creates and attaches a combat-only runtime.
- `EffectGameplayEventHub.Attach(World)` creates and attaches an effects-only runtime.
- `PresentationEventHub.Attach(World, ...)` creates and attaches a presentation-only runtime.
- Global/UI has individual injected streams and consumers but no root-owned hub/route fragment.

Calling any second `Attach` throws. More subtly, `CombatSession.Create` checks whether the world
already has a runtime; when it does, the method leaves its `CombatEventHub` null. That suppresses
combat event publication precisely in the intended root-composition case.

Before ECS-050, domain composition APIs must change to return route fragments without attaching:

```text
GlobalUiEventHub.BuildRoutes(consumers...) -> IEventRoute[]
CardGameplayEventHub.BuildRoutes(consumers...) -> IEventRoute[]
CombatEventHub.BuildRoutes(consumers...) -> IEventRoute[]
EffectGameplayEventHub.BuildRoutes(consumers...) -> IEventRoute[]
MetaGameEventHub.BuildRoutes(consumers...) -> IEventRoute[]
PresentationEventHub.BuildRoutes(consumers...) -> IEventRoute[]
```

An equivalent allocation-at-initialization API such as `AppendRoutes(RouteBuilder, ...)` is valid.
The required properties are that hubs never attach a runtime, route IDs are globally unique, every
stream is represented exactly once, consumer priority/declaration order is explicit, and the root
creates exactly one endpoint. `CombatSession.Create` must accept the root-owned `CombatEventHub`
(or a non-null combat event surface) and use `world.Events`; it must not create a fallback runtime.

### Current route coverage

| Domain | Current surface | Current route/consumer state | Gate before cutover |
| --- | --- | --- | --- |
| Global/UI | 15 ledger events plus the additional consolidated input/UI contracts | No unified hub or endpoint. Four event-consumer classes exist, but host commands, scene preparation, cache/audio, and UI actions are not assembled. | Every input, scene, host, timer, and UI stream appears once in the root endpoint with its required consumers. |
| Cards | 58 ledger events; `CardGameplayEventHub` exposes 28 streams | `CardGameplayComposition` routes 9 streams. | Expose all 58 streams or document a generated cross-domain owner for each; root routes all required inputs/outputs, including presentation and ECS-044 tracking consumers. |
| Combat | 32 streams | All 32 routes exist, but every local route has zero consumers. | Inject combat/card/effects/presentation consumers in frozen priority order, and keep the combat hub non-null in `CombatSession`. |
| Effects | 22 streams at the current ECS-043 boundary | Effects-only runtime attachment. | Return route fragments and supply gameplay consumers plus cross-domain card/combat/presentation outputs. |
| Presentation | 38 streams at the current ECS-045 boundary | Presentation-only runtime attachment; only a subset has request consumers. | Return route fragments. Zero-consumer notification streams are permitted only when the completeness audit proves no legacy subscription remains. |

Publishing an event to an unlisted stream is worse than a zero-consumer route: it does not
contribute to pending counts and is never drained. A zero-consumer route may be intentional only
for a terminal notification proven obsolete or externally observed by another explicit consumer.

## Input and host-command seam

The new `PlayerInputSystem` consumes the unique `PlayerInputState`; it does not poll hardware. The
legacy `MonoGamePlayerInputAdapter` produces a different legacy frame shape, so it cannot be passed
through unchanged. ECS-050 needs a MonoGame adapter that maps keyboard, mouse, gamepad, window
activity, render-destination coordinates, and previous-frame edges into the data-oriented
`PlayerInputFrame` and publishes `PlayerInputEvent`.

The input event must be drained before `SystemScheduler.Update`, otherwise the Input phase observes
the previous frame. The safe frame boundary is:

1. Capture hardware once.
2. Publish `PlayerInputEvent` and drain the root event barrier.
3. Run all scheduler phases once.
4. Execute host-side requests produced by routed consumers after scheduler completion.

`PlayerCommandEvent` requires a host consumer for quit, fullscreen, diagnostic overlays, debug
damage, and profiler toggling. That consumer may request host actions but must not mutate gameplay
components. Snapshot mode must continue suppressing nonessential host commands.

## Scene activation seam

`SceneLifecycleSystem` owns teardown and changes the unique `SceneState`. The scheduler owns a
separate `ActiveScene` value. No system may receive the scheduler, so the host must synchronize
those values after an update. The newly activated group then starts on the next frame, matching the
barrier between preparation/activation and scene execution.

Before the first frame, set `scheduler.ActiveScene` from the bootstrap scene. During transitions:

1. Route `LoadSceneEvent` into `SceneTransitionState`.
2. Let `SceneLifecycleSystem` record teardown and publish `PrepareSceneEvent`.
3. Route preparation to the external asset/authoring adapter and update preparation counters through
   owned state/events.
4. Let `SceneLoadingCoordinatorSystem` publish ready, then activate on the next eligible update.
5. After scheduler completion, copy the unique `SceneState.Current` to `scheduler.ActiveScene`.

The ECS-044 scene authoring paths must materialize title, way station, climb, battle, achievement,
snapshot, test-fight, and persistence state without legacy factories. No partially converted scene
may enter the scheduler.

## Descriptor and registration gate

The current descriptor arrays are a migration ledger, not a safe registration list:

- All 26 card descriptors have empty component, dynamic-buffer, and event access metadata.
- Twenty-five card types inherit the base no-op `Update`; several are route/API owners, while the
  rest are consolidated names or ECS-045 extraction names.
- The 28 parameterless combat descriptor shells inherit a no-op `Update` and have empty access
  metadata.
- The two active combat systems also need complete access declarations for their consolidated
  `CombatSession` writes.
- `DeckManagementSystem.Update` can record removal commands, but its inherited descriptor does not
  declare structural commands. Scheduling it with pending spawns can trigger the scheduler's
  structural-command validation.

Before registration, classify every descriptor into exactly one category:

| Category | Registration rule |
| --- | --- |
| Operational update system | Register once, with complete reads/writes, dynamic-buffer access, consumed/emitted event IDs, structural-write flag, barriers, and dependencies. |
| Event consumer/API owner | Route the consumer/API; register only if it also performs meaningful per-frame work with a complete descriptor. |
| Consolidated legacy name | Do not register separately. The mapping/audit must name the operational owner. |
| Presentation/external adapter | Register the extraction system when it updates ECS state or packet buffers; keep MonoGame draw/audio consumers outside the scheduler. |

ECS-050 must use an explicit allowlist of operational instances. It must not reflect over all
`IGameSystem` types or blindly register every entry returned by the current card composition. Build
the scheduler during startup so empty/missing dependencies and access conflicts fail before play.

## Rendering and draw-consumer gate

ECS-045 must provide a new-world fixture host and reusable packet buffers before `Game1` switches.
The coordinator should keep MonoGame objects outside ECS components and pass only read-only packet
spans/queues to draw consumers. The extraction phase may update reusable buffers; `Draw` may not
advance tweens, clear gameplay flags, publish gameplay events, or change scene state.

The following legacy draw branches need explicit packet consumers or characterized obsolescence:

- Title blood/background/menu.
- Way-station background, incense, POIs, dialogue, settings, and medals modal.
- Battle scene normal and additive passes.
- Climb and achievement scenes.
- Background-only climb dialogue.
- Global hotkeys, location/reward/narrative/card-list/booster/tooltips/alerts/diagnostics/dialogue,
  pause/game-over/transition/debug overlays, and cursor/trail.
- Poison, circular shockwave, and rectangular shockwave composites.

Snapshot fixtures must consume the same packets as the live host. A separate legacy snapshot world
is not an acceptable production bridge.

## Blockers

| ID | Severity | Blocking condition | Required owner/gate |
| --- | --- | --- | --- |
| C50-01 | Hard | ECS-046 has not passed; ECS-043 through ECS-045 are not all frozen. | Complete ECS-043/044/045, then ECS-046. |
| C50-02 | Hard | Domain hubs create mutually exclusive runtimes. | Domain route-fragment APIs and one coordinator-owned endpoint. |
| C50-03 | Hard | Root-attached runtime causes `CombatSession` to lose its event hub. | Inject the root combat hub; remove fallback attachment. |
| C50-04 | Hard | Card routes cover 9 of 58 ledger events and only 28 hub streams exist. | ECS-041 integration repair plus cross-domain consumers. |
| C50-05 | Hard | Combat routes have no consumers. | ECS-042 integration repair with priority/order characterization. |
| C50-06 | Hard | No global event hub, data-oriented hardware adapter, or host-command adapter exists. | ECS-040 integration repair and coordinator host adapter. |
| C50-07 | Hard | Card/combat descriptor shells and active systems lack scheduling/access metadata; deck structural writes are undeclared. | Owning-domain descriptor repair and ECS-046 audit. |
| C50-08 | Hard | ECS-044 scene/save/fresh-run materialization is not yet available. | ECS-044 parity gates. |
| C50-09 | Hard | Packet draw consumers and new-world snapshot host are not yet proven complete. | ECS-045 snapshots/read-only draw gate. |
| C50-10 | Hard | Snapshot, test-fight, and card-list-profile launch paths still depend on the legacy world/factories. | Coordinator launch adapters after domain APIs freeze. |

## Sequenced coordinator-only cutover checklist

Do not split this window across two active production worlds.

1. Confirm ECS-046 passes with zero unowned ledger responsibilities and all targeted domain tests,
   snapshots, and deterministic traces green.
2. Freeze domain construction APIs. Confirm every hub returns routes without attaching, every event
   has one stable ID/stream/route, `CombatSession` requires the root combat hub, and all required
   consumers have explicit priority.
3. Add the coordinator-owned root composition outside `Game1`. In a hostless integration test,
   construct registry, world, hubs, consumers, one endpoint/runtime, globals, systems, and scheduler;
   call `Build` and assert the initial scene's exact execution order.
4. Add positive route gates: unique global route IDs, full ledger coverage, required consumer names
   and priorities, nested-wave order, and no second runtime attachment API in any domain hub.
5. Add positive registration gates: explicit operational allowlist, no descriptor-only shells,
   complete access metadata, declared structural writes, and scene-group coverage.
6. Add host adapters for data-oriented input, player commands, asset preparation/cache invalidation,
   audio, GPU requests, telemetry, save extraction, and diagnostics. Test them without `Game1` where
   possible.
7. Port snapshot, test-fight, and card-list-profile setup to new-world authoring APIs. Run their
   focused nonvisual/fixture tests before touching `Game1`.
8. In one coordinator edit, replace the legacy `World` field and initialization with the tested root
   composition. Remove all legacy system construction/registration and static `EventManager`
   subscriptions from `Game1`.
9. Replace `RunUpdate` with capture, pre-input drain, scheduler update, active-scene synchronization,
   and external request consumption. Preserve profiling boundaries and one scheduler call per frame.
10. Replace scene draw calls with read-only packet consumers while preserving SpriteBatch/pass order,
    background-only dialogue, snapshot interception, shader ordering, letterboxing, and cursor rules.
11. Update unload/diagnostic extraction for the new scheduler and external resources.
12. Run `dotnet build`, focused root-composition tests, the full test project, snapshot verification,
    `dotnet run -- new`, title-to-climb-to-battle smoke, and
    `dotnet run -- test-fight hammer skeleton hard`.
13. Search `Game1.cs` for the legacy namespace, legacy `World`, `EventManager`, old factories,
    `GetComponent`, and `GetEntitiesWithComponent`; all must be absent before ECS-050 is complete.
14. Only after the ECS-050 smoke gate passes, begin the separately reversible ECS-051 deletion
    batches.

## Rehearsal verdict

The scheduler/world foundation is hostable, but the repository is not ready for the `Game1`
switch. The first integration repair is the root event graph; without it, all other cutover work
either throws during startup or silently suppresses cross-domain events. Descriptor registration,
input/scene adapters, ECS-044 materialization, and ECS-045 draw consumers are subsequent hard gates.
