using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using System.Linq;
using Xunit;

namespace Crusaders30XX.Tests;

public class DeckManagementSystemTests
{
    [Fact]
    public void DrawCard_clears_stale_filtered_from_hand_marker()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = new Deck();
        var card = CreateCard(entityManager);
        entityManager.AddComponent(card, new FilteredFromHand());
        entityManager.AddComponent(card, new HotKey { Button = FaceButton.B, Position = HotKeyPosition.Top });
        deck.DrawPile.Add(card);
        var system = new DeckManagementSystem(entityManager);

        bool drawn = system.DrawCard(deck);

        Assert.True(drawn);
        Assert.Contains(card, deck.Hand);
        Assert.DoesNotContain(card, deck.DrawPile);
        Assert.False(card.HasComponent<FilteredFromHand>());
        Assert.False(card.HasComponent<HotKey>());
        Assert.True(HandStateLoggingService.CountsForHandLayout(card));
    }

    [Fact]
    public void Run_deck_cards_do_not_get_suppress_stat_delta_display()
    {
        EventManager.Clear();
        SaveCache.DeleteSaveFilesIfPresent();
        try
        {
            SaveCache.StartNewRun();
            var entityManager = new EntityManager();
            const string cardKey = "strike|Black";
            var entry = SaveCache.AddRunDeckEntry(
                RunDeckService.PrimaryLoadoutId,
                cardKey,
                publishChange: false);
            Assert.NotNull(entry);

            RunDeckService.AddCardFromEntry(entityManager, entry.entryId);

            var deckCard = entityManager
                .GetEntitiesWithComponent<RunDeckCard>()
                .FirstOrDefault(e => e.GetComponent<RunDeckCard>()?.EntryId == entry.entryId);

            Assert.NotNull(deckCard);
            Assert.Equal(cardKey, deckCard.GetComponent<RunDeckCard>().CardKey);
            Assert.False(deckCard.HasComponent<SuppressStatDeltaDisplay>());

            var previewCard = EntityFactory.CreateCardFromDefinition(
                entityManager,
                "strike",
                CardData.CardColor.Black,
                suppressStatDeltaDisplay: true);

            Assert.NotNull(previewCard);
            Assert.True(previewCard.HasComponent<SuppressStatDeltaDisplay>());
        }
        finally
        {
            EventManager.Clear();
            SaveCache.DeleteSaveFilesIfPresent();
        }
    }

    [Fact]
    public void RemoveRandomCardEvent_removes_only_starter_cards_from_loadout()
    {
        EventManager.Clear();
        SaveCache.DeleteSaveFilesIfPresent();
        SaveCache.StartNewRun();

        var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
        loadout.cards =
        [
            new LoadoutCardEntry
            {
                entryId = SaveCache.AllocateRunDeckEntryId(),
                cardKey = "smite|Red",
                isStarter = true,
            },
            new LoadoutCardEntry
            {
                entryId = SaveCache.AllocateRunDeckEntryId(),
                cardKey = "smite|White",
                isStarter = true,
            },
            new LoadoutCardEntry
            {
                entryId = SaveCache.AllocateRunDeckEntryId(),
                cardKey = "fervor|Red",
                isStarter = false,
            },
        ];
        SaveCache.SaveLoadout(loadout);
        var nonStarterEntryId = loadout.cards.Single(entry => !entry.isStarter).entryId;
        var starterEntryIds = loadout.cards.Where(entry => entry.isStarter).Select(entry => entry.entryId).ToHashSet();

        var entityManager = new EntityManager();
        _ = new DeckManagementSystem(entityManager);
        RunDeckService.EnsureRunDeck(entityManager);

        var beforeCount = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.Count;
        Assert.Equal(3, beforeCount);

        EventManager.Publish(new RemoveRandomCardEvent { Amount = 1 });

        var after = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
        Assert.Equal(2, after.cards.Count);
        Assert.Contains(after.cards, entry => entry.entryId == nonStarterEntryId && entry.cardKey == "fervor|Red");
        Assert.Equal(1, after.cards.Count(entry => entry.isStarter));
        Assert.Single(starterEntryIds.Where(entryId => after.cards.All(entry => entry.entryId != entryId)));
    }

    [Fact]
    public void DrawRandomCardFromDiscardEvent_moves_cards_to_hand()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = CreateDeckEntity(entityManager);
        deck.DiscardPile.Add(CreateCard(entityManager));
        deck.DiscardPile.Add(CreateCard(entityManager));
        deck.DiscardPile.Add(CreateCard(entityManager));
        _ = new DeckManagementSystem(entityManager);

        EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = 2 });

        Assert.Equal(2, deck.Hand.Count);
        Assert.Single(deck.DiscardPile);
    }

    [Fact]
    public void DrawRandomCardFromDiscardEvent_partial_when_insufficient()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = CreateDeckEntity(entityManager);
        deck.DiscardPile.Add(CreateCard(entityManager));
        deck.DiscardPile.Add(CreateCard(entityManager));
        _ = new DeckManagementSystem(entityManager);

        EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = 5 });

        Assert.Equal(2, deck.Hand.Count);
        Assert.Empty(deck.DiscardPile);
    }

    [Fact]
    public void DrawRandomCardFromDiscardEvent_noop_when_empty()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = CreateDeckEntity(entityManager);
        _ = new DeckManagementSystem(entityManager);

        EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = 3 });

        Assert.Empty(deck.Hand);
        Assert.Empty(deck.DiscardPile);
    }

    [Fact]
    public void DrawCard_publishes_DrawPileEmptyEvent_when_empty_and_unrescued()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = CreateDeckEntity(entityManager);
        var system = new DeckManagementSystem(entityManager);

        int emptyEvents = 0;
        Entity eventDeck = null;
        EventManager.Subscribe<DrawPileEmptyEvent>(evt =>
        {
            emptyEvents++;
            eventDeck = evt.Deck;
        });

        bool drawn = system.DrawCard(deck);

        Assert.False(drawn);
        Assert.Equal(1, emptyEvents);
        Assert.Same(deck.Owner, eventDeck);
        Assert.Empty(deck.Hand);
    }

    [Fact]
    public void DrawCard_retries_after_DrawPileEmptyEvent_refills_draw_pile()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = CreateDeckEntity(entityManager);
        var card = CreateCard(entityManager);
        deck.DiscardPile.Add(card);
        var system = new DeckManagementSystem(entityManager);
        EventManager.Subscribe<DrawPileEmptyEvent>(evt =>
        {
            EventManager.Publish(new ShuffleRandomCardsFromDiscardToDrawPileEvent
            {
                Deck = evt.Deck,
                Amount = 1
            });
        });

        bool drawn = system.DrawCard(deck);

        Assert.True(drawn);
        Assert.Contains(card, deck.Hand);
        Assert.DoesNotContain(card, deck.DiscardPile);
        Assert.Empty(deck.DrawPile);
    }

    [Fact]
    public void ShuffleRandomCardsFromDiscardToDrawPileEvent_moves_cards_to_draw_pile_for_specified_deck()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var (targetDeckEntity, targetDeck) = CreateDeckWithEntity(entityManager);
        var (_, otherDeck) = CreateDeckWithEntity(entityManager);
        targetDeck.DiscardPile.Add(CreateCard(entityManager));
        targetDeck.DiscardPile.Add(CreateCard(entityManager));
        otherDeck.DiscardPile.Add(CreateCard(entityManager));
        _ = new DeckManagementSystem(entityManager);

        EventManager.Publish(new ShuffleRandomCardsFromDiscardToDrawPileEvent
        {
            Deck = targetDeckEntity,
            Amount = 2
        });

        Assert.Equal(2, targetDeck.DrawPile.Count);
        Assert.Empty(targetDeck.DiscardPile);
        Assert.Empty(otherDeck.DrawPile);
        Assert.Single(otherDeck.DiscardPile);
    }

    [Fact]
    public void ShuffleRandomCardsFromDiscardToDrawPileEvent_partial_when_insufficient()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = CreateDeckEntity(entityManager);
        deck.DiscardPile.Add(CreateCard(entityManager));
        deck.DiscardPile.Add(CreateCard(entityManager));
        _ = new DeckManagementSystem(entityManager);

        EventManager.Publish(new ShuffleRandomCardsFromDiscardToDrawPileEvent { Deck = deck.Owner, Amount = 5 });

        Assert.Equal(2, deck.DrawPile.Count);
        Assert.Empty(deck.DiscardPile);
    }

    [Fact]
    public void ShuffleRandomCardsFromDiscardToDrawPileEvent_skips_weapons()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = CreateDeckEntity(entityManager);
        var weapon = CreateWeaponCard(entityManager);
        var cardA = CreateCard(entityManager);
        var cardB = CreateCard(entityManager);
        deck.DiscardPile.Add(weapon);
        deck.DiscardPile.Add(cardA);
        deck.DiscardPile.Add(cardB);
        _ = new DeckManagementSystem(entityManager);

        EventManager.Publish(new ShuffleRandomCardsFromDiscardToDrawPileEvent { Deck = deck.Owner, Amount = 2 });

        Assert.Equal(2, deck.DrawPile.Count);
        Assert.Contains(cardA, deck.DrawPile);
        Assert.Contains(cardB, deck.DrawPile);
        Assert.DoesNotContain(weapon, deck.DrawPile);
        Assert.Single(deck.DiscardPile);
        Assert.Contains(weapon, deck.DiscardPile);
    }

    [Fact]
    public void ShuffleRandomCardsFromDiscardToDrawPileEvent_noop_when_discard_empty()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        var deck = CreateDeckEntity(entityManager);
        _ = new DeckManagementSystem(entityManager);

        EventManager.Publish(new ShuffleRandomCardsFromDiscardToDrawPileEvent { Deck = deck.Owner, Amount = 3 });

        Assert.Empty(deck.DrawPile);
        Assert.Empty(deck.DiscardPile);
    }

    private static Deck CreateDeckEntity(EntityManager entityManager)
    {
        return CreateDeckWithEntity(entityManager).Deck;
    }

    private static (Entity Entity, Deck Deck) CreateDeckWithEntity(EntityManager entityManager)
    {
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        return (deckEntity, deck);
    }

    private static Entity CreateCard(EntityManager entityManager)
    {
        var entity = entityManager.CreateEntity("tempest");
        entityManager.AddComponent(entity, new CardData { Card = new Tempest() });
        entityManager.AddComponent(entity, new Transform { Position = Vector2.Zero });
        entityManager.AddComponent(entity, new UIElement { Bounds = new Rectangle(-1000, -1000, 1, 1) });
        return entity;
    }

    private static Entity CreateWeaponCard(EntityManager entityManager)
    {
        var entity = entityManager.CreateEntity("weapon");
        entityManager.AddComponent(entity, new CardData { Card = new CardBase { IsWeapon = true } });
        return entity;
    }
}
