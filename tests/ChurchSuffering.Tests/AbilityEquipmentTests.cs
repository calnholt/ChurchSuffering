using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.Equipment;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class AbilityEquipmentTests : IDisposable
{
    public AbilityEquipmentTests()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    public static TheoryData<string> AbilityEquipmentIds => new()
    {
        "warbringer_bracers",
        "heartforge_cuirass",
        "fleetfoot_greaves",
        "oathbreaker_coif",
        "kunai_sheath",
        "whetstone_gauntlets",
        "sanctified_circlet",
        "bulwark_plate",
        "sunderstep_treads",
    };

    [Theory]
    [MemberData(nameof(AbilityEquipmentIds))]
    public void Ability_equipment_has_activation_only_metadata(string equipmentId)
    {
        var equipment = EquipmentFactory.Create(equipmentId);

        Assert.NotNull(equipment);
        Assert.Equal(0, equipment.Block);
        Assert.True(equipment.CanActivateDuringActionPhase);
        Assert.True(string.IsNullOrWhiteSpace(equipment.FlavorText));
    }

    [Fact]
    public void Oathbreaker_coif_removes_prior_turn_pledge_on_activation()
    {
        var entityManager = BuildActionBattle(out var player);
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        var pledgedCard = entityManager.CreateEntity("PledgedCard");
        entityManager.AddComponent(pledgedCard, new CardData { Card = new CardBase() });
        entityManager.AddComponent(pledgedCard, new Pledge { Owner = pledgedCard, CanPlay = true });
        deck.Hand.Add(pledgedCard);

        var equipmentEntity = AddEquipment(entityManager, player, "oathbreaker_coif");
        _ = new PledgeManagementSystem(entityManager);
        _ = new EquipmentManagerSystem(entityManager);

        EventManager.Publish(new EquipmentActivateEvent { EquipmentEntity = equipmentEntity });

        Assert.False(pledgedCard.HasComponent<Pledge>());
        Assert.True(equipmentEntity.GetComponent<EquippedEquipment>().Equipment.IsUsed);
    }

    [Fact]
    public void Oathbreaker_coif_cannot_activate_without_prior_turn_pledge()
    {
        var entityManager = BuildActionBattle(out var player);
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        var pledgedCard = entityManager.CreateEntity("PledgedCard");
        entityManager.AddComponent(pledgedCard, new CardData { Card = new CardBase() });
        entityManager.AddComponent(pledgedCard, new Pledge { Owner = pledgedCard, CanPlay = false });
        deck.Hand.Add(pledgedCard);

        var equipmentEntity = AddEquipment(entityManager, player, "oathbreaker_coif");
        var equipment = equipmentEntity.GetComponent<EquippedEquipment>().Equipment as OathbreakerCoif;
        equipment.Initialize(entityManager, equipmentEntity);

        Assert.False(equipment.CanActivate());
    }

    [Fact]
    public void Kunai_sheath_adds_kunai_to_hand_on_activation()
    {
        var entityManager = BuildActionBattle(out var player);
        var deckEntity = entityManager.CreateEntity("Deck");
        entityManager.AddComponent(deckEntity, new Deck());
        var equipmentEntity = AddEquipment(entityManager, player, "kunai_sheath");
        _ = new CardZoneSystem(entityManager);
        _ = new EquipmentManagerSystem(entityManager);

        EventManager.Publish(new EquipmentActivateEvent { EquipmentEntity = equipmentEntity });

        var deck = deckEntity.GetComponent<Deck>();
        Assert.Single(deck.Hand);
        Assert.Equal("kunai", deck.Hand[0].GetComponent<CardData>().Card.CardId);
    }

    [Fact]
    public void Sanctified_circlet_grants_temperance_on_activation()
    {
        var entityManager = BuildActionBattle(out var player);
        entityManager.AddComponent(player, new Temperance { Amount = 1 });
        var equipmentEntity = AddEquipment(entityManager, player, "sanctified_circlet");
        _ = new TemperanceManagerSystem(entityManager);
        _ = new EquipmentManagerSystem(entityManager);

        EventManager.Publish(new EquipmentActivateEvent { EquipmentEntity = equipmentEntity });

        Assert.Equal(3, player.GetComponent<Temperance>().Amount);
    }

    [Fact]
    public void Bulwark_plate_grants_aegis_on_activation()
    {
        var entityManager = BuildActionBattle(out var player);
        entityManager.AddComponent(player, new AppliedPassives());
        var equipmentEntity = AddEquipment(entityManager, player, "bulwark_plate");
        _ = new AppliedPassivesManagementSystem(entityManager);
        _ = new EquipmentManagerSystem(entityManager);

        EventManager.Publish(new EquipmentActivateEvent { EquipmentEntity = equipmentEntity });

        Assert.Equal(2, player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Aegis]);
    }

    [Fact]
    public void Sunderstep_treads_removes_enemy_guard_on_activation()
    {
        var entityManager = BuildActionBattle(out var player);
        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());
        entityManager.AddComponent(enemy, new AppliedPassives());
        var equipmentEntity = AddEquipment(entityManager, player, "sunderstep_treads");
        _ = new AppliedPassivesManagementSystem(entityManager);
        _ = new EquipmentManagerSystem(entityManager);

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = enemy,
            Type = AppliedPassiveType.Guard,
            Delta = 4
        });
        Assert.Equal(4, enemy.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Guard]);

        EventManager.Publish(new EquipmentActivateEvent { EquipmentEntity = equipmentEntity });

        Assert.False(enemy.GetComponent<AppliedPassives>().Passives.ContainsKey(AppliedPassiveType.Guard));
    }

    private static EntityManager BuildActionBattle(out Entity player)
    {
        var entityManager = new EntityManager();
        var phase = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phase, new PhaseState
        {
            Main = MainPhase.PlayerTurn,
            Sub = SubPhase.Action,
        });
        player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        return entityManager;
    }

    private static Entity AddEquipment(EntityManager entityManager, Entity player, string equipmentId)
    {
        var entity = entityManager.CreateEntity($"Equipment_{equipmentId}");
        var equipment = EquipmentFactory.Create(equipmentId);
        equipment.Initialize(entityManager, entity);
        entityManager.AddComponent(entity, new EquippedEquipment
        {
            EquippedOwner = player,
            Equipment = equipment,
        });
        return entity;
    }
}
