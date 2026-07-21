using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class BattlePileGamepadInputTests : IDisposable
{
	public BattlePileGamepadInputTests()
	{
		EventManager.Clear();
		StateSingleton.IsActive = false;
	}

	public void Dispose()
	{
		EventManager.Clear();
		StateSingleton.IsActive = false;
	}

	[Fact]
	public void IsDrawPileVisible_is_false_during_guided_tutorial_except_section_8()
	{
		var entityManager = new EntityManager();
		var tutorial = entityManager.CreateEntity("GuidedTutorial");
		entityManager.AddComponent(tutorial, new GuidedTutorial { Section = 1 });

		Assert.False(PileDisplayVisibilityService.IsDrawPileVisible(entityManager));

		tutorial.GetComponent<GuidedTutorial>().Section = 8;
		Assert.True(PileDisplayVisibilityService.IsDrawPileVisible(entityManager));
	}

	[Fact]
	public void IsDrawPileVisible_is_true_outside_guided_tutorial()
	{
		var entityManager = new EntityManager();
		Assert.True(PileDisplayVisibilityService.IsDrawPileVisible(entityManager));
	}

	[Fact]
	public void IsDiscardPileVisible_is_false_during_guided_tutorial()
	{
		var entityManager = new EntityManager();
		var tutorial = entityManager.CreateEntity("GuidedTutorial");
		entityManager.AddComponent(tutorial, new GuidedTutorial { Section = 8 });

		Assert.False(PileDisplayVisibilityService.IsDiscardPileVisible(entityManager));
	}

	[Fact]
	public void OpenDrawPile_publishes_modal_event_with_draw_title()
	{
		var entityManager = BuildBattleWorld(out _);
		OpenCardListModalEvent opened = null;
		EventManager.Subscribe<OpenCardListModalEvent>(evt => opened = evt);

		PileViewService.OpenDrawPile(entityManager);

		Assert.NotNull(opened);
		Assert.Equal(PileViewService.DrawPileTitle, opened.Title);
		Assert.Equal(CardListModalMode.CardList, opened.Mode);
	}

	[Fact]
	public void OpenDiscardPile_publishes_modal_event_with_discard_title()
	{
		var entityManager = BuildBattleWorld(out _);
		OpenCardListModalEvent opened = null;
		EventManager.Subscribe<OpenCardListModalEvent>(evt => opened = evt);

		PileViewService.OpenDiscardPile(entityManager);

		Assert.NotNull(opened);
		Assert.Equal(PileViewService.DiscardPileTitle, opened.Title);
	}

	[Fact]
	public void TryGetOpenPileView_returns_false_for_unrelated_modal()
	{
		var entityManager = BuildBattleWorld(out _);
		var modalEntity = entityManager.CreateEntity("CardListModal");
		entityManager.AddComponent(modalEntity, new CardListModal
		{
			IsOpen = true,
			Title = "Choose a card",
			IsSelectable = true,
		});

		Assert.False(PileViewService.TryGetOpenPileView(entityManager, out _));
		Assert.True(PileViewService.IsUnrelatedModalOpen(entityManager));
	}

	[Fact]
	public void Left_shoulder_opens_discard_when_visible_and_closed()
	{
		var entityManager = BuildBattleWorld(out var system);
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(evt =>
		{
			if (evt.Title == PileViewService.DiscardPileTitle) opens++;
		});

		PressShoulder(entityManager, system, leftShoulder: true);

		Assert.Equal(1, opens);
	}

	[Fact]
	public void Left_shoulder_closes_discard_when_discard_modal_open()
	{
		var entityManager = BuildBattleWorld(out var system);
		SetOpenPileModal(entityManager, PileViewService.DiscardPileTitle);
		int closes = 0;
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		PressShoulder(entityManager, system, leftShoulder: true);

		Assert.Equal(1, closes);
	}

	[Fact]
	public void Right_shoulder_opens_draw_when_visible_and_closed()
	{
		var entityManager = BuildBattleWorld(out var system);
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(evt =>
		{
			if (evt.Title == PileViewService.DrawPileTitle) opens++;
		});

		PressShoulder(entityManager, system, leftShoulder: false);

		Assert.Equal(1, opens);
	}

	[Fact]
	public void Right_shoulder_closes_draw_when_draw_modal_open()
	{
		var entityManager = BuildBattleWorld(out var system);
		SetOpenPileModal(entityManager, PileViewService.DrawPileTitle);
		int closes = 0;
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		PressShoulder(entityManager, system, leftShoulder: false);

		Assert.Equal(1, closes);
	}

	[Fact]
	public void Right_shoulder_switches_from_discard_to_draw()
	{
		var entityManager = BuildBattleWorld(out var system);
		SetOpenPileModal(entityManager, PileViewService.DiscardPileTitle);
		int closes = 0;
		string openedTitle = null;
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);
		EventManager.Subscribe<OpenCardListModalEvent>(evt => openedTitle = evt.Title);

		PressShoulder(entityManager, system, leftShoulder: false);

		Assert.Equal(1, closes);
		Assert.Equal(PileViewService.DrawPileTitle, openedTitle);
	}

	[Fact]
	public void Left_shoulder_switches_from_draw_to_discard()
	{
		var entityManager = BuildBattleWorld(out var system);
		SetOpenPileModal(entityManager, PileViewService.DrawPileTitle);
		int closes = 0;
		string openedTitle = null;
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);
		EventManager.Subscribe<OpenCardListModalEvent>(evt => openedTitle = evt.Title);

		PressShoulder(entityManager, system, leftShoulder: true);

		Assert.Equal(1, closes);
		Assert.Equal(PileViewService.DiscardPileTitle, openedTitle);
	}

	[Fact]
	public void Left_shoulder_does_nothing_when_discard_not_visible()
	{
		var entityManager = BuildBattleWorld(out var system);
		var tutorial = entityManager.CreateEntity("GuidedTutorial");
		entityManager.AddComponent(tutorial, new GuidedTutorial { Section = 8 });
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);

		PressShoulder(entityManager, system, leftShoulder: true);

		Assert.Equal(0, opens);
	}

	[Fact]
	public void Right_shoulder_does_nothing_when_draw_not_visible()
	{
		var entityManager = BuildBattleWorld(out var system);
		var tutorial = entityManager.CreateEntity("GuidedTutorial");
		entityManager.AddComponent(tutorial, new GuidedTutorial { Section = 1 });
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);

		PressShoulder(entityManager, system, leftShoulder: false);

		Assert.Equal(0, opens);
	}

	[Fact]
	public void Shoulder_buttons_do_nothing_when_unrelated_modal_open()
	{
		var entityManager = BuildBattleWorld(out var system);
		var modalEntity = entityManager.CreateEntity("CardListModal");
		entityManager.AddComponent(modalEntity, new CardListModal
		{
			IsOpen = true,
			Title = "Choose a card",
			IsSelectable = true,
		});
		int opens = 0;
		int closes = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);
		EventManager.Subscribe<CloseCardListModalEvent>(_ => closes++);

		PressShoulder(entityManager, system, leftShoulder: true);
		PressShoulder(entityManager, system, leftShoulder: false);

		Assert.Equal(0, opens);
		Assert.Equal(0, closes);
	}

	[Fact]
	public void Shoulder_buttons_do_nothing_when_battle_input_frozen()
	{
		var entityManager = BuildBattleWorld(out var system);
		var phaseEntity = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phaseEntity, new PhaseState { BattleAnimationActive = true });
		int opens = 0;
		EventManager.Subscribe<OpenCardListModalEvent>(_ => opens++);

		PressShoulder(entityManager, system, leftShoulder: true);
		PressShoulder(entityManager, system, leftShoulder: false);

		Assert.Equal(0, opens);
	}

	private static EntityManager BuildBattleWorld(out BattlePileGamepadInputSystem system)
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("SceneState");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Battle });

		var deckEntity = entityManager.CreateEntity("Deck");
		entityManager.AddComponent(deckEntity, new Deck());

		var inputEntity = entityManager.CreateEntity("PlayerInputState");
		entityManager.AddComponent(inputEntity, new PlayerInputState());

		system = new BattlePileGamepadInputSystem(entityManager);
		return entityManager;
	}

	private static void SetOpenPileModal(EntityManager entityManager, string title)
	{
		var modalEntity = entityManager.CreateEntity("CardListModal");
		entityManager.AddComponent(modalEntity, new CardListModal
		{
			IsOpen = true,
			Title = title,
		});
	}

	private static void PressShoulder(
		EntityManager entityManager,
		BattlePileGamepadInputSystem system,
		bool leftShoulder)
	{
		var button = leftShoulder ? PlayerButton.LeftShoulder : PlayerButton.RightShoulder;
		var inputEntity = entityManager.GetEntitiesWithComponent<PlayerInputState>().Single();
		inputEntity.GetComponent<PlayerInputState>().Frame = GamepadFrame(
			pressed: PlayerInputFrame.Mask(button));

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
