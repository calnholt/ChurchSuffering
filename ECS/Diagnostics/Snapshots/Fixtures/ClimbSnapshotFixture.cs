using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public enum ClimbSnapshotVariant
	{
		NoEvents,
		HazardEvent,
		CharacterEvent,
		HazardHoverPreview,
		CharacterHoverPreview,
		HazardConfirmation,
		CharacterSummary,
		CharacterDialog,
		ActiveEvents,
		HoverPreview,
		SoldShopSlot,
		EncounterRewardModal,
		ReplacementModal,
		InventoryOverlay,
	}

	public sealed class ClimbSnapshotFixture : IDisplaySnapshotFixture
	{
		private const int SnapshotSeed = 30030;
		private readonly ClimbSnapshotVariant _variant;
		private ClimbSceneSystem _climbScene;
		private ClimbHeaderLayoutSystem _headerLayout;
		private ClimbColumnLayoutSystem _columnLayout;
		private RewardModalDisplaySystem _rewardModal;
		private CardListModalSystem _cardListModal;
		private NarrativeEventModalDisplaySystem _narrativeModal;
		private DialogDisplaySystem _dialog;
		private bool _modalOpened;

		public ClimbSnapshotFixture(ClimbSnapshotVariant variant)
		{
			_variant = variant;
		}

		public string Id => _variant switch
		{
			ClimbSnapshotVariant.NoEvents => "climb-no-events",
			ClimbSnapshotVariant.HazardEvent => "climb-hazard-event",
			ClimbSnapshotVariant.CharacterEvent => "climb-character-event",
			ClimbSnapshotVariant.HazardHoverPreview => "climb-hazard-hover-preview",
			ClimbSnapshotVariant.CharacterHoverPreview => "climb-character-hover-preview",
			ClimbSnapshotVariant.HazardConfirmation => "climb-hazard-confirmation",
			ClimbSnapshotVariant.CharacterSummary => "climb-character-summary",
			ClimbSnapshotVariant.CharacterDialog => "climb-character-dialog",
			ClimbSnapshotVariant.ActiveEvents => "climb-active-events",
			ClimbSnapshotVariant.HoverPreview => "climb-hover-preview",
			ClimbSnapshotVariant.SoldShopSlot => "climb-sold-shop-slot",
			ClimbSnapshotVariant.EncounterRewardModal => "climb-encounter-reward-modal",
			ClimbSnapshotVariant.ReplacementModal => "climb-replacement-modal",
			ClimbSnapshotVariant.InventoryOverlay => "climb-inventory-overlay",
			_ => "climb",
		};

		public int WarmupFrames => _variant is ClimbSnapshotVariant.ReplacementModal or ClimbSnapshotVariant.InventoryOverlay ? 4 : 3;
		public string OutputFileName => Id;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			ConfigureSave();
			SetScene(ctx, SceneId.Climb);
			EventManager.Publish(new LoadSceneEvent
			{
				Scene = SceneId.Climb,
				PreviousScene = SceneId.Snapshot,
			});

			_climbScene = ctx.World.GetSystem<ClimbSceneSystem>();
			_headerLayout = ctx.World.GetSystem<ClimbHeaderLayoutSystem>();
			_columnLayout = ctx.World.GetSystem<ClimbColumnLayoutSystem>();
			_rewardModal = ctx.World.GetSystem<RewardModalDisplaySystem>();
			_cardListModal = ctx.World.GetSystem<CardListModalSystem>();
			_narrativeModal = ctx.World.GetSystem<NarrativeEventModalDisplaySystem>();
			_dialog = ctx.World.GetSystem<DialogDisplaySystem>();
			if (_variant == ClimbSnapshotVariant.CharacterDialog && _dialog != null)
			{
				_dialog.CharsPerSecond = 100000f;
			}

			if (_climbScene == null || _headerLayout == null || _columnLayout == null)
			{
				throw new DisplaySnapshotSetupException("Climb scene systems were not registered.");
			}
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			if (_variant is ClimbSnapshotVariant.HoverPreview
				or ClimbSnapshotVariant.HazardHoverPreview
				or ClimbSnapshotVariant.CharacterHoverPreview)
			{
				ForceHoverPreview(ctx.World.EntityManager);
			}

			if (_variant == ClimbSnapshotVariant.EncounterRewardModal)
			{
				OpenRewardModal();
			}
			else if (_variant == ClimbSnapshotVariant.ReplacementModal)
			{
				OpenReplacementModal(ctx.World.EntityManager);
			}
			else if (_variant == ClimbSnapshotVariant.InventoryOverlay)
			{
				OpenInventoryOverlay(ctx.World.EntityManager);
			}

			if (_variant == ClimbSnapshotVariant.CharacterDialog)
			{
				_climbScene.DrawBackgroundOnly();
				_dialog?.Draw();
				return;
			}

			_climbScene.Draw();

			if (_variant == ClimbSnapshotVariant.EncounterRewardModal)
			{
				_rewardModal?.Draw();
			}
			else if (_variant == ClimbSnapshotVariant.ReplacementModal)
			{
				_cardListModal?.Draw();
			}
			else if (_variant == ClimbSnapshotVariant.InventoryOverlay)
			{
				_cardListModal?.Draw();
			}
			else if (_variant is ClimbSnapshotVariant.HazardConfirmation or ClimbSnapshotVariant.CharacterSummary)
			{
				_narrativeModal?.Draw();
			}
		}

		private void ConfigureSave()
		{
			SaveCache.StartNewRun();
			var save = SaveCache.GetAll();
			save.isRunActive = true;
			save.runMapSeed = SnapshotSeed;
			save.pendingBattleNodeId = string.Empty;
			save.pendingDeckRewardOffer = null;

			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId)
				?? new LoadoutDefinition { id = RunDeckService.PrimaryLoadoutId, name = "Deck" };
			var cardKeys = new List<string>
			{
				"strike|White",
				"smite|White",
				"fervor|Red",
				"reckoning|White",
				"unburdened_strike|Black",
				"hold_the_line|White",
			};
			loadout.cards = cardKeys.Select((cardKey, index) => new LoadoutCardEntry
			{
				entryId = $"run_card_{index}",
				cardKey = cardKey,
				isStarter = true,
				restrictions = new List<string>(),
			}).ToList();
			save.nextRunDeckEntryId = loadout.cards.Count;
			loadout.weaponId = "sword";
			loadout.temperanceId = "angelic_aura";
			loadout.chestId = string.Empty;
			loadout.legsId = string.Empty;
			loadout.armsId = string.Empty;
			loadout.headId = string.Empty;
			loadout.medalIds = new List<string>();
			SaveCache.SaveLoadout(loadout);

			SaveCache.SaveClimbState(BuildClimbState());
		}

		private ClimbSaveState BuildClimbState()
		{
			int time = _variant is ClimbSnapshotVariant.HoverPreview
				or ClimbSnapshotVariant.HazardHoverPreview
				or ClimbSnapshotVariant.CharacterHoverPreview ? 6 : 5;
			var state = new ClimbSaveState
			{
				time = time,
				resources = new ClimbResourceSave { red = 2, white = 1, black = 1 },
				shopSlots = BuildShopSlots(time),
				encounterSlots = BuildEncounterSlots(time),
				eventSlots = BuildEventSlots(time),
				shownMedalIds = new List<string>(),
				shownEquipmentIds = new List<string>(),
				pendingReplacementOffer = _variant == ClimbSnapshotVariant.ReplacementModal
					? new ClimbReplacementOfferSave
					{
						shopSlotIndex = 3,
						incomingCardKey = "zealous_vow|Red",
						cost = new ClimbResourceSave { red = 1, white = 1, black = 0 },
					}
					: null,
				pendingEncounterReward = null,
			};

			if (_variant == ClimbSnapshotVariant.HazardConfirmation)
			{
				MarkPending(state, "event_0", ClimbEventFlowPhase.HazardConfirmation);
			}
			else if (_variant == ClimbSnapshotVariant.CharacterSummary)
			{
				MarkPending(state, "event_1", ClimbEventFlowPhase.CharacterSummary);
			}
			else if (_variant == ClimbSnapshotVariant.CharacterDialog)
			{
				MarkPending(state, "event_1", ClimbEventFlowPhase.CharacterDialogue, "72e54b0a-b057-46ce-a58a-30c085b882b0");
			}

			return state;
		}

		private static void MarkPending(ClimbSaveState state, string slotId, ClimbEventFlowPhase phase, string requestId = "")
		{
			var slot = state.eventSlots.First(eventSlot => string.Equals(eventSlot.id, slotId, StringComparison.Ordinal));
			slot.status = ClimbEventStatus.Pending;
			state.pendingEvent = new ClimbPendingEventSave
			{
				eventSlotId = slotId,
				phase = phase,
				dialogueRequestId = requestId,
			};
		}

		private List<ClimbShopSlotSave> BuildShopSlots(int time)
		{
			return new List<ClimbShopSlotSave>
			{
				new()
				{
					id = "shop_upgrade",
					kind = ClimbShopSlotKinds.Upgrade,
					cardKey = "smite|White|Upgraded",
					deckIndex = 1,
					cost = new ClimbResourceSave { red = 1, white = 1, black = 0 },
					timeCost = 1,
					generatedAtTime = time,
				},
				new()
				{
					id = "shop_replacement",
					kind = ClimbShopSlotKinds.Replacement,
					cardKey = "zealous_vow|Red",
					cost = new ClimbResourceSave { red = 1, white = 1, black = 0 },
					timeCost = 2,
					generatedAtTime = time,
				},
				new()
				{
					id = "shop_medal",
					kind = ClimbShopSlotKinds.Medal,
					itemId = "st_luke",
					cost = new ClimbResourceSave { red = 1, white = 0, black = 0 },
					timeCost = 0,
					generatedAtTime = time,
				},
				new()
				{
					id = "shop_medal_2",
					kind = ClimbShopSlotKinds.Medal,
					itemId = "st_michael",
					cost = new ClimbResourceSave { red = 0, white = 1, black = 0 },
					timeCost = 1,
					generatedAtTime = time,
				},
				new()
				{
					id = "shop_equipment",
					kind = ClimbShopSlotKinds.Equipment,
					itemId = "knightly_helm",
					cost = new ClimbResourceSave { red = 0, white = 1, black = 0 },
					timeCost = 1,
					isSold = _variant == ClimbSnapshotVariant.SoldShopSlot,
					generatedAtTime = time,
				},
			};
		}

		private List<ClimbEncounterSlotSave> BuildEncounterSlots(int time)
		{
			return new List<ClimbEncounterSlotSave>
			{
				new()
				{
					id = "encounter_0",
					enemyId = "skeleton",
					generatedAtTime = time,
					duration = 4,
					timeCost = _variant == ClimbSnapshotVariant.HoverPreview ? 2 : 3,
					rewardResources = _variant == ClimbSnapshotVariant.HoverPreview
						? new ClimbResourceSave { red = 1, white = 1, black = 0 }
						: new ClimbResourceSave { red = 1, white = 1, black = 1 },
					hasDeckReward = true,
				},
				new()
				{
					id = "encounter_1",
					enemyId = "demon",
					generatedAtTime = time,
					duration = 3,
					timeCost = 1,
					rewardResources = new ClimbResourceSave { red = 0, white = 1, black = 0 },
					hasDeckReward = true,
				},
				new()
				{
					id = "encounter_2",
					enemyId = "thornreaver",
					generatedAtTime = time,
					duration = 5,
					timeCost = 3,
					rewardResources = new ClimbResourceSave { red = 1, white = 1, black = 1 },
					hasDeckReward = true,
				},
			};
		}

		private List<ClimbEventSlotSave> BuildEventSlots(int time)
		{
			if (_variant == ClimbSnapshotVariant.NoEvents)
			{
				return BuildInactiveEventSlots(time, startIndex: 0);
			}

			bool legacyCombined = _variant is ClimbSnapshotVariant.ActiveEvents or ClimbSnapshotVariant.HoverPreview;
			bool hazardActive = legacyCombined || _variant is ClimbSnapshotVariant.HazardEvent
				or ClimbSnapshotVariant.HazardHoverPreview or ClimbSnapshotVariant.HazardConfirmation;
			bool characterActive = legacyCombined || _variant is ClimbSnapshotVariant.CharacterEvent
				or ClimbSnapshotVariant.CharacterHoverPreview or ClimbSnapshotVariant.CharacterSummary
				or ClimbSnapshotVariant.CharacterDialog;
			var slots = new List<ClimbEventSlotSave>
			{
				new()
				{
					id = "event_0",
					definitionId = "winter_reliquary",
					kind = ClimbEventKind.Hazard,
					hazardEffect = ClimbHazardEffectType.Frozen,
					scheduledAppearanceTime = hazardActive ? Math.Max(1, time - 1) : time + 2,
					activatedAtTime = hazardActive ? Math.Max(0, time - 1) : -1,
					duration = 4,
					timeCost = 0,
					effectAmount = 1,
					rewardResources = new ClimbResourceSave { red = 1, white = 0, black = 1 },
					status = hazardActive ? ClimbEventStatus.Active : ClimbEventStatus.Scheduled,
				},
				new()
				{
					id = "event_1",
					definitionId = "nun_counsel",
					kind = ClimbEventKind.Character,
					characterReward = ClimbCharacterRewardType.Temperance,
					scheduledAppearanceTime = characterActive ? time : time + 3,
					activatedAtTime = characterActive ? time : -1,
					duration = 4,
					timeCost = 1,
					rewardResources = new ClimbResourceSave { red = 0, white = 0, black = 0 },
					status = characterActive ? ClimbEventStatus.Active : ClimbEventStatus.Scheduled,
				},
			};
			slots.AddRange(BuildInactiveEventSlots(time, startIndex: 2));
			return slots;
		}

		private static List<ClimbEventSlotSave> BuildInactiveEventSlots(int time, int startIndex)
		{
			var definitions = new[] { "glass_psalm", "smith_forging", "second_footsteps", "nun_counsel", "penitents_chain" };
			var slots = new List<ClimbEventSlotSave>();
			for (int index = startIndex; index < ClimbRuleService.EventSlotCount; index++)
			{
				bool character = definitions[index].Contains("smith", StringComparison.Ordinal)
					|| definitions[index].Contains("nun", StringComparison.Ordinal);
				slots.Add(new ClimbEventSlotSave
				{
					id = $"event_{index}",
					definitionId = definitions[index],
					kind = character ? ClimbEventKind.Character : ClimbEventKind.Hazard,
					scheduledAppearanceTime = Math.Min(ClimbRuleService.MaxTime, time + index + 2),
					activatedAtTime = -1,
					duration = character ? 4 : 3,
					timeCost = character ? 1 : 0,
					rewardResources = character
						? new ClimbResourceSave { red = 0, white = 0, black = 0 }
						: new ClimbResourceSave { red = 1, white = 0, black = 0 },
					status = ClimbEventStatus.Scheduled,
				});
			}
			return slots;
		}

		private void ForceHoverPreview(EntityManager entityManager)
		{
			string targetSlotId = _variant switch
			{
				ClimbSnapshotVariant.HazardHoverPreview => "event_0",
				ClimbSnapshotVariant.CharacterHoverPreview => "event_1",
				_ => "encounter_0",
			};
			var target = entityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
				.FirstOrDefault(e => string.Equals(
					e.GetComponent<ClimbSlotPresentation>()?.SlotId,
					targetSlotId,
					StringComparison.OrdinalIgnoreCase));
			if (target == null) return;

			foreach (var ui in entityManager.GetEntitiesWithComponent<UIElement>()
				.Select(e => e.GetComponent<UIElement>())
				.Where(ui => ui != null))
			{
				ui.IsHovered = false;
			}

			var targetUi = target.GetComponent<UIElement>();
			if (targetUi != null)
			{
				targetUi.IsHovered = true;
			}

			var time = new GameTime(TimeSpan.FromSeconds(1d), TimeSpan.FromSeconds(1d / 60d));
			_headerLayout?.Update(time);
			_columnLayout?.Update(time);
		}

		private void OpenRewardModal()
		{
			if (_modalOpened) return;
			_modalOpened = true;
			if (_rewardModal == null)
			{
				throw new DisplaySnapshotSetupException("Reward modal system was not registered.");
			}

			_rewardModal.OpenEncounterRewardForSnapshot(
				new ClimbResourceSave { red = 2, white = 1, black = 1 });
		}

		private void OpenReplacementModal(EntityManager entityManager)
		{
			if (_modalOpened) return;
			_modalOpened = true;
			if (_cardListModal == null)
			{
				throw new DisplaySnapshotSetupException("Card list modal system was not registered.");
			}

			var deck = RunDeckService.EnsureRunDeck(entityManager)?.GetComponent<Deck>();
			var cards = new List<Entity>();
			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			for (int i = 0; i < (loadout?.cards?.Count ?? 0); i++)
			{
				var entry = loadout.cards[i];
				string key = entry.cardKey;
				if (!ClimbShopService.IsReplacementEligible(key)) continue;
				var card = deck?.Cards.FirstOrDefault(e =>
					string.Equals(e.GetComponent<RunDeckCard>()?.EntryId, entry.entryId, StringComparison.Ordinal));
				if (card == null) continue;
				if (card.GetComponent<CardListModalSelectionMetadata>() == null)
				{
					entityManager.AddComponent(card, new CardListModalSelectionMetadata
					{
						SelectionContext = CardListSelectionContexts.ClimbReplacement,
						EntryId = entry.entryId,
						CardKey = key,
						SourceIndex = i,
					});
				}
				cards.Add(card);
			}

			if (cards.Count == 0)
			{
				throw new DisplaySnapshotSetupException("No eligible replacement cards were created.");
			}

			_cardListModal.OpenForSnapshot(
				"Choose Replacement",
				cards,
				isSelectable: true,
				selectionContext: CardListSelectionContexts.ClimbReplacement);
			_cardListModal.Update(new GameTime(TimeSpan.FromSeconds(1d), TimeSpan.FromSeconds(1d / 60d)));
		}

		private void OpenInventoryOverlay(EntityManager entityManager)
		{
			if (_modalOpened) return;
			_modalOpened = true;
			if (_cardListModal == null)
			{
				throw new DisplaySnapshotSetupException("Card list modal system was not registered.");
			}

			var deck = RunDeckService.EnsureRunDeck(entityManager)?.GetComponent<Deck>();
			if (deck?.Cards == null || deck.Cards.Count == 0)
			{
				throw new DisplaySnapshotSetupException("No run deck cards were created.");
			}

			_cardListModal.OpenInventoryForSnapshot("Run Overview", deck.Cards.ToList());
			_cardListModal.Update(new GameTime(TimeSpan.FromSeconds(1d), TimeSpan.FromSeconds(1d / 60d)));
		}

		private static void SetScene(DisplaySnapshotContext ctx, SceneId sceneId)
		{
			var scene = ctx.SceneEntity.GetComponent<SceneState>();
			if (scene == null)
			{
				ctx.World.AddComponent(ctx.SceneEntity, new SceneState { Current = sceneId });
				return;
			}
			scene.Current = sceneId;
		}
	}
}
