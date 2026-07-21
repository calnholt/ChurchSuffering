using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class EquipmentDisplaySystemTests : IDisposable
{
	public EquipmentDisplaySystemTests()
	{
		EventManager.Clear();
		StateSingleton.IsActive = false;
		StateSingleton.IsTutorialActive = false;
	}

	public void Dispose()
	{
		EventManager.Clear();
		StateSingleton.IsActive = false;
		StateSingleton.IsTutorialActive = false;
	}

	[Fact]
	public void Layout_puts_parallax_only_on_root_and_parents_panels_and_tooltip()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var head = AddEquipment(entityManager, player, "helm_of_seeing");
		var legs = AddEquipment(entityManager, player, "knightly_grieves");
		entityManager.AddComponent(head, ParallaxLayer.GetUIParallaxLayer());
		entityManager.AddComponent(legs, ParallaxLayer.GetUIParallaxLayer());
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);

		display.Update(Frame());

		var root = entityManager.GetEntitiesWithComponent<EquipmentDisplayRoot>().Single();
		var tooltip = entityManager.GetEntitiesWithComponent<EquipmentTooltipState>().Single();
		Assert.NotNull(root.GetComponent<ParallaxLayer>());
		Assert.Same(root, head.GetComponent<ParentTransform>().Parent);
		Assert.Same(root, legs.GetComponent<ParentTransform>().Parent);
		Assert.Same(root, tooltip.GetComponent<ParentTransform>().Parent);
		Assert.False(head.HasComponent<ParallaxLayer>());
		Assert.False(legs.HasComponent<ParallaxLayer>());
		Assert.False(tooltip.HasComponent<ParallaxLayer>());
		Assert.Single(entityManager.GetEntitiesWithComponent<ParallaxLayer>());
	}

	[Fact]
	public void Moving_root_offsets_panel_hitbox_tooltip_and_last_panel_center()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "helm_of_seeing");
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);
		var tooltipDisplay = new EquipmentTooltipDisplaySystem(entityManager, null, null, null);
		display.Update(Frame());
		equipment.GetComponent<UIElement>().IsHovered = true;
		tooltipDisplay.Update(Frame());

		Rectangle panelBefore = display.GetPanelWorldBounds(equipment);
		Rectangle hitboxBefore = TransformResolverService.ResolveUIBounds(
			entityManager,
			equipment,
			equipment.GetComponent<UIElement>());
		Rectangle tooltipBefore = tooltipDisplay.GetTooltipWorldBounds();
		Vector2 centerBefore = equipment.GetComponent<EquipmentZone>().LastPanelCenter;

		var offset = new Vector2(37, -19);
		display.LeftMargin += (int)offset.X;
		display.TopMargin += (int)offset.Y;
		display.Update(Frame());
		equipment.GetComponent<UIElement>().IsHovered = true;
		tooltipDisplay.Update(Frame());

		Assert.Equal(Offset(panelBefore, offset), display.GetPanelWorldBounds(equipment));
		Assert.Equal(
			Offset(hitboxBefore, offset),
			TransformResolverService.ResolveUIBounds(
				entityManager,
				equipment,
				equipment.GetComponent<UIElement>()));
		Assert.Equal(Offset(tooltipBefore, offset), tooltipDisplay.GetTooltipWorldBounds());
		Assert.Equal(centerBefore + offset, equipment.GetComponent<EquipmentZone>().LastPanelCenter);
	}

	[Fact]
	public void Assigning_equipment_as_block_detaches_before_world_space_animation()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Block);
		var equipment = AddEquipment(entityManager, player, "knightly_grieves");
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned =
			[
				new PlannedAttack
				{
					AttackDefinition = new EnemyAttackBase
					{
						Id = EnemyAttackId.Cinderbolt,
						Damage = 5,
					},
				},
			],
		});
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);
		display.Update(Frame());
		var root = entityManager.GetEntitiesWithComponent<EquipmentDisplayRoot>().Single();
		root.GetComponent<Transform>().Position += new Vector2(18, -11);
		Rectangle panelBounds = display.GetPanelWorldBounds(equipment);
		display.Update(Frame());
		equipment.GetComponent<UIElement>().IsClicked = true;

		new EquipmentBlockInteractionSystem(entityManager).Update(Frame());

		Assert.False(equipment.HasComponent<ParentTransform>());
		Assert.Equal(
			new Vector2(panelBounds.Center.X, panelBounds.Center.Y),
			equipment.GetComponent<Transform>().Position);
		var assigned = equipment.GetComponent<AssignedBlockCard>();
		Assert.NotNull(assigned);
		Assert.Equal(
			new Vector2(panelBounds.Center.X, panelBounds.Center.Y),
			equipment.GetComponent<AssignedBlockPresentation>().StartPos);
		Assert.Equal(new Vector2(panelBounds.Center.X, panelBounds.Center.Y), assigned.ReturnTargetPos);
	}

	[Fact]
	public void Returned_equipment_reattaches_and_resumes_local_layout()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Block);
		var equipment = AddEquipment(entityManager, player, "knightly_grieves");
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);
		display.Update(Frame());
		var root = entityManager.GetEntitiesWithComponent<EquipmentDisplayRoot>().Single();
		entityManager.RemoveComponent<ParentTransform>(equipment);
		equipment.GetComponent<Transform>().Position = new Vector2(900, 700);
		equipment.GetComponent<EquipmentZone>().Zone = EquipmentZoneType.Default;

		display.Update(Frame());

		Assert.Same(root, equipment.GetComponent<ParentTransform>().Parent);
		Assert.Equal(new Vector2(0, display.PanelHeight * 3 + display.RowGap * 3), equipment.GetComponent<Transform>().Position);
		Assert.Equal(
			new Vector2(
				display.LeftMargin + display.PanelWidth / 2f,
				display.TopMargin + display.PanelHeight * 3 + display.RowGap * 3 + display.PanelHeight / 2),
			equipment.GetComponent<EquipmentZone>().LastPanelCenter);
	}

	[Fact]
	public void Quest_reward_overlay_does_not_refresh_used_equipment()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "helm_of_seeing");
		equipment.GetComponent<EquippedEquipment>().Equipment.MarkUsed();
		_ = new EquipmentManagerSystem(entityManager);

		EventManager.Publish(new ShowQuestRewardOverlay());

		var model = equipment.GetComponent<EquippedEquipment>().Equipment;
		Assert.True(model.IsUsed);
	}

	[Theory]
	[InlineData(SubPhase.Block, "knightly_grieves", true, "This equipment has already been used this battle!")]
	[InlineData(SubPhase.Action, "knightly_grieves", false, "This equipment cannot be activated during the Action phase!")]
	[InlineData(SubPhase.Action, "purging_bracers", true, "This equipment has already been used this battle!")]
	public void Invalid_block_and_action_clicks_emit_expected_message(
		SubPhase phase,
		string equipmentId,
		bool isUsed,
		string expectedMessage)
	{
		var entityManager = BuildBattle(out var player, phase);
		var equipment = AddEquipment(entityManager, player, equipmentId);
		if (isUsed)
		{
			equipment.GetComponent<EquippedEquipment>().Equipment.MarkUsed();
		}
		if (phase == SubPhase.Block)
		{
			var enemy = entityManager.CreateEntity("Enemy");
			entityManager.AddComponent(enemy, new AttackIntent
			{
				ActiveAttackSequence = 1,
				Planned =
				[
					new PlannedAttack
					{
						AttackDefinition = new EnemyAttackBase
						{
							Id = EnemyAttackId.Cinderbolt,
							Damage = 5,
						},
					},
				],
			});
		}
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);
		display.Update(Frame());
		equipment.GetComponent<UIElement>().IsClicked = true;
		var messages = new List<string>();
		EventManager.Subscribe<CantPlayCardMessage>(evt => messages.Add(evt.Message));

		new EquipmentBlockInteractionSystem(entityManager).Update(Frame());

		Assert.Equal([expectedMessage], messages);
		Assert.Null(equipment.GetComponent<AssignedBlockCard>());
	}

	[Fact]
	public void Other_battle_phases_are_hover_only_and_silent()
	{
		var entityManager = BuildBattle(out var player, SubPhase.PlayerEnd);
		var equipment = AddEquipment(entityManager, player, "helm_of_seeing");
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);
		display.Update(Frame());
		var ui = equipment.GetComponent<UIElement>();
		ui.IsHovered = true;
		ui.IsClicked = true;
		var messages = new List<string>();
		int activationRequests = 0;
		EventManager.Subscribe<CantPlayCardMessage>(evt => messages.Add(evt.Message));
		EventManager.Subscribe<EquipmentActivateEvent>(evt => activationRequests++);

		new EquipmentBlockInteractionSystem(entityManager).Update(Frame());

		Assert.True(ui.IsInteractable);
		Assert.True(ui.IsHovered);
		Assert.Empty(messages);
		Assert.Equal(0, activationRequests);
		Assert.Null(equipment.GetComponent<AssignedBlockCard>());
	}

	[Fact]
	public void Layout_enables_hover_highlight_when_equipment_is_available()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "helm_of_seeing");
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);

		display.Update(Frame());

		var ui = equipment.GetComponent<UIElement>();
		Assert.True(ui.ShowHoverHighlight);
		ui.IsHovered = true;
		Assert.True(UIElementHighlightSystem.ShouldShowHoverHighlight(ui));
	}

	[Fact]
	public void Layout_disables_hover_highlight_when_equipment_is_used_but_keeps_tooltips_available()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "helm_of_seeing");
		equipment.GetComponent<EquippedEquipment>().Equipment.MarkUsed();
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);
		var tooltipDisplay = new EquipmentTooltipDisplaySystem(entityManager, null, null, null);

		display.Update(Frame());

		var ui = equipment.GetComponent<UIElement>();
		Assert.False(ui.ShowHoverHighlight);
		Assert.True(ui.IsInteractable);
		Assert.Equal(TooltipType.Equipment, ui.TooltipType);
		ui.IsHovered = true;
		tooltipDisplay.Update(Frame());

		Assert.Same(
			equipment,
			entityManager.GetEntitiesWithComponent<EquipmentTooltipState>()
				.Single()
				.GetComponent<EquipmentTooltipState>()
				.EquipmentEntity);
		Assert.False(UIElementHighlightSystem.ShouldShowHoverHighlight(ui));
	}

	[Fact]
	public void Assigned_equipment_hover_uses_the_shared_equipment_tooltip()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Block);
		var equipment = AddEquipment(entityManager, player, "knightly_grieves");
		var ui = equipment.GetComponent<UIElement>();
		ui.Bounds = new Rectangle(680, 240, 76, 96);
		ui.TooltipType = TooltipType.Equipment;
		ui.IsHovered = true;
		entityManager.AddComponent(equipment, new EquipmentZone { Zone = EquipmentZoneType.AssignedBlock });
		entityManager.AddComponent(equipment, new AssignedBlockCard { IsEquipment = true });

		var tooltipEntity = entityManager.CreateEntity(EquipmentDisplaySystem.TooltipEntityName);
		entityManager.AddComponent(tooltipEntity, new EquipmentTooltipState());
		entityManager.AddComponent(tooltipEntity, new Transform());
		entityManager.AddComponent(tooltipEntity, new UIElement());

		new EquipmentTooltipDisplaySystem(entityManager, null, null, null).Update(Frame());

		var state = tooltipEntity.GetComponent<EquipmentTooltipState>();
		Assert.Same(equipment, state.EquipmentEntity);
		Assert.Same(equipment, state.AnchorEntity);
		Assert.True(state.TargetVisible);
	}

	[Fact]
	public void Action_phase_click_on_available_free_action_equipment_emits_activation_event()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "purging_bracers");
		new EquipmentDisplaySystem(entityManager, null, null, null).Update(Frame());
		equipment.GetComponent<UIElement>().IsClicked = true;
		int activationRequests = 0;
		EventManager.Subscribe<EquipmentActivateEvent>(evt =>
		{
			Assert.Same(equipment, evt.EquipmentEntity);
			activationRequests++;
		});

		new EquipmentBlockInteractionSystem(entityManager).Update(Frame());

		Assert.Equal(1, activationRequests);
	}

	[Fact]
	public void Equipment_tracks_persistent_uses_and_clears_per_battle_lock_on_refresh()
	{
		var equipment = EquipmentFactory.Create("pierced_heart_plate");

		Assert.Equal(3, equipment.MaxUses);
		Assert.Equal(3, equipment.RemainingUses);

		equipment.MarkUsed();

		Assert.True(equipment.IsUsed);
		Assert.Equal(2, equipment.RemainingUses);
		Assert.False(equipment.IsAvailable);

		equipment.RefreshForBattle();

		Assert.False(equipment.IsUsed);
		Assert.Equal(2, equipment.RemainingUses);
		Assert.True(equipment.IsAvailable);
	}

	[Fact]
	public void Equipment_stays_unavailable_after_all_uses_are_spent()
	{
		var equipment = EquipmentFactory.Create("pierced_heart_plate");

		equipment.MarkUsed();
		equipment.RefreshForBattle();
		equipment.MarkUsed();
		equipment.RefreshForBattle();
		equipment.MarkUsed();

		Assert.Equal(0, equipment.RemainingUses);
		Assert.True(equipment.IsUsed);
		Assert.False(equipment.IsAvailable);

		equipment.RefreshForBattle();

		Assert.False(equipment.IsUsed);
		Assert.Equal(0, equipment.RemainingUses);
		Assert.False(equipment.IsAvailable);
	}

	[Fact]
	public void Enemy_defeat_clears_per_battle_lock_without_restoring_uses()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "pierced_heart_plate");
		var model = equipment.GetComponent<EquippedEquipment>().Equipment;
		model.MarkUsed();
		_ = new EquipmentManagerSystem(entityManager);

		EventManager.Publish(new EnemyKilledEvent());

		Assert.False(model.IsUsed);
		Assert.Equal(2, model.RemainingUses);
		Assert.True(model.IsAvailable);
	}

	[Fact]
	public void Block_resolution_marks_equipment_used()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Block);
		var equipment = AddEquipment(entityManager, player, "knightly_grieves");
		entityManager.AddComponent(equipment, new AssignedBlockCard { IsEquipment = true });

		QueuedDiscardAssignedBlocksEvent.ResolveImmediately(entityManager, discardSpentBlocks: true);

		var model = equipment.GetComponent<EquippedEquipment>().Equipment;
		Assert.True(model.IsUsed);
		Assert.Equal(2, model.RemainingUses);
	}

	[Fact]
	public void Activation_marks_equipment_used()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "pierced_heart_plate");
		_ = new EquipmentManagerSystem(entityManager);

		EventManager.Publish(new EquipmentActivateEvent { EquipmentEntity = equipment });

		var model = equipment.GetComponent<EquippedEquipment>().Equipment;
		Assert.True(model.IsUsed);
		Assert.Equal(2, model.RemainingUses);
	}

	[Fact]
	public void Zero_block_equipment_cannot_be_assigned_to_block()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Block);
		var equipment = AddEquipment(entityManager, player, "bulwark_plate");
		equipment.GetComponent<EquippedEquipment>().Equipment.Block = 0;
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned =
			[
				new PlannedAttack
				{
					AttackDefinition = new EnemyAttackBase
					{
						Id = EnemyAttackId.Cinderbolt,
						Damage = 5,
					},
				},
			],
		});
		new EquipmentDisplaySystem(entityManager, null, null, null).Update(Frame());
		equipment.GetComponent<UIElement>().IsClicked = true;

		new EquipmentBlockInteractionSystem(entityManager).Update(Frame());

		Assert.Null(equipment.GetComponent<AssignedBlockCard>());
	}

	[Fact]
	public void Layout_preserves_foreign_named_equipment_tooltip_entities()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		AddEquipment(entityManager, player, "helm_of_seeing");
		CreateNamedTooltip(entityManager, "CardListModal_EquipmentTooltip");
		CreateNamedTooltip(entityManager, "BoosterPack_EquipmentTooltip");
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);

		display.Update(Frame());

		Assert.NotNull(entityManager.GetEntity("CardListModal_EquipmentTooltip")
			?.GetComponent<EquipmentTooltipState>());
		Assert.NotNull(entityManager.GetEntity("BoosterPack_EquipmentTooltip")
			?.GetComponent<EquipmentTooltipState>());
		Assert.NotNull(entityManager.GetEntity(EquipmentDisplaySystem.TooltipEntityName)
			?.GetComponent<EquipmentTooltipState>());
		Assert.Equal(3, entityManager.GetEntitiesWithComponent<EquipmentTooltipState>().Count());
	}

	[Fact]
	public void Hover_with_foreign_tooltip_present_layouts_battle_tooltip_centered_to_the_right()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		CreateNamedTooltip(entityManager, "Climb_EquipmentTooltip");
		var equipment = AddEquipment(entityManager, player, "helm_of_seeing");
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);
		var tooltipDisplay = new EquipmentTooltipDisplaySystem(
			entityManager,
			null,
			null,
			null,
			EquipmentDisplaySystem.TooltipEntityName);

		display.Update(Frame());
		equipment.GetComponent<UIElement>().IsHovered = true;
		tooltipDisplay.Update(Frame());

		var battleTooltip = entityManager.GetEntity(EquipmentDisplaySystem.TooltipEntityName);
		var battleState = battleTooltip.GetComponent<EquipmentTooltipState>();
		var foreignState = entityManager.GetEntity("Climb_EquipmentTooltip")
			.GetComponent<EquipmentTooltipState>();
		Rectangle panelBounds = display.GetPanelWorldBounds(equipment);
		Rectangle tooltipBounds = tooltipDisplay.GetTooltipWorldBounds();

		Assert.Same(equipment, battleState.EquipmentEntity);
		Assert.True(battleState.TargetVisible);
		Assert.False(foreignState.TargetVisible);
		Assert.Null(foreignState.EquipmentEntity);
		Assert.Equal(panelBounds.Right + tooltipDisplay.TooltipGap, tooltipBounds.X);
		Assert.InRange(tooltipBounds.Center.Y, panelBounds.Center.Y - 1, panelBounds.Center.Y + 1);
	}

	[Fact]
	public void Teardown_destroys_only_battle_equipment_tooltip_entity()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		AddEquipment(entityManager, player, "helm_of_seeing");
		CreateNamedTooltip(entityManager, "CardListModal_EquipmentTooltip");
		CreateNamedTooltip(entityManager, "BoosterPack_EquipmentTooltip");
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);
		display.Update(Frame());

		entityManager.GetEntitiesWithComponent<SceneState>().First()
			.GetComponent<SceneState>().Current = SceneId.Climb;
		display.Update(Frame());

		Assert.NotNull(entityManager.GetEntity("CardListModal_EquipmentTooltip")
			?.GetComponent<EquipmentTooltipState>());
		Assert.NotNull(entityManager.GetEntity("BoosterPack_EquipmentTooltip")
			?.GetComponent<EquipmentTooltipState>());
		Assert.Null(entityManager.GetEntity(EquipmentDisplaySystem.TooltipEntityName));
		Assert.Empty(entityManager.GetEntitiesWithComponent<EquipmentDisplayRoot>());
		Assert.Equal(2, entityManager.GetEntitiesWithComponent<EquipmentTooltipState>().Count());
	}

	[Fact]
	public void MarkUsed_persists_remaining_uses_to_the_loadout()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		RunEquipmentService.ApplyEquipmentToLoadout(loadout, "pierced_heart_plate");
		SaveCache.SaveLoadout(loadout);

		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "pierced_heart_plate");
		_ = new EquipmentManagerSystem(entityManager);

		equipment.GetComponent<EquippedEquipment>().Equipment.MarkUsed();

		var saved = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		Assert.Equal(2, saved.chestRemainingUses);
	}

	[Fact]
	public void CreatePlayer_restores_remaining_uses_from_the_loadout()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.chestId = "pierced_heart_plate";
		loadout.chestRemainingUses = 1;
		SaveCache.SaveLoadout(loadout);

		var world = new World();
		EntityFactory.CreatePlayer(world);

		var equipment = world.EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
			.Select(entity => entity.GetComponent<EquippedEquipment>().Equipment)
			.Single(item => item.Slot == EquipmentSlot.Chest);

		Assert.Equal(1, equipment.RemainingUses);
		Assert.True(equipment.IsAvailable);
	}

	[Fact]
	public void EquipOnPlayer_restores_remaining_uses_from_the_loadout()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.headId = "helm_of_seeing";
		loadout.headRemainingUses = 0;
		SaveCache.SaveLoadout(loadout);

		var entityManager = new EntityManager();
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());

		var equipmentEntity = RunEquipmentService.EquipOnPlayer(entityManager, "helm_of_seeing");
		var equipment = equipmentEntity.GetComponent<EquippedEquipment>().Equipment;

		Assert.Equal(0, equipment.RemainingUses);
		Assert.False(equipment.IsAvailable);
	}

	private static void CreateNamedTooltip(EntityManager entityManager, string name)
	{
		var entity = entityManager.CreateEntity(name);
		entityManager.AddComponent(entity, new EquipmentTooltipState());
		entityManager.AddComponent(entity, new Transform());
		entityManager.AddComponent(entity, new UIElement
		{
			IsInteractable = false,
			IsHidden = true,
			TooltipType = TooltipType.None,
		});
	}

	private static EntityManager BuildBattle(out Entity player, SubPhase subPhase)
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Battle });
		var phase = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phase, new PhaseState
		{
			Main = subPhase == SubPhase.Action ? MainPhase.PlayerTurn : MainPhase.EnemyTurn,
			Sub = subPhase,
		});
		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		return entityManager;
	}

	private static Entity AddEquipment(EntityManager entityManager, Entity player, string equipmentId)
	{
		var entity = entityManager.CreateEntity($"Equipment_{equipmentId}");
		var equipment = EquipmentFactory.Create(equipmentId);
		equipment.Initialize(entityManager, entity);
		entityManager.AddComponent(entity, new Transform());
		entityManager.AddComponent(entity, new UIElement { IsInteractable = true });
		entityManager.AddComponent(entity, new EquippedEquipment
		{
			EquippedOwner = player,
			Equipment = equipment,
		});
		return entity;
	}

	private static GameTime Frame()
	{
		return new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1d / 60d));
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
