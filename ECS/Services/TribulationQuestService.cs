using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Locations;
using System;
using System.Linq;

namespace ChurchSuffering.ECS.Services
{
	/// <summary>
	/// Service that creates tribulation components on the player based on quest data.
	/// </summary>
	public static class TribulationQuestService
	{
		/// <summary>
		/// Creates tribulation components on the player entity for the given quest.
		/// Reads tribulation data from LocationDefinitionCache and attaches components.
		/// </summary>
		public static void CreateTribulationsForQuest(EntityManager entityManager, string locationId, int questIndex)
		{
			if (entityManager == null || string.IsNullOrEmpty(locationId)) return;

			var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player == null) return;

			// Remove any existing tribulations first
			var existingTribulations = entityManager.GetEntitiesWithComponent<Tribulation>()
				.Where(e => e.GetComponent<Tribulation>()?.PlayerOwner == player)
				.ToList();
			foreach (var e in existingTribulations)
			{
				entityManager.DestroyEntity(e.Id);
			}

			// Get quest data from location definition
			if (!LocationDefinitionCache.TryGet(locationId, out var locationDef) || locationDef == null) return;
			if (questIndex < 0 || questIndex >= locationDef.pointsOfInterest.Count) return;
			var questDef = locationDef.pointsOfInterest[questIndex];
			if (questDef.tribulations == null || questDef.tribulations.Count == 0) return;
			// Create a tribulation component for each tribulation definition
			foreach (var tribDef in questDef.tribulations)
			{
				if (string.IsNullOrEmpty(tribDef.text) || string.IsNullOrEmpty(tribDef.trigger)) continue;

				var tribEntity = entityManager.CreateEntity($"Tribulation_{questDef.id}_{tribDef.trigger}");
        Console.WriteLine($"[TribulationQuestService] Creating tribulation for quest {questDef.id} {tribDef.trigger}");
				var tribulation = new Tribulation
				{
					PlayerOwner = player,
					QuestId = questDef.id,
					Text = tribDef.text,
					Trigger = tribDef.trigger
				};
				entityManager.AddComponent(tribEntity, tribulation);
			}
		}
	}
}

