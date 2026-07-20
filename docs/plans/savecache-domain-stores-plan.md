# SaveCache → Domain-Sliced Persistence Stores Plan

> Deep-module refactor (Ousterhout: small interface hiding large implementation). Standalone RFC; sibling of the combined `docs/plans/deep-module-refactor-round-2-plan.md` set. Large effort, wide blast radius, behavior-preserving, incremental.

## Document Status

- **Status:** Draft design, ready for implementation review.
- **Repository:** `Crusaders30XX`
- **Runtime:** .NET 8.0, MonoGame DesktopGL, ECS architecture.
- **RFC sequence:** #02 — local-substitutable; effort/risk large / medium.
- **Required final verification after implementation:** `dotnet build` from the repository root.
- **Save compatibility:** Behavior-preserving; incremental migration.

Break the `SaveCache` god-object into domain-sliced stores over an injectable `ISaveStore` backend.

---

## Context

**Why:** `improve-codebase-architecture` deep-module exploration (2026-07-15) flagged `ECS/Data/Save/SaveCache.cs` as the codebase's single largest god-object: **2175 lines**, ~90 `public static` methods, **704 references across 84 files** (verified grep). It is the sole persistence surface for every durable concern in the game, and it has quietly grown from a field-accessor cache into a hidden transactional business-rules engine sitting on top of raw disk I/O.

**What prompted it:** unlike the battle-layer RFCs (#1–#5 in `docs/plans/deep-module-refactor-round-2-plan.md`), the save layer's problem is not emergent control flow — it is one flat static class that (a) has no domain cohesion, (b) forces every caller to re-implement read-modify-write invariants, (c) mixes shallow accessors with a ~200-line transactional rules engine, and (d) hardwires real disk with no seam, so all 26 save-touching test files share one process-global `_save` on real disk and must run serially.

**Intended outcome:** slice the god-object into small typed domain stores (`ICollectionStore`, `IRunStore`, `IWayStationStore`, `IClimbStore`, `ISettingsStore`) over a shared, injectable `ISaveStore` / `ISaveBackend` seam (JSON-file backend for prod, in-memory backend for tests). Move invariant enforcement *into* the stores. Relocate the hidden climb rules engine to a domain service on top of the store — coordinating with, not duplicating, the in-flight climb-events plan. Migrate the 704 references incrementally (stores delegate to `SaveCache` first, cut over per-domain, retire the static last).

## Problem

### 1. God-object with no domain cohesion

`SaveCache` (`ECS/Data/Save/SaveCache.cs`) is `public static class` with ~90 methods spanning every durable concern, discoverable only by scrolling 2175 lines:
- **Collection / unlocks / reward progression:** `GetCollection` (:34), `SaveCollection` (:43), `UnlockAllCollectionItems` (:55), `IsCollectionItemUnlocked` (:99).
- **Settings:** `GetMusicVolumeLevel` (:112), `GetSfxVolumeLevel` (:121), `GetRumbleEnabled` (:130), `SetRumbleEnabled` (:139), `SetMusicVolumeLevel`/`SetSfxVolumeLevel` (:157/:162).
- **Loadouts:** `GetLoadout` (:196), `GetAllLoadouts` (:206), `SaveLoadout` (:216).
- **Run setup / lifecycle:** `ConfigurePrimaryRunSetup` (:235), `UnlockAllRunSetupOptions` (:78), `StartNewRun` (:401), `StartWayStationClimbAttempt` (:413), `IsRunActive` (:587), `MarkRunInactive` (:593).
- **Gold:** `GetGold` (:323), `AddGold` (:329), `TrySpendGoldAndAddToCollection ` (:1395).
- **WayStation meta / visit / dialogue:** `GetWayStationMeta` (:430), `GetWayStationVisit` (:440), `SaveWayStationVisit` (:474), `ClearWayStationVisit` (:485), `RecordWayStationClimbReturn` (:496), `MarkWayStationMedalPurchased` (:460), `HasSeenWayStationDialogueSegment` (:553), `MarkWayStationDialogueSegmentSeen` (:566), `MarkWayStationNpcDialogueOfferQueued` (:536).
- **Climb state + events:** `GetClimbState` (:757), `SaveClimbState` (:767), `EnsureClimbState` (:1072), `TryUpdateClimbEventLifecycle` (:778), `TryBeginClimbEvent` (:798), `TrySetClimbCharacterSummaryPhase` (:835), `TryResolveClimbHazard` (:866), `TryResolveClimbCharacter` (:971).
- **Run-deck entries + restrictions:** `AllocateRunDeckEntryId` (:1162), `GetRunDeckEntry` (:1173), `AddRunDeckEntry` (:1184), `TryRemoveRunDeckEntry` (:1233), `TryReplaceRunDeckEntry` (:1259), `TryUpgradeRunDeckEntry` (:1291), `GetRunDeckEntryRestrictions` (:1587), `AddRunDeckEntryRestriction` (:1599), `RemoveRunDeckEntryRestriction` (:1618), `SetRunDeckEntryRestrictions` (:1634), `SetRunDeckEntryRestrictionState` (:1651).
- **Deck-reward offers:** `GetPendingDeckRewardOffer` (:1319), `SetPendingDeckRewardOffer` (:1348), `ClearPendingDeckRewardOffer` (:1359), `GetAcceptedDeckRewardMutationCount` (:1328), `RecordAcceptedDeckRewardMutation` (:1337).
- **Tutorials / achievements / run-long passives:** `HasSeenTutorial` (:1484), `MarkTutorialSeen` (:1492), `IsGuidedTutorialCompleted` (:1508), `CompleteGuidedTutorial` (:1514), `PersistAchievements` (:1537), `GetRunLongPassivesSnapshot` (:1563), `SetRunLongPassive` (:1573), `ClearRunScopedState` (:1545). Everything shares one static `_save` field (:24), one `_filePath` (:25), one `_lock` (:26). Consuming a card-collection method drags the entire climb/waystation/run state into scope. `SaveFile.cs` (320 lines) is the thin data model; `SaveRepository.cs` (58 lines) is the thin JSON reader/writer. The behavior all piled onto the static in between.

### 2. Per-call-site invariant duplication (read-modify-write-persist)

Because the store exposes only shallow `Get*`/`Save*` accessors, every caller re-implements the same read-modify-write-persist sequence, so the invariant lives at N call sites instead of one:
- `CollectionProgressionSystem.cs` does `GetCollection() -> mutate -> ReconcileEarnedPacks -> SaveCollection()` **five times**: `:36-39`, `:45-51`, `:62-71`, `:77-80`, `:86-89`. The pack-reconcile invariant (`CollectionProgressionRules.ReconcileEarnedPacks`) is re-invoked by hand at each of the three point-adding sites.
- `ClimbShopService.cs` does `GetClimbState() -> mutate -> SaveClimbState()` at `:22-66`, `:77-88`, `:94-97`, `:107-142` — each re-deriving spend/time-advance ordering.
- `WayStationDialogueSystem.cs` does `GetWayStationVisit() -> mutate -> SaveWayStationVisit()` at `:156/:185`, `:220/:224/:229`, `:291`.
- `ClimbEncounterService.cs`: `GetClimbState()/SaveClimbState() ` at `:27/:32`, `:82/:126`, `:186/:191`.
- `StHomobonus.cs` (a **content object**, not a system): `GetClimbState()` (:41) -> mutate - > `SaveClimbState()` (:53). Nothing structurally prevents a caller from mutating a returned snapshot and forgetting to persist, or persisting without applying the domain invariant. (Mitigated only by `Get*` returning deep clones — e.g. `CloneCollection` :1953, `CloneClimbState` :1677 — which is itself defensive-copy overhead at every read.)

### 3. Shallow accessors vs a hidden transactional rules engine

Most methods are one-line field reads. But buried among them are `TryResolveClimbHazard` (:866-969) and `TryResolveClimbCharacter` (:971-1070) — ~200 lines of **business rules inside the persistence layer**:
- Resource crediting (`ClimbRuleService.AddResources`, :897), deterministic deck-entry restriction selection + application (:905-922), next-battle penalty accumulation (Burn/Fear, :923-932), run-long passive stacking (Shackled/Scar via `AddRunLongPassiveStacksLocked ` :1836, called :937/:944), card-upgrade resolution (`RunDeckService.BuildUpgradedCardKey`, :1027), climb-time advance + shop refresh + event lifecycle + encounter replenish/reroll (:1039-1052).
- A hand-rolled transaction: `CaptureClimbEventTransactionBackup` (:1804) / `RestoreClimbEventTransactionBackup` (:1817) snapshot `climb` + primary loadout + run-long passives, and every resolver rolls back on `Persist()` failure (:950-954, :1054-1058). The `ClimbEventTransactionBackup` nested class (:1797) exists solely to support this.
- Returns a rich `ClimbEventMutationResult` (`ECS/Data/Climb/ClimbEventMutationResult.cs`) consumed by `ClimbEventSystem.OnNarrativeModalChoiceRequested` (`ECS/Systems/ClimbEventSystem.cs:101-116`) to drive ECS hydration + animations. This is a domain service masquerading as a persistence accessor. The *pure* decision logic already lives correctly in `ClimbRuleService` (verified: `ApplyTime` :74, `AddResources` :178, `RefreshShopSlots` :203, `UpdateEventLifecycle` :123, `ReplenishEncounterSlots` :229, `RerollEncounterMutationTargets`:534 — all operate on `ClimbSaveState`, no disk). What's misplaced is the *orchestration + atomic apply*, which sits inside the disk-coupled static rather than in a service over a store. Note the same smell, smaller, in settings: `SetRumbleEnabled` publishes `RumbleSettingsChangedEvent` (:153) and `SetAudioVolumeLevels` publishes `AudioSettingsChangedEvent` (:188) — the persistence layer reaching into the static event bus, which `docs/coding-standards.md` forbids for services.

### 4. Disk-coupled, serial-test tax

`EnsureLoaded` (:366) and `Persist` (:352) do real disk I/O through `SaveRepository.Load`/`Save` against `%LocalApplicationData%/Crusaders30XX/save_file.json` (`ResolvePrimarySaveFilePath`:1111). Static, no injection, no seam.
- There is **no in-memory fake**. Every save-touching test drives real files. `DeleteSaveFilesIfPresent` (:607) is called **88 times across 26 test files** to scrub global state between tests. Canonical shape: `RunLifecycleTests.cs:12-13` (`DeleteSaveFilesIfPresent()` then `StartNewRun()`); the `PrepareRun` helper in `ClimbEventSystemTests.cs:397-424` does `DeleteSaveFilesIfPresent -> StartNewRun -> SaveLoadout -> SaveClimbState` just to stand up a fixture.
- Because state is a process-global on disk, tests are order-dependent and non-parallelizable. `TestAssembly.cs:3` pins `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. (The static event bus — candidate #6 in the combined RFC doc — is the *other* reason; this RFC removes one of the two blockers.) Dependency category: **local-substitutable** (a JSON file on local disk) — but implemented as a hardwired static with real disk and no seam, so it tests like a non-substitutable global.

## Proposed Interface

Two layers. A shared **seam** (`ISaveBackend` = disk; `ISaveStore` = single-cached-`SaveFile` + lock + transactional mutate over the backend), then small **domain stores** on top. No caller sees more than its own domain.

```csharp
namespace Crusaders30XX.ECS.Data.Save {
//---Seam---------------------------------------------------------------///Rawsubstitutablepersistence.Prod =
JSONfile; tests = in-memory.public interface ISaveBackend { SaveFile Load(); //fullfile, ora default SaveFile if
absent bool Save(SaveFilesave); //returns false on I/Ofailure(drivesrollback) } /// Shared root: ownsthe one cached
SaveFile + lock + load/persist+///atomicmutate-or-rollback.Generalizesthe climb-only
backup/restore///(SaveCache.cs:1804/:1817) intoa reusable transaction for every domain.public interface ISaveStore { T
Read(Func project); //EnsureLoaded, project a clone underlock bool Mutate(Func apply); //EnsureLoaded; if
apply()==true persist; //onSave() failure restore prior snapshot, return false void Reload();
//wasSaveCache.Reload(:342) void ResetForTests(); //wasDeleteSaveFilesIfPresent(:607) — clears cache;
nodiskonin-memory backend } //---Domainstores(small typed surfaces) ------------------------------- public interface
ISettingsStore { int GetMusicVolumeLevel(); int GetSfxVolumeLevel(); bool GetRumbleEnabled(); AudioSettings
SetAudioVolumeLevels(int? music, int? sfx); //returnsappliedlevels+ Changed flag bool SetRumbleEnabled(bool enabled);
//returnsChanged; caller publishes the event } public interface ICollectionStore {PlayerCollectionSave Get(); void
AddPoints(int points); //ownsReconcileEarnedPacksinvariant(killsthe 5xdup) void AwardClimbSegment(intnewTotalPoints);
void RecordClimbEnded(inttimeReached, bool abandoned, bool completedFinalBoss, int points); void
DismissClimbPointAward(); BoosterPackSave DequeuePendingPack(); //atomicpeek+remove bool IsUnlocked(stringitemId,
ForSaleItemTypetype); void UnlockAll(); } public interface IWayStationStore {WayStationMetaSave GetMeta();
WayStationVisitSave GetVisit(); void SaveVisit(WayStationVisitSave visit); void ClearVisit(); void
RecordClimbReturn(WayStationArrivalKind arrivalKind); //ownsthedialogue-thresholdbookkeeping (:496) void
MarkNpcDialogueOfferQueued(); void MarkMedalPurchased(string medalId); bool HasSeenDialogueSegment(string characterId,
string segmentId); void MarkDialogueSegmentSeen(string characterId, string segmentId); } public interface IRunStore {
bool IsRunActive(); void StartNewRun(); void StartWayStationClimbAttempt(); void MarkRunInactive(); void
ConfigurePrimaryRunSetup(string weaponId, string temperanceId, RunDifficulty difficulty); int GetGold(); void
AddGold(int amount); bool TrySpendGoldAndAddToCollection(stringitemId, int price, ForSaleItemTypetype, out int
newGold); LoadoutDefinition GetLoadout(stringid); IReadOnlyList GetAllLoadouts(); void
SaveLoadout(LoadoutDefinitiondef); //Run-deckentries+restrictions (the climbplan's Agent-A handoff API, relocated
behindthe seam): string AllocateRunDeckEntryId(); LoadoutCardEntry GetRunDeckEntry(string loadoutId, string entryId);
LoadoutCardEntry AddRunDeckEntry(string loadoutId, string cardKey, bool isStarter = false, bool countsAsTraded =
false, bool publishChange = true); bool TryRemoveRunDeckEntry(string loadoutId, string entryId, out LoadoutCardEntry
removed, bool publishChange = true); bool TryReplaceRunDeckEntry(string loadoutId, string outgoingEntryId, string
incomingCardKey, out LoadoutCardEntry replacement, bool countsAsTraded = true, bool publishChange = true); bool
TryUpgradeRunDeckEntry(string loadoutId, string entryId, string upgradedCardKey, out LoadoutCardEntry upgraded); List
GetRunDeckEntryRestrictions(string loadoutId, string entryId); bool SetRunDeckEntryRestrictionState(string loadoutId,
string entryId, IReadOnlyCollection names, IReadOnlyDictionary stacks); //Deck-rewardoffers, tutorials, run-long
passives (run-scoped durable): DeckRewardOfferSave GetPendingDeckRewardOffer(); void
SetPendingDeckRewardOffer(DeckRewardOfferSave offer); void ClearPendingDeckRewardOffer(); bool
HasSeenTutorial(stringkey); void MarkTutorialSeen(stringkey); IReadOnlyDictionary GetRunLongPassivesSnapshot(); void
SetRunLongPassive(string passiveTypeName, int stacks); void ClearRunScopedState(); } public interface IClimbStore
{ClimbSaveState Get(); void Save(ClimbSaveState state); void EnsureInitialized(); bool TryUpdateEventLifecycle(out
ClimbSaveState updated); bool TryBeginEvent(stringslotId, ClimbEventFlowPhase phase, string dialogueRequestId, out
ClimbEventSlotSave pendingSlot); bool TrySetCharacterSummaryPhase(stringslotId, string dialogueRequestId); bool
TryResolveHazard(stringslotId, out ClimbEventMutationResult result); bool TryResolveCharacter(stringslotId, out
ClimbEventMutationResult result); }}
```

### Usage example — before / after (`CollectionProgressionSystem`)

```csharp
//BEFORE(ECS/Systems/CollectionProgressionSystem.cs:36-39, repeated 3x for point-adding) var collection =
SaveCache.GetCollection(); collection.totalPoints += evt.Points;
CollectionProgressionRules.ReconcileEarnedPacks(collection); //invariantre-invokedbyhandateachsite
SaveCache.SaveCollection(collection); //AFTER—invariantlivesonce, insidethestore_collection.AddPoints(evt.Points);
//ICollectionStore.AddPointsownsreconcile+ persist atomically
```

`ICollectionStore.AddPoints` implementation is one `_store.Mutate(save=>{save.collection.totalPoints+=points; CollectionProgressionRules.ReconcileEarnedPacks(save.collection); return true; })`. The five duplicated read-modify-write blocks (`:36-39`, `:45-51`, `:62-71`, `:77-80`, `:86-89`) collapse to store calls; the reconcile invariant can no longer be forgotten or double-applied.

### What it hides

- **Per store:** the read-modify-write-persist dance, the defensive deep-clone-on-read, and each domain's invariant (collection pack-reconcile; waystation NPC-dialogue threshold `RecordWayStationClimbReturn`:496-534; climb event transaction).
- **Seam:** the single cached `SaveFile`, the lock, `EnsureLoaded`/`Persist`, version policy (`ApplyVersionPolicy` :631), file-path resolution + legacy migration (:1104-1160), and the atomic mutate-or-rollback that today only the climb resolvers get.

### Rejected alternatives

1. **Single `ISaveStore` facade (one interface, ~90 methods).** Reproduces the god-object as an interface: every caller still sees the whole surface, there's no domain cohesion, and a collection test still can't avoid dragging climb/waystation state into scope. Rejected — moves the smell behind `I`.
2. **Keep `SaveCache` static, inject only `ISaveBackend` behind it.** Buys the disk seam (in-memory backend for tests) cheaply, but leaves the static god-object and its process-global `_save` intact — tests still share global state and can't parallelize, invariants still duplicate at call sites. A useful *phase 0* (see migration) but not the destination.
3. **Repository-per-entity / ORM.** Over-engineering for one small JSON file with no query needs. Rejected.

## Dependency Strategy

Local-substitutable, in-process. The variability being isolated is "where the bytes live":
- **`JsonFileSaveBackend`** (prod): wraps the existing `SaveRepository.Load`/`Save` (:10/:33) + `ResolvePrimarySaveFilePath` path logic. Behavior byte-for-byte as today.
- **`InMemorySaveBackend`** (tests): holds one `SaveFile` in a field; `Save` copies it, `Load` returns it. Optional `FailNextSave` toggle to exercise the rollback path without a disk fault. Injection follows the **established repo pattern** for substitutable dependencies:
- **`IPlayerInputSource`** (`ECS/Input/IPlayerInputSource.cs`) + `MonoGamePlayerInputAdapter` (`MonoGamePlayerInputAdapter.cs: 7`) prod adapter + `MixedRumbleFakeInputSource` test fake, constructed once in `Game1.cs:239` and passed into `PlayerInputSystem` (:240), `ControllerRumbleSystem` (:243), `BoosterPackOpeningDisplaySystem` (:335).
- **`IAttackPresentationGate`** (`GraphicsAttackPresentationGate.cs:18` prod / `ImmediateAttackPresentationGate ` test double) injected into `EnemyAttackResolver`, constructed in `BattleSceneSystem.cs:799`. Same shape here: the composition root (`Game1`) builds `JsonFileSaveBackend -> shared ISaveStore -> thefive domain stores`, and passes the stores each system needs into its constructor. Stores are scene resources, not systems, so this respects the `docs/coding-standards.md` new-system checklist (#5: ctor may take resources, never other systems). Tests construct `InMemorySaveBackend -> ISaveStore -> stores` directly per test. **Migration wrinkle (honest):** not all 84 callers are systems. ~20 are static services (`ClimbShopService`, `ClimbEncounterService`, `ClimbRuleService`, `QuestCardRewardService`, `RunDeckService`, `PurchaseItemService`, `RunLifecycleService`, …) and a few content objects (`StHomobonus` medal, `EntityFactory`). Those can't take constructor injection cleanly. During migration they keep calling the `SaveCache` static shell, which now delegates to a process-default `ISaveStore` over `JsonFileSaveBackend`. Systems + tests adopt the injectable stores first; the static services are reclassified (many currently *write* save state, violating the "services are read-only" rule — the store surface makes that write boundary explicit) and retired last. This also nudges toward candidate #6: relocating the settings-store event publishing (`SetRumbleEnabled`:153 / audio :188) out of persistence and into the owning system.

## Testing Strategy

- **In-memory backend = full per-test isolation, no disk, parallelizable per domain.** Each test does `var store = new SaveStore(new InMemorySaveBackend()); var collection = new CollectionStore(store);` and exercises one domain with zero global state.
- **Delete boilerplate:** the **88 `DeleteSaveFilesIfPresent()` calls across 26 files** and the `StartNewRun`-then-mutate fixture ceremony (`ClimbEventSystemTests.cs:397-424`, `RunLifecycleTests.cs:12-13`) become `new InMemorySaveBackend()` per test. `EventManager.Clear()` stays — that's the static event bus (candidate #6), a separate blocker.
- **`DisableTestParallelization` (`TestAssembly.cs:3`) cannot flip yet** — it needs *both* the static save *and* the static event bus gone. This RFC removes one blocker. Save-only store tests that never touch `EventManager` can move to a parallel-enabled collection immediately; event-driven system tests stay serial until #6.
- **New boundary tests (invariants enforced in-store):**
- `CollectionStore.AddPoints` reconciles booster packs (locks the logic duplicated 5x in `CollectionProgressionSystem`); `DequeuePendingPack` is atomic.
- `WayStationStore.RecordClimbReturn` drives the NPC-dialogue threshold state machine (:496-534) directly, no scene.
- `SaveStore.Mutate` rolls back on backend `Save` failure (`InMemorySaveBackend.FailNextSave`), reproducing `RestoreClimbEventTransactionBackup` (:1817) without a real disk fault.
- **Coordinate the climb slice with the existing plan.** `docs/plans/climb-hazard-character-events-refactor-plan.md` already owns the hazard/character transaction (Agent B, §13) and asserts its outcomes through `SaveCache` (`ClimbEventSystemTests` — 44 `SaveCache` refs). Those assertions get repointed to `IClimbStore` over `InMemorySaveBackend` **after** that plan lands — do not rewrite the mechanics here.

## Implementation Recommendations

### Owns / Hides / Exposes per store

| Store | Owns (invariant) | Hides | Exposes |
| --- | --- | --- | --- |
| `ISettingsStore` | audio clamp (`ClampAudioVolumeLevel` :687), change detection | cache + persist | get/set volumes, rumble; returns `Changed` so the owning system (not the store) publishes settings events |
| `ICollectionStore` | `ReconcileEarnedPacks`, unlock-list dedupe (`AddMissingCollectionItems`:1909) | clone-on-read, pack queue | points/awards, pack dequeue, unlock queries, `UnlockAll` |
| `IWayStationStore` | NPC-dialogue threshold bookkeeping (:496-534), medal-purchase dedupe | meta vs per-visit split | meta/visit get+save, climb-return record, dialogue-seen tracking |
| `IRunStore` | run lifecycle meta-preservation (`CreateFreshRunPreservingMeta`:642), gold clamp, entry-id allocation, deck-entry provenance | loadout list, entry mutation, restriction persistence | lifecycle, gold, loadouts, run-deck entries+restrictions, offers, tutorials, run-long passives |
| `IClimbStore` | atomic event transaction + pending-slot/phase validation + rollback | backup/restore, single-write-per-resolution | state get/save, lifecycle tick, begin/summary/resolve event transitions |

**The hidden climb rules engine goes to a domain service ON TOP of the store, not inside persistence.** Split `TryResolveClimbHazard`/`TryResolveClimbCharacter` (:866/:971) into: (a) pure decisions — already in `ClimbRuleService` (§10.2 of the climb plan), untouched; (b) `IClimbStore.TryResolveHazard/TryResolveCharacter` — atomic validate+apply+persist via the shared `ISaveStore.Mutate` (rollback for free, deleting the bespoke `ClimbEventTransactionBackup` at:1797-1834); (c) orchestration — already `ClimbEventSystem.OnNarrativeModalChoiceRequested` (:101-116). This matches the climb plan §10.6 ("save repository performs locked atomic mutations, one persistence write per resolution, returns results for ECS sync, never draws/controls systems"). **The climb slice is a relocation, not a redesign — sequence it AFTER the climb plan lands; do not touch those mechanics concurrently.**

### Incremental migration (704 refs / 84 files — delegate-first, per-domain, retire static last)

- **Phase 0 — seam under the static.** Extract `_save`/`_lock`/`EnsureLoaded`/` Persist`/version-policy into `SaveStore : ISaveStore` over `ISaveBackend`; `JsonFileSaveBackend` wraps `SaveRepository` + path logic. `SaveCache` statics delegate to a process-default `SaveStore`. Add `InMemorySaveBackend`. Zero call-site changes; behavior-preserving.
- **Phase 1 — introduce stores as thin pass-throughs.** Add the five interfaces + impls delegating to `SaveCache`. No call-site changes yet. Wire construction in `Game1`.
- **Phase 2 — cut over per domain, smallest first.** `ICollectionStore` first (few callers: `CollectionProgressionSystem`, `AchievementMeterDisplaySystem`, `ClimbRuleService` unlock reads): inject the store, move `ReconcileEarnedPacks` into `AddPoints`, delete the 5 duplicated blocks. Then `ISettingsStore`, `IWayStationStore`, `IRunStore`. `IClimbStore` **last**, coordinated with the climb plan.
- **Phase 3 — pull invariants into stores.** Fold each domain's read-modify-write into store methods; delete call-site duplication (climb shop, waystation dialogue, encounter service).
- **Phase 4 — retire the static.** Once all 84 files (systems + tests) use stores, reduce `SaveCache` to a deprecated adapter for the remaining static services, then delete when those are injected/reclassified.

**Effort:** large (widest reference count of any deep-module candidate).

**Risk:** medium — each phase is behavior-preserving delegation, but blast radius is 84 files, so land per-domain behind green tests. **Relation to deferred work:** removes one of the two `DisableTestParallelization` blockers (the other is the static event bus, candidate #6); moving settings-event publishing out of persistence is a down payment on #6. The run-deck-entry sub-surface of `IRunStore` is the exact API the climb plan's Agent A publishes (§12 handoff) — align names to avoid a parallel mutation API.

## Critical files

- `ECS/Data/Save/SaveCache.cs` (2175 lines — the god-object; transactional engine :866-1070, backup/restore :1797-1834, disk seam :352/:366).
- `ECS/Data/Save/SaveFile.cs` (data model, `CURRENT_VERSION=25`:12), `ECS/Data/Save/SaveRepository.cs` (JSON disk I/O, wrap into `JsonFileSaveBackend`).
- `ECS/Data/Climb/ClimbEventMutationResult.cs` (resolver return type), `ECS/Services/ClimbRuleService.cs ` (pure rules the climb slice keeps).
- `ECS/Systems/CollectionProgressionSystem.cs` (5x read-modify-write to delete), `ECS/Services/ClimbShopService.cs`, `ECS/Scenes/WayStationScene/WayStationDialogueSystem.cs`, `ECS/Systems/ClimbEventSystem.cs` (transaction consumer).
- Precedent to mirror: `ECS/Input/IPlayerInputSource.cs` + `ECS/Input/MonoGamePlayerInputAdapter.cs` + `Game1.cs:239-245`; `ECS/Scenes/BattleScene/GraphicsAttackPresentationGate.cs` + `BattleSceneSystem.cs:799`.
- Tests: `tests/Crusaders30XX.Tests/TestAssembly.cs:3` (parallelization pin), `RunLifecycleTests.cs`, `ClimbEventSystemTests.cs` (`PrepareRun` :397), + the 26 files with 88 `DeleteSaveFilesIfPresent` calls.
- Coordinate: `docs/plans/climb-hazard-character-events-refactor-plan.md` (climb transaction owner — §10.2, §10.6, §12–13).

## Verification

- `dotnet build` from repo root — clean (per `AGENTS.md`).
- `dotnet test tests/Crusaders30XX.Tests` — full suite green (still serial until candidate #6).
- New per-domain store tests pass against `InMemorySaveBackend` with no `DeleteSaveFilesIfPresent`/disk: collection point/pack invariant, waystation dialogue-threshold state machine, `SaveStore.Mutate` rollback on `FailNextSave`.
- In-app (`dotnet run -- new`): start a new run, unlock a collection item, complete a WayStation visit, resolve a climb hazard/character event — confirm each persists correctly across a save/reload cycle (`SaveCache.Reload` / `ISaveStore.Reload`), matching pre-refactor behavior byte-for-byte in the JSON file.

---

