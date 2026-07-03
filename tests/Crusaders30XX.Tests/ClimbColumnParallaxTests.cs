using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class ClimbColumnParallaxTests : IDisposable
{
	public ClimbColumnParallaxTests()
	{
		EventManager.Clear();
		SaveCache.StartNewRun();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Layout_marks_columns_non_interactable_and_slots_interactable_when_available()
	{
		var entityManager = BuildWorld();
		var system = new ClimbColumnLayoutSystem(entityManager);

		system.Update(new GameTime());

		var columns = entityManager.GetEntitiesWithComponent<ClimbColumnPresentation>().ToList();
		Assert.NotEmpty(columns);
		Assert.All(columns, entity =>
		{
			var ui = entity.GetComponent<UIElement>();
			Assert.NotNull(ui);
			Assert.False(ui.IsInteractable);
		});

		var availableSlots = entityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
			.Where(entity =>
			{
				var presentation = entity.GetComponent<ClimbSlotPresentation>();
				return presentation != null && !presentation.IsUnavailable && !presentation.IsSold;
			})
			.ToList();
		Assert.NotEmpty(availableSlots);
		Assert.Contains(availableSlots, entity => entity.GetComponent<UIElement>()?.IsInteractable == true);
	}

	[Fact]
	public void Layout_configures_columns_and_slots_as_distinct_parallax_layers()
	{
		var entityManager = BuildWorld();
		var system = new ClimbColumnLayoutSystem(entityManager);

		system.Update(new GameTime());

		var root = entityManager.GetEntity(ClimbHeaderLayoutSystem.RootName);
		var columns = entityManager.GetEntitiesWithComponent<ClimbColumnPresentation>().ToList();
		var slots = entityManager.GetEntitiesWithComponent<ClimbSlotPresentation>().ToList();
		Assert.Equal(3, columns.Count);
		Assert.Equal(
			ClimbRuleService.ShopSlotCount + ClimbRuleService.EncounterSlotCount + ClimbRuleService.EventSlotCount,
			slots.Count);

		var columnSettings = ParallaxLayer.GetLocationParallaxLayer();
		Assert.All(columns, entity =>
		{
			AssertParallax(columnSettings, entity.GetComponent<ParallaxLayer>());
			Assert.Same(root, entity.GetComponent<ParentTransform>()?.Parent);
			Assert.Equal(Point.Zero, entity.GetComponent<UIElement>().Bounds.Location);
		});

		var slotSettings = ParallaxLayer.GetUIParallaxLayer();
		Assert.All(slots, entity =>
		{
			AssertParallax(slotSettings, entity.GetComponent<ParallaxLayer>());
			Assert.Same(root, entity.GetComponent<ParentTransform>()?.Parent);
			Assert.Equal(Point.Zero, entity.GetComponent<UIElement>().Bounds.Location);
		});
		Assert.True(slotSettings.MultiplierX > columnSettings.MultiplierX);
		Assert.True(slotSettings.MultiplierY > columnSettings.MultiplierY);
		Assert.True(slotSettings.MaxOffset > columnSettings.MaxOffset);

		columns[0].GetComponent<ParallaxLayer>().MultiplierX = -1f;
		columns[0].GetComponent<ParentTransform>().Parent = null;
		slots[0].GetComponent<ParallaxLayer>().MaxOffset = -1f;
		slots[0].GetComponent<ParentTransform>().Parent = null;
		system.Update(new GameTime());

		AssertParallax(columnSettings, columns[0].GetComponent<ParallaxLayer>());
		Assert.Same(root, columns[0].GetComponent<ParentTransform>().Parent);
		AssertParallax(slotSettings, slots[0].GetComponent<ParallaxLayer>());
		Assert.Same(root, slots[0].GetComponent<ParentTransform>().Parent);
	}

	[Fact]
	public void Resolved_bounds_follow_parallax_adjusted_transforms()
	{
		var entityManager = BuildWorld();
		var system = new ClimbColumnLayoutSystem(entityManager);
		system.Update(new GameTime());
		var offset = new Vector2(9f, -6f);

		var slot = entityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
			.First(entity => entity.GetComponent<UIElement>()?.Bounds.Width > 0);
		var slotUi = slot.GetComponent<UIElement>();
		Rectangle slotBoundsBefore = TransformResolverService.ResolveUIBounds(entityManager, slot, slotUi);
		slot.GetComponent<Transform>().Position += offset;
		Rectangle slotBoundsAfter = TransformResolverService.ResolveUIBounds(entityManager, slot, slotUi);
		Assert.Equal(Offset(slotBoundsBefore, offset), slotBoundsAfter);

		var column = entityManager.GetEntitiesWithComponent<ClimbColumnPresentation>().First();
		var presentation = column.GetComponent<ClimbColumnPresentation>();
		Rectangle innerBoundsBefore = TransformResolverService.ResolveLocalBounds(
			entityManager,
			column,
			presentation.InnerBounds);
		column.GetComponent<Transform>().Position += offset;
		Rectangle innerBoundsAfter = TransformResolverService.ResolveLocalBounds(
			entityManager,
			column,
			presentation.InnerBounds);
		Assert.Equal(Offset(innerBoundsBefore, offset), innerBoundsAfter);
	}

	[Fact]
	public void ComputePortraitSlotHeight_uses_portrait_meta_and_padding_defaults()
	{
		Assert.Equal(264, ClimbColumnDisplaySystem.ComputePortraitSlotHeight());
	}

	[Fact]
	public void Layout_sizes_encounter_and_event_slots_from_portrait_slot_height()
	{
		var entityManager = BuildWorld();
		var layout = new ClimbColumnLayoutSystem(entityManager);
		layout.Update(new GameTime());

		int expectedHeight = ClimbColumnDisplaySystem.ComputePortraitSlotHeight();
		var portraitSlots = entityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
			.Where(entity =>
			{
				var kind = entity.GetComponent<ClimbSlotPresentation>()?.Kind;
				return kind == ClimbSlotKind.Encounter || kind == ClimbSlotKind.Event;
			})
			.ToList();

		Assert.NotEmpty(portraitSlots);
		Assert.All(portraitSlots, entity =>
		{
			var ui = entity.GetComponent<UIElement>();
			Assert.NotNull(ui);
			Assert.Equal(expectedHeight, ui.Bounds.Height);
		});
	}

	[Fact]
	public void Layout_copies_encounter_battle_location_to_slot_presentation()
	{
		var climb = SaveCache.GetClimbState();
		var encounter = climb.encounterSlots.First(slot => !string.IsNullOrWhiteSpace(slot.enemyId));
		encounter.battleLocation = BattleLocation.Jungle;
		SaveCache.SaveClimbState(climb);
		var entityManager = BuildWorld();
		var layout = new ClimbColumnLayoutSystem(entityManager);

		layout.Update(new GameTime());

		var presentation = entityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
			.Select(entity => entity.GetComponent<ClimbSlotPresentation>())
			.Single(slot => slot.SlotId == encounter.id);
		Assert.Equal(BattleLocation.Jungle, presentation.BattleLocation);
	}

	[Fact]
	public void Events_column_enters_from_right_and_suppresses_climb_input()
	{
		var entityManager = BuildWorld();
		var layout = new ClimbColumnLayoutSystem(entityManager)
		{
			EventsEnterSeconds = 1f,
		};
		layout.Update(new GameTime());
		var twoColumn = ClimbColumnLayoutSystem.ComputeColumnsLayout(showEvents: false);
		var threeColumn = ClimbColumnLayoutSystem.ComputeColumnsLayout(showEvents: true);

		ActivateSingleEvent();
		layout.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5)));

		var shopBounds = GetColumnBounds(entityManager, ClimbColumnKind.Shop);
		var eventBounds = GetColumnBounds(entityManager, ClimbColumnKind.Event);
		var eventColumn = GetColumn(entityManager, ClimbColumnKind.Event).GetComponent<ClimbColumnPresentation>();
		Assert.InRange(shopBounds.X, threeColumn.Shop.X + 1, twoColumn.Shop.X - 1);
		Assert.InRange(eventBounds.X, threeColumn.Events.X + 1, Game1.VirtualWidth + ClimbColumnDisplaySystem.ColumnsGapValue - 1);
		Assert.InRange(eventColumn.Opacity, 0.01f, 0.99f);
		Assert.True(eventColumn.IsVisible);
		Assert.Contains(entityManager.GetEntitiesWithComponent<ClimbColumnTransitionInputSuppression>(), e => e.GetComponent<UIElement>() != null);

		layout.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.6)));

		shopBounds = GetColumnBounds(entityManager, ClimbColumnKind.Shop);
		eventBounds = GetColumnBounds(entityManager, ClimbColumnKind.Event);
		eventColumn = GetColumn(entityManager, ClimbColumnKind.Event).GetComponent<ClimbColumnPresentation>();
		Assert.Equal(threeColumn.Shop, shopBounds);
		Assert.Equal(threeColumn.Events, eventBounds);
		Assert.Equal(1f, eventColumn.Opacity, precision: 3);
		Assert.Empty(entityManager.GetEntitiesWithComponent<ClimbColumnTransitionInputSuppression>());
	}

	[Fact]
	public void Events_column_leaves_before_other_columns_move_and_restores_input()
	{
		ActivateSingleEvent();
		var entityManager = BuildWorld();
		var layout = new ClimbColumnLayoutSystem(entityManager)
		{
			EventsLeaveSeconds = 1f,
			EventsLeaveSplit = 0.5f,
		};
		layout.Update(new GameTime());
		var twoColumn = ClimbColumnLayoutSystem.ComputeColumnsLayout(showEvents: false);
		var threeColumn = ClimbColumnLayoutSystem.ComputeColumnsLayout(showEvents: true);

		ExpireAllEvents();
		layout.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.25)));

		var shopBounds = GetColumnBounds(entityManager, ClimbColumnKind.Shop);
		var eventBounds = GetColumnBounds(entityManager, ClimbColumnKind.Event);
		var eventColumn = GetColumn(entityManager, ClimbColumnKind.Event).GetComponent<ClimbColumnPresentation>();
		Assert.Equal(threeColumn.Shop, shopBounds);
		Assert.InRange(eventBounds.X, threeColumn.Events.X + 1, Game1.VirtualWidth + ClimbColumnDisplaySystem.ColumnsGapValue - 1);
		Assert.InRange(eventColumn.Opacity, 0.01f, 0.99f);
		Assert.NotEmpty(entityManager.GetEntitiesWithComponent<ClimbColumnTransitionInputSuppression>());
		Assert.Contains(entityManager.GetEntitiesWithComponent<ClimbSlotPresentation>(), e =>
		{
			var slot = e.GetComponent<ClimbSlotPresentation>();
			var ui = e.GetComponent<UIElement>();
			return slot?.Kind == ClimbSlotKind.Event && ui?.IsHidden == false;
		});

		layout.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5)));

		shopBounds = GetColumnBounds(entityManager, ClimbColumnKind.Shop);
		eventBounds = GetColumnBounds(entityManager, ClimbColumnKind.Event);
		Assert.InRange(shopBounds.X, threeColumn.Shop.X + 1, twoColumn.Shop.X - 1);
		Assert.Equal(Game1.VirtualWidth + ClimbColumnDisplaySystem.ColumnsGapValue, eventBounds.X);

		layout.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.3)));

		shopBounds = GetColumnBounds(entityManager, ClimbColumnKind.Shop);
		eventColumn = GetColumn(entityManager, ClimbColumnKind.Event).GetComponent<ClimbColumnPresentation>();
		Assert.Equal(twoColumn.Shop, shopBounds);
		Assert.False(eventColumn.IsVisible);
		Assert.Equal(0f, eventColumn.Opacity, precision: 3);
		Assert.Empty(entityManager.GetEntitiesWithComponent<ClimbColumnTransitionInputSuppression>());
		Assert.DoesNotContain(entityManager.GetEntitiesWithComponent<ClimbSlotPresentation>(), e =>
		{
			var slot = e.GetComponent<ClimbSlotPresentation>();
			var ui = e.GetComponent<UIElement>();
			return slot?.Kind == ClimbSlotKind.Event && ui?.IsHidden == false;
		});
	}

	private static EntityManager BuildWorld()
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Climb });
		var root = entityManager.CreateEntity(ClimbHeaderLayoutSystem.RootName);
		entityManager.AddComponent(root, new Transform());
		entityManager.AddComponent(root, new ClimbSceneRoot());
		return entityManager;
	}

	private static void ActivateSingleEvent()
	{
		var climb = SaveCache.GetClimbState();
		var slot = climb.eventSlots.First();
		foreach (var eventSlot in climb.eventSlots)
		{
			eventSlot.status = ClimbEventStatus.Expired;
		}
		slot.id = string.IsNullOrWhiteSpace(slot.id) ? "test_event" : slot.id;
		slot.status = ClimbEventStatus.Active;
		slot.activatedAtTime = ClimbRuleService.ClampTime(climb.time);
		slot.duration = Math.Max(2, slot.duration);
		slot.rewardResources = new ClimbResourceSave { red = 1, white = 0, black = 0 };
		SaveCache.SaveClimbState(climb);
	}

	private static void ExpireAllEvents()
	{
		var climb = SaveCache.GetClimbState();
		foreach (var slot in climb.eventSlots)
		{
			slot.status = ClimbEventStatus.Expired;
		}
		SaveCache.SaveClimbState(climb);
	}

	private static Entity GetColumn(EntityManager entityManager, ClimbColumnKind kind)
	{
		return entityManager.GetEntitiesWithComponent<ClimbColumnPresentation>()
			.Single(entity => entity.GetComponent<ClimbColumnPresentation>()?.Kind == kind);
	}

	private static Rectangle GetColumnBounds(EntityManager entityManager, ClimbColumnKind kind)
	{
		var entity = GetColumn(entityManager, kind);
		return TransformResolverService.ResolveUIBounds(entityManager, entity, entity.GetComponent<UIElement>());
	}

	private static void AssertParallax(ParallaxLayer expected, ParallaxLayer actual)
	{
		Assert.NotNull(actual);
		Assert.Equal(expected.MultiplierX, actual.MultiplierX);
		Assert.Equal(expected.MultiplierY, actual.MultiplierY);
		Assert.Equal(expected.MaxOffset, actual.MaxOffset);
		Assert.Equal(expected.SmoothTime, actual.SmoothTime);
	}

	private static Rectangle Offset(Rectangle bounds, Vector2 offset)
	{
		return new Rectangle(
			bounds.X + (int)offset.X,
			bounds.Y + (int)offset.Y,
			bounds.Width,
			bounds.Height);
	}
}
