using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class PassiveApplicationAnimationTests : IDisposable
{
	public PassiveApplicationAnimationTests() => EventManager.Clear();
	public void Dispose() => EventManager.Clear();

	[Fact]
	public void Catalog_covers_every_passive_with_unique_signatures()
	{
		var types = Enum.GetValues<AppliedPassiveType>();

		Assert.Equal(types.Length, PassiveApplicationRecipeCatalog.All.Count);
		Assert.All(types, type => Assert.True(PassiveApplicationRecipeCatalog.All.ContainsKey(type), type.ToString()));
		Assert.Equal(
			PassiveApplicationRecipeCatalog.All.Count,
			PassiveApplicationRecipeCatalog.All.Values.Select(recipe => recipe.Signature).Distinct().Count());
	}

	[Fact]
	public void Only_positive_deltas_create_application_animations()
	{
		var (system, target) = CreateSystem();

		Apply(target, AppliedPassiveType.Burn, 0);
		Apply(target, AppliedPassiveType.Burn, -1);
		Assert.Equal(0, system.AnimationCount);

		Apply(target, AppliedPassiveType.Burn, 2);
		Assert.Equal(1, system.AnimationCount);
	}

	[Fact]
	public void Rapid_duplicate_applications_coalesce_and_strengthen()
	{
		var (system, target) = CreateSystem();
		Apply(target, AppliedPassiveType.Burn, 1);
		Update(system, .05f);

		Apply(target, AppliedPassiveType.Burn, 2);

		Assert.Equal(1, system.AnimationCount);
		Assert.True(system.TryGetAnimation(target, AppliedPassiveType.Burn, out _, out float strength, out int delta));
		Assert.True(strength > 1f);
		Assert.Equal(3, delta);
	}

	[Fact]
	public void Duplicate_after_coalesce_window_replays_separately()
	{
		var (system, target) = CreateSystem();
		Apply(target, AppliedPassiveType.Burn, 1);
		Update(system, system.CoalesceWindowSeconds + .01f);

		Apply(target, AppliedPassiveType.Burn, 1);

		Assert.Equal(2, system.AnimationCount);
	}

	[Fact]
	public void Different_statuses_receive_target_local_stagger()
	{
		var (system, target) = CreateSystem();
		Apply(target, AppliedPassiveType.Burn, 1);
		Apply(target, AppliedPassiveType.Armor, 1);

		Assert.True(system.TryGetAnimation(target, AppliedPassiveType.Burn, out float burnStart, out _, out _));
		Assert.True(system.TryGetAnimation(target, AppliedPassiveType.Armor, out float armorStart, out _, out _));
		Assert.Equal(system.StatusStaggerSeconds, armorStart - burnStart, 3);
	}

	[Fact]
	public void Capacity_removes_oldest_animation_deterministically()
	{
		var (system, target) = CreateSystem();
		system.MaxPerTarget = 2;
		system.MaxGlobal = 2;
		Apply(target, AppliedPassiveType.Burn, 1);
		Update(system, .01f);
		Apply(target, AppliedPassiveType.Armor, 1);
		Update(system, .01f);
		Apply(target, AppliedPassiveType.Fear, 1);

		Assert.Equal(2, system.AnimationCount);
		Assert.False(system.TryGetAnimation(target, AppliedPassiveType.Burn, out _, out _, out _));
		Assert.True(system.TryGetAnimation(target, AppliedPassiveType.Armor, out _, out _, out _));
		Assert.True(system.TryGetAnimation(target, AppliedPassiveType.Fear, out _, out _, out _));
	}

	[Fact]
	public void Status_burst_publishes_one_shared_audio_cue()
	{
		var (system, target) = CreateSystem();
		int cueCount = 0;
		EventManager.Subscribe<PlaySfxEvent>(evt =>
		{
			if (evt.Track == SfxTrack.ApplyCard) cueCount++;
		});
		Apply(target, AppliedPassiveType.Burn, 1);
		Apply(target, AppliedPassiveType.Armor, 1);

		Update(system, 0f);
		Update(system, system.StatusStaggerSeconds);

		Assert.Equal(1, cueCount);
	}

	[Fact]
	public void Scene_change_clears_pending_and_active_animations()
	{
		var (system, target) = CreateSystem();
		Apply(target, AppliedPassiveType.Burn, 1);

		EventManager.Publish(new LoadSceneEvent { Scene = SceneId.Battle });

		Assert.Equal(0, system.AnimationCount);
	}

	private static (PassiveApplicationAnimationDisplaySystem System, Entity Target) CreateSystem()
	{
		var entityManager = new EntityManager();
		var target = entityManager.CreateEntity("Target");
		entityManager.AddComponent(target, new Transform { Position = new Vector2(500f, 500f) });
		return (new PassiveApplicationAnimationDisplaySystem(entityManager, null, null), target);
	}

	private static void Apply(Entity target, AppliedPassiveType type, int delta)
	{
		EventManager.Publish(new ApplyPassiveEvent { Target = target, Type = type, Delta = delta });
	}

	private static void Update(PassiveApplicationAnimationDisplaySystem system, float seconds)
	{
		system.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));
	}
}
