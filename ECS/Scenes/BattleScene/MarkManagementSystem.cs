using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Handles the Marksman passive for the Sniper enemy.
    /// At start of enemy turn, marks a random card in hand with a penalty.
    /// Playing the marked card triggers the penalty.
    /// Blocking with the marked card moves the mark to a different card.
    /// Pledging or holding a marked card clears the mark.
    /// </summary>
    public class MarkManagementSystem : Core.System
    {
        private static readonly Random _random = new Random();
        private static readonly MarkEffectType[] _effectPool = new[]
        {
            MarkEffectType.Lose1HP,
            MarkEffectType.Lose2HP,
            MarkEffectType.Gain2Bleed,
            MarkEffectType.Gain1Burn
        };

        public MarkManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<CardPlayedEvent>(OnCardPlayed);
            EventManager.Subscribe<CardBlockedEvent>(OnCardBlocked);
            EventManager.Subscribe<PledgeAddedEvent>(OnPledgeAdded);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private bool HasMarksmanEnemy()
        {
            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            if (enemy == null) return false;

            var ap = enemy.GetComponent<AppliedPassives>();
            return ap != null && ap.Passives.ContainsKey(AppliedPassiveType.Marksman);
        }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            LoggingService.Append("MarkManagementSystem.OnChangeBattlePhase", new System.Text.Json.Nodes.JsonObject
            {
                ["phase"] = evt.Current.ToString()
            });
            if (!HasMarksmanEnemy()) return;

            if (evt.Current == SubPhase.EnemyStart)
            {
                // Clear all existing marks
                ClearAllMarks();

                // Apply a new mark to a random hand card
                ApplyNewMark();
            }
            else if (evt.Current == SubPhase.PlayerEnd)
            {
                // If any marked cards remain in hand, clear them at end of turn.
                var markedCards = EntityManager.GetEntitiesWithComponent<Marked>().ToList();
                foreach (var card in markedCards)
                {
                    var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
                    if (deck?.Hand?.Contains(card) == true)
                    {
                        LoggingService.Append("MarkManagementSystem.OnChangeBattlePhase.PlayerEnd", new System.Text.Json.Nodes.JsonObject { ["message"] = "marked card held until PlayerEnd, clearing mark" });
                        EntityManager.RemoveComponent<Marked>(card);
                    }
                }
            }
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            LoggingService.Append("MarkManagementSystem.OnCardPlayed", new System.Text.Json.Nodes.JsonObject
            {
                ["cardId"] = evt.Card?.Id ?? -1,
                ["isMarked"] = evt.Card?.GetComponent<Marked>() != null
            });
            if (!HasMarksmanEnemy()) return;
            if (evt.Card == null) return;

            var marked = evt.Card.GetComponent<Marked>();
            if (marked == null) return;

            // Trigger the penalty effect
            ApplyPenaltyEffect(marked.EffectType);
            EntityManager.RemoveComponent<Marked>(evt.Card);
        }

        private void OnCardBlocked(CardBlockedEvent evt)
        {
            LoggingService.Append("MarkManagementSystem.OnCardBlocked", new System.Text.Json.Nodes.JsonObject
            {
                ["cardId"] = evt.Card?.Id ?? -1,
                ["isMarked"] = evt.Card?.GetComponent<Marked>() != null
            });
            if (!HasMarksmanEnemy()) return;
            if (evt.Card == null) return;

            var marked = evt.Card.GetComponent<Marked>();
            if (marked == null) return;

            // Remove mark from this card
            EntityManager.RemoveComponent<Marked>(evt.Card);

            // Move mark to a different card in hand with a new random effect
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck?.Hand == null) return;

            var eligibleCards = deck.Hand
                .Where(c => c != evt.Card)
                .Where(c => c.GetComponent<Marked>() == null)
                .Where(c => c.GetComponent<Pledge>() == null)
                .ToList();

            if (eligibleCards.Count > 0)
            {
                var newTarget = eligibleCards[_random.Next(eligibleCards.Count)];
                var newEffect = _effectPool[_random.Next(_effectPool.Length)];
                EntityManager.AddComponent(newTarget, new Marked { EffectType = newEffect });

                var cardData = newTarget.GetComponent<CardData>();
                LoggingService.Append("MarkManagementSystem.OnCardBlocked", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardData?.Card?.CardId ?? "unknown", ["newEffect"] = newEffect.ToString() });
            }
            else
            {
                LoggingService.Append("MarkManagementSystem.OnCardBlocked", new System.Text.Json.Nodes.JsonObject { ["message"] = "no eligible card to move mark to, mark disappears" });
            }
        }

        private void OnPledgeAdded(PledgeAddedEvent evt)
        {
            LoggingService.Append("MarkManagementSystem.OnPledgeAdded", new System.Text.Json.Nodes.JsonObject
            {
                ["cardId"] = evt.Card?.Id ?? -1,
                ["isMarked"] = evt.Card?.GetComponent<Marked>() != null
            });
            if (!HasMarksmanEnemy()) return;
            if (evt.Card == null) return;

            var marked = evt.Card.GetComponent<Marked>();
            if (marked == null) return;

            LoggingService.Append("MarkManagementSystem.OnPledgeAdded", new System.Text.Json.Nodes.JsonObject { ["message"] = "marked card pledged, clearing mark" });
            EntityManager.RemoveComponent<Marked>(evt.Card);
        }

        private void ClearAllMarks()
        {
            var markedCards = EntityManager.GetEntitiesWithComponent<Marked>().ToList();
            foreach (var card in markedCards)
            {
                EntityManager.RemoveComponent<Marked>(card);
            }
        }

        private void ApplyNewMark()
        {
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck?.Hand == null || deck.Hand.Count == 0)
            {
                LoggingService.Append("MarkManagementSystem.ApplyNewMark", new System.Text.Json.Nodes.JsonObject { ["message"] = "no cards in hand to mark" });
                return;
            }

            // Exclude pledged cards from being marked
            var eligibleCards = deck.Hand
                .Where(c => c.GetComponent<Pledge>() == null)
                .Where(c => c.GetComponent<Marked>() == null)
                .ToList();

            if (eligibleCards.Count == 0)
            {
                LoggingService.Append("MarkManagementSystem.ApplyNewMark", new System.Text.Json.Nodes.JsonObject { ["message"] = "no eligible cards to mark" });
                return;
            }

            var cardToMark = eligibleCards[_random.Next(eligibleCards.Count)];
            var effect = _effectPool[_random.Next(_effectPool.Length)];

            EntityManager.AddComponent(cardToMark, new Marked { EffectType = effect });

            var cardData = cardToMark.GetComponent<CardData>();
            LoggingService.Append("MarkManagementSystem.ApplyNewMark", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardData?.Card?.CardId ?? "unknown", ["effect"] = effect.ToString() });
        }

        private void ApplyPenaltyEffect(MarkEffectType effectType)
        {
            var player = EntityManager.GetEntity("Player");

            switch (effectType)
            {
                case MarkEffectType.Lose1HP:
                    LoggingService.Append("MarkManagementSystem.ApplyMarkEffect", new System.Text.Json.Nodes.JsonObject { ["effect"] = "Lose1HP" });
                    EventManager.Publish(new ModifyHpRequestEvent
                    {
                        Source = EntityManager.GetEntity("Enemy"),
                        Target = player,
                        Delta = -1,
                        DamageType = ModifyTypeEnum.Effect
                    });
                    break;

                case MarkEffectType.Lose2HP:
                    LoggingService.Append("MarkManagementSystem.ApplyMarkEffect", new System.Text.Json.Nodes.JsonObject { ["effect"] = "Lose2HP" });
                    EventManager.Publish(new ModifyHpRequestEvent
                    {
                        Source = EntityManager.GetEntity("Enemy"),
                        Target = player,
                        Delta = -2,
                        DamageType = ModifyTypeEnum.Effect
                    });
                    break;

                case MarkEffectType.Gain2Bleed:
                    LoggingService.Append("MarkManagementSystem.ApplyMarkEffect", new System.Text.Json.Nodes.JsonObject { ["effect"] = "Gain2Bleed" });
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Bleed,
                        Delta = 2
                    });
                    break;

                case MarkEffectType.Gain1Burn:
                    LoggingService.Append("MarkManagementSystem.ApplyMarkEffect", new System.Text.Json.Nodes.JsonObject { ["effect"] = "Gain1Burn" });
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Burn,
                        Delta = 1
                    });
                    break;
            }
        }

        public static string GetEffectDescription(MarkEffectType effectType)
        {
            return effectType switch
            {
                MarkEffectType.Lose1HP => "Lose 1 HP",
                MarkEffectType.Lose2HP => "Lose 2 HP",
                MarkEffectType.Gain2Bleed => "+2 Bleed",
                MarkEffectType.Gain1Burn => "+1 Burn",
                _ => "Unknown"
            };
        }
    }
}
