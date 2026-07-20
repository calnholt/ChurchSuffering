using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class DualColorCardTests : IDisposable
{
	public DualColorCardTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Dual_color_qualifies_as_either_color_but_only_fills_one_cost_slot()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, CardData.CardColor.White);
		entityManager.AddComponent(card, new DualColor
		{
			Owner = card,
			SecondaryColor = CardData.CardColor.Black,
		});

		Assert.Equal(
			[CardData.CardColor.White, CardData.CardColor.Black],
			CardColorQualificationService.GetQualifiedColors(card));
		Assert.True(CardColorQualificationService.IsEligibleForCost(card, "White"));
		Assert.True(CardColorQualificationService.IsEligibleForCost(card, "Black"));
		Assert.False(CardColorQualificationService.IsEligibleForCost(card, "Red"));
		Assert.Equal(0, CardPlayResolver.AnalyzeCostSolutions(["White", "Black"], [card]).SolutionCount);

		var secondCard = CreateCard(entityManager, CardData.CardColor.Black);
		Assert.Equal(1, CardPlayResolver.AnalyzeCostSolutions(["White", "Black"], [card, secondCard]).SolutionCount);
	}

	[Fact]
	public void Black_secondary_grants_block_bonus_and_colorless_temporarily_suppresses_both_colors()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, CardData.CardColor.White);
		entityManager.AddComponent(card, new DualColor
		{
			Owner = card,
			SecondaryColor = CardData.CardColor.Black,
		});

		Assert.Equal(card.GetComponent<CardData>().Card.Block + 1, BlockValueService.GetTotalBlockValue(card));

		entityManager.AddComponent(card, new Colorless { Owner = card });
		Assert.Empty(CardColorQualificationService.GetQualifiedColors(card));
		Assert.Equal(card.GetComponent<CardData>().Card.Block, BlockValueService.GetTotalBlockValue(card));

		entityManager.RemoveComponent<Colorless>(card);
		Assert.Equal(
			[CardData.CardColor.White, CardData.CardColor.Black],
			CardColorQualificationService.GetQualifiedColors(card));
		Assert.Equal(card.GetComponent<CardData>().Card.Block + 1, BlockValueService.GetTotalBlockValue(card));
	}

	[Fact]
	public void Dual_color_block_grants_each_matching_color_bonus_once()
	{
		var entityManager = new EntityManager();
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Courage());
		entityManager.AddComponent(player, new Temperance());
		_ = new CourageManagerSystem(entityManager);
		_ = new TemperanceManagerSystem(entityManager);
		var card = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(card, new DualColor
		{
			Owner = card,
			SecondaryColor = CardData.CardColor.White,
		});

		EventManager.Publish(new CardBlockedEvent { Card = card });

		Assert.Equal(1, player.GetComponent<Courage>().Amount);
		Assert.Equal(1, player.GetComponent<Temperance>().Amount);
	}

	[Fact]
	public void Dual_color_block_counts_one_card_one_block_value_and_each_color_counter()
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
		var card = CreateCard(entityManager, CardData.CardColor.Red);

		EventManager.Publish(new BlockAssignmentAdded
		{
			Card = card,
			DeltaBlock = 4,
			Colors = [CardData.CardColor.Red, CardData.CardColor.White],
		});

		var progress = entityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
			.Single()
			.GetComponent<EnemyAttackProgress>();
		Assert.Equal(1, progress.PlayedCards);
		Assert.Equal(4, progress.AssignedBlockTotal);
		Assert.Equal(1, progress.PlayedRed);
		Assert.Equal(1, progress.PlayedWhite);
		Assert.Equal(0, progress.PlayedBlack);

		EventManager.Publish(new BlockAssignmentRemoved
		{
			Card = card,
			DeltaBlock = -4,
			Colors = [CardData.CardColor.Red, CardData.CardColor.White],
		});
		Assert.Equal(0, progress.PlayedCards);
		Assert.Equal(0, progress.AssignedBlockTotal);
		Assert.Equal(0, progress.PlayedRed);
		Assert.Equal(0, progress.PlayedWhite);
	}

	[Fact]
	public void Applying_dual_color_persists_rehydrates_and_clones()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var entry = SaveCache.AddRunDeckEntry(
				RunDeckService.PrimaryLoadoutId,
				"strike|White",
				publishChange: false);
			Assert.NotNull(entry);

			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			Assert.All(
				entityManager.GetEntitiesWithComponent<RunDeckCard>(),
				entity => Assert.False(entity.HasComponent<DualColor>()));
			var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == entry.entryId);
			_ = new DualColorManagementSystem(entityManager);

			EventManager.Publish(new ApplyDualColorEvent
			{
				Card = card,
				SecondaryColor = CardData.CardColor.Black,
			});

			Assert.Equal(CardData.CardColor.Black, card.GetComponent<DualColor>().SecondaryColor);
			Assert.Equal("Black", SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, entry.entryId).secondaryColor);

			var clone = EntityFactory.CloneEntity(entityManager, card);
			Assert.Equal(CardData.CardColor.Black, clone.GetComponent<DualColor>().SecondaryColor);
			Assert.False(clone.HasComponent<RunDeckCard>());

			SaveCache.Reload();
			var reloadedEntityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(reloadedEntityManager);
			var reloaded = reloadedEntityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == entry.entryId);
			Assert.Equal(CardData.CardColor.Black, reloaded.GetComponent<DualColor>().SecondaryColor);
		}
		finally
		{
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Debug_command_applies_a_different_random_color_to_an_eligible_hand_card()
	{
		var entityManager = new EntityManager();
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var card = CreateCard(entityManager, CardData.CardColor.Red);
		deck.Hand.Add(card);
		_ = new DualColorManagementSystem(entityManager);

		new DebugCommandSystem(entityManager).Debug_ApplyDualColorHand();

		var dualColor = card.GetComponent<DualColor>();
		Assert.NotNull(dualColor);
		Assert.NotEqual(CardData.CardColor.Red, dualColor.SecondaryColor);
		Assert.True(CardColorQualificationService.IsPlayableColor(dualColor.SecondaryColor));
	}

	private static Entity CreateCard(EntityManager entityManager, CardData.CardColor color)
	{
		var card = entityManager.CreateEntity("Card");
		var definition = new Strike();
		entityManager.AddComponent(card, new CardData
		{
			Owner = card,
			Card = definition,
			Color = color,
		});
		entityManager.AddComponent(card, new ModifiedBlock { Owner = card });
		definition.Initialize(entityManager, card);
		return card;
	}
}
