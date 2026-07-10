using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class BoosterPackOpeningInputTests : IDisposable
{
	public BoosterPackOpeningInputTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Close_ui_event_publishes_one_overlay_close_event()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("CloseTarget");
		int closeEvents = 0;
		EventManager.Subscribe<CloseBoosterPackOpeningOverlayEvent>(_ => closeEvents++);

		UIElementEventDelegateService.HandleEvent(
			UIElementEventType.BoosterPackOpeningClose,
			entity,
			entityManager);

		Assert.Equal(1, closeEvents);
	}

	[Fact]
	public void None_ui_event_publishes_no_overlay_close_event()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("BlockedTarget");
		int closeEvents = 0;
		EventManager.Subscribe<CloseBoosterPackOpeningOverlayEvent>(_ => closeEvents++);

		UIElementEventDelegateService.HandleEvent(UIElementEventType.None, entity, entityManager);

		Assert.Equal(0, closeEvents);
	}

	[Fact]
	public void Preview_interaction_is_hidden_and_neutral_before_settlement()
	{
		var ui = new UIElement { EventType = UIElementEventType.CardClicked };

		BoosterPackOpeningDisplaySystem.ConfigurePreviewInteraction(
			ui,
			settled: false,
			canDismiss: false,
			new Rectangle(10, 20, 30, 40));

		Assert.True(ui.IsHidden);
		Assert.False(ui.IsInteractable);
		Assert.Equal(Rectangle.Empty, ui.Bounds);
		Assert.Equal(UIElementEventType.None, ui.EventType);
		Assert.Equal(UIElementEventType.None, ui.SecondaryEventType);
	}

	[Fact]
	public void Settled_preview_swallows_clicks_until_ready_then_closes()
	{
		var ui = new UIElement();
		var bounds = new Rectangle(10, 20, 30, 40);

		BoosterPackOpeningDisplaySystem.ConfigurePreviewInteraction(
			ui,
			settled: true,
			canDismiss: false,
			bounds);
		Assert.False(ui.IsHidden);
		Assert.True(ui.IsInteractable);
		Assert.Equal(bounds, ui.Bounds);
		Assert.Equal(UIElementEventType.None, ui.EventType);

		BoosterPackOpeningDisplaySystem.ConfigurePreviewInteraction(
			ui,
			settled: true,
			canDismiss: true,
			bounds);
		Assert.Equal(UIElementEventType.BoosterPackOpeningClose, ui.EventType);
		Assert.Equal(UIElementEventType.None, ui.SecondaryEventType);
	}
}
