# ECS-020 shared component layout

This report records the shared component/tag schema produced from the 21 rows assigned to
ECS-020 in `components.csv`, plus the new entity metadata contract required by the plan.
All entity ownership is implicit in the ECS row; no replacement component stores a legacy
`Owner` reference.

## Ledger-driven splitting

| Legacy entry | Data-oriented representation |
| --- | --- |
| `ActionPoints` | `ActionPoints.Current`; the legacy owner reference is removed. |
| `Animation` | Timing remains in `Animation`; booleans are packed into `AnimationFlags`, and the nested kind becomes the byte-backed `AnimationType`. |
| `Courage` | `Courage.Amount`; the legacy owner reference is removed. |
| `HP` | `HP.Max`, `Current`, and `UnscarredMax`; the legacy owner reference is removed. |
| `Intellect` | `Intellect.Value`; the legacy owner reference is removed. |
| `MaxHandSize` | `MaxHandSize.Value` with `DefaultValue` as immutable code metadata. |
| `ParallaxLayer` | Four numeric tuning fields remain together because the parallax query consumes them together. Static presets construct values without per-entity managed state. |
| `ParentTransform` | The legacy entity object becomes a generation-checked `EntityId`. |
| `Sprite` | Texture paths become `TextureAssetId`; the nullable source rectangle and visibility booleans become `SpriteFlags`. |
| `Temperance` | `Temperance.Amount`; the legacy owner reference is removed. |
| `Threat` | `Threat.Amount`; the legacy owner reference is removed. |
| `Transform` | Position, scale, rotation, and Z order remain one hot spatial component. |
| `UIElement` | Bounds, actions, suppression count, and pointer state remain in hot `UIElement`. Tooltip text/keyword strings and placement move to `TooltipMetadata` using `StringId`. Boolean interaction state is packed into `UIInteractionFlags`. |
| `InputContext` | The context string becomes `StringId`; booleans become `InputContextFlags`. |
| `PositionTween` | Target, current position, speed, and initialization state remain together for the tween system's sequential pass. |
| `DontDestroyOnLoad` | Becomes a fieldless tag. Scene ownership is represented only by `OwnedByScene`. |
| `DontDestroyOnReload` | Becomes a fieldless tag. |
| `OwnedByScene` | The legacy scene enum becomes the frozen byte-backed `SceneGroup`. |
| `SceneState` | Current scene uses the frozen byte-backed `SceneGroup`. |
| `ActorPresentationState` | Draw offset, scale multiplier, tint, and flash timer remain one hot actor-presentation component. |
| `BattlePresentationTransform` | Offset and scale remain one hot battle-presentation component. |

`EntityMetadata.Name` is the new shared indexed-name component required by the entity
metadata contract. Other legacy strings and catalog definitions remain assigned to their
domain owners; ECS-020 does not introduce a managed cold-component tier.

## Deterministic size report

Sizes are the managed-array element sizes returned by `Unsafe.SizeOf<T>()` on the .NET
runtime contract. `Ecs020ComponentLayoutTests` asserts every value and verifies that each
type is unmanaged. Rows are sorted by component name.

| Component | Bytes |
| --- | ---: |
| `ActionPoints` | 4 |
| `ActorPresentationState` | 24 |
| `Animation` | 12 |
| `BattlePresentationTransform` | 16 |
| `Courage` | 4 |
| `EntityMetadata` | 4 |
| `HP` | 12 |
| `InputContext` | 12 |
| `Intellect` | 4 |
| `MaxHandSize` | 4 |
| `OwnedByScene` | 1 |
| `ParallaxLayer` | 16 |
| `ParentTransform` | 8 |
| `PositionTween` | 24 |
| `SceneState` | 1 |
| `Sprite` | 28 |
| `Temperance` | 4 |
| `Threat` | 4 |
| `TooltipMetadata` | 16 |
| `Transform` | 24 |
| `UIElement` | 28 |
| **Total across component types** | **250** |

`DontDestroyOnLoad` and `DontDestroyOnReload` are registered as tags and therefore add
zero bytes to archetype rows. The CLR's empty-struct value size is not used for tag
storage.

## Focused verification

```bash
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj \
  --filter FullyQualifiedName~Ecs020
```

The focused tests cover generated registration, fieldless tags, exact sizes, a complete
component spawn/get round trip, and cached-query mutation with a persistence-tag filter.
