using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class DeckEmptyDeathCheckSystem : Core.System
	{
		private bool _playerDeathPublished;

		public DeckEmptyDeathCheckSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<StartOfTurnDrawResolvedEvent>(OnStartOfTurnDrawResolved);
			EventManager.Subscribe<PlayerDied>(_ => _playerDeathPublished = true);
			EventManager.Subscribe<StartBattleRequested>(_ => _playerDeathPublished = false);
			EventManager.Subscribe<DeleteCachesEvent>(_ => _playerDeathPublished = false);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Enumerable.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}

		private void OnStartOfTurnDrawResolved(StartOfTurnDrawResolvedEvent evt)
		{
			if (_playerDeathPublished || evt == null || evt.RequestedDrawCount <= 0) return;

			var deckEntity = evt.Deck ?? EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
			var deck = deckEntity?.GetComponent<Deck>();
			if (deck == null) return;

			int visibleHandCount = HandStateLoggingService.CountVisibleHand(deck.Hand);
			LoggingService.Append("DeckEmptyDeathCheckSystem.OnStartOfTurnDrawResolved", new JsonObject
			{
				["phase"] = evt.Phase.ToString(),
				["requestedDrawCount"] = evt.RequestedDrawCount,
				["visibleHandCount"] = visibleHandCount,
				["deckHandCount"] = deck.Hand.Count,
				["drawPileCount"] = deck.DrawPile.Count,
				["discardPileCount"] = deck.DiscardPile.Count
			});
			if (visibleHandCount > 0) return;

			var player = evt.Player ?? EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player == null) return;

			_playerDeathPublished = true;
			EventManager.Publish(new PlayerDied { Player = player });
		}
	}
}
