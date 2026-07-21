using System;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Systems
{
	public sealed class QueuedShuffleDeckAnimationEvent : EventQueue.IQueuedEvent
	{
		public string Name { get; } = "Rule.ShuffleDeckAnimation";
		public object Payload => _requestId;
		public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

		private readonly string _reason;
		private readonly string _targetEntityName;
		private readonly Guid _requestId = Guid.NewGuid();
		private Action<ShuffleDeckAnimationCompleted> _handler;

		public QueuedShuffleDeckAnimationEvent(string reason, string targetEntityName = "UI_DrawPileRoot")
		{
			_reason = reason ?? string.Empty;
			_targetEntityName = string.IsNullOrWhiteSpace(targetEntityName) ? "UI_DrawPileRoot" : targetEntityName;
		}

		public void StartResolving()
		{
			_handler = OnCompleted;
			EventManager.Subscribe(_handler);
			EventManager.Publish(new ShuffleDeckAnimationRequested
			{
				RequestId = _requestId,
				Reason = _reason,
				TargetEntityName = _targetEntityName,
			});
			State = EventQueue.EventState.Waiting;
		}

		public void Update(float deltaSeconds) { }

		private void OnCompleted(ShuffleDeckAnimationCompleted evt)
		{
			if (evt == null || evt.RequestId != _requestId) return;
			EventManager.Unsubscribe(_handler);
			State = EventQueue.EventState.Complete;
		}
	}
}
