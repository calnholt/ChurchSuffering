# ECS-041 card gameplay mapping

This document is the owned signoff for ECS-041. The implementation is under
`ECS/DataOriented/Gameplay/Cards/`; its focused tests are under
`tests/Crusaders30XX.Tests/DataOriented/Gameplay/Cards/`.

## Runtime invariants

- A card's identity is its `EntityId`; `CardId` is definition data only. Duplicate definitions therefore remain independent entities.
- `Deck` owns typed dynamic buffers for master membership, draw, hand, discard, exhaust, and assigned blocks. Zone buffers are ordered and use stable removal, never swap-back removal.
- Master membership is not an exclusive zone. A normal card remains in the master buffer while occupying exactly one gameplay zone.
- The draw-pile top is index zero. An empty draw publishes `DrawPileEmptyEvent` and does not implicitly reshuffle discard.
- All random selection and shuffle operations advance the deck's caller-owned `RuleRandomState`.
- Spawn commands record deferred entities in `CommandBuffer`; `DeckManagementSystem` finalizes their master and zone membership after playback.
- Generated `GeneratedCardCatalog` metadata initializes instances and dispatches every card hook. Card handlers emit only typed `RuleCommand` values.
- Cost matching is order-independent and allocation-free. Colorless cards satisfy `Any`, never a named color. Pledged and weapon cards cannot pay costs.
- Provider precedence uses the shared stable-entity ordering contracts: alternate play selects the first applicable provider and stat modifiers accumulate in stable order.

## System ledger (26/26)

| Legacy responsibility | New type | Phase / role |
| --- | --- | --- |
| ActionPointManagementSystem | `ActionPointManagementSystem` | Gameplay; typed AP set/delta consumer |
| AssignedBlocksToDiscardSystem | `AssignedBlocksToDiscardSystem` | Gameplay; assigned-block cleanup owner |
| DrawHandSystem | `DrawHandSystem` | Gameplay; pledged/token/weapon-aware draw count |
| CanPlayCardHighlightSystem | `CanPlayCardHighlightSystem` | Presentation; highlight state only |
| CantPlayCardMessageSystem | `CantPlayCardMessageSystem` | Presentation; typed rejection display input |
| CardApplicationManagementSystem | `CardApplicationManagementSystem` | Gameplay; application mutation owner |
| CardHoverDetectionSystem | `CardHoverDetectionSystem` | Interaction; cursor-derived hover only |
| CardPlaySystem | `CardPlaySystem` | Rules; validation, payment, generated dispatch and lifecycle |
| CardZoneSystem | `CardZoneSystem` | Gameplay; single ordered-zone movement authority |
| CursedManagementSystem | `CursedManagementSystem` | Gameplay; exact original definition/upgrade restoration |
| DeckEmptyDeathCheckSystem | `DeckEmptyDeathCheckSystem` | Gameplay; empty live-zone predicate |
| DeckManagementSystem | `DeckManagementSystem` | Gameplay; reset/shuffle/draw/discard/spawn finalization |
| DiscardSpecificCardHighlightSystem | `DiscardSpecificCardHighlightSystem` | Presentation; discard selection state |
| HandBlockInteractionSystem | `HandBlockInteractionSystem` | Interaction; typed block request owner |
| HandCardBoundsLateSystem | `HandCardBoundsLateSystem` | LatePresentation; bounds extraction only |
| MarkedForExhaustSystem | `MarkedForExhaustSystem` | Gameplay; marked/end-turn exhaust resolution |
| MarkedForSpecificDiscardSystem | `MarkedForSpecificDiscardSystem` | Gameplay; specific-discard lifecycle owner |
| MillCardSystem | `MillCardSystem` | Gameplay; ordered top-card mill and typed event |
| PledgeManagementSystem | `PledgeManagementSystem` | Gameplay; availability, once-per-action state, unlock/removal |
| PlunderManagementSystem | `PlunderManagementSystem` | Gameplay; deterministic snatch, threshold and rescue |
| RecoilManagementSystem | `RecoilManagementSystem` | Gameplay; deterministic marking and penalty aggregation |
| SealManagementSystem | `SealManagementSystem` | Gameplay; pledge-immune sealing and countdown |
| ShackleManagementSystem | `ShackleManagementSystem` | Gameplay; deterministic linked-card marking |
| CardListModalSystem | `CardListModalSystem` | Interaction; modal buffer/selection owner |
| CardShaderCompositorSystem | `CardShaderCompositorSystem` | RenderExtraction; pass data only |
| CardUsageTrackingSystem | `CardUsageTrackingSystem` | Gameplay; typed play/block/payment tracking |

`CardGameplayComposition.CompatibilitySystems` retains exactly these 26 names with unique stable
IDs for ledger/API audit. It is deliberately not a scheduler registration list.
`CardGameplayComposition.Systems` is the explicit operational allowlist. At this boundary it
contains only `DeckManagementSystem`, the sole card system with meaningful per-frame work
(deferred-spawn finalization). Event consumers and deterministic API owners run through typed
routes or callers instead of receiving empty scheduler updates.

## Component ledger (57/57)

The exact ledger names are represented as unmanaged components or empty tags:

`AnimatingHandToDiscard`, `AnimatingHandToDrawPile`, `AnimatingHandToZone`, `Brittle`,
`CanPlayHighlightSettings`, `CardData`, `CardGeometrySettings`, `CardListModal`,
`CardListModalClose`, `CardListModalSelectionMetadata`, `CardPlayStatContext`, `CardSheen`,
`CardToDiscardFlight`, `CardTooltip`, `Colorless`, `Cursed`, `CursedOriginalCard`, `DebugMenu`,
`Deck`, `EquippedWeapon`, `FilteredFromHand`, `Frozen`, `HPBarAnchor`, `HPBarOverride`, `Hint`,
`HotKey`, `Intimidated`, `LastPaymentCache`, `MarkedForBottomOfDrawPile`,
`MarkedForEndOfTurnDiscard`, `MarkedForExhaust`, `MarkedForReturnToDeck`,
`MarkedForSpecificDiscard`, `ModifiedDamage`, `PayCostCancelButton`, `PayCostOverlayState`,
`Player`, `Pledge`, `PledgeAvailabilityState`, `PledgePreview`, `PlunderRescueFlight`,
`PlunderSnatchFlight`, `Plundered`, `PortraitInfo`, `ProfilerOverlay`, `Recoil`, `Scorched`,
`Sealed`, `SelectedForPayment`, `Shackle`, `SuppressCardVisualEffects`, `SuppressCardZoneRender`,
`SuppressPortraitRender`, `SuppressStatDeltaDisplay`, `Thorned`, `TooltipOverrideBackup`, and
`UIDropdown`.

Domain-only supporting storage consists of `CardZoneLocation`, `PendingCardSpawn`,
`CardApplications`, and typed buffer elements. These do not replace or change shared ECS contracts.

## Event ledger (58/58)

All ECS-041 event payloads are unmanaged:

`AlertEvent`, `ChangeBattleLocationEvent`, `ApplyCardApplicationEvent`, `ApplyRecoilEvent`,
`CantPlayCardMessage`, `CardDiscardedForCostEvent`, `CardInHandHoveredEvent`,
`CardListModalCardSelectedEvent`, `CardListSelectionContexts`, `CardMoved`, `CardPlayedEvent`,
`CardShaderPassEvent`, `CardUpgradeConfirmedEvent`, `CardsDrawnEvent`, `CloseCardListModalEvent`,
`ClosePayCostOverlayEvent`, `DebugCommandEvent`, `DeckShuffleDrawEvent`, `DeckShuffleEvent`,
`DiscardAllCardsEvent`, `DiscardMarkedForSpecificDiscardEvent`, `DrawPileEmptyEvent`,
`DrawRandomCardFromDiscardEvent`, `EndTurnDisplayEvent`, `HandLayoutEvent`,
`HotKeyHoldCompletedEvent`, `IntimidateEvent`, `MarkedForSpecificDiscardEvent`, `MillCardEvent`,
`ModifyActionPointsEvent`, `ModifyCourageEvent`, `ModifySealsEvent`, `OpenCardListModalEvent`,
`OpenPayCostOverlayEvent`, `PayCostCandidateClicked`, `PayCostSatisfied`, `PledgeAddedEvent`,
`RedrawHandEvent`, `RemoveCardApplication`, `RemoveCardApplications`, `RemoveRandomCardEvent`,
`ResetDeckEvent`, `SealCardsEvent`, `SetActionPointsEvent`, `SetCourageEvent`, `ShackleEvent`,
`ShuffleRandomCardsFromDiscardToDrawPileEvent`, `ShuffleSealedIntoDrawPileEvent`,
`StartOfTurnDrawResolvedEvent`, `TopCardRemovedForMillEvent`, `CursorStateEvent`,
`HotKeySelectEvent`, `UIElementHoverEnteredEvent`, `PlunderCardEvent`, `PlunderForceDiscardEvent`,
`PlunderRescueEvent`, `PlunderTriggerEvent`, and `TrackingEvent`.

`CardGameplayEventHub` owns one instance stream for every event above. IDs occupy the stable,
contiguous range 41001–41058. `BuildRoutes` returns all 58 `IEventRoute` fragments without creating
or attaching an `EventRuntime`; the application root combines them with the other domains into one
endpoint. `CardGameplayRouteConsumers` accepts explicitly named, prioritized cross-domain
consumers. Composition injects the nine card-owned AP/deck consumers at priority 100 and appends
root consumers in declaration order. No assembly scanning, delegate discovery, or
publish-by-runtime-type is used.

## Scheduler registration contract

`DeckManagementSystem` is the only currently scheduled card-domain system. Its descriptor declares
the `PendingCardSpawn`/`CardData` reads, `Deck`/`CardZoneLocation` writes, all five zone buffer
read/write element types, seven consumed deck event IDs, three emitted movement/draw event IDs,
the after-system event barrier, and structural commands. This includes the structural removal of
`PendingCardSpawn` recorded during finalization.

The other 25 compatibility types are classified as event consumers, deterministic API owners,
consolidated legacy names, or ECS-045 extraction names. A root host registers only `Systems`; it
must never reflect over `CompatibilitySystems` or all `CardGameplaySystem` subclasses.

## Subscription ledger (90/90)

The 90 legacy subscription occurrences are reconciled by owner below. Counts match
`event-subscriptions.csv`; repeated cache/presentation subscriptions collapse into query-owned state,
while cross-domain notifications enter through generated composition routes.

| Owner | Rows | New routing responsibility |
| --- | ---: | --- |
| ActionPointManagementSystem | 3 | AP set/delta typed routes; phase reset is a composition input |
| AssignedBlocksToDiscardSystem | 1 | typed debug command route |
| BattlePhaseDrawSystem | 1 | phase transition to draw request |
| CanPlayCardHighlightSystem | 2 | highlight extraction query; cache deletion has no runtime subscription |
| CantPlayCardMessageSystem | 1 | `CantPlayCardMessage` route |
| CardApplicationManagementSystem | 3 | apply/remove-one/remove-many routes |
| CardListModalSystem | 4 | open/close/select routes; cache deletion becomes state reset |
| CardPlaySystem | 2 | play request and `PayCostSatisfied` routes |
| CardShaderCompositorSystem | 3 | base-pass start/completed routes; cache deletion becomes state reset |
| CardUsageTrackingSystem | 3 | played/blocked/discarded-for-cost routes |
| CardZoneSystem | 6 | move/finalize/moved/phase/defeat/reserve-return routes |
| CursedManagementSystem | 5 | application routes plus battle/scene lifecycle routes |
| DeckEmptyDeathCheckSystem | 4 | draw-resolved/death/battle-start routes; cache deletion becomes state reset |
| DeckManagementSystem | 11 | shuffle/draw/redraw/reset/remove/discard/random movement/scene routes |
| HandBlockInteractionSystem | 1 | typed assign-block route |
| MarkedForExhaustSystem | 1 | defeat-finalization route |
| MarkedForSpecificDiscardSystem | 3 | mark/discard/attack-resolved routes |
| MillCardSystem | 3 | mill/top-removed routes; cache deletion becomes state reset |
| PledgeManagementSystem | 10 | phase, pledge/apply/random/remove, battle/reset/move/discard/redraw routes |
| PlunderManagementSystem | 6 | phase/damage/trigger/animation/discard/reset routes |
| RecoilManagementSystem | 5 | apply/block/attack-resolved/defeat/enemy-reset routes |
| SealManagementSystem | 5 | seal/move/play/modify/shuffle routes |
| ShackleManagementSystem | 7 | block/unassign/phase/move/defeat/reset routes; cache deletion becomes state reset |

Total: 90. The two ledger systems without legacy subscriptions (`CardHoverDetectionSystem` and
`HandCardBoundsLateSystem`) run from ECS queries in their declared phases.

## Verification

`CardGameplayRuntimeTests` covers ordered traces, duplicates, deterministic shuffle, empty-draw
behavior, upgrade/cost/colorless/pledge rules, curse restoration, deferred typed spawning, all 58
route fragments and stable IDs, local/root consumer order, the operational scheduler allowlist and
descriptor completeness, all 26 compatibility responsibilities, and zero-allocation warmed query
enumeration. Existing generated card behavior trace tests remain the exhaustive 69-definition
lifecycle/command parity suite.
