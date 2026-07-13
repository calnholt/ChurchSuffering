# ADR 0011: Project-Owned Data-Oriented Archetype ECS

- Status: Accepted
- Date: 2026-07-13
- Decision owner: ECS conversion coordinator
- Implementation plan: `docs/plans/data-oriented-ecs-repository-conversion-plan.md`

## Context

The legacy ECS stores components as managed objects behind per-entity dictionaries and
iterates entity references rather than contiguous component data. The approved full
repository conversion requires exact behavior parity while replacing that runtime with
cache-oriented storage and eliminating steady-state ECS allocations.

## Decision

The replacement is a project-owned, managed-memory archetype/chunk ECS. Its normative
contracts are frozen in `docs/migration/data-oriented-ecs/contracts.md`; implementation
tasks may fill in those contracts but may not substitute another storage model or retain
the legacy ECS as a compatibility layer.

The conversion uses a distinct `Crusaders30XX.ECS.DataOriented` namespace until the clean
cutover. Runtime code remains deterministic and single-threaded. Components are unmanaged
structs, markers are empty tags, entity relationships use generation-checked `EntityId`
values, and variable-length state uses world-owned typed buffers. Systems own behavior,
declare access metadata, and record structural changes through command buffers. Events
and queued rules are world-owned typed streams with deterministic barriers and ordering.

Stable gameplay/content IDs remain domain contracts. Generated component, tag, event, and
system IDs are build-local runtime details and are never persisted.

## Consequences

- The runtime and generator can be developed beside the legacy namespace for isolated
  verification, but partially converted systems are not registered in the live game.
- Domain conversions depend on the foundation and shared-contract gates in the approved
  plan.
- The final cutover removes the legacy entity, component, system, event, queued behavior,
  factory, and polymorphic behavior runtime instead of adapting or mirroring it.
- Save compatibility is intentionally not preserved; fresh-run DTO materialization and
  extraction still require parity tests.
- Any change to the frozen contracts, type limit, scheduler ordering, scene grouping, or
  stable domain IDs requires an explicit ADR/plan revision approved by the coordinator.

## Verification

`scripts/audit-data-oriented-ecs-ledgers.rb` verifies that every discovered legacy
component, event/support type, event subscription, system, and object/content behavior
type has exactly one target classification and exactly one owning migration task.
