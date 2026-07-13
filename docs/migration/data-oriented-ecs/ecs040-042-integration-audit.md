# ECS-040–042 integration audit

Scope: read-only audit of the completed global/input/UI, card gameplay, and combat lanes. Runtime
source was not changed by this audit. Executable checks live in
`tests/Crusaders30XX.Tests/DataOriented/Gameplay/Integration/`.

## Outcome

No cross-lane blocker was found. The three lanes can coexist in the generated component registry
and scheduler ID space. Their remaining composition work is bounded to ECS-045 presentation/audio
extraction and ECS-050 host/event-route assembly.

| Check | Result |
| --- | --- |
| Generated component registry | Pass. IDs and metadata names are unique and contiguous; gameplay contains no manual `RegisterComponent`/`RegisterTag` calls. |
| System IDs | Pass. Global/UI, cards, and combat IDs are positive and mutually unique. |
| Event layout | Pass. Every ledger-assigned event resolves to an unmanaged value type. Event streams are instance-owned; no static stream or static `EventManager` remains. |
| Ledger/document counts | Pass: ECS-040 = 10 components / 15 events / 11 systems / 53 subscriptions; ECS-041 = 57 / 58 / 26 / 90; ECS-042 = 24 / 32 / 30 / 90. Every assigned component, event, and system name appears in its mapping document. |
| Forbidden dependencies | Pass. New gameplay paths contain no legacy `Crusaders30XX.ECS.Core`, direct `MouseState`/`GamePad` polling, LINQ, draw methods, gameplay services, or system-to-system constructor injection. |
| Scene groups and phases | Pass. Global/input/UI descriptors use `SceneGroup.Global`; card/combat descriptors use `SceneGroup.Battle`. Input and interaction precede rules/gameplay, presentation follows gameplay, and card shader extraction is in `RenderExtraction`. |

## Descriptor-shell ownership

Descriptor shells are intentional compatibility names for ledger coverage. They may not acquire
independent mutable state. A shell either names behavior consolidated into an existing runtime or
marks work owned by ECS-045. ECS-050 may register these descriptors only when their consolidated
owner or extraction adapter is present.

### ECS-040

- `HotKeyProgressRingSystem` is ECS-045 progress-ring extraction from the final `HotKey` component.
- `MusicManagerSystem` and `SoundEffectManagerSystem` are ECS-045 audio request extraction and external adapters.
- `UIElementHighlightSystem` is consolidated into `UIInteractionSystem` hover state; ECS-045 extracts its render packet.

`SceneLifecycleSystem` and `ModalInputSuppressionSystem` are non-ledger consolidated runtime owners
introduced to replace legacy stateful behavior. Their IDs remain in the global/UI range and do not
collide with the eleven ledger responsibilities.

### ECS-041

- `AssignedBlocksToDiscardSystem` is consolidated into `CardZoneOperations` and the ECS-042 block lifecycle.
- `DrawHandSystem` is consolidated into `DeckManagementSystem`; its static calculation excludes pledged, token, and weapon cards.
- `CanPlayCardHighlightSystem` uses `CardPlayRules` state and is rendered by ECS-045.
- `CantPlayCardMessageSystem` is a typed rejection stream consumed by ECS-045 UI extraction.
- `CardApplicationManagementSystem` is consolidated into typed application commands plus `CursedManagementSystem`, `SealManagementSystem`, and the other card-status owners.
- `CardHoverDetectionSystem` is consolidated into ECS-040 cursor/UI interaction.
- `DeckEmptyDeathCheckSystem` is the pure deck predicate used by deck/combat flow.
- `DiscardSpecificCardHighlightSystem` is selection state rendered by ECS-045.
- `HandBlockInteractionSystem` is consolidated into the ECS-042 `CombatSession` block API.
- `HandCardBoundsLateSystem` is ECS-045 late presentation/bounds extraction.
- `MarkedForSpecificDiscardSystem` is consolidated into `DeckManagementSystem` and `CardZoneSystem` movement.
- `CardListModalSystem` uses ECS-040 modal interaction and ECS-045 modal rendering.
- `CardShaderCompositorSystem` is ECS-045 shader/pass extraction; it remains in `RenderExtraction`.
- `CardUsageTrackingSystem` is the typed card-event boundary consumed by ECS-044 tracking/content routes.

The event/API-driven card systems whose `Update` method is empty are unscheduled compatibility owners:
`ActionPointManagementSystem`, `CardZoneSystem`, `PledgeManagementSystem`,
`RecoilManagementSystem`, `SealManagementSystem`, `CursedManagementSystem`,
`PlunderManagementSystem`, `ShackleManagementSystem`, `MarkedForExhaustSystem`, and
`MillCardSystem` expose typed consumers or deterministic operations used by composition.

### ECS-042

The combat rule and gameplay shells are consolidated into `CombatSession`, `CombatRuleRouter`, and
the mandatory/reactive `QueuedRuleRuntime<CombatRuleState>`:

`AnathemaManagementSystem`, `AssignedBlockLifecycleSystem`, `BattleSceneSystem`,
`BattleStateInfoManagementSystem`, `CourageManagerSystem`, `EnemyDamageManagerSystem`,
`EnemyDefeatFlowSystem`, `EnemyIntentPlanningSystem`, `EnemyPhaseFlowSystem`,
`HpManagementSystem`, `MarkedForEndOfTurnSystem`, `MustBeBlockedSystem`,
`PhaseChangeEventSystem`, `PhaseCoordinatorSystem`, `TestFightFlowSystem`,
`ThornedManagementSystem`, `TribulationManagerSystem`, and `WeaponManagementSystem`.

The remaining combat shells have an explicit adjacent owner:

- `BattlePileGamepadInputSystem` uses ECS-040 input and calls the consolidated combat block/confirm API.
- `CanPlayHighlightSettingsSystem` consumes ECS-041 validation state and is extracted by ECS-045.
- `BattleBackgroundSystem`, `CathedralLightingSystem`, `DesertBackgroundEffectSystem`,
  `EnemyIntentPipsSystem`, `ModularEffectActorPresentationSystem`,
  `ModularEffectCoordinatorSystem`, `PlayerHudFeedbackSystem`, and
  `PlayerWispParticleSystem` are ECS-045 presentation extraction responsibilities.

Only `AttackResolutionSystem` and `EnemyAttackProgressManagementSystem` actively drive the
consolidated combat runtime each scheduler update.

## Deferred integration gaps

These are not ECS-040–042 runtime blockers, but ECS-050 must close them before switching `Game1`:

1. ECS-041's former route/registration gap is closed: `CardGameplayEventHub` owns and returns all
   58 route fragments without attaching a runtime, accepts prioritized root consumers, and
   `CardGameplayComposition.Systems` exposes only the operational `DeckManagementSystem` with a
   complete access/structural descriptor. All 26 names remain auditable through
   `CompatibilitySystems`.
2. `CombatEventHub` owns all 32 ECS-042 streams, but its local endpoint intentionally has no
   consumers. ECS-050 must attach card, content, tracking, phase, and presentation consumers in
   stable priority order.
3. `GlobalUiEventContracts` use injected instance streams rather than one global hub. ECS-050 must
   compose the input, scene, host-command, and ECS-045 adapter routes into the world endpoint.
4. `CombatSystemBase` shells carry phase/scene/barrier metadata but empty access signatures. The
   consolidated active combat systems must provide complete access metadata before scheduler
   parallelism is enabled; descriptor-only shells must not be scheduled as independent writers.

## Audit maintenance

The integration tests intentionally verify stable boundaries rather than implementation text:
generated registry uniqueness, ID uniqueness, unmanaged event layouts, instance-owned streams,
ledger-to-document name/count coverage, constructor dependency rules, forbidden source patterns,
and the explicit shell ownership list above.
