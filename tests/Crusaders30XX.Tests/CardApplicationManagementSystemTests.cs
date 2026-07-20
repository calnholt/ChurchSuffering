using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class CardApplicationManagementSystemTests
{
	[Theory]
	[InlineData(CardApplicationTarget.HandAndDrawPile, true, true, false, false)]
	[InlineData(CardApplicationTarget.TopXCards, false, true, false, false)]
	[InlineData(CardApplicationTarget.DrawPile, false, true, false, false)]
	[InlineData(CardApplicationTarget.DrawPileAndDiscard, false, true, true, false)]
	[InlineData(CardApplicationTarget.Hand, true, false, false, false)]
	[InlineData(CardApplicationTarget.Deck, true, true, true, true)]
	public void Target_applies_only_to_cards_in_selected_zones(
		CardApplicationTarget target,
		bool appliesToHand,
		bool appliesToDrawPile,
		bool appliesToDiscardPile,
		bool appliesToExhaustPile)
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var handCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
			var drawCard = AddCard(entityManager, deck, deck.DrawPile, new Tempest());
			var discardCard = AddCard(entityManager, deck, deck.DiscardPile, new Tempest());
			var exhaustCard = AddCard(entityManager, deck, deck.ExhaustPile, new Tempest());
			var mutationSystem = SetupApplyPipeline(entityManager);

			EventManager.Publish(new ApplyCardApplicationEvent
			{
				Amount = 10,
				Type = CardApplicationType.Frozen,
				Target = target,
			});
			BattleMutationTestSupport.CompleteMutations(entityManager, mutationSystem);

			Assert.Equal(appliesToHand, handCard.HasComponent<Frozen>());
			Assert.Equal(appliesToDrawPile, drawCard.HasComponent<Frozen>());
			Assert.Equal(appliesToDiscardPile, discardCard.HasComponent<Frozen>());
			Assert.Equal(appliesToExhaustPile, exhaustCard.HasComponent<Frozen>());
		}
		finally
		{
			EventManager.Clear();
			BattleMutationTestSupport.ResetSettings();
		}
	}

	[Fact]
	public void Application_type_adds_the_corresponding_component()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var card = AddCard(entityManager, deck, deck.DrawPile, new Tempest());
			var mutationSystem = SetupApplyPipeline(entityManager);

			Apply(entityManager, mutationSystem, CardApplicationType.Frozen);
			Apply(entityManager, mutationSystem, CardApplicationType.Brittle);
			Apply(entityManager, mutationSystem, CardApplicationType.Scorched);
			Apply(entityManager, mutationSystem, CardApplicationType.Thorned);
			Apply(entityManager, mutationSystem, CardApplicationType.Colorless);

			Assert.True(card.HasComponent<Frozen>());
			Assert.True(card.HasComponent<Brittle>());
			Assert.True(card.HasComponent<Scorched>());
			Assert.True(card.HasComponent<Thorned>());
			Assert.True(card.HasComponent<Colorless>());
		}
		finally
		{
			EventManager.Clear();
			BattleMutationTestSupport.ResetSettings();
		}
	}

	[Fact]
	public void Sealed_application_adds_requested_stacks_and_can_reapply()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var card = AddCard(entityManager, deck, deck.Hand, new Tempest());
			var mutationSystem = SetupApplyPipeline(entityManager);

			for (int i = 0; i < 2; i++)
			{
				EventManager.Publish(new ApplyCardApplicationEvent
				{
					Amount = 1,
					StacksPerCard = 2,
					Type = CardApplicationType.Sealed,
					Target = CardApplicationTarget.Hand,
				});
				BattleMutationTestSupport.CompleteMutations(entityManager, mutationSystem);
			}

			Assert.Equal(4, card.GetComponent<Sealed>()?.Seals);
		}
		finally
		{
			EventManager.Clear();
			BattleMutationTestSupport.ResetSettings();
		}
	}

	[Fact]
	public void Sealed_stacks_persist_exactly_across_deck_hydration()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var entry = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.First();
			SaveCache.SetRunDeckEntryRestrictionState(
				RunDeckService.PrimaryLoadoutId,
				entry.entryId,
				[RunScopedStateService.RestrictionSealed],
				new System.Collections.Generic.Dictionary<string, int>
				{
					[RunScopedStateService.RestrictionSealed] = 3,
				});
			SaveCache.Reload();

			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == entry.entryId);

			Assert.Equal(3, card.GetComponent<Sealed>()?.Seals);
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Curse_is_factory_creatable_but_not_in_card_pool()
	{
		Assert.IsType<Curse>(CardFactory.Create(Curse.CardIdValue));
		Assert.DoesNotContain(CardId.Curse, CardFactory.GetAllCards().Keys);
	}

	[Theory]
	[InlineData(CardApplicationType.Colorless, RunScopedStateService.RestrictionColorless)]
	[InlineData(CardApplicationType.Scorched, RunScopedStateService.RestrictionScorched)]
	[InlineData(CardApplicationType.Thorned, RunScopedStateService.RestrictionThorned)]
	public void Exact_card_apply_and_remove_synchronize_persistence(
		CardApplicationType type,
		string restriction)
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var entry = SaveCache.AddRunDeckEntry(
				RunDeckService.PrimaryLoadoutId,
				"tempest|White",
				publishChange: false);
			Assert.NotNull(entry);
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var card = AddCard(entityManager, deck, deck.Hand, new Tempest());
			entityManager.AddComponent(card, new RunDeckCard
			{
				EntryId = entry.entryId,
				CardKey = entry.cardKey,
			});
			var mutationSystem = SetupApplyPipeline(entityManager);

			EventManager.Publish(new ApplyCardApplicationEvent
			{
				Card = card,
				Amount = 1,
				Type = type,
				Target = CardApplicationTarget.Deck,
			});
			BattleMutationTestSupport.CompleteMutations(entityManager, mutationSystem);

			Assert.True(HasApplication(card, type));
			Assert.Contains(
				restriction,
				SaveCache.GetRunDeckEntryRestrictions(RunDeckService.PrimaryLoadoutId, entry.entryId));

			EventManager.Publish(new RemoveCardApplication
			{
				Card = card,
				Type = type,
			});

			Assert.False(HasApplication(card, type));
			Assert.DoesNotContain(
				restriction,
				SaveCache.GetRunDeckEntryRestrictions(RunDeckService.PrimaryLoadoutId, entry.entryId));
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
			BattleMutationTestSupport.ResetSettings();
		}
	}

	[Theory]
	[InlineData(RunScopedStateService.RestrictionScorched)]
	[InlineData(RunScopedStateService.RestrictionThorned)]
	public void Saved_new_status_restrictions_hydrate_onto_run_deck_cards(string restriction)
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var entry = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.First();
			SaveCache.SetRunDeckEntryRestrictions(
				RunDeckService.PrimaryLoadoutId,
				entry.entryId,
				[restriction]);
			SaveCache.Reload();

			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == entry.entryId);

			if (restriction == RunScopedStateService.RestrictionScorched)
				Assert.True(card.HasComponent<Scorched>());
			else
				Assert.True(card.HasComponent<Thorned>());
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Random_zone_remove_only_removes_selected_application()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var first = AddCard(entityManager, deck, deck.Hand, new Tempest());
			var second = AddCard(entityManager, deck, deck.Hand, new Tempest());
			entityManager.AddComponent(first, new Colorless());
			entityManager.AddComponent(second, new Colorless());
			entityManager.AddComponent(first, new Brittle());
			_ = new CardApplicationManagementSystem(entityManager);

			EventManager.Publish(new RemoveCardApplications
			{
				Amount = 1,
				Type = CardApplicationType.Colorless,
				Target = CardApplicationTarget.Hand,
			});

			Assert.Equal(1, new[] { first, second }.Count(card => card.HasComponent<Colorless>()));
			Assert.True(first.HasComponent<Brittle>());
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Application_skips_ineligible_and_already_applied_cards()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var eligibleCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
			var alreadyFrozenCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
			var pledgedCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
			var weaponCard = AddCard(entityManager, deck, deck.Hand, new Dagger());
			entityManager.AddComponent(alreadyFrozenCard, new Frozen());
			entityManager.AddComponent(pledgedCard, new Pledge());
			_ = new CardApplicationManagementSystem(entityManager);
			var mutationSystem = SetupApplyPipeline(entityManager);

			EventManager.Publish(new ApplyCardApplicationEvent
			{
				Amount = 1,
				Type = CardApplicationType.Frozen,
				Target = CardApplicationTarget.Hand,
			});
			BattleMutationTestSupport.CompleteMutations(entityManager, mutationSystem);

			Assert.True(eligibleCard.HasComponent<Frozen>());
			Assert.True(alreadyFrozenCard.HasComponent<Frozen>());
			Assert.False(pledgedCard.HasComponent<Frozen>());
			Assert.False(weaponCard.HasComponent<Frozen>());
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Generic_system_ignores_cursed_events_without_throwing()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var card = AddCard(entityManager, deck, deck.Hand, new Tempest());
			_ = new CardApplicationManagementSystem(entityManager);

			var exception = Record.Exception(() =>
			{
				EventManager.Publish(new ApplyCardApplicationEvent
				{
					Card = card,
					Amount = 1,
					Type = CardApplicationType.Cursed,
					Target = CardApplicationTarget.Hand,
				});
				EventManager.Publish(new RemoveCardApplication
				{
					Card = card,
					Type = CardApplicationType.Cursed,
				});
				EventManager.Publish(new RemoveCardApplications
				{
					Amount = 1,
					Type = CardApplicationType.Cursed,
					Target = CardApplicationTarget.Hand,
				});
			});

			Assert.Null(exception);
			Assert.False(card.HasComponent<Cursed>());
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static BattleCardMutationDisplaySystem SetupApplyPipeline(EntityManager entityManager)
	{
		var mutationSystem = BattleMutationTestSupport.CreateBattleMutationPipeline(entityManager);
		_ = new CardApplicationManagementSystem(entityManager);
		return mutationSystem;
	}

	private static void Apply(
		EntityManager entityManager,
		BattleCardMutationDisplaySystem mutationSystem,
		CardApplicationType type)
	{
		EventManager.Publish(new ApplyCardApplicationEvent
		{
			Amount = 1,
			Type = type,
			Target = CardApplicationTarget.DrawPile,
		});
		BattleMutationTestSupport.CompleteMutations(entityManager, mutationSystem);
	}

	private static bool HasApplication(Entity card, CardApplicationType type)
	{
		return type switch
		{
			CardApplicationType.Frozen => card.HasComponent<Frozen>(),
			CardApplicationType.Brittle => card.HasComponent<Brittle>(),
			CardApplicationType.Scorched => card.HasComponent<Scorched>(),
			CardApplicationType.Thorned => card.HasComponent<Thorned>(),
			CardApplicationType.Colorless => card.HasComponent<Colorless>(),
			CardApplicationType.Sealed => card.HasComponent<Sealed>(),
			_ => false,
		};
	}

	private static Deck CreateDeck(EntityManager entityManager)
	{
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		return deck;
	}

	private static Entity AddCard(
		EntityManager entityManager,
		Deck deck,
		System.Collections.Generic.ICollection<Entity> zone,
		CardBase definition)
	{
		var card = entityManager.CreateEntity(definition.CardId);
		entityManager.AddComponent(card, new CardData { Card = definition });
		deck.Cards.Add(card);
		zone.Add(card);
		return card;
	}
}
