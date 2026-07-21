using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class WayStationPenanceUiTests : IDisposable
{
	public WayStationPenanceUiTests()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
	}

	public void Dispose()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
	}

	[Fact]
	public void Motion_gates_entrance_and_hides_after_exit()
	{
		var world = new World();
		var root = world.CreateEntity(WayStationSceneConstants.ModalRootName);
		var state = new WayStationPenanceModalState { RequestedVisible = true };
		world.AddComponent(root, state);
		world.AddComponent(root, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Root });
		var motion = new WayStationPenanceMotionSystem(world.EntityManager);

		motion.Update(Elapsed(1.44));
		Assert.Equal(WayStationPenanceModalPhase.Entering, state.Phase);
		Assert.False(state.InteractionEnabled);

		motion.Update(Elapsed(0.01));
		Assert.Equal(WayStationPenanceModalPhase.Visible, state.Phase);
		Assert.True(state.InteractionEnabled);

		state.RequestedVisible = false;
		motion.Update(Elapsed(0.47));
		Assert.Equal(WayStationPenanceModalPhase.Hidden, state.Phase);
		Assert.False(state.InteractionEnabled);
	}

	[Fact]
	public void Weapon_and_node_clicks_follow_per_weapon_unlocks()
	{
		SaveCache.GetAll().waystation = new WayStationMetaSave
		{
			highestPenanceByWeapon = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			{
				["sword"] = 24,
				["dagger"] = 8,
				["hammer"] = 12,
			},
		};
		var world = new World();
		var scene = world.CreateEntity("SceneState");
		world.AddComponent(scene, new SceneState { Current = SceneId.WayStation });
		var controller = new WayStationClimbSettingsModalSystem(world);
		var motion = new WayStationPenanceMotionSystem(world.EntityManager);
		EventManager.Publish(new OpenWayStationClimbSettingsModalEvent());
		motion.Update(Elapsed(1.45));
		controller.Update(Elapsed(0));
		Assert.Null(world.EntityManager.GetEntity(WayStationSceneConstants.ModalRootName).GetComponent<UIElement>());
		var setup = WayStationRunSetupService.GetRunSetup(world.EntityManager);
		Assert.Equal(StartingWeapon.Sword, setup.SelectedWeapon);
		Assert.Equal(24, setup.SelectedPenanceLevel);

		world.EntityManager.GetEntity(WayStationSceneConstants.HammerButtonName).GetComponent<UIElement>().IsClicked = true;
		controller.Update(Elapsed(0));
		Assert.Equal(StartingWeapon.Hammer, setup.SelectedWeapon);
		Assert.Equal(12, setup.SelectedPenanceLevel);

		world.EntityManager.GetEntity(WayStationSceneConstants.HammerButtonName).GetComponent<UIElement>().IsClicked = false;
		world.EntityManager.GetEntity($"{WayStationSceneConstants.NodePrefix}12").GetComponent<UIElement>().IsClicked = true;
		controller.Update(Elapsed(0));
		Assert.Equal(11, setup.SelectedPenanceLevel);

		world.EntityManager.GetEntity($"{WayStationSceneConstants.NodePrefix}12").GetComponent<UIElement>().IsClicked = false;
		world.EntityManager.GetEntity($"{WayStationSceneConstants.NodePrefix}13").GetComponent<UIElement>().IsClicked = true;
		controller.Update(Elapsed(0));
		Assert.Equal(11, setup.SelectedPenanceLevel);
	}

	[Fact]
	public void Ease_slam_preserves_overshoot()
	{
		Assert.True(WayStationPenanceMotionSystem.CubicBezier(0.7f, 0.2f, 1.35f, 0.4f, 1f) > 1f);
	}

	[Fact]
	public void Weapon_selection_flash_does_not_scale_the_weapon()
	{
		var world = new World();
		var root = world.CreateEntity(WayStationSceneConstants.ModalRootName);
		world.AddComponent(root, new WayStationPenanceModalState
		{
			RequestedVisible = true,
			Phase = WayStationPenanceModalPhase.Visible,
			InteractionEnabled = true,
		});
		world.AddComponent(root, new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Root });
		var weapon = world.CreateEntity("SelectedWeapon");
		world.AddComponent(weapon, new WayStationPenanceWeaponPresentation { IsSelected = true });
		var weaponMotion = new WayStationPenanceMotion { Role = WayStationPenanceMotionRole.Weapon };
		world.AddComponent(weapon, weaponMotion);
		var motionSystem = new WayStationPenanceMotionSystem(world.EntityManager);

		EventManager.Publish(new WayStationPenanceSelectionChangedEvent { WeaponChanged = true });
		motionSystem.Update(Elapsed(0.25));

		Assert.Equal(1f, weaponMotion.Scale);
		Assert.True(weaponMotion.Glow > 0f);
	}

	private static GameTime Elapsed(double seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
}
