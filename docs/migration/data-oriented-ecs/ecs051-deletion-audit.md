# ECS-051 deletion audit

Status: the dependency cut is complete for the production and test projects. The working tree
contains 701 deleted legacy production `.cs` files and 153 retired legacy/parity test `.cs` files. The
explicit compile allowlists now contain only the data-oriented runtime, its generator, and the
small external host-support island listed below.

This audit records the dependency boundary after the `Game1` cutover. It does not treat stable
domain IDs, MonoGame hardware capture, launch parsing, display metrics, or PNG comparison as an
ECS compatibility layer.

## Deleted production inventory

The following paths are deleted in full. Counts are the tracked C# files at the deletion boundary.

| Path | Files | Responsibility removed |
| --- | ---: | --- |
| `ECS/Core/**/*.cs` | 10 | `Entity`, `EntityManager`, object `World`, `System`, `SystemManager`, managed `IComponent`, static `EventManager`, `EventQueue`, `EventQueueBridge`, and `TimerScheduler` |
| `ECS/Components/**/*.cs` | 16 | All class components and component-owned `Entity Owner` references |
| `ECS/Events/**/*.cs` | 33 | Old class event payloads and static-bus contracts |
| `ECS/Factories/**/*.cs` | 10 | Entity/card/enemy/equipment/medal/temperance factories |
| `ECS/Objects/**/*.cs` | 183 | `CardBase`, enemy/attack/equipment/medal/achievement behavior bases and subclasses |
| `ECS/Scenes/**/*.cs` | 246 | Old scene parents, update systems, display systems, and draw/state behavior |
| `ECS/Systems/**/*.cs` | 8 | Remaining root legacy systems |
| `ECS/Services/**/*.cs` | 70 | Legacy read/write helpers and object-runtime services |
| `ECS/Singletons/**/*.cs` | 3 | Font, state, and way-station mutable singletons |
| `ECS/Utils/**/*.cs` | 6 | Utilities used only by deleted object-runtime presentation code |
| `ECS/Data/**/*.cs` except `ECS/Data/Ids/GameIds.cs` | 34 | Old save/cache, loadout, location, tutorial, dialogue, visual-effect, achievement, and telemetry models |
| `ECS/Diagnostics/Snapshots/Fixtures/**/*.cs` | 33 | Legacy-world snapshot authoring |
| `ECS/Diagnostics/Snapshots/{DisplaySnapshotContext,DisplaySnapshotHost,DisplaySnapshotRegistry,IDisplaySnapshotFixture}.cs` | 4 | Legacy snapshot world/fixture host |
| `ECS/Diagnostics/{BoundedHistogram,DebugAttributes,FrameProfiler,GpuProfiler,TestFightRuntime}.cs` | 5 | Diagnostics coupled to the object system manager/scenes |
| `ECS/Input/{CursorTarget,HotKeyHoldTracker,InputContextResolver,PlayerInputService}.cs` | 4 | Legacy ECS input targeting and input system |
| `ECS/Rendering/**/*.cs` except `DisplayMetrics.cs` | 36 | Legacy immediate-mode overlays, card render models, pools, compositors, and UI helpers |

These groups total 701 production files. The central runtime island was cyclic: components,
events, factories, objects, services, scenes, and systems referenced one another, so trying to
remove `Core` first would never compile. The explicit project allowlist made the whole island one
reversible deletion batch instead.

## Deleted test inventory

All 148 tracked C# files directly under `tests/Crusaders30XX.Tests/` were legacy-runtime tests and
are deleted. The two migration-only `LegacyEcsCharacterizationTests.cs` and
`LegacyEcsPerformanceFixtureTests.cs` sources and three superseded source-parity tests under
`DataOriented/Content` are also removed, for 153 retired test/parity files total. The historical
performance result remains as data under `tests/PerformanceBaselines/`; the benchmark projects
consume that artifact and do not compile the old runtime.

The test project now compiles exactly:

```text
tests/Crusaders30XX.Tests/DataOriented/**/*.cs
tests/Crusaders30XX.Tests/DataOrientedStorageTests.cs
tests/Crusaders30XX.Tests/Ecs020ComponentLayoutTests.cs
tests/Crusaders30XX.Tests/Ecs020ComponentRoundTripTests.cs
```

That is 27 domain/integration files plus three foundation files at this audit point. Generator and
benchmark projects are independent and remain; their `legacy` terminology refers to committed
baseline JSON or a reference model, not to the deleted object ECS.

## Required external host/support island

The following 18 non-data-oriented C# files remain in the main project allowlist.

| Files | Why they remain | Deletion condition |
| --- | --- | --- |
| `ECS/Data/Ids/GameIds.cs` | Frozen save/content IDs used throughout generated catalogs and gameplay. It is data, not an ECS component API. | May be moved mechanically to a neutral/data-oriented namespace; numeric values and keys must not change. |
| `ECS/Input/MonoGamePlayerInputAdapter.cs`, `IPlayerInputSource.cs`, `PlayerInputFrame.cs`, `GamepadRumbleMixer.cs`, `RumbleGroup.cs`, `RumbleModels.cs` | Sole MonoGame keyboard/mouse/gamepad hardware boundary and its local input/rumble value types. `Game1` captures from it; no ECS system polls hardware. | Make the MonoGame adapter emit the data-oriented host snapshot/frame directly, then remove `CentralInputFrameAdapter` aliases and this legacy-namespace island. |
| `ECS/Rendering/DisplayMetrics.cs` | Pure 1920x1080 logical-canvas/letterbox calculation used by `Game1`. | Retain as host support or move to a neutral host namespace. |
| `ECS/Diagnostics/CardListProfileLaunchOptions.cs`, `GpuProfilingRuntimeOptions.cs`, `NewGameLaunchOptions.cs`, `ShaderRuntimeOptions.cs`, `TestFightLaunchOptions.cs`, `TutorialLaunchOptions.cs`, `UnlockLaunchOptions.cs` | Pure command-line host parsing. `TestFightLaunchOptions` now validates against stable `EnemyId` and the generated catalog; it no longer uses `EnemyFactory` or `RunDifficulty`. | Retain as host support or consolidate under the new host namespace. |
| `ECS/Diagnostics/Snapshots/DisplaySnapshotBaselineComparer.cs`, `DisplaySnapshotLaunchOptions.cs`, `DisplaySnapshotSetupException.cs` | PNG path/comparison and launch parsing used by the new-world snapshot materializer in `Game1`. They do not construct a world or fixture. | Retain until the unchanged-baseline ECS-052 verification is complete; they can then remain as generic snapshot tooling. |

`Program.cs` no longer imports `ECS.Data.Save` or calls `SaveCache`. Fresh/unlock launch flags are
handled without pulling the deleted factories, services, events, or save object graph back into
the build. `TestFightLaunchOptions` likewise no longer imports `ECS.Factories` or
`ECS.Singletons`. Those were the two compile roots that previously kept the legacy island alive.

## Residual namespace dependencies

- `ECS/DataOriented` has zero references to `ECS.Core`, `Components`, `Systems`, `Events`,
  `Factories`, `Objects`, `Scenes`, `Services`, or `Singletons`.
- `Program.cs` has zero references to those namespaces and zero references to `ECS.Data.Save`.
- `ECS/DataOriented/Integration/Host/CentralInputFrameAdapter.cs` has the only four references to
  `Crusaders30XX.ECS.Input` (aliases for device, frame, button, and mask). This is the temporary
  MonoGame input seam described above, not an entity/component bridge.
- The replacement runtime still imports `Crusaders30XX.ECS.Data.Ids` in 95 production files. The
  contract explicitly freezes those domain IDs independently of ECS type IDs. Moving the file and
  updating the namespace is mechanical cleanup, not a prerequisite for deleting the old runtime.

## Forbidden API audit

Against `ECS/DataOriented`, `Game1.cs`, and `Program.cs`, all semantic counts are zero:

| Pattern | Count |
| --- | ---: |
| `GetComponent<T>` | 0 |
| `GetEntitiesWithComponent` | 0 |
| `GetRelevantEntities` | 0 |
| static `EventManager` usage | 0 |
| managed `class ... : IComponent` | 0 |
| legacy core/domain namespace reference | 0 |

The new runtime legitimately has unmanaged `EntityId Owner` fields. A raw search for the token
`Owner` is therefore not a valid old-component check. Search specifically for managed component
implementations and `Entity Owner` instead. Audit tests also contain forbidden names as string or
regex assertions; those are evidence, not API use.

Reproducible searches:

```bash
rg -n 'GetComponent<|GetEntitiesWithComponent|GetRelevantEntities' \
  ECS/DataOriented Game1.cs Program.cs -g '*.cs'

rg -n '\bEventManager\b|class .*: .*IComponent|\bEntity Owner\b' \
  ECS/DataOriented Game1.cs Program.cs -g '*.cs'

rg -n 'Crusaders30XX\.ECS\.(Core|Components|Systems|Events|Factories|Objects|Scenes|Services|Singletons)' \
  ECS/DataOriented Game1.cs Program.cs -g '*.cs'

rg -n 'Crusaders30XX\.ECS\.Input' ECS/DataOriented -g '*.cs'
rg -n 'Crusaders30XX\.ECS\.Data\.Save|SaveCache' Program.cs Game1.cs ECS/DataOriented -g '*.cs'
```

## Recommended deletion batches

The executed dependency-safe sequence is:

1. Sever the two executable roots: replace `SaveCache` calls in `Program`, and change test-fight
   parsing from `EnemyFactory`/the singleton difficulty enum to stable generated definitions.
2. Put the main and test projects on explicit new-runtime allowlists. Build to prove the old island
   is no longer a compile dependency.
3. Delete all tracked top-level legacy tests. Preserve the committed performance baseline artifact.
4. Delete the 37-file legacy snapshot fixture/host island.
5. Delete the cyclic 585-file core/component/event/factory/object/scene/system/service/singleton/
   utility island together with the 34 obsolete data files, five obsolete diagnostics files, four
   obsolete ECS-input files, and 36 obsolete rendering files.
6. Build and run the data-oriented test allowlist. The completed verification is a zero-warning,
   zero-error solution build and 266/266 passing new-runtime tests. Repository searches above must
   remain empty.
7. Later, rehome or replace the 18-file external support island. The hardware adapter, display
   metrics, launch parsers, snapshot comparer, and stable IDs are still live host dependencies.

## Blockers and verdict

There is no blocker to the ECS-051 old-runtime deletion: neither executable nor test project
compiles a legacy entity, component, system, static event runtime, object behavior, factory, or
legacy snapshot fixture. The only residual old-named dependency is the deliberately isolated
MonoGame input boundary, plus the frozen domain-ID namespace. Removing those paths entirely
requires namespace/host cleanup, but not gameplay conversion or dual-world compatibility.
