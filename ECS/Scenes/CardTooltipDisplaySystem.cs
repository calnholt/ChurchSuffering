using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Services;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Card Tooltip")]
	public class CardTooltipDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private CardGeometrySettings _settings;
		private readonly Dictionary<string, Entity> _tooltipCardCache = new();
		private float _pulsePhase;
		private int? _lastPulseHoverId;

		[DebugEditable(DisplayName = "Gap Override (px)", Step = 1, Min = 0, Max = 200)]
		public int GapOverride { get; set; } = 0;

		[DebugEditable(DisplayName = "Screen Padding (px)", Step = 1, Min = 0, Max = 200)]
		public int ScreenPadding { get; set; } = 8;

		[DebugEditable(DisplayName = "Pulse Cycle (s)", Step = 0.1f, Min = 0.5f, Max = 10f)]
		public float PulseCycleSeconds { get; set; } = 4f;

		[DebugEditable(DisplayName = "Pulse Upgraded Hold Frac", Step = 0.01f, Min = 0f, Max = 0.45f)]
		public float PulseUpgradedHoldFrac { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Pulse Base Hold Frac", Step = 0.01f, Min = 0f, Max = 0.45f)]
		public float PulseBaseHoldFrac { get; set; } = 0.45f;

		public CardTooltipDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			EventManager.Subscribe<DeleteCachesEvent>(_ => _tooltipCardCache.Clear());
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<CardTooltip>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			float cycle = System.Math.Max(0.001f, PulseCycleSeconds);
			_pulsePhase += (float)gameTime.ElapsedGameTime.TotalSeconds / cycle;
			if (_pulsePhase >= 1f) _pulsePhase -= (float)System.Math.Floor(_pulsePhase);
		}

		public void Draw()
		{
			if (StateSingleton.IsTutorialActive) return;
			// Find top-most hovered entity with CardTooltip
			var hoverables = GetRelevantEntities()
				.Select(e => new { E = e, UI = e.GetComponent<UIElement>(), T = e.GetComponent<Transform>(), CT = e.GetComponent<CardTooltip>(), CD = e.GetComponent<CardData>() })
				.Where(x => x.UI != null && x.UI.IsHovered && x.UI.TooltipType == TooltipType.Card && x.CT != null && !string.IsNullOrWhiteSpace(x.CT.CardId))
				.OrderByDescending(x => x.T?.ZOrder ?? 0)
				.ToList();
			var top = hoverables.FirstOrDefault();
			if (top == null)
			{
				_lastPulseHoverId = null;
				return;
			}

			if (top.CT.CrossfadeUpgradePreview && _lastPulseHoverId != top.E.Id)
			{
				_pulsePhase = 0f;
				_lastPulseHoverId = top.E.Id;
			}
			else if (!top.CT.CrossfadeUpgradePreview)
			{
				_lastPulseHoverId = null;
			}

			// Ensure settings
			if (_settings == null)
			{
				_settings = CardGeometryService.GetSettings(EntityManager);
				if (_settings == null) return;
			}

			int w = (int)System.Math.Round(_settings.CardWidth * top.CT.TooltipScale);
			int h = (int)System.Math.Round(_settings.CardHeight * top.CT.TooltipScale);
			int gap = GapOverride > 0 ? GapOverride : System.Math.Max(0, top.UI.TooltipOffsetPx);
			Rectangle anchorBounds = TransformResolverService.ResolveUIBounds(EntityManager, top.E, top.UI);

			int rx = anchorBounds.X;
			int ry = anchorBounds.Y;
			switch (top.UI.TooltipPosition)
			{
				case TooltipPosition.Above:
					rx = anchorBounds.X + (anchorBounds.Width - w) / 2;
					ry = anchorBounds.Y - h - gap;
					break;
				case TooltipPosition.Below:
					rx = anchorBounds.X + (anchorBounds.Width - w) / 2;
					ry = anchorBounds.Bottom + gap;
					break;
				case TooltipPosition.Right:
					rx = anchorBounds.Right + gap;
					ry = anchorBounds.Y + (anchorBounds.Height - h) / 2;
					break;
				case TooltipPosition.Left:
					rx = anchorBounds.X - w - gap;
					ry = anchorBounds.Y + (anchorBounds.Height - h) / 2;
					break;
			}

			// Clamp to screen
			var vp = _graphicsDevice.Viewport;
			int pad = System.Math.Max(0, ScreenPadding);
			rx = System.Math.Max(pad, System.Math.Min(rx, vp.Width - w - pad));
			ry = System.Math.Max(pad, System.Math.Min(ry, vp.Height - h - pad));

			// Convert tooltip rect top-left to the card center expected by CardDisplaySystem (account for CardOffsetYExtra)
			int offsetY = (int)System.Math.Round(_settings.CardOffsetYExtra * top.CT.TooltipScale);
			var center = new Vector2(rx + w / 2f, ry + (h / 2f + offsetY));

			var color = top.CT.CardColor ?? top.CD?.Color ?? CardData.CardColor.White;
			var previewRestrictions = BuildPreviewRestrictions(top.CT, top.E);

			if (top.CT.CrossfadeUpgradePreview)
			{
				var baseCard = GetOrCreateTooltipCard(top.CT.CardId, color, isUpgraded: false, previewRestrictions);
				var upgradedCard = GetOrCreateTooltipCard(top.CT.CardId, color, isUpgraded: true, previewRestrictions);
				if (baseCard == null || upgradedCard == null) return;

				// Draw base card always at full opacity underneath
				EventManager.Publish(new CardRenderScaledEvent
				{
					Card = baseCard,
					Position = center,
					Scale = top.CT.TooltipScale,
					Alpha = 1f,
				});

				// Upgraded card pulses 1 -> 0 -> 1 over the cycle, revealing the base when faded out.
				// Phase is split into four zones: upgraded hold | fade out | base hold | fade in
				float upgradedAlpha = ComputePulseAlpha(_pulsePhase, PulseUpgradedHoldFrac, PulseBaseHoldFrac);

				if (upgradedAlpha > 0.001f)
				{
					EventManager.Publish(new CardRenderScaledEvent
					{
						Card = upgradedCard,
						Position = center,
						Scale = top.CT.TooltipScale,
						Alpha = upgradedAlpha,
					});
				}
				return;
			}

			var cardEntity = GetOrCreateTooltipCard(top.CT.CardId, color, top.CT.IsUpgraded, previewRestrictions);
			if (cardEntity == null) return;

			EventManager.Publish(new CardRenderScaledEvent { Card = cardEntity, Position = center, Scale = top.CT.TooltipScale });
		}

		private Entity GetOrCreateTooltipCard(
			string cardId,
			CardData.CardColor color,
			bool isUpgraded,
			IReadOnlyList<string> previewRestrictions)
		{
			var restrictionKey = string.Join(",", NormalizeRestrictions(previewRestrictions));
			var key = cardId + "|" + color + (isUpgraded ? "|upgraded" : "") + "|" + restrictionKey;
			if (!_tooltipCardCache.TryGetValue(key, out var cardEntity) || cardEntity == null)
			{
				cardEntity = ECS.Factories.EntityFactory.CreateCardFromDefinition(EntityManager, cardId, color, allowWeapons: true, index: 0, isUpgraded: isUpgraded);
				if (cardEntity != null)
				{
					var ui = cardEntity.GetComponent<UIElement>();
					if (ui != null)
					{
						ui.IsInteractable = false;
						ui.TooltipType = TooltipType.None;
					}
					ApplyPreviewRestrictions(cardEntity, previewRestrictions);
					_tooltipCardCache[key] = cardEntity;
				}
			}
			return cardEntity;
		}

		private static IReadOnlyList<string> BuildPreviewRestrictions(CardTooltip tooltip, Entity sourceEntity)
		{
			var restrictions = new List<string>();
			foreach (var restriction in tooltip?.PreviewRestrictionNames ?? new List<string>())
			{
				AddRestriction(restrictions, restriction);
			}

			if (sourceEntity?.HasComponent<Colorless>() == true)
			{
				AddRestriction(restrictions, RunScopedStateService.RestrictionColorless);
			}

			return NormalizeRestrictions(restrictions);
		}

		private void ApplyPreviewRestrictions(Entity cardEntity, IReadOnlyList<string> restrictions)
		{
			foreach (var restriction in NormalizeRestrictions(restrictions))
			{
				switch (restriction)
				{
					case RunScopedStateService.RestrictionFrozen:
						EntityManager.AddComponent(cardEntity, new Frozen { Owner = cardEntity });
						break;
					case RunScopedStateService.RestrictionBrittle:
						EntityManager.AddComponent(cardEntity, new Brittle { Owner = cardEntity });
						break;
					case RunScopedStateService.RestrictionScorched:
						EntityManager.AddComponent(cardEntity, new Scorched { Owner = cardEntity });
						break;
					case RunScopedStateService.RestrictionThorned:
						EntityManager.AddComponent(cardEntity, new Thorned { Owner = cardEntity });
						break;
					case RunScopedStateService.RestrictionColorless:
						EntityManager.AddComponent(cardEntity, new Colorless { Owner = cardEntity });
						break;
					case RunScopedStateService.RestrictionCursed:
						CardApplicationManagementSystem.ApplyCursedRuntime(EntityManager, cardEntity);
						break;
				}
			}
		}

		private static List<string> NormalizeRestrictions(IReadOnlyList<string> restrictions)
		{
			var normalized = new List<string>();
			foreach (var restriction in restrictions ?? new List<string>())
			{
				AddRestriction(normalized, restriction);
			}
			return normalized
				.OrderBy(restriction => restriction, System.StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static void AddRestriction(List<string> restrictions, string restriction)
		{
			if (restrictions == null || string.IsNullOrWhiteSpace(restriction)) return;
			if (!restrictions.Contains(restriction, System.StringComparer.OrdinalIgnoreCase))
			{
				restrictions.Add(restriction);
			}
		}

		private static float ComputePulseAlpha(float phase, float upgradedHoldFrac, float baseHoldFrac)
		{
			float totalHold = System.Math.Clamp(upgradedHoldFrac, 0f, 0.45f) + System.Math.Clamp(baseHoldFrac, 0f, 0.45f);
			float fadeFrac = (1f - totalHold) / 2f;
			if (fadeFrac <= 0f) return 1f;

			float uEnd = upgradedHoldFrac;
			float f1End = uEnd + fadeFrac;
			float bEnd = f1End + baseHoldFrac;
			float f2End = bEnd + fadeFrac;

			if (phase <= uEnd)
				return 1f;
			if (phase <= f1End)
				return 1f - (phase - uEnd) / fadeFrac;
			if (phase <= bEnd)
				return 0f;
			if (phase <= f2End)
				return (phase - bEnd) / fadeFrac;
			return 1f;
		}
	}
}
