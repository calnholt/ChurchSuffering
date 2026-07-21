using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class ScarHandlingTests : IDisposable
{
	public ScarHandlingTests()
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
	public void Gaining_scar_immediately_lowers_max_hp()
	{
		var (_, player, _) = BuildWorld();
		var hp = player.GetComponent<HP>();

		EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Scar, Delta = 3 });

		Assert.Equal(20, hp.UnscarredMax);
		Assert.Equal(17, hp.Max);
		Assert.Equal(17, hp.Current);
		Assert.Equal(3, GetPassive(player, AppliedPassiveType.Scar));
	}

	[Fact]
	public void Removing_scar_stack_does_not_restore_current_battle_max_hp()
	{
		var (_, player, _) = BuildWorld();
		var hp = player.GetComponent<HP>();
		EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Scar, Delta = 3 });

		EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Scar, Delta = -1 });

		Assert.Equal(17, hp.Max);
		Assert.Equal(17, hp.Current);
		Assert.Equal(2, GetPassive(player, AppliedPassiveType.Scar));
	}

	[Fact]
	public void Battle_start_removes_one_scar_stack_without_restoring_encounter_max_hp()
	{
		var (_, player, _) = BuildWorld();
		var hp = player.GetComponent<HP>();
		player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Scar] = 3;
		hp.Current = 8;

		EventManager.Publish(new ApplyBattleMaxHpEvent { Target = player, ScarPenalty = 3 });
		EventManager.Publish(new FullyHealEvent { Target = player });
		Assert.Equal(17, hp.Max);
		Assert.Equal(17, hp.Current);

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
		PumpEventQueue();

		Assert.Equal(2, GetPassive(player, AppliedPassiveType.Scar));
		Assert.Equal(17, hp.Max);
		Assert.Equal(17, hp.Current);
	}

	[Fact]
	public void Next_battle_recalculates_max_hp_from_remaining_scar_stacks()
	{
		var (_, player, _) = BuildWorld();
		var hp = player.GetComponent<HP>();
		player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Scar] = 3;

		EventManager.Publish(new ApplyBattleMaxHpEvent { Target = player, ScarPenalty = 3 });
		EventManager.Publish(new FullyHealEvent { Target = player });
		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
		PumpEventQueue();

		EventManager.Publish(new ApplyBattleMaxHpEvent { Target = player, ScarPenalty = GetPassive(player, AppliedPassiveType.Scar) });
		EventManager.Publish(new FullyHealEvent { Target = player });

		Assert.Equal(2, GetPassive(player, AppliedPassiveType.Scar));
		Assert.Equal(18, hp.Max);
		Assert.Equal(18, hp.Current);
	}

	[Fact]
	public void Max_hp_increase_recomputes_visible_max_against_current_scar_stacks()
	{
		var (_, player, _) = BuildWorld();
		var hp = player.GetComponent<HP>();
		player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Scar] = 3;
		hp.Max = 17;
		hp.Current = 12;

		EventManager.Publish(new IncreaseMaxHpEvent { Target = player, Delta = 2 });

		Assert.Equal(22, hp.UnscarredMax);
		Assert.Equal(19, hp.Max);
		Assert.Equal(19, hp.Current);
	}

	private static (EntityManager EntityManager, Entity Player, Entity Enemy) BuildWorld()
	{
		var entityManager = new EntityManager();
		_ = new HpManagementSystem(entityManager);
		_ = new AppliedPassivesManagementSystem(entityManager);

		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new HP { Max = 20, Current = 20, UnscarredMax = 20 });
		entityManager.AddComponent(player, new AppliedPassives());

		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());
		entityManager.AddComponent(enemy, new HP { Max = 30, Current = 30, UnscarredMax = 30 });
		entityManager.AddComponent(enemy, new AppliedPassives());

		return (entityManager, player, enemy);
	}

	private static void PumpEventQueue()
	{
		while (!EventQueue.IsIdle)
		{
			EventQueue.Update(AppliedPassivesManagementSystem.Duration + 0.1f);
		}
	}

	private static int GetPassive(Entity owner, AppliedPassiveType type)
	{
		var passives = owner.GetComponent<AppliedPassives>()?.Passives;
		if (passives == null) return 0;
		return passives.TryGetValue(type, out int stacks) ? stacks : 0;
	}
}
