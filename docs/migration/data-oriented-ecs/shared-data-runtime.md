# Shared Data Runtime Signoff

This document records the ECS-020 through ECS-023 integration gate signed off on
2026-07-13. The frozen namespace and stable-ID rules in `contracts.md` remain
normative.

## Frozen authoring boundary

- Shared unmanaged components and fieldless tags live in
  `Crusaders30XX.ECS.DataOriented.Components`. Their current layout report is in
  `ecs020-component-layout.md`.
- Content modules are top-level static partial classes annotated with one of the
  domain definition attributes in `Crusaders30XX.ECS.DataOriented.Definitions`.
  Domain code adds modules; it does not edit a central registration switch.
- Applying `[assembly: DefinitionCatalog(typeof(TId))]` enables compile-time
  completeness checking for a converted domain. Duplicate IDs and invalid module,
  table, or handler shapes are always compile-time errors.
- Generated dense catalogs live in
  `Crusaders30XX.ECS.DataOriented.Generated`. Lookup returns array-backed values or
  spans, and exceptional handler dispatch is a generated direct switch.
- Exceptional handlers receive the domain-specific ref-struct context in
  `Crusaders30XX.ECS.DataOriented.Rules`. The context exposes `ReadOnlyWorld`, compact
  targets, stable definition IDs, and an append-only `RuleCommandWriter`.
- Runtime rule output uses the fixed-layout unmanaged `RuleCommand` union. Command
  sequence numbers are assigned by `RuleCommandBuffer` in append order.

The initial gate was reopened during the first content probes because planning,
lifecycle triggers, deterministic random input, provider/replacement behavior, and
several typed rule effects were not yet representable. The amended boundary now also
freezes:

- named stat, effect, condition, trigger, fact, handler, meta-resource, and visual-recipe
  IDs;
- sorted caller-owned rule facts and deterministic caller-owned random state;
- typed rule commands for requirements, result dependencies, scheduling, card/deck
  operations, instance mutation, explicit spawning, max-resource changes, and meta
  resource changes;
- unified staged card, enemy, attack, equipment, and medal contexts with bounded
  span-backed result and enemy-plan writers;
- typed equipment/medal trigger envelopes, reset/lifetime state, alternate-play and
  stat providers, and replacement plans with deterministic equipped-entity precedence.

The content probes were rerun against this shape at the contract level. Their previously
reported behaviors are now representable without direct world writes, runtime delegates,
or unregistered integer handlers.

## Gate verification

- Serialized solution build: passed with two pre-existing unused-local warnings.
- ECS-020/ECS-022 focused runtime tests: 10 passed.
- Generator/analyzer tests: 19 passed, including emitted-assembly execution of direct
  static handler dispatch.
- Amended ECS-023 runtime contract tests: 21 passed (31 together with ECS-020/ECS-022).
- Warmed rule-handler invocation and command append: zero allocated bytes.
- Shared component layout: 21 unmanaged components totaling 250 bytes when measured
  independently; two persistence tags have no instance fields and contribute no chunk
  row bytes.
- Migration ledger audit: 1,663 entries classified exactly once.

`ReadOnlyWorld` deliberately does not expose the mutable-span query API. Definition
handlers receive explicit target handles or read-only target buffers; mutation remains
represented by appended rule commands.
