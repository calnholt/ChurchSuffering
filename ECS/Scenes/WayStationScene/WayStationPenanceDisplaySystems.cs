using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.RunSetup;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	internal static class WayStationPenanceDraw
	{
		public static readonly Color Ink = Hex("050606");
		public static readonly Color Charcoal = Hex("101112");
		public static readonly Color Iron = Hex("242628");
		public static readonly Color Bone = Hex("EEE9DF");
		public static readonly Color DimBone = Hex("A8A399");
		public static readonly Color Ash = Hex("6F6B64");
		public static readonly Color Blood = Hex("C41E3A");
		public static readonly Color DimBlood = Hex("7C1226");
		public static readonly Color DarkBlood = Hex("3A0812");
		public static readonly Color Gold = Hex("E8C45D");

		public static bool ShouldDraw(EntityManager entityManager)
		{
			var scene = entityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>()?.Current;
			var state = entityManager.GetEntity(WayStationSceneConstants.ModalRootName)?.GetComponent<WayStationPenanceModalState>();
			return scene is SceneId.WayStation or SceneId.Snapshot
				&& state?.Phase != WayStationPenanceModalPhase.Hidden;
		}

		public static Rectangle Animate(Rectangle bounds, WayStationPenanceMotion motion)
		{
			if (motion == null) return bounds;
			var center = bounds.Center.ToVector2() + motion.Offset;
			int width = Math.Max(1, (int)MathF.Round(bounds.Width * motion.Scale));
			int height = Math.Max(1, (int)MathF.Round(bounds.Height * motion.Scale));
			return new Rectangle((int)MathF.Round(center.X - width / 2f), (int)MathF.Round(center.Y - height / 2f), width, height);
		}

		public static void Border(SpriteBatch batch, Texture2D pixel, Rectangle rect, Color color, int thickness = 1)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			thickness = Math.Max(1, thickness);
			batch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			batch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			batch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			batch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		public static Rectangle Expanded(Rectangle rect, int horizontal, int vertical)
		{
			return new Rectangle(rect.X - horizontal, rect.Y - vertical, rect.Width + horizontal * 2, rect.Height + vertical * 2);
		}

		public static void CenterText(SpriteBatch batch, SpriteFont font, string text, Rectangle rect, float scale, Color color)
		{
			Vector2 size = font.MeasureString(text ?? string.Empty) * scale;
			batch.DrawString(font, text ?? string.Empty, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		public static void Fit(SpriteBatch batch, Texture2D texture, Rectangle bounds, Color tint)
		{
			if (texture == null || bounds.Width <= 0 || bounds.Height <= 0) return;
			float scale = Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
			int width = Math.Max(1, (int)MathF.Round(texture.Width * scale));
			int height = Math.Max(1, (int)MathF.Round(texture.Height * scale));
			batch.Draw(texture, new Rectangle(bounds.Center.X - width / 2, bounds.Center.Y - height / 2, width, height), tint);
		}

		private static Color Hex(string value)
		{
			return new Color(Convert.ToByte(value[..2], 16), Convert.ToByte(value.Substring(2, 2), 16), Convert.ToByte(value.Substring(4, 2), 16));
		}
	}

	[DebugTab("WayStation Penance Masthead")]
	public sealed class WayStationPenanceMastheadDisplaySystem : Core.System
	{
		private readonly SpriteBatch _batch;
		private readonly Texture2D _pixel;
		private readonly SpriteFont _title = FontSingleton.TitleFont;
		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.05f, Max = 1f)] public float TitleScale { get; set; } = 0.39f;
		[DebugEditable(DisplayName = "Title Shadow Y", Step = 1, Min = 0, Max = 20)] public int TitleShadowY { get; set; } = 4;
		[DebugEditable(DisplayName = "Rule Width", Step = 1, Min = 20, Max = 240)] public int RuleWidth { get; set; } = 90;
		[DebugEditable(DisplayName = "Rule Gap", Step = 1, Min = 0, Max = 40)] public int RuleGap { get; set; } = 9;
		[DebugEditable(DisplayName = "Diamond Size", Step = 1, Min = 2, Max = 20)] public int DiamondSize { get; set; } = 7;

		public WayStationPenanceMastheadDisplaySystem(EntityManager entityManager, SpriteBatch batch, ImageAssetService assets) : base(entityManager)
		{
			_batch = batch;
			_pixel = assets.GetPixel(Color.White);
		}
		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<WayStationPenanceMastheadPresentation>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
		public void Draw()
		{
			if (!WayStationPenanceDraw.ShouldDraw(EntityManager)) return;
			var entity = GetRelevantEntities().FirstOrDefault();
			var presentation = entity?.GetComponent<WayStationPenanceMastheadPresentation>();
			var motion = entity?.GetComponent<WayStationPenanceMotion>();
			if (presentation == null || motion == null) return;
			Rectangle bounds = WayStationPenanceDraw.Animate(presentation.Bounds, motion);
			float alpha = MathHelper.Clamp(motion.Opacity, 0f, 1f);
			string title = "Begin the Climb";
			Vector2 size = _title.MeasureString(title) * TitleScale * motion.Scale;
			var position = new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f - 8f);
			_batch.DrawString(_title, title, position + new Vector2(0, TitleShadowY), Color.Black * (0.9f * alpha), 0f, Vector2.Zero, TitleScale * motion.Scale, SpriteEffects.None, 0f);
			_batch.DrawString(_title, title, position, Color.White * alpha, 0f, Vector2.Zero, TitleScale * motion.Scale, SpriteEffects.None, 0f);

			var ruleMotion = EntityManager.GetEntity(WayStationSceneConstants.MastheadRuleName)?.GetComponent<WayStationPenanceMotion>();
			float ruleAlpha = MathHelper.Clamp(ruleMotion?.Opacity ?? 1f, 0f, 1f);
			int width = (int)MathF.Round(RuleWidth * MathHelper.Clamp(ruleMotion?.WidthProgress ?? 1f, 0f, 1f));
			int y = bounds.Center.Y + 38;
			for (int i = 0; i < width; i++)
			{
				float a = i / (float)Math.Max(1, width - 1);
				_batch.Draw(_pixel, new Rectangle(bounds.Center.X - RuleGap - DiamondSize / 2 - width + i, y, 1, 1), WayStationPenanceDraw.Blood * (a * ruleAlpha));
				_batch.Draw(_pixel, new Rectangle(bounds.Center.X + RuleGap + DiamondSize / 2 + i, y, 1, 1), WayStationPenanceDraw.Blood * ((1f - a) * ruleAlpha));
			}
			_batch.Draw(_pixel, bounds.Center.ToVector2() + new Vector2(0, 38), null, WayStationPenanceDraw.Blood * ruleAlpha, MathHelper.PiOver4, new Vector2(0.5f), new Vector2(DiamondSize), SpriteEffects.None, 0f);
		}
	}

	[DebugTab("WayStation Penance Weapons")]
	public sealed class WayStationPenanceWeaponDisplaySystem : Core.System
	{
		private readonly SpriteBatch _batch;
		private readonly Texture2D _pixel;
		private readonly Dictionary<StartingWeapon, Texture2D> _art;
		private readonly SpriteFont _title = FontSingleton.TitleFont;
		private readonly SpriteFont _body = FontSingleton.ChakraPetchFont;
		private readonly SpriteFont _grenze = FontSingleton.GrenzeFont;
		[DebugEditable(DisplayName = "Name Scale", Step = 0.01f, Min = 0.05f, Max = 0.5f)] public float NameScale { get; set; } = 0.20f;
		[DebugEditable(DisplayName = "Record Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)] public float RecordScale { get; set; } = 0.075f;
		[DebugEditable(DisplayName = "Record Value Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)] public float RecordValueScale { get; set; } = 0.11f;
		[DebugEditable(DisplayName = "Art Height", Step = 1, Min = 100, Max = 280)] public int ArtHeight { get; set; } = 216;
		[DebugEditable(DisplayName = "Art Top", Step = 1, Min = 0, Max = 60)] public int ArtTop { get; set; } = 14;
		[DebugEditable(DisplayName = "Glow Layers", Step = 1, Min = 1, Max = 16)] public int GlowLayers { get; set; } = 8;
		[DebugEditable(DisplayName = "Glow Radius", Step = 1, Min = 0, Max = 100)] public int GlowRadius { get; set; } = 36;
		[DebugEditable(DisplayName = "Hover Alpha", Step = 0.01f, Min = 0f, Max = 1f)] public float HoverAlpha { get; set; } = 0.7f;

		public WayStationPenanceWeaponDisplaySystem(EntityManager entityManager, SpriteBatch batch, ImageAssetService assets) : base(entityManager)
		{
			_batch = batch;
			_pixel = assets.GetPixel(Color.White);
			_art = new Dictionary<StartingWeapon, Texture2D>
			{
				[StartingWeapon.Sword] = assets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("sword")),
				[StartingWeapon.Dagger] = assets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("dagger")),
				[StartingWeapon.Hammer] = assets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("hammer")),
			};
		}
		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<WayStationPenanceWeaponPresentation>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
		public void Draw()
		{
			if (!WayStationPenanceDraw.ShouldDraw(EntityManager)) return;
			foreach (var entity in GetRelevantEntities().OrderBy(e => e.GetComponent<WayStationPenanceMotion>()?.Index ?? 0))
			{
				var presentation = entity.GetComponent<WayStationPenanceWeaponPresentation>();
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				var ui = entity.GetComponent<UIElement>();
				if (presentation?.IsUnlocked != true || motion == null || ui?.Bounds.IsEmpty != false) continue;
				Rectangle rect = WayStationPenanceDraw.Animate(ui.Bounds, motion);
				float alpha = MathHelper.Clamp(motion.Opacity, 0f, 1f);
				if (presentation.IsSelected)
				{
					float flash = 1f + motion.Glow;
					for (int i = GlowLayers; i >= 1; i--)
					{
						int inflate = (int)MathF.Round(GlowRadius * flash * i / Math.Max(1f, GlowLayers));
						_batch.Draw(_pixel, WayStationPenanceDraw.Expanded(rect, inflate, inflate), WayStationPenanceDraw.Blood * (0.018f * alpha));
					}
				}

				for (int y = 0; y < rect.Height; y++)
				{
					float t = y / (float)Math.Max(1, rect.Height - 1);
					Color fill = Color.Lerp(new Color(14, 14, 15), new Color(7, 7, 8), t) * (0.88f * alpha);
					if (presentation.IsSelected && t > 0.45f) fill = Color.Lerp(fill, WayStationPenanceDraw.DarkBlood, (t - 0.45f) / 0.55f * 0.55f);
					_batch.Draw(_pixel, new Rectangle(rect.X, rect.Y + y, rect.Width, 1), fill);
				}
				Color border = presentation.IsSelected ? WayStationPenanceDraw.Blood : ui.IsHovered ? Color.White * HoverAlpha : Color.White * 0.24f;
				WayStationPenanceDraw.Border(_batch, _pixel, rect, border * alpha, 1);
				if (presentation.IsSelected) WayStationPenanceDraw.Border(_batch, _pixel, WayStationPenanceDraw.Expanded(rect, 2, 2), WayStationPenanceDraw.Blood * (0.4f * alpha), 1);

				var artRect = new Rectangle(rect.X + 12, rect.Y + ArtTop, rect.Width - 24, Math.Min(ArtHeight, rect.Height - 100));
				WayStationPenanceDraw.Fit(_batch, _art[presentation.Weapon], artRect, Color.White * alpha);
				var nameRect = new Rectangle(rect.X, rect.Y + 237, rect.Width, 42);
				WayStationPenanceDraw.CenterText(_batch, _title, presentation.Weapon.ToString(), nameRect, NameScale, Color.White * alpha);
				string recordLabel = "BEST PENANCE";
				string recordValue = presentation.HighestUnlockedLevel == 0 ? "-" : WayStationClimbSettingsModalSystem.ToRoman(presentation.HighestUnlockedLevel);
				Vector2 labelSize = _body.MeasureString(recordLabel) * RecordScale;
				Vector2 valueSize = _grenze.MeasureString(recordValue) * RecordValueScale;
				float startX = rect.Center.X - (labelSize.X + 8 + valueSize.X) / 2f;
				_batch.DrawString(_body, recordLabel, new Vector2(startX, rect.Y + 292), WayStationPenanceDraw.DimBone * alpha, 0f, Vector2.Zero, RecordScale, SpriteEffects.None, 0f);
				_batch.DrawString(_grenze, recordValue, new Vector2(startX + labelSize.X + 8, rect.Y + 290), WayStationPenanceDraw.Gold * alpha, 0f, Vector2.Zero, RecordValueScale, SpriteEffects.None, 0f);
			}
		}
	}

	[DebugTab("WayStation Penance Track")]
	public sealed class WayStationPenanceTrackDisplaySystem : Core.System
	{
		private readonly SpriteBatch _batch;
		private readonly Texture2D _pixel;
		private readonly SpriteFont _body = FontSingleton.ChakraPetchFont;
		[DebugEditable(DisplayName = "Label Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)] public float LabelScale { get; set; } = 0.085f;
		[DebugEditable(DisplayName = "Frame Alpha", Step = 0.01f, Min = 0f, Max = 1f)] public float FrameAlpha { get; set; } = 0.6f;
		[DebugEditable(DisplayName = "Line Alpha", Step = 0.01f, Min = 0f, Max = 1f)] public float LineAlpha { get; set; } = 0.24f;
		[DebugEditable(DisplayName = "Fill Height", Step = 1, Min = 1, Max = 12)] public int FillHeight { get; set; } = 3;
		[DebugEditable(DisplayName = "Fill Glow", Step = 1, Min = 0, Max = 40)] public int FillGlow { get; set; } = 14;

		public WayStationPenanceTrackDisplaySystem(EntityManager entityManager, SpriteBatch batch, ImageAssetService assets) : base(entityManager) { _batch = batch; _pixel = assets.GetPixel(Color.White); }
		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<WayStationPenanceTrackPresentation>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
		public void Draw()
		{
			if (!WayStationPenanceDraw.ShouldDraw(EntityManager)) return;
			var entity = GetRelevantEntities().FirstOrDefault();
			var track = entity?.GetComponent<WayStationPenanceTrackPresentation>();
			var motion = entity?.GetComponent<WayStationPenanceMotion>();
			if (track == null || motion == null) return;
			float alpha = MathHelper.Clamp(motion.Opacity, 0f, 1f);
			var labelMotion = EntityManager.GetEntity(WayStationSceneConstants.TrackLabelName)?.GetComponent<WayStationPenanceMotion>();
			var labelRect = WayStationPenanceDraw.Animate(track.LabelBounds, labelMotion);
			float labelAlpha = MathHelper.Clamp(labelMotion?.Opacity ?? 1f, 0f, 1f);
			WayStationPenanceDraw.CenterText(_batch, _body, "THE STATIONS OF PENANCE", labelRect, LabelScale, Color.White * labelAlpha);
			int ruleY = labelRect.Center.Y;
			_batch.Draw(_pixel, new Rectangle(labelRect.X - 70, ruleY, 60, 1), WayStationPenanceDraw.Blood * (0.55f * labelAlpha));
			_batch.Draw(_pixel, new Rectangle(labelRect.Right + 10, ruleY, 60, 1), WayStationPenanceDraw.Blood * (0.55f * labelAlpha));

			int frameWidth = Math.Max(1, (int)MathF.Round(track.FrameBounds.Width * MathHelper.Clamp(motion.WidthProgress, 0f, 1f)));
			var frame = new Rectangle(track.FrameBounds.Center.X - frameWidth / 2, track.FrameBounds.Y + (int)motion.Offset.Y, frameWidth, track.FrameBounds.Height);
			_batch.Draw(_pixel, frame, new Color(6, 6, 7) * (FrameAlpha * alpha));
			WayStationPenanceDraw.Border(_batch, _pixel, frame, Color.White * (0.12f * alpha));
			_batch.Draw(_pixel, new Rectangle(track.FrameBounds.X + 40, track.FrameBounds.Center.Y, track.FrameBounds.Width - 80, 1), Color.White * (LineAlpha * alpha));

			var fillMotion = EntityManager.GetEntity(WayStationSceneConstants.TrackFillName)?.GetComponent<WayStationPenanceMotion>();
			float targetWidth = track.FillWidth;
			if (fillMotion?.AnimatedValue >= 0f)
			{
				var nodes = EntityManager.GetEntitiesWithComponent<WayStationPenanceNodePresentation>()
					.OrderBy(node => node.GetComponent<WayStationPenanceNodePresentation>().Level)
					.Take(2)
					.Select(node => node.GetComponent<UIElement>()?.Bounds.Center.X ?? 0)
					.ToArray();
				int pitch = nodes.Length == 2 ? Math.Max(1, nodes[1] - nodes[0]) : 47;
				targetWidth = Math.Max(0f, fillMotion.AnimatedValue - 1f) * pitch;
			}
			int fillWidth = Math.Max(0, (int)MathF.Round(targetWidth * MathHelper.Clamp(fillMotion?.WidthProgress ?? 1f, 0f, 1f)));
			if (fillWidth <= 0) return;
			int fillX = track.FrameBounds.X + 34 + 15;
			int fillY = track.FrameBounds.Center.Y - FillHeight / 2;
			_batch.Draw(_pixel, new Rectangle(fillX, fillY - FillGlow / 2, fillWidth, FillHeight + FillGlow), WayStationPenanceDraw.Blood * (0.12f * alpha));
			for (int x = 0; x < fillWidth; x++)
			{
				Color color = Color.Lerp(WayStationPenanceDraw.DimBlood, WayStationPenanceDraw.Blood, x / (float)Math.Max(1, fillWidth - 1));
				_batch.Draw(_pixel, new Rectangle(fillX + x, fillY, 1, FillHeight), color * alpha);
			}
		}
	}

	[DebugTab("WayStation Penance Nodes")]
	public sealed class WayStationPenanceNodeDisplaySystem : Core.System
	{
		private readonly SpriteBatch _batch;
		private readonly Texture2D _pixel;
		private readonly SpriteFont _grenze = FontSingleton.GrenzeFont;
		private readonly SpriteFont _body = FontSingleton.ChakraPetchFont;
		[DebugEditable(DisplayName = "Number Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)] public float NumberScale { get; set; } = 0.10f;
		[DebugEditable(DisplayName = "Tick Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)] public float TickScale { get; set; } = 0.065f;
		[DebugEditable(DisplayName = "Hover Scale", Step = 0.01f, Min = 1f, Max = 2f)] public float HoverScale { get; set; } = 1.2f;
		[DebugEditable(DisplayName = "Current Scale", Step = 0.01f, Min = 1f, Max = 2f)] public float CurrentScale { get; set; } = 1.24f;
		[DebugEditable(DisplayName = "Glow Radius", Step = 1, Min = 0, Max = 50)] public int GlowRadius { get; set; } = 20;

		public WayStationPenanceNodeDisplaySystem(EntityManager entityManager, SpriteBatch batch, ImageAssetService assets) : base(entityManager) { _batch = batch; _pixel = assets.GetPixel(Color.White); }
		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<WayStationPenanceNodePresentation>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
		public void Draw()
		{
			if (!WayStationPenanceDraw.ShouldDraw(EntityManager)) return;
			foreach (var entity in GetRelevantEntities().OrderBy(e => e.GetComponent<WayStationPenanceNodePresentation>()?.Level ?? 0))
			{
				var node = entity.GetComponent<WayStationPenanceNodePresentation>();
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				var ui = entity.GetComponent<UIElement>();
				if (node == null || motion == null || ui == null) continue;
				float visualScale = motion.Scale * (node.IsCurrent ? CurrentScale : ui.IsHovered && node.IsUnlocked ? HoverScale : 1f);
				var rect = WayStationPenanceDraw.Animate(ui.Bounds, new WayStationPenanceMotion { Scale = visualScale, Offset = motion.Offset });
				float alpha = MathHelper.Clamp(motion.Opacity, 0f, 1f);
				if (node.IsCurrent)
				{
					_batch.Draw(_pixel, rect.Center.ToVector2(), null, WayStationPenanceDraw.Blood * (0.18f * alpha), MathHelper.PiOver4, new Vector2(0.5f), new Vector2(rect.Width + GlowRadius), SpriteEffects.None, 0f);
					_batch.Draw(_pixel, rect.Center.ToVector2(), null, WayStationPenanceDraw.Blood * (0.3f * alpha), MathHelper.PiOver4, new Vector2(0.5f), new Vector2(rect.Width + 8), SpriteEffects.None, 0f);
				}
				Color border = node.IsActive ? WayStationPenanceDraw.Blood : node.IsUnlocked ? Color.White * 0.42f : Color.White * 0.13f;
				Color fill = node.IsActive ? WayStationPenanceDraw.Blood : node.IsUnlocked ? new Color(18, 18, 20) : new Color(10, 10, 11);
				_batch.Draw(_pixel, rect.Center.ToVector2(), null, border * alpha, MathHelper.PiOver4, new Vector2(0.5f), new Vector2(rect.Width), SpriteEffects.None, 0f);
				_batch.Draw(_pixel, rect.Center.ToVector2(), null, fill * alpha, MathHelper.PiOver4, new Vector2(0.5f), new Vector2(Math.Max(1, rect.Width - 3)), SpriteEffects.None, 0f);
				WayStationPenanceDraw.CenterText(_batch, _grenze, node.Level.ToString(), rect, NumberScale * visualScale, Color.White * (alpha * (node.IsUnlocked ? 0.88f : 0.26f)));
				if (node.Level % 6 == 0)
				{
					var tick = new Rectangle(ui.Bounds.Center.X - 24, ui.Bounds.Bottom + 15, 48, 14);
					WayStationPenanceDraw.CenterText(_batch, _body, WayStationClimbSettingsModalSystem.ToRoman(node.Level), tick, TickScale, WayStationPenanceDraw.Ash * alpha);
				}
			}
		}
	}

	[DebugTab("WayStation Penance Tally")]
	public sealed class WayStationPenanceTallyDisplaySystem : Core.System
	{
		private readonly SpriteBatch _batch;
		private readonly ImageAssetService _assets;
		private readonly SpriteFont _body = FontSingleton.ChakraPetchFont;
		private readonly SpriteFont _grenze = FontSingleton.GrenzeFont;
		[DebugEditable(DisplayName = "Name Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)] public float NameScale { get; set; } = 0.085f;
		[DebugEditable(DisplayName = "Count Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)] public float CountScale { get; set; } = 0.11f;
		[DebugEditable(DisplayName = "Empty Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)] public float EmptyScale { get; set; } = 0.085f;
		[DebugEditable(DisplayName = "Chip Radius", Step = 1, Min = 0, Max = 40)] public int ChipRadius { get; set; } = 17;
		[DebugEditable(DisplayName = "Fill Alpha", Step = 0.01f, Min = 0f, Max = 1f)] public float FillAlpha { get; set; } = 0.78f;

		public WayStationPenanceTallyDisplaySystem(EntityManager entityManager, SpriteBatch batch, ImageAssetService assets) : base(entityManager) { _batch = batch; _assets = assets; }
		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<WayStationPenanceTallyPresentation>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
		public void Draw()
		{
			if (!WayStationPenanceDraw.ShouldDraw(EntityManager)) return;
			bool any = false;
			foreach (var entity in GetRelevantEntities().OrderBy(e => e.GetComponent<WayStationPenanceMotion>()?.Index ?? 0))
			{
				var tally = entity.GetComponent<WayStationPenanceTallyPresentation>();
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				if (tally == null || motion == null || tally.Bounds.Width <= 1 || motion.Opacity <= 0.001f) continue;
				any = true;
				var rect = WayStationPenanceDraw.Animate(tally.Bounds, motion);
				float alpha = MathHelper.Clamp(motion.Opacity, 0f, 1f);
				_batch.Draw(_assets.GetRoundedRect(rect.Width, rect.Height, Math.Min(ChipRadius, rect.Height / 2)), rect, new Color(30, 5, 10) * (FillAlpha * alpha));
				var inner = WayStationPenanceDraw.Expanded(rect, -1, -1);
				if (inner.Width > 0 && inner.Height > 0)
					_batch.Draw(_assets.GetRoundedRect(inner.Width, inner.Height, Math.Min(ChipRadius, inner.Height / 2)), inner, WayStationPenanceDraw.DarkBlood * (0.25f * alpha));
				string name = WayStationClimbSettingsModalSystem.DisplayName(tally.Type);
				string count = $"x{Math.Max(tally.CurrentCount, tally.DisplayedCount)}";
				Vector2 nameSize = _body.MeasureString(name) * NameScale;
				Vector2 countSize = _grenze.MeasureString(count) * CountScale;
				float x = rect.Center.X - (nameSize.X + 6 + countSize.X) / 2f;
				_batch.DrawString(_body, name, new Vector2(x, rect.Center.Y - nameSize.Y / 2f), WayStationPenanceDraw.Bone * alpha, 0f, Vector2.Zero, NameScale, SpriteEffects.None, 0f);
				_batch.DrawString(_grenze, count, new Vector2(x + nameSize.X + 6, rect.Center.Y - countSize.Y / 2f), Color.Lerp(Color.White, new Color(255, 139, 160), motion.Glow) * alpha, 0f, Vector2.Zero, CountScale, SpriteEffects.None, 0f);
			}

			if (!any)
			{
				var track = EntityManager.GetEntitiesWithComponent<WayStationPenanceTrackPresentation>().FirstOrDefault()?.GetComponent<WayStationPenanceTrackPresentation>();
				if (track != null)
				{
					var empty = new Rectangle(track.FrameBounds.Center.X - 300, track.FrameBounds.Bottom + 30, 600, 34);
					WayStationPenanceDraw.CenterText(_batch, _body, "No penance borne - an unburdened climb.", empty, EmptyScale, WayStationPenanceDraw.DimBone);
				}
			}
		}
	}

	[DebugTab("WayStation Penance Footer")]
	public sealed class WayStationPenanceFooterDisplaySystem : Core.System
	{
		private readonly SpriteBatch _batch;
		private readonly Texture2D _pixel;
		private readonly SpriteFont _title = FontSingleton.TitleFont;
		private readonly SpriteFont _body = FontSingleton.ChakraPetchFont;
		[DebugEditable(DisplayName = "Depart Scale", Step = 0.01f, Min = 0.05f, Max = 0.6f)] public float DepartScale { get; set; } = 0.21f;
		[DebugEditable(DisplayName = "Summary Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)] public float SummaryScale { get; set; } = 0.075f;
		[DebugEditable(DisplayName = "Close Scale", Step = 0.01f, Min = 0.05f, Max = 0.5f)] public float CloseScale { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Button Shadow", Step = 1, Min = 0, Max = 40)] public int ButtonShadow { get; set; } = 12;
		[DebugEditable(DisplayName = "Hover Glow", Step = 1, Min = 0, Max = 60)] public int HoverGlow { get; set; } = 34;

		public WayStationPenanceFooterDisplaySystem(EntityManager entityManager, SpriteBatch batch, ImageAssetService assets) : base(entityManager) { _batch = batch; _pixel = assets.GetPixel(Color.White); }
		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<WayStationPenanceFooterPresentation>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
		public void Draw()
		{
			if (!WayStationPenanceDraw.ShouldDraw(EntityManager)) return;
			var entity = GetRelevantEntities().FirstOrDefault();
			var footer = entity?.GetComponent<WayStationPenanceFooterPresentation>();
			var motion = entity?.GetComponent<WayStationPenanceMotion>();
			if (footer == null || motion == null) return;
			float alpha = MathHelper.Clamp(motion.Opacity, 0f, 1f);
			var depart = WayStationPenanceDraw.Animate(footer.DepartBounds, motion);
			bool hovered = EntityManager.GetEntity(WayStationSceneConstants.DepartButtonName)?.GetComponent<UIElement>()?.IsHovered == true;
			if (hovered) _batch.Draw(_pixel, WayStationPenanceDraw.Expanded(depart, HoverGlow, HoverGlow), WayStationPenanceDraw.Blood * (0.08f * alpha));
			_batch.Draw(_pixel, new Rectangle(depart.X, depart.Y + ButtonShadow, depart.Width, depart.Height), Color.Black * (0.7f * alpha));
			for (int y = 0; y < depart.Height; y++)
			{
				float t = y / (float)Math.Max(1, depart.Height - 1);
				Color top = hovered ? new Color(84, 12, 24) : new Color(24, 24, 26);
				Color bottom = hovered ? new Color(26, 4, 8) : new Color(8, 8, 9);
				_batch.Draw(_pixel, new Rectangle(depart.X, depart.Y + y, depart.Width, 1), Color.Lerp(top, bottom, t) * (0.92f * alpha));
			}
			WayStationPenanceDraw.Border(_batch, _pixel, depart, (hovered ? WayStationPenanceDraw.Blood : Color.White * 0.55f) * alpha);
			_batch.Draw(_pixel, new Rectangle(depart.X + 16, depart.Center.Y, 34, 1), WayStationPenanceDraw.Blood * (0.9f * alpha));
			_batch.Draw(_pixel, new Rectangle(depart.Right - 50, depart.Center.Y, 34, 1), WayStationPenanceDraw.Blood * (0.9f * alpha));
			WayStationPenanceDraw.CenterText(_batch, _title, "Depart", depart, DepartScale, Color.White * alpha);
			WayStationPenanceDraw.CenterText(_batch, _body, footer.Summary, WayStationPenanceDraw.Animate(footer.SummaryBounds, motion), SummaryScale, WayStationPenanceDraw.DimBone * alpha);

			var closeMotion = EntityManager.GetEntity(WayStationSceneConstants.CloseButtonName)?.GetComponent<WayStationPenanceMotion>();
			var close = WayStationPenanceDraw.Animate(footer.CloseBounds, closeMotion);
			float closeAlpha = MathHelper.Clamp(closeMotion?.Opacity ?? 1f, 0f, 1f);
			bool closeHover = EntityManager.GetEntity(WayStationSceneConstants.CloseButtonName)?.GetComponent<UIElement>()?.IsHovered == true;
			_batch.Draw(_pixel, close, (closeHover ? WayStationPenanceDraw.DarkBlood : new Color(6, 6, 6)) * (0.68f * closeAlpha));
			WayStationPenanceDraw.Border(_batch, _pixel, close, (closeHover ? WayStationPenanceDraw.Blood : Color.White * 0.4f) * closeAlpha);
			WayStationPenanceDraw.CenterText(_batch, _title, "X", close, CloseScale, Color.White * closeAlpha);
		}
	}
}
