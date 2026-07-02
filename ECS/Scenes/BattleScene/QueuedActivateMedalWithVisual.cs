using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using System;
using System.Text.Json.Nodes;

namespace Crusaders30XX.ECS.Systems
{
	public sealed class QueuedActivateMedalWithVisual : EventQueue.IQueuedEvent
	{
		public string Name { get; }
		public object Payload { get; }
		public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

		private readonly EntityManager _entityManager;
		private readonly Entity _medalEntity;
		private Action<VisualEffectCompleted> _handler;
		private Guid _requestId;

		public QueuedActivateMedalWithVisual(EntityManager entityManager, Entity medalEntity)
		{
			_entityManager = entityManager;
			_medalEntity = medalEntity;
			Name = "Trigger.ActivateMedalWithVisual";
			Payload = medalEntity?.Id;
		}

		public void StartResolving()
		{
			var equipped = _medalEntity?.GetComponent<EquippedMedal>();
			var medal = equipped?.Medal;
			if (medal == null)
			{
				State = EventQueue.EventState.Complete;
				return;
			}

			EventManager.Publish(new MedalTriggered { MedalEntity = _medalEntity, MedalId = medal.Id });
			medal.Activate();

			var request = VisualEffectRequestFactory.ForMedal(
				_entityManager,
				_medalEntity,
				medal.ActivationEffectRecipe);
			if (request == null)
			{
				LoggingService.Append("QueuedActivateMedalWithVisual.StartResolving", new JsonObject
				{
					["reason"] = "RequestCreationFailed",
					["medalId"] = medal.Id ?? string.Empty
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
