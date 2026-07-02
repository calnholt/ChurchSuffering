using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Handles shackle effects on cards.
	/// Shackled cards are linked: assigning one as block assigns all shackled cards in hand.
	/// Unassigning one unassigns all shackled cards currently assigned.
	/// All shackle components are removed at the end of the enemy turn.
	/// </summary>
	public class ShackleManagementSystem : Core.System
	{
		private bool _isProcessing = false;
		private bool _blockedWithShackledCard = false;
		private bool _shacklesAppliedThisEnemyTurn = false;
		public ShackleManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<BlockAssignmentAdded>(OnBlockAssignmentAdded);
			EventManager.Subscribe<UnassignCardAsBlockRequested>(OnUnassignCardAsBlockRequested);
			EventManager.Subscribe<ChangeBattlePhaseEvent>(OnPhaseChanged);
			EventManager.Subscribe<CardMoved>(OnCardMoved);
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
			EventManager.Subscribe<BeginDefeatPresentationEvent>(OnBeginDefeatPresentation);
			EventManager.Subscribe<EnemyPhaseResetEvent>(_ => ClearEnemyTurnShackles());
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void ApplyShackleEffect()
		{
			var passives = GetComponentHelper.GetAppliedPassives(EntityManager, "Player");
			if (passives == null) return;
			if (!passives.Passives.TryGetValue(AppliedPassiveType.Shackled, out int shackledAmount) || shackledAmount <= 0)
			{
				return;
			}
			var handCards = GetComponentHelper.GetHandOfCards(EntityManager);
			var availableCards = handCards
				.Where(c => c.GetComponent<Intimidated>() == null
					&& c.GetComponent<Shackle>() == null)
				.ToList();

			// Need at least 2 non-intimidated cards to apply shackle
			if (availableCards.Count <= 1) return;

			var random = new Random();
			var cardsToShackle = availableCards
				.OrderBy(x => random.Next())
				.Take(2)
				.ToList();

			foreach (var card in cardsToShackle)
			{
				EntityManager.AddComponent(card, new Shackle { Owner = card });
				LoggingService.Append("ShackleManagementSystem.ApplyShackleEffect", new System.Text.Json.Nodes.JsonObject { ["action"] = "Card shackled", ["cardId"] = card.Id });
			}
		}

		private void OnBlockAssignmentAdded(BlockAssignmentAdded evt)
		{
			if (_isProcessing) return;
			if (evt.Card == null || evt.Card.GetComponent<Shackle>() == null) return;

			var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
			if (deckEntity == null) return;
			var deck = deckEntity.GetComponent<Deck>();

			// Find other shackled cards in hand
			var otherShackledInHand = deck.Hand
				.Where(c => c != evt.Card && c.GetComponent<Shackle>() != null)
				.ToList();

			if (otherShackledInHand.Count == 0) return;

			_isProcessing = true;
			try
			{
				foreach (var card in otherShackledInHand)
				{
					// Skip if already assigned (though they should be in hand)
					if (card.GetComponent<AssignedBlockCard>() != null) continue;

					int blockVal = BlockValueService.GetTotalBlockValue(card);
					string color = CardColorQualificationService.GetQualifiedColor(card)?.ToString();

					var t = card.GetComponent<Transform>();
					if (t != null)
					{
						var startPos = t.Position;
						EventManager.Publish(new CardMoveRequested
						{
							Card = card,
							Deck = deckEntity,
							Destination = CardZoneType.AssignedBlock,
							Reason = "ShackleAssignBlock"
						});

						var abc = card.GetComponent<AssignedBlockCard>();
						if (abc != null)
						{
							abc.ReturnTargetPos = startPos;
						}
					}
					EventManager.Publish(new BlockAssignmentAdded 
					{ 
						Card = card, 
						Color = color, 
						DeltaBlock = blockVal 
					});
				}
			}
			finally
			{
				_isProcessing = false;
			}
		}

		private void OnUnassignCardAsBlockRequested(UnassignCardAsBlockRequested evt)
		{
			if (_isProcessing) return;
			if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
			if (evt.CardEntity == null || evt.CardEntity.GetComponent<Shackle>() == null) return;

			// Find other shackled cards that are currently assigned
			var otherShackledAssigned = EntityManager.GetEntitiesWithComponent<AssignedBlockCard>()
				.Where(e => e != evt.CardEntity && e.GetComponent<Shackle>() != null)
				.ToList();

			if (otherShackledAssigned.Count == 0) return;

			_isProcessing = true;
			try
			{
				foreach (var card in otherShackledAssigned)
				{
					EventManager.Publish(new UnassignCardAsBlockRequested { CardEntity = card });
				}
			}
			finally
			{
				_isProcessing = false;
			}
		}

		private void OnPhaseChanged(ChangeBattlePhaseEvent evt)
		{
			if (evt.Current == SubPhase.EnemyStart)
			{
				_shacklesAppliedThisEnemyTurn = false;
			}
			else if (evt.Current == SubPhase.PreBlock)
			{
				if (!_shacklesAppliedThisEnemyTurn)
				{
					ApplyShackleEffect();
					_shacklesAppliedThisEnemyTurn = true;
				}
			}
			else if (evt.Current == SubPhase.EnemyEnd)
			{
				ClearEnemyTurnShackles();
			}
		}

		private void OnBeginDefeatPresentation(BeginDefeatPresentationEvent evt)
		{
			if (evt?.IsPreview == true) return;
			ClearEnemyTurnShackles();
		}

		private void ClearEnemyTurnShackles()
		{
			RemoveAllShackles();
			_blockedWithShackledCard = false;
			_shacklesAppliedThisEnemyTurn = false;
		}

		private void OnCardMoved(CardMoved evt)
		{
			if ((evt.To == CardZoneType.DiscardPile || evt.To == CardZoneType.ExhaustPile) && evt.From == CardZoneType.AssignedBlock)
			{
				if (evt.Card.GetComponent<Shackle>() != null && !_blockedWithShackledCard)
				{
					EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Shackled, Delta = -1 });
					_blockedWithShackledCard = true;
				}
			}
		}

		private void OnDeleteCaches(DeleteCachesEvent evt)
		{
			_blockedWithShackledCard = false;
			_isProcessing = false;
			_shacklesAppliedThisEnemyTurn = false;
		}
		private void RemoveAllShackles()
		{
			var shackledEntities = EntityManager.GetEntitiesWithComponent<Shackle>().ToList();
			foreach (var entity in shackledEntities)
			{
				EntityManager.RemoveComponent<Shackle>(entity);
				LoggingService.Append("ShackleManagementSystem.RemoveAllShackles", new System.Text.Json.Nodes.JsonObject { ["action"] = "Removed shackle", ["entityId"] = entity.Id });
			}
		}
	}
}
