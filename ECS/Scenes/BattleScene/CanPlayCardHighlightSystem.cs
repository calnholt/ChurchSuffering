using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Scenes.BattleScene
{
    [DebugTab("Can Play Highlight")]
    public class CanPlayCardHighlightSystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly Dictionary<(int w, int h, int r), Texture2D> _roundedRectCache = new();
        private double _totalSeconds;

        // Cached per-frame state for the event handler
        private readonly HashSet<Entity> _playableCards = new();
        private CanPlayHighlightSettings _cachedSettings;
        private int _cachedCornerRadius;
        private int _cachedBorderThickness;
        private CardGeometrySettings _cachedGeometrySettings;
        private float _cachedPulseAmount;
        private Color _cachedGlowColor;

        public CanPlayCardHighlightSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            EventManager.Subscribe<DeleteCachesEvent>(_ => _roundedRectCache.Clear());
            EventManager.Subscribe<HighlightRenderEvent>(OnHighlightRender);
        }

        protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            _totalSeconds = gameTime.TotalGameTime.TotalSeconds;
            _playableCards.Clear();
            if (GuidedTutorialService.IsActive(EntityManager)) return;

            // Get current phase
            var phaseEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
            if (phaseEntity == null) return;
            var phase = phaseEntity.GetComponent<PhaseState>();
            if (phase.Sub != SubPhase.Action && phase.Sub != SubPhase.Block) return;

            // Get hand
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null || deck.Hand.Count == 0) return;

            // Cache settings
            var settingsEntity = EntityManager.GetEntitiesWithComponent<CanPlayHighlightSettings>().FirstOrDefault();
            _cachedSettings = settingsEntity?.GetComponent<CanPlayHighlightSettings>() ?? new CanPlayHighlightSettings();

            _cachedGeometrySettings = CardGeometryService.GetSettings(EntityManager);
            _cachedCornerRadius = _cachedGeometrySettings?.CardCornerRadius ?? CardGeometrySettings.DefaultCornerRadius;
            _cachedBorderThickness = _cachedGeometrySettings?.HighlightBorderThickness ?? CardGeometrySettings.DefaultHighlightBorderThickness;

            // Pulse animation
            var hs = _cachedSettings;
            float pulse01 = (float)(Math.Cos(_totalSeconds * hs.GlowPulseSpeed) * 0.5 + 0.5);
            float eased = (float)Math.Pow(MathHelper.Clamp(pulse01, 0f, 1f), hs.GlowEasingPower);
            _cachedPulseAmount = MathHelper.Lerp(
                MathHelper.Clamp(hs.GlowMinIntensity, 0f, 1f),
                MathHelper.Clamp(hs.GlowMaxIntensity, 0f, 1f),
                eased);
            _cachedGlowColor = new Color((byte)hs.GlowColorR, (byte)hs.GlowColorG, (byte)hs.GlowColorB);

            // Pre-gather data needed for action phase checks
            Entity player = null;
            ActionPoints ap = null;
            AppliedPassives appliedPassives = null;
            List<Entity> costEligibleCards = null;

            if (phase.Sub == SubPhase.Action)
            {
                player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                ap = player?.GetComponent<ActionPoints>();
                appliedPassives = GetComponentHelper.GetAppliedPassives(EntityManager, "Player");
                costEligibleCards = BuildCostEligibleCards(deck);
            }

            // Check for active attack intent during block phase
            PlannedAttack activePlannedAttack = null;
            if (phase.Sub == SubPhase.Block)
            {
                var enemy = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
                var pa = enemy?.GetComponent<AttackIntent>()?.Planned?.FirstOrDefault();
                activePlannedAttack = pa;
            }

            // Build set of playable cards
            foreach (var cardEntity in deck.Hand)
            {
                if (cardEntity.GetComponent<AnimatingHandToDiscard>() != null) continue;
                if (cardEntity.GetComponent<AnimatingHandToZone>() != null) continue;
                if (cardEntity.GetComponent<AnimatingHandToDrawPile>() != null) continue;
                if (cardEntity.GetComponent<AssignedBlockCard>() != null) continue;
                if (cardEntity.GetComponent<SelectedForPayment>() != null) continue;
                if (cardEntity.GetComponent<FilteredFromHand>() != null) continue;

                var data = cardEntity.GetComponent<CardData>();
                if (data == null) continue;

                bool canPlay = false;
                if (phase.Sub == SubPhase.Action)
                    canPlay = IsPlayableInAction(cardEntity, data, ap, appliedPassives, costEligibleCards);
                else if (phase.Sub == SubPhase.Block)
                    canPlay = IsPlayableInBlock(cardEntity, data, activePlannedAttack);

                if (canPlay)
                    _playableCards.Add(cardEntity);
            }

            base.Update(gameTime);
        }

        private void OnHighlightRender(HighlightRenderEvent evt)
        {
            if (!_playableCards.Contains(evt.Entity)) return;

            var geometry = CardGeometryService.GetVisualGeometry(EntityManager, evt.Entity, evt.Transform?.Position);
            var cardRect = geometry.Bounds;

            if (cardRect.Width <= 1 || cardRect.Height <= 1) return;

            int cornerRadius = (int)Math.Round(_cachedCornerRadius * geometry.Scale);
            int borderThickness = (int)Math.Round(_cachedBorderThickness * geometry.Scale);
            DrawGlow(cardRect, geometry.Rotation, cornerRadius, borderThickness, _cachedSettings, _cachedPulseAmount, _cachedGlowColor);
        }

        // --- Action phase playability check ---
        private bool IsPlayableInAction(Entity cardEntity, CardData data, ActionPoints ap, AppliedPassives appliedPassives, List<Entity> costEligibleCards)
        {
            var card = data.Card;
            var alternateProfile = AlternateCardPlayService.GetProfile(EntityManager, cardEntity, SubPhase.Action);

            // Relics can't be played
            if (card.Type == CardType.Relic) return false;

            // Block cards need an alternate play profile during Action phase
            if (card.Type == CardType.Block && alternateProfile?.AllowsPlay != true) return false;

            // Need AP unless free action
            bool isFree = card.IsFreeAction || alternateProfile?.IsFreeAction == true;
            if (!isFree)
            {
                int currentAp = ap?.Current ?? 0;
                if (currentAp <= 0) return false;
            }

            // Card-specific CanPlay check (now pure bool, safe per-frame)
            bool skipBlockCanPlay = card.Type == CardType.Block && alternateProfile?.AllowsPlay == true;
            if (!skipBlockCanPlay && card.CanPlay != null && !card.CanPlay(EntityManager, cardEntity)) return false;

            var pledge = cardEntity.GetComponent<Pledge>();
            if (pledge != null && !pledge.CanPlay) return false;

            // Silenced + pledged can't play
            if (cardEntity.HasComponent<Pledge>() && appliedPassives != null)
            {
                appliedPassives.Passives.TryGetValue(AppliedPassiveType.Silenced, out int silencedStacks);
                if (silencedStacks > 0) return false;
            }

            // Check discard costs can be satisfied (after Vigor reduction)
            var costs = VigorService.GetEffectiveCost(card, VigorService.GetPlayerVigorStacks(EntityManager));
            if (costs != null && costs.Count > 0)
            {
                // Build available cards excluding self
                var available = costEligibleCards.Where(c => c != cardEntity).ToList();
                if (!CanSatisfyCosts(costs, available)) return false;
            }

            return true;
        }

        // --- Block phase playability check ---
        private bool IsPlayableInBlock(Entity cardEntity, CardData data, PlannedAttack activePlannedAttack)
        {
            return activePlannedAttack != null
                && EnemyBlockerEligibilityService.IsEligibleHandBlocker(EntityManager, cardEntity, activePlannedAttack);
        }

        // --- Build list of cards eligible to pay costs (excludes weapons, tokens, yellow, pledged) ---
        private List<Entity> BuildCostEligibleCards(Deck deck)
        {
            var result = new List<Entity>();
            foreach (var c in deck.Hand)
            {
                var cd = c.GetComponent<CardData>();
                if (cd == null) continue;
                if (cd.Color == CardData.CardColor.Yellow) continue;
                if (!cd.Card.CanDiscardForCost) continue;
                if (c.GetComponent<Pledge>() != null) continue;
                result.Add(c);
            }
            return result;
        }

        // --- Greedy cost satisfaction (mirrors CardPlaySystem.CanSatisfy) ---
        private bool CanSatisfyCosts(List<string> requiredCosts, List<Entity> candidates)
        {
            var remaining = new List<string>(requiredCosts);
            var used = new HashSet<Entity>();

            // Match specific colors first
            foreach (var e in candidates)
            {
                if (remaining.Count == 0) break;
                int idx = remaining.FindIndex(r =>
                    r != "Any" && CardColorQualificationService.IsEligibleForCost(e, r));
                if (idx >= 0)
                {
                    used.Add(e);
                    remaining.RemoveAt(idx);
                }
            }

            // Then satisfy Any with remaining cards
            foreach (var e in candidates)
            {
                if (remaining.Count == 0) break;
                if (used.Contains(e)) continue;
                int idx = remaining.FindIndex(r => r == "Any");
                if (idx >= 0)
                {
                    used.Add(e);
                    remaining.RemoveAt(idx);
                }
            }

            return remaining.Count == 0;
        }

        // --- Draw glow layers around a card rect ---
        private void DrawGlow(Rectangle bounds, float rotation, int cornerRadius, int borderThickness, CanPlayHighlightSettings hs, float pulseAmount, Color glowColor)
        {
            int th = borderThickness;
            var highlightRect = new Rectangle(
                bounds.X - th,
                bounds.Y - th,
                bounds.Width + th * 2,
                bounds.Height + th * 2);

            int radius = Math.Max(0, cornerRadius + th);
            var baseTex = GetRoundedRectTexture(highlightRect.Width, highlightRect.Height, radius);
            var center = new Vector2(highlightRect.X + highlightRect.Width / 2f, highlightRect.Y + highlightRect.Height / 2f);

            int layers = hs.GlowLayers;
            float spread = hs.GlowSpread;

            for (int i = layers; i >= 1; i--)
            {
                float spreadAnim = 1f + hs.GlowSpreadAmplitude * (float)Math.Sin(_totalSeconds * hs.GlowSpreadSpeed);
                float scale = 1f + i * spread * spreadAnim;
                float layerAlpha = MathHelper.Clamp(pulseAmount * (0.22f / i), 0f, hs.MaxAlpha);
                _spriteBatch.Draw(
                    baseTex,
                    position: center,
                    sourceRectangle: null,
                    color: glowColor * layerAlpha,
                    rotation: rotation,
                    origin: new Vector2(baseTex.Width / 2f, baseTex.Height / 2f),
                    scale: new Vector2(scale, scale),
                    effects: SpriteEffects.None,
                    layerDepth: 0f);
            }
        }

        private Texture2D GetRoundedRectTexture(int width, int height, int radius)
        {
            var key = (width, height, radius);
            if (_roundedRectCache.TryGetValue(key, out var tex)) return tex;
            var texture = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, width, height, radius);
            _roundedRectCache[key] = texture;
            return texture;
        }
    }
}
