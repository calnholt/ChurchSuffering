using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.Diagnostics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Event Queue")]
	public class EventQueueSystem : Core.System
	{
		[DebugEditable(DisplayName = "Enqueue Log (Rule)")]
		public bool DebugEnqueueLogRule { get; set; }

		[DebugEditable(DisplayName = "Enqueue Log (Trigger)")]
		public bool DebugEnqueueLogTrigger { get; set; }

		[DebugEditable(DisplayName = "Enqueue Wait 0.5s (Rule)")]
		public bool DebugEnqueueWaitRule { get; set; }

		[DebugEditable(DisplayName = "Clear Queues")]
		public bool DebugClear { get; set; }

		[DebugEditable(DisplayName = "Enqueue Bus Publish (Rule)")]
		public bool DebugEnqueueBusPublishRule { get; set; }

		[DebugEditable(DisplayName = "Enqueue WaitFor Bus Event (Trigger)")]
		public bool DebugEnqueueWaitBusTrigger { get; set; }

		public EventQueueSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			EventQueue.Update(dt);

			// Handle debug toggles once
			if (DebugEnqueueLogRule)
			{
				DebugEnqueueLogRule = false;
				EventQueue.EnqueueRule(new EventQueue.LogEvent("Rule.Log", "Hello from Rules queue"));
			}
			if (DebugEnqueueLogTrigger)
			{
				DebugEnqueueLogTrigger = false;
				EventQueue.EnqueueTrigger(new EventQueue.LogEvent("Trigger.Log", "Hello from Trigger queue"));
			}
			if (DebugEnqueueWaitRule)
			{
				DebugEnqueueWaitRule = false;
				EventQueue.EnqueueRule(new EventQueue.WaitSecondsEvent("Rule.Wait", 0.5f));
			}
			if (DebugClear)
			{
				DebugClear = false;
				EventQueue.Clear();
			}

			if (DebugEnqueueBusPublishRule)
			{
				DebugEnqueueBusPublishRule = false;
				// Publish a DebugCommandEvent via the queue when it starts
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChurchSuffering.ECS.Events.DebugCommandEvent>(
					"Rule.PublishBus", new ChurchSuffering.ECS.Events.DebugCommandEvent { Command = "EventQueue.Published" }
				));
			}
			if (DebugEnqueueWaitBusTrigger)
			{
				DebugEnqueueWaitBusTrigger = false;
				// Wait in the trigger queue until that bus event fires
				EventQueue.EnqueueTrigger(new EventQueueBridge.WaitForEvent<ChurchSuffering.ECS.Events.DebugCommandEvent>(
					"Trigger.WaitBus", payload: null
				));
			}
		}
	}
}


