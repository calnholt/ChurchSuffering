# ECS-042 combat migration mapping

This is the owned reconciliation for every ledger row assigned to ECS-042. The replacement uses world-owned unmanaged components and dynamic buffers, generated enemy catalogs, typed `RuleCommand` execution, and a deterministic mandatory/reactive `QueuedRuleRuntime<CombatRuleState>`. Legacy static subscriptions do not survive the boundary.

## Operational registration and root composition

`CombatGameplayComposition` is the sole ECS-042 scheduler allowlist. It registers exactly two
systems: `AttackResolutionSystem`, the Rules-phase owner of `CombatSession` and
`CombatRuleRouter`, and `EnemyAttackProgressManagementSystem`, the Gameplay-phase owner of block
aggregate reconciliation. Both override `Update` and publish complete component, dynamic-buffer,
event, structural-write, phase, and barrier metadata. The 28 other legacy names below are
compatibility ledger names, not constructible `IGameSystem` shells and never scheduler entries.

Their exact consolidated owners are:

- `AttackResolutionSystem`: Anathema, attack resolution, battle scene/state flow, Courage, enemy
  damage/defeat/intent/phase flow, HP, end-of-turn marking, blocking requirements, phase changes,
  test-fight flow, Thorned, Tribulation, and weapon rule behavior.
- `EnemyAttackProgressManagementSystem`: assigned-block lifecycle and attack-progress aggregates.
- ECS-040/ECS-041 input and interaction owners: battle-pile gamepad input and can-play highlight.
- ECS-045 extraction/consumers: battle backgrounds and lighting, enemy intent pips, modular-effect
  presentation/coordinator, HUD feedback, and wisp particles.

`CombatEventHub.BuildRoutes` returns all 32 stable routes and never creates or attaches a runtime.
`CombatOwnedEventConsumers` registers combat command/resource consumers at priority 100 before it
is bound to the session. Root composition may add lower-priority cross-domain consumers through
`CombatRouteConsumers`; notification/output routes intentionally remain composable. The root must
attach its one `EventRuntime` before calling `CombatSession.Create(world, hub, ...)`, and the
session retains that injected hub for every publication.

## Systems (30/30)

| Legacy ledger key | Legacy type | Replacement |
| --- | --- | --- |
| `ECS/Scenes/BattleScene/AnathemaManagementSystem.cs#AnathemaManagementSystem` | `AnathemaManagementSystem` | Consolidated into scheduled `AttackResolutionSystem`; no separate registration. |
| `ECS/Scenes/BattleScene/AssignedBlockLifecycleSystem.cs#AssignedBlockLifecycleSystem` | `AssignedBlockLifecycleSystem` | Consolidated into scheduled `EnemyAttackProgressManagementSystem`; no separate registration. |
| `ECS/Scenes/BattleScene/AttackResolutionSystem.cs#AttackResolutionSystem` | `AttackResolutionSystem` | Operational scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/BattleBackgroundSystem.cs#BattleBackgroundSystem` | `BattleBackgroundSystem` | ECS-045 battle-background extraction/consumer; no ECS-042 registration. |
| `ECS/Scenes/BattleScene/BattlePileGamepadInputSystem.cs#BattlePileGamepadInputSystem` | `BattlePileGamepadInputSystem` | ECS-040 root input plus ECS-041 pile interaction; no ECS-042 registration. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#BattleSceneSystem` | `BattleSceneSystem` | Consolidated into scheduled `AttackResolutionSystem`; scene materialization belongs to ECS-044. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#BattleStateInfoManagementSystem` | `BattleStateInfoManagementSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/CanPlayHighlightSettingsSystem.cs#CanPlayHighlightSettingsSystem` | `CanPlayHighlightSettingsSystem` | ECS-041 interaction/highlight owner; no ECS-042 registration. |
| `ECS/Scenes/BattleScene/CathedralLightingSystem.cs#CathedralLightingSystem` | `CathedralLightingSystem` | ECS-045 lighting extraction/consumer; no ECS-042 registration. |
| `ECS/Scenes/BattleScene/CourageManagerSystem.cs#CourageManagerSystem` | `CourageManagerSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/DesertBackgroundEffectSystem.cs#DesertBackgroundEffectSystem` | `DesertBackgroundEffectSystem` | ECS-045 background extraction/consumer; no ECS-042 registration. |
| `ECS/Scenes/BattleScene/EnemyAttackProgressManagementSystem.cs#EnemyAttackProgressManagementSystem` | `EnemyAttackProgressManagementSystem` | Operational scheduled `EnemyAttackProgressManagementSystem`. |
| `ECS/Scenes/BattleScene/EnemyDamageManagerSystem.cs#EnemyDamageManagerSystem` | `EnemyDamageManagerSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/EnemyDefeatFlowSystem.cs#EnemyDefeatFlowSystem` | `EnemyDefeatFlowSystem` | Consolidated into scheduled `AttackResolutionSystem`; presentation hand-off is ECS-045. |
| `ECS/Scenes/BattleScene/EnemyIntentPipsSystem.cs#EnemyIntentPipsSystem` | `EnemyIntentPipsSystem` | ECS-045 intent-pip extraction/consumer; no ECS-042 registration. |
| `ECS/Scenes/BattleScene/EnemyIntentPlanningSystem.cs#EnemyIntentPlanningSystem` | `EnemyIntentPlanningSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs#EnemyPhaseFlowSystem` | `EnemyPhaseFlowSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#HpManagementSystem` | `HpManagementSystem` | Consolidated into scheduled `AttackResolutionSystem` plus priority-100 resource consumers. |
| `ECS/Scenes/BattleScene/MarkedForEndOfTurnSystem.cs#MarkedForEndOfTurnSystem` | `MarkedForEndOfTurnSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/ModularEffectActorPresentationSystem.cs#ModularEffectActorPresentationSystem` | `ModularEffectActorPresentationSystem` | ECS-045 effect extraction/consumer; no ECS-042 registration. |
| `ECS/Scenes/BattleScene/ModularEffectCoordinatorSystem.cs#ModularEffectCoordinatorSystem` | `ModularEffectCoordinatorSystem` | ECS-045 request coordination; combat rule hand-off remains in `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/MustBeBlockedSystem.cs#MustBeBlockedSystem` | `MustBeBlockedSystem` | Priority-100 `MustBeBlockedEvent` consumer plus scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/PhaseChangeEventSystem.cs#PhaseChangeEventSystem` | `PhaseChangeEventSystem` | Priority-100 phase consumer plus scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/PhaseCoordinatorSystem.cs#PhaseCoordinatorSystem` | `PhaseCoordinatorSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/PlayerHudFeedbackSystem.cs#PlayerHudFeedbackSystem` | `PlayerHudFeedbackSystem` | ECS-045 HUD extraction/consumer; resource state is owned by `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/PlayerWispParticleSystem.cs#PlayerWispParticleSystem` | `PlayerWispParticleSystem` | ECS-045 particle extraction/consumer; no ECS-042 registration. |
| `ECS/Scenes/BattleScene/TestFightFlowSystem.cs#TestFightFlowSystem` | `TestFightFlowSystem` | Consolidated into scheduled `AttackResolutionSystem`; authoring belongs to ECS-044. |
| `ECS/Scenes/BattleScene/ThornedManagementSystem.cs#ThornedManagementSystem` | `ThornedManagementSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/TribulationManagerSystem.cs#TribulationManagerSystem` | `TribulationManagerSystem` | Consolidated into scheduled `AttackResolutionSystem`. |
| `ECS/Scenes/BattleScene/WeaponManagementSystem.cs#WeaponManagementSystem` | `WeaponManagementSystem` | Consolidated into scheduled `AttackResolutionSystem`. |

## Components (24/24)

| Legacy ledger key | Legacy type | Replacement |
| --- | --- | --- |
| `ECS/Components/CardComponents.cs#BattleInfo` | `BattleInfo` | `BattleInfo` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#BattleStateInfo` | `BattleStateInfo` | `BattleStateInfo` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#Battlefield` | `Battlefield` | `Battlefield` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#CannotBlockThisAttack` | `CannotBlockThisAttack` | `CannotBlockThisAttack` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#CourageTooltipAnchor` | `CourageTooltipAnchor` | `CourageTooltipAnchor` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#Enemy` | `Enemy` | `Enemy` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#ModifiedBlock` | `ModifiedBlock` | `ModifiedBlock` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#PhaseState` | `PhaseState` | `PhaseState` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#RelentlessStrikeBattleState` | `RelentlessStrikeBattleState` | `RelentlessStrikeBattleState` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CardComponents.cs#ThreatTooltipAnchor` | `ThreatTooltipAnchor` | `ThreatTooltipAnchor` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#AmbushState` | `AmbushState` | `AmbushState` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#AssignedBlockCard` | `AssignedBlockCard` | `AssignedBlockCard` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#AssignedBlockPresentation` | `AssignedBlockPresentation` | `AssignedBlockPresentation` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#AssignedBlockRailPresentation` | `AssignedBlockRailPresentation` | `AssignedBlockRailPresentation` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#AttackIntent` | `AttackIntent` | `AttackIntent` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#EnemyArsenal` | `EnemyArsenal` | `EnemyArsenal` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#EnemyAttackBannerAnchor` | `EnemyAttackBannerAnchor` | `EnemyAttackBannerAnchor` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#EnemyAttackBannerPresentation` | `EnemyAttackBannerPresentation` | `EnemyAttackBannerPresentation` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#EnemyAttackProgress` | `EnemyAttackProgress` | `EnemyAttackProgress` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#ExhaustOnBlock` | `ExhaustOnBlock` | `ExhaustOnBlock` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#NextTurnAttackIntent` | `NextTurnAttackIntent` | `NextTurnAttackIntent` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/CombatComponents.cs#Tribulation` | `Tribulation` | `Tribulation` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/Scenes.cs#AmbushTextAnchor` | `AmbushTextAnchor` | `AmbushTextAnchor` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |
| `ECS/Components/Scenes.cs#AmbushTimerAnchor` | `AmbushTimerAnchor` | `AmbushTimerAnchor` unmanaged component/tag in `CombatContracts.cs`; variable-length state uses generation-checked dynamic-buffer handles. |

## Events (32/32)

| Legacy ledger key | Legacy type | Replacement |
| --- | --- | --- |
| `ECS/Events/AmbushEvents.cs#AmbushTimerExpired` | `AmbushTimerExpired` | `AmbushTimerExpired` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/BattlePhaseEvents.cs#ChangeBattlePhaseEvent` | `ChangeBattlePhaseEvent` | `ChangeBattlePhaseEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/BattlePhaseEvents.cs#ProceedToNextPhase` | `ProceedToNextPhase` | `ProceedToNextPhase` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/BattlePhaseEvents.cs#ShowConfirmButtonEvent` | `ShowConfirmButtonEvent` | `ShowConfirmButtonEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/BlockEvents.cs#AssignedBlockReturnCompleted` | `AssignedBlockReturnCompleted` | `AssignedBlockReturnCompleted` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/BlockEvents.cs#BlockAssignmentAdded` | `BlockAssignmentAdded` | `BlockAssignmentAdded` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/BlockEvents.cs#BlockAssignmentRemoved` | `BlockAssignmentRemoved` | `BlockAssignmentRemoved` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CardEvents.cs#CardBlockedEvent` | `CardBlockedEvent` | `CardBlockedEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CardEvents.cs#MustBeBlockedEvent` | `MustBeBlockedEvent` | `MustBeBlockedEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#ApplyEffect` | `ApplyEffect` | `ApplyEffect` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#AttackResolved` | `AttackResolved` | `AttackResolved` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#EnemyAbsorbComplete` | `EnemyAbsorbComplete` | `EnemyAbsorbComplete` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#EnemyAttackImpactNow` | `EnemyAttackImpactNow` | `EnemyAttackImpactNow` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#EnemyDamageAppliedEvent` | `EnemyDamageAppliedEvent` | `EnemyDamageAppliedEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#EnemyKilledEvent` | `EnemyKilledEvent` | `EnemyKilledEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#EnemyPhaseLethalEvent` | `EnemyPhaseLethalEvent` | `EnemyPhaseLethalEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#EnemyPhaseResetEvent` | `EnemyPhaseResetEvent` | `EnemyPhaseResetEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#IntentPlanned` | `IntentPlanned` | `IntentPlanned` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#OnEnemyAttackHitEvent` | `OnEnemyAttackHitEvent` | `OnEnemyAttackHitEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#ResolveAttack` | `ResolveAttack` | `ResolveAttack` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#ResolvingEnemyDamageEvent` | `ResolvingEnemyDamageEvent` | `ResolvingEnemyDamageEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#ShowStunnedOverlay` | `ShowStunnedOverlay` | `ShowStunnedOverlay` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/CombatEvents.cs#TriggerEnemyAttackDisplayEvent` | `TriggerEnemyAttackDisplayEvent` | `TriggerEnemyAttackDisplayEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/HpEvents.cs#ApplyBattleMaxHpEvent` | `ApplyBattleMaxHpEvent` | `ApplyBattleMaxHpEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/HpEvents.cs#FullyHealEvent` | `FullyHealEvent` | `FullyHealEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/HpEvents.cs#HealEvent` | `HealEvent` | `HealEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/HpEvents.cs#IncreaseMaxHpEvent` | `IncreaseMaxHpEvent` | `IncreaseMaxHpEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/HpEvents.cs#ModifyHpEvent` | `ModifyHpEvent` | `ModifyHpEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/HpEvents.cs#PlayerDied` | `PlayerDied` | `PlayerDied` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/HpEvents.cs#SetHpEvent` | `SetHpEvent` | `SetHpEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/ThreatEvents.cs#ModifyThreatEvent` | `ModifyThreatEvent` | `ModifyThreatEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |
| `ECS/Events/ThreatEvents.cs#SetThreatEvent` | `SetThreatEvent` | `SetThreatEvent` unmanaged event contract in `CombatContracts.cs`; authoritative ordering is represented by `CombatRuleKind` queue states. |

## Subscriptions (90/90)

Each legacy subscription is folded into its owning descriptor system, an explicit combat-session input, or a staged queued rule. Presentation-only signals remain typed outbound contracts for ECS-045; card-zone mutations remain typed `RuleCommand` outputs for ECS-041.

| Legacy subscription key | Legacy event | Replacement route |
| --- | --- | --- |
| `ECS/Scenes/BattleScene/AnathemaManagementSystem.cs#PledgeAddedEvent#1` | `PledgeAddedEvent` | Explicit `PledgeAddedEvent` input/queue transition owned by `AnathemaManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/AssignedBlockLifecycleSystem.cs#AssignedBlockReturnCompleted#1` | `AssignedBlockReturnCompleted` | Explicit `AssignedBlockReturnCompleted` input/queue transition owned by `AssignedBlockLifecycleSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/AssignedBlockLifecycleSystem.cs#BlockAssignmentAdded#1` | `BlockAssignmentAdded` | Explicit `BlockAssignmentAdded` input/queue transition owned by `AssignedBlockLifecycleSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/AssignedBlockLifecycleSystem.cs#BlockAssignmentRemoved#1` | `BlockAssignmentRemoved` | Explicit `BlockAssignmentRemoved` input/queue transition owned by `AssignedBlockLifecycleSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/AssignedBlockLifecycleSystem.cs#CardMoved#1` | `CardMoved` | Explicit `CardMoved` input/queue transition owned by `AssignedBlockLifecycleSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/AssignedBlockLifecycleSystem.cs#UnassignCardAsBlockRequested#1` | `UnassignCardAsBlockRequested` | Explicit `UnassignCardAsBlockRequested` input/queue transition owned by `AssignedBlockLifecycleSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/AttackResolutionSystem.cs#<inferred-delegate-type>#1` | `<inferred-delegate-type>` | Explicit `<inferred-delegate-type>` input/queue transition owned by `AttackResolutionSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/AttackResolutionSystem.cs#<inferred-delegate-type>#2` | `<inferred-delegate-type>` | Explicit `<inferred-delegate-type>` input/queue transition owned by `AttackResolutionSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/AttackResolutionSystem.cs#ResolveAttack#1` | `ResolveAttack` | Explicit `ResolveAttack` input/queue transition owned by `AttackResolutionSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleBackgroundSystem.cs#ChangeBattleLocationEvent#1` | `ChangeBattleLocationEvent` | Explicit `ChangeBattleLocationEvent` input/queue transition owned by `BattleBackgroundSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#BattlePhaseAnimationCompleteEvent#1` | `BattlePhaseAnimationCompleteEvent` | Explicit `BattlePhaseAnimationCompleteEvent` input/queue transition owned by `BattleSceneSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#DeleteCachesEvent#1` | `DeleteCachesEvent` | Explicit `DeleteCachesEvent` input/queue transition owned by `BattleSceneSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#DialogEnded#1` | `DialogEnded` | Explicit `DialogEnded` input/queue transition owned by `BattleSceneSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#DialogueSequenceCompleted#1` | `DialogueSequenceCompleted` | Explicit `DialogueSequenceCompleted` input/queue transition owned by `BattleSceneSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#LoadSceneEvent#1` | `LoadSceneEvent` | Explicit `LoadSceneEvent` input/queue transition owned by `BattleSceneSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#PrepareSceneEvent#1` | `PrepareSceneEvent` | Explicit `PrepareSceneEvent` input/queue transition owned by `BattleSceneSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#StartBattleRequested#1` | `StartBattleRequested` | Explicit `StartBattleRequested` input/queue transition owned by `BattleSceneSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleSceneSystem.cs#TransitionCompleteEvent#1` | `TransitionCompleteEvent` | Explicit `TransitionCompleteEvent` input/queue transition owned by `BattleSceneSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#ApplyEffect#1` | `ApplyEffect` | Explicit `ApplyEffect` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#LoadSceneEvent#1` | `LoadSceneEvent` | Explicit `LoadSceneEvent` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#ModifyCourageRequestEvent#1` | `ModifyCourageRequestEvent` | Explicit `ModifyCourageRequestEvent` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#ModifyHpEvent#1` | `ModifyHpEvent` | Explicit `ModifyHpEvent` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#SetCourageEvent#1` | `SetCourageEvent` | Explicit `SetCourageEvent` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#StartBattleRequested#1` | `StartBattleRequested` | Explicit `StartBattleRequested` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#TopCardRemovedForMillEvent#1` | `TopCardRemovedForMillEvent` | Explicit `TopCardRemovedForMillEvent` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/BattleStateInfoManagementSystem.cs#TrackingEvent#1` | `TrackingEvent` | Explicit `TrackingEvent` input/queue transition owned by `BattleStateInfoManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/CourageManagerSystem.cs#ApplyEffect#1` | `ApplyEffect` | Explicit `ApplyEffect` input/queue transition owned by `CourageManagerSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/CourageManagerSystem.cs#CardMoved#1` | `CardMoved` | Explicit `CardMoved` input/queue transition owned by `CourageManagerSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/CourageManagerSystem.cs#ModifyCourageRequestEvent#1` | `ModifyCourageRequestEvent` | Explicit `ModifyCourageRequestEvent` input/queue transition owned by `CourageManagerSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/CourageManagerSystem.cs#SetCourageEvent#1` | `SetCourageEvent` | Explicit `SetCourageEvent` input/queue transition owned by `CourageManagerSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyAttackProgressManagementSystem.cs#ApplyPassiveEvent#1` | `ApplyPassiveEvent` | Explicit `ApplyPassiveEvent` input/queue transition owned by `EnemyAttackProgressManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyAttackProgressManagementSystem.cs#BlockAssignmentAdded#1` | `BlockAssignmentAdded` | Explicit `BlockAssignmentAdded` input/queue transition owned by `EnemyAttackProgressManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyAttackProgressManagementSystem.cs#BlockAssignmentRemoved#1` | `BlockAssignmentRemoved` | Explicit `BlockAssignmentRemoved` input/queue transition owned by `EnemyAttackProgressManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyAttackProgressManagementSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `EnemyAttackProgressManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyAttackProgressManagementSystem.cs#LoadSceneEvent#1` | `LoadSceneEvent` | Explicit `LoadSceneEvent` input/queue transition owned by `EnemyAttackProgressManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyAttackProgressManagementSystem.cs#RemovePassive#1` | `RemovePassive` | Explicit `RemovePassive` input/queue transition owned by `EnemyAttackProgressManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyAttackProgressManagementSystem.cs#UpdatePassive#1` | `UpdatePassive` | Explicit `UpdatePassive` input/queue transition owned by `EnemyAttackProgressManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyDamageManagerSystem.cs#ApplyEffect#1` | `ApplyEffect` | Explicit `ApplyEffect` input/queue transition owned by `EnemyDamageManagerSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyDamageManagerSystem.cs#EnemyAttackImpactNow#1` | `EnemyAttackImpactNow` | Explicit `EnemyAttackImpactNow` input/queue transition owned by `EnemyDamageManagerSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyDefeatFlowSystem.cs#BeginDefeatPresentationEvent#1` | `BeginDefeatPresentationEvent` | Explicit `BeginDefeatPresentationEvent` input/queue transition owned by `EnemyDefeatFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyDefeatFlowSystem.cs#DeleteCachesEvent#1` | `DeleteCachesEvent` | Explicit `DeleteCachesEvent` input/queue transition owned by `EnemyDefeatFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyDefeatFlowSystem.cs#DialogueSequenceCompleted#1` | `DialogueSequenceCompleted` | Explicit `DialogueSequenceCompleted` input/queue transition owned by `EnemyDefeatFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyDefeatFlowSystem.cs#PixelBurstAnimationCompleted#1` | `PixelBurstAnimationCompleted` | Explicit `PixelBurstAnimationCompleted` input/queue transition owned by `EnemyDefeatFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyDefeatFlowSystem.cs#StartBattleRequested#1` | `StartBattleRequested` | Explicit `StartBattleRequested` input/queue transition owned by `EnemyDefeatFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyDefeatFlowSystem.cs#VictoryAnimationCompleteEvent#1` | `VictoryAnimationCompleteEvent` | Explicit `VictoryAnimationCompleteEvent` input/queue transition owned by `EnemyDefeatFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyIntentPipsSystem.cs#DeleteCachesEvent#1` | `DeleteCachesEvent` | Explicit `DeleteCachesEvent` input/queue transition owned by `EnemyIntentPipsSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyIntentPlanningSystem.cs#ApplyPassiveEvent#1` | `ApplyPassiveEvent` | Explicit `ApplyPassiveEvent` input/queue transition owned by `EnemyIntentPlanningSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyIntentPlanningSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `EnemyIntentPlanningSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs#BattlePresentationCompleted#1` | `BattlePresentationCompleted` | Explicit `BattlePresentationCompleted` input/queue transition owned by `EnemyPhaseFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs#BattlePresentationStarted#1` | `BattlePresentationStarted` | Explicit `BattlePresentationStarted` input/queue transition owned by `EnemyPhaseFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs#DeleteCachesEvent#1` | `DeleteCachesEvent` | Explicit `DeleteCachesEvent` input/queue transition owned by `EnemyPhaseFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs#DialogueSequenceCompleted#1` | `DialogueSequenceCompleted` | Explicit `DialogueSequenceCompleted` input/queue transition owned by `EnemyPhaseFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs#EnemyPhaseLethalEvent#1` | `EnemyPhaseLethalEvent` | Explicit `EnemyPhaseLethalEvent` input/queue transition owned by `EnemyPhaseFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs#StartBattleRequested#1` | `StartBattleRequested` | Explicit `StartBattleRequested` input/queue transition owned by `EnemyPhaseFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/EnemyPhaseFlowSystem.cs#VisualEffectCompleted#1` | `VisualEffectCompleted` | Explicit `VisualEffectCompleted` input/queue transition owned by `EnemyPhaseFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#ApplyBattleMaxHpEvent#1` | `ApplyBattleMaxHpEvent` | Explicit `ApplyBattleMaxHpEvent` input/queue transition owned by `HpManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#ApplyPassiveEvent#1` | `ApplyPassiveEvent` | Explicit `ApplyPassiveEvent` input/queue transition owned by `HpManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#FullyHealEvent#1` | `FullyHealEvent` | Explicit `FullyHealEvent` input/queue transition owned by `HpManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#HealEvent#1` | `HealEvent` | Explicit `HealEvent` input/queue transition owned by `HpManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#IncreaseMaxHpEvent#1` | `IncreaseMaxHpEvent` | Explicit `IncreaseMaxHpEvent` input/queue transition owned by `HpManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#ModifyHpRequestEvent#1` | `ModifyHpRequestEvent` | Explicit `ModifyHpRequestEvent` input/queue transition owned by `HpManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#RemovePassive#1` | `RemovePassive` | Explicit `RemovePassive` input/queue transition owned by `HpManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/HpManagementSystem.cs#SetHpEvent#1` | `SetHpEvent` | Explicit `SetHpEvent` input/queue transition owned by `HpManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/MarkedForEndOfTurnSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `MarkedForEndOfTurnSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/ModularEffectActorPresentationSystem.cs#ModifyHpEvent#1` | `ModifyHpEvent` | Explicit `ModifyHpEvent` input/queue transition owned by `ModularEffectActorPresentationSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/ModularEffectActorPresentationSystem.cs#StartDebuffAnimation#1` | `StartDebuffAnimation` | Explicit `StartDebuffAnimation` input/queue transition owned by `ModularEffectActorPresentationSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/ModularEffectCoordinatorSystem.cs#EnemyDamageAppliedEvent#1` | `EnemyDamageAppliedEvent` | Explicit `EnemyDamageAppliedEvent` input/queue transition owned by `ModularEffectCoordinatorSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/ModularEffectCoordinatorSystem.cs#LoadSceneEvent#1` | `LoadSceneEvent` | Explicit `LoadSceneEvent` input/queue transition owned by `ModularEffectCoordinatorSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/ModularEffectCoordinatorSystem.cs#VisualEffectRequested#1` | `VisualEffectRequested` | Explicit `VisualEffectRequested` input/queue transition owned by `ModularEffectCoordinatorSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/MustBeBlockedSystem.cs#AmbushTimerExpired#1` | `AmbushTimerExpired` | Explicit `AmbushTimerExpired` input/queue transition owned by `MustBeBlockedSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/MustBeBlockedSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `MustBeBlockedSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/MustBeBlockedSystem.cs#MustBeBlockedEvent#1` | `MustBeBlockedEvent` | Explicit `MustBeBlockedEvent` input/queue transition owned by `MustBeBlockedSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PhaseChangeEventSystem.cs#BattlePhaseAnimationCompleteEvent#1` | `BattlePhaseAnimationCompleteEvent` | Explicit `BattlePhaseAnimationCompleteEvent` input/queue transition owned by `PhaseChangeEventSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PhaseChangeEventSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `PhaseChangeEventSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PhaseChangeEventSystem.cs#DeleteCachesEvent#1` | `DeleteCachesEvent` | Explicit `DeleteCachesEvent` input/queue transition owned by `PhaseChangeEventSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PhaseCoordinatorSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `PhaseCoordinatorSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PhaseCoordinatorSystem.cs#ChangeBattlePhaseEvent#2` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `PhaseCoordinatorSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PlayerHudFeedbackSystem.cs#ModifyActionPointsEvent#1` | `ModifyActionPointsEvent` | Explicit `ModifyActionPointsEvent` input/queue transition owned by `PlayerHudFeedbackSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PlayerHudFeedbackSystem.cs#ModifyCourageEvent#1` | `ModifyCourageEvent` | Explicit `ModifyCourageEvent` input/queue transition owned by `PlayerHudFeedbackSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PlayerHudFeedbackSystem.cs#ModifyTemperanceEvent#1` | `ModifyTemperanceEvent` | Explicit `ModifyTemperanceEvent` input/queue transition owned by `PlayerHudFeedbackSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PlayerHudFeedbackSystem.cs#SetActionPointsEvent#1` | `SetActionPointsEvent` | Explicit `SetActionPointsEvent` input/queue transition owned by `PlayerHudFeedbackSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PlayerHudFeedbackSystem.cs#SetCourageEvent#1` | `SetCourageEvent` | Explicit `SetCourageEvent` input/queue transition owned by `PlayerHudFeedbackSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/PlayerHudFeedbackSystem.cs#SetTemperanceEvent#1` | `SetTemperanceEvent` | Explicit `SetTemperanceEvent` input/queue transition owned by `PlayerHudFeedbackSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/QueuedWaitAbsorbEvent.cs#<inferred-delegate-type>#1` | `<inferred-delegate-type>` | Explicit `<inferred-delegate-type>` input/queue transition owned by `QueuedWaitAbsorbEvent`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/TestFightFlowSystem.cs#DeleteCachesEvent#1` | `DeleteCachesEvent` | Explicit `DeleteCachesEvent` input/queue transition owned by `TestFightFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/TestFightFlowSystem.cs#PlayerDied#1` | `PlayerDied` | Explicit `PlayerDied` input/queue transition owned by `TestFightFlowSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/ThornedManagementSystem.cs#CardDiscardedForCostEvent#1` | `CardDiscardedForCostEvent` | Explicit `CardDiscardedForCostEvent` input/queue transition owned by `ThornedManagementSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/TribulationManagerSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `TribulationManagerSystem`; no static subscription or direct system reference. |
| `ECS/Scenes/BattleScene/WeaponManagementSystem.cs#ChangeBattlePhaseEvent#1` | `ChangeBattlePhaseEvent` | Explicit `ChangeBattlePhaseEvent` input/queue transition owned by `WeaponManagementSystem`; no static subscription or direct system reference. |

## Runtime responsibility map

- HP/resources: shared `HP`, `Courage`, `Temperance`, `ActionPoints`, and `Threat` components; prevention order is Wounded, Armor, Guard, then Aegis.
- Phases and ordering: `PhaseState`, `CombatRuleState`, and mandatory queue transitions preserve battle start, block, impact, action, enemy turn, phase transition, victory, and defeat order.
- Enemies: `GeneratedEnemyCatalog` plans into dynamic intent buffers; `GeneratedEnemyAttackCatalog` executes channel/reveal/block/confirm/hit/threshold/progress stages.
- Blocks: ordered `BlockAssignmentEntry` buffers preserve duplicate card identity, color counts, exact/minimum requirements, black +1 block, and resource gains.
- Passives: compact `CombatPassive` buffers drive Armor, Guard, Aegis, Wounded, Fear, Slow, Bleed, Windchill, Channel, Power, and content-authored effects.
- Queued resolution: presentation creates a one-frame pending head state; mandatory work precedes reactive work and resumes without replaying completed attack stages.
- Cross-domain commands: card/deck/presentation commands remain typed at the ECS-041/ECS-045 boundaries and are never converted to arbitrary handlers.
