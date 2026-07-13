# Data-oriented ECS foundation runtime

This is the implementation-facing reference for the foundation currently compiled under
`Crusaders30XX.ECS.DataOriented`. The approved conversion plan and `contracts.md` remain
normative. This document records the ECS-010 through ECS-015 surface audited on
2026-07-13 and the centralized ECS-016 foundation signoff.

## Compiled surface

| Area | Current API and behavior |
| --- | --- |
| Type metadata | `IComponent`, `ITag`, `ComponentTypeRegistry`, `ComponentType<T>`, and the generated `GeneratedComponentRegistry`. Registration is sealed before `World` construction. Components are constrained to unmanaged structs; tags are empty unmanaged structs; the generator/analyzer enforces the shared 512-type limit. |
| Entity storage | `EntityId`, `SpawnBundle`, `World.Create`, `IsAlive`, `Has`, `Get`, `TryGet`, `Set`, `Add`, `AddTag`, `Remove`, batch `Transition`, `Enable`, `Disable`, single/bulk `Destroy`, and signature-filtered teardown. Entity index zero is reserved and reused indexes receive a new generation. |
| Archetypes/chunks | Eight-word `ComponentSignature`, 16 KiB row-budget capacity clamped to 1-1024, typed component arrays, tag-only signature bits, cached single-type transitions, one-move batch transitions, swap removal, and empty-chunk reuse. |
| Queries | `World.Query<T1...T8>`, generated query descriptors, cached matching-archetype arrays, incremental matching when an archetype is created, `All`/`Any`/`None`, and optional disabled inclusion. Query chunks expose entity/component spans and `QueryChunk.Rows`. |
| Deferred structure | Reusable `CommandBuffer` commands for create, destroy, enable/disable, add/set/remove component, add/remove tag, batch transition, deferred-entity references, and typed dynamic-buffer mutations. Playback retains record order. |
| Variable-length state | Generation-checked `DynamicBufferHandle<T>`, typed buffer spans and ordered mutation, established-capacity reuse, owner cleanup on world destruction, mutation-command handlers, and allocating debug snapshots. |
| Indexes/resources | `UniqueIndex<TKey>`, world-integrated `RegisterUnique<TTag>`, `GetUnique<TTag>`, and `TryGetUnique<TTag>` with duplicate rejection and destruction cleanup, `EntityNameIndex`, `StringTable`, and integer-backed `StringId`, `TextureAssetId`, `SoundId`, and `VisualEffectRecipeId`. |
| Scheduling | `IGameSystem`, reusable per-system command buffers, descriptors with component/buffer/event access metadata and dependencies, frozen phase and scene enums, cached execution arrays, dependency/conflict/cycle validation, enforced `RecordsStructuralCommands`, descriptor-declared event barriers, and optional per-system time/allocation profiles. |
| Events/rules | A world-owned `EventRuntime`, reusable unmanaged `EventStream<T>` arrays, explicit typed routes with stable priority order, nested event waves, a configurable cascade guard, mandatory and reactive-trigger FIFO lanes, and unmanaged queued state that resumes after a `Pending` result. |
| Verification harness | The standalone Release benchmark runner loads the ECS-000 JSON baseline, reports paired raw/empty/net timing and allocations, and supplies randomized reference-model infrastructure for lifecycle, components/tags, enable/disable, queries, command playback, and dynamic buffers. |

## Required usage invariants

- Cache a query instance during system initialization. Calling `World.Query` creates a
  new query cache; it is not a per-frame operation.
- Iterate query rows through `QueryChunk.Rows`. `Entities`, component spans, and `Count`
  describe physical chunk rows and therefore include disabled rows even when the query's
  default filter excludes them. `Rows` is the filtering iterator.
- Dispose/enclose query enumerators through normal `foreach` before direct structural
  writes. An active iteration rejects direct create, destroy, enable/disable,
  add/remove/tag, transition, and bulk teardown with the query debug name.
- Record structural changes discovered during iteration in the reusable command buffer
  and play them after the iteration/system barrier.
- Create mutable variable-length state through `World.CreateDynamicBuffer`; the owning
  live `EntityId` is validated and destruction releases every owned typed buffer.
- Treat generated component IDs as build-local. Domain/save identifiers remain the
  explicit stable IDs frozen in `ECS/Data/Ids/GameIds.cs`.
- System command buffers play back after every sequential system. `AfterSystem` drains
  events immediately after that playback; `AfterPhase` drains once after the phase.
- Mandatory queued rules take precedence over reactive triggers. A pending rule retains
  its unmanaged head state and resumes on a later `Process` call.
- Debug snapshots and string/index catalog initialization may allocate. Warmed query
  iteration, established-capacity buffer/event/rule operations, and scheduler execution
  with profiling either disabled or enabled are allocation-free paths asserted by tests.

## Verification commands

Run from the repository root:

```bash
dotnet build

dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj \
  --filter FullyQualifiedName~DataOriented

dotnet test tests/Crusaders30XX.ECS.Generators.Tests/Crusaders30XX.ECS.Generators.Tests.csproj

dotnet test tests/Crusaders30XX.ECS.Benchmarks.Tests/Crusaders30XX.ECS.Benchmarks.Tests.csproj \
  -c Release

scripts/audit-data-oriented-ecs-ledgers.rb

dotnet run --project tests/Crusaders30XX.ECS.Benchmarks/Crusaders30XX.ECS.Benchmarks.csproj \
  -c Release -- \
  --quick \
  --legacy-baseline tests/PerformanceBaselines/legacy-ecs-initial.json \
  --output debug/performance/ecs-foundation-quick.json
```

## ECS-016 signoff results

Centralized verification passed:

- The serialized solution build completed with only two pre-existing unused-local
  warnings.
- All 56 focused `DataOriented` tests passed, including world unique-tag ownership,
  structural-command declaration enforcement, world-owned events, and warmed scheduler
  allocation checks with profiling enabled and disabled.
- All 15 generator/analyzer tests and all 16 Release benchmark/model tests passed.
- The migration ledger audit passed for all 1,663 entries.

The Release quick artifact at `debug/performance/ecs-foundation-quick.json` reported:

| Cached-query workload | Raw median | Processed rows | Allocation/collections | Checksum |
| --- | ---: | ---: | --- | --- |
| 2 components | ~0.359 ms | 80,000 | 0 bytes; Gen0/1/2 all 0 | Stable |
| 4 components | ~0.673 ms | 80,000 | 0 bytes; Gen0/1/2 all 0 | Stable |

This quick capture is a deterministic smoke check, not the retained throughput gate for
the later performance phase. No hard ECS-016 blocker remains.

## Risks and follow-up work

- Raw query chunk spans make disabled physical rows visible. This is not a failure of
  the tested `foreach (row in chunk.Rows)` path, but domain systems must not loop over
  `Count` directly unless `IncludeDisabled` is intentional. Revisit the exposure before
  broad domain conversion if misuse becomes likely.
- The frozen contracts name `ReadOnlyWorld`, while foundation `SystemContext.World`
  currently exposes mutable `World`. The approved plan assigns the read-only view and
  handler contexts to ECS-022, so this is required follow-up shared-rule work rather than
  an ECS-016 blocker; until then, descriptor access declarations are not a write barrier.
- The retained ECS-000 performance artifact is a CPU-only legacy fixture and marks draw
  submission unsupported. In-game draw/update gates remain ECS-062 work.
- Current microbenchmarks include contiguous-array validation, direct storage, and
  cached-query access. Structural-command and established-capacity buffer/event/scheduler
  benchmark workloads still need retained comparison coverage before later
  throughput/allocation gates; this is benchmark evolution, not domain conversion.
- Adding player, deck, scene, and other domain-specific unique tags is later shared/domain
  schema work.
