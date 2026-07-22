using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Climb;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class ClimbEventSystem : Core.System
	{
		public ClimbEventSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ClimbEventSlotSelectedEvent>(OnEventSlotSelected);
			EventManager.Subscribe<NarrativeModalChoiceRequested>(OnNarrativeModalChoiceRequested);
			EventManager.Subscribe<DialogueSequenceCompleted>(OnDialogueSequenceCompleted);
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		}

		public override void Update(GameTime gameTime)
		{
			if (!IsClimbScene()) return;
			SaveCache.TryUpdateClimbEventLifecycle(out _);
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt?.Scene != SceneId.Climb) return;
			SaveCache.TryUpdateClimbEventLifecycle(out var climb);
			climb ??= SaveCache.GetClimbState();
			ResumePendingFlow(climb);
			if (climb?.pendingEvent == null
				&& climb?.pendingEncounterReward == null
				&& ClimbRuleService.HasPendingFinalEncounter(climb))
			{
				ClimbEncounterService.TryQueuePendingFinalEncounter(EntityManager);
			}
		}

		private void OnEventSlotSelected(ClimbEventSlotSelectedEvent evt)
		{
			if (evt == null || string.IsNullOrWhiteSpace(evt.SlotId)) return;
			if (!IsClimbScene() || HasBlockingInputContext()) return;

			SaveCache.TryUpdateClimbEventLifecycle(out var climb);
			if (climb?.pendingEvent != null) return;
			var slot = FindSlot(climb, evt.SlotId);
			if (slot?.status != ClimbEventStatus.Active) return;

			if (slot.kind == ClimbEventKind.Hazard)
			{
				if (!SaveCache.TryBeginClimbEvent(
					slot.id,
					ClimbEventFlowPhase.HazardConfirmation,
					string.Empty,
					out var pendingSlot))
				{
					return;
				}
				ShowHazardConfirmation(pendingSlot);
				return;
			}

			Guid requestId = Guid.NewGuid();
			if (!SaveCache.TryBeginClimbEvent(
				slot.id,
				ClimbEventFlowPhase.CharacterDialogue,
				requestId.ToString("D"),
				out var characterSlot))
			{
				return;
			}
			ShowCharacterDialogue(characterSlot, requestId);
		}

		private void OnDialogueSequenceCompleted(DialogueSequenceCompleted evt)
		{
			if (evt == null || evt.RequestId == Guid.Empty) return;
			var climb = SaveCache.GetClimbState();
			var pending = climb?.pendingEvent;
			if (pending?.phase != ClimbEventFlowPhase.CharacterDialogue) return;
			if (!Guid.TryParse(pending.dialogueRequestId, out Guid expectedId) || expectedId != evt.RequestId) return;

			var slot = FindSlot(climb, pending.eventSlotId);
			var definition = ClimbEventCatalog.Get(slot?.definitionId);
			if (slot?.kind != ClimbEventKind.Character || definition == null) return;
			if (!string.Equals(evt.DefinitionId, definition.DefinitionId, StringComparison.OrdinalIgnoreCase)
				|| !string.Equals(evt.SegmentId, definition.DialogueSegmentId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (!SaveCache.TrySetClimbCharacterSummaryPhase(slot.id, pending.dialogueRequestId)) return;
			ShowCharacterSummary(slot);
		}

		private void OnNarrativeModalChoiceRequested(NarrativeModalChoiceRequested evt)
		{
			if (evt == null || evt.ChoiceIndex != 0 || evt.Handled) return;
			if (ClimbEventContextIds.TryParseHazard(evt.ResolutionContextId, out string hazardSlotId))
			{
				if (!SaveCache.TryResolveClimbHazard(hazardSlotId, out var result) || !result.Succeeded) return;
				SynchronizeHazardResult(result);
				evt.Handled = true;
				return;
			}

			if (!ClimbEventContextIds.TryParseCharacter(evt.ResolutionContextId, out string characterSlotId)) return;
			if (!SaveCache.TryResolveClimbCharacter(characterSlotId, out var characterResult) || !characterResult.Succeeded) return;
			SynchronizeCharacterResult(characterResult);
			evt.Handled = true;
		}

		private void ResumePendingFlow(ClimbSaveState climb)
		{
			var pending = climb?.pendingEvent;
			var slot = FindSlot(climb, pending?.eventSlotId);
			if (pending == null || slot?.status != ClimbEventStatus.Pending) return;

			if (pending.phase == ClimbEventFlowPhase.HazardConfirmation)
			{
				ShowHazardConfirmation(slot);
			}
			else if (pending.phase == ClimbEventFlowPhase.CharacterDialogue
				&& Guid.TryParse(pending.dialogueRequestId, out Guid requestId))
			{
				ShowCharacterDialogue(slot, requestId);
			}
			else if (pending.phase == ClimbEventFlowPhase.CharacterSummary)
			{
				ShowCharacterSummary(slot);
			}
		}

		private void ShowHazardConfirmation(ClimbEventSlotSave slot)
		{
			var definition = ClimbEventCatalog.Get(slot?.definitionId);
			if (slot == null || definition?.Kind != ClimbEventKind.Hazard) return;
			string restrictionName = ClimbRuleService.GetRestrictionName(slot.hazardEffect);
			bool hasEligibleTarget = string.IsNullOrWhiteSpace(restrictionName)
				|| ClimbRuleService.GetEligibleRestrictionEntries(
					SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId),
					restrictionName).Count > 0;
			string effectSummary = ClimbRuleService.BuildHazardEffectSummary(
				slot.hazardEffect,
				slot.effectAmount,
				hasEligibleTarget);
			string resourceSummary = ClimbRuleService.BuildResourceSummary(slot.rewardResources);

			EventManager.Publish(new ShowNarrativeEventOverlay
			{
				RunMapEventId = string.Empty,
				EventTypeId = string.Empty,
				ResolutionContextId = ClimbEventContextIds.Hazard(slot.id),
				Content = new NarrativeModalContent
				{
					Title = definition.Title,
					Body = $"{definition.NarrativeBody}\n\nEffect: {effectSummary}\nGain: {resourceSummary}",
					ConfirmLabel = "Confirm",
				},
			});
		}

		private static void ShowCharacterDialogue(ClimbEventSlotSave slot, Guid requestId)
		{
			var definition = ClimbEventCatalog.Get(slot?.definitionId);
			if (slot == null || definition?.Kind != ClimbEventKind.Character || requestId == Guid.Empty) return;
			EventManager.Publish(new DialogueSequenceRequested
			{
				DefinitionId = definition.DefinitionId,
				SegmentId = definition.DialogueSegmentId,
				RequestId = requestId,
				BackgroundOnly = true,
			});
		}

		private void ShowCharacterSummary(ClimbEventSlotSave slot)
		{
			var definition = ClimbEventCatalog.Get(slot?.definitionId);
			if (slot == null || definition?.Kind != ClimbEventKind.Character) return;
			string body = definition.SummaryBody;
			if (slot.characterReward == ClimbCharacterRewardType.RandomCardUpgrade
				&& ClimbRuleService.GetEligibleSmithEntries(SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId)).Count == 0)
			{
				body = definition.NoTargetSummaryBody;
			}

			EventManager.Publish(new ShowNarrativeEventOverlay
			{
				RunMapEventId = string.Empty,
				EventTypeId = string.Empty,
				ResolutionContextId = ClimbEventContextIds.Character(slot.id),
				Content = new NarrativeModalContent
				{
					Title = definition.SummaryTitle,
					Body = body,
					ConfirmLabel = "Proceed",
				},
			});
		}

		private void SynchronizeHazardResult(ClimbEventMutationResult result)
		{
			if (result?.AlreadyResolved != true && HasResources(result?.ResourcesGained))
			{
				EventManager.Publish(new ClimbResourceAcquisitionAnimationRequested
				{
					Resources = CloneResources(result.ResourcesGained),
				});
			}

			RunDeckService.EnsureRunDeck(EntityManager);
			RunScopedStateService.HydrateRunCardRestrictions(EntityManager);
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault(entity => entity.IsActive);
			RunScopedStateService.HydrateRunLongPassivesOntoPlayer(player);
			if (player != null)
			{
				var passives = SaveCache.GetRunLongPassivesSnapshot();
				int scarTotal = passives.FirstOrDefault(pair =>
					string.Equals(pair.Key, AppliedPassiveType.Scar.ToString(), StringComparison.OrdinalIgnoreCase)).Value;
				if (scarTotal > 0)
				{
					EventManager.Publish(new ApplyBattleMaxHpEvent
					{
						Target = player,
						ScarPenalty = scarTotal,
					});
				}
			}
		}

		private static bool HasResources(ClimbResourceSave resources)
		{
			return (resources?.red ?? 0) > 0
				|| (resources?.white ?? 0) > 0
				|| (resources?.black ?? 0) > 0;
		}

		private static ClimbResourceSave CloneResources(ClimbResourceSave resources)
		{
			return new ClimbResourceSave
			{
				red = Math.Max(0, resources?.red ?? 0),
				white = Math.Max(0, resources?.white ?? 0),
				black = Math.Max(0, resources?.black ?? 0),
			};
		}

		private void SynchronizeCharacterResult(ClimbEventMutationResult result)
		{
			RunDeckService.EnsureRunDeck(EntityManager);
			if (!string.IsNullOrWhiteSpace(result.UpgradedEntryId))
			{
				if (RunDeckService.TryParseCardKey(result.UpgradedCardKey, out var cardId, out var color, out _))
				{
					string baseKey = RunDeckService.BuildCardKey(cardId, color, isUpgraded: false);
					var beforeEntry = SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, result.UpgradedEntryId);
					string beforeSecondary = beforeEntry?.secondaryColor ?? string.Empty;
					CardUpgradeService.InvokeUpgradeConfirmed(result.UpgradedCardKey, result.UpgradedEntryId);
					var afterEntry = SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, result.UpgradedEntryId);
					EventManager.Publish(new ClimbCardUpgradeAnimationRequested
					{
						DeckEntryId = result.UpgradedEntryId,
						BaseCardKey = baseKey,
						UpgradedCardKey = result.UpgradedCardKey,
						BeforeSecondaryColor = beforeSecondary,
						AfterSecondaryColor = afterEntry?.secondaryColor ?? beforeSecondary,
						DelayClimbTurnoverUntilComplete = true,
					});
				}
				else
				{
					CardUpgradeService.InvokeUpgradeConfirmed(result.UpgradedCardKey, result.UpgradedEntryId);
				}
			}
			if (result.ReachedFinalTime)
			{
				ClimbEncounterService.TryQueuePendingFinalEncounter(EntityManager);
			}
		}

		private bool HasBlockingInputContext()
		{
			return EntityManager.GetEntitiesWithComponent<InputContext>()
				.Select(entity => entity.GetComponent<InputContext>())
				.Any(context => context?.IsActive == true
					&& !context.IsDiagnostic
					&& !string.Equals(context.Id, InputContextIds.Gameplay, StringComparison.Ordinal));
		}

		private bool IsClimbScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Climb;
		}

		private static ClimbEventSlotSave FindSlot(ClimbSaveState climb, string slotId)
		{
			return climb?.eventSlots?.FirstOrDefault(slot => slot != null
				&& string.Equals(slot.id, slotId, StringComparison.OrdinalIgnoreCase));
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}
	}
}
