using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class FearSlowPassiveTests : IDisposable
{
	public FearSlowPassiveTests()
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
	public void Fear_forces_all_planned_attacks_to_ambush()
	{
		var (_, player, _, intent) = BuildWorld();
		player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Fear] = 2;
		_ = new EnemyIntentPlanningSystem(_entityManager);

		PublishEnemyStart();

		Assert.NotEmpty(intent.Planned);
		Assert.All(intent.Planned, pa => Assert.True(pa.IsAmbush));
	}

	[Fact]
	public void Without_fear_zero_ambush_attack_does_not_ambush()
	{
		var (_, player, _, intent) = BuildWorld();
		_ = new EnemyIntentPlanningSystem(_entityManager);

		PublishEnemyStart();

		Assert.NotEmpty(intent.Planned);
		Assert.All(intent.Planned, pa => Assert.False(pa.IsAmbush));
	}

	[Fact]
	public void Battle_end_removes_one_fear_stack()
	{
		var (_, player, enemy, _) = BuildWorld();
		_ = new AppliedPassivesManagementSystem(_entityManager);
		player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Fear] = 3;

		EventManager.Publish(new EnemyKilledEvent { Enemy = enemy });

		Assert.Equal(2, GetPassive(player, AppliedPassiveType.Fear));
	}

	[Fact]
	public void Player_end_removes_one_slow_stack()
	{
		var (_, player, _, _) = BuildWorld();
		_ = new AppliedPassivesManagementSystem(_entityManager);
		player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Slow] = 4;

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PlayerEnd, Previous = SubPhase.Action });

		Assert.Equal(3, GetPassive(player, AppliedPassiveType.Slow));
	}

	private void PublishEnemyStart()
	{
		var phaseState = _entityManager.GetEntitiesWithComponent<PhaseState>().First().GetComponent<PhaseState>();
		phaseState.Sub = SubPhase.PlayerEnd;
		phaseState.TurnNumber = 1;
		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart });
	}

	private EntityManager _entityManager;

	private (EntityManager EntityManager, Entity Player, Entity Enemy, AttackIntent Intent) BuildWorld()
	{
		_entityManager = new EntityManager();
		var entityManager = _entityManager;

		var phaseEntity = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phaseEntity, new PhaseState
		{
			Main = MainPhase.EnemyTurn,
			Sub = SubPhase.Block,
			TurnNumber = 1,
		});

		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new AppliedPassives());

		var definition = new FallenShepherd();
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy
		{
			Id = definition.Id,
			Name = definition.Name,
			EnemyBase = definition,
		});
		entityManager.AddComponent(enemy, new EnemyArsenal { AttackIds = new() { EnemyAttackId.FallenShepherdPhase1 } });
		entityManager.AddComponent(enemy, new AppliedPassives());
		var intent = new AttackIntent();
		entityManager.AddComponent(enemy, intent);
		entityManager.AddComponent(enemy, new NextTurnAttackIntent());

		return (entityManager, player, enemy, intent);
	}

	private static int GetPassive(Entity owner, AppliedPassiveType type)
	{
		var passives = owner.GetComponent<AppliedPassives>()?.Passives;
		if (passives == null) return 0;
		return passives.TryGetValue(type, out int stacks) ? stacks : 0;
	}
}
