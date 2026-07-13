# ECS-044 meta-game mapping

This document is the authoritative conversion map for ECS-044. The row keys remain in the five
canonical CSV ledgers in this directory; this document identifies the new owners and the scheduler
boundary used by the cutover audit.

## Root composition contract

`MetaGameComposition.Create` returns route fragments only. `MetaGameEventHub.BuildRoutes` returns
all 80 ECS-044 routes in stable ledger order with IDs `44001` through `44080`; it never creates or
attaches an `EventRuntime`. `MetaGameCrossDomainRoutes` supplies explicit registrations to the
global/UI, card, combat, and effect hubs that own canonical cross-domain streams.

The root scheduler allowlist is exactly:

- `ClimbRuntimeSystem`
- `WayStationRuntimeSystem`
- `RewardRuntimeSystem`
- `AchievementRuntimeSystem`
- `TutorialRuntimeSystem`
- `DialogueRuntimeSystem`
- `RunLifecycleRuntimeSystem`

Each allowlisted system has a unique `4491`-`4497` ID and declares component/buffer access,
consumed/emitted event IDs, structural writes, scene group, phase, and barrier behavior. The twelve
ledger-named compatibility descriptors (`4401`-`4412`) are exposed only through
`CompatibilitySystems` and must not be scheduled.

## Ledger reconciliation

| Ledger | Rows | New authoritative owner |
|---|---:|---|
| `components.csv` | 50 | Same-named unmanaged declarations in `MetaGameComponents.cs`; variable data uses eight world-owned buffer element types. |
| `events.csv` | 80 | Same-named unmanaged contracts and streams in `MetaGameEvents.cs`; `BuildRoutes` contains one route per row. |
| `systems.csv` | 12 | Same-named unscheduled descriptors in `MetaGameComposition.CompatibilitySystems`, mapped below to operational owners. |
| `event-subscriptions.csv` | 64 | Typed local consumers in `MetaGameComposition` or typed canonical-hub registrations in `MetaGameCrossDomainRoutes`, grouped below. |
| `object-behaviors.csv` | 22 | `GeneratedMetaObjectCatalog`: nineteen achievement definitions, `EventBase` catalog helper, and the `IceboundTithe`/`PrunedVocation` narrative definitions. |

The component count intentionally excludes buffer elements, DTOs, catalog records, and events.
Save DTOs are boundary objects rather than ECS state. Only save version 1 is accepted; no old-save
migration or compatibility branch exists.

## System rows

| Legacy ledger name | Unscheduled descriptor | Operational owner |
|---|---:|---|
| `AchievementExplosionSystem` | 4401 | `AchievementRuntimeSystem` |
| `AchievementSceneSystem` | 4402 | `AchievementRuntimeSystem` |
| `GuidedTutorialDirectorSystem` | 4403 | `TutorialRuntimeSystem` |
| `TutorialManager` | 4404 | `TutorialRuntimeSystem` |
| `ClimbSceneSystem` | 4405 | `ClimbRuntimeSystem` |
| `WayStationClimbSettingsModalSystem` | 4406 | `WayStationRuntimeSystem` |
| `WayStationDialogueSystem` | 4407 | `DialogueRuntimeSystem` |
| `WayStationSaintsMedalsModalSystem` | 4408 | `WayStationRuntimeSystem` |
| `ClimbEncounterSystem` | 4409 | `ClimbRuntimeSystem` |
| `ClimbEventSystem` | 4410 | `ClimbRuntimeSystem` and `DialogueRuntimeSystem` |
| `CollectionProgressionSystem` | 4411 | `AchievementRuntimeSystem`, `RewardRuntimeSystem`, and achievement trigger consumers |
| `RunDeckLifecycleSystem` | 4412 | `RunLifecycleRuntimeSystem` and `MetaSaveAdapter` |

## Subscription rows

All 64 rows are retained. The grouping below is exhaustive; counts sum to 64. The exact source,
event, and occurrence key for every row remains authoritative in `event-subscriptions.csv`.

| Legacy source group | Rows | Typed route owner |
|---|---:|---|
| `QuestScopedCardModificationCleanup` | 1 | `RewardRuntimeSystem` consumes `ShowQuestRewardOverlay`. |
| Nineteen achievement definitions | 26 | `MetaAchievementTriggerConsumer` plus `MetaGameCrossDomainRoutes` card/combat/effect consumers publish typed progress updates. |
| Achievement scene systems | 5 | `AchievementRuntimeSystem` handles reveal/seen/animation state; global scene registrations handle load/prepare/cache events. |
| `GuidedTutorialDirectorSystem` | 4 | `TutorialRuntimeSystem` plus combat/global registrations. |
| `TutorialManager` | 3 | `TutorialRuntimeSystem` plus combat/global registrations. |
| `ClimbSceneSystem` | 3 | `MetaGameCrossDomainRoutes` global load/prepare/cache consumers. |
| Way-station climb modal | 2 | `WayStationRuntimeSystem` handles load-owned state and `OpenWayStationClimbSettingsModalEvent`. |
| Way-station dialogue | 4 | `DialogueRuntimeSystem`; the presentation transition completion is a root presentation hand-off. |
| Way-station saints-medals modal | 2 | `WayStationRuntimeSystem`, registered explicitly on the global/UI hub. |
| `ClimbEncounterSystem` | 1 | `ClimbRuntimeSystem`. |
| `ClimbEventSystem` | 4 | `ClimbRuntimeSystem` and `DialogueRuntimeSystem`; global load stays on the global/UI hub. |
| `CollectionProgressionSystem` | 6 | `AchievementRuntimeSystem`, `RewardRuntimeSystem`, and `MetaAchievementTriggerConsumer`. |
| `RunDeckLifecycleSystem` | 3 | `RunLifecycleRuntimeSystem`. |

## Runtime boundaries

- `DeterministicClimbGenerator` fills caller-owned spans; the generated fixture is stable for a
  seed and forces a terminal encounter in every column.
- `MetaSaveAdapter.Spawn` materializes climb, way-station, run-card, equipped-equipment,
  equipped-medal, and achievement entities. `Extract` emits stable DTO arrays without persisting
  generated type IDs or entity handles.
- Modals and dialogue are separate component state. Opening climb settings interrupts an active
  dialogue and records the modal cause instead of mutating draw state.
- `GeneratedMetaObjectCatalog` replaces all 22 object rows without runtime behavior objects,
  delegates, or reflection dispatch.
- Per-frame systems use cached queries and direct spans. There is no LINQ, direct hardware input,
  static `EventManager`, system injection, or rendering mutation in the meta-game runtime.

## Focused verification

`MetaGameRuntimeTests` covers the deterministic climb vector, fresh-save materialization and
round-trip extraction, rejection of old save versions, modal interruption, dialogue/tutorial/
achievement cascades, cross-domain route composition, exact route/component/object counts, unique
IDs, compatibility isolation, and operational access metadata.
