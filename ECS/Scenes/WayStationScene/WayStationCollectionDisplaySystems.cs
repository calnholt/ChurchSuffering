using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using ChurchSuffering.ECS.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static ChurchSuffering.ECS.Components.CardData;

namespace ChurchSuffering.ECS.Systems
{
	public abstract class WayStationCollectionDisplaySystemBase : Core.System
	{
		protected readonly GraphicsDevice GraphicsDevice;
		protected readonly SpriteBatch SpriteBatch;
		protected readonly ImageAssetService ImageAssets;
		protected readonly Texture2D Pixel;
		protected readonly SpriteFont TitleFont = FontSingleton.TitleFont;
		protected readonly SpriteFont BodyFont = FontSingleton.ChakraPetchFont;
		protected readonly SpriteFont ItalicFont = FontSingleton.ChakraPetchBoldItalicFont;
		protected readonly RasterizerState ScissorRasterizer =
			new() { ScissorTestEnable = true, CullMode = CullMode.None };

		protected WayStationCollectionDisplaySystemBase(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			GraphicsDevice = graphicsDevice;
			SpriteBatch = spriteBatch;
			ImageAssets = imageAssets;
			Pixel = imageAssets.GetPixel(Color.White);
		}

		protected override IEnumerable<Entity> GetRelevantEntities() =>
			EntityManager.GetEntitiesWithComponent<WayStationCollectionModalRoot>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		protected bool TryGetView(
			out Entity root,
			out WayStationCollectionModalState state,
			out WayStationCollectionModalLayout layout,
			out WayStationCollectionCatalog catalog,
			out ModalAnimationRenderState render)
		{
			root = EntityManager.GetEntity(WayStationSceneConstants.CollectionModalRootName);
			state = root?.GetComponent<WayStationCollectionModalState>();
			layout = root?.GetComponent<WayStationCollectionModalLayout>();
			catalog = root?.GetComponent<WayStationCollectionCatalogComponent>()?.Catalog;
			var animation = root?.GetComponent<ModalAnimation>();
			render = layout == null
				? default
				: ModalAnimationRenderState.From(animation, layout.Shell);
			return root != null && state != null && layout != null && catalog != null && render.ShouldDraw;
		}

		protected void DrawRounded(Rectangle rect, int radius, Color color)
		{
			if (rect.Width <= 0 || rect.Height <= 0 || color.A == 0) return;
			SpriteBatch.Draw(ImageAssets.GetRoundedRect(rect.Width, rect.Height, radius), rect, color);
		}

		protected void DrawRoundedBorder(Rectangle rect, int radius, int thickness, Color color)
		{
			DrawRounded(rect, radius, color);
			var inner = new Rectangle(
				rect.X + thickness,
				rect.Y + thickness,
				Math.Max(1, rect.Width - thickness * 2),
				Math.Max(1, rect.Height - thickness * 2));
			DrawRounded(inner, Math.Max(0, radius - thickness), Color.Black);
		}

		protected void DrawBorder(Rectangle rect, Color color, int thickness = 1)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			SpriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			SpriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			SpriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			SpriteBatch.Draw(Pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		protected void DrawCentered(SpriteFont font, string text, Rectangle bounds, Color color, float scale)
		{
			if (font == null || string.IsNullOrEmpty(text)) return;
			string safe = TextUtils.FilterUnsupportedGlyphs(font, text);
			Vector2 size = font.MeasureString(safe) * scale;
			SpriteBatch.DrawString(
				font,
				safe,
				new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f),
				color,
				0f,
				Vector2.Zero,
				scale,
				SpriteEffects.None,
				0f);
		}

		protected int DrawWrapped(
			SpriteFont font,
			string text,
			Rectangle bounds,
			Color color,
			float scale,
			int lineGap = 2)
		{
			if (font == null || string.IsNullOrWhiteSpace(text)) return 0;
			int y = bounds.Y;
			foreach (string line in TextUtils.WrapText(font, text, scale, Math.Max(1, bounds.Width)))
			{
				string safe = TextUtils.FilterUnsupportedGlyphs(font, line);
				SpriteBatch.DrawString(font, safe, new Vector2(bounds.X, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				y += (int)Math.Ceiling(font.LineSpacing * scale) + lineGap;
			}
			return y - bounds.Y;
		}

		protected void WithClip(Rectangle logicalClip, Action draw)
		{
			Rectangle prior = GraphicsDevice.ScissorRectangle;
			SpriteBatch.End();
			SpriteBatch.Begin(
				SpriteSortMode.Immediate,
				BlendState.AlphaBlend,
				SamplerState.AnisotropicClamp,
				DepthStencilState.None,
				ScissorRasterizer,
				null,
				Game1.Display.SpriteBatchTransform);
			GraphicsDevice.ScissorRectangle = Game1.Display.LogicalToRender(
				Rectangle.Intersect(logicalClip, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight)));
			try { draw(); }
			finally
			{
				SpriteBatch.End();
				GraphicsDevice.ScissorRectangle = prior;
				SpriteBatch.Begin(
					SpriteSortMode.Immediate,
					BlendState.AlphaBlend,
					SamplerState.AnisotropicClamp,
					DepthStencilState.None,
					ScissorRasterizer,
					null,
					Game1.Display.SpriteBatchTransform);
			}
		}
	}

	[DebugTab("WayStation Collection Chrome")]
	public sealed class WayStationCollectionChromeDisplaySystem : WayStationCollectionDisplaySystemBase
	{
		[DebugEditable(DisplayName = "Dim Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float DimAlpha { get; set; } = 0.72f;

		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.1f, Max = 0.8f)]
		public float TitleScale { get; set; } = 0.36f;

		[DebugEditable(DisplayName = "Subtitle Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)]
		public float SubtitleScale { get; set; } = 0.09f;

		public WayStationCollectionChromeDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager, graphicsDevice, spriteBatch, imageAssets) { }

		public void Draw()
		{
			if (!TryGetView(out var root, out var state, out var layout, out var catalog, out var render)) return;
			ModalOverlayChrome.DrawDim(
				SpriteBatch,
				Pixel,
				Game1.VirtualWidth,
				Game1.VirtualHeight,
				(int)Math.Round(255 * DimAlpha * render.DimAlphaMultiplier));

			Rectangle shell = render.Transform(layout.Shell);
			float alpha = render.ShellAlpha;
			for (int i = 5; i >= 1; i--)
			{
				var shadow = new Rectangle(
					shell.X - i * 4,
					shell.Y + 18 + i * 5,
					shell.Width + i * 8,
					shell.Height + i * 3);
				DrawRounded(shadow, 5, Color.Black * (0.07f * i * alpha));
			}
			DrawVerticalGradient(shell, new Color(16, 15, 14) * (0.97f * alpha), new Color(8, 8, 8) * (0.97f * alpha));
			DrawBorder(shell, Color.White * (0.50f * alpha), Math.Max(1, (int)Math.Round(2 * render.ShellScale)));
			var outer = new Rectangle(shell.X - 5, shell.Y - 5, shell.Width + 10, shell.Height + 10);
			DrawBorder(outer, Color.White * (0.15f * alpha));

			Rectangle header = render.Transform(layout.Header);
			var titleBounds = new Rectangle(header.X, header.Y + 17, header.Width, 54);
			DrawCentered(TitleFont, "The Crusader's Reliquary", titleBounds, Color.White * alpha, render.TransformScale(TitleScale));
			DrawCentered(BodyFont, "WAYSTATION COLLECTION", new Rectangle(header.X, header.Y + 68, header.Width, 24), new Color(200, 192, 184) * alpha, render.TransformScale(SubtitleScale));

			DrawTabs(state, layout, catalog, render);
			var body = render.Transform(layout.Body);
			SpriteBatch.Draw(Pixel, body, Color.Black * (0.18f * alpha));
			SpriteBatch.Draw(Pixel, new Rectangle(body.X, body.Y, body.Width, 1), Color.White * (0.35f * alpha));

			var footer = render.Transform(layout.Footer);
			SpriteBatch.Draw(Pixel, footer, Color.Black * (0.30f * alpha));
			SpriteBatch.Draw(Pixel, new Rectangle(footer.X, footer.Y, footer.Width, 1), Color.White * (0.12f * alpha));
			DrawFooter(root, state, layout, catalog, render);
			DrawClose(layout, render);
		}

		private void DrawTabs(
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			WayStationCollectionCatalog catalog,
			ModalAnimationRenderState render)
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<WayStationCollectionTabPresentation>())
			{
				var tab = entity.GetComponent<WayStationCollectionTabPresentation>();
				var motion = entity.GetComponent<WayStationCollectionMotion>();
				bool active = tab.Tab == state.ActiveTab;
				float hover = motion?.Hover ?? 0f;
				var baseBounds = tab.Bounds;
				baseBounds.Y -= (int)Math.Round(3 * hover);
				Rectangle bounds = render.Transform(baseBounds);
				Color fill = active
					? Color.White * 0.18f
					: Color.White * (0.055f + hover * 0.05f);
				var mask = ImageAssets.GetRoundedRectPerCorner(bounds.Width, bounds.Height, 22, 22, 0, 0);
				if (active)
				{
					for (int i = 3; i >= 1; i--)
						SpriteBatch.Draw(mask, new Rectangle(bounds.X - i * 3, bounds.Y - i * 3, bounds.Width + i * 6, bounds.Height + i * 3), Color.White * (0.025f * i * render.ShellAlpha));
				}
				SpriteBatch.Draw(mask, bounds, fill * render.ShellAlpha);
				(int unlocked, int total) = tab.Tab switch
				{
					WayStationCollectionTab.Saints => (catalog.Saints.Count, catalog.SaintTotal),
					WayStationCollectionTab.Equipment => (catalog.Equipment.Count, catalog.EquipmentTotal),
					_ => (catalog.Cards.Count, catalog.CardTotal),
				};
				DrawCentered(TitleFont, tab.Tab.ToString(), new Rectangle(bounds.X, bounds.Y + 9, bounds.Width, 31), Color.White * render.ShellAlpha, render.TransformScale(0.16f));
				DrawCentered(BodyFont, $"{unlocked} / {total}", new Rectangle(bounds.X, bounds.Y + 41, bounds.Width, 20), new Color(200, 192, 184) * render.ShellAlpha, render.TransformScale(0.07f));
			}
		}

		private void DrawFooter(
			Entity root,
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			WayStationCollectionCatalog catalog,
			ModalAnimationRenderState render)
		{
			(string label, int unlocked, int total) = state.ActiveTab switch
			{
				WayStationCollectionTab.Saints => ("COMMUNION OF SAINTS", catalog.Saints.Count, catalog.SaintTotal),
				WayStationCollectionTab.Equipment => ("EQUIPMENT UNLOCKED", catalog.Equipment.Count, catalog.EquipmentTotal),
				_ => ("CARDS UNLOCKED", catalog.Cards.Count, catalog.CardTotal),
			};
			float alpha = render.ShellAlpha;
			var meter = render.Transform(layout.FooterMeter);
			DrawRounded(meter, meter.Height / 2, Color.White * (0.08f * alpha));
			float progress = root.GetComponent<WayStationCollectionMotion>()?.MeterProgress ?? 0f;
			var fill = new Rectangle(meter.X, meter.Y, Math.Max(0, (int)Math.Round(meter.Width * progress)), meter.Height);
			if (fill.Width > 0)
			{
				DrawRounded(fill, fill.Height / 2, new Color(139, 0, 0) * alpha);
				var bright = new Rectangle(fill.X + fill.Width / 2, fill.Y, fill.Width - fill.Width / 2, fill.Height);
				DrawRounded(bright, bright.Height / 2, new Color(180, 50, 50) * (0.75f * alpha));
			}
			var labelAnchor = render.Transform(layout.FooterLabelAnchor);
			string safe = TextUtils.FilterUnsupportedGlyphs(BodyFont, label);
			Vector2 labelSize = BodyFont.MeasureString(safe) * render.TransformScale(0.095f);
			SpriteBatch.DrawString(BodyFont, safe, new Vector2(labelAnchor.X, labelAnchor.Y - labelSize.Y / 2f), new Color(200, 192, 184) * alpha, 0f, Vector2.Zero, render.TransformScale(0.095f), SpriteEffects.None, 0f);
			var countAnchor = render.Transform(layout.FooterCountAnchor);
			string count = $"{unlocked} / {total}";
			Vector2 countSize = TitleFont.MeasureString(count) * render.TransformScale(0.15f);
			SpriteBatch.DrawString(TitleFont, count, new Vector2(countAnchor.X - countSize.X, countAnchor.Y - countSize.Y / 2f), Color.White * alpha, 0f, Vector2.Zero, render.TransformScale(0.15f), SpriteEffects.None, 0f);
		}

		private void DrawClose(WayStationCollectionModalLayout layout, ModalAnimationRenderState render)
		{
			var entity = EntityManager.GetEntity(WayStationSceneConstants.CollectionModalCloseButtonName);
			float hover = entity?.GetComponent<WayStationCollectionMotion>()?.Hover
				?? (entity?.GetComponent<UIElement>()?.IsHovered == true ? 1f : 0f);
			Rectangle bounds = render.Transform(layout.CloseButton);
			for (int i = 3; i >= 1 && hover > 0.01f; i--)
			{
				var glow = new Rectangle(bounds.X - i * 4, bounds.Y - i * 4, bounds.Width + i * 8, bounds.Height + i * 8);
				SpriteBatch.Draw(Pixel, glow, new Color(196, 30, 58) * (0.035f * i * hover * render.ShellAlpha));
			}
			SpriteBatch.Draw(Pixel, bounds, Color.Lerp(Color.Black * 0.55f, new Color(139, 0, 0), hover) * render.ShellAlpha);
			DrawBorder(bounds, Color.Lerp(Color.White * 0.5f, new Color(196, 30, 58), hover) * render.ShellAlpha, 2);
			DrawCentered(BodyFont, "X", bounds, Color.White * render.ShellAlpha, render.TransformScale(0.17f));
		}

		private void DrawVerticalGradient(Rectangle rect, Color top, Color bottom)
		{
			const int slices = 24;
			for (int i = 0; i < slices; i++)
			{
				int y0 = rect.Y + rect.Height * i / slices;
				int y1 = rect.Y + rect.Height * (i + 1) / slices;
				SpriteBatch.Draw(Pixel, new Rectangle(rect.X, y0, rect.Width, Math.Max(1, y1 - y0)), Color.Lerp(top, bottom, i / (float)(slices - 1)));
			}
			var highlight = new Rectangle(rect.Center.X - rect.Width / 3, rect.Y, rect.Width * 2 / 3, rect.Height / 4);
			SpriteBatch.Draw(Pixel, highlight, Color.White * 0.018f);
		}

	}

	[DebugTab("WayStation Collection Cards")]
	public sealed class WayStationCollectionCardsDisplaySystem : WayStationCollectionDisplaySystemBase
	{
		[DebugEditable(DisplayName = "Rest Fan Degrees", Step = 1f, Min = 0f, Max = 20f)]
		public float RestFanDegrees { get; set; } = 6f;

		[DebugEditable(DisplayName = "Hover Fan Degrees", Step = 1f, Min = 0f, Max = 25f)]
		public float HoverFanDegrees { get; set; } = 11f;

		public WayStationCollectionCardsDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager, graphicsDevice, spriteBatch, imageAssets) { }

		public void Draw()
		{
			if (!TryGetView(out _, out var state, out var layout, out var catalog, out var render)
				|| state.ActiveTab != WayStationCollectionTab.Cards) return;
			DrawFilters(state, layout, render);
			var visibleIds = new HashSet<string>(
				WayStationCollectionCatalogService.FilterCards(catalog, state.ActiveCardFilter).Select(item => item.Id),
				StringComparer.OrdinalIgnoreCase);
			if (visibleIds.Count == 0)
			{
				DrawCentered(BodyFont, "No unlocked cards match this filter.", render.Transform(layout.CardGridClip), new Color(160, 154, 148) * render.ShellAlpha, render.TransformScale(0.12f));
				return;
			}

			var stacks = EntityManager.GetEntitiesWithComponent<WayStationCollectionCardStackPresentation>()
				.Where(entity => visibleIds.Contains(entity.GetComponent<WayStationCollectionCardStackPresentation>().CardId))
				.OrderBy(entity => entity.GetComponent<WayStationCollectionMotion>()?.Hover ?? 0f)
				.ToArray();
			foreach (var entity in stacks)
				DrawStack(entity, layout, render);
		}

		private void DrawFilters(
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			ModalAnimationRenderState render)
		{
			var entities = EntityManager.GetEntitiesWithComponent<WayStationCollectionFilterPresentation>()
				.OrderBy(entity => entity.GetComponent<WayStationCollectionFilterPresentation>().Filter)
				.ToArray();
			if (entities.Length == 0) return;
			Rectangle first = render.Transform(entities[0].GetComponent<WayStationCollectionFilterPresentation>().Bounds);
			string label = "TYPE";
			Vector2 size = BodyFont.MeasureString(label) * render.TransformScale(0.075f);
			SpriteBatch.DrawString(BodyFont, label, new Vector2(first.X - size.X - 18, first.Center.Y - size.Y / 2f), new Color(155, 148, 140) * render.ShellAlpha, 0f, Vector2.Zero, render.TransformScale(0.075f), SpriteEffects.None, 0f);
			foreach (var entity in entities)
			{
				var filter = entity.GetComponent<WayStationCollectionFilterPresentation>();
				Rectangle bounds = render.Transform(filter.Bounds);
				bool active = filter.Filter == state.ActiveCardFilter;
				float hover = entity.GetComponent<WayStationCollectionMotion>()?.Hover ?? 0f;
				Color fill = active ? Color.White : Color.White * (0.06f + 0.04f * hover);
				Color text = active ? new Color(15, 14, 13) : new Color(220, 214, 206);
				DrawRounded(bounds, bounds.Height / 2, Color.White * ((active ? 0.75f : 0.25f + hover * 0.2f) * render.ShellAlpha));
				var inner = new Rectangle(bounds.X + 1, bounds.Y + 1, Math.Max(1, bounds.Width - 2), Math.Max(1, bounds.Height - 2));
				DrawRounded(inner, Math.Max(0, bounds.Height / 2 - 1), fill * render.ShellAlpha);
				DrawCentered(BodyFont, filter.Filter.ToString(), bounds, text * render.ShellAlpha, render.TransformScale(0.085f));
			}
		}

		private void DrawStack(
			Entity entity,
			WayStationCollectionModalLayout layout,
			ModalAnimationRenderState render)
		{
			var stack = entity.GetComponent<WayStationCollectionCardStackPresentation>();
			if (stack.Bounds == Rectangle.Empty) return;
			Rectangle conservative = stack.Bounds;
			conservative.Inflate(65, 40);
			if (!conservative.Intersects(layout.CardGridClip)) return;
			var motion = entity.GetComponent<WayStationCollectionMotion>();
			float fan = motion?.FanAngle ?? 0f;
			float angle = MathHelper.ToRadians(MathHelper.Lerp(RestFanDegrees, HoverFanDegrees, fan));
			var anchor = GetCardAnchor(stack.Bounds, layout.CardScale);
			var transformedAnchor = render.Transform(anchor);
			float scale = layout.CardScale * render.ShellScale;
			Rectangle clip = render.Transform(layout.CardGridClip);
			if (stack.IsWeapon)
			{
				Entity weapon = stack.ShowUpgradePreview
					? stack.UpgradedWhiteCard ?? stack.WhiteCard
					: stack.WhiteCard;
				DrawCard(weapon, transformedAnchor + new Vector2(0, -7 * fan * render.ShellScale), scale, render.ShellAlpha, 0f, clip);
				return;
			}
			if (fan > 0.01f)
			{
				Rectangle frontRect = render.Transform(stack.Bounds);
				var shadow = new Rectangle(frontRect.X + 10, frontRect.Y + 14, frontRect.Width, frontRect.Height);
				var visibleShadow = Rectangle.Intersect(shadow, clip);
				DrawRounded(visibleShadow, 12, Color.Black * (0.42f * fan * render.ShellAlpha));
			}

			var current = ResolveLayers(stack, stack.FrontColor, stack.ShowUpgradePreview);
			if (!stack.PendingFrontColor.HasValue)
			{
				DrawSettledStack(current, transformedAnchor, scale, angle, fan, clip, render);
				return;
			}

			var target = ResolveLayers(stack, stack.PendingFrontColor.Value, stack.ShowUpgradePreview);
			DrawSwitchingStack(
				current,
				target,
				stack.ColorSwitchProgress,
				transformedAnchor,
				scale,
				angle,
				fan,
				clip,
				render);
		}

		private void DrawSettledStack(
			(Entity Front, Entity Left, Entity Right) layers,
			Vector2 anchor,
			float scale,
			float angle,
			float fan,
			Rectangle clip,
			ModalAnimationRenderState render)
		{
			DrawCard(layers.Left, anchor + new Vector2(-12, 5) * render.ShellScale, scale, render.ShellAlpha, -angle, clip);
			DrawCard(layers.Right, anchor + new Vector2(12, 5) * render.ShellScale, scale, render.ShellAlpha, angle, clip);
			DrawCard(layers.Front, anchor + new Vector2(0, -7 * fan * render.ShellScale), scale, render.ShellAlpha, 0f, clip);
		}

		private void DrawSwitchingStack(
			(Entity Front, Entity Left, Entity Right) current,
			(Entity Front, Entity Left, Entity Right) target,
			float progress,
			Vector2 anchor,
			float scale,
			float angle,
			float fan,
			Rectangle clip,
			ModalAnimationRenderState render)
		{
			float linearProgress = MathHelper.Clamp(progress, 0f, 1f);
			float easedProgress = 1f - MathF.Pow(1f - linearProgress, 3f);
			var drawOrder = linearProgress < 0.5f ? current : target;
			DrawSwitchingCard(drawOrder.Left, current, target, easedProgress, anchor, scale, angle, fan, clip, render);
			DrawSwitchingCard(drawOrder.Right, current, target, easedProgress, anchor, scale, angle, fan, clip, render);
			DrawSwitchingCard(drawOrder.Front, current, target, easedProgress, anchor, scale, angle, fan, clip, render);
		}

		private void DrawSwitchingCard(
			Entity card,
			(Entity Front, Entity Left, Entity Right) current,
			(Entity Front, Entity Left, Entity Right) target,
			float progress,
			Vector2 anchor,
			float scale,
			float angle,
			float fan,
			Rectangle clip,
			ModalAnimationRenderState render)
		{
			StackLayerVisual start = GetLayerVisual(card, current, angle, fan);
			StackLayerVisual end = GetLayerVisual(card, target, angle, fan);
			var offset = Vector2.Lerp(start.Offset, end.Offset, progress) * render.ShellScale;
			float rotation = MathHelper.Lerp(start.Rotation, end.Rotation, progress);
			DrawCard(card, anchor + offset, scale, render.ShellAlpha, rotation, clip);
		}

		private void DrawCard(Entity card, Vector2 anchor, float scale, float alpha, float rotation, Rectangle clip)
		{
			if (card == null) return;
			EventManager.Publish(new CardRenderScaledEvent
			{
				Card = card,
				Position = anchor,
				Scale = scale,
				Alpha = alpha,
				Rotation = rotation,
				ClipRect = clip,
				PreferCachedBase = true,
			});
		}

		private StackLayerVisual GetLayerVisual(
			Entity card,
			(Entity Front, Entity Left, Entity Right) layers,
			float angle,
			float fan)
		{
			if (card == layers.Front)
				return new StackLayerVisual(new Vector2(0, -7 * fan), 0f);
			if (card == layers.Left)
				return new StackLayerVisual(new Vector2(-12, 5), -angle);
			return new StackLayerVisual(new Vector2(12, 5), angle);
		}

		private static (Entity Front, Entity Left, Entity Right) ResolveLayers(
			WayStationCollectionCardStackPresentation stack,
			CardColor frontColor,
			bool upgraded)
		{
			Entity white = upgraded ? stack.UpgradedWhiteCard ?? stack.WhiteCard : stack.WhiteCard;
			Entity red = upgraded ? stack.UpgradedRedCard ?? stack.RedCard : stack.RedCard;
			Entity black = upgraded ? stack.UpgradedBlackCard ?? stack.BlackCard : stack.BlackCard;
			return frontColor switch
			{
				CardColor.Red => (red, white, black),
				CardColor.Black => (black, red, white),
				_ => (white, black, red),
			};
		}

		private readonly record struct StackLayerVisual(
			Vector2 Offset,
			float Rotation);

		private Vector2 GetCardAnchor(Rectangle bounds, float scale)
		{
			var settings = CardGeometryService.GetSettings(EntityManager);
			int offset = (int)Math.Round((settings?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra) * scale);
			return new Vector2(bounds.Center.X, bounds.Center.Y + offset);
		}
	}

	[DebugTab("WayStation Collection Saints")]
	public sealed class WayStationCollectionSaintsDisplaySystem : WayStationCollectionDisplaySystemBase
	{
		[DebugEditable(DisplayName = "Medal Size", Step = 2, Min = 40, Max = 140)]
		public int MedalSize { get; set; } = 88;

		[DebugEditable(DisplayName = "Body Scale", Step = 0.01f, Min = 0.04f, Max = 0.3f)]
		public float BodyScale { get; set; } = 0.105f;

		public WayStationCollectionSaintsDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager, graphicsDevice, spriteBatch, imageAssets) { }

		public void Draw()
		{
			if (!TryGetView(out _, out var state, out var layout, out var catalog, out var render)
				|| state.ActiveTab != WayStationCollectionTab.Saints) return;
			Rectangle wall = render.Transform(layout.SaintWall);
			Rectangle detail = render.Transform(layout.SaintDetail);
			SpriteBatch.Draw(Pixel, new Rectangle(wall.Right - 1, wall.Y, 1, wall.Height), Color.White * (0.16f * render.ShellAlpha));
			DrawToolbar(layout, render);
			if (catalog.Saints.Count == 0)
			{
				DrawCentered(BodyFont, "No saints have been unlocked.", render.Transform(layout.Body), new Color(160, 154, 148) * render.ShellAlpha, render.TransformScale(0.12f));
				return;
			}
			DrawMedalWall(state, layout, catalog, render);
			DrawDetail(state, layout, catalog, render);
		}

		private void DrawToolbar(WayStationCollectionModalLayout layout, ModalAnimationRenderState render)
		{
			Rectangle toolbar = render.Transform(layout.SaintToolbar);
			string text = "MEDALS ACQUIRED";
			Vector2 size = BodyFont.MeasureString(text) * render.TransformScale(0.095f);
			SpriteBatch.DrawString(
				BodyFont,
				text,
				new Vector2(toolbar.X + 28 * render.ShellScale, toolbar.Center.Y - size.Y / 2f),
				new Color(200, 192, 184) * render.ShellAlpha,
				0f,
				Vector2.Zero,
				render.TransformScale(0.095f),
				SpriteEffects.None,
				0f);
			SpriteBatch.Draw(Pixel, new Rectangle(toolbar.X, toolbar.Bottom - 1, toolbar.Width, 1), Color.White * (0.08f * render.ShellAlpha));
		}

		private void DrawMedalWall(
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			WayStationCollectionCatalog catalog,
			ModalAnimationRenderState render)
		{
			Rectangle clip = render.Transform(layout.SaintListClip);
			WithClip(clip, () =>
			{
				foreach (var item in catalog.Saints)
				{
					var entity = EntityManager.GetEntity(WayStationSceneConstants.CollectionSaintTilePrefix + item.Id);
					var tile = entity?.GetComponent<WayStationCollectionSaintTilePresentation>();
					if (tile == null || !tile.Bounds.Intersects(layout.SaintListClip)) continue;
					var motion = entity.GetComponent<WayStationCollectionMotion>();
					float hover = motion?.Hover ?? 0f;
					float scale = MathHelper.Lerp(1f, 1.1f, hover);
					Rectangle bounds = render.Transform(tile.Bounds);
					var center = new Vector2(bounds.Center.X, bounds.Y + MedalSize * render.ShellScale / 2f);
					bool selected = string.Equals(state.SelectedMedalId, item.Id, StringComparison.OrdinalIgnoreCase);
					if (selected || hover > 0.01f)
					{
						int glowSize = (int)Math.Round((MedalSize + 20 + hover * 8) * render.ShellScale);
						Texture2D glow = ImageAssets.GetAntiAliasedCircle(Math.Max(2, glowSize / 2));
						SpriteBatch.Draw(glow, new Rectangle((int)center.X - glowSize / 2, (int)center.Y - glowSize / 2, glowSize, glowSize), Color.White * ((selected ? 0.17f : 0.09f) * render.ShellAlpha));
						if (selected)
							DrawRing(center, (MedalSize * scale + 8) * render.ShellScale, 3f, Color.White * (0.88f * render.ShellAlpha));
					}
					MedalIconRenderService.DrawMedalIcon(
						SpriteBatch,
						GraphicsDevice,
						TitleFont,
						center,
						Math.Max(1, (int)Math.Round(MedalSize * render.ShellScale)),
						item.Id,
						ImageAssets,
						scale,
						opacity: render.ShellAlpha);
					DrawMedalLabel(
						item.Medal.Name,
						new Rectangle(bounds.X, bounds.Y + (int)Math.Round(94 * render.ShellScale), bounds.Width, (int)Math.Round(32 * render.ShellScale)),
						(selected ? Color.White : new Color(200, 192, 184)) * render.ShellAlpha,
						render.TransformScale(0.072f));
				}
			});
		}

		private void DrawMedalLabel(string text, Rectangle bounds, Color color, float scale)
		{
			var lines = TextUtils.WrapText(BodyFont, text ?? string.Empty, scale, Math.Max(1, bounds.Width))
				.Take(2)
				.ToArray();
			if (lines.Length == 0) return;
			float lineHeight = BodyFont.LineSpacing * scale;
			float y = bounds.Center.Y - lines.Length * lineHeight / 2f;
			foreach (string line in lines)
			{
				string safe = TextUtils.FilterUnsupportedGlyphs(BodyFont, line);
				Vector2 size = BodyFont.MeasureString(safe) * scale;
				SpriteBatch.DrawString(BodyFont, safe, new Vector2(bounds.Center.X - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				y += lineHeight;
			}
		}

		private void DrawDetail(
			WayStationCollectionModalState state,
			WayStationCollectionModalLayout layout,
			WayStationCollectionCatalog catalog,
			ModalAnimationRenderState render)
		{
			var entry = catalog.Saints.FirstOrDefault(item =>
				string.Equals(item.Id, state.SelectedMedalId, StringComparison.OrdinalIgnoreCase));
			if (entry == null)
			{
				DrawCentered(BodyFont, "Select a saint to read their chronicle.", render.Transform(layout.SaintDetailClip), new Color(160, 154, 148) * render.ShellAlpha, render.TransformScale(0.11f));
				return;
			}
			Rectangle clip = render.Transform(layout.SaintDetailClip);
			WithClip(clip, () =>
			{
				int x = layout.SaintDetailClip.X;
				int y = layout.SaintDetailClip.Y - state.SaintDetailScrollOffset;
				int width = layout.SaintDetailClip.Width;
				float alpha = render.ShellAlpha;
				float titleScale = render.TransformScale(0.26f);
				string saintName = TextUtils.FilterUnsupportedGlyphs(TitleFont, entry.Medal.Name);
				SpriteBatch.DrawString(TitleFont, saintName, render.Transform(new Vector2(x, y)), Color.White * alpha, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
				y += 62;

				var effectLines = TextUtils.WrapText(BodyFont, entry.Medal.Text ?? string.Empty, BodyScale, width - 44);
				var keywords = TooltipTextService.GetKeywordTooltipBlocks(entry.Medal.Text ?? string.Empty);
				int effectHeight = 30 + effectLines.Count * 24 + keywords.Sum(item => TextUtils.WrapText(BodyFont, item.Text, 0.085f, width - 68).Count * 20 + 12);
				var effectRect = new Rectangle(x, y, width, Math.Max(86, effectHeight));
				DrawRounded(render.Transform(effectRect), Math.Max(2, (int)Math.Round(6 * render.ShellScale)), Color.White * (0.055f * alpha));
				DrawBorder(render.Transform(effectRect), Color.White * (0.13f * alpha));
				int effectY = y + 15;
				effectY += DrawWrappedTransformed(entry.Medal.Text, new Rectangle(x + 20, effectY, width - 40, effectRect.Bottom - effectY), BodyFont, new Color(240, 236, 230) * alpha, BodyScale, render) + 8;
				foreach (var keyword in keywords)
				{
					var rule = render.Transform(new Rectangle(x + 20, effectY, 2, 40));
					SpriteBatch.Draw(Pixel, rule, new Color(212, 168, 74) * (0.65f * alpha));
					effectY += DrawWrappedTransformed(keyword.Text, new Rectangle(x + 34, effectY, width - 56, 100), BodyFont, new Color(166, 158, 150) * alpha, 0.082f, render) + 8;
				}
				y = effectRect.Bottom + 25;

				var saint = entry.Saint;
				if (saint == null)
				{
					DrawWrappedTransformed("No chronicle is available for this saint.", new Rectangle(x, y, width, 100), BodyFont, new Color(180, 176, 168) * alpha, BodyScale, render);
					return;
				}
				DrawSectionLabel("YEARS ON EARTH", x, ref y, render);
				y += DrawWrappedTransformed(saint.lifespan, new Rectangle(x, y, width, 80), ItalicFont ?? BodyFont, new Color(200, 192, 184) * alpha, BodyScale, render) + 18;
				DrawSectionLabel("LIFE AND WITNESS", x, ref y, render);
				foreach (string paragraph in saint.bioParagraphs ?? [])
					y += DrawWrappedTransformed(paragraph, new Rectangle(x, y, width, 300), BodyFont, new Color(180, 176, 168) * alpha, BodyScale, render) + 15;
				DrawSectionLabel("COMMON PATRONAGES", x, ref y, render);
				y += DrawWrappedTransformed(saint.patronages, new Rectangle(x, y, width, 180), BodyFont, new Color(200, 192, 184) * alpha, BodyScale, render) + 20;
				DrawSectionLabel(string.IsNullOrWhiteSpace(saint.prayerTitle) ? "PRAYER" : saint.prayerTitle.ToUpperInvariant(), x, ref y, render, new Color(212, 168, 74));
				var prayerRule = render.Transform(new Rectangle(x, y, 3, 130));
				SpriteBatch.Draw(Pixel, prayerRule, new Color(196, 30, 58) * (0.75f * alpha));
				DrawWrappedTransformed(saint.prayerText, new Rectangle(x + 20, y, width - 20, 300), ItalicFont ?? BodyFont, new Color(240, 236, 230) * alpha, BodyScale, render);
			});
		}

		private int DrawWrappedTransformed(
			string text,
			Rectangle bounds,
			SpriteFont font,
			Color color,
			float scale,
			ModalAnimationRenderState render)
		{
			int lines = 0;
			foreach (string line in TextUtils.WrapText(font, text ?? string.Empty, scale, Math.Max(1, bounds.Width)))
			{
				string safe = TextUtils.FilterUnsupportedGlyphs(font, line);
				SpriteBatch.DrawString(font, safe, render.Transform(new Vector2(bounds.X, bounds.Y + lines * (font.LineSpacing * scale + 3))), color, 0f, Vector2.Zero, render.TransformScale(scale), SpriteEffects.None, 0f);
				lines++;
			}
			return (int)Math.Ceiling(lines * (font.LineSpacing * scale + 3));
		}

		private void DrawSectionLabel(
			string label,
			int x,
			ref int y,
			ModalAnimationRenderState render,
			Color? color = null)
		{
			string safe = TextUtils.FilterUnsupportedGlyphs(BodyFont, label);
			SpriteBatch.DrawString(BodyFont, safe, render.Transform(new Vector2(x, y)), (color ?? new Color(200, 192, 184)) * render.ShellAlpha, 0f, Vector2.Zero, render.TransformScale(0.075f), SpriteEffects.None, 0f);
			y += 28;
		}

		private void DrawRing(Vector2 center, float diameter, float thickness, Color color)
		{
			Texture2D ring = PrimitiveTextureFactory.GetAntialiasedRingMask(
				GraphicsDevice,
				Math.Max(2, (int)Math.Round(diameter)),
				Math.Max(2, (int)Math.Round(diameter)),
				Math.Max(1, (int)Math.Round(thickness)));
			int size = Math.Max(2, (int)Math.Round(diameter));
			SpriteBatch.Draw(ring, new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size), color);
		}
	}

	[DebugTab("WayStation Collection Equipment")]
	public sealed class WayStationCollectionEquipmentDisplaySystem : WayStationCollectionDisplaySystemBase
	{
		[DebugEditable(DisplayName = "Tile Radius", Step = 1, Min = 0, Max = 24)]
		public int TileRadius { get; set; } = 6;

		[DebugEditable(DisplayName = "Body Scale", Step = 0.01f, Min = 0.04f, Max = 0.3f)]
		public float BodyScale { get; set; } = 0.075f;

		public WayStationCollectionEquipmentDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager, graphicsDevice, spriteBatch, imageAssets) { }

		public void Draw()
		{
			if (!TryGetView(out _, out var state, out var layout, out var catalog, out var render)
				|| state.ActiveTab != WayStationCollectionTab.Equipment) return;
			DrawHeaders(layout, render);
			if (catalog.Equipment.Count == 0)
			{
				DrawCentered(BodyFont, "No equipment has been unlocked.", render.Transform(layout.EquipmentContentClip), new Color(160, 154, 148) * render.ShellAlpha, render.TransformScale(0.12f));
				return;
			}
			Rectangle clip = render.Transform(layout.EquipmentContentClip);
			WithClip(clip, () =>
			{
				foreach (var entry in catalog.Equipment)
				{
					var entity = EntityManager.GetEntity(WayStationSceneConstants.CollectionEquipmentTilePrefix + entry.Id);
					var tile = entity?.GetComponent<WayStationCollectionEquipmentTilePresentation>();
					if (tile == null || !tile.Bounds.Intersects(layout.EquipmentContentClip)) continue;
					DrawTile(entity, tile, entry, render);
				}
			});
		}

		private void DrawHeaders(WayStationCollectionModalLayout layout, ModalAnimationRenderState render)
		{
			EquipmentSlot[] slots = [EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Arms, EquipmentSlot.Legs];
			Rectangle header = render.Transform(layout.EquipmentHeader);
			int columnWidth = header.Width / slots.Length;
			for (int i = 0; i < slots.Length; i++)
			{
				var column = new Rectangle(header.X + i * columnWidth, header.Y, columnWidth, header.Height);
				if (i > 0)
					SpriteBatch.Draw(Pixel, new Rectangle(column.X, header.Y, 1, render.Transform(layout.EquipmentHall).Height), Color.White * (0.12f * render.ShellAlpha));
				Texture2D icon = ImageAssets.TryGetTexture(slots[i].ToString().ToLowerInvariant());
				if (icon != null)
				{
					var iconRect = EquipmentArtService.GetContainedBounds(icon, new Rectangle(column.Center.X - 92, column.Center.Y - 20, 40, 40));
					SpriteBatch.Draw(icon, iconRect, Color.White * render.ShellAlpha);
				}
				string label = slots[i].ToString().ToUpperInvariant();
				Vector2 size = BodyFont.MeasureString(label) * render.TransformScale(0.08f);
				SpriteBatch.DrawString(BodyFont, label, new Vector2(column.Center.X - 35, column.Center.Y - size.Y / 2f), new Color(210, 204, 196) * render.ShellAlpha, 0f, Vector2.Zero, render.TransformScale(0.08f), SpriteEffects.None, 0f);
			}
			SpriteBatch.Draw(Pixel, new Rectangle(header.X, header.Bottom - 1, header.Width, 1), Color.White * (0.10f * render.ShellAlpha));
		}

		private void DrawTile(
			Entity entity,
			WayStationCollectionEquipmentTilePresentation tile,
			WayStationCollectionEquipmentEntry entry,
			ModalAnimationRenderState render)
		{
			var equipment = entry.Equipment;
			var motion = entity.GetComponent<WayStationCollectionMotion>();
			float hover = motion?.Hover ?? 0f;
			Rectangle bounds = render.Transform(tile.Bounds);
			int radius = Math.Max(1, (int)Math.Round(TileRadius * render.ShellScale));
			if (hover > 0.01f)
			{
				for (int i = 3; i >= 1; i--)
				{
					var glow = new Rectangle(bounds.X - i * 3, bounds.Y - i * 3, bounds.Width + i * 6, bounds.Height + i * 6);
					DrawRounded(glow, radius + i * 2, Color.White * (0.025f * i * hover * render.ShellAlpha));
				}
			}
			DrawRounded(bounds, radius, Color.White * ((0.035f + hover * 0.035f) * render.ShellAlpha));
			DrawBorder(bounds, Color.White * ((0.14f + hover * 0.35f) * render.ShellAlpha));

			Rectangle artRect = render.Transform(tile.ArtBounds);
			Texture2D art = EquipmentArtService.GetTexture(ImageAssets, equipment);
			if (art != null)
			{
				float artScale = MathHelper.Lerp(1f, 1.08f, hover);
				var contained = EquipmentArtService.GetContainedBounds(art, artRect);
				var scaled = ScaleAroundCenter(contained, artScale);
				SpriteBatch.Draw(art, scaled, Color.White * render.ShellAlpha);
			}

			int bodyX = artRect.Right + (int)Math.Round(16 * render.ShellScale);
			int bodyWidth = Math.Max(20, bounds.Right - bodyX - (int)Math.Round(12 * render.ShellScale));
			int y = bounds.Y + (int)Math.Round(14 * render.ShellScale);
			string name = TextUtils.FilterUnsupportedGlyphs(TitleFont, equipment.Name ?? entry.Id);
			SpriteBatch.DrawString(TitleFont, name, new Vector2(bodyX, y), Color.White * render.ShellAlpha, 0f, Vector2.Zero, render.TransformScale(0.125f), SpriteEffects.None, 0f);
			if (equipment.Block > 0)
			{
				int chipRight = bounds.Right - (int)Math.Round(12 * render.ShellScale);
				int chipTop = bounds.Y + (int)Math.Round(10 * render.ShellScale);
				DrawBlockChip(equipment, chipRight, chipTop, render);
			}
			y += (int)Math.Round(32 * render.ShellScale);
			DrawColorGem(equipment.Color, bodyX, y, render);
			DrawUsePips(equipment, bodyX + 34, y + 2, render);
			y += (int)Math.Round(30 * render.ShellScale);
			string description = !string.IsNullOrWhiteSpace(equipment.Text) ? equipment.Text : equipment.FlavorText;
			SpriteFont font = !string.IsNullOrWhiteSpace(equipment.Text) ? BodyFont : ItalicFont ?? BodyFont;
			DrawKeywordText(font, description, new Rectangle(bodyX, y, bodyWidth, bounds.Bottom - y - 8), render);
		}

		private void DrawColorGem(CardColor color, int x, int y, ModalAnimationRenderState render)
		{
			Color gem = color switch
			{
				CardColor.Red => new Color(204, 34, 34),
				CardColor.Black => new Color(32, 32, 32),
				_ => Color.White,
			};
			int size = Math.Max(4, (int)Math.Round(10 * render.ShellScale));
			Texture2D circle = ImageAssets.GetAntiAliasedCircle(Math.Max(2, size / 2));
			SpriteBatch.Draw(circle, new Rectangle(x, y, size, size), gem * render.ShellAlpha);
			DrawBorder(new Rectangle(x, y, size, size), Color.White * (0.30f * render.ShellAlpha));
		}

		private void DrawBlockChip(
			ChurchSuffering.ECS.Objects.Equipment.EquipmentBase equipment,
			int right,
			int y,
			ModalAnimationRenderState render)
		{
			int width = Math.Max(18, (int)Math.Round(38 * render.ShellScale));
			int labelHeight = Math.Max(7, (int)Math.Round(13 * render.ShellScale));
			int valueHeight = Math.Max(12, (int)Math.Round(32 * render.ShellScale));
			var label = new Rectangle(right - width, y, width, labelHeight);
			var value = new Rectangle(label.X, label.Bottom, width, valueHeight);
			DrawRounded(label, Math.Max(1, (int)Math.Round(3 * render.ShellScale)), CardPalette.BlockLabelSlabBackground(equipment.Color) * render.ShellAlpha);
			DrawRounded(value, Math.Max(1, (int)Math.Round(3 * render.ShellScale)), CardPalette.BlockChipBackground(equipment.Color) * render.ShellAlpha);
			DrawCentered(BodyFont, "BLOCK", label, CardPalette.BlockLabelSlabText(equipment.Color) * render.ShellAlpha, render.TransformScale(0.042f));
			DrawCentered(TitleFont, equipment.Block.ToString(), value, CardPalette.BlockChipText(equipment.Color) * render.ShellAlpha, render.TransformScale(0.09f));
		}

		private void DrawUsePips(
			ChurchSuffering.ECS.Objects.Equipment.EquipmentBase equipment,
			int x,
			int y,
			ModalAnimationRenderState render)
		{
			int count = Math.Max(0, equipment.MaxUses);
			int size = Math.Max(3, (int)Math.Round(7 * render.ShellScale));
			Texture2D circle = ImageAssets.GetAntiAliasedCircle(Math.Max(2, size / 2));
			for (int i = 0; i < count; i++)
				SpriteBatch.Draw(circle, new Rectangle(x + i * (size + 4), y, size, size), Color.White * (0.78f * render.ShellAlpha));
		}

		private void DrawKeywordText(
			SpriteFont font,
			string text,
			Rectangle bounds,
			ModalAnimationRenderState render)
		{
			if (font == null || string.IsNullOrWhiteSpace(text)) return;
			var runs = TooltipTextService.GetKeywordTextRuns(text);
			float scale = render.TransformScale(BodyScale);
			float x = bounds.X;
			float y = bounds.Y;
			float lineHeight = font.LineSpacing * scale + 2;
			foreach (var run in runs)
			{
				string safe = TextUtils.FilterUnsupportedGlyphs(font, run.Text);
				foreach (string token in SplitKeepingSpaces(safe))
				{
					Vector2 size = font.MeasureString(token) * scale;
					if (!string.IsNullOrWhiteSpace(token) && x + size.X > bounds.Right)
					{
						x = bounds.X;
						y += lineHeight;
					}
					if (y + lineHeight > bounds.Bottom) return;
					SpriteBatch.DrawString(
						font,
						token,
						new Vector2(x, y),
						(run.IsKeyword ? new Color(212, 168, 74) : new Color(190, 184, 176)) * render.ShellAlpha,
						0f,
						Vector2.Zero,
						scale,
						SpriteEffects.None,
						0f);
					x += size.X;
				}
			}
		}

		private static IEnumerable<string> SplitKeepingSpaces(string text)
		{
			if (string.IsNullOrEmpty(text)) yield break;
			int start = 0;
			for (int i = 0; i < text.Length; i++)
			{
				if (!char.IsWhiteSpace(text[i])) continue;
				if (i > start) yield return text[start..i];
				yield return text[i].ToString();
				start = i + 1;
			}
			if (start < text.Length) yield return text[start..];
		}

		private static Rectangle ScaleAroundCenter(Rectangle rect, float scale)
		{
			int width = Math.Max(1, (int)Math.Round(rect.Width * scale));
			int height = Math.Max(1, (int)Math.Round(rect.Height * scale));
			return new Rectangle(rect.Center.X - width / 2, rect.Center.Y - height / 2, width, height);
		}
	}
}
