using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class BlockedEnemyAttackPresentationTests : IDisposable
{
	public BlockedEnemyAttackPresentationTests()
	{
		EventManager.Clear();
		EventQueue.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
		EventQueue.Clear();
	}

	[Theory]
	[InlineData(AppliedPassiveType.Aegis)]
	[InlineData(AppliedPassiveType.Armor)]
	[InlineData(AppliedPassiveType.Guard)]
	[InlineData(AppliedPassiveType.Shield)]
	public void Fully_prevented_enemy_damage_replaces_impact_feedback(AppliedPassiveType prevention)
	{
		var context = BuildCombat(prevention);
		var playedSfx = new List<SfxTrack>();
		EventManager.Subscribe<PlaySfxEvent>(evt => playedSfx.Add(evt.Track));

		StartEnemyImpact(context, damage: 5);

		Assert.Equal(20, context.Player.GetComponent<HP>().Current);
		Assert.Contains(SfxTrack.ShieldBlock, playedSfx);
		Assert.DoesNotContain(SfxTrack.SwordImpact, playedSfx);
		var effects = ActiveEffects(context.EntityManager);
		var attack = Assert.Single(effects, effect => effect.DrivesGameplayImpact);
		Assert.DoesNotContain(VisualEffectModule.Shake, attack.Recipe.Modules);
		var blocked = Assert.Single(effects, effect => !effect.DrivesGameplayImpact);
		Assert.Contains(VisualEffectModule.ShieldWard, blocked.Recipe.Modules);
	}

	[Fact]
	public void Damaging_enemy_attack_keeps_authored_impact_feedback()
	{
		var context = BuildCombat();
		var playedSfx = new List<SfxTrack>();
		EventManager.Subscribe<PlaySfxEvent>(evt => playedSfx.Add(evt.Track));

		StartEnemyImpact(context, damage: 5);

		Assert.Equal(15, context.Player.GetComponent<HP>().Current);
		Assert.Contains(SfxTrack.SwordImpact, playedSfx);
		Assert.DoesNotContain(SfxTrack.ShieldBlock, playedSfx);
		var attack = Assert.Single(ActiveEffects(context.EntityManager));
		Assert.Contains(VisualEffectModule.Shake, attack.Recipe.Modules);
	}

	private static CombatContext BuildCombat(AppliedPassiveType? prevention = null)
	{
		var entityManager = new EntityManager();
		_ = new HpManagementSystem(entityManager);
		_ = new EnemyDamageManagerSystem(entityManager);
		var coordinator = new ModularEffectCoordinatorSystem(entityManager);

		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new HP { Current = 20, Max = 20 });
		entityManager.AddComponent(player, new Transform { Position = new Vector2(300f, 500f) });
		var passives = new AppliedPassives();
		if (prevention.HasValue)
		{
			passives.Passives[prevention.Value] = 5;
		}
		entityManager.AddComponent(player, passives);

		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());
		entityManager.AddComponent(enemy, new AttackIntent());
		entityManager.AddComponent(enemy, new Transform { Position = new Vector2(900f, 500f) });
		return new CombatContext(entityManager, coordinator, player, enemy);
	}

	private static void StartEnemyImpact(CombatContext context, int damage)
	{
		EventManager.Publish(new ApplyEffect
		{
			EffectType = "Damage",
			Amount = damage,
			Percentage = 100,
			Source = context.Enemy,
			Target = context.Player
		});
		EventManager.Publish(new VisualEffectRequested
		{
			Recipe = VisualEffectPresets.EnemySlash(),
			Source = context.Enemy,
			Target = context.Player,
			SourceKind = VisualEffectSourceKind.EnemyAttack,
			SourceId = "test_attack",
			DrivesGameplayImpact = true
		});
		context.Coordinator.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.2)));
	}

	private static List<ActiveVisualEffect> ActiveEffects(EntityManager entityManager)
	{
		return entityManager.GetEntitiesWithComponent<ActiveVisualEffect>()
			.Select(entity => entity.GetComponent<ActiveVisualEffect>())
			.Where(effect => effect != null)
			.ToList();
	}

	private sealed record CombatContext(
		EntityManager EntityManager,
		ModularEffectCoordinatorSystem Coordinator,
		Entity Player,
		Entity Enemy);
}
