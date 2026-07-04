using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardStatusManagementSystemTests : System.IDisposable
{
	public CardStatusManagementSystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Brittle_sole_blocker_publishes_mill_event()
	{
		var entityManager = new EntityManager();
		AddCurrentAttackProgress(entityManager, playedCards: 1);
		var brittleCard = CreateCard(entityManager, "BrittleCard");
		entityManager.AddComponent(brittleCard, new Brittle { Owner = brittleCard });
		entityManager.AddComponent(brittleCard, new AssignedBlockCard());
		var normalCard = CreateCard(entityManager, "NormalCard");
		entityManager.AddComponent(normalCard, new AssignedBlockCard());
		_ = new BrittleManagementSystem(entityManager);
		int millEvents = 0;
		EventManager.Subscribe<MillCardEvent>(_ => millEvents++);

		EventManager.Publish(new CardBlockedEvent { Card = normalCard });
		EventManager.Publish(new CardBlockedEvent { Card = brittleCard });

		Assert.Equal(1, millEvents);
	}

	[Fact]
	public void Brittle_noops_when_card_is_not_brittle_or_not_sole_blocker()
	{
		var entityManager = new EntityManager();
		AddCurrentAttackProgress(entityManager, playedCards: 2);
		var brittleCard = CreateCard(entityManager, "BrittleCard");
		entityManager.AddComponent(brittleCard, new Brittle { Owner = brittleCard });
		entityManager.AddComponent(brittleCard, new AssignedBlockCard());
		var normalCard = CreateCard(entityManager, "NormalCard");
		entityManager.AddComponent(normalCard, new AssignedBlockCard());
		_ = new BrittleManagementSystem(entityManager);
		int millEvents = 0;
		EventManager.Subscribe<MillCardEvent>(_ => millEvents++);

		EventManager.Publish(new CardBlockedEvent { Card = normalCard });
		EventManager.Publish(new CardBlockedEvent { Card = brittleCard });

		Assert.Equal(0, millEvents);
	}

	[Fact]
	public void Scorched_pledged_card_publishes_player_hp_loss()
	{
		var entityManager = new EntityManager();
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		var scorchedCard = CreateCard(entityManager, "ScorchedCard");
		entityManager.AddComponent(scorchedCard, new Scorched { Owner = scorchedCard });
		var normalCard = CreateCard(entityManager, "NormalCard");
		_ = new ScorchedManagementSystem(entityManager);
		ModifyHpRequestEvent hpEvent = null;
		EventManager.Subscribe<ModifyHpRequestEvent>(evt => hpEvent = evt);

		EventManager.Publish(new PledgeAddedEvent { Card = normalCard });
		Assert.Null(hpEvent);

		EventManager.Publish(new PledgeAddedEvent { Card = scorchedCard });

		Assert.NotNull(hpEvent);
		Assert.Same(player, hpEvent.Source);
		Assert.Same(player, hpEvent.Target);
		Assert.Equal(-1, hpEvent.Delta);
		Assert.Equal(ModifyTypeEnum.Effect, hpEvent.DamageType);
		Assert.False(hpEvent.IgnoresAegis);
	}

	[Fact]
	public void Thorned_cost_discarded_card_publishes_player_scar()
	{
		var entityManager = new EntityManager();
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		var thornedCard = CreateCard(entityManager, "ThornedCard");
		entityManager.AddComponent(thornedCard, new Thorned { Owner = thornedCard });
		var normalCard = CreateCard(entityManager, "NormalCard");
		_ = new ThornedManagementSystem(entityManager);
		ApplyPassiveEvent passiveEvent = null;
		EventManager.Subscribe<ApplyPassiveEvent>(evt => passiveEvent = evt);

		EventManager.Publish(new CardDiscardedForCostEvent { Card = normalCard });
		Assert.Null(passiveEvent);

		EventManager.Publish(new CardDiscardedForCostEvent { Card = thornedCard });

		Assert.NotNull(passiveEvent);
		Assert.Same(player, passiveEvent.Target);
		Assert.Equal(AppliedPassiveType.Scar, passiveEvent.Type);
		Assert.Equal(1, passiveEvent.Delta);
	}

	[Fact]
	public void Scorched_and_thorned_card_tooltips_describe_status_effects()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, "StatusCard");
		entityManager.AddComponent(card, new Scorched { Owner = card });
		entityManager.AddComponent(card, new Thorned { Owner = card });

		var tooltip = TooltipTextService.BuildCardTooltip(card, "Strike");

		Assert.Contains("This card is scorched - when pledged, lose 1 HP.", tooltip);
		Assert.Contains("This card is thorned - when discarded to pay a card cost, gain 1 scar.", tooltip);
	}

	[Fact]
	public void Scorched_and_thorned_card_tooltip_blocks_are_stacked_and_recursive()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, "StatusCard");
		entityManager.AddComponent(card, new Scorched { Owner = card });
		entityManager.AddComponent(card, new Thorned { Owner = card });

		var blocks = TooltipTextService.BuildTooltipBlocks(card, string.Empty);

		Assert.Equal(
			["scorched", "thorned", "scar"],
			blocks.Select(block => block.Id).ToArray());
		Assert.Contains("This card is scorched - when pledged, lose 1 HP.", blocks[0].Text);
		Assert.Contains("This card is thorned - when discarded to pay a card cost, gain 1 scar.", blocks[1].Text);
		Assert.Contains("X Scar - Lose X max HP.", blocks[2].Text);
	}

	[Fact]
	public void Scorched_and_thorned_keyword_tooltips_are_discovered()
	{
		var tooltip = TooltipTextService.GetKeywordTooltip("Apply scorched and thorned.");

		Assert.Contains("Scorched - When pledged, lose 1 HP.", tooltip);
		Assert.Contains("Thorned - When discarded to pay a card cost, gain 1 scar.", tooltip);
	}

	[Fact]
	public void Keyword_tooltip_blocks_are_recursive_and_deduplicated()
	{
		var sharpenBlocks = TooltipTextService.GetKeywordTooltipBlocks("Gain 5 sharpen.");
		Assert.Equal(["sharpen"], sharpenBlocks.Select(block => block.Id).ToArray());
		Assert.Contains("X Sharpen - Your next weapon attack this turn gains +X damage.", sharpenBlocks[0].Text);

		var guardBlocks = TooltipTextService.GetKeywordTooltipBlocks("Gain 2 guard.");
		Assert.Equal(["guard", "aggression"], guardBlocks.Select(block => block.Id).ToArray());

		var frostbiteBlocks = TooltipTextService.GetKeywordTooltipBlocks("Gain 1 frostbite.");
		Assert.Equal(["frostbite"], frostbiteBlocks.Select(block => block.Id).ToArray());
	}

	[Fact]
	public void Keyword_tooltips_discover_late_registry_entries_without_other_matches()
	{
		Assert.Contains("X Darkness - The enemy loses X damage", TooltipTextService.GetKeywordTooltip("Gain darkness."));
		Assert.Contains("X Silenced - You cannot play pledged cards", TooltipTextService.GetKeywordTooltip("Gain silenced."));
		Assert.Contains("Sealed - Sealed cards cost HP", TooltipTextService.GetKeywordTooltip("Add 2 seals."));
	}

	private static Entity CreateCard(EntityManager entityManager, string name)
	{
		var card = entityManager.CreateEntity(name);
		entityManager.AddComponent(card, new CardData());
		return card;
	}

	private static void AddCurrentAttackProgress(EntityManager entityManager, int playedCards)
	{
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned = [new PlannedAttack()],
		});
		var progressEntity = entityManager.CreateEntity("EnemyAttackProgress");
		entityManager.AddComponent(progressEntity, new EnemyAttackProgress
		{
			Enemy = enemy,
			AttackSequence = 1,
			PlayedCards = playedCards,
		});
	}
}
