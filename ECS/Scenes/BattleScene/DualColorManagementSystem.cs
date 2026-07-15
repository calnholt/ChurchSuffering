using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public sealed class DualColorManagementSystem : Core.System
	{
		public DualColorManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ApplyDualColorEvent>(OnApplyDualColor);
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnApplyDualColor(ApplyDualColorEvent evt)
		{
			var card = evt?.Card;
			var data = card?.GetComponent<CardData>();
			if (card == null
				|| data?.Card == null
				|| data.Card.IsWeapon
				|| data.Card.IsToken
				|| card.HasComponent<DualColor>()
				|| !CardColorQualificationService.IsPlayableColor(data.Color)
				|| !CardColorQualificationService.IsPlayableColor(evt.SecondaryColor)
				|| data.Color == evt.SecondaryColor)
			{
				return;
			}

			EntityManager.AddComponent(card, new DualColor
			{
				Owner = card,
				SecondaryColor = evt.SecondaryColor,
			});

			var runDeckCard = card.GetComponent<RunDeckCard>();
			if (!string.IsNullOrWhiteSpace(runDeckCard?.EntryId))
			{
				SaveCache.SetRunDeckEntrySecondaryColor(
					RunDeckService.PrimaryLoadoutId,
					runDeckCard.EntryId,
					evt.SecondaryColor.ToString());
			}
		}
	}
}
