using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class ModularEffectActorPresentationSystemTests
{
	[Fact]
	public void Target_shake_moves_only_the_effect_target()
	{
		var entityManager = new EntityManager();
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());

		var effectEntity = entityManager.CreateEntity("PlayerAttackEffect");
		entityManager.AddComponent(effectEntity, new ActiveVisualEffect
		{
			Recipe = new VisualEffectRecipe { Intensity = 1f }
				.WithModules(VisualEffectModule.TargetShake),
			Timing = new VisualEffectTiming
			{
				DurationSeconds = 0.6f,
				ImpactTimeSeconds = 0.2f
			},
			Source = player,
			Target = enemy,
			SourceAnchor = new Vector2(200f, 400f),
			TargetAnchor = new Vector2(800f, 400f),
			ElapsedSeconds = 0.3f
		});

		var system = new ModularEffectActorPresentationSystem(entityManager);
		system.Update(new GameTime(TimeSpan.Zero, TimeSpan.Zero));

		Assert.Equal(Vector2.Zero, player.GetComponent<ActorPresentationState>().DrawOffset);
		Assert.NotEqual(Vector2.Zero, enemy.GetComponent<ActorPresentationState>().DrawOffset);
	}
}
