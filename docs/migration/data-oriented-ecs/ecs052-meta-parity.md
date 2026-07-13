# ECS-052 meta-game parity audit

Status: guided-tutorial definition and typed progression parity implemented; climb economy and
reward persistence remain blocked by missing runtime state.

## Implemented guided-tutorial lane

`GuidedTutorialCatalog` is the immutable data-oriented replacement for the deleted
`GuidedTutorialDefinitions` object graph. Managed instructional text and target metadata remain in
the catalog; authored ECS state stores only a compact `StringId`, action code, completion byte, and
world-owned `TutorialStepEntry` buffer.

Exact characterized coverage:

| Contract | Covered count |
| --- | ---: |
| Sections | 8 |
| Turns | 9 |
| Stock-hand card entries | 33 |
| Enemy attack entries | 9 |
| Guided messages | 14 |
| Phase/message schedule entries | 14 |
| Pending dialogue keys | 3 |
| Typed progression commands | 4 (`start`, `advance`, `restart`, `skip`) |

The catalog preserves section/player/enemy HP, teach flags, draw-pile visibility, both section-8
turns, card colors/colorless state, attack IDs, message text, target type/ID, orientation,
conditions, and block/action phase placement. `Materialize` authors a generation-checked tutorial
entity and owned dynamic step buffer without behavior objects or per-instance managed data.

End-to-end event coverage proves:

1. `TutorialStartedEvent` starts at step zero and retains the supplied tutorial ID.
2. An action which does not match the current step leaves progress unchanged.
3. Matching actions complete steps in order.
4. Normal completion publishes both `TutorialCompletedEvent` and the already-declared terminal
   `AllTutorialsCompletedEvent` exactly once.
5. The completion event retains the tutorial ID instead of the previous hard-coded zero.
6. Restart clears every completion byte and returns to step zero.
7. Skip enters `Skipped` and publishes one terminal notification.

Focused executable coverage is in
`tests/Crusaders30XX.Tests/DataOriented/Gameplay/Meta/Ecs052GuidedTutorialParityTests.cs`.
The lane passes 13 test cases, including nine exact stock-hand cases.

## Guided-tutorial cases not yet covered

| Missing case | Runtime blocker |
| --- | --- |
| Spawn tutorial battle actors, HP, deck/stock hand, and phase state from a section | The meta tutorial runtime has no battle-session authoring hand-off. |
| Automatically prepare section/turn stock hands | `StockHand` exists, but no operational system consumes this catalog into card/deck buffers. |
| Feed section/turn facts to tutorial enemy planning | Enemy planning supports tutorial facts, but root composition does not connect tutorial progression to those facts. |
| Advance section 8 to turn 2 without rebuilding turn 1 | No typed turn-transition event/state exists in the meta runtime. |
| Evaluate message conditions against the current hand | The catalog retains condition strings, but no condition evaluator or card-target resolver is connected. |
| Resolve transformed HUD/card/equipment target bounds | No data-oriented tutorial target/bubble presentation system exists. |
| Show the three pending dialogues after their sections | Dialogue keys are catalogued, but completion is not connected to dialogue authoring. |
| Persist whole-campaign guided-tutorial completion | `MetaSaveDto` has no tutorial-progress field by current design. |

The deleted input-gate characterization allowed all battle actions while a tutorial was active.
The new generic UI gate instead permits tagged targets through `TutorialInteractionPermitted`; it
does not yet express per-step action gating. This lane does not silently choose new input semantics.

## Climb/shop/reward cases not yet covered

The surviving `DeterministicClimbGenerator` proves repeatable generic slot generation only. It does
not implement the deleted climb economy characterization:

| Missing case | Runtime blocker |
| --- | --- |
| Exact initial shop/encounter/event schedules, durations, diversity, and expiry | `ClimbSlotEntry` has only column/row/kind/content/price/roll; no generated-at time, duration, enemy, location, mutation, or reward payload. |
| Spend colored resources and advance climb time on purchase | No live run resource/time component or buffer exists. `MetaSaveDto.Gold` is boundary data only. |
| Purchase/equip medals and equipment | `ClimbShopSlotSelectedEvent` currently only sets `Purchased`; it does not validate price or materialize inventory changes. |
| Upgrade/replace the selected deck entry while preserving required identity/state | Shop action state does not identify reward kind/payload sufficiently and has no pending replacement buffer. |
| Reject sold or unaffordable offers atomically | Sold is checked, but affordability and atomic wallet/inventory writes cannot be represented. |
| Complete encounters, persist resources/reward offer, reroll slots, and queue the final encounter | No queued encounter, climb resource reward, pending encounter reward, or final-encounter state exists. |
| Populate/apply/skip quest and booster rewards across save extraction/spawn | Overlay buffers can be created, but current events carry no full reward payload and no acceptance mutation is implemented. |

Adding those cases requires an approved expansion of meta components/events/save DTOs and a root
hand-off to card/equipment/medal authoring. Marking the existing purchased bit or empty overlay as
parity would lose gameplay state and is not acceptable.
