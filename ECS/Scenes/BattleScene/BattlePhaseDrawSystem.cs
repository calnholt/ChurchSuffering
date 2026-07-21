using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Draws cards based on phase transitions:
	/// - On StartOfBattle entry: draw up to Intellect (respect MaxHandSize)
	/// - On Block entry from phases other than StartOfBattle and ProcessEnemyAttack: draw up to Intellect
	/// </summary>
	public class DrawHandSystem : Core.System
	{

		public DrawHandSystem(EntityManager entityManager) : base(entityManager)
		{
			var s = entityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
			EventManager.Subscribe<ChangeBattlePhaseEvent>(_ =>
			{
				LoggingService.Append("DrawHandSystem.OnChangeBattlePhase", new System.Text.Json.Nodes.JsonObject
				{
					["current"] = _.Current.ToString(),
					["previous"] = _.Previous.ToString()
				});

				// TODO: mindfog system? - kinda shoehorned in here
				if (_.Current == SubPhase.PlayerEnd)
				{
					var passives = GetComponentHelper.GetAppliedPassives(EntityManager, "Player");
					if (passives != null)
					{
						if (passives.Passives.TryGetValue(AppliedPassiveType.MindFog, out int mindFogAmount) && mindFogAmount > 0)
						{
							EventManager.Publish(new DiscardAllCardsEvent());
						}
					}
				}
				if (_.Current == SubPhase.EnemyStart)
				{
					DrawUpToIntellect(_.Current);
				}
			});
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return System.Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}

		private void DrawUpToIntellect(SubPhase phase)
		{
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player == null) return;
			int intellect = player.GetComponent<Intellect>()?.Value ?? 0;
			if (intellect <= 0) return;
			int maxHandSize = player.GetComponent<MaxHandSize>()?.Value ?? 0;
			var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
			var deck = deckEntity?.GetComponent<Deck>();
			if (deck == null) return;

			HandStateLoggingService.AppendHandSnapshot("DrawHandSystem.DrawUpToIntellect.handSnapshot", deck, "beforeDrawCalculation", phase);
			LoggingService.Append("DrawHandSystem.DrawUpToIntellect", new System.Text.Json.Nodes.JsonObject { ["deckHandCount"] = deck.Hand.Count, ["intellect"] = intellect, ["maxHandSize"] = maxHandSize });
			int effectiveHandCount = GetEffectiveHandCountForDraw(deck.Hand, true);
			int spaceLeft = System.Math.Max(0, maxHandSize - effectiveHandCount);
			int toDraw = System.Math.Min(spaceLeft, intellect);
			LoggingService.Append("DrawHandSystem.DrawUpToIntellect.result", new System.Text.Json.Nodes.JsonObject { ["effectiveHandCount"] = effectiveHandCount, ["spaceLeft"] = spaceLeft, ["toDraw"] = toDraw });
			for (int i = 0; i < toDraw; i++)
			{
				EventQueueBridge.EnqueueTriggerAction("DrawHandSystem.DrawCard", () => EventManager.Publish(new RequestDrawCardsEvent { Count = 1 }), 0.12f);
			}
			EventQueueBridge.EnqueueTriggerAction("DrawHandSystem.StartOfTurnDrawResolved", () => EventManager.Publish(new StartOfTurnDrawResolvedEvent
			{
				Player = player,
				Deck = deckEntity,
				Phase = phase,
				RequestedDrawCount = toDraw
			}), 0f);
		}

		public static int CalculateCardsToDraw(int intellect, int maxHandSize, System.Collections.Generic.IEnumerable<Entity> hand)
		{
			return CalculateCardsToDraw(intellect, maxHandSize, GetEffectiveHandCountForDraw(hand));
		}

		public static int GetEffectiveHandCountForDraw(System.Collections.Generic.IEnumerable<Entity> hand, bool emitLogs = false)
		{
			int effectiveHandCount = 0;
			foreach (var e in hand)
			{
				string debugId = e.GetComponent<CardData>()?.Card?.CardId ?? $"entity#{e.Id}";
				bool isActive = e.IsActive;
				string reason = HandStateLoggingService.GetDrawCountReason(e);
				if (HandStateLoggingService.CountsForDraw(e))
				{
					effectiveHandCount++;
					LogDrawCount(emitLogs, debugId, reason, isActive);
				}
				else
				{
					LogDrawSkip(emitLogs, debugId, reason, isActive);
				}
			}

			return effectiveHandCount;
		}

		private static int CalculateCardsToDraw(int intellect, int maxHandSize, int effectiveHandCount)
		{
			int spaceLeft = System.Math.Max(0, maxHandSize - effectiveHandCount);
			return System.Math.Min(spaceLeft, intellect);
		}

		private static void LogDrawSkip(bool emitLogs, string cardId, string reason, bool isActive)
		{
			if (!emitLogs) return;
			LoggingService.Append("DrawHandSystem.DrawUpToIntellect.skip", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardId, ["reason"] = reason, ["isActive"] = isActive });
		}

		private static void LogDrawSkip(bool emitLogs, int entityId, string reason, bool isActive)
		{
			if (!emitLogs) return;
			LoggingService.Append("DrawHandSystem.DrawUpToIntellect.skip", new System.Text.Json.Nodes.JsonObject { ["entityId"] = entityId, ["reason"] = reason, ["isActive"] = isActive });
		}

		private static void LogDrawCount(bool emitLogs, string cardId, string reason, bool isActive)
		{
			if (!emitLogs) return;
			LoggingService.Append("DrawHandSystem.DrawUpToIntellect.count", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardId, ["reason"] = reason, ["isActive"] = isActive });
		}
	}
}
