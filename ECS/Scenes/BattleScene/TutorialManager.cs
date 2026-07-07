using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Factories;
using Microsoft.Xna.Framework;
using Crusaders30XX.ECS.Data.Locations;

namespace Crusaders30XX.ECS.Systems
{
    public class TutorialManager : Core.System
    {
        private readonly Queue<TutorialDefinition> _tutorialQueue = new Queue<TutorialDefinition>();
        private TutorialDefinition _activeTutorial = null;
        private bool _tutorialActive = false;
        private int _lastProcessedTurn = -1;
        private SubPhase _lastProcessedPhase = SubPhase.StartBattle;

        public TutorialDefinition ActiveTutorial => _activeTutorial;
        public bool IsTutorialActive => _tutorialActive;
        public int QueueCount => _tutorialQueue.Count;

        public TutorialManager(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<AdvanceTutorialEvent>(OnAdvanceTutorial);
            EventManager.Subscribe<LoadSceneEvent>(_ => Reset());
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            base.Update(gameTime);

            // If we have queued tutorials but none active, start the next one
            if (!_tutorialActive && _tutorialQueue.Count > 0)
            {
                StartNextTutorial();
            }
        }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            var phaseState = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
            if (phaseState == null) return;

            int turnNumber = phaseState.TurnNumber;
            SubPhase currentPhase = evt.Current;
            var guided = GuidedTutorialService.GetState(EntityManager);

            // Avoid reprocessing the same phase/turn combination
            if (turnNumber == _lastProcessedTurn && currentPhase == _lastProcessedPhase)
                return;

            _lastProcessedTurn = turnNumber;
            _lastProcessedPhase = currentPhase;

            LoggingService.Append("TutorialManager.OnChangeBattlePhase", new System.Text.Json.Nodes.JsonObject { ["phase"] = currentPhase.ToString(), ["turnNumber"] = turnNumber });

            if (guided != null)
            {
                QueueGuidedTutorials(guided, currentPhase);
                return;
            }

            // Queue tutorials based on phase and turn number
            if (currentPhase == SubPhase.Block && turnNumber == 1)
            {
                QueueTutorialsForFirstBlockPhase();
            }
            else if (currentPhase == SubPhase.Action && turnNumber == 1)
            {
                QueueTutorialsForFirstActionPhase();
            }
            else if (currentPhase == SubPhase.Block && turnNumber == 2)
            {
                QueueTutorialsForSecondBlockPhase();
            }

            if (currentPhase == SubPhase.Action)
            {
                TryQueueTutorial("cost");
                TryQueueTutorial("threat");
                TryQueueTutorial("pledge");
            }
            if (currentPhase == SubPhase.Block)
            {
                TryQueueTutorial("equipment");
            }
        }

        private void QueueGuidedTutorials(GuidedTutorial guided, SubPhase phase)
        {
            foreach (string key in GuidedTutorialDefinitions.GetMessageKeys(
                guided.Section,
                guided.TurnWithinSection,
                phase,
                guided.ConfirmedAttackCountThisTurn))
            {
                TryQueueTutorial(key);
            }
        }

        private void QueueTutorialRange(params string[] keys)
        {
            foreach (string key in keys)
                TryQueueTutorial(key);
        }

        private void QueueTutorialsForFirstBlockPhase()
        {
            TryQueueTutorial("how_to_win");
            TryQueueTutorial("block_phase_overview");
            TryQueueTutorial("card_block_value");
        }

        private void QueueTutorialsForFirstActionPhase()
        {
            TryQueueTutorial("action_phase_overview");
            TryQueueTutorial("action_points");
            TryQueueTutorial("card_damage");
            TryQueueTutorial("weapon");
        }

        private void QueueTutorialsForSecondBlockPhase()
        {
            TryQueueTutorial("card_colors");
            TryQueueTutorial("courage");
            TryQueueTutorial("temperance");
        }

        private void TryQueueTutorial(string key)
        {
            if (SaveCache.HasSeenTutorial(key))
                return;

            var guided = GuidedTutorialService.GetState(EntityManager);
            if (key.StartsWith("teach_", StringComparison.Ordinal) && guided?.SessionSeenTeaches != null && guided.SessionSeenTeaches.Contains(key))
                return;

            if (!TutorialDefinitionCache.TryGet(key, out var tutorial) || tutorial == null)
            {
                LoggingService.Append("TutorialManager.QueueTutorial.notFound", new System.Text.Json.Nodes.JsonObject { ["key"] = key });
                return;
            }

            if (!CheckCondition(tutorial.condition))
                return;

            _tutorialQueue.Enqueue(tutorial);
            LoggingService.Append("TutorialManager.QueueTutorial.queued", new System.Text.Json.Nodes.JsonObject { ["key"] = key });
        }

        private bool CheckCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return true;

            switch (condition)
            {
                case "has_cost_card":
                    return HasCostCardInHand();
                case "has_non_free_card":
                    return HasNonFreeCardInHand();
                case "has_reckoning_in_hand":
                    return HasCardInHand("reckoning");
                case "has_equipment":
                    return HasEquipment();
                case "has_medal":
                    return HasMedal();
                case "has_tribulation":
                    return HasTribulation();
                case "can_pledge":
                    return CanPledge();
                case "threat_enabled":
                    return IsThreatEnabled();
                default:
                    return true;
            }
        }

        private bool IsThreatEnabled()
        {
            var queued = EntityManager.GetEntity("QueuedEvents")?.GetComponent<QueuedEvents>();
            if (queued != null && queued.QuestIndex == 0) return false;
            return true;
        }

        private bool CanPledge()
        {
            return PledgeAvailabilityService.IsAvailable(EntityManager);
        }

        private bool HasNonFreeCardInHand()
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null || deck.Hand == null)
                return false;

            foreach (var card in deck.Hand)
            {
                var cardData = card.GetComponent<CardData>();
                if (cardData?.Card == null) continue;
                if (!cardData.Card.IsFreeAction)
                    return true;
            }

            return false;
        }

        private bool HasCardInHand(string cardId)
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null || deck.Hand == null)
                return false;

            return deck.Hand.Any(card =>
                string.Equals(card.GetComponent<CardData>()?.Card?.CardId, cardId, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasCostCardInHand()
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null || deck.Hand == null)
                return false;

            foreach (var card in deck.Hand)
            {
                var cardData = card.GetComponent<CardData>();
                if (cardData == null) continue;

                var cardObj = CardFactory.Create(cardData.Card.CardId);
                if (cardObj != null && cardObj.Cost != null && cardObj.Cost.Count > 0)
                    return true;
            }

            return false;
        }

        private bool HasEquipment()
        {
            var equipmentEntity = EntityManager.GetEntitiesWithComponent<EquippedEquipment>().FirstOrDefault();
            return equipmentEntity != null;
        }

        private bool HasMedal()
        {
            var medalEntity = EntityManager.GetEntitiesWithComponent<EquippedMedal>().FirstOrDefault();
            return medalEntity != null;
        }

        private bool HasTribulation()
        {
            var tribulationEntity = EntityManager.GetEntitiesWithComponent<Tribulation>().FirstOrDefault();
            return tribulationEntity != null;
        }

        private void StartNextTutorial()
        {
            if (_tutorialQueue.Count == 0)
            {
                _tutorialActive = false;
                _activeTutorial = null;
                EventManager.Publish(new AllTutorialsCompletedEvent());
                return;
            }

            _activeTutorial = _tutorialQueue.Dequeue();
            _tutorialActive = true;

            if (_activeTutorial.key.StartsWith("teach_", StringComparison.Ordinal))
            {
                var guided = GuidedTutorialService.GetState(EntityManager);
                guided?.SessionSeenTeaches?.Add(_activeTutorial.key);
            }
            else if (!_activeTutorial.key.StartsWith("guided_", StringComparison.Ordinal))
            {
                SaveCache.MarkTutorialSeen(_activeTutorial.key);
            }

            LoggingService.Append("TutorialManager.StartNextTutorial", new System.Text.Json.Nodes.JsonObject { ["key"] = _activeTutorial.key });
            EventManager.Publish(new TutorialStartedEvent { Tutorial = _activeTutorial });
        }

        private void OnAdvanceTutorial(AdvanceTutorialEvent evt)
        {
            if (!_tutorialActive || _activeTutorial == null)
                return;

            LoggingService.Append("TutorialManager.OnAdvanceTutorial", new System.Text.Json.Nodes.JsonObject { ["key"] = _activeTutorial.key });
            EventManager.Publish(new TutorialCompletedEvent { Tutorial = _activeTutorial });

            _activeTutorial = null;
            _tutorialActive = false;

            // Start next tutorial if available
            if (_tutorialQueue.Count > 0)
            {
                StartNextTutorial();
            }
            else
            {
                EventManager.Publish(new AllTutorialsCompletedEvent());
            }
        }

        private void Reset()
        {
            _tutorialQueue.Clear();
            _activeTutorial = null;
            _tutorialActive = false;
            _lastProcessedTurn = -1;
            _lastProcessedPhase = SubPhase.StartBattle;
        }

        /// <summary>
        /// Resolves target entity bounds for the active tutorial.
        /// Returns a list of rectangles for multi-target tutorials.
        /// </summary>
        public List<Rectangle> ResolveTargetBounds()
        {
            var bounds = new List<Rectangle>();
            if (_activeTutorial == null)
                return bounds;

            var targetIds = new List<string>();
            if (_activeTutorial.targetIds != null && _activeTutorial.targetIds.Count > 0)
            {
                targetIds.AddRange(_activeTutorial.targetIds);
            }
            else if (!string.IsNullOrEmpty(_activeTutorial.targetId))
            {
                targetIds.Add(_activeTutorial.targetId);
            }

            foreach (var targetId in targetIds)
            {
                var rect = ResolveSingleTargetBounds(_activeTutorial.targetType, targetId);
                if (rect.Width > 0 && rect.Height > 0)
                {
                    bounds.Add(rect);
                }
            }

            return bounds;
        }

        private Rectangle ResolveSingleTargetBounds(string targetType, string targetId)
        {
            switch (targetType)
            {
                case "entity_name":
                    return GetEntityBounds(targetId);
                case "component_hand":
                    return GetFirstHandCardBounds();
                case "component_any":
                    return GetFirstComponentBounds(targetId);
                case "ui_region":
                    return GetUIRegionBounds(targetId);
                case "card_with_cost":
                    return GetCardWithCostBounds();
                case "equipment":
                    return GetEquipmentBounds();
                default:
                    return Rectangle.Empty;
            }
        }

        internal Rectangle GetEntityBounds(string entityName)
        {
            var entity = EntityManager.GetEntity(entityName);
            if (entity == null)
                return Rectangle.Empty;

            var ui = entity.GetComponent<UIElement>();
            if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
                return TransformResolverService.ResolveUIBounds(EntityManager, entity, ui);

            var transform = entity.GetComponent<Transform>();
            if (transform != null)
            {
                // Fallback to transform position with default size
                return new Rectangle((int)transform.Position.X - 50, (int)transform.Position.Y - 50, 100, 100);
            }

            return Rectangle.Empty;
        }

        private Rectangle GetFirstHandCardBounds()
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null || deck.Hand == null || deck.Hand.Count == 0)
                return Rectangle.Empty;

            var firstCard = deck.Hand.FirstOrDefault();
            if (firstCard == null)
                return Rectangle.Empty;

            var ui = firstCard.GetComponent<UIElement>();
            if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
                return new Rectangle(ui.Bounds.X, ui.Bounds.Y, (ui.Bounds.Width - 20) * deck.Hand.Count, ui.Bounds.Height);

            return Rectangle.Empty;
        }

        private Rectangle GetFirstComponentBounds(string componentTypeName)
        {
            // Map component type names to actual component types
            Entity entity = null;
            switch (componentTypeName)
            {
                case "EnemyDisplay":
                    entity = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
                    break;
                case "CardData":
                    var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                    var deck = deckEntity?.GetComponent<Deck>();
                    entity = deck?.Hand?.FirstOrDefault();
                    break;
                default:
                    return Rectangle.Empty;
            }

            if (entity == null)
                return Rectangle.Empty;

            var ui = entity.GetComponent<UIElement>();
            if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
                return ui.Bounds;

            var transform = entity.GetComponent<Transform>();
            if (transform != null)
            {
                return new Rectangle((int)transform.Position.X - 50, (int)transform.Position.Y - 50, 100, 100);
            }

            return Rectangle.Empty;
        }

        private Rectangle GetEquipmentBounds()
        {
            var equipmentEntity = EntityManager.GetEntitiesWithComponent<EquippedEquipment>().FirstOrDefault();
            if (equipmentEntity == null)
            {
                LoggingService.Append("TutorialManager.GetEquipmentBounds", new System.Text.Json.Nodes.JsonObject { ["message"] = "entity not found" });
                return Rectangle.Empty;
            }

            var ui = equipmentEntity.GetComponent<UIElement>();
            if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
            {
                return new Rectangle(ui.Bounds.X, ui.Bounds.Y, ui.Bounds.Width, ui.Bounds.Height);
            }

            LoggingService.Append("TutorialManager.GetEquipmentBounds", new System.Text.Json.Nodes.JsonObject { ["message"] = "bounds not found" });
            return Rectangle.Empty;
        }

        private Rectangle GetMedalBounds()
        {
            var medalEntity = EntityManager.GetEntitiesWithComponent<EquippedMedal>().FirstOrDefault();
            if (medalEntity == null)
            {
                LoggingService.Append("TutorialManager.GetMedalBounds", new System.Text.Json.Nodes.JsonObject { ["message"] = "entity not found" });
                return Rectangle.Empty;
            }
            var ui = medalEntity.GetComponent<UIElement>();
            if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
            {
                return new Rectangle(ui.Bounds.X, ui.Bounds.Y, ui.Bounds.Width, ui.Bounds.Height);
            }

            LoggingService.Append("TutorialManager.GetMedalBounds", new System.Text.Json.Nodes.JsonObject { ["message"] = "bounds not found" });
            return Rectangle.Empty;
        }
        private Rectangle GetTribulationBounds()
        {
            var tribulationEntity = EntityManager.GetEntitiesWithComponent<Tribulation>().FirstOrDefault();
            if (tribulationEntity == null)
            {
                return Rectangle.Empty;
            }
            var ui = tribulationEntity.GetComponent<UIElement>();
            if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
            {
                return new Rectangle(ui.Bounds.X, ui.Bounds.Y, ui.Bounds.Width, ui.Bounds.Height);
            }
            LoggingService.Append("TutorialManager.GetTribulationBounds", new System.Text.Json.Nodes.JsonObject { ["message"] = "bounds not found" });
            return Rectangle.Empty;
        }
        private Rectangle GetUIRegionBounds(string regionId)
        {
            Rectangle CardBounds(string cardId)
            {
                var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
                var card = deck?.Hand?.FirstOrDefault(entity =>
                    string.Equals(entity.GetComponent<CardData>()?.Card?.CardId, cardId, StringComparison.OrdinalIgnoreCase));
                return card?.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty;
            }

            Rectangle Union(params Rectangle[] rectangles)
            {
                return UnionBounds(rectangles);
            }

            // Predefined UI regions
            switch (regionId)
            {
                case "player_hand":
                    var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
                    return UnionBounds(deck?.Hand
                        .Select(card => card.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty)
                        ?? Enumerable.Empty<Rectangle>());

                case "enemy_attack_display":
                    return new Rectangle(Game1.VirtualWidth / 4, Game1.VirtualHeight / 4, Game1.VirtualWidth / 2, 150);
                case "first_black_card":
                    return GetFirstCardBoundsByColor(CardData.CardColor.Black);
                case "first_red_card":
                    return GetFirstCardBoundsByColor(CardData.CardColor.Red);
                case "first_white_card":
                    return GetFirstCardBoundsByColor(CardData.CardColor.White);
                case "reckoning":
                    return CardBounds("reckoning");
                case "ap_and_smite_ap":
                    return Union(GetEntityBounds("UI_APTooltip"), CardBounds("smite"));
                case "smite_damage":
                    return CardBounds("smite");
                case "absolution_reckoning_costs":
                    return Union(CardBounds("absolution"), CardBounds("reckoning"));
                case "absolution_and_courage":
                    return Union(CardBounds("absolution"), GetEntityBounds("UI_CourageTooltip"));
                case "reckoning_and_temperance":
                    return Union(CardBounds("reckoning"), GetEntityBounds("UI_TemperanceTooltip"));
                case "smite_and_litany":
                    return Union(CardBounds("smite"), CardBounds("litany_of_wrath"));
                case "fervor_and_pledge":
                    return Union(CardBounds("fervor"), GetEntityBounds("UI_PledgeTooltip"));
                case "litany_and_fervor":
                    return Union(CardBounds("litany_of_wrath"), CardBounds("fervor"));

                default:
                    return Rectangle.Empty;
            }
        }

        internal static Rectangle UnionBounds(IEnumerable<Rectangle> rectangles)
        {
            var valid = rectangles.Where(rect => rect.Width > 0 && rect.Height > 0).ToList();
            if (valid.Count == 0) return Rectangle.Empty;

            int left = valid.Min(rect => rect.Left);
            int top = valid.Min(rect => rect.Top);
            int right = valid.Max(rect => rect.Right);
            int bottom = valid.Max(rect => rect.Bottom);
            return new Rectangle(left, top, right - left, bottom - top);
        }

        private Rectangle GetFirstCardBoundsByColor(CardData.CardColor color)
        {
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck?.Hand == null) return Rectangle.Empty;

            foreach (var card in deck.Hand)
            {
                var cardData = card.GetComponent<CardData>();
                var ui = card.GetComponent<UIElement>();
                if (cardData?.Color == color && ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
                    return ui.Bounds;
            }
            return Rectangle.Empty;
        }

        private Rectangle GetCardWithCostBounds()
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null || deck.Hand == null || deck.Hand.Count == 0)
                return Rectangle.Empty;

            foreach (var card in deck.Hand)
            {
                var cardData = card.GetComponent<CardData>();
                if (cardData == null) continue;

                if (cardData.Card.Cost != null && cardData.Card.Cost.Count > 0)
                {
                    var ui = card.GetComponent<UIElement>();
                    if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
                    {
                        return new Rectangle(ui.Bounds.X, ui.Bounds.Y, ui.Bounds.Width, ui.Bounds.Height);
                    }

                }
            }
            return Rectangle.Empty;
        }
    }
}
