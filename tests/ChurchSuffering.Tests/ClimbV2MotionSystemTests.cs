using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

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
	public void Encounter_entrance_restores_ashes_colors_gradually_while_motion_settles()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("encounter");
		entityManager.AddComponent(entity, new ClimbSlotPresentation { Kind = ClimbSlotKind.Encounter });
		entityManager.AddComponent(entity, new ClimbEncounterPresentation());
		entityManager.AddComponent(entity, new ClimbV2ChoiceMotion
		{
			Phase = ClimbV2MotionPhase.Entering,
			Grayscale = 1f,
			Sepia = 1f,
		});
		var system = new ClimbV2MotionSystem(entityManager);

		system.Update(Frame(0.36f));

		var motion = entity.GetComponent<ClimbV2ChoiceMotion>();
		Assert.Equal(ClimbV2MotionPhase.Entering, motion.Phase);
		Assert.Equal(0.5f, motion.Grayscale, 3);
		Assert.Equal(0.5f, motion.Sepia, 3);
		Assert.Equal(0.79f, motion.Brightness, 3);
		Assert.Equal(2.5f, motion.Blur, 3);

		system.Update(Frame(0.36f));

		Assert.Equal(ClimbV2MotionPhase.Settled, motion.Phase);
		Assert.Equal(0f, motion.Grayscale);
		Assert.Equal(0f, motion.Sepia);
		Assert.Equal(1f, motion.Brightness);
		Assert.Equal(0f, motion.Blur);
	}

	[Fact]
	public void Purchase_motion_holds_the_departed_item_transparent_until_layout_reconciliation()
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
		Assert.Equal(ClimbV2MotionPhase.AwaitingPurchaseReconciliation, motion.Phase);
		Assert.True(motion.Initialized);
		Assert.Equal(new Vector2(105f, 0f), motion.Offset);
		Assert.Equal(0f, motion.Opacity);
		Assert.True(ClimbV2LayoutSystem.TryAdoptPresentation(motion, "sold", 0f));

		ClimbV2LayoutSystem.ReconcilePurchasedPresentation(motion, hidden: true);

		Assert.Equal(ClimbV2MotionPhase.Settled, motion.Phase);
		Assert.Equal(0f, motion.Opacity);
	}

	[Fact]
	public void Refreshed_shop_offer_enters_after_purchase_reconciliation()
	{
		var motion = new ClimbV2ChoiceMotion
		{
			Phase = ClimbV2MotionPhase.AwaitingPurchaseReconciliation,
			Opacity = 0f,
			Fingerprint = "sold",
			Initialized = true,
		};

		Assert.True(ClimbV2LayoutSystem.TryAdoptPresentation(motion, "replacement", 0f));
		ClimbV2LayoutSystem.ReconcilePurchasedPresentation(motion, hidden: false);

		Assert.Equal(ClimbV2MotionPhase.Entering, motion.Phase);
		Assert.Equal("replacement", motion.Fingerprint);
		Assert.Equal(0f, motion.Opacity);
		Assert.Equal(new Vector2(-105f, 0f), motion.Offset);
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
	public void Turnover_waits_for_requested_card_and_encounter_resource_animations()
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
			EventManager.Publish(new ClimbCardBoonAnimationRequested
			{
				CardKey = "strike|White",
				DelayClimbTurnoverUntilComplete = true,
			});

			Assert.True(layout.IsTurnoverHeld);
			EventManager.Publish(new ClimbCardUpgradeAnimationCompleted { ReleasesClimbTurnover = true });
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
		Assert.Equal(new Rectangle(36, 120, 315, 674), ClimbV2LayoutSystem.ShopBounds);
		Assert.Equal(new Rectangle(365, 110, 1190, 915), ClimbV2LayoutSystem.EncounterBounds);
		Assert.Equal(new Rectangle(1569, 135, 315, 465), ClimbV2LayoutSystem.EventBounds);
	}

	[Fact]
	public void V2_layout_configures_buttons_and_attached_rails_with_matching_ui_parallax()
	{
		EventManager.Clear();
		SaveCache.StartNewRun();
		var entityManager = BuildClimbEntityManager();
		var layout = new ClimbV2LayoutSystem(entityManager);
		try
		{
			layout.Update(Frame(0f));
			var root = entityManager.GetEntity(ClimbV2LayoutSystem.RootName);
			var buttons = entityManager.GetAllEntities()
				.Where(entity => entity.GetComponent<ClimbShopItemPresentation>() != null
					|| entity.GetComponent<ClimbEncounterPresentation>() != null
					|| entity.GetComponent<ClimbEventPresentation>() != null
					|| entity.GetComponent<ClimbOverviewButton>() != null)
				.ToList();
			var rails = entityManager.GetEntitiesWithComponent<ClimbChoiceRailPresentation>().ToList();

			Assert.NotEmpty(buttons);
			Assert.NotEmpty(rails);
			foreach (var entity in buttons.Concat(rails))
			{
				AssertParallax(ParallaxLayer.GetUIParallaxLayer(), entity.GetComponent<ParallaxLayer>());
				Assert.Same(root, entity.GetComponent<ParentTransform>()?.Parent);
				Assert.Equal(Point.Zero, entity.GetComponent<UIElement>().Bounds.Location);
			}

			var visible = buttons.First(entity => entity.GetComponent<UIElement>()?.Bounds.Width > 0);
			var ui = visible.GetComponent<UIElement>();
			Rectangle before = TransformResolverService.ResolveUIBounds(entityManager, visible, ui);
			var delta = new Vector2(7f, -4f);
			visible.GetComponent<Transform>().Position += delta;
			Rectangle after = TransformResolverService.ResolveUIBounds(entityManager, visible, ui);
			Assert.Equal(new Rectangle(before.X + 7, before.Y - 4, before.Width, before.Height), after);
		}
		finally
		{
			layout.Shutdown();
			EventManager.Clear();
		}
	}

	[Fact]
	public void Choice_hover_inflates_the_button_and_rail_by_the_same_geometry()
	{
		var rect = new Rectangle(100, 200, 300, 42);

		Assert.Equal(new Rectangle(98, 199, 304, 44), ClimbV2Draw.ApplyChoiceHover(rect, ClimbSlotKind.Shop, hovered: true));
		Assert.Equal(new Rectangle(98, 198, 304, 46), ClimbV2Draw.ApplyChoiceHover(rect, ClimbSlotKind.Encounter, hovered: true));
		Assert.Equal(rect, ClimbV2Draw.ApplyChoiceHover(rect, ClimbSlotKind.Event, hovered: false));
	}

	[Fact]
	public void Expiry_preview_loops_opacity_and_grayscale_then_restores()
	{
		var entityManager = new EntityManager();
		var root = entityManager.CreateEntity(ClimbV2LayoutSystem.RootName);
		var preview = new ClimbPreviewState { IsActive = true, SourceSlotId = "selected" };
		preview.WouldVanishSlotIds.Add("expiring");
		entityManager.AddComponent(root, preview);
		var choice = entityManager.CreateEntity("choice");
		entityManager.AddComponent(choice, new ClimbSlotPresentation { SlotId = "expiring" });
		var system = new ClimbChoicePreviewDisplaySystem(entityManager, null, null);

		system.Update(Frame(system.PulseSeconds / 2f));

		var visual = choice.GetComponent<ClimbChoiceExpiryPreviewPresentation>();
		Assert.Equal(1f, visual.Strength, 3);
		Assert.Equal(system.MinimumOpacity, visual.OpacityMultiplier, 3);
		Assert.Equal(system.MaximumGrayscale, visual.Grayscale, 3);

		preview.Clear();
		system.Update(Frame(system.RestoreSeconds / 2f));
		Assert.Equal(0.5f, visual.Strength, 3);
		system.Update(Frame(system.RestoreSeconds / 2f));
		Assert.Equal(0f, visual.Strength, 3);
		Assert.Equal(1f, visual.OpacityMultiplier, 3);
		Assert.Equal(0f, visual.Grayscale, 3);
	}

	[Fact]
	public void Choice_opacity_can_be_deferred_to_the_flattened_composite()
	{
		var entityManager = new EntityManager();
		var choice = entityManager.CreateEntity("choice");
		entityManager.AddComponent(choice, new ClimbV2ChoiceMotion { Opacity = 0.8f });

		var (_, compositeAlpha) = ClimbV2Draw.Motion(choice);
		var (_, fallbackAlpha) = ClimbV2Draw.Motion(choice, 0.35f);

		Assert.Equal(0.8f, compositeAlpha, 3);
		Assert.Equal(0.28f, fallbackAlpha, 3);
	}

	[Fact]
	public void Hazard_rail_shows_resource_reward_and_stays_without_time()
	{
		EventManager.Clear();
		SaveCache.StartNewRun();
		var climb = SaveCache.GetClimbState();
		climb.time = 5;
		climb.eventSlots[0] = new ClimbEventSlotSave
		{
			id = "hazard",
			definitionId = "winter_reliquary",
			kind = ClimbEventKind.Hazard,
			activatedAtTime = 4,
			duration = 4,
			timeCost = 0,
			rewardResources = new ClimbResourceSave { red = 1, black = 1 },
			status = ClimbEventStatus.Active,
		};
		climb.eventSlots[1] = new ClimbEventSlotSave
		{
			id = "character",
			definitionId = "nun_counsel",
			kind = ClimbEventKind.Character,
			activatedAtTime = 5,
			duration = 4,
			timeCost = 1,
			status = ClimbEventStatus.Active,
		};
		SaveCache.SaveClimbState(climb);
		var entityManager = BuildClimbEntityManager();
		var layout = new ClimbV2LayoutSystem(entityManager);
		try
		{
			layout.Update(Frame(0f));
			var rails = entityManager.GetEntitiesWithComponent<ClimbChoiceRailPresentation>()
				.Select(entity => entity.GetComponent<ClimbChoiceRailPresentation>()).ToList();
			var hazard = Assert.Single(rails, rail => rail.SourceSlotId == "hazard");
			var character = Assert.Single(rails, rail => rail.SourceSlotId == "character");

			Assert.Equal(ClimbChoiceRailOutcomeKind.Reward, hazard.OutcomeKind);
			Assert.Equal(1, hazard.Resources.red);
			Assert.Equal(1, hazard.Resources.black);
			Assert.False(hazard.ShowTime);
			Assert.True(hazard.Stays >= 0);
			Assert.Equal(ClimbChoiceRailOutcomeKind.None, character.OutcomeKind);
			Assert.True(character.ShowTime);
		}
		finally
		{
			layout.Shutdown();
			EventManager.Clear();
		}
	}

	[Fact]
	public void Character_portrait_fit_preserves_the_source_aspect_ratio()
	{
		var container = new Rectangle(10, 20, 100, 155);

		Rectangle fitted = ClimbV2Draw.Contain(525, 1417, container);

		Assert.Equal(57, fitted.Width);
		Assert.Equal(155, fitted.Height);
		Assert.Equal(container.Center, fitted.Center);
	}

	[Fact]
	public void Encounter_portrait_parallax_matches_v1_cursor_direction_and_clamps_magnitude()
	{
		Vector2 target = EncounterDisplaySystem.ComputePortraitParallaxTarget(
			new Vector2(Game1.VirtualWidth, 0f), true, 0.01f, 0.01f, 151f);
		Vector2 clamped = EncounterDisplaySystem.ComputePortraitParallaxTarget(
			new Vector2(Game1.VirtualWidth, 0f), true, 0.01f, 0.01f, 5f);

		Assert.Equal(-9.6f, target.X, 3);
		Assert.Equal(5.4f, target.Y, 3);
		Assert.Equal(5f, clamped.Length(), 3);
		Assert.Equal(Vector2.Zero, EncounterDisplaySystem.ComputePortraitParallaxTarget(Vector2.Zero, false, 1f, 1f, 5f));
	}

	private static EntityManager BuildClimbEntityManager()
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Climb });
		return entityManager;
	}

	private static void AssertParallax(ParallaxLayer expected, ParallaxLayer actual)
	{
		Assert.NotNull(actual);
		Assert.Equal(expected.MultiplierX, actual.MultiplierX);
		Assert.Equal(expected.MultiplierY, actual.MultiplierY);
		Assert.Equal(expected.MaxOffset, actual.MaxOffset);
		Assert.Equal(expected.SmoothTime, actual.SmoothTime);
	}

	private static GameTime Frame(float seconds) => new(
		TimeSpan.FromSeconds(seconds),
		TimeSpan.FromSeconds(seconds));
}
