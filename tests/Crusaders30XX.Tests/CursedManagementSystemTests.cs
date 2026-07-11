using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class CursedManagementSystemTests
{
	[Theory]
	[InlineData(CardApplicationType.Cursed, RunScopedStateService.RestrictionCursed)]
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
			_ = new CursedManagementSystem(entityManager);

			EventManager.Publish(new ApplyCardApplicationEvent
			{
				Card = card,
				Amount = 1,
				Type = type,
				Target = CardApplicationTarget.Deck,
			});

			Assert.True(card.HasComponent<Cursed>());
			Assert.Contains(
				restriction,
				SaveCache.GetRunDeckEntryRestrictions(RunDeckService.PrimaryLoadoutId, entry.entryId));

			EventManager.Publish(new RemoveCardApplication
			{
				Card = card,
				Type = type,
			});

			Assert.False(card.HasComponent<Cursed>());
			Assert.DoesNotContain(
				restriction,
				SaveCache.GetRunDeckEntryRestrictions(RunDeckService.PrimaryLoadoutId, entry.entryId));
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Saved_cursed_restriction_hydrates_onto_run_deck_cards()
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
				[RunScopedStateService.RestrictionCursed]);
			SaveCache.Reload();

			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == entry.entryId);

			Assert.True(card.HasComponent<Cursed>());
			Assert.Equal(Curse.CardIdValue, card.GetComponent<CardData>()?.Card?.CardId);
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Apply_event_converts_card_to_curse_and_stores_original()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var card = AddCard(entityManager, deck, deck.DrawPile, new Tempest());
			_ = new CursedManagementSystem(entityManager);

			EventManager.Publish(new ApplyCardApplicationEvent
			{
				Amount = 1,
				Type = CardApplicationType.Cursed,
				Target = CardApplicationTarget.DrawPile,
			});

			Assert.True(card.HasComponent<Cursed>());
			Assert.True(card.HasComponent<CursedOriginalCard>());
			Assert.Equal(Curse.CardIdValue, card.GetComponent<CardData>()?.Card?.CardId);
			Assert.Equal("tempest", card.GetComponent<CursedOriginalCard>()?.CardId);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Cursed_card_restores_original_tooltip_on_remove()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var card = EntityFactory.CreateCardFromDefinition(
				entityManager,
				"tempest",
				CardData.CardColor.Black,
				index: 0,
				isUpgraded: true);
			entityManager.AddComponent(card, new Brittle());
			_ = new CursedManagementSystem(entityManager);

			EventManager.Publish(new ApplyCardApplicationEvent
			{
				Card = card,
				Amount = 1,
				Type = CardApplicationType.Cursed,
				Target = CardApplicationTarget.Deck,
			});

			Assert.True(card.HasComponent<Cursed>());
			Assert.Equal(Curse.CardIdValue, card.GetComponent<CardData>()?.Card?.CardId);
			Assert.Equal(CardData.CardColor.Black, card.GetComponent<CardData>()?.Color);
			var tooltip = card.GetComponent<CardTooltip>();
			Assert.NotNull(tooltip);
			Assert.Equal("tempest", tooltip.CardId);
			Assert.Equal(CardData.CardColor.Black, tooltip.CardColor);
			Assert.True(tooltip.IsUpgraded);
			Assert.Contains(RunScopedStateService.RestrictionBrittle, tooltip.PreviewRestrictionNames);
			Assert.DoesNotContain(RunScopedStateService.RestrictionCursed, tooltip.PreviewRestrictionNames);
			Assert.Equal(TooltipType.Card, card.GetComponent<UIElement>()?.TooltipType);

			EventManager.Publish(new RemoveCardApplication
			{
				Card = card,
				Type = CardApplicationType.Cursed,
			});

			Assert.False(card.HasComponent<Cursed>());
			Assert.False(card.HasComponent<CursedOriginalCard>());
			Assert.Equal("tempest", card.GetComponent<CardData>()?.Card?.CardId);
			Assert.True(card.GetComponent<CardData>()?.Card?.IsUpgraded);
			Assert.Equal(TooltipType.Text, card.GetComponent<UIElement>()?.TooltipType);
			Assert.Null(card.GetComponent<CardTooltip>());
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Saved_cursed_card_tooltip_survives_battle_init_cleanup()
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
				[RunScopedStateService.RestrictionCursed]);
			SaveCache.Reload();

			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == entry.entryId);

			Assert.True(card.HasComponent<Cursed>());
			Assert.NotNull(card.GetComponent<CardTooltip>());
			var originalCardId = card.GetComponent<CursedOriginalCard>()?.CardId;
			Assert.False(string.IsNullOrWhiteSpace(originalCardId));

			SimulateBattleInitCardTooltipCleanup(entityManager);
			Assert.Null(card.GetComponent<CardTooltip>());

			_ = new CursedManagementSystem(entityManager);
			EventManager.Publish(new StartBattleRequested());

			var tooltip = card.GetComponent<CardTooltip>();
			Assert.NotNull(tooltip);
			Assert.Equal(originalCardId, tooltip.CardId);
			Assert.Equal(TooltipType.Card, card.GetComponent<UIElement>()?.TooltipType);
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Random_removal_removes_only_selected_cursed_cards()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deck = CreateDeck(entityManager);
			var first = AddCard(entityManager, deck, deck.Hand, new Tempest());
			var second = AddCard(entityManager, deck, deck.Hand, new Tempest());
			CursedManagementSystem.ApplyCursedRuntime(entityManager, first);
			CursedManagementSystem.ApplyCursedRuntime(entityManager, second);
			_ = new CursedManagementSystem(entityManager);

			EventManager.Publish(new RemoveCardApplications
			{
				Amount = 1,
				Type = CardApplicationType.Cursed,
				Target = CardApplicationTarget.Hand,
			});

			Assert.Equal(1, new[] { first, second }.Count(card => card.HasComponent<Cursed>()));
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static void SimulateBattleInitCardTooltipCleanup(EntityManager entityManager)
	{
		foreach (var entity in entityManager.GetEntitiesWithComponent<CardTooltip>().ToList())
		{
			var cardData = entity.GetComponent<CardData>();
			var definition = CardFactory.Create(cardData?.Card?.CardId);
			if (definition != null && string.IsNullOrEmpty(definition.CardTooltip))
			{
				entity.RemoveComponent<CardTooltip>();
			}
		}
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
