# ECS-052 domain parity audit

Status: the deleted test inventory has been reconciled at suite level, and the highest-risk
uncovered deterministic gameplay lane (enemy planning) now has executable new-runtime coverage.
This document is intentionally candid: a clean build and ledger completeness do not prove that
the 903 deleted legacy characterizations were replaced one for one.

## Method and exact inventory boundary

The deletion boundary in `git status` contains 148 tracked C# files directly under
`tests/Crusaders30XX.Tests/`. Reading each source from `git show HEAD:<path>` finds exactly 145
test suites with 903 `[Fact]`/`[Theory]` declarations, plus three support sources with no tests:
`BattleMutationTestSupport`, `MixedRumbleFakeInputSource`, and `TestAssembly`.

The categorized lists below are exact: every one of the 145 deleted suites appears once. The
number in parentheses is the legacy declaration count, not the number of generated theory cases.

### Cards, deck zones, and card rules (32 suites / 191 declarations)

- `CardApplicationManagementSystemTests` (8), `CardPlayUpgradeTests` (5),
  `CardStatModifierServiceTests` (5), `CardStatusManagementSystemTests` (9),
  `CardZoneSystemTests` (6), `CarpeDiemTests` (3), `ColorlessCardTests` (18),
  `CrimsonRiteTests` (2), `CursedManagementSystemTests` (6),
  `DeckEmptyDeathCheckSystemTests` (6), `DeckManagementSystemTests` (12),
  `DiscardCostMessageServiceTests` (2), `HammerStarterCardTests` (7),
  `HandStateLoggingServiceTests` (3), `KunaiTests` (3), `MaleficRiteTests` (5),
  `MarkedForExhaustSystemTests` (4), `PledgeAndBlockInteractionTests` (8),
  `PledgeAvailabilityServiceTests` (8), `PledgeDrawSystemTests` (3),
  `QuestCardRewardServiceTests` (23), `RecoilManagementSystemTests` (2),
  `RunDeckEntryServiceTests` (3), `ScarHandlingTests` (5), `SeizeTests` (4),
  `ShackleManagementSystemTests` (4), `StarterDeckSaveTests` (5), `SubZeroTests` (3),
  `VanguardsPromiseTests` (7), `WeaponManagementSystemTests` (2),
  `CardUsageTelemetryTests` (5), and `CardInputRoutingTests` (5).

### Combat, enemies, passives, and test-fight flow (28 suites / 131 declarations)

- `AppliedPassivesServiceGalvanizeTests` (8), `AppliedPassivesServicePreviewTests` (7),
  `AssignedBlockLifecycleSystemTests` (3), `BleedManagementSystemTests` (5),
  `EnemyAttackConfirmAvailabilityServiceTests` (15), `EnemyAttackFlowTests` (2),
  `EnemyAttackMustBlockRequirementServiceTests` (6),
  `EnemyAttackProgressOverrideServiceTests` (2), `EnemyDamageThresholdTests` (8),
  `EnemyDefeatFlowSystemTests` (6), `EnemyFactoryTests` (4),
  `EnemyIntentPlanningSystemTests` (2), `EnemyPhaseFlowSystemTests` (4),
  `EnemyPhaseResetServiceTests` (1), `EntombTests` (1),
  `FallenShepherdAttackTests` (13), `FearSlowPassiveTests` (4), `FrostEaterTests` (4),
  `FrostbiteReplacementEffectTests` (7), `FrozenClawTests` (1), `GuardDamageTests` (9),
  `InfernalExecutionTests` (1), `IntimidateManagementSystemTests` (4),
  `StrangeForceConditionTests` (3),
  `TestFightFlowSystemTests` (2), `TestFightRuntimeTests` (1),
  `TestFightSetupServiceTests` (5), and `VigorManagementSystemTests` (3).

### Equipment and medals (5 suites / 69 declarations)

- `AbilityEquipmentTests` (7), `EquipmentServiceTests` (3), `MedalCounterTests` (49),
  `StGeorgeMedalTests` (7), and `TemperanceFactoryTests` (3).

### Climb, progression, dialogue, tutorial, save, and meta-game (15 suites / 141 declarations)

- `AchievementTests` (8), `AudioSettingsSaveTests` (5), `BattleClimbPackageTests` (4),
  `BattleLocationAssetServiceTests` (4),
  `ClimbEncounterServiceTests` (7), `ClimbEventSystemTests` (10), `ClimbRuleServiceTests` (28),
  `ClimbShopServiceTests` (11), `CollectionProgressionTests` (10),
  `CollectionUnlockTests` (1), `DialogRepositoryTests` (3),
  `GuidedTutorialDefinitionTests` (22), `RunLifecycleTests` (1),
  `WayStationDialogueTests` (17), and `WayStationRunSetupTests` (10).

### Input, rumble, launch parsing, and host routing (10 suites / 84 declarations)

- `BattleInputGateTests` (7), `BattlePileGamepadInputTests` (16),
  `BoosterPackOpeningInputTests` (4),
  `GamepadRumbleMixerTests` (7), `GuidedTutorialInputGateTests` (3),
  `PlayerInputArchitectureTests` (33), `RumbleIntegrationTests` (3),
  `TestFightLaunchOptionsTests` (4), `TitleMenuResumeRoutingTests` (5), and
  `UnlockLaunchOptionsTests` (2).

### Presentation, animation, snapshots, resources, and visual effects (49 suites / 255 declarations)

- `AppliedPassivesDisplaySystemAnimationTests` (6), `AssignedBlockAnimationServiceTests` (2),
  `BattleCardMutationDisplaySystemTests` (5), `BlockedEnemyAttackPresentationTests` (2),
  `BoosterPackOpeningAnimationServiceTests` (13), `CardDisplaySystemTests` (4),
  `CardGeometryServiceTests` (5), `CardListPresentationTests` (3),
  `CardShaderCompositorTests` (3), `CardVisualEffectsSuppressionSystemTests` (10),
  `ClimbBackgroundDisplaySystemTests` (4), `ClimbColumnParallaxTests` (13),
  `ClimbResourceAcquisitionDisplaySystemTests` (4), `CrusaderPortraitAssetsTests` (3),
  `CursorTrailDisplaySystemTests` (5), `DialogPresentationMathTests` (3),
  `EndTurnDisplaySystemTests` (4), `EnemyAttackAnimationServiceTests` (6),
  `EnemyDamageMeterAnimationServiceTests` (5), `EnemyIntentPipsSystemTests` (1),
  `EquipmentArtServiceTests` (1), `EquipmentDisplaySystemTests` (16),
  `EquipmentTooltipSnapshotVariantTests` (3), `GuardianAngelFlightServiceTests` (2),
  `GuardianAngelMessageServiceTests` (4), `GuardianAngelSpeechQueueTests` (6),
  `HandDisplaySystemTests` (7), `LayeredHolesOverlayTests` (1),
  `MipmappedTextureUtilityTests` (7), `ModalAnimationSystemTests` (2),
  `ModularEffectActorPresentationSystemTests` (1), `PassiveApplicationAnimationTests` (8),
  `PlayerHudHealthDisplaySystemTests` (11), `PlayerHudIntegrationTests` (4),
  `PlayerHudLayoutSystemTests` (9), `PlayerHudPledgeDisplaySystemTests` (3),
  `PlayerHudResourceDisplaySystemTests` (5), `PlayerHudSnapshotVariantTests` (3),
  `PlayerHudTemperanceDisplaySystemTests` (4), `PortraitPixelBurstLayoutTests` (7),
  `PortraitPixelBurstMotionTests` (5), `PortraitPixelBurstSamplerTests` (5),
  `RewardModalDisplaySystemTests` (12), `TransformResolverServiceTests` (4),
  `VisualEffectDisplayMathTests` (2), `VisualEffectModuleDebugCatalogTests` (7),
  `VisualEffectPaletteTests` (4), `VisualEffectRequestFactoryTests` (3), and
  `VisualEffectSequenceAuthoringTests` (8).

### Foundation and diagnostics (6 suites / 32 declarations)

- `DisplayMetricsTests` (6), `DisplaySnapshotFrameworkTests` (14),
  `EntityManagerBulkDestroyTests` (2), `EventManagerTests` (2), `FrameProfilerTests` (6),
  and `WeightedLruCacheTests` (2).

## Existing replacement suites

The surviving tests replace architecture and selected semantics, not filenames:

| Replacement surface | Surviving suites | What is actually characterized |
| --- | --- | --- |
| ECS storage/events/scheduling | `WorldStorageTests`, `QueryAndCommandBufferTests`, `DataOrientedStorageTests`, `EventRuntimeTests`, `QueuedRuleRuntimeTests`, `SystemSchedulerTests` | Generations, structural transitions, queries, buffers, event waves, FIFO rules, scheduler order, and allocation gates. |
| Cards and deck | `CardBehaviorTraceTests`, `CardGameplayRuntimeTests` | All 69 definition-stage command traces, ordered zones, deterministic shuffle, empty draw, pledge/colorless basics, cursed restoration, deferred spawns, and routes. |
| Combat and enemies | `CombatRuntimeTests`, `EnemyBehaviorParityTests` | Full-fight ordering, block requirements, damage prevention, passives, ambush, phase/defeat flow, all-enemy six-turn deterministic planning, authored arsenal membership, and tutorial Horde fallback. |
| Equipment/medals/effects | `EquipmentMedalBehaviorTraceTests`, `MedalProviderParityTests`, `EffectGameplayRuntimeTests`, provider-contract tests | All generated primary traces, exceptional commands, provider precedence, replacement suppression, counter/reset contracts, passive mechanics, and routed consumption. |
| Meta-game | `MetaGameRuntimeTests` | Deterministic climb fixture generation, fresh materialization/round-trip, dialogue/modal/tutorial/achievement routes, and cross-domain progress. |
| Input/global UI | `Ecs040GlobalUiTests`, `Ecs040HostabilityTests`, `Ecs050ExternalHostAdapterTests` | Central capture conversion, targeting/context/Z order, interaction reset, host commands, scene requests, and read-only external drains. |
| Presentation/resources | `PresentationRenderingTests`, `Ecs052TextRenderingTests`, `Ecs052ProductionResourceCatalogTests`, `Ecs052SnapshotLaunchOutputCatalogTests`, ECS-050 authoring tests | Packet ordering/extraction, transform/tween/parallax math, read-only draws, text authoring, resource bindings, fixture registration, and output-name routing. |
| Root integration | `DataOrientedGameRuntimeTests`, ECS-040/045/046/050 audit suites | One world/event runtime/scheduler, route coverage, operational allowlists, scene/combat rebinding, authoring cleanup, and forbidden-old-runtime gates. |

## Genuinely uncovered semantics after cutover

The following are conversion-parity gaps, not merely renamed tests:

1. **Pixel parity is not established.** Packet/resource/text coverage does not replace the old
   card, HUD, equipment, modal, climb, animation, shader, particle, or visual-effect assertions,
   and registered fixtures are not proof that unchanged baselines verify.
2. **Climb and shop behavior is materially under-characterized.** Encounter/event schedules,
   final-time preemption, reward persistence, shop costs/refresh/exclusions, replacement flows,
   collection leveling/booster packs, and battle-climb packages account for most of the deleted
   meta suite and have only broad fresh-save and deterministic-fixture coverage.
3. **Guided tutorial parity is materially under-characterized.** The 22-test definition suite,
   input gates, stock hands, section advancement, restart, messages, and scene resume paths do not
   have an equivalent new-runtime suite.
4. **Card traces do not prove integrated card outcomes.** Static command traces cover every card,
   but payment/autopay, stat previews, deck-empty rescue, pledge/block interactions, persisted
   restrictions, exact duplicate run-deck identity, reward selection, telemetry, and many
   card-specific runtime sequences remain uncovered.
5. **Enemy attack hooks are only selectively integrated.** All definitions and planners exist,
   but the deleted Fallen Shepherd, Frost Eater, Infernal Execution, Entomb, Frozen Claw, Strange
   Force, threshold, progress-override, and impossible-requirement suites are not all represented
   as end-to-end attack-stage traces.
6. **Persistence/settings/telemetry parity is not established.** Save compatibility is explicitly
   out of scope, but fresh audio/rumble defaults, run lifecycle resets, collection persistence,
   active-run telemetry reconciliation, and corruption recovery were gameplay-visible behaviors.
7. **Rumble and advanced input consumers are partial.** The stable physical-button vocabulary,
   previous-device/connectivity/glyph metadata, cross-device hot-key hold cancellation, pile
   shoulder switching/tutorial visibility, pause toggle, phase-aware card actions, and booster
   settlement gate are covered by the ECS-052 input parity suites. Rich pause option navigation,
   selectable non-pile card lists, mixed-device rumble arbitration/integration, and the rest of the
   deleted 33-test input architecture matrix are not all replaced.
8. **Test-fight host lifecycle is partial.** Deterministic test-fight authoring and root startup are
   covered; repeated fight reset, command-line aliases/errors, and the old runtime loop semantics
   are not exhaustively characterized.

These gaps mean ECS-052 cannot be accepted solely from the surviving green unit suite. They should
be dispatched as isolated domain repairs, with visual verification treated as an independent hard
gate.

## Enemy-planning parity repair completed by this audit

`EnemyBehaviorParityTests` adds 29 focused cases:

- a frozen six-turn trace for every one of the 25 enemies, plus all three Fallen Shepherd phases;
- an authored-arsenal/generated-attack membership check for every phase;
- explicit tutorial/non-tutorial Horde behavior.

The new coverage found a real conversion regression: missing `TutorialSection` facts defaulted to
numeric section zero, which the planner interpreted as tutorial section 1-3 and therefore emitted
`TutorialHordeStrike3`. The legacy Horde fallback is `Pounce` when no guided tutorial is active.
`EnemyContentBehavior.TutorialAttack` now checks fact presence before interpreting the section, so
ordinary Horde battles again plan `Pounce` while section 8 turn 2 still plans
`TutorialHordeStrike6`.

Focused verification:

```bash
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj --no-restore \
  --filter FullyQualifiedName~EnemyBehaviorParityTests --maxcpucount:1
```

Result: 29 passed, 0 failed.
