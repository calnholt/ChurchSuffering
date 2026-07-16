# Deep-Module Refactor — Round 2 Plan Index

## Document Status

- **Status:** Draft index for Round 2 deep-module refactors.
- **Repository:** `Crusaders30XX`
- **Round 1 status:** RFCs #1–#4 shipped; RFC #5 (`ui-draw-primitives-consolidation-plan.md`) still open.

---

## Context

**Why:** The first deep-module pass shipped its top four refactors — `EnemyAttackResolver` (+`IAttackPresentationGate`), pure `CardPlayResolver`, removal of the `EnemyAttackBase` static `EntityManager`, and the `CardRenderPipeline`. Only Round 1 RFC #5 ([`ui-draw-primitives-consolidation-plan.md`](ui-draw-primitives-consolidation-plan.md)) is still open. This round is a fresh `improve-codebase-architecture` exploration (2026-07-15) for the **next** set of deep-module opportunities — shallow, tightly-coupled clusters that can hide behind a small interface to become testable and AI-navigable.

**What prompted it:** three parallel Explore passes over (a) non-battle scenes / persistence / run progression, (b) battle systems outside the attack + card-play loops, and (c) cross-cutting infra + the god-objects. The friction encountered navigating each cluster is the signal.

**Intended outcome:** eight independently-orchestrable RFCs, each self-contained and ready to become its own GitHub issue / PR. Every doc follows the same template (Context / Problem / Proposed Interface / Dependency Strategy / Testing Strategy / Implementation Recommendations / Critical files / Verification) and grounds its signatures in real `file:line` evidence.

## Cross-cutting principles (all RFCs)

- **Replace, don't layer.** Old shallow-module tests become waste once boundary tests exist — delete them. New tests assert observable outcomes through the public interface.
- Keep low-friction content authoring (per-card / per-medal / per-passive conventions) intact — deep modules must not make adding content harder. Where an RFC touches an authoring path (`create-medal`, passive definitions), it updates the convention.
- **Reuse the shipped seams.** Two ports/adapters already exist as templates: `IPlayerInputSource` (+`MonoGamePlayerInputAdapter`) and `IAttackPresentationGate` (+`ImmediateAttackPresentationGate`). RFCs #03 and #06 copy that shape directly.
- All eight are **in-process** or **local-substitutable** — no new package / assembly / network boundary.

## The eight RFCs

| # | RFC | Hook | Dep. category | Effort / risk |
|---|-----|------|---------------|---------------|
| 01 | [Card-status application resolver](card-status-application-resolver-plan.md) | Pure selection resolver; kills a 3-file type-dispatch + an 8-system divergent-predicate duplication; fixes the `CardApplicationService`→system inversion + a real eligibility-divergence bug | in-process | medium / medium |
| 02 | [SaveCache → domain stores](savecache-domain-stores-plan.md) | Break the 2175-line, 704-ref static persistence god-object into domain-sliced stores over an injectable backend (in-memory for tests); lift the hidden transactional climb-rules engine out of persistence | local-substitutable | large / medium |
| 03 | [Scene navigator](scene-navigator-plan.md) | Pull scene-identity mutation, `OwnedByScene`/`DontDestroyOnLoad` entity teardown, and run-end out of a `Draw()` "Display" system into a synchronous facade; wipe stays behind a gate | in-process | medium / medium |
| 04 | [Declarative medal triggers](declarative-medal-triggers-plan.md) | Replace 28× hand-wired `Subscribe`/`Unsubscribe` with a declarative trigger hook (like `EquipmentBase`); `MedalManagerSystem` owns leak-proof lifecycle | in-process | medium / low |
| 05 | [Passive tick/expiry table](passive-tick-expiry-table-plan.md) | Replace the 587-line phase-keyed switch + 5 hardcoded scope `HashSet`s with a per-passive definition table; add `Passive(type)` / `GetPlayer` accessors to kill 42×/88× copy-paste | in-process | medium-large / medium |
| 06 | [Medal/equipment presentation gate](medal-equipment-presentation-gate-plan.md) | Extend the shipped `IAttackPresentationGate` pattern to medal + equipment activation so gameplay is separable from visual timing and testable synchronously | local-substitutable | small / low |
| 07 | [Scene-module composition + unified render list](scene-module-composition-plan.md) | One `SceneModule` owns compose/activate/deactivate/clean up; a single ordered list declares update-phase + draw position together, killing the split-brain and the 3-edit ritual | in-process | medium-large / medium |
| 08 | [WayStation run-setup as component state](waystation-run-setup-component-plan.md) | Fold the `WayStationRunSetupSingleton` global static (feeding battle balance from a UI modal) onto a component — the cheapest standalone win | in-process | small / low |

## Recommended sequencing (opinionated)

1. **#08 (run-setup component)** — first. ~1 file + 1 helper, deletes a process-global, de-risks parallel tests. Trivial warm-up.
2. **#06 (medal/equipment gate)** — small, has a copy-one-for-one precedent (`IAttackPresentationGate`), best testability-per-line.
3. **#01 (card-status resolver)** — clean pure seam, reconciles the divergent eligibility predicates (fixes the `GetHandOfCards`-vs-`Deck.Hand` correctness gap), collapses heavy pipeline tests.
4. **#03 (scene navigator)** — biggest navigability payoff ("how does a scene change happen?"); establishes the scene-lifecycle seam #07 builds on.
5. **#04 (declarative medals)** — fixes the latent subscription-leak class; content-authoring sensitive, so land it deliberately and update `create-medal`.
6. **#07 (scene-module composition)** — wide but mechanical; overlaps #03 (that owns *transitions*, this owns *per-scene composition + draw order*); snapshot-gated.
7. **#05 (passive tick/expiry table)** — larger blast radius; sequence after any phase-flow work (the Stun branch re-enqueues the phase sequence).
8. **#02 (SaveCache stores)** — largest job; incremental (delegate-first → per-domain cutover → retire static); coordinate the climb slice with the in-flight `climb-hazard-character-events-refactor-plan.md`.

## Relationship to the prior round + deferred work

- **Still open from round 1:** RFC #5 [`ui-draw-primitives-consolidation-plan.md`](ui-draw-primitives-consolidation-plan.md) (documented, not implemented).
- **Deferred (candidate #6 in round 1):** injectable per-battle event bus to relax `[assembly: CollectionBehavior(DisableTestParallelization = true)]` (`TestAssembly.cs:3`). Round-2 RFCs #02 and #08 each remove one shared global, chipping away at the serial-test tax; the full bus refactor remains the foundational, highest-blast-radius prerequisite and is intentionally not one of these eight.

## Verification (per RFC)

The repo builds with `dotnet build` and tests run with `dotnet test tests/Crusaders30XX.Tests` (xUnit, serial). Each RFC's own **Verification** section lists its boundary tests, the shallow tests it deletes, and the in-app scenario to drive end-to-end.

---

