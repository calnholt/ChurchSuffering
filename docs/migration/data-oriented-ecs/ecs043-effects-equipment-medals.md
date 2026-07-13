# ECS-043 equipment, medals, passives, and replacements

Status: implemented in `ECS/DataOriented/Gameplay/Effects/`.

The runtime stores passives and once-only trigger state in world-owned dynamic buffers. Equipment and medal entities carry compact generated catalog IDs plus their shared usage/counter state. Provider resolution reads caller-owned equipped-medal spans: alternate play and replacement select the lowest active equipped `EntityId`, while card-stat modifiers accumulate. Generated equipment and medal handlers remain the only behavior tables; this lane publishes their `RuleCommand` values as typed hand-offs.

The root event composition path is `EffectGameplayEventHub.BuildRoutes()`. It does not attach a second `EventRuntime`. `BuildLedgerRoutes()` exposes the twenty ECS-043-owned ledger routes separately for audit and composition tests.

## Component rows (11)

| Legacy row | Data-oriented mapping |
|---|---|
| AppliedPassives | `AppliedPassives` + sorted `DynamicBufferHandle<PassiveEntry>` |
| EquipmentHighlightSettings | Shared hot component already supplied by ECS-040 in `ECS.DataOriented.Components`; reused without duplication |
| EquipmentZone | `EquipmentZone` |
| EquippedEquipment | `EquippedEquipment` with `EquipmentId` and `EquipmentUsageState` |
| EquippedMedal | `EquippedMedal` with `MedalId` and `MedalRuntimeState` |
| EquippedTemperanceAbility | `EquippedTemperanceAbility` |
| PassiveMeterComponent | `PassiveMeterComponent` |
| TemperanceTooltipAnchor | `TemperanceTooltipAnchor` tag |
| EquipmentDisplayRoot | `EquipmentDisplayRoot` tag |
| EquipmentTooltipSource | `EquipmentTooltipSource` |
| EquipmentTooltipState | `EquipmentTooltipState` |

`EffectTriggerTracking` is a domain-local buffer-handle component used for once-per-phase and once-per-battle state.

## Event rows (20)

Direct typed events are `EquipmentAbilityTriggered`, `EquipmentActivated`, `EquipmentDestroyed`, `EquipmentUseResolved`, `MedalTriggered`, `EquipmentActivateEvent`, `MedalActivateEvent`, `ApplyPassiveEvent`, `FrostbiteTriggered`, `PassiveTriggered`, `RemoveAllPassives`, `RemovePassive`, `TribulationTriggered`, `UpdatePassive`, `PoisonDamageEvent`, `ModifyTemperanceEvent`, `SetTemperanceEvent`, and `TriggerTemperance`.

The legacy `IReplacementEffectProvider` row maps to the generated/shared `ProviderSource` and `MedalProviderRules` contract; no runtime interface or object registry remains. `ReplacementEffectAction` maps to shared `ReplacementAction`. `ReplacementEffectResolved` is the fixed-size typed event form of the shared `ReplacementPlan`. `EffectRuleCommandEvent` is the twentieth owned route and hands generated catalog output to cross-domain executors. Temperance resolution and draw requests are supplemental domain-local routes.

## System rows (12) and scheduled ownership

The twelve legacy identities are `AppliedPassivesManagementSystem`, `BleedManagementSystem`, `BrittleManagementSystem`, `EquipmentBlockInteractionSystem`, `EquipmentManagerSystem`, `IntimidateManagementSystem`, `MedalManagerSystem`, `PoisonSystem`, `ReplacementEffectSystem`, `ScorchedManagementSystem`, `TemperanceManagerSystem`, and `VigorManagementSystem`. They implement `IUnscheduledEffectLedgerSystem` and are audit-only; `EffectGameplayComposition` never schedules them.

ECS-043 has no steady per-frame work, so `EffectGameplayComposition.Systems` is intentionally empty. Scheduling route-only owners would add four no-op updates. Instead, the root event graph owns four typed consumers:

- `PassiveEffectRuntimeSystem` owns applied-passive storage plus Bleed, Brittle, Intimidate, Poison, Scorched, and Vigor passive rule state.
- `EquipmentEffectRuntimeSystem` owns activation/usage, block-zone hand-off, reset, and generated equipment command publication.
- `MedalEffectRuntimeSystem` owns medal counter/trigger dispatch, stable provider ordering, and first-wins replacement resolution.
- `TemperanceEffectRuntimeSystem` owns clamped temperance changes and the eight folded temperance behavior rows.

They expose no direct system references; cross-system work is typed event/command data. Their state transitions execute during the root event barrier, and none implements `IGameSystem`.

## Subscription rows (29)

| Consumer | Legacy subscriptions represented by typed inputs/rules |
|---|---|
| AppliedPassivesManagementSystem | ApplyEffect, ApplyPassiveEvent, CardPlayed, ChangeBattlePhase, EnemyKilled, LoadScene, RemoveAllPassives, RemovePassive, UpdatePassive |
| BleedManagementSystem | ConfirmBlocksRequested |
| BrittleManagementSystem | CardBlocked |
| EquipmentManagerSystem | EnemyKilled, EquipmentActivateEvent |
| IntimidateManagementSystem | BeginDefeatPresentation, ChangeBattlePhase, EnemyPhaseReset, Intimidate |
| MedalManagerSystem | MedalActivateEvent |
| PoisonSystem | PassiveTriggered, TutorialCompleted, TutorialStarted, UpdatePassive |
| ReplacementEffectSystem | ReplaceableEffectRequest |
| ScorchedManagementSystem | PledgeAdded |
| TemperanceManagerSystem | CardMoved, LoadScene, ModifyTemperanceEvent, SetTemperanceEvent |
| VigorManagementSystem | CardPlayed |

These total 29 ledger rows. Existing ECS-040 through ECS-042 producers publish the cross-lane card/combat/scene inputs; ECS-043 owns only the effects-side state transition and typed output contracts.

## Folded object behaviors (8)

`TemperanceBase` is folded into `TemperanceAbilityDefinition`/`TemperanceAbilityCatalog`. The seven static entries preserve legacy thresholds and effects: Angelic Aura (2, Aegis 3), Fling Fling (3, two white Kunai), Iron Resolve (3, Vigor 1), Measured Breath (3, draw 1), Radiance (4, enemy Stun 1), Static Surge (3, Galvanize 1), and Unsheath (3, Sharpen 5).

## Determinism and resets

- Passive buffers remain sorted by `EffectId`; stack updates and lifetime resets allocate no per-frame objects.
- Equipment usage and medal counters use `EquipmentMedalStateRules` and refresh from battle epochs.
- Provider eligibility requires active, same-owner equipment state. First-wins paths compare entity index then generation; stat paths accumulate every applicable provider.
- Olaf replacement handling suppresses original Frostbite threshold damage even when no redirect target exists; an eligible primary enemy receives the generated `-3` effect-damage action.
- Phase/battle trigger tracking records source, semantic trigger, lifetime, and epoch in a reusable buffer.
