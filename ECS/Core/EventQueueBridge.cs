using System;

namespace ChurchSuffering.ECS.Core
{
    /// <summary>
    /// Generic bridge helpers to interoperate between EventQueue and EventManager without game-specific logic.
    /// </summary>
    public static class EventQueueBridge
    {
        /// <summary>
        /// A queued event that immediately publishes a bus event (via EventManager) when it starts, then completes.
        /// </summary>
        public class QueuedPublish<T> : EventQueue.IQueuedEvent where T : class
        {
            public string Name { get; }
            public object Payload { get; }
            public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

            private readonly T _eventData;

            public QueuedPublish(string name, T eventData)
            {
                Name = name;
                Payload = eventData;
                _eventData = eventData;
            }

            public void StartResolving()
            {
                EventManager.Publish(_eventData);
                State = EventQueue.EventState.Complete;
            }

            public void Update(float deltaSeconds) { }
        }

        /// <summary>
        /// A queued event that waits until a bus event of type T is published, then completes.
        /// </summary>
        public class WaitForEvent<T> : EventQueue.IQueuedEvent where T : class
        {
            public string Name { get; }
            public object Payload { get; }
            public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

            private Action<T> _handler;

            public WaitForEvent(string name, object payload = null)
            {
                Name = name;
                Payload = payload;
            }

            public void StartResolving()
            {
                State = EventQueue.EventState.Waiting;
                _handler = OnEvent;
                EventManager.Subscribe(_handler);
            }

            public void Update(float deltaSeconds) { }

            private void OnEvent(T _)
            {
                // Complete and detach
                State = EventQueue.EventState.Complete;
                EventManager.Unsubscribe(_handler);
            }
        }

        /// <summary>
        /// A queued trigger that executes an arbitrary Action immediately when it starts, then waits a fixed time before completing.
        /// This lets you run any block of code and intentionally stall the queue for visual pacing or timing.
        /// </summary>
        public class TriggerActionThenWait : EventQueue.IQueuedEvent
        {
            public string Name { get; }
            public object Payload { get; }
            public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

            private readonly Action _action;
            private float _remainingSeconds;

            public TriggerActionThenWait(string name, Action action, float waitSeconds, object payload = null)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                Name = name;
                _action = action;
                _remainingSeconds = Math.Max(0f, waitSeconds);
                // Default payload is the wait duration for easy introspection if none provided
                Payload = payload ?? waitSeconds;
            }

            public void StartResolving()
            {
                // Execute immediately
                _action();

                if (_remainingSeconds > 0f)
                {
                    State = EventQueue.EventState.Waiting;
                }
                else
                {
                    State = EventQueue.EventState.Complete;
                }
            }

            public void Update(float deltaSeconds)
            {
                if (State != EventQueue.EventState.Waiting) return;
                _remainingSeconds -= Math.Max(0f, deltaSeconds);
                if (_remainingSeconds <= 0f)
                {
                    State = EventQueue.EventState.Complete;
                }
            }
        }

        /// <summary>
        /// Helper to enqueue a trigger that runs an Action and then waits for a duration before allowing the next event.
        /// </summary>
        public static void EnqueueTriggerAction(string name, Action action, float waitSeconds, object payload = null)
        {
            EventQueue.EnqueueTrigger(new TriggerActionThenWait(name, action, waitSeconds, payload));
        }

        /// <summary>
        /// Overload with an auto-generated name.
        /// </summary>
        public static void EnqueueTriggerAction(Action action, float waitSeconds)
        {
            EnqueueTriggerAction("TriggerActionThenWait", action, waitSeconds);
        }
    }
}


