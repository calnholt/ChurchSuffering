using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;
using static ChurchSuffering.ECS.Components.CardData;

namespace ChurchSuffering.Tests;

public class WayStationCollectionCatalogTests
{
	[Fact]
	public void FreshProfileIncludesOnlySwordWeapon()
	{
		var catalog = WayStationCollectionCatalogService.Build(
			new PlayerCollectionSave(),
			new WayStationMetaSave());

		Assert.Contains(catalog.Cards, item => item.Id == "sword");
		Assert.DoesNotContain(catalog.Cards, item => item.Id == "dagger");
		Assert.DoesNotContain(catalog.Cards, item => item.Id == "hammer");
		Assert.True(catalog.CardTotal > catalog.Cards.Count);
	}

	[Fact]
	public void CatalogIgnoresUnknownDuplicateTokenAndNonLoadoutIds()
	{
		var collection = new PlayerCollectionSave
		{
			cardIds = ["strike", "STRIKE", "unknown_card", "kunai", "curse"],
		};
		var catalog = WayStationCollectionCatalogService.Build(collection, new WayStationMetaSave());

		Assert.Equal(1, catalog.Cards.Count(item => item.Id.Equals("strike", StringComparison.OrdinalIgnoreCase)));
		Assert.DoesNotContain(catalog.Cards, item => item.Id is "unknown_card" or "kunai" or "curse");
	}

	[Fact]
	public void DaggerAndHammerUseWaystationProgression()
	{
		var collection = new PlayerCollectionSave { cardIds = ["dagger", "hammer"] };
		var locked = WayStationCollectionCatalogService.Build(collection, new WayStationMetaSave());
		Assert.DoesNotContain(locked.Cards, item => item.Id is "dagger" or "hammer");

		var unlocked = WayStationCollectionCatalogService.Build(collection, new WayStationMetaSave
		{
			highestPenanceByWeapon = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			{
				["sword"] = 0,
				["dagger"] = 0,
				["hammer"] = 0,
			},
		});
		Assert.Contains(unlocked.Cards, item => item.Id == "dagger");
		Assert.Contains(unlocked.Cards, item => item.Id == "hammer");
	}

	[Fact]
	public void SaintsComeFromCollectionAndEquipmentIsGroupedBySlot()
	{
		var collection = new PlayerCollectionSave
		{
			medalIds = ["st_michael", "ST_MICHAEL", "not_a_saint"],
			equipmentIds = ["fleetfoot_greaves", "knightly_helm", "knightly_chest", "knightly_gauntlets"],
		};
		var meta = new WayStationMetaSave { purchasedMedalIds = ["st_rita"] };
		var catalog = WayStationCollectionCatalogService.Build(collection, meta);

		Assert.Single(catalog.Saints);
		Assert.Equal("st_michael", catalog.Saints[0].Id);
		Assert.DoesNotContain(catalog.Saints, item => item.Id == "st_rita");
		Assert.Equal(
			[EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Arms, EquipmentSlot.Legs],
			catalog.Equipment.Select(item => item.Equipment.Slot));
		Assert.True(catalog.SaintTotal > catalog.Saints.Count);
		Assert.True(catalog.EquipmentTotal > catalog.Equipment.Count);
	}

	[Fact]
	public void CardFiltersClassifyWeaponsBeforeCardType()
	{
		var collection = new PlayerCollectionSave
		{
			cardIds = ["strike", "mantlet", "fury"],
		};
		var catalog = WayStationCollectionCatalogService.Build(collection, new WayStationMetaSave());

		Assert.Equal(catalog.Cards.Count, WayStationCollectionCatalogService.FilterCards(catalog, WayStationCollectionCardFilter.All).Count);
		Assert.All(
			WayStationCollectionCatalogService.FilterCards(catalog, WayStationCollectionCardFilter.Weapon),
			item => Assert.True(item.Card.IsWeapon));
		Assert.All(
			WayStationCollectionCatalogService.FilterCards(catalog, WayStationCollectionCardFilter.Attack),
			item =>
			{
				Assert.False(item.Card.IsWeapon);
				Assert.Equal(ChurchSuffering.ECS.Objects.Cards.CardType.Attack, item.Card.Type);
			});
		Assert.Contains(WayStationCollectionCatalogService.FilterCards(catalog, WayStationCollectionCardFilter.Block), item => item.Id == "mantlet");
		Assert.Contains(WayStationCollectionCatalogService.FilterCards(catalog, WayStationCollectionCardFilter.Prayer), item => item.Id == "fury");
	}

	[Fact]
	public void ModalResetAndColorCycleUseFixedDefaults()
	{
		var catalog = WayStationCollectionCatalogService.Build(
			new PlayerCollectionSave { medalIds = ["st_rita", "st_michael"] },
			new WayStationMetaSave());
		var state = new WayStationCollectionModalState
		{
			ActiveTab = WayStationCollectionTab.Equipment,
			ActiveCardFilter = WayStationCollectionCardFilter.Weapon,
			CardScrollOffset = 100,
			SaintListScrollOffset = 100,
			SaintDetailScrollOffset = 100,
			EquipmentScrollOffset = 100,
		};

		WayStationCollectionModalLogic.Reset(state, catalog);

		Assert.Equal(WayStationCollectionTab.Cards, state.ActiveTab);
		Assert.Equal(WayStationCollectionCardFilter.All, state.ActiveCardFilter);
		Assert.Equal(0, state.CardScrollOffset);
		Assert.Equal(0, state.SaintListScrollOffset);
		Assert.Equal(0, state.SaintDetailScrollOffset);
		Assert.Equal(0, state.EquipmentScrollOffset);
		Assert.Equal(catalog.Saints[0].Id, state.SelectedMedalId);
		Assert.Equal(CardColor.Red, WayStationCollectionModalLogic.NextColor(CardColor.White));
		Assert.Equal(CardColor.Black, WayStationCollectionModalLogic.NextColor(CardColor.Red));
		Assert.Equal(CardColor.White, WayStationCollectionModalLogic.NextColor(CardColor.Black));
	}

	[Fact]
	public void MotionApproachIsFrameStepIndependent()
	{
		float at30 = Simulate(30);
		float at60 = Simulate(60);
		float at120 = Simulate(120);

		Assert.InRange(Math.Abs(at30 - at60), 0f, 0.0002f);
		Assert.InRange(Math.Abs(at60 - at120), 0f, 0.0002f);
		Assert.InRange(at120, 0.998f, 1f);
	}

	[Fact]
	public void CardColorCommitsOnlyAfterSwitchAnimationCompletes()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("CardStack");
		var stack = new WayStationCollectionCardStackPresentation
		{
			FrontColor = CardColor.White,
			PendingFrontColor = CardColor.Red,
			ColorSwitchProgress = 0f,
		};
		entityManager.AddComponent(entity, stack);
		entityManager.AddComponent(entity, new WayStationCollectionMotion());
		var motion = new WayStationCollectionMotionSystem(entityManager)
		{
			CardSwitchDuration = 0.28f,
		};

		motion.Update(new GameTime(TimeSpan.FromSeconds(0.14), TimeSpan.FromSeconds(0.14)));

		Assert.Equal(CardColor.White, stack.FrontColor);
		Assert.Equal(CardColor.Red, stack.PendingFrontColor);
		Assert.InRange(stack.ColorSwitchProgress, 0.499f, 0.501f);

		motion.Update(new GameTime(TimeSpan.FromSeconds(0.28), TimeSpan.FromSeconds(0.14)));

		Assert.Equal(CardColor.Red, stack.FrontColor);
		Assert.Null(stack.PendingFrontColor);
		Assert.Equal(1f, stack.ColorSwitchProgress);
	}

	[Fact]
	public void UpgradePreviewModifierAcceptsShiftOrLeftTrigger()
	{
		PlayerInputFrame none = default;
		PlayerInputFrame shift = none with
		{
			DownButtons = PlayerInputFrame.Mask(PlayerButton.Shift),
		};
		PlayerInputFrame trigger = none with { LeftTrigger = 0.15f };

		Assert.False(WayStationCollectionModalLogic.IsUpgradePreviewModifierHeld(none));
		Assert.True(WayStationCollectionModalLogic.IsUpgradePreviewModifierHeld(shift));
		Assert.True(WayStationCollectionModalLogic.IsUpgradePreviewModifierHeld(trigger));
	}

	[Fact]
	public void CloseHotKeyUsesGamepadBWithoutKeyboardBinding()
	{
		var hotKey = WayStationCollectionModalLogic.CreateCloseHotKey();

		Assert.Equal(FaceButton.B, hotKey.Button);
		Assert.False(hotKey.IsKeyboardMouseEnabled);
		Assert.Null(hotKey.KeyboardButton);
	}

	[Fact]
	public void CollectionControlsWinHitTestingOverRootAndScrollBlockers()
	{
		EventManager.Clear();
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(scene, new SceneState());
		const string contextId = "collection-test";

		var root = CreateInputEntity(
			entityManager,
			"Root",
			new Rectangle(0, 0, 400, 400),
			WayStationCollectionInputLayers.Root,
			contextId);
		entityManager.AddComponent(root, new InputContext
		{
			Id = contextId,
			Priority = 100,
			IsActive = true,
		});
		var scroll = CreateInputEntity(
			entityManager,
			"Scroll",
			new Rectangle(20, 20, 300, 300),
			WayStationCollectionInputLayers.ScrollBlocker,
			contextId);
		var control = CreateInputEntity(
			entityManager,
			"Control",
			new Rectangle(50, 50, 100, 100),
			WayStationCollectionInputLayers.Control,
			contextId);
		var source = new CollectionFakeInputSource(new PlayerInputFrame(
			1,
			true,
			false,
			PlayerInputDevice.KeyboardMouse,
			PlayerInputDevice.KeyboardMouse,
			GamepadGlyphStyle.Xbox,
			new Vector2(75, 75),
			Vector2.Zero,
			0f,
			Vector2.Zero,
			Vector2.Zero,
			0f,
			0f,
			PlayerInputFrame.Mask(PlayerButton.Primary),
			PlayerInputFrame.Mask(PlayerButton.Primary),
			PlayerButtonMask.None));
		var input = new PlayerInputSystem(entityManager, source);
		var interaction = new UIInteractionSystem(entityManager);

		input.Update(new GameTime());
		interaction.Update(new GameTime());

		Assert.True(control.GetComponent<UIElement>().IsHovered);
		Assert.True(control.GetComponent<UIElement>().IsClicked);
		Assert.False(scroll.GetComponent<UIElement>().IsHovered);
		Assert.False(root.GetComponent<UIElement>().IsHovered);
		EventManager.Clear();
	}

	[Fact]
	public void KeywordRunsPreserveCasingPunctuationAndBoundaries()
	{
		var runs = TooltipTextService.GetKeywordTextRuns("Gain 2 AEGIS, then reburned.");

		Assert.Contains(runs, run => run.IsKeyword && run.Text == "AEGIS");
		Assert.DoesNotContain(runs, run => run.IsKeyword && run.Text.Contains("burn", StringComparison.OrdinalIgnoreCase));
		Assert.Equal("Gain 2 AEGIS, then reburned.", string.Concat(runs.Select(run => run.Text)));
	}

	private static float Simulate(int framesPerSecond)
	{
		float value = 0f;
		float dt = 1f / framesPerSecond;
		for (int i = 0; i < framesPerSecond; i++)
			value = WayStationCollectionModalLogic.Approach(value, 1f, 0.28f, dt);
		return value;
	}

	private static Entity CreateInputEntity(
		EntityManager entityManager,
		string name,
		Rectangle bounds,
		int zOrder,
		string contextId)
	{
		var entity = entityManager.CreateEntity(name);
		entityManager.AddComponent(entity, new Transform { ZOrder = zOrder });
		entityManager.AddComponent(entity, new UIElement
		{
			Bounds = bounds,
			IsInteractable = true,
			LayerType = UILayerType.Overlay,
		});
		entityManager.AddComponent(entity, new InputContextMember { ContextId = contextId });
		return entity;
	}

	private sealed class CollectionFakeInputSource(PlayerInputFrame frame) : IPlayerInputSource
	{
		public PlayerInputFrame Capture(
			bool isWindowActive,
			Rectangle renderDestination,
			int virtualWidth,
			int virtualHeight) => frame;

		public void SetRumbleChannel(string channelId, RumbleMotorState motors) { }
		public void ClearRumbleChannel(string channelId) { }
		public void PlayRumblePattern(RumblePattern pattern, RumbleGroup group = RumbleGroup.Default) { }
		public void ClearRumbleGroup(RumbleGroup group) { }
		public void ClearAllRumble() { }
		public void SetRumbleLevel(int level) { }
		public void TickRumble(float deltaSeconds) { }
	}
}
