using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using System;

namespace Crusaders30XX.ECS.Systems
{
	public sealed class QueuedWaitVisualEffectImpact : EventQueue.IQueuedEvent
	{
		public string Name { get; }
		public object Payload { get; }
		public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

		private readonly Guid _requestId;
		private Action<VisualEffectImpactReached> _handler;

		public QueuedWaitVisualEffectImpact(Guid requestId)
		{
			_requestId = requestId;
			Name = "Rule.WaitVisualEffectImpact";
			Payload = requestId;
		}

		public void StartResolving()
		{
			State = EventQueue.EventState.Waiting;
			_handler = OnImpactReached;
			EventManager.Subscribe(_handler);
		}

		public void Update(float deltaSeconds) { }

		private void OnImpactReached(VisualEffectImpactReached evt)
		{
			if (evt == null || evt.RequestId != _requestId) return;
			EventManager.Unsubscribe(_handler);
			State = EventQueue.EventState.Complete;
		}
	}
}
