using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class PlayerHudHealthDisplaySystemTests
{
	[Fact]
	public void Normal_health_preserves_fraction_and_percentages()
	{
		var state = PlayerHudHealthRendering.BuildRenderState(18, 20, 0.75f, 3, 0.20f);

		Assert.Equal(18, state.Current);
		Assert.Equal(20, state.Max);
		Assert.Equal("18/20", state.FractionText);
		Assert.Equal(0.9f, state.TargetPercent, 3);
		Assert.Equal(0.75f, state.DisplayedPercent, 3);
		Assert.Equal(3, state.IncomingDamage);
		Assert.False(state.IsLowHealth);
	}

	[Fact]
	public void Zero_health_has_empty_fill_and_remains_visible_as_fraction()
	{
		var state = PlayerHudHealthRendering.BuildRenderState(0, 20, 0f, 10, 0.20f);

		Assert.Equal("0/20", state.FractionText);
		Assert.Equal(0f, state.TargetPercent);
		Assert.Equal(0, state.IncomingDamage);
		Assert.True(state.IsLowHealth);
	}

	[Fact]
	public void Over_max_health_clamps_fill_without_clamping_fraction()
	{
		var state = PlayerHudHealthRendering.BuildRenderState(30, 20, 1.4f, 0, 0.20f);

		Assert.Equal("30/20", state.FractionText);
		Assert.Equal(1f, state.TargetPercent);
		Assert.Equal(1f, state.DisplayedPercent);
		Assert.False(state.IsLowHealth);
	}

	[Fact]
	public void Low_health_uses_threshold_and_flash_alpha_controls()
	{
		var state = PlayerHudHealthRendering.BuildRenderState(4, 20, 0.2f, 0, 0.20f);
		float alpha = PlayerHudHealthRendering.CalculatePulseAlpha(0f, 2f, 0.35f, 1f);

		Assert.True(state.IsLowHealth);
		Assert.Equal(0.675f, alpha, 3);
	}

	[Fact]
	public void Lethal_damage_preview_equals_current_health()
	{
		var entityManager = new EntityManager();
		AddActiveAttack(entityManager, attackSequence: 1, damage: 8);

		Assert.Equal(8, PlayerHudHealthRendering.CalculateTotalIncomingDamage(entityManager, 8));
	}

	[Fact]
	public void Overkill_damage_preview_is_clamped_to_current_health()
	{
		var entityManager = new EntityManager();
		AddActiveAttack(entityManager, attackSequence: 1, damage: 50);

		Assert.Equal(8, PlayerHudHealthRendering.CalculateTotalIncomingDamage(entityManager, 8));
	}

	[Fact]
	public void Multiple_enemies_sum_only_their_active_attacks()
	{
		var entityManager = new EntityManager();
		var enemyA = AddAttackIntent(entityManager, "enemy-a", activeAttackSequence: 1, plannedCount: 2);
		var enemyB = AddAttackIntent(entityManager, "enemy-b", activeAttackSequence: 3, plannedCount: 1);
		AddProgress(entityManager, enemyA, attackSequence: 1, damage: 3);
		AddProgress(entityManager, enemyB, attackSequence: 3, damage: 5);
		AddProgress(entityManager, enemyA, attackSequence: 2, damage: 20);
		AddProgress(entityManager, enemyA, attackSequence: 99, damage: 20);

		Assert.Equal(8, PlayerHudHealthRendering.CalculateTotalIncomingDamage(entityManager, 20));
	}

	[Fact]
	public void Track_bounds_use_layout_owned_health_geometry()
	{
		var anchor = new PlayerHudAnchor
		{
			HealthPaddingLeft = 14,
			HealthPaddingRight = 18,
			HealthPaddingVertical = 8,
			HealthTrackHeight = 26,
			ContentGap = 8,
		};

		var track = PlayerHudHealthRendering.CalculateTrackBounds(
			new Rectangle(100, 200, 500, 44),
			anchor,
			20f);

		Assert.Equal(new Rectangle(142, 209, 440, 26), track);
	}

	[Fact]
	public void Enemy_region_matches_player_health_size_below_stable_portrait()
	{
		var playerHealthBounds = new Rectangle(100, 200, 559, 36);

		Rectangle enemyBounds = PlayerHudHealthRendering.CalculateEnemyRegionBounds(
			playerHealthBounds,
			new Vector2(1400f, 300f),
			400,
			0.5f,
			10,
			4);

		Assert.Equal(new Rectangle(1130, 404, 559, 36), enemyBounds);
	}

	[Fact]
	public void Enemy_region_is_empty_without_resolved_portrait_geometry()
	{
		Rectangle enemyBounds = PlayerHudHealthRendering.CalculateEnemyRegionBounds(
			new Rectangle(100, 200, 559, 36),
			new Vector2(1400f, 300f),
			0,
			1f,
			0,
			4);

		Assert.Equal(Rectangle.Empty, enemyBounds);
	}

	[Fact]
	public void Legacy_hp_renderer_includes_only_plundered_entities()
	{
		var player = new Entity(1);
		player.AddComponent(new Player());
		player.AddComponent(new HP());
		var enemy = new Entity(2);
		enemy.AddComponent(new Enemy());
		enemy.AddComponent(new HP());
		var temporary = new Entity(3);
		temporary.AddComponent(new HP());
		var plundered = new Entity(4);
		plundered.AddComponent(new Plundered());
		plundered.AddComponent(new HP());

		#pragma warning disable CS0618 // The test locks down the deprecated renderer's remaining scope.
		Assert.False(HPDisplaySystem.ShouldRenderLegacyPlunderHp(player));
		Assert.False(HPDisplaySystem.ShouldRenderLegacyPlunderHp(enemy));
		Assert.False(HPDisplaySystem.ShouldRenderLegacyPlunderHp(temporary));
		Assert.True(HPDisplaySystem.ShouldRenderLegacyPlunderHp(plundered));
		#pragma warning restore CS0618
	}

	private static void AddActiveAttack(EntityManager entityManager, int attackSequence, int damage)
	{
		var enemy = AddAttackIntent(entityManager, $"enemy-{attackSequence}", attackSequence);
		AddProgress(entityManager, enemy, attackSequence, damage);
	}

	private static Entity AddAttackIntent(
		EntityManager entityManager,
		string entityName,
		int activeAttackSequence,
		int plannedCount = 1)
	{
		var enemy = entityManager.CreateEntity(entityName);
		var intent = new AttackIntent { ActiveAttackSequence = activeAttackSequence };
		for (int i = 0; i < plannedCount; i++)
		{
			intent.Planned.Add(new PlannedAttack());
		}
		entityManager.AddComponent(enemy, intent);
		return enemy;
	}

	private static void AddProgress(EntityManager entityManager, Entity enemy, int attackSequence, int damage)
	{
		var progressEntity = entityManager.CreateEntity($"progress-{attackSequence}");
		entityManager.AddComponent(progressEntity, new EnemyAttackProgress
		{
			Enemy = enemy,
			AttackSequence = attackSequence,
			ActualDamage = damage,
		});
	}
}
