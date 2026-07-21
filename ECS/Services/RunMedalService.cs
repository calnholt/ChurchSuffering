using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Medals;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Services
{
	public static class RunMedalService
	{
		public static bool IsEquippedOnPlayer(EntityManager entityManager, string medalId)
		{
			if (entityManager == null || string.IsNullOrWhiteSpace(medalId)) return false;

			return entityManager.GetEntitiesWithComponent<EquippedMedal>()
				.Any(entity => string.Equals(
					entity.GetComponent<EquippedMedal>()?.Medal?.Id,
					medalId,
					StringComparison.OrdinalIgnoreCase));
		}

		public static Entity AcquireAndEquipPersisted(EntityManager entityManager, string medalId)
		{
			if (entityManager == null || string.IsNullOrWhiteSpace(medalId)) return null;
			if (MedalFactory.Create(medalId) == null) return null;

			var loadout = SaveCache.GetLoadout("loadout_1") ?? new LoadoutDefinition { id = "loadout_1" };
			loadout.medalIds ??= new System.Collections.Generic.List<string>();
			if (!loadout.medalIds.Any(id => string.Equals(id, medalId, StringComparison.OrdinalIgnoreCase)))
			{
				loadout.medalIds.Add(medalId);
				SaveCache.SaveLoadout(loadout);
			}

			if (IsEquippedOnPlayer(entityManager, medalId)) return null;
			return AcquireAndEquip(entityManager, medalId);
		}

		public static Entity AcquireAndEquip(EntityManager entityManager, string medalId)
		{
			if (entityManager == null || string.IsNullOrWhiteSpace(medalId)) return null;

			var player = entityManager.GetEntity("Player");
			if (player == null || !player.IsActive) return null;

			var medal = MedalFactory.Create(medalId);
			if (medal == null) return null;

			var medalEntity = entityManager.CreateEntity($"Medal_{medalId}_{Guid.NewGuid():N}");
			medal.Initialize(entityManager, medalEntity);
			entityManager.AddComponent(medalEntity, new EquippedMedal { EquippedOwner = player, Medal = medal });
			entityManager.AddComponent(medalEntity, ParallaxLayer.GetUIParallaxLayer());
			entityManager.AddComponent(medalEntity, new UIElement { IsInteractable = false });
			entityManager.AddComponent(medalEntity, new Transform { Position = Vector2.Zero, ZOrder = 10001 });
			if (!medalEntity.HasComponent<DontDestroyOnLoad>())
			{
				entityManager.AddComponent(medalEntity, new DontDestroyOnLoad());
			}

			medal.OnAcquire();
			return medalEntity;
		}
	}
}
