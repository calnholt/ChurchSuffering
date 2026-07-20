using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Medals;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardStatModifierServiceTests : IDisposable
{
    public CardStatModifierServiceTests()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    [Fact]
    public void Stored_damage_and_block_modifiers_are_included()
    {
        var entityManager = BuildCombatWorld(out var player, out _);
        var strike = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.White);
        var block = EntityFactory.CreateCardFromDefinition(entityManager, "shield_of_faith", CardData.CardColor.White);
        AttackDamageValueService.ApplyDelta(strike, 2, "Test damage");
        BlockValueService.ApplyDelta(block, 3, "Test block");

        Assert.Equal(strike.GetComponent<CardData>().Card.Damage + 2, CardStatModifierService.GetCardDamage(entityManager, strike).TotalValue);
        Assert.Equal(block.GetComponent<CardData>().Card.Block + 3, CardStatModifierService.GetCardBlock(entityManager, block).TotalValue);
    }

    [Fact]
    public void Colorless_black_card_ignores_only_intrinsic_black_block_bonus()
    {
        var entityManager = BuildCombatWorld(out _, out _);
        var card = EntityFactory.CreateCardFromDefinition(entityManager, "shield_of_faith", CardData.CardColor.Black);
        BlockValueService.ApplyDelta(card, 2, "Test bonus");

        Assert.Equal(card.GetComponent<CardData>().Card.Block + 3, CardStatModifierService.GetCardBlock(entityManager, card).TotalValue);

        entityManager.AddComponent(card, new Colorless { Owner = card });

        Assert.Equal(card.GetComponent<CardData>().Card.Block + 2, CardStatModifierService.GetCardBlock(entityManager, card).TotalValue);
    }

    [Fact]
    public void StChristopher_adds_block_to_brittle_cards_only()
    {
        var entityManager = BuildCombatWorld(out var player, out _);
        EquipMedal(entityManager, player, new StChristopher());
        var brittle = EntityFactory.CreateCardFromDefinition(entityManager, "shield_of_faith", CardData.CardColor.White);
        var normal = EntityFactory.CreateCardFromDefinition(entityManager, "shield_of_faith", CardData.CardColor.White);
        entityManager.AddComponent(brittle, new Brittle { Owner = brittle });

        Assert.Equal(brittle.GetComponent<CardData>().Card.Block + 1, CardStatModifierService.GetCardBlock(entityManager, brittle).TotalValue);
        Assert.Equal(normal.GetComponent<CardData>().Card.Block, CardStatModifierService.GetCardBlock(entityManager, normal).TotalValue);
    }

    [Fact]
    public void StLawrence_applies_scorched_payment_bonus_only_during_resolution()
    {
        var entityManager = BuildCombatWorld(out var player, out _);
        EquipMedal(entityManager, player, new StLawrence());
        var card = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.Red);
        entityManager.AddComponent(card, new Scorched { Owner = card });
        var payment1 = entityManager.CreateEntity("Payment1");
        var payment2 = entityManager.CreateEntity("Payment2");

        int baseDamage = card.GetComponent<CardData>().Card.Damage;
        Assert.Equal(baseDamage, CardStatModifierService.GetCardDamage(entityManager, card, CardStatQueryMode.Preview).TotalValue);

        entityManager.AddComponent(card, new CardPlayStatContext
        {
            Owner = card,
            PaymentCards = [payment1, payment2],
        });

        Assert.Equal(baseDamage + 2, CardStatModifierService.GetCardDamage(entityManager, card, CardStatQueryMode.Resolution).TotalValue);
    }

    [Fact]
    public void Autopay_passes_payment_cards_to_resolution_stat_context()
    {
        var entityManager = BuildActionBattle(out var player, out var deck);
        EquipMedal(entityManager, player, new StLawrence());
        var playedCard = CreateTrackingAttackCard(entityManager, "Scorched Attack", 5, ["Any"], out var resolvedDamage);
        entityManager.AddComponent(playedCard, new Scorched { Owner = playedCard });
        var paymentCard = CreatePaymentCard(entityManager, "Payment");
        deck.Cards.Add(playedCard);
        deck.Hand.Add(playedCard);
        deck.Cards.Add(paymentCard);
        deck.Hand.Add(paymentCard);
        _ = new CardPlaySystem(entityManager);

        EventManager.Publish(new PlayCardRequested { Card = playedCard });

        Assert.Equal(6, resolvedDamage.Value);
    }

    [Fact]
    public void StAdrian_adds_base_damage_per_hit_only_while_armed()
    {
        var entityManager = BuildCombatWorld(out var player, out _);
        var medal = new StAdrian { ProcChancePercent = 100 };
        EquipMedal(entityManager, player, medal);

        var armed = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.Red);
        var other = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.Red);
        int baseDamage = armed.GetComponent<CardData>().Card.Damage;

        Assert.Equal(baseDamage, CardStatModifierService.GetCardDamage(entityManager, armed, CardStatQueryMode.Preview).TotalValue);
        Assert.Equal(baseDamage, CardStatModifierService.GetCardDamage(entityManager, armed, CardStatQueryMode.Resolution).TotalValue);

        EventManager.Publish(new PlayCardRequested { Card = armed, CostsPaid = true });

        Assert.Equal(baseDamage, CardStatModifierService.GetCardDamage(entityManager, armed, CardStatQueryMode.Preview).TotalValue);
        Assert.Equal(baseDamage + 2, CardStatModifierService.GetCardDamage(entityManager, armed, CardStatQueryMode.Resolution).TotalValue);
        Assert.Equal(baseDamage + 2, CardStatModifierService.GetCardDamage(entityManager, armed, CardStatQueryMode.Resolution).TotalValue);
        Assert.Equal(baseDamage, CardStatModifierService.GetCardDamage(entityManager, other, CardStatQueryMode.Resolution).TotalValue);
    }

    private static EntityManager BuildCombatWorld(out Entity player, out Entity enemy)
    {
        var entityManager = new EntityManager();
        player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new AppliedPassives());

        enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());
        entityManager.AddComponent(enemy, new AppliedPassives());
        return entityManager;
    }

    private static EntityManager BuildActionBattle(out Entity player, out Deck deck)
    {
        var entityManager = new EntityManager();
        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = MainPhase.PlayerTurn,
            Sub = SubPhase.Action,
        });

        var deckEntity = entityManager.CreateEntity("Deck");
        deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player { DeckEntity = deckEntity });
        entityManager.AddComponent(player, new ActionPoints { Current = 1 });
        entityManager.AddComponent(player, new Courage());
        entityManager.AddComponent(player, new AppliedPassives());

        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());
        entityManager.AddComponent(enemy, new AppliedPassives());

        var tutorial = entityManager.CreateEntity("GuidedTutorial");
        entityManager.AddComponent(tutorial, new GuidedTutorial { Section = 1 });

        return entityManager;
    }

    private static void EquipMedal(EntityManager entityManager, Entity player, MedalBase medal)
    {
        var medalEntity = entityManager.CreateEntity($"Medal_{medal.Id}");
        entityManager.AddComponent(medalEntity, new EquippedMedal
        {
            EquippedOwner = player,
            Medal = medal,
        });
        medal.Initialize(entityManager, medalEntity);
    }

    private static Entity CreateTrackingAttackCard(
        EntityManager entityManager,
        string name,
        int damage,
        List<string> cost,
        out CapturedInt resolvedDamage)
    {
        resolvedDamage = new CapturedInt();
        var definition = new TrackingAttackCard(name, damage, cost, resolvedDamage);
        var entity = entityManager.CreateEntity(name);
        entityManager.AddComponent(entity, new CardData
        {
            Card = definition,
            Color = CardData.CardColor.Red,
            Owner = entity,
        });
        entityManager.AddComponent(entity, new ModifiedDamage());
        entityManager.AddComponent(entity, new ModifiedBlock());
        definition.Initialize(entityManager, entity);
        return entity;
    }

    private static Entity CreatePaymentCard(EntityManager entityManager, string name)
    {
        var entity = entityManager.CreateEntity(name);
        var definition = new CardBase
        {
            CardId = "payment",
            Name = name,
            Type = CardType.Prayer,
        };
        entityManager.AddComponent(entity, new CardData
        {
            Card = definition,
            Color = CardData.CardColor.White,
            Owner = entity,
        });
        entityManager.AddComponent(entity, new ModifiedBlock());
        definition.Initialize(entityManager, entity);
        return entity;
    }

    private sealed class CapturedInt
    {
        public int Value { get; set; }
    }

    private sealed class TrackingAttackCard : CardBase
    {
        private readonly CapturedInt _resolvedDamage;

        public TrackingAttackCard(string name, int damage, List<string> cost, CapturedInt resolvedDamage)
        {
            CardId = "tracking_attack";
            Name = name;
            Damage = damage;
            Cost = cost;
            Type = CardType.Attack;
            _resolvedDamage = resolvedDamage;
            OnPlay = (entityManager, card) =>
            {
                _resolvedDamage.Value = GetDerivedDamage(entityManager, card);
            };
        }
    }
}
