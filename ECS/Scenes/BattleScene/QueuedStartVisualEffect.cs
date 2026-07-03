using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Systems
{
	public sealed class QueuedStartVisualEffect : EventQueue.IQueuedEvent
	{
		public string Name { get; }
		public object Payload { get; }
		public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

		private readonly VisualEffectRequested _request;

		public QueuedStartVisualEffect(VisualEffectRequested request)
		{
			_request = request;
			Name = "Rule.StartVisualEffect";
			Payload = request?.RequestId;
		}

		public void StartResolving()
		{
			if (_request != null)
			{
				EventManager.Publish(_request);
			}
			State = EventQueue.EventState.Complete;
		}

		public void Update(float deltaSeconds) { }
	}
}
