# ECS-052 input parity repair

Status: the host-to-ECS input boundary again carries the stable device, button, and glyph
vocabulary required by keyboard/gamepad hot keys, and the first-party pause, pile, card, tutorial,
dialog, and booster consumers are operational. This repair remains hardware-independent:
MonoGame polling stays in `ECS/Input/MonoGamePlayerInputAdapter.cs`, and data-oriented gameplay
receives only `HostInputSnapshot` primitives.

## Restored contracts

- `PlayerInputFrame` now preserves `PreviousDevice`, `DeviceChanged`, gamepad connectivity, and
  Xbox/PlayStation glyph style.
- The existing eleven data-oriented button IDs retain their numeric values. Physical bindings
  missing at cutover were appended: Escape, Back/View, B/X/Y, Start, shoulders, left-stick click,
  Space, Enter, Shift, and WASD movement.
- The central frame adapter maps every legacy hardware-adapter button. Semantic aliases are
  additive: left-stick click also maps to ShowHint, and Shift also maps to Modifier.
- `PlayerInputGlyphs` resolves gamepad-capable stable bindings to a small unmanaged glyph
  vocabulary. Rendering can select Xbox or PlayStation art from the frame style without importing
  MonoGame input state.
- An in-progress hot-key hold is canceled when the active device changes. A fresh press on the
  replacement device may still begin a new hold on the transition frame.
- Inactive windows continue to clear down/pressed/released actions while retaining device
  capability metadata for deterministic hint presentation.

## Restored consumers

- `PauseMenuInputSystem` toggles the unmanaged pause overlay with keyboard Escape or gamepad Start.
  Back remains a distinct binding and does not pause.
- `BattlePileInputSystem` opens, closes, and switches the display-only draw/discard modal with the
  shoulder buttons. It ignores unrelated card-list modals and frozen/defeated battle state.
- Guided-tutorial pile visibility matches the legacy contract: the discard pile is hidden during
  an active guided tutorial, while the draw pile becomes available only in section 8.
- `CardListModalSystem` now materializes the requested ordered card zone into its unmanaged modal
  buffer and owns open/close state through typed card events.
- The existing meta cross-domain registration now consumes validated `UIActionEvent` values and
  routes hand-card play/block/pledge requests by combat phase. Cards outside the hand remain
  display-only.
- Root-owned route composition connects those typed requests to card-owned and combat-owned
  consumers. Card play performs deterministic auto-payment and resource synchronization; pledge
  uses the card rule owner; block assign/remove and confirm/end-turn use the active combat
  session. No input consumer writes gameplay state directly.
- Tutorial/dialog skip actions resolve the active overlay when the clicked control is a child.
  Booster close is swallowed until every authored reward has been revealed.

The two new operational systems are registered through their existing compositions. The pile
system explicitly runs after `UIInteractionSystem`; event output remains owned by the card/meta
hubs and the application still has one root event runtime.

## Executable coverage

`Ecs052InputParityTests` provides 41 deterministic cases covering all stable button mappings,
semantic aliases, previous-device transitions, connectivity, glyph style and glyph resolution,
inactive-window behavior, and cross-device hold cancellation. `Ecs052InputConsumerParityTests`
adds 7 deterministic consumer cases for pause, pile switching/visibility/freezing, phase-aware card
routing, booster settlement, and tutorial-child routing. The adjacent ECS-040/ECS-050, card, and
meta suites remain green.

Focused verification:

```bash
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj --no-restore \
  --filter FullyQualifiedName~Ecs052InputParityTests --maxcpucount:1
```

Result: 48 input and consumer cases passed, 0 failed. The focused adjacent suite completed with
102 passed, 0 failed.

## Remaining domain work

Mixed-device rumble requests now drain through the production host into the centralized four-motor
mixer, including delayed segments, group clears, enabled state, and global strength. Rich
pause-menu option navigation (sliders/toggles) and selectable, non-pile card-list workflows still
depend on their owning presentation/meta authoring.
