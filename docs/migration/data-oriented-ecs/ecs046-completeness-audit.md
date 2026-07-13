# ECS-046 completeness audit

Status: audit gates are implemented for ECS-040 through ECS-045. Coordinator acceptance is still
required before the plan item is marked complete.

Run the strict gate from the repository root:

```bash
scripts/audit-data-oriented-ecs-completeness.rb --write
dotnet test tests/Crusaders30XX.Tests/Crusaders30XX.Tests.csproj \
  --filter FullyQualifiedName~DataOriented.Integration.Ecs046CompletenessAuditTests
```

The generated detailed inventory is
[`ecs046-pre-audit-findings.md`](ecs046-pre-audit-findings.md). It records every completed-domain
legacy system and event-subscription ledger key plus conservative line-level candidates for legacy
entity references, component lookups, LINQ queries, service mutations, and draw-state mutations.
Those legacy occurrences are an ECS-050/ECS-051 cutover and deletion inventory; they are not a
failure while their runtime responsibility has a complete new-world mapping.

## Blocking gates

The strict audit fails for any of these conditions in a completed domain:

- a component, event, system, event-subscription, or object-behavior ledger row without mapping evidence;
- a new data-oriented dependency on the old ECS namespace or static `EventManager`;
- direct MonoGame hardware-state polling, a legacy component lookup, or a LINQ query in new runtime
  production source;
- a domain-owned `EventRuntime` construction or attachment instead of root route fragments;
- an operational scheduler owner with an empty `Update`;
- an operational scheduler owner with neither declared component/buffer/event access nor the
  explicit conservative `RequiresExclusiveWorldAccess` contract.

`RequiresExclusiveWorldAccess` is reserved for the queued-rule driver because the queued command
kind selects its component and buffer writes at runtime. The scheduler treats it as conflicting
with every same-phase system and rejects the graph unless an explicit dependency path orders the
pair. The root composition must therefore supply all same-phase predecessors through
`EventQueueSystem<TState>`'s `runsAfter` list.

## Operational versus compatibility ownership

The audit script contains the explicit scheduler allowlist used by this pre-audit. Every other
completed-domain system ledger identity must remain one of:

- an unscheduled typed event consumer or deterministic API owner;
- a legacy name consolidated into an operational owner named by its mapping document;
- a presentation/external adapter responsibility whose mutating extraction owner is scheduled and
  whose draw/audio/device consumer remains outside the scheduler.

Compatibility identity arrays are audit surfaces only. They must never be reflected over or
registered wholesale. In particular, ECS-043's four effects runtime owners are routed consumers,
not no-op scheduled systems.

## Current pre-audit result

The post-cutover system-domain gate covers 1,159 rows: all 226 system rows, all 499 remaining
legacy subscription rows, and all 30 system-domain object-behavior rows have mapping evidence
alongside their assigned component and
event rows. The latest strict run reports zero blockers. The full legacy static-candidate
counts remain in the generated findings file so ECS-050/ECS-051 can prove that the old runtime has
been removed rather than losing the deletion inventory during conversion.
