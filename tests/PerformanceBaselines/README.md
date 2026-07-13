# Legacy ECS baseline

`legacy-ecs-initial.json` is the ECS-000 reference capture for the object-based ECS.
It contains fixed-seed CPU workloads for representative battle and Climb iteration,
including entity/component counts, system/query calls, processed rows, allocations,
GC counts, and ECS update timing. The fixture deliberately does not initialize a
graphics device, so CPU draw-submission timing is marked unsupported.

Capture a new local result without replacing the committed initial artifact:

```bash
./scripts/capture-legacy-ecs-baseline.sh
```

Pass an explicit output path only when a named comparison artifact is required:

```bash
./scripts/capture-legacy-ecs-baseline.sh debug/performance/legacy-ecs-comparison.json
```

The script always runs `LegacyEcsPerformanceFixtureTests` in Release. The fixture
uses seed `1337`, disables shaders, leaves diagnostic overlays unopened, warms for
120 frames, and samples 300 frames at a fixed 120 Hz simulation delta.

## ECS-000 test invocation diagnosis

The previously reported silent/non-completing invocation is reproducible as delayed
console output, but not as a hung test host. On this reference machine the full suite
completed in about 14 seconds after discovery. The build and test host can remain
silent while MSBuild is compiling, and the suite then emits a very large volume of
application logging (more than 80,000 captured output tokens in the diagnostic run).
Callers that buffer output therefore appear silent and may report completion late.

For actionable progress and a compact retained failure record, use a normal console
logger plus TRX output:

```bash
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj \
  --logger "console;verbosity=normal" \
  --logger "trx;LogFileName=legacy.trx"
```

At baseline capture time, the existing suite completed with 1,101 tests: 1,092
passed and 9 failed. Eight failures are the cases of
`AbilityEquipmentTests.Ability_equipment_has_activation_only_metadata` (expected
zero block, actual one), and one is
`EquipmentServiceTests.Activation_only_shop_tooltip_omits_block_and_uses_and_includes_free_action`
(the tooltip contains `Block: 1`). These failures predate ECS-000 and were not
changed because baseline work must preserve legacy behavior.
