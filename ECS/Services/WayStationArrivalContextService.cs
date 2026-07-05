using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Services
{
	public static class WayStationArrivalContextService
	{
		private const string EntityName = "WayStationArrivalContext";

		public static void Set(EntityManager entityManager, WayStationArrivalKind kind)
		{
			if (entityManager == null) return;
			var state = Ensure(entityManager);
			state.Kind = kind;
			state.HasPendingContext = true;
		}

		public static WayStationArrivalKind Consume(EntityManager entityManager)
		{
			if (entityManager == null) return WayStationArrivalKind.Initial;
			var state = entityManager.GetEntity(EntityName)?.GetComponent<WayStationArrivalContextState>();
			if (state?.HasPendingContext != true) return WayStationArrivalKind.SameVisit;
			var kind = state.Kind;
			state.Kind = WayStationArrivalKind.SameVisit;
			state.HasPendingContext = false;
			return kind;
		}

		private static WayStationArrivalContextState Ensure(EntityManager entityManager)
		{
			var entity = entityManager.GetEntity(EntityName);
			if (entity == null)
			{
				entity = entityManager.CreateEntity(EntityName);
				entityManager.AddComponent(entity, new WayStationArrivalContextState());
				entityManager.AddComponent(entity, new DontDestroyOnLoad());
			}

			var state = entity.GetComponent<WayStationArrivalContextState>();
			if (state == null)
			{
				state = new WayStationArrivalContextState();
				entityManager.AddComponent(entity, state);
			}

			return state;
		}
	}
}
