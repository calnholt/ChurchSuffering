using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using System;

namespace ChurchSuffering.ECS.Systems
{
	public sealed class QueuedWaitVisualEffectComplete : EventQueue.IQueuedEvent
	{
		public string Name { get; }
		public object Payload { get; }
		public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

		private readonly Guid _requestId;
		private Action<VisualEffectCompleted> _handler;

		public QueuedWaitVisualEffectComplete(Guid requestId)
		{
			_requestId = requestId;
			Name = "Rule.WaitVisualEffectComplete";
			Payload = requestId;
		}

		public void StartResolving()
		{
			State = EventQueue.EventState.Waiting;
			_handler = OnCompleted;
			EventManager.Subscribe(_handler);
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
