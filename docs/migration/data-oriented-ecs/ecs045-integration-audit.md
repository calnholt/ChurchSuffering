# ECS-045 integration audit

ECS-045 passes its independent contract audit after one bounded snapshot-host repair. This audit covers presentation and rendering production sources only; it does not claim that the new runtime is wired into `Game1`, which remains ECS-050 work.

## Verified inventory and contracts

- The migration ledgers contain exactly 135 systems, 9 components, 38 events, and 175 event subscriptions owned by ECS-045. Every assigned ledger key appears in `ecs045-presentation-rendering-mapping.md`.
- All nine ledger component replacements are unmanaged `IComponent` structs included by `GeneratedComponentRegistry`. All 38 event payloads are unmanaged.
- `PresentationEventHub` exposes 38 distinct typed streams and a root-composable 38-route set with unique IDs. Attaching it to an isolated world routes and drains one event from every stream.
- Presentation `SystemId` values are valid and remain unique across the data-oriented assembly. Constructors do not inject another `IGameSystem`.
- The draw consumer has no `World` field or parameter. Its packet-store access is exposed as `ReadOnlySpan<RenderPacket>`, and the existing behavioral test confirms drawing leaves world state, structural-move count, and extraction version unchanged.
- Presentation/rendering sources contain no direct mouse, keyboard, or gamepad polling, no legacy `EventManager`, no LINQ query operators, and no obvious hot-path collection materialization. Reusable arrays grow only through explicit capacity paths.

## Snapshot fixture parity repair

The initial new-world host registered 38 IDs while the current legacy `DisplaySnapshotRegistry` registered 46. It omitted the two additional card-shader fixtures, the narrative-event modal, two climb inventory fixtures, and three card-list scroll fixtures. `NewWorldSnapshotFixtureHost` now registers the exact 46-ID set, with a uniqueness and exact-set regression test. The authoritative fixture usage and verification instructions remain in [`docs/display-snapshots.md`](../../display-snapshots.md).

## Remaining cutover gates

- ECS-050 must compose `PresentationEventHub.BuildRoutes` with the other domain routes, plus these systems, request queues, packet store, diagnostics, and external draw/audio/GPU sinks into the production new-world host.
- The relevant visual snapshots cannot demonstrate new-world pixel parity until that ECS-050 host performs actual fixture setup and extraction. Registration parity alone is intentionally not represented as snapshot verification.
- Final acceptance still requires warmed allocation measurements and the relevant `--verify` snapshot runs from `docs/display-snapshots.md` after cutover.
