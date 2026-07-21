using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using System;
using System.Text.Json.Nodes;

namespace ChurchSuffering.ECS.Systems
{
	public sealed class QueuedActivateEquipmentWithVisual : EventQueue.IQueuedEvent
	{
		public string Name { get; }
		public object Payload { get; }
		public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

		private readonly EntityManager _entityManager;
		private readonly Entity _equipmentEntity;
		private Action<VisualEffectCompleted> _handler;
		private Guid _requestId;

		public QueuedActivateEquipmentWithVisual(EntityManager entityManager, Entity equipmentEntity)
		{
			_entityManager = entityManager;
			_equipmentEntity = equipmentEntity;
			Name = "Trigger.ActivateEquipmentWithVisual";
			Payload = equipmentEntity?.Id;
		}

		public void StartResolving()
		{
			var equipped = _equipmentEntity?.GetComponent<EquippedEquipment>();
			var equipment = equipped?.Equipment;
			if (equipment == null)
			{
				State = EventQueue.EventState.Complete;
				return;
			}

			equipment.OnActivate(_entityManager, _equipmentEntity);
			equipment.MarkUsed();
			EventManager.Publish(new EquipmentAbilityTriggered { Equipment = _equipmentEntity, EquipmentId = equipment.Id });

			var request = VisualEffectRequestFactory.ForEquipment(
				_entityManager,
				_equipmentEntity,
				equipment.ActivationEffectRecipe);
			if (request == null)
			{
				LoggingService.Append("QueuedActivateEquipmentWithVisual.StartResolving", new JsonObject
				{
					["reason"] = "RequestCreationFailed",
					["equipmentId"] = equipment.Id ?? string.Empty
				});
				State = EventQueue.EventState.Complete;
				return;
			}

			_requestId = request.RequestId;
			_handler = OnCompleted;
			EventManager.Subscribe(_handler);
			EventManager.Publish(request);
			State = EventQueue.EventState.Waiting;
		}

		public void Update(float deltaSeconds) { }

		private void OnCompleted(VisualEffectCompleted evt)
		{
			if (evt == null || evt.RequestId != _requestId) return;
			EventManager.Unsubscribe(_handler);
			State = EventQueue.EventState.Complete;
		}
	}
}
