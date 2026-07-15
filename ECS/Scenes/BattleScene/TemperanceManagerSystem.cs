using System;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Manages the player's Temperance resource by listening to events and applying changes.
    /// </summary>
    public class TemperanceManagerSystem : Core.System
    {
        public TemperanceManagerSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<CardBlockedEvent>(OnCardBlocked);
            EventManager.Subscribe<ModifyTemperanceEvent>(OnModifyTemperance);
            EventManager.Subscribe<SetTemperanceEvent>(OnSetTemperanceEvent);
            EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
            LoggingService.Append("TemperanceManagerSystem.ctor", new System.Text.Json.Nodes.JsonObject { ["message"] = "subscribed to CardBlockedEvent, ModifyTemperanceEvent" });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnCardBlocked(CardBlockedEvent evt)
        {
            LoggingService.Append("TemperanceManagerSystem.OnCardBlocked", new System.Text.Json.Nodes.JsonObject
            {
                ["cardId"] = evt.Card?.Id ?? -1
            });
            if (!CardColorQualificationService.QualifiesAs(evt.Card, CardData.CardColor.White)) return;
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            if (player == null) return;
            var t = player.GetComponent<Temperance>();
            if (t == null) { t = new Temperance(); EntityManager.AddComponent(player, t); }
            EventManager.Publish(new ModifyTemperanceEvent { Delta = 1 });
        }

        private void OnModifyTemperance(ModifyTemperanceEvent evt)
        {
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            if (player == null) return;
            var t = player.GetComponent<Temperance>();
            if (t == null) { t = new Temperance(); EntityManager.AddComponent(player, t); }
            int before = t.Amount;
            t.Amount = Math.Max(0, t.Amount + evt.Delta);
            LoggingService.Append("TemperanceManagerSystem.OnModifyTemperance", new System.Text.Json.Nodes.JsonObject
            {
                ["delta"] = evt.Delta,
                ["before"] = before,
                ["after"] = t.Amount
            });
            if (evt.Delta > 0)
            {
                EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.GainTemperance, Volume = 0.5f });
            }
            TryTriggerTemperanceAbility(player, t);
        }
        private void OnSetTemperanceEvent(SetTemperanceEvent evt)
        {
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            if (player == null) return;
            var t = player.GetComponent<Temperance>();
            if (t == null) { t = new Temperance(); EntityManager.AddComponent(player, t); }
            LoggingService.Append("TemperanceManagerSystem.OnSetTemperanceEvent", new System.Text.Json.Nodes.JsonObject
            {
                ["amount"] = evt.Amount
            });
            t.Amount = Math.Max(0, evt.Amount);
        }

        private void OnLoadScene(LoadSceneEvent evt)
        {
            if (evt.Scene == SceneId.Battle) return;
            EventManager.Publish(new SetTemperanceEvent { Amount = 0 });
        }

        private void TryTriggerTemperanceAbility(Entity player, Temperance t)
        {
            var equipped = player.GetComponent<EquippedTemperanceAbility>();
            if (equipped == null || string.IsNullOrEmpty(equipped.AbilityId)) return;

            var ability = TemperanceFactory.Create(equipped.AbilityId);
            if (ability == null)
            {
                Console.WriteLine($"[TemperanceManagerSystem] No activation logic for id={equipped.AbilityId}");
                return;
            }
            if (ability.Threshold <= 0) return;
            while (t.Amount >= ability.Threshold)
            {
                // Spend threshold and activate
                t.Amount -= ability.Threshold;
                ability.Activate(EntityManager);
                EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.Temperance, Volume = 0.5f });
            }
        }
    }
}
