# ECS-040 Global, Scene, Input, and UI Mapping

This note maps every ECS-040 ledger row to its data-oriented replacement or to the
architecturally approved downstream extraction boundary. It does not change the frozen
CSV ledgers.

## Components

| Legacy row | Replacement |
| --- | --- |
| `InputContextMember` | Unmanaged `InputContextMember` with compact `StringId` membership. |
| `PlayerInputState` | Unique unmanaged `PlayerInputState` selected by `PlayerInputSingleton`. |
| `EntityListOverlay` | Unmanaged `EntityListOverlay`; presentation reads it in ECS-045. |
| `GameOverOverlayState` | Unmanaged `GameOverOverlayState`. |
| `LocationCustomizeButton` | Fieldless `LocationCustomizeButton` tag. |
| `ModalAnimation` | Unmanaged `ModalAnimation`, advanced by `ModalInputSuppressionSystem`. |
| `ModalInputSuppression` | Unmanaged per-element suppression contribution; no managed lookup set. |
| `NarrativeEventOverlayState` | Unmanaged state with compact string IDs. |
| `QueuedEvents` | Unmanaged header plus `DynamicBufferHandle<QueuedEventData>`. |
| `ScenePreparationState` | Unique unmanaged preparation counters; asset names/errors remain cold presentation data. |

The ECS-020 `SceneState`, `OwnedByScene`, `DontDestroyOnLoad`, and
`DontDestroyOnReload` contracts are reused rather than duplicated. `SceneTransitionState`
adds the explicit request/preparing/ready/activation state machine.

## Events

All fifteen assigned event rows are unmanaged values in `GlobalUiEventContracts.cs`:

- `PlayerCommandEvent`, `PlayerInputEvent`, and `SetPlayerInputEnabledEvent`;
- `DeleteCachesEvent`, `LoadSceneEvent`, `PrepareSceneEvent`,
  `ScenePreparationReady`, `SceneDeactivating`, `SceneActivating`, and `SceneActivated`;
- `NarrativeEventOverlayClosedEvent`, `OpenWayStationSaintsMedalsModalEvent`,
  `PrepareMusicTrackEvent`, `ShowNarrativeEventOverlay`, and `TreasureChestOpened`.

ECS-040 reserves event IDs 4001-4040. The five consolidated input/UI contracts
(`CursorInputEvent`, `UIHoverChangedEvent`, `UIClickEvent`, `UIActionEvent`, and
`TimerElapsedEvent`) occupy otherwise unused IDs in that range. `GlobalUiEventHub`
owns all twenty streams and returns all twenty routes through `BuildRoutes`; it never
creates or attaches an `EventRuntime`. The root can append those routes to the other
domain fragments and construct exactly one `EventRoutingEndpoint`.

World-state consumers use priority `100`, cross-domain consumers default to `0`, and
external host outputs use priority `-100`. The hub always installs the four required
world consumers for player frames, input enablement, scene loads, and scene-preparation
readiness. All other consumers are declared explicitly by the root through
`GlobalUiRouteConsumers`.

## Host and scheduler seams

`HostInputSnapshot` is the hardware-independent ECS-040 boundary. The external host
maps its keyboard, mouse, and gamepad state into primitive button masks, axes, pointer
coordinates, and render-destination values. `HostInputAdapter` then calculates edge
masks, transforms the pointer into virtual-canvas coordinates, and produces both
`PlayerInputEvent` and `CursorInputEvent`. Neither the adapter nor any data-oriented
system references `MouseState`, `GamePadState`, `KeyboardState`, or a hardware polling
API. The host publishes both events and drains the root barrier before scheduler update.

`HostCommandRequestQueue` is a fixed-capacity external hand-off for quit, fullscreen,
diagnostic overlay, debug-damage, and profiler requests. It never mutates world state or
calls `Game1`; the host drains it after scheduler completion. Snapshot mode suppresses
all nonessential host commands.

`GlobalUiComposition.Systems` is the operational allowlist:

- `PlayerInputSystem`, `ModalInputSuppressionSystem`, `UIInteractionSystem`, and
  `HotKeySystem`;
- `SceneLifecycleSystem` and `SceneLoadingCoordinatorSystem`;
- `TimerSchedulerSystem` and `HighlightSettingsSystem`;
- the generic overload also adds `EventQueueSystem<TState>` when the root supplies the
  shared rule runtime.

Their descriptors declare the component/tag access, cross-domain event IDs,
structural-command use, barriers, and same-phase dependencies used by scheduler
validation. `HotKeyProgressRingSystem`, `MusicManagerSystem`,
`SoundEffectManagerSystem`, and `UIElementHighlightSystem` remain named compatibility
responsibilities and are never included in the scheduler allowlist; ECS-045 owns their
packet/audio extraction replacements.

## Systems

| Legacy system row | ECS-040 result |
| --- | --- |
| `EventQueueSystem` | `EventQueueSystem<TState>` drives the shared two-lane `QueuedRuleRuntime<TState>` in the Rules phase. Its descriptor requires exclusive world access, and the root supplies explicit `runsAfter` dependencies for every same-phase operational system. |
| `SceneLoadingCoordinatorSystem` | Counter-based `SceneLoadingCoordinatorSystem`; typed `PrepareSceneEvent` consumers owned by presentation register asset jobs. |
| `HighlightSettingsSystem` | Unique-setting invariant system with a generated component descriptor. |
| `PlayerInputSystem` | Input-phase context and cursor resolver with cached queries and typed command output. |
| `TimerSchedulerSystem` | World timer component update plus typed elapsed event. |
| `UIInteractionSystem` | Interaction-phase reset, hover transition, click, and action dispatch. |
| `HotKeySystem` | `HotKeySystem` uses the arbitrated ECS-041 component for context eligibility, deterministic Z-order binding selection, unmanaged hold progress/cancellation/completion, parent targeting, and typed select/action events. |
| `HotKeyProgressRingSystem` | No mutating/drawing system in ECS-040. ECS-045 extracts a progress-ring packet from the final hot-key component; draw remains outside the scheduler. |
| `MusicManagerSystem` | No asset/device manager in ECS-040. `PrepareMusicTrackEvent` is typed here; ECS-045 owns audio request extraction and the external playback adapter. |
| `SoundEffectManagerSystem` | Superseded by ECS-045 typed SFX request extraction and external playback adapter. |
| `UIElementHighlightSystem` | Hover state is produced by `UIInteractionSystem`; ECS-045 extracts and renders highlight packets. No ECS-040 draw method or graphics resource is retained. |

The host must copy the activated unique `SceneState.Current` to
`SystemScheduler.ActiveScene` after an activation barrier. This is intentionally an
ECS-050 host responsibility because systems may not reference another system or the
scheduler.

## Event-subscription rows

The subscription ledger incorrectly makes ECS-040 the apparent behavioral owner of
several card/enemy/medal subscribers; its stated migration decision is the shared
world-owned typed routing mechanism. Their mapping is therefore:

- `EventQueueBridge` inferred delegate becomes `EventQueueSystem<TState>` plus the
  generated `IRuleRoutingEndpoint<TState>`; no delegate discovery remains.
- Card routes: `DeusVult` (`CardPlayedEvent`, `ChangeBattlePhaseEvent`), `Graveward`
  (`TopCardRemovedForMillEvent`), `MaleficRite` (`TrackingEvent`), `Purge`
  (`PledgeAddedEvent`), and `RelentlessStrike` (`EnemyKilledEvent`) use the ECS-030/041
  static handler and typed trigger routes.
- Enemy routes: `DustWuurm` and `FireSkeleton` (`ChangeBattlePhaseEvent`),
  `GlacialGuardian` (`CardMoved`), and `Succubus` (`ModifyCourageEvent`) use the
  ECS-031/042 static handler and typed trigger routes.
- Medal routes: `StAnthonyOfPadua` (`ChangeBattlePhaseEvent`, `DrawPileEmptyEvent`),
  `StAugustine` (`ChangeBattlePhaseEvent`), `StBartholomew`
  (`ChangeBattlePhaseEvent`, `ModifyHpRequestEvent`), `StBenedict`
  (`PledgeAddedEvent`), `StClare` (`ChangeBattlePhaseEvent`), `StElijah`
  (`ChangeBattlePhaseEvent`, `PledgeAddedEvent`), `StFrancisDeSales`
  (`ChangeBattlePhaseEvent`), `StHomobonus` (`ShowQuestRewardOverlay`), `StIgnatius`
  (`ChangeBattlePhaseEvent`), `StJerome` (`ApplyPassiveEvent`), `StJoanOfArc`
  (`CardPlayedEvent`), `StLazarus` (`TopCardRemovedForMillEvent`), `StLonginus`
  (`PledgeAddedEvent`), `StLouieIX`, `StLuke`, and `StMichael`
  (`ChangeBattlePhaseEvent`), `StMonica` (`TriggerTemperance`), `StPaulMiki`
  (`CardBlockedEvent`, `ChangeBattlePhaseEvent`), `StPeter` (`CardBlockedEvent`),
  `StRita` (`ChangeBattlePhaseEvent`, `TrackingEvent`), `StSebastian`
  (`EnemyKilledEvent`), and `StSimonOfCyrene` (`ChangeBattlePhaseEvent`) use the
  ECS-032/043 provider, replacement, and typed trigger routes.
- `SceneLoadingCoordinatorSystem`'s `SceneTransitionRequested` input is now
  `LoadSceneEventConsumer`; `SceneActivated` is typed output consumed by preparation
  adapters rather than a static subscription.
- `HotKeySystem`'s `DeleteCachesEvent` becomes an ECS-045 cache consumer;
  `HotKeyHoldCompletedEvent` is published by the new input-side `HotKeySystem`.
- Music subscriptions (`AudioSettingsChangedEvent`, `ChangeMusicTrack`,
  `PrepareMusicTrackEvent`, `StopMusic`) and SFX subscriptions
  (`AudioSettingsChangedEvent`, `PlaySfxEvent`, `StopSfxEvent`) become generated ECS-045
  audio routes. ECS-040 supplies the assigned `PrepareMusicTrackEvent` value.
- `PlayerInputSystem`'s `SetPlayerInputEnabledEvent` is
  `SetPlayerInputEnabledEventConsumer` and writes the unique input component.
- `GoldManagementService`'s inferred delegate becomes a typed meta-resource route in
  ECS-044; services no longer mutate world state.
- `Game1`'s `DeleteCachesEvent` and `PlayerCommandEvent` handlers become ECS-050 host
  route consumers. The ECS-040 systems publish both typed values and never call `Game1`.

This accounts for all 51 post-cutover ECS-040 subscription rows without preserving static event
subscriptions, runtime delegates, or direct behavior-object callbacks.

The two former `Game1` static subscriptions were removed when ECS-050 switched the host to routed
request consumers.
