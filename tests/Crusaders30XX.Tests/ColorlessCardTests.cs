using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Objects.Medals;
using Crusaders30XX.ECS.Objects.Achievements;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class ColorlessCardTests
{
	[Theory]
	[InlineData(CardData.CardColor.Red)]
	[InlineData(CardData.CardColor.White)]
	[InlineData(CardData.CardColor.Black)]
	public void Colorless_has_no_qualified_color_but_remains_eligible_for_any(CardData.CardColor printedColor)
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, printedColor);
		entityManager.AddComponent(card, new Colorless());

		Assert.Empty(CardColorQualificationService.GetQualifiedColors(card));
		Assert.False(CardColorQualificationService.IsEligibleForCost(card, "Red"));
		Assert.False(CardColorQualificationService.IsEligibleForCost(card, "White"));
		Assert.False(CardColorQualificationService.IsEligibleForCost(card, "Black"));
		Assert.True(CardColorQualificationService.IsEligibleForCost(card, "Any"));
		Assert.Equal(printedColor, card.GetComponent<CardData>().Color);
	}

	[Fact]
	public void Removing_colorless_restores_printed_color_qualification()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(card, new Colorless());

		entityManager.RemoveComponent<Colorless>(card);

		Assert.Equal(
			[CardData.CardColor.Red],
			CardColorQualificationService.GetQualifiedColors(card));
		Assert.True(CardColorQualificationService.IsEligibleForCost(card, "Red"));
	}

	[Fact]
	public void Colorless_fails_only_color_restrictions_and_passes_not_color_restrictions()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(card, new Colorless());

		Assert.False(CardColorQualificationService.MeetsBlockingRestriction(card, BlockingRestrictionType.OnlyRed));
		Assert.False(CardColorQualificationService.MeetsBlockingRestriction(card, BlockingRestrictionType.OnlyWhite));
		Assert.False(CardColorQualificationService.MeetsBlockingRestriction(card, BlockingRestrictionType.OnlyBlack));
		Assert.True(CardColorQualificationService.MeetsBlockingRestriction(card, BlockingRestrictionType.NotRed));
		Assert.True(CardColorQualificationService.MeetsBlockingRestriction(card, BlockingRestrictionType.NotWhite));
		Assert.True(CardColorQualificationService.MeetsBlockingRestriction(card, BlockingRestrictionType.NotBlack));
	}

	[Fact]
	public void Colorless_black_card_loses_only_intrinsic_black_block_bonus()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, CardData.CardColor.Black);
		var modified = card.GetComponent<ModifiedBlock>();
		modified.Modifications.Add(new Modification { Delta = 2, Reason = "Test bonus" });

		Assert.Equal(card.GetComponent<CardData>().Card.Block + 3, BlockValueService.GetTotalBlockValue(card));

		entityManager.AddComponent(card, new Colorless());

		Assert.Equal(card.GetComponent<CardData>().Card.Block + 2, BlockValueService.GetTotalBlockValue(card));
	}

	[Fact]
	public void Colorless_blocker_grants_no_courage_or_temperance()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			entityManager.AddComponent(player, new Player());
			entityManager.AddComponent(player, new Courage());
			entityManager.AddComponent(player, new Temperance());
			_ = new CourageManagerSystem(entityManager);
			_ = new TemperanceManagerSystem(entityManager);
			var card = CreateCard(entityManager, CardData.CardColor.Red);
			entityManager.AddComponent(card, new Colorless());

			EventManager.Publish(new CardBlockedEvent { Card = card });

			Assert.Equal(0, player.GetComponent<Courage>().Amount);
			Assert.Equal(0, player.GetComponent<Temperance>().Amount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Colorless_block_assignment_keeps_block_and_blocker_count_without_color_count()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var enemy = entityManager.CreateEntity("Enemy");
			entityManager.AddComponent(enemy, new AttackIntent
			{
				ActiveAttackSequence = 1,
				Planned =
				[
					new PlannedAttack
					{
						AttackId = EnemyAttackId.Cinderbolt,
						AttackDefinition = new Cinderbolt(),
					},
				],
			});
			_ = new EnemyAttackProgressManagementSystem(entityManager);
			var card = CreateCard(entityManager, CardData.CardColor.Black);
			entityManager.AddComponent(card, new Colorless());

			EventManager.Publish(new BlockAssignmentAdded
			{
				Card = card,
				DeltaBlock = 3,
				Colors = CardColorQualificationService.GetQualifiedColors(card),
			});

			var progress = entityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
				.Single()
				.GetComponent<EnemyAttackProgress>();
			Assert.Equal(1, progress.PlayedCards);
			Assert.Equal(3, progress.AssignedBlockTotal);
			Assert.Equal(0, progress.PlayedRed);
			Assert.Equal(0, progress.PlayedWhite);
			Assert.Equal(0, progress.PlayedBlack);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Temporary_clone_inherits_persistent_statuses_without_run_deck_identity()
	{
		var entityManager = new EntityManager();
		var source = EntityFactory.CreateCardFromDefinition(
			entityManager,
			"strike",
			CardData.CardColor.White,
			cardKey: "strike|White",
			persistForRun: true);
		entityManager.AddComponent(source, new Frozen { Owner = source });
		entityManager.AddComponent(source, new Brittle { Owner = source });
		entityManager.AddComponent(source, new Scorched { Owner = source });
		entityManager.AddComponent(source, new Thorned { Owner = source });
		entityManager.AddComponent(source, new Colorless());

		var clone = EntityFactory.CloneEntity(entityManager, source);

		Assert.Same(clone, clone.GetComponent<Frozen>().Owner);
		Assert.Same(clone, clone.GetComponent<Brittle>().Owner);
		Assert.Same(clone, clone.GetComponent<Scorched>().Owner);
		Assert.Same(clone, clone.GetComponent<Thorned>().Owner);
		Assert.Same(clone, clone.GetComponent<Colorless>().Owner);
		Assert.True(clone.HasComponent<Frozen>());
		Assert.True(clone.HasComponent<Brittle>());
		Assert.True(clone.HasComponent<Scorched>());
		Assert.True(clone.HasComponent<Thorned>());
		Assert.True(clone.HasComponent<Colorless>());
		Assert.False(clone.HasComponent<RunDeckCard>());
	}

	[Fact]
	public void Color_selection_returns_no_color_when_hand_has_only_colorless_cards()
	{
		var entityManager = new EntityManager();
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var card = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(card, new Colorless());
		deck.Hand.Add(card);

		Assert.Null(PlayerHandColorService.GetRandomCardColorInPlayerHand(entityManager));
	}

	[Fact]
	public void Color_selection_returns_null_when_hand_has_only_pledged_cards()
	{
		var entityManager = new EntityManager();
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var card = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(card, new Pledge());
		deck.Hand.Add(card);

		Assert.Null(PlayerHandColorService.GetRandomCardColorInPlayerHand(entityManager));
	}

	[Fact]
	public void Color_selection_ignores_pledged_cards()
	{
		var entityManager = new EntityManager();
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var pledgedRed = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(pledgedRed, new Pledge());
		var playableWhite = CreateCard(entityManager, CardData.CardColor.White);
		deck.Hand.Add(pledgedRed);
		deck.Hand.Add(playableWhite);

		Assert.Equal(CardData.CardColor.White, PlayerHandColorService.GetRandomCardColorInPlayerHand(entityManager));
	}

	[Fact]
	public void Color_counting_card_ignores_colorless_payment_cards()
	{
		var entityManager = new EntityManager();
		var reap = new Reap();
		var reapEntity = CreateCard(entityManager, CardData.CardColor.Black);
		var first = CreateCard(entityManager, CardData.CardColor.Red);
		var second = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(first, new Colorless());
		var cacheEntity = entityManager.CreateEntity("LastPaymentCache");
		entityManager.AddComponent(cacheEntity, new LastPaymentCache
		{
			PaymentCards = [first, second],
			HasData = true,
		});

		Assert.Equal(0, reap.GetConditionalDamage(entityManager, reapEntity));

		entityManager.RemoveComponent<Colorless>(first);

		Assert.Equal(2, reap.GetConditionalDamage(entityManager, reapEntity));
	}

	[Fact]
	public void Black_card_medal_ignores_colorless_blocker()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StPeter();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			var card = CreateCard(entityManager, CardData.CardColor.Black);
			entityManager.AddComponent(card, new Colorless());

			EventManager.Publish(new CardBlockedEvent { Card = card });

			Assert.Equal(0, medal.CurrentCount);

			entityManager.RemoveComponent<Colorless>(card);
			EventManager.Publish(new CardBlockedEvent { Card = card });

			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Red_card_achievement_ignores_colorless_play()
	{
		EventManager.Clear();
		try
		{
			var progress = new AchievementProgress
			{
				AchievementId = "red_card_apprentice",
				State = AchievementState.Visible,
			};
			var achievement = new RedCardApprentice();
			achievement.Initialize(progress, new EntityManager());
			var entityManager = new EntityManager();
			var card = CreateCard(entityManager, CardData.CardColor.Red);
			entityManager.AddComponent(card, new Colorless());

			EventManager.Publish(new CardPlayedEvent { Card = card });
			Assert.Equal(0, progress.CurrentValue);

			entityManager.RemoveComponent<Colorless>(card);
			EventManager.Publish(new CardPlayedEvent { Card = card });
			Assert.Equal(1, progress.CurrentValue);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Colorless_persists_across_reload_and_run_deck_rehydration()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var entry = SaveCache.GetLoadout("loadout_1").cards.First();
			SaveCache.SetRunDeckEntryRestrictions(
				"loadout_1",
				entry.entryId,
				[RunScopedStateService.RestrictionColorless]);
			SaveCache.Reload();

			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == entry.entryId);

			Assert.Equal(entry.cardKey, card.GetComponent<RunDeckCard>().CardKey);
			Assert.True(card.HasComponent<Colorless>());
			Assert.Contains(
				TooltipTextService.ColorlessStatus,
				TooltipTextService.BuildCardTooltip(card, card.GetComponent<UIElement>().Tooltip));
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Colorless_status_tooltip_suppressed_during_guided_tutorial()
	{
		var entityManager = new EntityManager();
		var tutorialEntity = entityManager.CreateEntity("GuidedTutorial");
		entityManager.AddComponent(tutorialEntity, new GuidedTutorial { Section = 1 });

		var card = CreateCard(entityManager, CardData.CardColor.Black);
		entityManager.AddComponent(card, new Colorless { Owner = card });
		entityManager.AddComponent(card, new UIElement { Tooltip = "Strike" });

		var tooltip = TooltipTextService.BuildCardTooltip(card, card.GetComponent<UIElement>().Tooltip, entityManager);

		Assert.DoesNotContain(TooltipTextService.ColorlessStatus, tooltip);
		Assert.Contains("Strike", tooltip);
	}

	[Fact]
	public void Colorless_status_tooltip_suppressed_during_tutorial_bubbles()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, CardData.CardColor.Black);
		entityManager.AddComponent(card, new Colorless { Owner = card });
		entityManager.AddComponent(card, new UIElement { Tooltip = "Strike" });

		StateSingleton.IsTutorialActive = true;
		try
		{
			var tooltip = TooltipTextService.BuildCardTooltip(card, card.GetComponent<UIElement>().Tooltip, entityManager);
			Assert.DoesNotContain(TooltipTextService.ColorlessStatus, tooltip);
			Assert.Contains("Strike", tooltip);
		}
		finally
		{
			StateSingleton.IsTutorialActive = false;
		}
	}

	[Fact]
	public void Explicit_removal_prevents_later_rehydration()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var entry = SaveCache.GetLoadout("loadout_1").cards.First();
			SaveCache.SetRunDeckEntryRestrictions(
				"loadout_1",
				entry.entryId,
				[RunScopedStateService.RestrictionColorless]);
			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == entry.entryId);
			_ = new CardApplicationManagementSystem(entityManager);

			EventManager.Publish(new RemoveCardApplication
			{
				Card = card,
				Type = CardApplicationType.Colorless,
			});
			RunScopedStateService.HydrateRunCardRestrictions(entityManager);

			Assert.False(card.HasComponent<Colorless>());
			Assert.Empty(SaveCache.GetRunDeckEntryRestrictions("loadout_1", entry.entryId));
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Exhaust_removes_only_target_duplicate_entry_and_its_restrictions()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var targetEntry = SaveCache.GetLoadout("loadout_1").cards.First();
			var duplicateEntry = SaveCache.AddRunDeckEntry(
				"loadout_1",
				targetEntry.cardKey,
				publishChange: false);
			Assert.NotNull(duplicateEntry);
			Assert.NotEqual(targetEntry.entryId, duplicateEntry.entryId);
			SaveCache.SetRunDeckEntryRestrictions(
				"loadout_1",
				targetEntry.entryId,
				[RunScopedStateService.RestrictionColorless]);
			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == targetEntry.entryId);

			RunDeckService.ExhaustRunCard(entityManager, card);

			Assert.Null(SaveCache.GetRunDeckEntry("loadout_1", targetEntry.entryId));
			Assert.Empty(SaveCache.GetRunDeckEntryRestrictions("loadout_1", targetEntry.entryId));
			var survivingEntry = SaveCache.GetRunDeckEntry("loadout_1", duplicateEntry.entryId);
			Assert.NotNull(survivingEntry);
			Assert.Equal(targetEntry.cardKey, survivingEntry.cardKey);

			RunDeckService.EnsureRunDeck(entityManager);
			var duplicateCard = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == duplicateEntry.entryId);
			Assert.False(duplicateCard.HasComponent<Colorless>());
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	private static Entity CreateCard(EntityManager entityManager, CardData.CardColor color)
	{
		var card = entityManager.CreateEntity("Card");
		entityManager.AddComponent(card, new CardData
		{
			Card = new Strike(),
			Color = color,
		});
		entityManager.AddComponent(card, new ModifiedBlock());
		return card;
	}
}
