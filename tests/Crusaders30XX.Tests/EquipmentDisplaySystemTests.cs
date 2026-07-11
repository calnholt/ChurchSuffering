using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
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
		Assert.Equal(new Vector2(panelBounds.Center.X, panelBounds.Center.Y), assigned.StartPos);
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
	public void Quest_reward_overlay_does_not_replenish_equipment_uses()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "helm_of_seeing");
		equipment.GetComponent<EquippedEquipment>().Equipment.RemainingUses = 0;
		_ = new EquipmentManagerSystem(entityManager);

		EventManager.Publish(new ShowQuestRewardOverlay());

		var model = equipment.GetComponent<EquippedEquipment>().Equipment;
		Assert.Equal(0, model.RemainingUses);
	}

	[Theory]
	[InlineData(SubPhase.Block, "knightly_grieves", 0, "Not enough uses!")]
	[InlineData(SubPhase.Action, "knightly_grieves", 2, "This equipment cannot be activated during the Action phase!")]
	[InlineData(SubPhase.Action, "purging_bracers", 0, "Not enough uses!")]
	public void Invalid_block_and_action_clicks_emit_expected_message(
		SubPhase phase,
		string equipmentId,
		int remainingUses,
		string expectedMessage)
	{
		var entityManager = BuildBattle(out var player, phase);
		var equipment = AddEquipment(entityManager, player, equipmentId);
		equipment.GetComponent<EquippedEquipment>().Equipment.RemainingUses = remainingUses;
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
	public void Layout_enables_hover_highlight_when_equipment_has_uses()
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
	public void Layout_disables_hover_highlight_when_equipment_exhausted()
	{
		var entityManager = BuildBattle(out var player, SubPhase.Action);
		var equipment = AddEquipment(entityManager, player, "helm_of_seeing");
		equipment.GetComponent<EquippedEquipment>().Equipment.RemainingUses = 0;
		var display = new EquipmentDisplaySystem(entityManager, null, null, null);

		display.Update(Frame());

		var ui = equipment.GetComponent<UIElement>();
		Assert.False(ui.ShowHoverHighlight);
		ui.IsHovered = true;
		Assert.False(UIElementHighlightSystem.ShouldShowHoverHighlight(ui));
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
