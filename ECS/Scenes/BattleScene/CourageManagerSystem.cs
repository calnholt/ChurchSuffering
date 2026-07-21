using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Manages the player's Courage resource by listening to events and applying changes.
	/// </summary>
	public class CourageManagerSystem : Core.System
	{
		public CourageManagerSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ModifyCourageRequestEvent>(OnModifyCourage);
			EventManager.Subscribe<SetCourageEvent>(OnSetCourageEvent);
			EventManager.Subscribe<ApplyEffect>(OnApplyEffect);
			EventManager.Subscribe<CardBlockedEvent>(OnCardBlocked);
			LoggingService.Append("CourageManagerSystem.ctor", new System.Text.Json.Nodes.JsonObject { ["message"] = "subscribed to ModifyCourageEvent, CardBlockedEvent" });
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			// Not entity-driven; listens to events
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnModifyCourage(ModifyCourageRequestEvent evt)
		{
			LoggingService.Append("CourageManagerSystem.OnModifyCourage", new System.Text.Json.Nodes.JsonObject
			{
				["delta"] = evt.Delta,
				["reason"] = evt.Reason
			});
			var player = EntityManager.GetEntitiesWithComponent<Player>()
				.FirstOrDefault(e => e.HasComponent<Courage>());
			if (player == null) return;
			var courage = player.GetComponent<Courage>();
			if (courage == null) return;
			int old = courage.Amount;
			int amount = Math.Max(0, old + evt.Delta);
			int newAmount = amount;
			courage.Amount = newAmount;
			if (evt.Delta > 0)
			{
				EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.GainCourage, Volume = 0.5f });
			}
			EventManager.Publish(new ModifyCourageEvent { Delta = newAmount - old, Reason = evt.Reason });
		}

		private void OnSetCourageEvent(SetCourageEvent evt)
		{
			LoggingService.Append("CourageManagerSystem.OnSetCourageEvent", new System.Text.Json.Nodes.JsonObject
			{
				["amount"] = evt.Amount
			});
			var player = EntityManager.GetEntitiesWithComponent<Player>()
				.FirstOrDefault(e => e.HasComponent<Courage>());
			if (player == null) return;
			var courage = player.GetComponent<Courage>();
			if (courage == null) return;
			courage.Amount = evt.Amount;
		}

		private void OnApplyEffect(ApplyEffect evt)
		{
			// Translate data-driven effects to resource changes
			string type = evt.EffectType ?? string.Empty;
			if (type != "LoseCourage") return;
			LoggingService.Append("CourageManagerSystem.OnApplyEffect", new System.Text.Json.Nodes.JsonObject
			{
				["effectType"] = evt.EffectType,
				["amount"] = evt.Amount
			});
			// Only apply if the target is the player
			if (evt.Target == null || !evt.Target.HasComponent<Player>()) return;
			int amt = Math.Max(0, evt.Amount);
			if (amt <= 0) amt = 1; // default to 1 if unspecified or invalid
			EventManager.Publish(new ModifyCourageRequestEvent { Delta = -amt });
		}

		private void OnCardBlocked(CardBlockedEvent evt)
		{
			LoggingService.Append("CourageManagerSystem.OnCardBlocked", new System.Text.Json.Nodes.JsonObject
			{
				["cardId"] = evt.Card?.Id ?? -1
			});
			if (!CardColorQualificationService.QualifiesAs(evt.Card, CardData.CardColor.Red)) return;
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player == null) return;
			var c = player.GetComponent<Courage>();
			if (c == null) { c = new Courage(); EntityManager.AddComponent(player, c); }
			EventManager.Publish(new ModifyCourageRequestEvent { Delta = 1 });
		}
	}
}

