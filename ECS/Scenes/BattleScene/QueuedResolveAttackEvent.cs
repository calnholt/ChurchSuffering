using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Queued event that publishes ResolveAttack for the current active attack.
    /// Completes immediately after publishing.
    /// </summary>
    public class QueuedResolveAttackEvent : EventQueue.IQueuedEvent
    {
        public string Name { get; }
        public object Payload { get; }
        public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

        public QueuedResolveAttackEvent()
        {
            Name = "Rule.ResolveAttack";
            Payload = null;
        }

        public void StartResolving()
        {
            EventManager.Publish(new ResolveAttack());
            State = EventQueue.EventState.Complete;
        }

        public void Update(float deltaSeconds) { }
    }
}

