using System.Collections.Generic;
using ChurchSuffering.ECS.Objects.Events;

namespace ChurchSuffering.ECS.Factories
{
	public static class EventFactory
	{
		public static EventBase Create(string eventId)
		{
			return eventId switch
			{
				"icebound_tithe" => new IceboundTithe(),
				"pruned_vocation" => new PrunedVocation(),
				_ => null
			};
		}

		public static Dictionary<string, EventBase> GetAllEvents()
		{
			return new Dictionary<string, EventBase>
			{
				{ "icebound_tithe", new IceboundTithe() },
				{ "pruned_vocation", new PrunedVocation() },
			};
		}
	}
}
