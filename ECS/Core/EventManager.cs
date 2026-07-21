using System;
using System.Collections.Generic;
using System.Linq;

namespace ChurchSuffering.ECS.Core
{
    /// <summary>
    /// Manages event-driven communication between systems
    /// </summary>
    public static class EventManager
    {
        /// <summary>
        /// Wrapper class to store handler delegate with its priority
        /// </summary>
        private class PrioritizedHandler
        {
            public Delegate Handler { get; set; }
            public int Priority { get; set; }
        }

        private sealed class HandlerCollection
        {
            public List<PrioritizedHandler> Handlers { get; } = new();
            public PrioritizedHandler[] OrderedSnapshot { get; private set; } = Array.Empty<PrioritizedHandler>();

            public void RebuildSnapshot()
            {
                OrderedSnapshot = Handlers
                    .OrderByDescending(handler => handler.Priority)
                    .ToArray();
            }
        }

        private static readonly Dictionary<Type, HandlerCollection> _eventHandlers = new();
        
        /// <summary>
        /// Subscribe to an event type with optional priority.
        /// Higher priority handlers execute first. Default priority is 0.
        /// </summary>
        /// <param name="handler">The handler to subscribe</param>
        /// <param name="priority">Execution priority (higher = earlier). Default: 0</param>
        public static void Subscribe<T>(Action<T> handler, int priority = 0) where T : class
        {
            var eventType = typeof(T);
            if (!_eventHandlers.ContainsKey(eventType))
            {
                _eventHandlers[eventType] = new HandlerCollection();
            }
            HandlerCollection collection = _eventHandlers[eventType];
            collection.Handlers.Add(new PrioritizedHandler
            { 
                Handler = handler, 
                Priority = priority 
            });
            collection.RebuildSnapshot();
        }
        
        /// <summary>
        /// Unsubscribe from an event type
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : class
        {
            var eventType = typeof(T);
            if (_eventHandlers.ContainsKey(eventType))
            {
                HandlerCollection collection = _eventHandlers[eventType];
                collection.Handlers.RemoveAll(ph => ph.Handler.Equals(handler));
                collection.RebuildSnapshot();
            }
        }
        
        /// <summary>
        /// Publish an event to all subscribers in priority order (highest first)
        /// </summary>
        public static void Publish<T>(T eventData) where T : class
        {
            var eventType = typeof(T);
            if (_eventHandlers.ContainsKey(eventType))
            {
                PrioritizedHandler[] handlers = _eventHandlers[eventType].OrderedSnapshot;
                foreach (var prioritizedHandler in handlers)
                {
                    try
                    {
                        ((Action<T>)prioritizedHandler.Handler)(eventData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in event handler: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Publishes handlers in their normal priority order while allowing the caller to
        /// measure the high- and low-priority portions independently.
        /// </summary>
        public static void PublishPartitioned<T>(
            T eventData,
            int highPriorityMinimum,
            Action<Action> measureHighPriority,
            Action<Action> measureLowPriority) where T : class
        {
            if (!_eventHandlers.TryGetValue(typeof(T), out HandlerCollection collection)) return;
            PrioritizedHandler[] handlers = collection.OrderedSnapshot;
            InvokeMeasured(
                measureHighPriority,
                () => InvokeHandlers(handlers, eventData, handler => handler.Priority >= highPriorityMinimum));
            InvokeMeasured(
                measureLowPriority,
                () => InvokeHandlers(handlers, eventData, handler => handler.Priority < highPriorityMinimum));
        }

        private static void InvokeMeasured(Action<Action> measure, Action action)
        {
            if (measure != null) measure(action);
            else action();
        }

        private static void InvokeHandlers<T>(
            PrioritizedHandler[] handlers,
            T eventData,
            Func<PrioritizedHandler, bool> include) where T : class
        {
            foreach (PrioritizedHandler prioritizedHandler in handlers)
            {
                if (!include(prioritizedHandler)) continue;
                try
                {
                    ((Action<T>)prioritizedHandler.Handler)(eventData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in event handler: {ex}");
                }
            }
        }
        
        /// <summary>
        /// Clear all event handlers (useful for cleanup)
        /// </summary>
        public static void Clear()
        {
            _eventHandlers.Clear();
        }
        
        /// <summary>
        /// Get the number of active event handlers for debugging
        /// </summary>
        public static int GetEventHandlerCount()
        {
            return _eventHandlers.Values.Sum(collection => collection.Handlers.Count);
        }
    }
}
