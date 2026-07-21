using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Enemies;

namespace ChurchSuffering.ECS.Services
{
	public static class ClimbEncounterService
	{
		public struct CompletionResult
		{
			public bool Completed;
			public string EncounterSlotId;
			public ClimbResourceSave Resources;
			public DeckRewardOfferSave DeckRewardOffer;
			public bool PendingFinalEncounter;
		}

		public static bool TryQueueEncounter(EntityManager entityManager, string encounterSlotId)
		{
			if (entityManager == null || string.IsNullOrWhiteSpace(encounterSlotId)) return false;
			var climb = SaveCache.GetClimbState();
			int seed = SaveCache.GetAll()?.runMapSeed ?? 0;
			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			if (ClimbRuleService.EnsureEncounterMutationTargets(climb, seed, loadout))
			{
				SaveCache.SaveClimbState(climb);
			}
			var slot = climb?.encounterSlots?.FirstOrDefault(e =>
				e != null
				&& !e.isCompleted
				&& string.Equals(e.id, encounterSlotId, StringComparison.OrdinalIgnoreCase));
			if (slot == null || string.IsNullOrWhiteSpace(slot.enemyId)) return false;

			var queuedEntity = entityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
			if (queuedEntity == null)
			{
				queuedEntity = entityManager.CreateEntity("QueuedEvents");
				entityManager.AddComponent(queuedEntity, new QueuedEvents());
				entityManager.AddComponent(queuedEntity, new DontDestroyOnLoad());
			}

			var queued = queuedEntity.GetComponent<QueuedEvents>();
			queued.CurrentIndex = -1;
			queued.Events.Clear();
			queued.Events.Add(new QueuedEvent
			{
				EventId = slot.enemyId,
				EventType = QueuedEventType.Enemy,
			});
			queued.IsClimbEncounter = true;
			queued.ClimbEncounterSlotId = slot.id;
			queued.BattleLocation = slot.battleLocation;
			queued.LocationId = "climb";
			queued.QuestIndex = 0;

			if (TryPublishMutationAnimation(slot, loadout))
			{
				return true;
			}

			EventManager.Publish(new ShowTransition { Scene = SceneId.Battle });
			return true;
		}

		public static CompletionResult CompleteQueuedEncounter(EntityManager entityManager)
		{
			var result = new CompletionResult
			{
				Resources = new ClimbResourceSave { red = 0, white = 0, black = 0 },
			};
			var queued = entityManager?.GetEntitiesWithComponent<QueuedEvents>()
				.FirstOrDefault()
				?.GetComponent<QueuedEvents>();
			if (queued?.IsClimbEncounter != true || string.IsNullOrWhiteSpace(queued.ClimbEncounterSlotId)) return result;

			var climb = SaveCache.GetClimbState();
			var slot = climb?.encounterSlots?.FirstOrDefault(e =>
				e != null
				&& string.Equals(e.id, queued.ClimbEncounterSlotId, StringComparison.OrdinalIgnoreCase));
			if (slot == null || slot.isCompleted) return result;

			int previousTime = climb.time;
			int seed = SaveCache.GetAll()?.runMapSeed ?? 0;
			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			int appliedTime = ClimbRuleService.ApplyTime(climb, slot.timeCost);
			if (ClimbRuleService.ShouldRefreshShopAtTime(climb, previousTime, climb.time))
			{
				ClimbRuleService.RefreshShopSlots(climb, seed, loadout);
			}
			ClimbRuleService.AddResources(climb.resources, slot.rewardResources);
			ClimbRuleService.UpdateEventLifecycle(climb);

			slot.isCompleted = true;
			ClimbRuleService.ReplenishEncounterSlots(climb, seed, loadout);
			if (appliedTime > 0)
			{
				ClimbRuleService.RerollEncounterMutationTargets(climb, seed, loadout);
			}
			result.Completed = true;
			result.EncounterSlotId = slot.id;
			result.Resources = CloneResources(slot.rewardResources);
			result.PendingFinalEncounter = ClimbRuleService.HasPendingFinalEncounter(climb);

			if (slot.hasDeckReward)
			{
				result.DeckRewardOffer = QuestCardRewardService.GenerateAndPersistPendingOffer(0, restrictToCollection: true);
			}
			else
			{
				QuestCardRewardService.SkipPendingOffer();
			}

			climb.pendingEncounterReward = new ClimbEncounterRewardSave
			{
				encounterSlotId = slot.id,
				resources = CloneResources(result.Resources),
				deckRewardOffer = CloneDeckRewardOffer(result.DeckRewardOffer),
				pendingFinalEncounter = result.PendingFinalEncounter,
			};
			SaveCache.SaveClimbState(climb);
			return result;
		}

		public static bool TryQueuePendingFinalEncounter(EntityManager entityManager)
		{
			if (entityManager == null) return false;
			var climb = SaveCache.GetClimbState();
			if (!ClimbRuleService.HasPendingFinalEncounter(climb)) return false;

			var queuedEntity = entityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
			if (queuedEntity == null)
			{
				queuedEntity = entityManager.CreateEntity("QueuedEvents");
				entityManager.AddComponent(queuedEntity, new QueuedEvents());
				entityManager.AddComponent(queuedEntity, new DontDestroyOnLoad());
			}

			var queued = queuedEntity.GetComponent<QueuedEvents>();
			queued.CurrentIndex = -1;
			queued.Events.Clear();
			queued.Events.Add(new QueuedEvent
			{
				EventId = "fallen_shepherd",
				EventType = QueuedEventType.Enemy,
			});
			queued.IsClimbEncounter = true;
			queued.ClimbEncounterSlotId = "final";
			queued.BattleLocation = BattleLocationAssetService.FinalEncounterLocation;
			queued.LocationId = "climb";
			queued.QuestIndex = 0;
			EnsureFallenShepherdIntroDialog(queuedEntity);
			EventManager.Publish(new ShowTransition { Scene = SceneId.Battle });
			return true;
		}

		private static void EnsureFallenShepherdIntroDialog(Entity queuedEntity)
		{
			if (queuedEntity == null) return;
			var pending = queuedEntity.GetComponent<PendingQuestDialog>();
			if (pending == null)
			{
				queuedEntity.AddComponent(new PendingQuestDialog
				{
					DialogId = "fallen_shepherd",
					SegmentId = "intro",
					RequestId = Guid.NewGuid(),
					WillShowDialog = true,
				});
				return;
			}

			pending.DialogId = "fallen_shepherd";
			pending.SegmentId = "intro";
			pending.RequestId = Guid.NewGuid();
			pending.WillShowDialog = true;
		}

		public static bool ResolvePendingEncounterReward(EntityManager entityManager)
		{
			var climb = SaveCache.GetClimbState();
			if (climb?.pendingEncounterReward == null) return false;

			bool shouldQueueFinalEncounter = ClimbRuleService.HasPendingFinalEncounter(climb);
			climb.pendingEncounterReward = null;
			SaveCache.SaveClimbState(climb);

			if (shouldQueueFinalEncounter)
			{
				TryQueuePendingFinalEncounter(entityManager);
			}

			return true;
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

		private static bool TryPublishMutationAnimation(ClimbEncounterSlotSave slot, LoadoutDefinition loadout)
		{
			if (slot == null
				|| string.IsNullOrWhiteSpace(slot.cardMutationDeckEntryId)
				|| string.IsNullOrWhiteSpace(slot.cardMutationCardKey)
				|| string.IsNullOrWhiteSpace(slot.cardMutationRestrictionName))
			{
				return false;
			}

			var entry = (loadout?.cards ?? new List<LoadoutCardEntry>())
				.FirstOrDefault(card => string.Equals(card?.entryId, slot.cardMutationDeckEntryId, StringComparison.Ordinal));
			if (entry == null) return false;
			if ((entry.restrictions ?? new List<string>()).Contains(slot.cardMutationRestrictionName, StringComparer.OrdinalIgnoreCase)) return false;

			EventManager.Publish(new ClimbCardMutationAnimationRequested
			{
				DeckEntryId = entry.entryId,
				CardKey = entry.cardKey,
				RestrictionName = slot.cardMutationRestrictionName,
				CurrentRestrictionNames = (entry.restrictions ?? new List<string>())
					.Where(name => !string.IsNullOrWhiteSpace(name))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList(),
				TransitionToBattleOnComplete = true,
			});
			return true;
		}

		private static DeckRewardOfferSave CloneDeckRewardOffer(DeckRewardOfferSave offer)
		{
			if (offer == null) return null;
			return new DeckRewardOfferSave
			{
				rewardGold = offer.rewardGold,
				options = offer.options == null
					? new List<DeckRewardOfferOptionSave>()
					: offer.options.Select(o => new DeckRewardOfferOptionSave
					{
						kind = o.kind ?? string.Empty,
						loadoutIndex = o.loadoutIndex,
						outgoingEntryId = o.outgoingEntryId ?? string.Empty,
						outgoingCardKey = o.outgoingCardKey ?? string.Empty,
						incomingCardKey = o.incomingCardKey ?? string.Empty,
						upgradedCardKey = o.upgradedCardKey ?? string.Empty,
					}).ToList()
			};
		}
	}
}
