using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class RewardModalGamepadInputTests : IDisposable
{
	public RewardModalGamepadInputTests()
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
	public void Right_shoulder_opens_overview_when_reward_visible_on_climb_with_prevent_clicking()
	{
		var entityManager = BuildWorld(SceneId.Climb, out var system);
		SetInteractiveRewardOverlay(entityManager);
		StateSingleton.PreventClicking = true;
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(evt =>
		{
			if (evt.Title == ClimbOverviewViewService.OverviewTitle
				&& evt.Mode == CardListModalMode.Inventory)
			{
				opens++;
			}
		});

		PressRightShoulder(entityManager, system);

		Assert.Equal(1, opens);
	}

	[Fact]
	public void Right_shoulder_opens_overview_when_reward_visible_on_battle()
	{
		var entityManager = BuildWorld(SceneId.Battle, out var system);
		SetInteractiveRewardOverlay(entityManager);
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(evt =>
		{
			if (evt.Title == ClimbOverviewViewService.OverviewTitle
				&& evt.Mode == CardListModalMode.Inventory)
			{
				opens++;
			}
		});

		PressRightShoulder(entityManager, system);

		Assert.Equal(1, opens);
	}

	[Fact]
	public void Right_shoulder_closes_overview_when_open_over_reward()
	{
		var entityManager = BuildWorld(SceneId.Climb, out var system);
		SetInteractiveRewardOverlay(entityManager);
		SetOpenOverviewModal(entityManager);
		int closes = 0;
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(1, closes);
	}

	[Fact]
	public void Right_shoulder_does_nothing_when_reward_entering()
	{
		var entityManager = BuildWorld(SceneId.Climb, out var system);
		SetRewardOverlay(entityManager, QuestRewardPresentationPhase.Entering);
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(0, opens);
	}

	[Fact]
	public void Right_shoulder_does_nothing_when_reward_claiming()
	{
		var entityManager = BuildWorld(SceneId.Climb, out var system);
		SetRewardOverlay(entityManager, QuestRewardPresentationPhase.Claiming, dismissInProgress: true);
		int opens = 0;
		int closes = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(0, opens);
		Assert.Equal(0, closes);
	}

	[Fact]
	public void Right_shoulder_does_nothing_when_reward_hidden()
	{
		var entityManager = BuildWorld(SceneId.Climb, out var system);
		SetRewardOverlay(entityManager, QuestRewardPresentationPhase.Hidden, isOpen: false);
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);

		PressRightShoulder(entityManager, system);

		Assert.Equal(0, opens);
	}

	[Fact]
	public void IsInteractiveOverlayOpen_requires_visible_non_preview()
	{
		var entityManager = BuildWorld(SceneId.Climb, out _);
		Assert.False(RewardModalDisplaySystem.IsInteractiveOverlayOpen(entityManager));

		SetInteractiveRewardOverlay(entityManager);
		Assert.True(RewardModalDisplaySystem.IsInteractiveOverlayOpen(entityManager));

		SetRewardOverlay(entityManager, QuestRewardPresentationPhase.Visible, isPreviewOnly: true);
		Assert.False(RewardModalDisplaySystem.IsInteractiveOverlayOpen(entityManager));
	}

	[Fact]
	public void Close_overlay_closes_open_climb_overview()
	{
		var entityManager = BuildWorld(SceneId.Climb, out _);
		SetInteractiveRewardOverlay(entityManager);
		SetOpenOverviewModal(entityManager);

		int closes = 0;
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		// Mirror CloseOverlay cleanup path used by RewardModalDisplaySystem.
		if (ClimbOverviewViewService.IsOverviewOpen(entityManager))
			ClimbOverviewViewService.Close(entityManager);

		Assert.Equal(1, closes);
	}

	private static EntityManager BuildWorld(SceneId sceneId, out RewardModalGamepadInputSystem system)
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("SceneState");
		entityManager.AddComponent(scene, new SceneState { Current = sceneId });

		var deckEntity = entityManager.CreateEntity("Deck");
		entityManager.AddComponent(deckEntity, new Deck());

		var inputEntity = entityManager.CreateEntity("PlayerInputState");
		entityManager.AddComponent(inputEntity, new PlayerInputState());

		system = new RewardModalGamepadInputSystem(entityManager);
		return entityManager;
	}

	private static void SetInteractiveRewardOverlay(EntityManager entityManager) =>
		SetRewardOverlay(entityManager, QuestRewardPresentationPhase.Visible);

	private static void SetRewardOverlay(
		EntityManager entityManager,
		QuestRewardPresentationPhase phase,
		bool isOpen = true,
		bool isPreviewOnly = false,
		bool dismissInProgress = false)
	{
		var overlay = entityManager.GetEntity("QuestRewardOverlay")
			?? entityManager.CreateEntity("QuestRewardOverlay");
		var state = overlay.GetComponent<QuestRewardOverlayState>();
		if (state == null)
		{
			state = new QuestRewardOverlayState();
			entityManager.AddComponent(overlay, state);
		}

		state.IsOpen = isOpen;
		state.IsPreviewOnly = isPreviewOnly;
		state.DismissInProgress = dismissInProgress;
		state.Phase = phase;
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
		RewardModalGamepadInputSystem system)
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
