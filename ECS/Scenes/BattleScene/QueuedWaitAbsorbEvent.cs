using System.Text.Json.Nodes;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Queued event that waits for the enemy absorb animation to complete (EnemyAbsorbComplete).
    /// </summary>
    public class QueuedWaitAbsorbEvent : EventQueue.IQueuedEvent
    {
        public string Name { get; }
        public object Payload { get; }
        public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

        private System.Action<EnemyAbsorbComplete> _handler;

        public QueuedWaitAbsorbEvent()
        {
            Name = "Rule.WaitAbsorb";
            Payload = null;
        }

        public void StartResolving()
        {
            State = EventQueue.EventState.Waiting;
            _handler = OnAbsorbComplete;
            EventManager.Subscribe(_handler);
        }

        public void Update(float deltaSeconds) { }

        private void OnAbsorbComplete(EnemyAbsorbComplete e)
        {
            LoggingService.Append("QueuedWaitAbsorbEvent.OnAbsorbComplete", new JsonObject());
            EventManager.Unsubscribe(_handler);
            State = EventQueue.EventState.Complete;
        }
    }
}

