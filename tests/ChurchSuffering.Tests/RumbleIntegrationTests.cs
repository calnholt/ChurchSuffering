using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class RumbleIntegrationTests
{
	[Fact]
	public void Visual_effect_recipe_and_beat_copy_rumble_authoring()
	{
		var recipe = new VisualEffectRecipe { Intensity = 1.2f }
			.WithImpactRumble(RumbleProfile.HeavyImpact, 0.8f)
			.WithModules(VisualEffectModule.Shockwave);

		Assert.Equal(RumbleProfile.HeavyImpact, recipe.Clone().ImpactRumbleProfile);
		Assert.Equal(0.8f, recipe.WithIntensity(0.5f).ImpactRumbleScale);

		var beat = new VisualEffectBeat
		{
			ImpactRumbleProfile = RumbleProfile.Soft,
			ImpactRumbleScale = 0.6f,
		}.WithModules(VisualEffectModule.Rays);
		VisualEffectRecipe legacy = beat.ToLegacyRecipe();

		Assert.Equal(RumbleProfile.Soft, legacy.ImpactRumbleProfile);
		Assert.Equal(0.6f, legacy.ImpactRumbleScale);
	}

	[Fact]
	public void Modular_effect_publishes_rumble_at_impact_but_preview_stays_silent()
	{
		EventManager.Clear();
		var entityManager = new EntityManager();
		Entity source = CreateAnchor(entityManager, "Source", new Vector2(10f, 10f));
		Entity target = CreateAnchor(entityManager, "Target", new Vector2(20f, 20f));
		var system = new ModularEffectCoordinatorSystem(entityManager);
		var requests = new List<RumbleRequested>();
		EventManager.Subscribe<RumbleRequested>(requests.Add);
		var recipe = new VisualEffectRecipe { Intensity = 1f }
			.WithImpactRumble(RumbleProfile.MediumImpact);

		EventManager.Publish(new VisualEffectRequested
		{
			Recipe = recipe,
			Source = source,
			Target = target,
			TimingOverride = new VisualEffectTiming { DurationSeconds = 0.2f, ImpactTimeSeconds = 0.05f },
		});
		system.Update(new GameTime(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)));
		Assert.Single(requests);
		Assert.Equal(RumbleProfile.MediumImpact, requests[0].Profile);

		EventManager.Publish(new VisualEffectRequested
		{
			Recipe = recipe,
			Source = source,
			Target = target,
			IsPreview = true,
			TimingOverride = new VisualEffectTiming { DurationSeconds = 0.2f, ImpactTimeSeconds = 0.05f },
		});
		system.Update(new GameTime(TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(0.1)));
		Assert.Single(requests);
		EventManager.Clear();
	}

	[Fact]
	public void Achievement_completion_uses_celebration_pattern_and_disabled_setting_clears_output()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.SetRumbleLevel(100);
		var entityManager = new EntityManager();
		var source = new MixedRumbleFakeInputSource();
		_ = new ControllerRumbleSystem(entityManager, source, SaveCache.GetRumbleLevel());
		EventManager.Publish(new PlayerInputEvent
		{
			Frame = default(PlayerInputFrame) with
			{
				IsWindowActive = true,
				IsGamepadConnected = true,
				Device = PlayerInputDevice.Gamepad,
				PreviousDevice = PlayerInputDevice.Gamepad,
			}
		});

		EventManager.Publish(new AchievementCompletedEvent { AchievementId = "test" });
		Assert.Contains(source.VibrationCalls, call =>
			call.High > 0f && call.LeftTrigger > 0f && call.RightTrigger > 0f);

		SaveCache.SetRumbleLevel(0);
		Assert.Equal((0f, 0f, 0f, 0f), source.VibrationCalls[^1]);
		SaveCache.SetRumbleLevel(100);
		EventManager.Clear();
	}

	private static Entity CreateAnchor(EntityManager entityManager, string name, Vector2 position)
	{
		Entity entity = entityManager.CreateEntity(name);
		entityManager.AddComponent(entity, new Transform { Position = position });
		return entity;
	}
}
