# ECS-052 static snapshot parity

This record tracks fixture-specific data-oriented snapshot authoring that verifies against the
existing approved PNG baselines. Baselines are not copied into runtime resources and have not
been replaced or accepted during this work.

## Completed fixture clusters

| Fixture | Variants | Verification |
| --- | ---: | --- |
| `pause-menu` | 2 | unchanged baselines pass |
| `hotkey-hints` | 3 | unchanged baselines pass |
| `equipment-tooltip` | 3 | unchanged baselines pass |
| `guardian-angel` | 5 | unchanged baselines pass |
| `enemy-damage-meter` | 4 | unchanged baselines pass |
| `player-hud` | 6 | unchanged baselines pass |

The 23 verified frames are authored as ECS sprite and text packet inputs. Production resource
bindings use generated primitives for geometry and the real content-pipeline assets for equipment,
the gothic battle background, Guardian Angel art, player/enemy portraits, and the pledge icon.
The HUD fixtures exercise pixel-aligned antialiased masks and letter-spaced packet text; equipment
flavor copy also exercises the production bold-italic font binding.

## Verification

- The fixture-specific structural and production-resource tests live in
  `Ecs052PauseHotKeySnapshotAuthoringTests` and `Ecs052ProductionResourceCatalogTests`.
- The canonical commands remain in `docs/display-snapshots.md` and the fixture scripts under
  `scripts/` remain the multi-variant entry points.
- `dotnet test ... --filter FullyQualifiedName~DataOriented` covers the materializers, immutable text
  catalog, render extraction, and host resource bindings.

ECS-052 remains open until every required unit, integration, characterization, and approved visual
suite passes. Fixtures that still resolve to `GenericDefinition` are not considered parity-complete.
