using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Keeps run deck ECS entities in sync with save loadout and run lifecycle events.
	/// </summary>
	public class RunDeckLifecycleSystem : Core.System
	{
		public RunDeckLifecycleSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<QuestSelected>(_ => RunDeckService.EnsureRunDeck(EntityManager));
			EventManager.Subscribe<LoadoutCardAdded>(OnLoadoutCardAdded);
			EventManager.Subscribe<LoadoutCardRemoved>(OnLoadoutCardRemoved);
		}

		private void OnLoadoutCardAdded(LoadoutCardAdded evt)
		{
			if (evt == null || string.IsNullOrWhiteSpace(evt.EntryId)) return;
			if (!string.Equals(evt.LoadoutId, RunDeckService.PrimaryLoadoutId, System.StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			RunDeckService.AddCardFromEntry(EntityManager, evt.EntryId);
		}

		private void OnLoadoutCardRemoved(LoadoutCardRemoved evt)
		{
			if (evt == null || string.IsNullOrWhiteSpace(evt.EntryId)) return;
			if (!string.Equals(evt.LoadoutId, RunDeckService.PrimaryLoadoutId, System.StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			RunDeckService.RemoveCardByEntryId(EntityManager, evt.EntryId);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}
	}
}
