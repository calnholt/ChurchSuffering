using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class ModalAnimationSystemTests
{
	[Fact]
	public void Hidden_modal_clears_stale_root_overlay_input()
	{
		var entityManager = new EntityManager();
		var modal = CreateModalRoot(entityManager, ModalAnimationPhase.Hidden);
		var system = new ModalAnimationSystem(entityManager);

		system.Update(new GameTime());

		var ui = modal.GetComponent<UIElement>();
		var context = modal.GetComponent<InputContext>();
		Assert.False(context.IsActive);
		Assert.False(ui.IsInteractable);
		Assert.True(ui.IsHidden);
		Assert.Equal(Rectangle.Empty, ui.Bounds);
		Assert.Equal(UILayerType.Default, ui.LayerType);
		Assert.False(ui.IsHovered);
		Assert.False(ui.IsClicked);
		Assert.Equal(
			InputContextIds.Gameplay,
			InputContextResolver.ResolveCursorContext(entityManager, new Vector2(100, 100)));
	}

	[Fact]
	public void Completed_exit_restores_members_and_clears_root_overlay_input()
	{
		var entityManager = new EntityManager();
		var modal = CreateModalRoot(entityManager, ModalAnimationPhase.Exiting);
		var animation = modal.GetComponent<ModalAnimation>();
		animation.ExitDurationSeconds = 0.1f;
		animation.ExitSequence = 1;

		var member = entityManager.CreateEntity("ModalMember");
		entityManager.AddComponent(member, new UIElement
		{
			Bounds = new Rectangle(10, 10, 20, 20),
			IsInteractable = true,
			IsHidden = true,
			LayerType = UILayerType.Overlay,
		});
		member.GetComponent<UIElement>().Suppress();
		entityManager.AddComponent(member, new InputContextMember { ContextId = "overlay.test" });
		entityManager.AddComponent(member, new ModalInputSuppression { ContextId = "overlay.test" });

		var system = new ModalAnimationSystem(entityManager);
		system.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.2)));

		var ui = modal.GetComponent<UIElement>();
		var context = modal.GetComponent<InputContext>();
		Assert.Equal(ModalAnimationPhase.Hidden, animation.Phase);
		Assert.Equal(1, animation.CompletedExitSequence);
		Assert.False(context.IsActive);
		Assert.False(ui.IsInteractable);
		Assert.True(ui.IsHidden);
		Assert.Equal(Rectangle.Empty, ui.Bounds);
		Assert.Equal(UILayerType.Default, ui.LayerType);
		Assert.True(member.GetComponent<UIElement>().IsInteractable);
		Assert.Null(member.GetComponent<ModalInputSuppression>());
		Assert.Equal(
			InputContextIds.Gameplay,
			InputContextResolver.ResolveCommandContext(entityManager));
	}

	private static Entity CreateModalRoot(EntityManager entityManager, ModalAnimationPhase phase)
	{
		var modal = entityManager.CreateEntity("ModalRoot");
		entityManager.AddComponent(modal, new UIElement
		{
			Bounds = new Rectangle(0, 0, 1920, 1080),
			IsInteractable = true,
			IsHidden = false,
			IsHovered = true,
			IsClicked = true,
			LayerType = UILayerType.Overlay,
		});
		entityManager.AddComponent(modal, new InputContext
		{
			Id = "overlay.test",
			Priority = 720,
			IsActive = true,
		});
		entityManager.AddComponent(modal, new InputContextMember { ContextId = "overlay.test" });
		entityManager.AddComponent(modal, new ModalAnimation
		{
			Phase = phase,
			RequestedVisible = false,
			InputContextId = "overlay.test",
		});
		return modal;
	}
}
