using System;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Handles intimidate effects from enemy attacks.
	/// During the block phase, if an attack has effectsOnAttack with type=Intimidate,
	/// randomly picks cards from the player's hand (block value greater than 0 only)
	/// and adds the Intimidated component.
	/// At the end of the enemy turn, removes all Intimidate components.
	/// </summary>
	public class IntimidateManagementSystem : Core.System
	{
		public IntimidateManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ChangeBattlePhaseEvent>(OnPhaseChanged);
			EventManager.Subscribe<IntimidateEvent>(OnIntimidate);
			EventManager.Subscribe<BeginDefeatPresentationEvent>(OnBeginDefeatPresentation);
			EventManager.Subscribe<EnemyPhaseResetEvent>(_ => ClearEnemyTurnIntimidation());
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnPhaseChanged(ChangeBattlePhaseEvent evt)
		{
			// When entering the Block phase, apply intimidate effects
			if (evt.Current == SubPhase.EnemyEnd)
			{
				ClearEnemyTurnIntimidation();
			}
			if (evt.Current == SubPhase.PreBlock)
			{
				var passives = GetComponentHelper.GetAppliedPassives(EntityManager, "Player");
				if (passives == null) return;
				if (passives.Passives.TryGetValue(AppliedPassiveType.Intimidated, out int intimidationAmount) && intimidationAmount > 0)
				{
					ApplyIntimidateEffects(intimidationAmount);
				}
			}
		}

		private void OnIntimidate(IntimidateEvent evt)
		{
			ApplyIntimidateEffects(evt.Amount);
		}

		private void ApplyIntimidateEffects(int amount)
		{
			// Get player's hand
			var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
			if (deckEntity == null) return;

			var deck = deckEntity.GetComponent<Deck>();
			if (deck == null || deck.Hand == null || deck.Hand.Count == 0) return;

			// For each intimidate effect, randomly pick cards
				
				// Get cards that are not already intimidated (exclude pledged cards and zero-block cards)
				var availableCards = deck.Hand
					.Where(c => c.GetComponent<Intimidated>() == null
						&& c.GetComponent<Pledge>() == null
						&& c.GetComponent<Shackle>() == null
						&& BlockValueService.GetTotalBlockValue(c) > 0)
					.ToList();

				if (availableCards.Count == 0) return;

				// Randomly select cards to intimidate
				var random = new Random();
				var cardsToIntimidate = availableCards
					.OrderBy(x => random.Next())
					.Take(amount)
					.ToList();

				// Add Intimidated component to selected cards
				foreach (var card in cardsToIntimidate)
				{
					var cardData = card.GetComponent<CardData>();
					EntityManager.AddComponent(card, new Intimidated { Owner = card });
					LoggingService.Append("IntimidateManagementSystem.ApplyIntimidateEffects", new System.Text.Json.Nodes.JsonObject { ["action"] = "Card intimidated", ["cardId"] = cardData?.Card.CardId ?? "unknown" });
				}
		}

		private void OnBeginDefeatPresentation(BeginDefeatPresentationEvent evt)
		{
			if (evt?.IsPreview == true) return;
			ClearEnemyTurnIntimidation();
		}

		private void ClearEnemyTurnIntimidation()
		{
			RemoveAllIntimidateEffects();
		}

		private void RemoveAllIntimidateEffects()
		{
			// Get all cards with Intimidated component
			var intimidatedCards = EntityManager.GetEntitiesWithComponent<Intimidated>().ToList();

			foreach (var card in intimidatedCards)
			{
				var cardData = card.GetComponent<CardData>();
				EntityManager.RemoveComponent<Intimidated>(card);
				LoggingService.Append("IntimidateManagementSystem.RemoveAllIntimidateEffects", new System.Text.Json.Nodes.JsonObject { ["action"] = "Removed intimidation", ["cardId"] = cardData?.Card.CardId ?? "unknown" });
			}
		}
	}
}

