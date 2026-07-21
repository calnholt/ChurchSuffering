using System;
using System.Collections.Generic;

namespace ChurchSuffering.ECS.Core
{
    /// <summary>
    /// Lightweight, generic event queue manager with separate Rules and Trigger queues.
    /// No coupling to game logic. Consumers define their own event payload types.
    /// </summary>
    public static class EventQueue
    {
        public enum EventState { Pending, Resolving, Waiting, Complete }

        public interface IQueuedEvent
        {
            string Name { get; }
            object Payload { get; }
            EventState State { get; set; }
            void StartResolving();
            void Update(float deltaSeconds);
        }

        private static readonly Queue<IQueuedEvent> _rules = new();
        private static readonly Queue<IQueuedEvent> _triggers = new();
        private static IQueuedEvent _active;

        public static bool IsIdle => _active == null && _rules.Count == 0 && _triggers.Count == 0;
        public static int RulesCount => _rules.Count;
        public static int TriggersCount => _triggers.Count;

        public static void EnqueueRule(IQueuedEvent evt)
        {
            if (evt != null) _rules.Enqueue(evt);
        }

        public static void EnqueueTrigger(IQueuedEvent evt)
        {
            if (evt != null) _triggers.Enqueue(evt);
        }

        public static void Clear()
        {
            _rules.Clear();
            _triggers.Clear();
            _active = null;
        }

        public static void Update(float deltaSeconds)
        {
            if (_active == null)
            {
                // Triggers get priority when idle so they can stall just like rules
                if (_triggers.Count > 0)
                {
                    _active = _triggers.Dequeue();
                }
                else if (_rules.Count > 0)
                {
                    _active = _rules.Dequeue();
                }

                if (_active != null)
                {
                    if (_active.State == EventState.Pending)
                    {
                        _active.State = EventState.Resolving;
                        _active.StartResolving();
                    }
                }
            }

            if (_active != null)
            {
                // Allow long-running events to progress
                _active.Update(deltaSeconds);
                if (_active.State == EventState.Complete)
                {
                    _active = null;
                }
            }
        }

        // Simple built-in events for testing only (no game coupling)
        public class LogEvent : IQueuedEvent
        {
            public string Name { get; }
            public object Payload { get; }
            public EventState State { get; set; } = EventState.Pending;

            public LogEvent(string name, string message)
            {
                Name = name;
                Payload = message;
            }

            public void StartResolving()
            {
                Console.WriteLine($"[EventQueue] {Name}: {Payload}");
                State = EventState.Complete;
            }

            public void Update(float deltaSeconds) { }
        }

        public class WaitSecondsEvent : IQueuedEvent
        {
            public string Name { get; }
            public object Payload { get; }
            public EventState State { get; set; } = EventState.Pending;
            private float _remaining;

            public WaitSecondsEvent(string name, float seconds)
            {
                Name = name;
                Payload = seconds;
                _remaining = Math.Max(0f, seconds);
            }

            public void StartResolving()
            {
                State = EventState.Waiting;
            }

            public void Update(float deltaSeconds)
            {
                if (State != EventState.Waiting) return;
                _remaining -= Math.Max(0f, deltaSeconds);
                if (_remaining <= 0f)
                {
                    State = EventState.Complete;
                }
            }
        }
    }
}


