using System;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Handles the block-driven removal of persistent seal stacks.
	/// </summary>
	public class SealManagementSystem : Core.System
	{
		public SealManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<CardMoved>(OnCardMoved);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		/// <summary>
		/// When a sealed card is used to block (moves from AssignedBlock to DiscardPile), it loses 1 seal.
		/// </summary>
		private void OnCardMoved(CardMoved evt)
		{
			LoggingService.Append("SealManagementSystem.OnCardMoved", new System.Text.Json.Nodes.JsonObject
			{
				["cardId"] = evt.Card?.Id ?? -1,
				["from"] = evt.From.ToString(),
				["to"] = evt.To.ToString()
			});
			if (evt.From == CardZoneType.AssignedBlock && evt.To == CardZoneType.DiscardPile)
			{
				var sealedComp = evt.Card.GetComponent<Sealed>();
				if (sealedComp != null)
				{
					RemoveSeals(evt.Card, 1, "used to block");
				}
			}
		}

		private void RemoveSeals(Entity card, int amount, string reason)
		{
			var sealedComp = card.GetComponent<Sealed>();
			if (sealedComp == null) return;

			sealedComp.Seals -= amount;
			RunScopedStateService.SyncCardRestrictionsFromComponents(card);
			var cardData = card.GetComponent<CardData>();
			LoggingService.Append("SealManagementSystem.RemoveSeals", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardData?.Card.CardId ?? "unknown", ["amountRemoved"] = amount, ["reason"] = reason, ["sealCount"] = sealedComp.Seals });

			if (sealedComp.Seals <= 0)
			{
				FreeCard(card);
			}
		}

		private void FreeCard(Entity card)
		{
			EntityManager.RemoveComponent<Sealed>(card);
			RunScopedStateService.SyncCardRestrictionsFromComponents(card);
			var cardData = card.GetComponent<CardData>();
			LoggingService.Append("SealManagementSystem.FreeCard", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardData?.Card.CardId ?? "unknown" });
		}
	}
}
