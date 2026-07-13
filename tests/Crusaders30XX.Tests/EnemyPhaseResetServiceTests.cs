using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class EnemyPhaseResetServiceTests
{
	[Fact]
	public void Reset_advances_phase_and_clears_battle_passives()
	{
		var world = new World();
		var phaseEntity = world.CreateEntity("PhaseState");
		world.AddComponent(phaseEntity, new PhaseState
		{
			Main = MainPhase.PlayerTurn,
			Sub = SubPhase.Action,
			TurnNumber = 7,
		});

		var player = world.CreateEntity("Player");
		world.AddComponent(player, new Player());
		world.AddComponent(player, new HP { Max = 25, Current = 4 });
		world.AddComponent(player, new Courage { Amount = 6 });
		world.AddComponent(player, new Temperance { Amount = 3 });
		world.AddComponent(player, new ActionPoints { Current = 1 });
		world.AddComponent(player, new AppliedPassives
		{
			Passives =
			{
				[AppliedPassiveType.Might] = 2,
				[AppliedPassiveType.Frostbite] = 4,
				[AppliedPassiveType.Burn] = 1,
			},
		});

		var deckEntity = world.CreateEntity("Deck");
		var deck = new Deck();
		world.AddComponent(deckEntity, deck);
		var drawCard = CreateCard(world, "Draw");
		var handCard = CreateCard(world, "Hand");
		var discardCard = CreateCard(world, "Discard");
		var exhaustedCard = CreateCard(world, "Exhausted");
		world.AddComponent(handCard, new Pledge());
		world.AddComponent(handCard, new Frozen());
		world.AddComponent(handCard, new HotKey { Button = FaceButton.B, Position = HotKeyPosition.Top });
		deck.Cards.AddRange(new[] { drawCard, handCard, discardCard, exhaustedCard });
		deck.DrawPile.Add(drawCard);
		deck.Hand.Add(handCard);
		deck.DiscardPile.Add(discardCard);
		deck.DiscardPile.Add(exhaustedCard);
		deck.ExhaustPile.Add(exhaustedCard);

		var definition = new FallenShepherd();
		definition.MaxHealth = 30;
		definition.CurrentHealth = 0;
		var enemy = world.CreateEntity("Enemy");
		world.AddComponent(enemy, new Enemy
		{
			Id = definition.Id,
			Name = definition.Name,
			MaxHealth = 30,
			CurrentHealth = 0,
			EnemyBase = definition,
		});
		world.AddComponent(enemy, new HP { Max = 30, Current = 0 });
		world.AddComponent(enemy, new EnemyArsenal { AttackIds = new() { EnemyAttackId.FallenShepherdPhase1 } });
		world.AddComponent(enemy, new AttackIntent());
		world.AddComponent(enemy, new NextTurnAttackIntent());
		world.AddComponent(enemy, new AppliedPassives
		{
			Passives =
			{
				[AppliedPassiveType.Sharpen] = 1,
			},
		});
		foreach (var passive in AppliedPassivesManagementSystem.GetBattlePassives())
		{
			player.GetComponent<AppliedPassives>().Passives[passive] = 1;
			enemy.GetComponent<AppliedPassives>().Passives[passive] = 1;
		}

		bool reset = EnemyPhaseResetService.TryResetForNextPhase(
			world.EntityManager,
			enemy,
			new Random(123));

		Assert.True(reset);
		Assert.Equal(2, definition.CurrentPhase);
		Assert.Equal(new[] { EnemyAttackId.FallenShepherdPhase2 }, enemy.GetComponent<EnemyArsenal>().AttackIds);
		Assert.Equal(25, player.GetComponent<HP>().Current);
		Assert.Equal(30, enemy.GetComponent<HP>().Current);
		Assert.Equal(7, phaseEntity.GetComponent<PhaseState>().TurnNumber);
		Assert.Equal(6, player.GetComponent<Courage>().Amount);
		Assert.Equal(3, player.GetComponent<Temperance>().Amount);
		Assert.False(player.GetComponent<AppliedPassives>().Passives.ContainsKey(AppliedPassiveType.Might));
		Assert.True(player.GetComponent<AppliedPassives>().Passives.ContainsKey(AppliedPassiveType.Frostbite));
		Assert.False(enemy.GetComponent<AppliedPassives>().Passives.ContainsKey(AppliedPassiveType.Sharpen));
		foreach (var passive in AppliedPassivesManagementSystem.GetBattlePassives())
		{
			Assert.False(player.GetComponent<AppliedPassives>().Passives.ContainsKey(passive));
			Assert.False(enemy.GetComponent<AppliedPassives>().Passives.ContainsKey(passive));
		}
		Assert.Empty(deck.Hand);
		Assert.Empty(deck.DiscardPile);
		Assert.Equal(3, deck.DrawPile.Count);
		Assert.DoesNotContain(exhaustedCard, deck.DrawPile);
		Assert.True(handCard.HasComponent<Frozen>());
		Assert.True(handCard.HasComponent<Pledge>());
		Assert.False(handCard.HasComponent<HotKey>());
	}

	private static Entity CreateCard(World world, string name)
	{
		var card = world.CreateEntity(name);
		world.AddComponent(card, new CardData());
		world.AddComponent(card, new Transform());
		world.AddComponent(card, new UIElement());
		return card;
	}
}
