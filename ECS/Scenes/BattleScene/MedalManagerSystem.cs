using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Listens to battle phase changes and triggers equipped medals when their triggers match.
	/// Initially supports StartOfBattle heal for Medal of Saint Luke.

	/// </summary>
	public class MedalManagerSystem : Core.System
	{
		[DebugEditable(DisplayName = "Activation Delay (s)", Step = 0.05f, Min = 0f, Max = 2f)]
		public float ActivationDelaySeconds { get; set; } = 0.3f;
		public MedalManagerSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<MedalActivateEvent>(OnMedalActivate);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnMedalActivate(MedalActivateEvent e)
		{
			LoggingService.Append("MedalManagerSystem.OnMedalActivate", new System.Text.Json.Nodes.JsonObject
			{
				["entityId"] = e.MedalEntity?.Id ?? -1
			});
			var medal = e.MedalEntity?.GetComponent<EquippedMedal>()?.Medal;
			if (medal == null) return;
			if (medal.ActivationEffectRecipe != null)
			{
				EventQueue.EnqueueTrigger(new QueuedActivateMedalWithVisual(EntityManager, e.MedalEntity));
				return;
			}
			EventQueueBridge.EnqueueTriggerAction(() =>
			{
				EventManager.Publish(new MedalTriggered { MedalEntity = e.MedalEntity, MedalId = medal.Id });
				medal.Activate();
			}, ActivationDelaySeconds);
		}

	}
}


