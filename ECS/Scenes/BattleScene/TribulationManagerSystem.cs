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
	/// Listens to battle phase changes and triggers tribulations when their triggers match.
	/// Triggers tribulations AFTER medals using the same delay mechanism.
	/// </summary>

	[DebugTab("TribulationManagerSystem")]
	public class TribulationManagerSystem : Core.System
	{
		[DebugEditable(DisplayName = "Activation Delay (s)", Step = 0.05f, Min = 0f, Max = 2f)]
		public float ActivationDelaySeconds { get; set; } = 0.3f;

		public TribulationManagerSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ChangeBattlePhaseEvent>(OnPhaseChanged);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnPhaseChanged(ChangeBattlePhaseEvent e)
		{
			if (e.Current != SubPhase.StartBattle) return;
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player == null) return;
			LoggingService.Append("TribulationManagerSystem.OnPhaseChanged", new System.Text.Json.Nodes.JsonObject { ["phase"] = e.Current.ToString() });

			// Get all tribulations for the player
			var tribulations = EntityManager.GetEntitiesWithComponent<Tribulation>()
				.Where(e => e.GetComponent<Tribulation>()?.PlayerOwner == player)
				.Select(e => e.GetComponent<Tribulation>())
				.Where(t => t != null && string.Equals(t.Trigger, "StartOfBattle", StringComparison.OrdinalIgnoreCase))
				.ToList();
			LoggingService.Append("TribulationManagerSystem.OnPhaseChanged.found", new System.Text.Json.Nodes.JsonObject { ["count"] = tribulations.Count });
			foreach (var tribulation in tribulations)
			{
				ActivateTribulation(player, tribulation);
			}
		}

		private void ActivateTribulation(Entity player, Tribulation tribulation)
		{
			LoggingService.Append("TribulationManagerSystem.ActivateTribulation", new System.Text.Json.Nodes.JsonObject { ["questId"] = tribulation.QuestId });
			if (player == null || tribulation == null || string.IsNullOrEmpty(tribulation.QuestId)) return;

			EventQueueBridge.EnqueueTriggerAction(() =>
			{
				EventManager.Publish(new TribulationTriggered { QuestId = tribulation.QuestId });
				ApplyTribulationService.ApplyTribulationEffect(player, tribulation.QuestId);
			}, ActivationDelaySeconds);
		}
	}
}

