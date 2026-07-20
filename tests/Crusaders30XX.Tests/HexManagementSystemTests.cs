using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class HexManagementSystemTests
{
    [Fact]
    public void Hex_covers_a_card_and_preserves_its_hover_preview()
    {
        var entityManager = new EntityManager();
        var card = EntityFactory.CreateCardFromDefinition(
            entityManager,
            "tempest",
            CardData.CardColor.Black,
            isUpgraded: true);

        HexManagementSystem.ApplyHexRuntime(entityManager, card);

        Assert.True(card.HasComponent<Hexed>());
        Assert.Equal(Hex.CardIdValue, card.GetComponent<CardData>()?.Card?.CardId);
        Assert.Equal("tempest", card.GetComponent<CursedOriginalCard>()?.CardId);
        Assert.Equal(CardData.CardColor.Black, card.GetComponent<CursedOriginalCard>()?.Color);
        Assert.Equal("tempest", card.GetComponent<CardTooltip>()?.CardId);
    }

    [Fact]
    public void Unplayed_hex_restores_the_original_card_at_player_end_even_from_discard()
    {
        EventManager.Clear();
        try
        {
            var entityManager = new EntityManager();
            var deckEntity = entityManager.CreateEntity("Deck");
            var deck = new Deck();
            entityManager.AddComponent(deckEntity, deck);
            var card = EntityFactory.CreateCardFromDefinition(entityManager, "tempest", CardData.CardColor.White);
            deck.Cards.Add(card);
            deck.DiscardPile.Add(card);
            HexManagementSystem.ApplyHexRuntime(entityManager, card);
            _ = new HexManagementSystem(entityManager);

            EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PlayerEnd });

            Assert.False(card.HasComponent<Hexed>());
            Assert.False(card.HasComponent<CursedOriginalCard>());
            Assert.Equal("tempest", card.GetComponent<CardData>()?.Card?.CardId);
        }
        finally
        {
            EventManager.Clear();
        }
    }

    [Fact]
    public void Playing_hex_converts_the_covered_card_to_curse()
    {
        EventManager.Clear();
        try
        {
            var entityManager = new EntityManager();
            var card = EntityFactory.CreateCardFromDefinition(entityManager, "tempest", CardData.CardColor.Red);
            HexManagementSystem.ApplyHexRuntime(entityManager, card);
            _ = new HexManagementSystem(entityManager);

            EventManager.Publish(new CardPlayedEvent { Card = card });

            Assert.False(card.HasComponent<Hexed>());
            Assert.True(card.HasComponent<Cursed>());
            Assert.Equal(Curse.CardIdValue, card.GetComponent<CardData>()?.Card?.CardId);
            Assert.Equal("tempest", card.GetComponent<CursedOriginalCard>()?.CardId);
        }
        finally
        {
            EventManager.Clear();
        }
    }

    [Fact]
    public void Hex_deals_five_derived_attack_damage()
    {
        EventManager.Clear();
        try
        {
            var entityManager = new EntityManager();
            var player = entityManager.CreateEntity("Player");
            var enemy = entityManager.CreateEntity("Enemy");
            var card = entityManager.CreateEntity("Hex");
            entityManager.AddComponent(card, new CardData { Card = new Hex() });
            ModifyHpRequestEvent damage = null;
            EventManager.Subscribe<ModifyHpRequestEvent>(evt => damage = evt);

            card.GetComponent<CardData>().Card.OnPlay(entityManager, card);

            Assert.NotNull(damage);
            Assert.Equal(player, damage.Source);
            Assert.Equal(enemy, damage.Target);
            Assert.Equal(-5, damage.Delta);
            Assert.Equal(card, damage.AttackCard);
        }
        finally
        {
            EventManager.Clear();
        }
    }

    [Fact]
    public void Hex_is_factory_creatable_but_not_in_card_pool()
    {
        var hex = Assert.IsType<Hex>(CardFactory.Create(Hex.CardIdValue));

        Assert.Equal(5, hex.Damage);
        Assert.Equal(3, hex.Block);
        Assert.True(hex.IsFreeAction);
        Assert.False(hex.CanAddToLoadout);
        Assert.DoesNotContain(CardId.Hex, CardFactory.GetAllCards().Keys);
    }
}
