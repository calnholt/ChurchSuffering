# Content Catalog Signoff

ECS-030 through ECS-033 were signed off on 2026-07-13.

## Catalog coverage

| Domain | Stable IDs | Attributed modules |
| --- | ---: | ---: |
| Cards | 69 | 69 |
| Enemies | 25 | 25 |
| Enemy attacks | 91 | 91 |
| Equipment | 24 | 24 |
| Medals | 28 | 28 |

Each domain enables complete-catalog generation, so missing and duplicate stable IDs are
compile-time diagnostics. Generated catalogs use the frozen numeric values in
`stable-domain-ids.csv`; legacy factory keys are compared in the definition parity tests.

Definitions preserve immutable legacy metadata and upgrades. Exceptional behavior uses
generated static dispatch into staged read-only handler contexts and appends typed rule
commands/results. No converted module emits `RuleCommandKind.Custom` or writes directly
to a world.

## Verification

- Serialized solution build: passed with the two pre-existing unused-local warnings.
- Content definition and behavioral parity suites: 126 passed.
- All data-oriented runtime/content tests: 213 passed.
- Generator/analyzer tests: 19 passed.
- Release model/benchmark tests: 16 passed.
- Migration ledger audit: 1,663 entries classified exactly once.

The content suites cover every stable definition, every declared card and attack hook,
deterministic enemy planning, equipment/medal primary traces, staged acquisition and
reactive behavior, provider precedence, replacement suppression, and reset lifetimes.
