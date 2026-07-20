using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class ClimbOverviewGamepadInputTests : IDisposable
{
	public ClimbOverviewGamepadInputTests()
	{
		EventManager.Clear();
		StateSingleton.IsActive = false;
		StateSingleton.PreventClicking = false;
	}

	public void Dispose()
	{
		EventManager.Clear();
		StateSingleton.IsActive = false;
		StateSingleton.PreventClicking = false;
	}

	[Fact]
	public void Open_publishes_modal_event_with_overview_title()
	{
		var entityManager = BuildClimbWorld(out _);
		OpenCardListModalEvent opened = null;
		EventManager.Subscribe<OpenCardListModalEvent>(evt => opened = evt);

		ClimbOverviewViewService.Open(entityManager);

		Assert.NotNull(opened);
		Assert.Equal(ClimbOverviewViewService.OverviewTitle, opened.Title);
		Assert.Equal(CardListModalMode.Inventory, opened.Mode);
	}

	[Fact]
	public void IsOverviewOpen_returns_false_for_unrelated_modal()
	{
		var entityManager = BuildClimbWorld(out _);
		var modalEntity = entityManager.CreateEntity("CardListModal");
		entityManager.AddComponent(modalEntity, new CardListModal
		{
			IsOpen = true,
			Title = "Replace a Card",
			IsSelectable = true,
		});

		Assert.False(ClimbOverviewViewService.IsOverviewOpen(entityManager));
		Assert.True(ClimbOverviewViewService.IsUnrelatedModalOpen(entityManager));
	}

	[Fact]
	public void Right_shoulder_opens_overview_when_closed()
	{
		var entityManager = BuildClimbWorld(out var system);
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(evt =>
		{
			if (evt.Title == ClimbOverviewViewService.OverviewTitle) opens++;
		});

		PressRightShoulder(entityManager, system);

		Assert.Equal(1, opens);
	}

	[Fact]
	public void Right_shoulder_closes_overview_when_open()
	{
		var entityManager = BuildClimbWorld(out var system);
		SetOpenOverviewModal(entityManager);
		int closes = 0;
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(1, closes);
	}

	[Fact]
	public void Right_shoulder_does_nothing_when_unrelated_modal_open()
	{
		var entityManager = BuildClimbWorld(out var system);
		var modalEntity = entityManager.CreateEntity("CardListModal");
		entityManager.AddComponent(modalEntity, new CardListModal
		{
			IsOpen = true,
			Title = "Replace a Card",
			IsSelectable = true,
		});
		int opens = 0;
		int closes = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(0, opens);
		Assert.Equal(0, closes);
	}

	[Fact]
	public void Right_shoulder_does_nothing_when_not_climb_scene()
	{
		var entityManager = BuildClimbWorld(out var system);
		entityManager.GetEntitiesWithComponent<SceneState>().Single().GetComponent<SceneState>().Current = SceneId.Battle;
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(0, opens);
	}

	[Fact]
	public void Right_shoulder_does_nothing_when_pause_menu_active()
	{
		var entityManager = BuildClimbWorld(out var system);
		var pause = entityManager.CreateEntity("PauseMenu");
		entityManager.AddComponent(pause, new PauseMenuOverlay { Phase = PauseMenuPhase.Visible });
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(0, opens);
	}

	[Fact]
	public void Right_shoulder_does_nothing_when_prevent_clicking_while_closed()
	{
		var entityManager = BuildClimbWorld(out var system);
		StateSingleton.PreventClicking = true;
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(0, opens);
	}

	[Fact]
	public void Right_shoulder_closes_overview_even_when_prevent_clicking()
	{
		var entityManager = BuildClimbWorld(out var system);
		SetOpenOverviewModal(entityManager);
		StateSingleton.PreventClicking = true;
		int closes = 0;
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(1, closes);
	}

	private static EntityManager BuildClimbWorld(out ClimbOverviewGamepadInputSystem system)
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("SceneState");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Climb });

		var deckEntity = entityManager.CreateEntity("Deck");
		entityManager.AddComponent(deckEntity, new Deck());

		var inputEntity = entityManager.CreateEntity("PlayerInputState");
		entityManager.AddComponent(inputEntity, new PlayerInputState());

		system = new ClimbOverviewGamepadInputSystem(entityManager);
		return entityManager;
	}

	private static void SetOpenOverviewModal(EntityManager entityManager)
	{
		var modalEntity = entityManager.CreateEntity("CardListModal");
		entityManager.AddComponent(modalEntity, new CardListModal
		{
			IsOpen = true,
			Title = ClimbOverviewViewService.OverviewTitle,
			Mode = CardListModalMode.Inventory,
		});
	}

	private static void PressRightShoulder(
		EntityManager entityManager,
		ClimbOverviewGamepadInputSystem system)
	{
		var inputEntity = entityManager.GetEntitiesWithComponent<PlayerInputState>().Single();
		inputEntity.GetComponent<PlayerInputState>().Frame = GamepadFrame(
			pressed: PlayerInputFrame.Mask(PlayerButton.RightShoulder));

		system.Update(new GameTime());
	}

	private static PlayerInputFrame GamepadFrame(
		PlayerButtonMask down = PlayerButtonMask.None,
		PlayerButtonMask pressed = PlayerButtonMask.None)
	{
		return new PlayerInputFrame(
			1,
			true,
			true,
			PlayerInputDevice.Gamepad,
			PlayerInputDevice.Gamepad,
			GamepadGlyphStyle.Xbox,
			Vector2.Zero,
			Vector2.Zero,
			0f,
			Vector2.Zero,
			Vector2.Zero,
			0f,
			0f,
			down,
			pressed,
			PlayerButtonMask.None);
	}
}
