using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class AssignedBlockLifecycleSystemTests : IDisposable
{
	public AssignedBlockLifecycleSystemTests()
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
	public void Stale_unassign_request_is_a_no_op()
	{
		var entityManager = new EntityManager();
		var equipment = entityManager.CreateEntity("ReturnedEquipment");
		entityManager.AddComponent(equipment, new UIElement
		{
			IsInteractable = true,
			EventType = UIElementEventType.UnassignCardAsBlock,
		});
		_ = new AssignedBlockLifecycleSystem(entityManager);

		EventManager.Publish(new UnassignCardAsBlockRequested { CardEntity = equipment });

		Assert.False(equipment.HasComponent<AssignedBlockCard>());
		Assert.True(equipment.GetComponent<UIElement>().IsInteractable);
	}

	[Fact]
	public void Equipment_return_clears_assignment_presentation_and_stale_event_type()
	{
		var entityManager = new EntityManager();
		var equipment = entityManager.CreateEntity("Equipment");
		entityManager.AddComponent(equipment, new AssignedBlockCard { IsEquipment = true });
		entityManager.AddComponent(equipment, new AssignedBlockPresentation
		{
			Phase = AssignedBlockPresentation.PhaseState.Returning,
		});
		entityManager.AddComponent(equipment, new EquipmentZone { Zone = EquipmentZoneType.AssignedBlock });
		entityManager.AddComponent(equipment, new UIElement
		{
			EventType = UIElementEventType.UnassignCardAsBlock,
			IsInteractable = false,
		});
		_ = new AssignedBlockLifecycleSystem(entityManager);

		EventManager.Publish(new AssignedBlockReturnCompleted { Card = equipment });

		Assert.False(equipment.HasComponent<AssignedBlockCard>());
		Assert.False(equipment.HasComponent<AssignedBlockPresentation>());
		Assert.Equal(EquipmentZoneType.Default, equipment.GetComponent<EquipmentZone>().Zone);
		Assert.Equal(UIElementEventType.None, equipment.GetComponent<UIElement>().EventType);
		Assert.True(equipment.GetComponent<UIElement>().IsInteractable);
	}

	[Fact]
	public void Hotkey_moves_to_newest_idle_assignment_and_falls_back()
	{
		var entityManager = new EntityManager();
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent { Planned = { new PlannedAttack() } });
		var older = AddAssignment(entityManager, "Older", 10);
		var newer = AddAssignment(entityManager, "Newer", 20);
		var system = new AssignedBlockLifecycleSystem(entityManager);

		EventManager.Publish(new BlockAssignmentAdded { Card = older });
		EventManager.Publish(new BlockAssignmentAdded { Card = newer });

		Assert.Null(older.GetComponent<HotKey>());
		Assert.Equal(FaceButton.B, newer.GetComponent<HotKey>().Button);
		Assert.False(newer.GetComponent<HotKey>().IsKeyboardMouseEnabled);

		newer.GetComponent<AssignedBlockPresentation>().Phase = AssignedBlockPresentation.PhaseState.Returning;
		system.Update(new GameTime());

		Assert.Equal(FaceButton.B, older.GetComponent<HotKey>().Button);
		Assert.False(older.GetComponent<HotKey>().IsKeyboardMouseEnabled);
		Assert.Null(newer.GetComponent<HotKey>());
	}

	private static Entity AddAssignment(EntityManager entityManager, string name, long assignedAt)
	{
		var entity = entityManager.CreateEntity(name);
		entityManager.AddComponent(entity, new AssignedBlockCard { AssignedAtTicks = assignedAt });
		entityManager.AddComponent(entity, new AssignedBlockPresentation
		{
			Phase = AssignedBlockPresentation.PhaseState.Idle,
		});
		entityManager.AddComponent(entity, new UIElement());
		return entity;
	}
}
