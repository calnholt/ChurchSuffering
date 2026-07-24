using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.Medals;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Objects.Medals;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class StJosephMedalTests : IDisposable
{
	public StJosephMedalTests()
	{
		EventManager.Clear();
		EventQueue.Clear();
		StateSingleton.IsActive = false;
		StateSingleton.PreventClicking = false;
		StateSingleton.IsTutorialActive = false;
		StateSingleton.IsPledgeEnabled = true;
	}

	public void Dispose()
	{
		EventManager.Clear();
		EventQueue.Clear();
		StateSingleton.IsActive = false;
		StateSingleton.PreventClicking = false;
		StateSingleton.IsTutorialActive = false;
		StateSingleton.IsPledgeEnabled = true;
	}

	[Fact]
	public void Medal_catalogs_include_st_joseph()
	{
		var medal = Assert.IsType<StJoseph>(MedalFactory.Create("st_joseph"));

		Assert.Equal("St. Joseph", medal.Name);
		Assert.Equal(
			"You can block with your pledged card. You can pledge block cards.",
			medal.Text);
		Assert.Contains(MedalId.StJoseph, MedalFactory.GetAllMedals().Keys);
		Assert.Equal("st_joseph", MedalId.StJoseph.ToKey());
		Assert.True(GuardianAngelMessageService.HasMedalMessages(MedalId.StJoseph));
		Assert.Equal(2, GuardianAngelMessageService.GetMedalMessages(MedalId.StJoseph).Count);
		Assert.True(SaintBlurbDefinitionCache.TryGet("st_joseph", out var blurb));
		Assert.Equal(2, blurb.bioParagraphs.Count);
	}

	[Fact]
	public void Block_card_can_be_pledged_and_used_to_block_with_st_joseph()
	{
		var entityManager = BuildBlockBattle(
			out var player,
			out var deck,
			out _,
			out var plannedAttack);
		var phase = entityManager.GetEntitiesWithComponent<PhaseState>()
			.First()
			.GetComponent<PhaseState>();
		phase.Main = MainPhase.PlayerTurn;
		phase.Sub = SubPhase.Action;
		var blockCard = AddCard(entityManager, deck, new CardBase
		{
			Type = CardType.Block,
			Block = 3,
		});

		var withoutMedal = PledgeAvailabilityService.Evaluate(entityManager);

		Assert.False(withoutMedal.IsAvailable);
		Assert.Equal(PledgeAvailabilityFailure.NoEligibleCard, withoutMedal.Failure);
		Assert.Equal(
			PledgeCardEligibilityFailure.Block,
			PledgeAvailabilityService.EvaluateCard(entityManager, blockCard).Failure);

		EquipMedal(entityManager, player, new StJoseph());
		_ = new PledgeManagementSystem(entityManager);

		Assert.True(PledgeAvailabilityService.IsAvailable(entityManager));

		EventManager.Publish(new PledgeCardRequested { Card = blockCard });

		Assert.True(blockCard.HasComponent<Pledge>());
		phase.Main = MainPhase.EnemyTurn;
		phase.Sub = SubPhase.Block;
		Assert.True(EnemyBlockerEligibilityService.IsEligibleHandBlocker(
			entityManager,
			blockCard,
			plannedAttack));
	}

	[Fact]
	public void St_joseph_only_overrides_the_block_card_pledge_restriction()
	{
		var entityManager = new EntityManager();
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		EquipMedal(entityManager, player, new StJoseph());

		var sealedBlock = CreateCard(entityManager, new CardBase { Type = CardType.Block });
		entityManager.AddComponent(sealedBlock, new Sealed());
		var weaponBlock = CreateCard(entityManager, new CardBase
		{
			Type = CardType.Block,
			IsWeapon = true,
		});
		var relic = CreateCard(entityManager, new CardBase { Type = CardType.Relic });
		var tokenBlock = CreateCard(entityManager, new CardBase
		{
			Type = CardType.Block,
			IsToken = true,
		});

		Assert.Equal(
			PledgeCardEligibilityFailure.Sealed,
			PledgeAvailabilityService.EvaluateCard(entityManager, sealedBlock).Failure);
		Assert.Equal(
			PledgeCardEligibilityFailure.Weapon,
			PledgeAvailabilityService.EvaluateCard(entityManager, weaponBlock).Failure);
		Assert.Equal(
			PledgeCardEligibilityFailure.Relic,
			PledgeAvailabilityService.EvaluateCard(entityManager, relic).Failure);
		Assert.Equal(
			PledgeCardEligibilityFailure.Token,
			PledgeAvailabilityService.EvaluateCard(entityManager, tokenBlock).Failure);
	}

	[Fact]
	public void Pledged_card_is_eligible_only_when_an_equipped_provider_overrides_the_rule()
	{
		var entityManager = BuildBlockBattle(out var player, out var deck, out _, out var plannedAttack);
		var pledgedCard = AddPledgedCard(entityManager, deck);

		var withoutMedal = EnemyBlockerEligibilityService.EvaluateHandBlocker(
			entityManager,
			pledgedCard,
			plannedAttack);

		Assert.False(withoutMedal.IsEligible);
		Assert.Equal(HandBlockEligibilityFailure.Pledged, withoutMedal.Failure);
		Assert.Equal(0, EnemyBlockerEligibilityService.CountEligibleBlockers(entityManager, plannedAttack));

		EquipMedal(entityManager, player, new StJoseph());

		Assert.True(EnemyBlockerEligibilityService.IsEligibleHandBlocker(
			entityManager,
			pledgedCard,
			plannedAttack));
		Assert.Equal(1, EnemyBlockerEligibilityService.CountEligibleBlockers(entityManager, plannedAttack));
	}

	[Fact]
	public void St_joseph_does_not_override_other_blocking_restrictions()
	{
		var entityManager = BuildBlockBattle(out var player, out var deck, out _, out var plannedAttack);
		var pledgedCard = AddPledgedCard(entityManager, deck);
		EquipMedal(entityManager, player, new StJoseph());
		entityManager.AddComponent(pledgedCard, new Intimidated());

		var result = EnemyBlockerEligibilityService.EvaluateHandBlocker(
			entityManager,
			pledgedCard,
			plannedAttack);

		Assert.False(result.IsEligible);
		Assert.Equal(HandBlockEligibilityFailure.Intimidated, result.Failure);
	}

	[Fact]
	public void Assignment_and_unassignment_preserve_the_pledge()
	{
		var entityManager = BuildBlockBattle(out var player, out var deck, out var deckEntity, out _);
		var pledgedCard = AddPledgedCard(entityManager, deck);
		EquipMedal(entityManager, player, new StJoseph());
		_ = new CardZoneSystem(entityManager);
		_ = new PledgeManagementSystem(entityManager);
		_ = new AssignedBlockLifecycleSystem(entityManager);
		_ = new HandBlockInteractionSystem(entityManager);

		EventManager.Publish(new AssignCardAsBlockRequested { Card = pledgedCard });

		Assert.True(pledgedCard.HasComponent<Pledge>());
		Assert.True(pledgedCard.HasComponent<AssignedBlockCard>());
		Assert.DoesNotContain(pledgedCard, deck.Hand);

		EventManager.Publish(new UnassignCardAsBlockRequested { CardEntity = pledgedCard });
		EventManager.Publish(new AssignedBlockReturnCompleted { Card = pledgedCard });

		Assert.True(pledgedCard.HasComponent<Pledge>());
		Assert.False(pledgedCard.HasComponent<AssignedBlockCard>());
		Assert.Contains(pledgedCard, deckEntity.GetComponent<Deck>().Hand);
	}

	[Fact]
	public void Confirmed_block_resolution_clears_pledge_when_card_reaches_discard()
	{
		var entityManager = BuildBlockBattle(out var player, out var deck, out _, out _);
		var pledgedCard = AddPledgedCard(entityManager, deck);
		EquipMedal(entityManager, player, new StJoseph());
		_ = new CardZoneSystem(entityManager);
		_ = new PledgeManagementSystem(entityManager);
		_ = new HandBlockInteractionSystem(entityManager);

		EventManager.Publish(new AssignCardAsBlockRequested { Card = pledgedCard });
		entityManager.GetEntitiesWithComponent<PhaseState>().First().GetComponent<PhaseState>().Sub = SubPhase.EnemyAttack;
		QueuedDiscardAssignedBlocksEvent.ResolveImmediately(entityManager, discardSpentBlocks: true);

		Assert.Contains(pledgedCard, deck.DiscardPile);
		Assert.False(pledgedCard.HasComponent<Pledge>());
		Assert.False(pledgedCard.HasComponent<AssignedBlockCard>());
	}

	[Theory]
	[InlineData(CardZoneType.DiscardPile)]
	[InlineData(CardZoneType.DrawPile)]
	[InlineData(CardZoneType.ExhaustPile)]
	public void Any_final_pile_arrival_clears_pledge(CardZoneType destination)
	{
		var entityManager = new EntityManager();
		var card = entityManager.CreateEntity("PledgedAssignedBlock");
		entityManager.AddComponent(card, new Pledge { Owner = card, CanPlay = false });
		_ = new PledgeManagementSystem(entityManager);

		EventManager.Publish(new CardMoved
		{
			Card = card,
			From = CardZoneType.AssignedBlock,
			To = destination,
		});

		Assert.False(card.HasComponent<Pledge>());
	}

	private static EntityManager BuildBlockBattle(
		out Entity player,
		out Deck deck,
		out Entity deckEntity,
		out PlannedAttack plannedAttack)
	{
		var entityManager = new EntityManager();
		var phaseEntity = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phaseEntity, new PhaseState
		{
			Main = MainPhase.EnemyTurn,
			Sub = SubPhase.Block,
		});

		deckEntity = entityManager.CreateEntity("Deck");
		deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);

		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player { DeckEntity = deckEntity });

		plannedAttack = new PlannedAttack { AttackDefinition = new EnemyAttackBase() };
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned = [plannedAttack],
		});

		return entityManager;
	}

	private static Entity AddPledgedCard(EntityManager entityManager, Deck deck)
	{
		var card = AddCard(entityManager, deck, new CardBase { Block = 3 });
		entityManager.AddComponent(card, new Pledge { Owner = card, CanPlay = false });
		entityManager.AddComponent(card, new UIElement { IsInteractable = true });
		entityManager.AddComponent(card, new Transform { Position = new Vector2(100f, 200f) });
		return card;
	}

	private static Entity AddCard(
		EntityManager entityManager,
		Deck deck,
		CardBase definition)
	{
		var card = CreateCard(entityManager, definition);
		deck.Hand.Add(card);
		return card;
	}

	private static Entity CreateCard(EntityManager entityManager, CardBase definition)
	{
		var card = entityManager.CreateEntity("Card");
		entityManager.AddComponent(card, new CardData
		{
			Card = definition,
			Color = CardData.CardColor.White,
		});
		return card;
	}

	private static Entity EquipMedal(EntityManager entityManager, Entity player, MedalBase medal)
	{
		var medalEntity = entityManager.CreateEntity($"Medal_{medal.Id}");
		medal.Initialize(entityManager, medalEntity);
		entityManager.AddComponent(medalEntity, new EquippedMedal
		{
			EquippedOwner = player,
			Medal = medal,
		});
		return medalEntity;
	}
}
