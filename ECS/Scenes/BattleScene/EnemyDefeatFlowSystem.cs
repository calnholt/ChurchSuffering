using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.Diagnostics;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public class EnemyDefeatFlowSystem : Core.System
	{
		private enum PostVictoryKind
		{
			None,
			RestartTestFight,
			StartTutorialDialogue,
			AdvanceTutorial,
			ShowWayStationTransition,
			ShowQuestReward,
			ShowNextBattleTransition,
		}

		private struct PostVictoryAction
		{
			public PostVictoryKind Kind;
			public ShowQuestRewardOverlay QuestReward;
			public ShowTransition Transition;
			public DialogueSequenceRequested TutorialDialogue;
		}

		private readonly ImageAssetService _imageAssets;

		private Guid? _pendingBurstId;
		private Entity _pendingEnemy;
		private bool _pendingIsPreview;
		private Guid _tutorialDialogueRequestId;
		private PostVictoryAction _pendingPostVictory;
		private bool _waitingForVictoryAnimation;

		public EnemyDefeatFlowSystem(EntityManager entityManager, ImageAssetService imageAssets) : base(entityManager)
		{
			_imageAssets = imageAssets;
			EventManager.Subscribe<BeginDefeatPresentationEvent>(OnBeginDefeatPresentation);
			EventManager.Subscribe<PixelBurstAnimationCompleted>(OnPixelBurstCompleted);
			EventManager.Subscribe<VictoryAnimationCompleteEvent>(OnVictoryAnimationComplete);
			EventManager.Subscribe<DeleteCachesEvent>(_ => OnCachesCleared());
			EventManager.Subscribe<StartBattleRequested>(_ => OnCachesCleared());
			EventManager.Subscribe<DialogueSequenceCompleted>(OnTutorialDialogueCompleted);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnBeginDefeatPresentation(BeginDefeatPresentationEvent evt)
		{
			if (evt?.Enemy == null) return;
			if (!evt.IsPreview && evt.Enemy.HasComponent<SuppressPortraitRender>()) return;

			if (!evt.IsPreview)
			{
				EventQueue.Clear();

				if (WillOpenQuestRewardModal(evt.Enemy))
				{
					EventManager.Publish(new ChangeMusicTrack { Track = MusicTrack.QuestComplete });
				}
			}

			if (_pendingBurstId.HasValue) return;

			SetDefeatPresentationActive(true);
			EnsureSuppressPortrait(evt.Enemy);

			if (!PortraitPixelBurstRequestBuilder.TryBuild(EntityManager, _imageAssets, evt.Enemy, evt.IsPreview, out var request))
			{
				FinishWithoutBurst(evt.Enemy, evt.IsPreview);
				return;
			}

			_pendingBurstId = request.BurstId;
			_pendingEnemy = evt.Enemy;
			_pendingIsPreview = evt.IsPreview;
			EventManager.Publish(request);
		}

		private void OnPixelBurstCompleted(PixelBurstAnimationCompleted evt)
		{
			if (evt == null || !_pendingBurstId.HasValue || evt.BurstId != _pendingBurstId.Value) return;

			var enemy = _pendingEnemy;
			var isPreview = _pendingIsPreview;
			ResetPending();

			if (isPreview)
			{
				EndDefeatPresentationAndRestorePortrait(enemy);
				return;
			}

			CompleteRealDefeat(enemy);
		}

		private void CompleteRealDefeat(Entity enemy)
		{
			EndDefeatPresentationInputFreeze();

			LoggingService.Append("EnemyDefeatFlowSystem.CompleteRealDefeat", new System.Text.Json.Nodes.JsonObject
			{
				["enemyId"] = enemy?.Id ?? -1
			});

			EventManager.Publish(new EnemyKilledEvent { Enemy = enemy });

			var action = ResolvePostVictoryAction(enemy);
			StageVictoryAnimation(action);
		}

		private void StageVictoryAnimation(PostVictoryAction action)
		{
			_pendingPostVictory = action;
			_waitingForVictoryAnimation = true;
			EventManager.Publish(new ShowVictoryAnimationEvent());
		}

		private void OnVictoryAnimationComplete(VictoryAnimationCompleteEvent evt)
		{
			if (!_waitingForVictoryAnimation) return;

			_waitingForVictoryAnimation = false;
			var action = _pendingPostVictory;
			_pendingPostVictory = default;
			ExecutePostVictoryAction(action);
		}

		private PostVictoryAction ResolvePostVictoryAction(Entity enemy)
		{
			if (TestFightRuntime.IsActive)
			{
				TestFightRuntime.RecordVictory();
				TestFightSetupService.ResetEncounterQueue(EntityManager);
				return new PostVictoryAction
				{
					Kind = PostVictoryKind.RestartTestFight,
					Transition = new ShowTransition { Scene = SceneId.Battle },
				};
			}

			var enemyId = enemy?.GetComponent<Enemy>()?.EnemyBase?.Id;
			var tutorial = GuidedTutorialService.GetState(EntityManager);
			if (tutorial != null)
			{
				var sectionDef = GuidedTutorialDefinitions.GetSection(tutorial.Section);
				if (!string.IsNullOrEmpty(sectionDef.PendingDialogKey))
				{
					return new PostVictoryAction
					{
						Kind = PostVictoryKind.StartTutorialDialogue,
						TutorialDialogue = new DialogueSequenceRequested
						{
							DefinitionId = "guided_tutorial",
							SegmentId = sectionDef.PendingDialogKey,
							RequestId = Guid.NewGuid(),
						},
					};
				}

				return new PostVictoryAction { Kind = PostVictoryKind.AdvanceTutorial };
			}

			if (enemyId == EnemyId.FallenShepherd)
			{
				SaveCache.RecordWayStationClimbCompletion();
				WayStationArrivalContextService.Set(EntityManager, WayStationArrivalKind.ReturnedFromCompletedClimb);
				return new PostVictoryAction
				{
					Kind = PostVictoryKind.ShowWayStationTransition,
					Transition = new ShowTransition { Scene = SceneId.WayStation, EndRunOnLoad = true },
				};
			}

			var queuedEntity = EntityManager.GetEntity("QueuedEvents");
			var queued = queuedEntity?.GetComponent<QueuedEvents>();
			if (queued != null
				&& queued.Events != null
				&& queued.Events.Count > 0
				&& queued.CurrentIndex >= 0
				&& queued.CurrentIndex == queued.Events.Count - 1)
			{
				LoggingService.Append("EnemyDefeatFlowSystem.QuestComplete", new System.Text.Json.Nodes.JsonObject
				{
					["message"] = "attempting to save quest completion"
				});

				if (queued.IsClimbEncounter)
				{
					var climbCompletion = ClimbEncounterService.CompleteQueuedEncounter(EntityManager);
					return new PostVictoryAction
					{
						Kind = PostVictoryKind.ShowQuestReward,
						QuestReward = new ShowQuestRewardOverlay
						{
							Message = "Encounter Complete!",
							TitleLine1 = "Encounter",
							TitleLine2 = "Complete!",
							RewardGold = 0,
							HasCardReward = climbCompletion.DeckRewardOffer?.options != null && climbCompletion.DeckRewardOffer.options.Count > 0,
							RewardCardKeys = climbCompletion.DeckRewardOffer?.options?
								.Select(o => string.Equals(o.kind, DeckRewardOfferKinds.Exchange, StringComparison.OrdinalIgnoreCase)
									? o.incomingCardKey
									: o.upgradedCardKey)
								.Where(k => !string.IsNullOrWhiteSpace(k))
								.ToList() ?? new List<string>(),
							DeckRewardOffer = climbCompletion.DeckRewardOffer,
							IsEncounterReward = true,
							ClimbResources = climbCompletion.Resources,
							DismissScene = climbCompletion.PendingFinalEncounter ? SceneId.Battle : SceneId.Climb,
						},
					};
				}

				var completion = QuestCompleteService.SaveIfCompletedHighest(EntityManager);
				return new PostVictoryAction
				{
					Kind = PostVictoryKind.ShowQuestReward,
					QuestReward = new ShowQuestRewardOverlay
					{
						Message = "Quest Complete!",
						RewardGold = completion.IsNewlyCompleted ? completion.RewardGold : 0,
						HasCardReward = completion.HasCardReward,
						RewardCardKey = completion.RewardCardKey,
						RewardCardKeys = completion.RewardCardKeys,
						DeckRewardOffer = completion.DeckRewardOffer
					},
				};
			}

			return new PostVictoryAction
			{
				Kind = PostVictoryKind.ShowNextBattleTransition,
				Transition = new ShowTransition { Scene = SceneId.Battle },
			};
		}

		private void ExecutePostVictoryAction(PostVictoryAction action)
		{
			switch (action.Kind)
			{
				case PostVictoryKind.RestartTestFight:
				case PostVictoryKind.ShowWayStationTransition:
				case PostVictoryKind.ShowNextBattleTransition:
					EventManager.Publish(action.Transition);
					break;
				case PostVictoryKind.StartTutorialDialogue:
					_tutorialDialogueRequestId = action.TutorialDialogue.RequestId;
					EventManager.Publish(action.TutorialDialogue);
					break;
				case PostVictoryKind.AdvanceTutorial:
					GuidedTutorialService.AdvanceToNextSection(EntityManager);
					break;
				case PostVictoryKind.ShowQuestReward:
					EventManager.Publish(action.QuestReward);
					break;
			}
		}

		private bool WillOpenQuestRewardModal(Entity enemy)
		{
			if (TestFightRuntime.IsActive) return false;
			if (GuidedTutorialService.GetState(EntityManager) != null) return false;

			var enemyId = enemy?.GetComponent<Enemy>()?.EnemyBase?.Id;
			if (enemyId == EnemyId.FallenShepherd) return false;

			var queued = EntityManager.GetEntity("QueuedEvents")?.GetComponent<QueuedEvents>();
			return queued != null
				&& queued.Events != null
				&& queued.Events.Count > 0
				&& queued.CurrentIndex >= 0
				&& queued.CurrentIndex == queued.Events.Count - 1;
		}

		private void OnTutorialDialogueCompleted(DialogueSequenceCompleted evt)
		{
			if (evt == null || evt.RequestId == Guid.Empty || evt.RequestId != _tutorialDialogueRequestId) return;
			_tutorialDialogueRequestId = Guid.Empty;

			var tutorial = GuidedTutorialService.GetState(EntityManager);
			if (tutorial == null) return;

			var sectionDef = GuidedTutorialDefinitions.GetSection(tutorial.Section);
			if (sectionDef.PendingDialogKey == "last_of_them")
			{
				GuidedTutorialService.Complete(EntityManager);
			}
			else
			{
				GuidedTutorialService.AdvanceToNextSection(EntityManager);
			}
		}

		private void FinishWithoutBurst(Entity enemy, bool isPreview)
		{
			ResetPending();
			if (isPreview)
			{
				EndDefeatPresentationAndRestorePortrait(enemy);
				return;
			}

			CompleteRealDefeat(enemy);
		}

		private void EndDefeatPresentationInputFreeze()
		{
			SetDefeatPresentationActive(false);
		}

		private void EndDefeatPresentationAndRestorePortrait(Entity enemy)
		{
			EndDefeatPresentationInputFreeze();
			RestorePortraitAfterPreview(enemy);
		}

		private void RestorePortraitAfterPreview(Entity enemy)
		{
			if (enemy != null && enemy.HasComponent<SuppressPortraitRender>())
			{
				EntityManager.RemoveComponent<SuppressPortraitRender>(enemy);
			}
		}

		private static void EnsureSuppressPortrait(Entity enemy)
		{
			if (!enemy.HasComponent<SuppressPortraitRender>())
			{
				enemy.AddComponent(new SuppressPortraitRender());
			}
		}

		private void SetDefeatPresentationActive(bool active)
		{
			var phaseEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			var phase = phaseEntity?.GetComponent<PhaseState>();
			if (phase != null)
			{
				phase.DefeatPresentationActive = active;
			}
		}

		private void OnCachesCleared()
		{
			ResetPending();
			ClearVictoryStaging();
			SetDefeatPresentationActive(false);
		}

		private void ResetPending()
		{
			_pendingBurstId = null;
			_pendingEnemy = null;
			_pendingIsPreview = false;
			_tutorialDialogueRequestId = Guid.Empty;
		}

		private void ClearVictoryStaging()
		{
			_pendingPostVictory = default;
			_waitingForVictoryAnimation = false;
		}
	}
}
