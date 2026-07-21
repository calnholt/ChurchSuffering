using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Locations;
using ChurchSuffering.ECS.Events;
using System;
using System.Linq;

namespace ChurchSuffering.ECS.Services
{
	/// <summary>
	/// Service that applies tribulation effects to the player based on quest ID.
	/// Effects are keyed entirely by quest ID - no string parsing needed.
	/// </summary>
	public static class ApplyTribulationService
	{
		/// <summary>
		/// Applies tribulation effects for the given quest ID.
		/// Looks up quest data and applies effects based on hardcoded quest ID matching.
		/// </summary>
		public static void ApplyTribulationEffect(Entity player, string questId)
		{
			if (player == null || string.IsNullOrEmpty(questId)) return;

			// Apply effects based on quest ID
			// Example: desert_3 applies burn 1 at start of battle
			switch (questId)
			{
				default:
					break;
			}
		}
	}
}

