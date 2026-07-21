using System;
using System.Linq;
using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Singletons;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Legacy renderer retained only for the plundered-card damage gauge.
	/// Technical debt: move that gauge into PlunderDisplaySystem, then delete this system.
	/// </summary>
	[Obsolete("Legacy renderer retained only for the plundered-card damage gauge; move that gauge into PlunderDisplaySystem before deleting this system.")]
	[DebugTab("Legacy Plunder HP Display")]
	public class HPDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private Texture2D _pixel;
		private Texture2D _roundedBack;
		private Texture2D _roundedFill;
		private Texture2D _roundedHighlight;
		private int _cachedRoundedWidth;
		private int _cachedRoundedHeight;
		private int _cachedCornerRadius;
		private SpriteFont _font = FontSingleton.ContentFont;
		private class FillAnimState { public float Start; public float End; public float Displayed; public float Elapsed; public float Duration; }
		private readonly Dictionary<int, FillAnimState> _animByEntityId = new Dictionary<int, FillAnimState>();
		private float _accumSeconds;

		[DebugEditable(DisplayName = "Bar Width", Step = 2, Min = 10, Max = 2000)]
		public int BarWidth { get; set; } = 200;

		[DebugEditable(DisplayName = "Bar Height", Step = 1, Min = 4, Max = 200)]
		public int BarHeight { get; set; } = 22;

		[DebugEditable(DisplayName = "Offset X", Step = 2, Min = -2000, Max = 2000)]
		public int OffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Offset Y", Step = 2, Min = -2000, Max = 2000)]
		public int OffsetY { get; set; } = 26;

		[DebugEditable(DisplayName = "Corner Radius", Step = 1, Min = 0, Max = 64)]
		public int CornerRadius { get; set; } = 12;

		[DebugEditable(DisplayName = "Fill % / Sec", Step = 0.1f, Min = 0.1f, Max = 10f)]
		public float FillPercentPerSecond { get; set; } = 0.5f;

		[DebugEditable(DisplayName = "Tween Seconds", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float FillTweenSeconds { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Ease Exponent", Step = 0.1f, Min = 1f, Max = 5f)]
		public float FillEaseExponent { get; set; } = 2.5f;

		// Low-HP flashing
		[DebugEditable(DisplayName = "Flash Enabled")]
		public bool FlashEnabled { get; set; } = true;

		[DebugEditable(DisplayName = "Flash Threshold %", Step = 0.05f, Min = 0f, Max = 1f)]
		public float FlashThresholdPercent { get; set; } = 0.20f;

		[DebugEditable(DisplayName = "Flash Hz", Step = 0.1f, Min = 0.1f, Max = 10f)]
		public float FlashFrequencyHz { get; set; } = 2.0f;

		[DebugEditable(DisplayName = "Flash Min Intensity", Step = 0.05f, Min = 0f, Max = 1f)]
		public float FlashMinIntensity { get; set; } = 0.25f;

		[DebugEditable(DisplayName = "Flash Max Intensity", Step = 0.05f, Min = 0f, Max = 1f)]
		public float FlashMaxIntensity { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Flash Overlay Strength", Step = 0.05f, Min = 0f, Max = 1f)]
		public float FlashOverlayStrength { get; set; } = 0.6f; // kept for compatibility; not used when opacity flashing

		[DebugEditable(DisplayName = "Flash Alpha Min", Step = 0.05f, Min = 0f, Max = 1f)]
		public float FlashAlphaMin { get; set; } = 0.35f;

		[DebugEditable(DisplayName = "Flash Alpha Max", Step = 0.05f, Min = 0f, Max = 1f)]
		public float FlashAlphaMax { get; set; } = 1.0f;

		// Incoming damage overlay flashing (previews potential HP loss)
		[DebugEditable(DisplayName = "Incoming Damage Flash Enabled")]
		public bool IncomingDamageFlashEnabled { get; set; } = true;

		[DebugEditable(DisplayName = "Incoming Damage Flash Hz", Step = 0.1f, Min = 0.1f, Max = 10f)]
		public float IncomingDamageFlashHz { get; set; } = 0.4f;

		[DebugEditable(DisplayName = "Incoming Damage Alpha Min", Step = 0.05f, Min = 0f, Max = 1f)]
		public float IncomingDamageAlphaMin { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Incoming Damage Alpha Max", Step = 0.05f, Min = 0f, Max = 1f)]
		public float IncomingDamageAlphaMax { get; set; } = 0.45f;

		// Pill look controls (do not modify texture creation)
		[DebugEditable(DisplayName = "Highlight Opacity", Step = 0.05f, Min = 0f, Max = 1f)]
		public float HighlightOpacity { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Highlight Height", Step = 1, Min = 1, Max = 400)]
		public int HighlightHeight { get; set; } = 13;

		[DebugEditable(DisplayName = "Highlight Offset Y", Step = 1, Min = -200, Max = 200)]
		public int HighlightOffsetY { get; set; } = 0;

		public HPDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<ChurchSuffering.ECS.Components.HP>()
				.Where(ShouldRenderLegacyPlunderHp);
		}

		internal static bool ShouldRenderLegacyPlunderHp(Entity entity)
		{
			return entity != null && entity.HasComponent<Plundered>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var hp = entity.GetComponent<ChurchSuffering.ECS.Components.HP>();
			if (hp == null) return;
			float targetPct = hp.Max > 0 ? MathHelper.Clamp(hp.Current / (float)hp.Max, 0f, 1f) : 0f;
			if (!_animByEntityId.TryGetValue(entity.Id, out var state))
			{
				state = new FillAnimState { Start = targetPct, End = targetPct, Displayed = targetPct, Elapsed = 0f, Duration = Math.Max(0.001f, FillTweenSeconds) };
				_animByEntityId[entity.Id] = state;
			}
			// Start a new tween if target changed
			if (Math.Abs(state.End - targetPct) > 0.0001f)
			{
				state.Start = state.Displayed;
				state.End = targetPct;
				state.Elapsed = 0f;
				state.Duration = Math.Max(0.001f, FillTweenSeconds);
			}
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			state.Elapsed += dt;
			_accumSeconds += dt;
			float t = state.Duration <= 0f ? 1f : MathHelper.Clamp(state.Elapsed / state.Duration, 0f, 1f);
			float eased = EaseInOutPow(t, FillEaseExponent);
			state.Displayed = MathHelper.Lerp(state.Start, state.End, eased);
			_animByEntityId[entity.Id] = state;
		}

		private static float MoveTowards(float current, float target, float maxDelta)
		{
			if (Math.Abs(target - current) <= maxDelta) return target;
			return current + Math.Sign(target - current) * maxDelta;
		}

		private static float EaseInOutPow(float t, float power)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float p = Math.Max(1f, power);
			if (t < 0.5f)
			{
				return 0.5f * (float)Math.Pow(t * 2f, p);
			}
			else
			{
				return 1f - 0.5f * (float)Math.Pow((1f - t) * 2f, p);
			}
		}

		public void Draw()
		{
			var entities = GetRelevantEntities().ToList();
			if (entities.Count == 0) return;

			foreach (var hpEntity in entities)
			{
				var hp = hpEntity.GetComponent<ChurchSuffering.ECS.Components.HP>();
				var parentTransform = hpEntity.GetComponent<Transform>();
				if (hp == null || parentTransform == null) continue;

				// Per-entity override takes priority over global defaults
				var hpOverride = hpEntity.GetComponent<HPBarOverride>();
				int width, height;
				int x, y;
				if (hpOverride != null)
				{
					width = Math.Max(4, hpOverride.BarWidth);
					height = Math.Max(2, hpOverride.BarHeight);
					var center = new Vector2(parentTransform.Position.X + hpOverride.OffsetX, parentTransform.Position.Y + hpOverride.OffsetY);
					x = (int)Math.Round(center.X - width / 2f);
					y = (int)Math.Round(center.Y - height / 2f);
				}
				else
				{
					width = Math.Max(4, BarWidth);
					height = Math.Max(2, BarHeight);

					// Position below the entity's portrait, using base (stable) scale if provided
					float visualHalfHeight = 0f;
					var pInfo = hpEntity.GetComponent<PortraitInfo>();
					if (pInfo != null)
					{
						float baseScale = (pInfo.BaseScale > 0f) ? pInfo.BaseScale : 1f;
						visualHalfHeight = Math.Max(visualHalfHeight, (pInfo.TextureHeight * baseScale) * 0.5f);
					}
					// Center X aligns to entity; OffsetX shifts from center, OffsetY moves downward/upward
					var center = new Vector2(parentTransform.Position.X + OffsetX, parentTransform.Position.Y + visualHalfHeight + OffsetY);
					x = (int)Math.Round(center.X - width / 2f);
					y = (int)Math.Round(center.Y - height / 2f);
				}

			// Prepare rounded textures (cache per size)
			int radius = Math.Max(0, Math.Min(CornerRadius, Math.Min(width, height) / 2));
			bool needsRebuild = _roundedBack == null || _cachedRoundedWidth != width || _cachedRoundedHeight != height || _cachedCornerRadius != radius;
			if (needsRebuild)
			{
				_roundedBack?.Dispose();
				_roundedFill?.Dispose();
				_roundedHighlight?.Dispose();
				_roundedBack = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, width, height, radius);
				_roundedFill = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, width, height, radius);
				_roundedHighlight = CreateRoundedHighlightTexture(_graphicsDevice, width, height, radius);
				_cachedRoundedWidth = width;
				_cachedRoundedHeight = height;
				_cachedCornerRadius = radius;
			}

			// Background rounded rect (dark gray)
				var backRect = new Rectangle(x, y, width, height);
				_spriteBatch.Draw(_roundedBack, backRect, new Color((byte)40, (byte)40, (byte)40));
				// Publish/update HP bar anchor rect for this entity
				var anchor = hpEntity.GetComponent<HPBarAnchor>();
				if (anchor == null)
				{
					anchor = new HPBarAnchor { Rect = backRect };
					EntityManager.AddComponent(hpEntity, anchor);
				}
				else
				{
					anchor.Rect = backRect;
				}

			// Fill
				float targetPctForDraw = hp.Max > 0 ? MathHelper.Clamp(hp.Current / (float)hp.Max, 0f, 1f) : 0f;
				float pct = _animByEntityId.TryGetValue(hpEntity.Id, out var s) ? s.Displayed : targetPctForDraw;
				int fillW = (int)Math.Round(width * pct);
				var fillRect = new Rectangle(x, y, Math.Max(0, fillW), height);
			var fillColorBase = Color.Lerp(new Color((byte)120, (byte)0, (byte)0), new Color((byte)255, (byte)40, (byte)40), pct);
			float alphaFactor = 1f;
			if (FlashEnabled && targetPctForDraw <= MathHelper.Clamp(FlashThresholdPercent, 0f, 1f))
			{
				float phase = (float)Math.Sin(MathHelper.TwoPi * Math.Max(0.01f, FlashFrequencyHz) * _accumSeconds);
				float norm = 0.5f * (phase + 1f); // 0..1
				float intensity = MathHelper.Lerp(MathHelper.Clamp(FlashMinIntensity, 0f, 1f), MathHelper.Clamp(FlashMaxIntensity, 0f, 1f), norm);
				alphaFactor = MathHelper.Lerp(MathHelper.Clamp(FlashAlphaMin, 0f, 1f), MathHelper.Clamp(FlashAlphaMax, 0f, 1f), intensity);
			}
			var fillColor = Color.FromNonPremultiplied(fillColorBase.R, fillColorBase.G, fillColorBase.B, (int)MathHelper.Clamp(255f * alphaFactor, 0f, 255f));
			// For the fill, draw the rounded texture but clip to fill width via destination width
			if (fillRect.Width > 0)
			{
				int srcW = Math.Max(1, fillRect.Width);
				var src = new Rectangle(0, 0, srcW, height);
				_spriteBatch.Draw(_roundedFill, fillRect, src, fillColor);
				if (_roundedHighlight != null)
				{
					// Adjustable pill overlay band
					int bandH = Math.Max(1, Math.Min(HighlightHeight, height));
					int destY = y + (height - bandH) / 2 + HighlightOffsetY;
					var bandDest = new Rectangle(x, destY, fillRect.Width, bandH);
					int srcBandY = Math.Max(0, (height - bandH) / 2);
					var bandSrc = new Rectangle(0, srcBandY, srcW, bandH);
					var tint = Color.FromNonPremultiplied(255, 255, 255, (int)(MathHelper.Clamp(HighlightOpacity * alphaFactor, 0f, 1f) * 255f));
					_spriteBatch.Draw(_roundedHighlight, bandDest, bandSrc, tint);
				}
			}

			// Incoming damage overlay on player's HP bar: flash a segment representing potential HP loss
			var isPlayer = hpEntity.GetComponent<ChurchSuffering.ECS.Components.Player>() != null;
			if (isPlayer && IncomingDamageFlashEnabled && hp.Max > 0)
			{
				int totalIncoming = 0;
				var activeSequences = EntityManager.GetEntitiesWithComponent<AttackIntent>()
					.Select(en => en.GetComponent<AttackIntent>())
					.Where(ai => ai != null && ai.Planned.Count > 0)
					.Select(ai => ai.ActiveAttackSequence)
					.ToList();

				if (activeSequences.Count > 0)
				{
					foreach (var e in EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>())
					{
						var p = e.GetComponent<EnemyAttackProgress>();
						if (p != null && p.ActualDamage > 0 && activeSequences.Contains(p.AttackSequence))
						{
							totalIncoming += p.ActualDamage;
						}
					}
				}
				if (totalIncoming > 0)
				{
					float dmgPct = MathHelper.Clamp(totalIncoming / (float)hp.Max, 0f, 1f);
					int dmgPixels = (int)Math.Round(width * dmgPct);
					int overlayW = Math.Max(0, Math.Min(fillW, dmgPixels));
					if (overlayW > 0)
					{
						int overlayX = x + Math.Max(0, fillW - overlayW);
						var overlayRect = new Rectangle(overlayX, y, overlayW, height);
						float phase = (float)Math.Sin(MathHelper.TwoPi * Math.Max(0.01f, IncomingDamageFlashHz) * _accumSeconds);
						float norm = 0.5f * (phase + 1f);
						float a = MathHelper.Lerp(MathHelper.Clamp(IncomingDamageAlphaMin, 0f, 1f), MathHelper.Clamp(IncomingDamageAlphaMax, 0f, 1f), norm);
						var dmgColor = Color.FromNonPremultiplied(255, 255, 255, (int)Math.Round(a * 255f));
						int srcW2 = Math.Max(1, overlayW);
						bool atRightCap = (fillW >= width);
						if (atRightCap)
						{
							// Align source to the right edge so the overlay inherits the bar's rounded cap
							int srcX = Math.Max(0, width - srcW2);
							var src2 = new Rectangle(srcX, 0, srcW2, height);
							_spriteBatch.Draw(_roundedFill, overlayRect, src2, dmgColor);
						}
						else
						{
							// Inside the bar (not reaching the right cap): draw a flat rectangle to avoid inner rounding
							_spriteBatch.Draw(_pixel, overlayRect, dmgColor);
						}
					}
				}
			}

			// Centered HP text: "current/max"
				if (_font != null)
				{
					string hpText = $"{Math.Max(0, hp.Current)}/{Math.Max(0, hp.Max)}";
					var textSize = _font.MeasureString(hpText);
					float scale = Math.Min(1f, Math.Min(width / Math.Max(1f, textSize.X), height / Math.Max(1f, textSize.Y)));
					var textPos = new Vector2(x + width / 2f - (textSize.X * scale) / 2f, y + height / 2f - (textSize.Y * scale) / 2f);
					_spriteBatch.DrawString(_font, hpText, textPos, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				}
			}
		}

		private static Texture2D CreateRoundedHighlightTexture(GraphicsDevice gd, int width, int height, int radius)
		{
			var tex = new Texture2D(gd, width, height, false, SurfaceFormat.Color);
			var data = new Color[width * height];
			int r = Math.Max(0, radius);
			int r2 = r * r;
			int w = width;
			int h = height;
			for (int y = 0; y < h; y++)
			{
				float v = h > 1 ? (y / (float)(h - 1)) : 0.5f; // 0..1
				float t = 2f * (v - 0.5f);
				float parabola = Math.Max(0f, 1f - (t * t)); // 1 at center, 0 at edges
				float maxAlpha = 0.35f; // intensity
				byte a = (byte)Math.Round(MathHelper.Clamp(parabola * maxAlpha, 0f, 1f) * 255f);
				for (int x = 0; x < w; x++)
				{
					bool inside = true;
					if (x < r && y < r)
					{
						int dx = r - x - 1;
						int dy = r - y - 1;
						inside = (dx * dx + dy * dy) <= r2;
					}
					else if (x >= w - r && y < r)
					{
						int dx = x - (w - r);
						int dy = r - y - 1;
						inside = (dx * dx + dy * dy) <= r2;
					}
					else if (x < r && y >= h - r)
					{
						int dx = r - x - 1;
						int dy = y - (h - r);
						inside = (dx * dx + dy * dy) <= r2;
					}
					else if (x >= w - r && y >= h - r)
					{
						int dx = x - (w - r);
						int dy = y - (h - r);
						inside = (dx * dx + dy * dy) <= r2;
					}

					data[y * w + x] = inside ? Color.FromNonPremultiplied(255, 255, 255, a) : Color.Transparent;
				}
			}
			tex.SetData(data);
			return tex;
		}
	}
}
