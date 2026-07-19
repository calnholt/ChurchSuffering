using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class ClimbV2MotionSystemTests
{
	[Fact]
	public void ActiveMotion_suppresses_clicks_without_clearing_hover_then_restores_input()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("choice");
		entityManager.AddComponent(entity, new OwnedByScene { Scene = SceneId.Climb });
		entityManager.AddComponent(entity, new ClimbSlotPresentation());
		entityManager.AddComponent(entity, new UIElement { IsInteractable = true, IsHovered = true });
		entityManager.AddComponent(entity, new ClimbV2ChoiceMotion { Phase = ClimbV2MotionPhase.Entering });
		var system = new ClimbV2MotionSystem(entityManager);

		system.Update(Frame(0f));

		Assert.False(entity.GetComponent<UIElement>().IsInteractable);
		Assert.True(entity.GetComponent<UIElement>().IsHovered);
		Assert.NotNull(entity.GetComponent<ClimbV2InputSuppression>());

		entity.GetComponent<ClimbV2ChoiceMotion>().Phase = ClimbV2MotionPhase.Settled;
		system.Update(Frame(0f));

		Assert.True(entity.GetComponent<UIElement>().IsInteractable);
		Assert.True(entity.GetComponent<UIElement>().IsHovered);
		Assert.Null(entity.GetComponent<ClimbV2InputSuppression>());
	}

	[Fact]
	public void Purchase_motion_completes_and_allows_the_layout_to_adopt_new_content()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("shop");
		entityManager.AddComponent(entity, new ClimbSlotPresentation { Kind = ClimbSlotKind.Shop });
		entityManager.AddComponent(entity, new ClimbShopItemPresentation());
		entityManager.AddComponent(entity, new ClimbV2ChoiceMotion
		{
			Phase = ClimbV2MotionPhase.Purchasing,
			Initialized = true,
		});
		var system = new ClimbV2MotionSystem(entityManager);

		system.Update(Frame(1f));

		var motion = entity.GetComponent<ClimbV2ChoiceMotion>();
		Assert.Equal(ClimbV2MotionPhase.Settled, motion.Phase);
		Assert.False(motion.Initialized);
		Assert.Equal(Vector2.Zero, motion.Offset);
		Assert.Equal(1f, motion.Opacity);
	}

	[Fact]
	public void Replacement_presentation_is_deferred_until_the_outgoing_choice_finishes_exiting()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("encounter");
		entityManager.AddComponent(entity, new ClimbSlotPresentation { Kind = ClimbSlotKind.Encounter });
		entityManager.AddComponent(entity, new ClimbEncounterPresentation());
		var motion = new ClimbV2ChoiceMotion
		{
			Phase = ClimbV2MotionPhase.Settled,
			Fingerprint = "old",
			Initialized = true,
		};
		entityManager.AddComponent(entity, motion);

		Assert.False(ClimbV2LayoutSystem.TryAdoptPresentation(motion, "new", 0f));
		Assert.Equal(ClimbV2MotionPhase.AshesExiting, motion.Phase);
		Assert.Equal("old", motion.Fingerprint);

		var system = new ClimbV2MotionSystem(entityManager);
		system.Update(Frame(1.1f));

		Assert.Equal(ClimbV2MotionPhase.Entering, motion.Phase);
		Assert.True(ClimbV2LayoutSystem.TryAdoptPresentation(motion, "new", 0f));
		Assert.Equal("new", motion.Fingerprint);
	}

	[Fact]
	public void Shop_refresh_keeps_the_outgoing_fingerprint_through_its_staggered_exit()
	{
		var motion = new ClimbV2ChoiceMotion
		{
			Phase = ClimbV2MotionPhase.Settled,
			Fingerprint = "old-shop-item",
			Initialized = true,
		};

		Assert.False(ClimbV2LayoutSystem.TryAdoptPresentation(motion, "new-shop-item", 0.3f));
		Assert.Equal("old-shop-item", motion.Fingerprint);
		Assert.Equal(0.3f, motion.DelaySeconds);

		Assert.False(ClimbV2LayoutSystem.TryAdoptPresentation(motion, "new-shop-item", 0.3f));
		Assert.Equal(ClimbV2MotionPhase.AshesExiting, motion.Phase);
		Assert.Equal("old-shop-item", motion.Fingerprint);
	}

	[Fact]
	public void Encounter_battle_transition_is_the_only_battle_return_that_captures_climb_presentations()
	{
		var queued = new QueuedEvents { IsClimbEncounter = true };

		Assert.True(ClimbV2LayoutSystem.ShouldCaptureReturnSnapshot(
			new SceneDeactivating { From = SceneId.Climb, To = SceneId.Battle },
			queued));
		Assert.False(ClimbV2LayoutSystem.ShouldCaptureReturnSnapshot(
			new SceneDeactivating { From = SceneId.Climb, To = SceneId.WayStation },
			queued));
		Assert.False(ClimbV2LayoutSystem.ShouldCaptureReturnSnapshot(
			new SceneDeactivating { From = SceneId.Climb, To = SceneId.Battle },
			new QueuedEvents()));
	}

	[Fact]
	public void Turnover_waits_for_requested_upgrade_and_encounter_resource_animations()
	{
		EventManager.Clear();
		var layout = new ClimbV2LayoutSystem(new EntityManager());
		try
		{
			EventManager.Publish(new ClimbCardUpgradeAnimationRequested
			{
				BaseCardKey = "strike|White",
				UpgradedCardKey = "strike|White|Upgraded",
				DelayClimbTurnoverUntilComplete = true,
			});
			EventManager.Publish(new ClimbResourceAcquisitionAnimationRequested
			{
				Resources = new ClimbResourceSave { red = 1 },
				DelayClimbTurnoverUntilComplete = true,
			});

			Assert.True(layout.IsTurnoverHeld);
			EventManager.Publish(new ClimbCardUpgradeAnimationCompleted { ReleasesClimbTurnover = true });
			Assert.True(layout.IsTurnoverHeld);
			EventManager.Publish(new ClimbResourceAcquisitionAnimationCompleted { ReleasesClimbTurnover = true });
			Assert.False(layout.IsTurnoverHeld);
		}
		finally
		{
			layout.Shutdown();
			EventManager.Clear();
		}
	}

	[Fact]
	public void Pending_character_event_remains_presented_until_resolution_completes()
	{
		Assert.True(ClimbV2LayoutSystem.IsPresentedEvent(new ClimbEventSlotSave { status = ClimbEventStatus.Active }));
		Assert.True(ClimbV2LayoutSystem.IsPresentedEvent(new ClimbEventSlotSave { status = ClimbEventStatus.Pending }));
		Assert.False(ClimbV2LayoutSystem.IsPresentedEvent(new ClimbEventSlotSave { status = ClimbEventStatus.Completed }));
		Assert.False(ClimbV2LayoutSystem.IsPresentedEvent(new ClimbEventSlotSave { status = ClimbEventStatus.Expired }));
	}

	[Fact]
	public void V2_layout_keeps_empty_event_space_fixed()
	{
		Assert.Equal(new Rectangle(36, 120, 315, 561), ClimbV2LayoutSystem.ShopBounds);
		Assert.Equal(new Rectangle(365, 110, 1190, 915), ClimbV2LayoutSystem.EncounterBounds);
		Assert.Equal(new Rectangle(1569, 135, 315, 465), ClimbV2LayoutSystem.EventBounds);
	}

	private static GameTime Frame(float seconds) => new(
		TimeSpan.FromSeconds(seconds),
		TimeSpan.FromSeconds(seconds));
}
