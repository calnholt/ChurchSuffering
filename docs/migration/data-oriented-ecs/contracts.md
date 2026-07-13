# Frozen Data-Oriented ECS Contracts

This document freezes the shared contracts required by ECS-001. The approved conversion
plan remains authoritative for detailed behavior and acceptance gates.

## Namespace map

| Concern | Namespace |
| --- | --- |
| Entity, world, signature, archetype, chunk, query, command buffer | `Crusaders30XX.ECS.DataOriented.Core` |
| Scheduler, descriptors, contexts, phases, scene groups | `Crusaders30XX.ECS.DataOriented.Systems` |
| Typed events, queues, routing | `Crusaders30XX.ECS.DataOriented.Events` |
| Shared/domain component structs and tags | `Crusaders30XX.ECS.DataOriented.Components` |
| Dynamic buffers and indexes | `Crusaders30XX.ECS.DataOriented.Storage` |
| Definition modules, catalogs, effects, conditions | `Crusaders30XX.ECS.DataOriented.Definitions` |
| Compact asset IDs and resource stores | `Crusaders30XX.ECS.DataOriented.Resources` |
| Render extraction and render packets | `Crusaders30XX.ECS.DataOriented.Rendering` |
| Generated implementation details | `Crusaders30XX.ECS.DataOriented.Generated` |

The legacy `Crusaders30XX.ECS.Core` namespace must not be referenced from the replacement
runtime. Shared immutable domain enums currently in `Crusaders30XX.ECS.Data.Ids` may be
used during conversion and move mechanically at cutover without changing their values or
keys.

## Public runtime contracts

- `EntityId` is `public readonly record struct EntityId(int Index, int Generation)`.
  Index zero is null and is never assigned; stale generations are rejected.
- `IComponent` and `ITag` are marker interfaces. Implementations are structs; component
  implementations satisfy `unmanaged`; tags have no instance fields.
- `ComponentSignature` contains exactly eight `ulong` words. Components and tags share
  the 512-type build-local limit.
- `DynamicBufferHandle<T>` is a generation-checked readonly record struct constrained to
  unmanaged element types.
- `World`, queries of arity one through eight, `SpawnBundle`, `CommandBuffer`,
  `SystemContext`, `ReadOnlyWorld`, `SystemDescriptor`, and `IGameSystem` expose the
  operations and semantics in sections 5.4 through 5.10 of the plan.
- Direct structural writes during active query iteration throw in every configuration.
  System command buffers play back after each sequential system.
- Runtime generic access uses generated numeric metadata. Per-row reflection, hashing,
  dictionaries, interface enumeration, LINQ, and allocation are forbidden.

## Frozen scheduler phases

The serialized execution order is:

1. `Input`
2. `Interaction`
3. `Rules`
4. `Gameplay`
5. `Presentation`
6. `LatePresentation`
7. `RenderExtraction`

Event-wave barriers and command-buffer playback occur at descriptor-declared barriers;
command playback always occurs after each system. Draw is outside scheduler state mutation
and consumes extracted packets only.

## Frozen scene groups

`Global`, `TitleMenu`, `WayStation`, `Climb`, `Battle`, `Achievement`, and `Snapshot` are
the complete initial scene-group set. `Global` runs with every active scene. Exactly one
non-global scene group is active during ordinary runtime updates. Inactive scene systems
are absent from cached execution arrays rather than invoked and asked to return early.

## Stable IDs

The names, explicit `ushort` numeric values, declaration order, and `ToKey()` values of `CardId`, `EnemyId`,
`EnemyAttackId`, `EquipmentId`, and `MedalId` in `ECS/Data/Ids/GameIds.cs` are frozen.
They are domain/save identifiers, not generated ECS type identifiers. Existing values
must never be reordered or renamed; new values append only after a coordinator contract
update. `stable-domain-ids.csv` is the machine-audited frozen snapshot.

The following compact IDs are also reserved contracts and will be implemented as
unmanaged integer-backed value types or enums: `StringId`, `TextureAssetId`, `SoundId`,
`VisualEffectRecipeId`, and IDs for any catalog introduced by a later domain task.
`default` is invalid/null for value-type resource handles unless a domain enum explicitly
documents a valid zero member.

## Ledger ownership

The CSV ledgers beside this document assign every legacy type/subscription to exactly one
task. They are generated snapshots, not a source-code generation input. Run:

```bash
scripts/audit-data-oriented-ecs-ledgers.rb
```

If an intentional legacy declaration changes before its domain conversion, regenerate the
ledgers with `--write`, review the assignment diff, and obtain coordinator approval before
continuing.
