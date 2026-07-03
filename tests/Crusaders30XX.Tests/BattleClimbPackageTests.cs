using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class BattleClimbPackageTests : IDisposable
{
	public BattleClimbPackageTests()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
	}

	public void Dispose()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
	}

	[Fact]
	public void First_climb_encounter_applies_and_clears_entire_package_once()
	{
		SavePackage(courage: 2, temperance: 3, vigor: 1, burn: 4, fear: 5);
		var player = CreateManagedPlayer(courage: 0, temperance: 7);
		var queued = new QueuedEvents { IsClimbEncounter = true, CurrentIndex = -1 };

		Assert.True(BattleSceneSystem.IsFirstQueuedClimbEncounter(queued));
		Assert.True(BattleSceneSystem.ApplyPendingClimbBattlePackage(true, player));
		Assert.Equal(2, player.GetComponent<Courage>().Amount);
		Assert.Equal(10, player.GetComponent<Temperance>().Amount);
		AssertPassive(player, AppliedPassiveType.Vigor, 1);
		AssertPassive(player, AppliedPassiveType.Burn, 4);
		AssertPassive(player, AppliedPassiveType.Fear, 5);

		var consumed = SaveCache.GetClimbState();
		Assert.Equal(0, consumed.nextBattleBonus.courage);
		Assert.Equal(0, consumed.nextBattleBonus.temperance);
		Assert.Equal(0, consumed.nextBattleBonus.vigor);
		Assert.Equal(0, consumed.nextBattlePenalty.burn);
		Assert.Equal(0, consumed.nextBattlePenalty.fear);

		Assert.False(BattleSceneSystem.ApplyPendingClimbBattlePackage(true, player));
		Assert.Equal(2, player.GetComponent<Courage>().Amount);
		Assert.Equal(10, player.GetComponent<Temperance>().Amount);
		AssertPassive(player, AppliedPassiveType.Vigor, 1);
		AssertPassive(player, AppliedPassiveType.Burn, 4);
		AssertPassive(player, AppliedPassiveType.Fear, 5);
	}

	[Fact]
	public void Later_queued_battle_does_not_consume_or_apply_package()
	{
		SavePackage(courage: 2, temperance: 3, vigor: 1, burn: 4, fear: 5);
		var player = CreateManagedPlayer(courage: 0, temperance: 0);
		var queued = new QueuedEvents { IsClimbEncounter = true, CurrentIndex = 0 };

		Assert.False(BattleSceneSystem.IsFirstQueuedClimbEncounter(queued));
		Assert.False(BattleSceneSystem.ApplyPendingClimbBattlePackage(false, player));
		Assert.Equal(0, player.GetComponent<Courage>().Amount);
		Assert.Equal(0, player.GetComponent<Temperance>().Amount);
		Assert.Empty(player.GetComponent<AppliedPassives>().Passives);
		Assert.Equal(2, SaveCache.GetClimbState().nextBattleBonus.courage);
	}

	[Fact]
	public void Final_encounter_is_eligible_and_empty_package_is_a_no_op()
	{
		var player = CreateManagedPlayer(courage: 4, temperance: 6);
		var finalEncounter = new QueuedEvents
		{
			IsClimbEncounter = true,
			ClimbEncounterSlotId = "final",
			CurrentIndex = -1,
		};

		Assert.True(BattleSceneSystem.IsFirstQueuedClimbEncounter(finalEncounter));
		Assert.False(BattleSceneSystem.ApplyPendingClimbBattlePackage(true, player));
		Assert.Equal(4, player.GetComponent<Courage>().Amount);
		Assert.Equal(6, player.GetComponent<Temperance>().Amount);
		Assert.Empty(player.GetComponent<AppliedPassives>().Passives);
	}

	[Fact]
	public void Final_encounter_load_resolves_the_gate_even_after_previous_location()
	{
		var previousBattle = new QueuedEvents
		{
			IsClimbEncounter = true,
			BattleLocation = BattleLocation.Jungle,
		};
		Assert.Equal(BattleLocation.Jungle, BattleSceneSystem.ResolveBattleLocationForLoad(previousBattle, guidedTutorial: false));

		var finalEncounter = new QueuedEvents
		{
			IsClimbEncounter = true,
			ClimbEncounterSlotId = "final",
			BattleLocation = BattleLocation.TheGate,
		};

		Assert.Equal(BattleLocation.TheGate, BattleSceneSystem.ResolveBattleLocationForLoad(finalEncounter, guidedTutorial: false));
		Assert.Equal(BattleLocation.Desert, BattleSceneSystem.ResolveBattleLocationForLoad(finalEncounter, guidedTutorial: true));
	}

	private static Entity CreateManagedPlayer(int courage, int temperance)
	{
		var entityManager = new EntityManager();
		_ = new CourageManagerSystem(entityManager);
		_ = new TemperanceManagerSystem(entityManager);
		_ = new AppliedPassivesManagementSystem(entityManager);
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Courage { Amount = courage });
		entityManager.AddComponent(player, new Temperance { Amount = temperance });
		entityManager.AddComponent(player, new AppliedPassives());
		return player;
	}

	private static void SavePackage(int courage, int temperance, int vigor, int burn, int fear)
	{
		var climb = SaveCache.GetClimbState();
		climb.nextBattleBonus = new ClimbNextBattleBonusSave
		{
			courage = courage,
			temperance = temperance,
			vigor = vigor,
		};
		climb.nextBattlePenalty = new ClimbNextBattlePenaltySave
		{
			burn = burn,
			fear = fear,
		};
		SaveCache.SaveClimbState(climb);
	}

	private static void AssertPassive(Entity player, AppliedPassiveType type, int expected)
	{
		Assert.True(player.GetComponent<AppliedPassives>().Passives.TryGetValue(type, out int actual));
		Assert.Equal(expected, actual);
	}
}
