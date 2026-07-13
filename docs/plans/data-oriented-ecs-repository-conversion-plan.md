# Full Repository Data-Oriented ECS Conversion - Multi-Agent Implementation Plan

## Document Status

- **Status:** Approved design, ready for implementation.
- **Repository:** `Crusaders30XX`.
- **Runtime:** .NET 8.0, MonoGame DesktopGL.
- **Migration type:** Full repository conversion with a clean final cutover.
- **ECS ownership:** Project-owned implementation; do not add a third-party ECS runtime.
- **Memory model:** Managed typed arrays and spans; no unsafe or native memory.
- **Component model:** Fixed-size unmanaged structs and empty tags.
- **Behavior policy:** Exact gameplay, visual, input, and ordering parity.
- **Threading:** Deterministic single-thread execution in this conversion, with scheduler metadata that permits later parallelization.
- **Performance target:** 120 Hz CPU budget on the reference development machine.
- **Save compatibility:** None. Validate with a fresh run.
- **Required final verification:** Full build, tests, snapshots, deterministic gameplay fixtures, and performance gates in this document.

This document is intentionally comprehensive. It is the source of truth for a multi-agent conversion and closes the architecture decisions that implementers would otherwise have to make locally.

---

## 1. Objective

Replace the current object-oriented ECS implementation with a cache-oriented archetype/chunk ECS across the entire repository.

The finished runtime must provide:

1. Value-type, generation-checked entity handles.
2. Unmanaged struct components stored in contiguous typed columns.
3. Empty tags for marker state.
4. Cached archetype queries with allocation-free chunk/span iteration.
5. Batched structural changes through deterministic command buffers.
6. World-owned contiguous dynamic buffers for variable-length state.
7. Generated type registration, queries, spawn bundles, authoring catalogs, and dispatch tables.
8. Data-only components with behavior owned by systems.
9. Scene-aware system groups that do not update inactive scenes.
10. World-owned typed event streams and deterministic rules/trigger queues.
11. One-file authoring modules for cards, enemies, attacks, equipment, and medals.
12. Allocation-free steady-state ECS, scheduling, event, and render-extraction paths.

The existing ECS must not remain as a compatibility layer in the finished repository.

---

## 2. Review Findings and Motivation

### 2.1 Current storage is not cache-oriented

- `Entity` owns `Dictionary<Type, IComponent>`.
- Every current component is a heap-allocated class.
- `EntityManager` maintains `Dictionary<Type, List<Entity>>`, which makes entity references contiguous but leaves component data scattered across the managed heap.
- A typical system iterates entity references, then performs one or more per-entity type dictionary lookups.
- Components carry an `Owner` reference back to their entity, adding another pointer and coupling data to the old entity object.
- Multi-component filtering is expressed through entity intersection or secondary `HasComponent`/`GetComponent` checks rather than direct component columns.

### 2.2 Current queries and scheduling add avoidable overhead

- `GetEntitiesWithComponent<T>()` creates a LINQ `Where` iterator for active filtering.
- `GetEntitiesWithComponents()` builds LINQ intersections.
- The base `System` API exposes `IEnumerable<Entity>`, forcing interface-based iteration and preventing direct span access.
- `SystemManager` copies phase lists to arrays every frame.
- Inactive scene systems remain registered and are still called unless they individually return early.
- Many hot paths add `Where`, `Select`, `OrderBy`, `FirstOrDefault`, `ToList`, or anonymous objects on top of ECS queries.

### 2.3 The object model defeats data-oriented iteration

- `CardData` points to a polymorphic `CardBase` containing strings, lists, delegates, mutable definition data, and behavior.
- `Enemy`, `EquippedEquipment`, and `EquippedMedal` point to disposable behavior objects.
- `Deck` stores several `List<Entity>` collections.
- Components such as UI, battle tracking, planned attacks, and queued events combine hot state with strings, collections, or object graphs.
- Card, enemy, equipment, and medal behavior is dispatched through delegates or virtual/object calls rather than compact IDs and generated static dispatch.

### 2.4 Scale and current performance

The static review found approximately:

- 196 component classes and zero component structs.
- 2,063 `GetComponent<T>()` call sites.
- 727 `GetEntitiesWithComponent<T>()` call sites.
- 225 system classes.
- 157 implementations of `GetRelevantEntities()`.

The latest available performance report recorded the battle ECS update at approximately 3.31 ms average and 6.77 ms P95 across 6,374 battle frames. That run had diagnostic UI active and did not measure allocations, entity counts, query counts, or hardware cache misses. It proves that update cost is material, but it is not sufficient as the conversion baseline.

### 2.5 Verdict

The current architecture provides ECS organization and component indexing, but not the usual cache-local benefits of an archetype or structure-of-arrays ECS. A local index optimization would not meet the requested end state. The repository requires a full entity, component, query, system, event, content-authoring, and rendering-data conversion.

---

## 3. Canonical Architecture Decisions

These decisions are closed. Implementers must not substitute alternatives without revising this plan first.

### 3.1 Project-owned ECS

- Build the new ECS in this repository.
- Do not add Arch, Friflo, DefaultEcs, Flecs, or another ECS dependency.
- External benchmark or test packages require a separate explicit decision; the default benchmark harness uses the .NET runtime and repository code only.

### 3.2 Managed contiguous memory

- Use managed typed arrays, `Span<T>`, `ReadOnlySpan<T>`, pooling, and generated generic accessors.
- Do not use `unsafe`, `NativeMemory`, pinned native blocks, or native bindings.
- Component columns may be separate arrays; all values for one component type within a chunk must remain contiguous.

### 3.3 Strict unmanaged ECS data

- Every ECS component must satisfy `unmanaged`.
- Every marker must be an empty tag.
- Components may contain `EntityId`, enums, numeric primitives, MonoGame unmanaged value types, compact resource IDs, and dynamic-buffer handles.
- Components must not contain `string`, arrays, lists, dictionaries, delegates, interfaces, class references, textures, fonts, or other managed references.
- Strings, immutable definitions, and assets live in catalogs/resource stores behind compact IDs.
- Variable-length mutable state lives in world-owned dynamic buffers.

### 3.4 Exact parity

- Preserve current rules, card outcomes, upgrades, enemy behavior, event priority, phase ordering, scene behavior, input targeting, visuals, animation timing, audio triggers, and deterministic outcomes.
- Do not redesign gameplay or UI during the conversion.
- If characterization exposes a likely existing defect, record it separately; do not silently change it in this migration.

### 3.5 Single-threaded initial scheduler

- Execute systems serially in deterministic order.
- Every system still declares component reads, component writes, event inputs, event outputs, and structural-write behavior.
- Validate those declarations and build a dependency graph now.
- Do not schedule parallel jobs as part of this implementation.

### 3.6 Single-file content authoring

- Each card remains understandable from one card-specific source file.
- The file contains immutable metadata, upgrade changes, common declarative effects, and exceptional static behavior handlers.
- Apply the same pattern to enemies, attacks, equipment, and medals.
- Generated catalogs and dispatch code are outputs; authors do not maintain central switch statements manually.

### 3.7 Clean cutover

- The migration branch may temporarily compile old and new namespaces side by side for isolated tests.
- Do not build a production bridge that mirrors entities or components between two worlds.
- Do not ship or merge while both ECS runtimes are active.
- Delete the old core, old components, old systems, adapters, and unused object behavior after the final cutover.

---

## 4. Explicit Non-Goals

- Do not preserve old save compatibility.
- Do not introduce multithreaded system execution.
- Do not change game balance, content, visuals, input semantics, or animation timing.
- Do not replace MonoGame, SpriteBatch, the content pipeline, or the scene model.
- Do not introduce a general scripting language for content behavior.
- Do not move immutable game definitions into save files.
- Do not retain object-style components as a permanent cold-component tier.
- Do not permit managed fields merely because a component is infrequently queried.
- Do not make draw functions manage state.
- Do not pass system instances into other systems.
- Do not bypass `CursorEvents`/the existing input abstraction.
- Do not optimize unrelated asset loading or GPU shader work unless required to meet the defined CPU draw-submission budget.

---

## 5. Target Public Contracts

Names are normative unless an unavoidable namespace conflict requires a mechanical equivalent.

### 5.1 Entity identity

```csharp
public readonly record struct EntityId(int Index, int Generation)
{
    public static EntityId Null => default;
    public bool IsNull => Index == 0;
}
```

- Index `0` is never assigned.
- Reused indexes increment generation.
- World APIs reject dead or stale handles.
- Components and events store `EntityId`, never entity objects.
- Entity names are debug/content metadata represented by `StringId` or an indexed name component.

### 5.2 Components and tags

```csharp
public interface IComponent { }
public interface ITag { }
```

- The incremental generator verifies that component and tag declarations are structs and that components are unmanaged.
- Tags do not allocate component columns.
- Components use fields for simple data and derived read-only helpers only when those helpers do not mutate state or hide structural behavior.

### 5.3 Type registration and signatures

- Generate stable runtime component/tag IDs for the current build.
- Use a 512-bit signature implemented as eight `ulong` words.
- Component and tag IDs share the same 512-type limit.
- Exceeding the limit is a compile-time diagnostic.
- Save data must never persist generated runtime type IDs; saves continue to use stable domain IDs.

### 5.4 World operations

Required operations:

```text
Create(in SpawnBundle)
Destroy(EntityId)
IsAlive(EntityId)
Has<T>(EntityId)
Get<T>(EntityId) -> ref T
TryGet<T>(EntityId, out value)
Add<T>(EntityId, in T)
Remove<T>(EntityId)
Set<T>(EntityId, in T)
Enable(EntityId)
Disable(EntityId)
Query<T1...T8>(filter)
GetDynamicBuffer<T>(handle)
GetUnique<TTag>()
```

- `Create` accepts a generated spawn bundle describing the final signature and values so construction performs one archetype placement.
- `Add`, `Remove`, `Destroy`, `Enable`, and `Disable` are structural writes.
- Direct structural writes are legal only outside active query iteration.
- Invalid direct structural writes throw a descriptive exception in all configurations.

### 5.5 Archetypes and chunks

- An archetype is identified by its complete component/tag signature.
- Cache add/remove transition edges between archetypes.
- Store entity locations in world-owned arrays: archetype, chunk, row, generation, alive/enabled state.
- Each chunk stores one `EntityId[]` and one typed `T[]` for each data component.
- Compute chunk capacity as `clamp(floor(16 KiB / rowSize), 1, 1024)` using entity and component sizes; tags contribute no row bytes.
- Swap-remove rows and update the moved entity's location immediately.
- Pool empty chunks for reuse.
- Adding multiple components to one entity must use a batch/bundle transition so the entity moves once.

### 5.6 Queries

- Support one through eight returned component types.
- Support arbitrary `All`, `Any`, and `None` component/tag masks as filters.
- Cache the matching archetype list on the query object.
- Append newly created matching archetypes without rebuilding unrelated query caches.
- Iterate chunks, obtain typed spans once per chunk, then iterate rows.
- Do not perform hashing, reflection, type tests, dictionary lookups, interface enumeration, LINQ, or allocation per row.
- Disabled entities are excluded by default; a query may explicitly include them.

### 5.7 Structural command buffers

- Each system receives a reusable command buffer from `SystemContext`.
- Commands include create bundle, destroy, enable/disable, add/remove/set component, add/remove tag, and dynamic-buffer mutation.
- Playback occurs after each sequential system, preserving visibility for the next system.
- Commands retain record order.
- Future parallel scheduling may merge buffers only in declared deterministic system order.

### 5.8 Dynamic buffers

```csharp
public readonly record struct DynamicBufferHandle<T>(int Index, int Generation)
    where T : unmanaged;
```

- Buffers use pooled contiguous `T[]` storage owned by the world.
- Buffer handles are generation checked.
- Buffer growth may allocate during warm-up; steady-state mutation at established capacity must not allocate.
- Destroying the owning entity releases its owned buffers back to pools.
- Use buffers for deck zones, planned attacks, queued rules/triggers, tracking entries, passive stacks, modal choices, and similar variable-length state.
- Do not create linked entity lists for ordered card zones.

### 5.9 Systems and scheduling

```csharp
public interface IGameSystem
{
    SystemDescriptor Descriptor { get; }
    void Update(ref SystemContext context);
}
```

`SystemDescriptor` declares:

- Stable system ID.
- Phase.
- Scene group.
- Read component mask.
- Write component mask.
- Read/write dynamic-buffer types.
- Consumed and emitted event types.
- Explicit before/after dependencies.
- Whether the system records structural commands.

Scheduler requirements:

- Generate and validate the dependency graph during initialization.
- Reject cycles with a diagnostic that names the dependency path.
- Cache phase execution arrays; rebuild only when registration changes.
- Invoke only global systems and the active scene group.
- Keep late presentation/layout as an explicit phase rather than a separate ad hoc list.
- Profile execution time and allocated bytes per system in debug/performance fixtures.

### 5.10 Events and queued rules

- Replace static `EventManager` with world-owned typed event streams.
- Events are unmanaged structs containing compact IDs and entity handles.
- Generated routing maps event types to ordered consumers without reflection or runtime delegate discovery.
- Event buffers are reusable contiguous arrays.
- Preserve current priority ordering and nested publication through deterministic event waves.
- Drain an event wave before advancing past its declared scheduler barrier.
- Add a configurable high event-count guard; exceeding it reports the recent event/system chain and stops resolution rather than hanging.
- Preserve separate mandatory rules and reactive trigger FIFO lanes.
- Multi-frame rules store explicit typed state and resume on later updates.
- Do not represent every transient event as an ECS entity; use event streams to avoid archetype churn.

### 5.11 Resources, strings, and definitions

- Introduce compact unmanaged IDs such as `StringId`, `TextureAssetId`, `SoundId`, `CardId`, `EnemyId`, `EnemyAttackId`, `EquipmentId`, `MedalId`, and `VisualEffectRecipeId`.
- Immutable catalogs own strings, costs, descriptions, definition tables, recipes, and other managed content.
- Rendering resource stores map asset IDs to MonoGame resources outside ECS component memory.
- Runtime ECS components store IDs and instance state only.
- Catalog initialization may allocate; per-frame catalog lookup must not.

### 5.12 Content definition modules

Each content item uses a static partial module, for example:

```csharp
[CardDefinition(CardId.Strike)]
public static partial class StrikeCard
{
    public static CardDefinition Definition => ...;
    public static CardUpgradeDefinition Upgrade => ...;
    public static void BuildPlayCommands(ref CardPlayContext context) { ... }
}
```

- Common behavior is encoded as compact declarative effect/condition structs.
- Exceptional behavior uses static handlers in the same file.
- Handlers receive a read-only world view and append typed rule commands; they do not directly perform cross-system writes.
- Generate dense catalogs indexed by stable domain enums/IDs.
- Generate direct switch dispatch to static handlers.
- Do not create per-instance definition objects, delegates, closures, or reflection registrations.
- Apply equivalent modules to enemy definitions, attacks, equipment, medals, and other polymorphic content.

### 5.13 Rendering

- Update/presentation systems extract compact render packets into reusable per-layer buffers.
- Packets contain resolved asset IDs, geometry, color, source rectangle, layer, Z order, and effect parameters.
- Mark render layers dirty when membership or sorting keys change.
- Re-sort only dirty layers; animation values may update packet fields without rebuilding membership.
- Draw methods read packets and issue MonoGame calls only.
- Draw methods do not mutate ECS state, dynamic buffers, caches, timelines, or input state.

---

## 6. Domain Data Conversion Rules

### 6.1 Component classification

For every old `IComponent`, record one target classification in the component migration ledger:

| Old shape | Target |
| --- | --- |
| Empty marker class | `ITag` |
| Fixed mutable state | Unmanaged `IComponent` struct |
| Immutable content/metadata | Catalog definition referenced by ID |
| Ordered or variable-length mutable collection | `DynamicBufferHandle<T>` plus world-owned buffer |
| Entity relationship | `EntityId` field or relation component |
| Managed engine resource | Resource ID resolved outside ECS |
| Mixed hot/cold state | Split into small hot component plus IDs/buffers for cold data |
| Behavior/delegate/object | System, declarative command/effect data, or generated static handler |

No old component may be copied mechanically into a managed struct.

### 6.2 Required hot/cold splits

- Split `UIElement` interaction geometry/flags from tooltip and display text metadata.
- Split `CardData` instance state from immutable card definition, upgrade definition, and behavior.
- Split enemy instance combat state from enemy and attack definitions.
- Split presentation animation values from immutable rendering configuration.
- Split battle counters into typed counters/buffers rather than dictionaries keyed by strings.
- Split scene/modal state from text, choices, and content definitions.

### 6.3 Deck and card zones

- The deck entity owns dynamic buffers for master order, draw pile, discard pile, exhaust pile, and hand.
- Each entry is an `EntityId`; cards also carry compact zone/order state needed for direct queries.
- The deck/card-zone owning system is the only writer of both the buffers and card zone component.
- Card moves update source buffer, destination buffer, and card state atomically through one rule command.
- Preserve all current order, shuffle, duplicate-card identity, weapon, token, and transient-zone semantics.

### 6.4 Object behavior removal

- Replace card delegates and `CardBase` subclasses.
- Replace enemy and enemy-attack behavior objects.
- Replace equipment and medal activation/provider interfaces.
- Convert replacement effects, stat modifiers, alternate play rules, and conditional effects into generated provider tables or typed effect/condition systems.
- Remove object disposal from entity destruction; catalog/resources own their own lifetime.

### 6.5 Unique entities

Use generated unique indexes rather than repeated singleton queries for at least:

- Scene state.
- Player.
- Deck.
- Phase state.
- Queued rule state.
- Player input state.
- Global settings/configuration entities.

Duplicate creation of a unique type must fail immediately with both entity IDs in the diagnostic.

---

## 7. Migration Strategy

### 7.1 Branch and namespace strategy

- Perform the work on a dedicated conversion branch.
- Add the new runtime under a distinct data-oriented namespace while the old runtime still compiles.
- New subsystem tests instantiate only the new world.
- Do not register partially converted systems in the live game.
- Domain agents create new files rather than rewriting shared old files during parallel waves.
- The integration owner performs the final `Game1` registration switch and deletes old files after all new subsystem gates pass.

### 7.2 Vertical conversion order

1. Core runtime and generators.
2. Shared IDs, components, buffers, events, and definition catalogs.
3. Global lifecycle, scene scheduling, and input/UI.
4. Card definitions, deck zones, and card play.
5. Enemy definitions, attacks, and combat resolution.
6. Equipment, medals, passives, modifiers, and replacement effects.
7. Climb, way station, rewards, tutorials, dialogue, achievements, and save DTO adapters.
8. Rendering, visual effects, audio requests, diagnostics, and snapshots.
9. `Game1` cutover and old-runtime deletion.
10. Parity repair, performance tuning, and documentation.

### 7.3 Buildability policy

- Foundation and isolated subsystem tasks must end with a compiling repository.
- New systems may remain unregistered until cutover.
- The coordinator must not merge a subagent task whose declared tests fail.
- The cutover task may be a coordinated integration window, but it must return to a compiling state before unrelated work resumes.

---

## 8. Multi-Agent Execution Protocol

### 8.1 Roles and concurrency

- One **coordinator/integration owner** owns this plan, shared contracts, `Game1.cs`, final registration, old-code deletion, and merge decisions.
- Up to three implementation subagents work concurrently.
- A subagent receives exactly one task package at a time.
- Subagents may not change architecture decisions or start dependent tasks early.

### 8.2 Shared-file ownership

Only the coordinator may edit during parallel waves:

- `Game1.cs`.
- The root project file and solution membership after foundation setup.
- This plan's checkboxes/status.
- Global system registration tables.
- Shared component/event/catalog ID enums after their initial contract is frozen.
- Final deletion lists and architecture documentation.

The core-runtime owner exclusively edits the new archetype/chunk/query/command-buffer implementation. The generator owner exclusively edits generator/analyzer code. Domain agents work in separate domain directories and must not edit another domain's files.

### 8.3 Subagent handoff requirements

Every subagent response must include:

1. Completed task IDs.
2. Files added, changed, or deleted.
3. Contract deviations, if any; deviations require coordinator approval before merge.
4. Tests and commands run with outcomes.
5. Remaining failures classified as introduced or pre-existing.
6. Any discovered behavior not covered by the plan.
7. Suggested next unblocked task IDs.

### 8.4 Subagent prompt template

```text
Read AGENTS.md and docs/plans/data-oriented-ecs-repository-conversion-plan.md completely.
Implement task <ID> only. Respect its dependencies and file ownership boundaries.
Do not edit Game1.cs, shared IDs/contracts, the plan file, or another task's domain.
Preserve exact behavior. Add the task's required tests and run its verification commands.
Return the required handoff report; do not begin follow-on work.
```

### 8.5 Coordinator merge checklist

- Confirm dependency task IDs are complete.
- Inspect `git diff` for ownership violations and unrelated edits.
- Verify no managed component fields or old entity references entered new code.
- Run the task's targeted tests.
- Run `dotnet build` after every merged task.
- Update task status only from the coordinator session.
- Resolve shared-contract changes before dispatching further dependent agents.

---

## 9. Dependency Graph and Task List

### 9.1 Phase A - Baseline and contracts

#### [x] ECS-000: Establish the legacy baseline

- **Owner:** Verification subagent.
- **Dependencies:** None.
- **May run with:** ECS-001.
- **Deliverables:**
  - Diagnose the current no-output/non-completing test invocation.
  - Record build and test status without changing behavior.
  - Add deterministic legacy characterization fixtures for representative battle, climb, scene transition, card play, and input paths.
  - Add a Release performance fixture with diagnostics closed, shaders disabled, fixed seed, warm-up, fixed sample count, and JSON output.
  - Record entities, components, system calls, query calls, allocated bytes, frame CPU scopes, and GC counts where possible without materially changing release behavior.
  - Capture the initial legacy result artifact used by later comparison.
- **Verification:** `dotnet build`; full existing tests; new characterization fixtures; one battle and one climb performance capture.

#### [x] ECS-001: Freeze architecture contracts and migration ledgers

- **Owner:** Coordinator.
- **Dependencies:** None.
- **May run with:** ECS-000.
- **Deliverables:**
  - Add an ADR for the custom archetype/chunk ECS.
  - Freeze namespaces, public contracts, system phases, scene groups, and component/tag limits.
  - Produce component, event, system, and object-behavior migration ledgers covering every old type.
  - Assign each type to one and only one later domain task.
  - Freeze shared stable domain IDs before parallel domain conversion begins.
- **Verification:** Ledger scripts/findings show no unclassified component, system, subscription, or polymorphic content class.

### 9.2 Phase B - Runtime foundation

#### [x] ECS-010: Implement entity, archetype, and chunk storage

- **Owner:** Core-runtime subagent.
- **Dependencies:** ECS-001.
- **May run with:** ECS-011 and ECS-012.
- **Deliverables:** Entity handles/generations, location table, 512-bit signatures, archetype lookup, transition-edge cache, typed column chunks, capacity calculation, pooling, swap removal, enable/disable state, spawn bundles' runtime endpoint, and bulk destruction.
- **Verification:** Focused unit tests for create/get/set, stale handles, generation reuse, archetype moves, swap removal, enable/disable, chunk reuse, and bulk teardown.

#### [x] ECS-011: Implement incremental generators and analyzers

- **Owner:** Generator subagent.
- **Dependencies:** ECS-001.
- **May run with:** ECS-010 and ECS-012.
- **Deliverables:** Component/tag registration, unmanaged validation, 512-type diagnostic, spawn-bundle generation, query arities one through eight, stable descriptor generation, and generated debug metadata.
- **Verification:** Generator snapshot tests and compile-fail tests for managed fields, class components, excessive types, duplicate IDs, and invalid query declarations.

#### [x] ECS-012: Build benchmark and model-test harnesses

- **Owner:** Verification subagent.
- **Dependencies:** ECS-000 and frozen contracts from ECS-001.
- **May run with:** ECS-010 and ECS-011.
- **Deliverables:** Standalone Release microbenchmark runner; legacy baseline loader; randomized reference-model framework; allocation measurement; optional platform hardware-counter command documentation.
- **Verification:** Repeated runs produce stable medians and machine-readable output; empty harness overhead is measured and subtracted/reported.

#### [x] ECS-013: Implement cached queries and structural command buffers

- **Owner:** Core-runtime subagent.
- **Dependencies:** ECS-010 and ECS-011.
- **May run with:** None in core files.
- **Deliverables:** Cached archetype query matching, chunk/span enumeration, filters, disabled inclusion, structural-change guards, all command types, deterministic playback, and batch transitions.
- **Verification:** Query correctness for all/any/none, new-archetype cache updates, no allocations after warm-up, structural mutation exception, command order, and batched single-move behavior.

#### [x] ECS-014: Implement dynamic buffers, indexes, and resource IDs

- **Owner:** Core-runtime subagent.
- **Dependencies:** ECS-010 and ECS-011.
- **May run with:** ECS-015 after shared interfaces freeze.
- **Deliverables:** Generation-checked dynamic buffers, pooling/growth/release, unique indexes, string/name indexes, compact resource ID base contracts, and debug inspection hooks.
- **Verification:** Buffer order, capacity reuse, stale handles, owner destruction, unique collisions, name lookup, and zero steady allocation.

#### [x] ECS-015: Implement scheduler, event streams, and queued rules

- **Owner:** Systems-runtime subagent.
- **Dependencies:** ECS-011 and ECS-013; dynamic-buffer interface from ECS-014 frozen.
- **May run with:** Completion of ECS-014 internals.
- **Deliverables:** System descriptors, dependency graph validation, cached phase arrays, scene groups, profiling, typed event streams, generated routing endpoint, event waves/cycle guard, command playback barriers, rules FIFO, trigger FIFO, and multi-frame queued-state contract.
- **Verification:** System order, cycle diagnostics, scene activation, event priority/nesting, queue precedence, multi-frame resume, and per-system allocation metrics.

#### [x] ECS-016: Foundation integration gate

- **Owner:** Coordinator plus verification subagent.
- **Dependencies:** ECS-010 through ECS-015.
- **May run with:** None.
- **Deliverables:** Consolidated core API, resolved contract inconsistencies, foundation documentation, benchmark comparison against legacy primitives, and frozen APIs for domain agents.
- **Gate:** All core/model/generator tests pass; queries and scheduler allocate zero bytes after warm-up; no domain work begins until this gate is signed off.

### 9.3 Phase C - Shared data and authoring infrastructure

#### [x] ECS-020: Define shared data-oriented components and tags

- **Owner:** Shared-schema subagent.
- **Dependencies:** ECS-016 and migration ledgers.
- **May run with:** ECS-021 and ECS-022.
- **Deliverables:** Entity metadata, scene state, transforms, hierarchy handles, timing/tween state, common UI interaction state, common combat resources, common presentation state, persistence tags, and component splitting documented by the ledger.
- **Verification:** Analyzer passes; component-size report is generated; no managed fields; component round-trip and query tests pass.

#### [x] ECS-021: Implement definition-module generator and catalogs

- **Owner:** Generator subagent.
- **Dependencies:** ECS-016 and stable domain IDs.
- **May run with:** ECS-020 and ECS-022.
- **Deliverables:** Definition attributes/contracts, dense catalog generation, duplicate/missing ID diagnostics, static handler dispatch, declarative effect/condition table generation, and debug enumeration metadata.
- **Verification:** Sample definition modules compile and dispatch without reflection, delegates, closures, or per-use allocation.

#### [x] ECS-022: Implement shared rule commands and read-only contexts

- **Owner:** Gameplay-contract subagent.
- **Dependencies:** ECS-016.
- **May run with:** ECS-020 and ECS-021.
- **Deliverables:** Unmanaged rule-command union/types, read-only world view, card/enemy/equipment/medal handler contexts, targeting handles, compact stat/effect/condition data, and deterministic command append APIs.
- **Verification:** Command serialization-in-memory tests, ordering tests, invalid direct write tests, and zero-allocation handler invocation.

#### [x] ECS-023: Shared-data integration gate

- **Owner:** Coordinator.
- **Dependencies:** ECS-020 through ECS-022.
- **Deliverables:** Frozen component namespaces, shared IDs, rule commands, catalogs, and authoring APIs.
- **Gate:** Domain subagents can add content modules and systems without editing shared contracts.

### 9.4 Phase D - Parallel content conversion

#### [x] ECS-030: Convert all cards and upgrades

- **Owner:** Card-content subagent.
- **Dependencies:** ECS-023.
- **May run with:** ECS-031 and ECS-032.
- **File ownership:** New card definition/module directory and card-specific tests only.
- **Deliverables:** One module per card; immutable metadata, cost, color/type, upgrade delta, visual recipe ID, common effects, exceptional static handlers, and generated catalog coverage for every existing card.
- **Verification:** Legacy-vs-new definition snapshots and behavioral command traces for base/upgraded variants and special hooks.

#### [x] ECS-031: Convert enemies and enemy attacks

- **Owner:** Enemy-content subagent.
- **Dependencies:** ECS-023.
- **May run with:** ECS-030 and ECS-032.
- **File ownership:** New enemy/attack definition directories and tests only.
- **Deliverables:** Enemy definitions, phase data, arsenals, attack definitions, conditions, impact effects, planning metadata, visuals, and exceptional static handlers.
- **Verification:** Catalog completeness; deterministic planning traces; attack requirement, damage, passive, phase, and special-condition parity tests.

#### [x] ECS-032: Convert equipment and medals

- **Owner:** Equipment/medal content subagent.
- **Dependencies:** ECS-023.
- **May run with:** ECS-030 and ECS-031.
- **File ownership:** New equipment/medal definition directories and tests only.
- **Deliverables:** Definitions, slots, triggers, stat modifiers, replacement effects, alternate play providers, activation rules, visual recipe IDs, and exceptional static handlers.
- **Verification:** Definition snapshots and behavioral traces for every equipment and medal item.

#### [x] ECS-033: Content catalog integration gate

- **Owner:** Coordinator plus verification subagent.
- **Dependencies:** ECS-030 through ECS-032.
- **Deliverables:** Resolve generated catalog diagnostics and ensure all old factory keys map exactly once to stable new IDs.
- **Gate:** No missing content IDs; all content parity suites pass; domain agents no longer need to instantiate old behavior objects.

### 9.5 Phase E - Parallel system conversion

#### [x] ECS-040: Convert global lifecycle, scenes, input, and UI interaction

- **Owner:** Global/UI systems subagent.
- **Dependencies:** ECS-023; relevant content IDs from ECS-033 where needed.
- **May run with:** ECS-041 and ECS-042.
- **File ownership:** New global, scene-lifecycle, input, and base UI system directories.
- **Deliverables:** Scene-owned tags, persistent entity handling, bulk teardown, scene groups, transition state, player input state, cursor targeting, input contexts, UI reset/click/hover dispatch, modal suppression, and unique globals.
- **Verification:** Scene transition traces, input tests, rotated bounds, Z-order winner, modal/context blocking, and allocation-free steady cursor targeting.

#### [x] ECS-041: Convert deck, card zones, and card gameplay

- **Owner:** Card-gameplay systems subagent.
- **Dependencies:** ECS-023 and ECS-030.
- **May run with:** ECS-040 and ECS-042.
- **File ownership:** New deck/card gameplay systems and tests.
- **Deliverables:** Deck buffers, shuffle/draw/discard/exhaust/hand movement, card spawning, play validation, cost/payment, upgrades, modifiers, alternate play, pledge, recoil, colorless/frozen/sealed/cursed states, and card lifecycle commands.
- **Verification:** Exact ordered-zone traces; deterministic shuffle; duplicate identity; all card behavior tests; zero LINQ/allocation in per-frame card queries.

#### [x] ECS-042: Convert combat, enemies, phases, and queued resolution

- **Owner:** Combat systems subagent.
- **Dependencies:** ECS-023 and ECS-031.
- **May run with:** ECS-040 and ECS-041.
- **File ownership:** New combat/enemy/phase systems and tests.
- **Deliverables:** HP/resources, phase coordinator, enemy planning, attack progress, block assignment, requirements, damage/prevention, defeat flow, passives applied by attacks, ambush, multi-phase enemies, and rules/trigger queue integration.
- **Verification:** Existing combat suites ported; deterministic full-fight event trace; attack-order and block-condition parity; no direct system references.

#### [x] ECS-043: Convert equipment, medals, passives, and stat/replacement systems

- **Owner:** Effects systems subagent.
- **Dependencies:** ECS-033, ECS-041, and stable combat command contracts from ECS-042.
- **May run with:** ECS-044 and ECS-045.
- **File ownership:** New equipment/medal/passive/modifier systems and tests.
- **Deliverables:** Equip state, triggers, provider tables, passive storage, stat aggregation, replacement resolution, once-per-phase/battle tracking buffers, equipment zones, and activation events.
- **Verification:** All equipment/medal/passive tests, ordering/stacking tests, provider precedence, and reset lifetime parity.

#### [x] ECS-044: Convert climb, way station, rewards, saves, achievements, tutorials, and dialogue

- **Owner:** Meta-game systems subagent.
- **Dependencies:** ECS-033 and ECS-040; card/equipment APIs from ECS-041/ECS-043 frozen.
- **May run with:** ECS-043 and ECS-045.
- **File ownership:** New climb/meta/dialogue/tutorial systems and save adapters.
- **Deliverables:** Climb columns/events/shops, way station, run deck/equipment materialization, reward/card-list modals, booster packs, achievements, tutorials, dialogue queues, save DTO-to-runtime spawning, and runtime-to-save extraction.
- **Verification:** Existing meta-game tests, fresh-save flow, deterministic climb fixture, modal interruption behavior, and no old-save migration.

#### [x] ECS-045: Convert presentation, rendering, visual effects, audio requests, and diagnostics

- **Owner:** Presentation systems subagent.
- **Dependencies:** ECS-023 and ECS-040; presentation contracts from ECS-041/ECS-042 frozen.
- **May run with:** ECS-043 and ECS-044.
- **File ownership:** New presentation/render extraction/display/diagnostic systems and tests.
- **Deliverables:** Transform/tween/parallax, render packets, card/hand/player/enemy/HUD displays, tooltips, overlays, visual effects, shader request components, audio event requests, profiler, debug menu, and entity inspector.
- **Verification:** Draw methods are read-only; packet buffers allocate zero after warm-up; Z/layer parity; all relevant snapshot fixtures pass when registered in a new-world fixture host.

#### [x] ECS-046: System conversion completeness audit

- **Owner:** Verification subagent.
- **Dependencies:** ECS-040 through ECS-045.
- **Deliverables:** Compare migration ledgers to new implementation; report every unconverted old system, service mutation, event subscription, entity reference, component lookup, LINQ query, and draw-state mutation.
- **Gate:** Every old runtime responsibility is either mapped to a new implementation or explicitly proven obsolete before cutover.

### 9.6 Phase F - Cutover and deletion

#### [x] ECS-050: Integrate the new world into `Game1`

- **Owner:** Coordinator/integration owner only.
- **Dependencies:** ECS-046.
- **May run with:** None.
- **Deliverables:** Initialize the new world, catalogs, resources, scheduler, scene groups, input, update phases, late presentation phase, render extraction, and draw consumers. Register only new systems.
- **Verification:** Build; title-to-climb-to-battle smoke flow; fresh run; test-fight launch; no old world instantiated.

#### [x] ECS-051: Delete the old ECS and object behavior runtime

- **Owner:** Coordinator with one deletion-audit subagent.
- **Dependencies:** ECS-050 smoke gate.
- **May run with:** Targeted parity repair only after each deletion batch.
- **Deliverables:** Remove old `Entity`, `EntityManager`, `World`, `System`, `SystemManager`, old components, old systems, static event runtime, old queued wrappers, behavior base classes/subclasses, obsolete factories, temporary adapters, and unused imports.
- **Verification:** Repository searches find no old APIs, `Owner` component fields, managed `IComponent` implementations, `GetComponent`, `GetEntitiesWithComponent`, `GetRelevantEntities`, or object behavior instantiation.

#### [ ] ECS-052: Restore full parity after cutover

- **Owner:** Coordinator dispatches isolated failures to the owning domain subagent.
- **Dependencies:** ECS-051.
- **Deliverables:** Fix only conversion regressions identified by tests, traces, snapshots, and smoke fixtures. Keep fixes within the responsible domain.
- **Verification:** All unit, integration, characterization, and visual suites pass with approved unchanged baselines.

### 9.7 Phase G - Performance and completion

#### [ ] ECS-060: Eliminate steady-state allocations and query regressions

- **Owner:** Performance subagent plus owning domain agents for fixes.
- **Dependencies:** ECS-052.
- **Deliverables:** Allocation reports by system/query/event/render path; remove closures, LINQ, transient lists/arrays, query recreation, string formatting, and accidental buffer growth from steady frames.
- **Verification:** Zero ECS-attributable managed bytes per steady frame after warm-up.

#### [ ] ECS-061: Meet cache and throughput gates

- **Owner:** Core-runtime and performance subagents.
- **Dependencies:** ECS-060.
- **Deliverables:** Legacy/new microbenchmark comparison and one hardware-counter capture on the reference platform.
- **Verification:**
  - Two- and four-component 10,000-entity update loops are at least 3x legacy throughput.
  - Data-cache misses per processed entity are at least 50% below legacy in the representative four-component loop.
  - Batched creation, structural changes, and destruction regress no more than 20% from legacy.
  - Queries, event streams, scheduler, command playback, and established-capacity buffers allocate zero bytes.

#### [ ] ECS-062: Meet in-game 120 Hz CPU gates

- **Owner:** Performance and presentation subagents.
- **Dependencies:** ECS-060 and ECS-061.
- **Deliverables:** Release battle/climb reports using fixed fixtures, diagnostics closed, shaders disabled, 1920x1080 logical resolution, fixed seed, and identical reference hardware.
- **Verification:**
  - ECS update P95 is at most 2.0 ms.
  - Combined update plus CPU draw submission P95 is at most 8.33 ms, excluding presentation/v-sync wait.
  - No ECS-attributable Gen0 collections occur after warm-up.
  - No ECS scope exceeds 16.67 ms after warm-up outside explicitly identified content/scene loading barriers.

#### [ ] ECS-063: Final documentation and repository cleanup

- **Owner:** Coordinator/documentation subagent.
- **Dependencies:** ECS-052 and stable final APIs.
- **Deliverables:** Update `AGENTS.md` pointers, architecture, coding standards, build/run checks, profiler documentation, relevant ADRs, and snapshot documentation. Remove superseded ECS guidance and temporary migration ledgers/scripts that no longer provide maintenance value.
- **Verification:** Documentation commands and API examples match compiled code; `CLAUDE.md` remains the existing symlink.

#### [ ] ECS-064: Final acceptance

- **Owner:** Coordinator.
- **Dependencies:** ECS-060 through ECS-063.
- **Verification sequence:**
  1. `dotnet build`
  2. `dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj`
  3. Every fixture and variant from `docs/display-snapshots.md` with `--verify`
  4. `dotnet run -- test-fight hammer skeleton hard`
  5. Fresh normal run with `dotnet run -- new`
  6. Release battle and climb performance fixtures
  7. Repository searches for forbidden old APIs and managed ECS fields
- **Completion condition:** All functional, visual, architectural, allocation, throughput, cache, and CPU-frame gates pass; no required work or compatibility code remains.

---

## 10. Recommended Three-Subagent Waves

The coordinator occupies one collaboration slot, leaving three subagent slots. Use these waves to maximize parallel work without overlapping ownership.

| Wave | Subagent A | Subagent B | Subagent C | Coordinator |
| --- | --- | --- | --- | --- |
| 1 | ECS-000 baseline | ECS-010 core after contract freeze | ECS-011 generators after contract freeze | ECS-001 contracts |
| 2 | ECS-012 benchmarks | ECS-013 queries/commands | ECS-014 buffers/indexes | Merge and contract arbitration |
| 3 | ECS-015 scheduler/events | Core model tests/perf support | Documentation/foundation audit | ECS-016 gate |
| 4 | ECS-020 shared components | ECS-021 definition generator | ECS-022 rule contracts | ECS-023 gate |
| 5 | ECS-030 cards | ECS-031 enemies/attacks | ECS-032 equipment/medals | ECS-033 integration |
| 6 | ECS-040 global/UI | ECS-041 deck/cards | ECS-042 combat/enemies | Contract arbitration |
| 7 | ECS-043 effects | ECS-044 meta-game | ECS-045 presentation | Integration preparation |
| 8 | ECS-046 audit | Targeted missing tests | Cutover rehearsal support | ECS-050/051 cutover |
| 9 | Domain parity repairs | Snapshot repairs | Allocation audit | ECS-052 coordination |
| 10 | ECS-061 core perf | ECS-062 game perf | ECS-063 docs | ECS-064 acceptance |

Do not start a later wave merely because one slot is idle if its dependencies are not frozen.

---

## 11. Test Plan

### 11.1 Core correctness

- Entity creation, destruction, index reuse, and generation increments.
- Stale entity and buffer handle rejection.
- Component add/remove/set and tag add/remove.
- Multi-component batch transitions move once.
- Swap removal updates moved entity locations.
- Chunk growth, empty chunk reuse, and archetype transition caching.
- Enabled/disabled query behavior.
- All/any/none query matching and query cache updates.
- Structural mutation rejection during queries.
- Command recording/playback order and entity references created by a buffer.
- Dynamic-buffer growth, ordering, reuse, and release.
- Unique-index collision and cleanup.
- Scheduler ordering, scene groups, dependency conflicts, and cycle diagnostics.
- Event ordering, nested publication, cycle guard, and multi-frame queue resume.

### 11.2 Model-based testing

Run long randomized sequences against both the new world and a deliberately simple reference model:

- Create/destroy.
- Add/remove/set components and tags.
- Enable/disable.
- Query different masks.
- Record/play commands.
- Mutate dynamic buffers.

After every operation batch, compare alive entities, generations, signatures, component values, tags, enabled state, buffer contents, and query results.

### 11.3 Domain parity

- Snapshot every immutable definition and upgrade transformation.
- Compare old/new command traces for every card hook.
- Compare enemy planning, requirements, attack resolution, phase changes, and conditional effects.
- Compare equipment/medal triggers, provider precedence, replacements, modifiers, and lifetime resets.
- Compare deck order after every move/shuffle/draw/discard/exhaust operation.
- Compare event priority and rules/trigger queue traces.
- Compare scene transitions, modal interruptions, tutorial gates, achievements, and fresh-save materialization.

### 11.4 Rendering and input parity

- Verify every existing visual snapshot and variant.
- Compare render packet order to legacy Z/layer order in focused tests.
- Verify transform, tween, parallax, clipping, tooltip, modal, overlay, and animation timing.
- Verify cursor targeting, rotations, hidden/interactable rules, input contexts, and tutorial suppression.
- Assert draw methods do not change world version, component state, buffers, events, or commands.

### 11.5 Performance testing

- Warm every query, archetype, system, event stream, buffer capacity, and render layer before sampling.
- Report median, P95, maximum, allocations, GC counts, entities, archetypes, chunks, and processed rows.
- Keep legacy and new benchmark definitions semantically identical.
- Store machine/runtime/build metadata with every result.
- Do not compare Debug results to Release results.
- Run hardware counters on the same machine and workload for legacy and new captures.

---

## 12. Failure Modes and Required Handling

| Failure | Required behavior |
| --- | --- |
| Stale `EntityId` or buffer handle | Throw diagnostic in development/tests; safe failure API where explicitly requested by caller |
| Structural write during query | Throw immediately and name the active query/system |
| System dependency cycle | Abort scheduler initialization and print the dependency path |
| Duplicate unique entity | Reject creation/indexing and identify both entities |
| Duplicate/missing content ID | Compile-time generator diagnostic |
| Managed field in component | Compile-time analyzer error |
| Component/tag count over 512 | Compile-time generator error |
| Query requests more than eight returned components | Compile-time diagnostic; split hot/cold state rather than adding random per-row lookups |
| Event cascade exceeds guard | Stop resolution and report recent event/system chain |
| Dynamic buffer generation mismatch | Reject access; never expose another buffer's storage |
| Catalog/resource ID missing | Fail during catalog/resource validation before entering gameplay |
| Performance regression | Keep task incomplete; profile and correct before final acceptance |
| Parity disagreement | Treat legacy characterization as authoritative unless the plan is explicitly revised |

---

## 13. Completion Checklist

- [ ] No `Entity` reference class remains in runtime gameplay code.
- [ ] No component owns a dictionary of components.
- [ ] All components are unmanaged structs and all markers are tags.
- [ ] No component contains managed references.
- [ ] No component contains an `Owner` field/property.
- [ ] No old `GetComponent`, `GetEntitiesWithComponent`, or `GetRelevantEntities` APIs remain.
- [ ] All per-frame ECS iteration uses cached chunk/span queries.
- [ ] All structural mutation inside systems uses command buffers.
- [ ] All variable-length ECS state uses world-owned dynamic buffers or immutable catalogs.
- [ ] All entity relationships use generation-checked `EntityId`.
- [ ] Static global event state is removed.
- [ ] Only active scene groups execute.
- [ ] Cards, enemies, attacks, equipment, and medals use generated definition modules/catalogs.
- [ ] Per-instance behavior objects, delegates, and behavior factories are removed.
- [ ] Draw paths are read-only and consume reusable render packets.
- [ ] Services remain read-only.
- [ ] Old ECS and temporary migration adapters are deleted.
- [ ] Full tests and snapshots pass without intentional baseline changes.
- [ ] ECS steady-state allocation is zero after warm-up.
- [ ] Microbenchmark throughput/cache gates pass.
- [ ] Battle and climb 120 Hz CPU gates pass.
- [ ] Architecture, standards, build, profiling, and snapshot documentation reflect the final runtime.

---

## 14. Assumptions

- A dedicated long-lived conversion branch is acceptable.
- Exact parity takes precedence over opportunistic cleanup.
- The current developer machine is the reference performance machine unless the project explicitly records a replacement before ECS-000.
- Runtime-generated component IDs are not save contracts.
- Existing save compatibility is intentionally discarded; save DTOs still require correct fresh-run persistence behavior.
- Catalogs and resource stores may use managed memory because they are not ECS component columns; their hot lookups must still be indexed and allocation-free.
- MonoGame draw submission remains single-threaded.
- The component/tag hard limit of 512 is sufficient after marker conversion and hot/cold splitting; exceeding it requires a deliberate architecture revision.
- The completed migration lands only after old-runtime removal and every final gate passes.
