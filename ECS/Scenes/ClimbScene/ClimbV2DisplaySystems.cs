using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems;

internal static class ClimbV2Draw
{
	public static readonly Color Ink = new(8, 9, 10);
	public static readonly Color Paper = new(238, 231, 220);
	public static readonly Color Muted = new(183, 175, 163);
	public static readonly Color Red = new(229, 44, 72);
	public static readonly Color RedDark = new(141, 20, 40);
	public static readonly Color Gold = new(211, 168, 94);
	public static readonly Color Green = new(143, 209, 167);

	public static Rectangle Shift(Rectangle rect, Vector2 offset) => new(
		rect.X + (int)MathF.Round(offset.X), rect.Y + (int)MathF.Round(offset.Y), rect.Width, rect.Height);

	public static Rectangle ResolveBounds(EntityManager entityManager, Entity entity)
	{
		var ui = entity?.GetComponent<UIElement>();
		return ui == null ? Rectangle.Empty : TransformResolverService.ResolveUIBounds(entityManager, entity, ui);
	}

	public static Rectangle ApplyChoiceHover(Rectangle rect, ClimbSlotKind kind, bool hovered)
	{
		if (!hovered || rect.IsEmpty) return rect;
		int inflateY = kind == ClimbSlotKind.Shop ? 1 : 2;
		return new Rectangle(rect.X - 2, rect.Y - inflateY, rect.Width + 4, rect.Height + inflateY * 2);
	}

	public static Rectangle Contain(Texture2D texture, Rectangle container)
	{
		return texture == null ? Rectangle.Empty : Contain(texture.Width, texture.Height, container);
	}

	internal static Rectangle Contain(int sourceWidth, int sourceHeight, Rectangle container)
	{
		if (sourceWidth <= 0 || sourceHeight <= 0 || container.Width <= 0 || container.Height <= 0) return Rectangle.Empty;
		float scale = Math.Min(container.Width / (float)sourceWidth, container.Height / (float)sourceHeight);
		int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
		int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
		return new Rectangle(container.Center.X - width / 2, container.Center.Y - height / 2, width, height);
	}

	public static void Border(SpriteBatch batch, Texture2D pixel, Rectangle rect, Color color, int thickness = 1)
	{
		if (rect.Width <= 0 || rect.Height <= 0) return;
		batch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
		batch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
		batch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
		batch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
	}

	public static void Text(SpriteBatch batch, SpriteFont font, string text, Vector2 position, float scale, Color color)
	{
		if (font == null || string.IsNullOrEmpty(text)) return;
		batch.DrawString(font, ToAscii(text), position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
	}

	public static void FittedText(SpriteBatch batch, SpriteFont font, string text, Vector2 position, float preferredScale, float maxWidth, Color color)
	{
		if (font == null || string.IsNullOrEmpty(text) || maxWidth <= 0f) return;
		string ascii = ToAscii(text);
		float measuredWidth = font.MeasureString(ascii).X;
		float scale = measuredWidth <= 0f ? preferredScale : Math.Min(preferredScale, maxWidth / measuredWidth);
		batch.DrawString(font, ascii, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
	}

	public static void Wrapped(SpriteBatch batch, SpriteFont font, string text, Rectangle rect, float scale, Color color, int maxLines = 3)
	{
		if (font == null || string.IsNullOrWhiteSpace(text)) return;
		var words = ToAscii(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var lines = new List<string>();
		string line = string.Empty;
		foreach (string word in words)
		{
			string candidate = line.Length == 0 ? word : line + " " + word;
			if (font.MeasureString(candidate).X * scale <= rect.Width || line.Length == 0)
			{
				line = candidate;
				continue;
			}
			lines.Add(line);
			line = word;
			if (lines.Count >= maxLines) break;
		}
		if (lines.Count < maxLines && line.Length > 0) lines.Add(line);
		float lineHeight = font.LineSpacing * scale * 0.92f;
		for (int i = 0; i < lines.Count; i++) Text(batch, font, lines[i], new Vector2(rect.X, rect.Y + i * lineHeight), scale, color);
	}

	public static string ToAscii(string value)
	{
		if (string.IsNullOrEmpty(value)) return string.Empty;
		return new string(value.Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
	}

	public static void Cover(SpriteBatch batch, Texture2D texture, Rectangle destination, Color color)
	{
		Cover(batch, texture, destination, color, false);
	}

	public static void Cover(SpriteBatch batch, Texture2D texture, Rectangle destination, Color color, bool alignTop)
	{
		if (texture == null || destination.Width <= 0 || destination.Height <= 0) return;
		float destRatio = destination.Width / (float)destination.Height;
		float sourceRatio = texture.Width / (float)texture.Height;
		var source = new Rectangle(0, 0, texture.Width, texture.Height);
		if (sourceRatio > destRatio)
		{
			int width = Math.Max(1, (int)Math.Round(texture.Height * destRatio));
			source.X = (texture.Width - width) / 2;
			source.Width = width;
		}
		else
		{
			int height = Math.Max(1, (int)Math.Round(texture.Width / destRatio));
			source.Y = alignTop ? 0 : (texture.Height - height) / 2;
			source.Height = height;
		}
		batch.Draw(texture, destination, source, color);
	}

	public static (Vector2 Offset, float Alpha) Motion(Entity entity, float opacityMultiplier = 1f)
	{
		var motion = entity?.GetComponent<ClimbV2ChoiceMotion>();
		float alpha = (motion?.Opacity ?? 1f) * opacityMultiplier;
		return (motion?.Offset ?? Vector2.Zero, MathHelper.Clamp(alpha, 0f, 1f));
	}

	public static void SoftRoundedShadow(SpriteBatch batch, ImageAssetService assets, Rectangle rect,
		int topLeftRadius, int topRightRadius, int bottomRightRadius, int bottomLeftRadius,
		int offsetY, int blurRadius, float opacity, float alpha)
	{
		if (rect.IsEmpty || opacity <= 0f || alpha <= 0f) return;
		const int layerStep = 3;
		int layerCount = Math.Max(1, (blurRadius + layerStep - 1) / layerStep);
		float layerOpacity = 1f - MathF.Pow(1f - MathHelper.Clamp(opacity, 0f, 0.95f), 1f / layerCount);
		for (int layer = layerCount; layer >= 1; layer--)
		{
			int spread = layer * layerStep;
			var shadowRect = new Rectangle(rect.X - spread, rect.Y + offsetY - spread, rect.Width + spread * 2, rect.Height + spread * 2);
			Texture2D mask = assets.GetRoundedRectPerCorner(
				shadowRect.Width,
				shadowRect.Height,
				topLeftRadius + spread,
				topRightRadius + spread,
				bottomRightRadius + spread,
				bottomLeftRadius + spread);
			batch.Draw(mask, shadowRect, Color.Black * (layerOpacity * alpha));
		}
	}
}

[DebugTab("Climb V2 Shop Container")]
public sealed class ShopContainerDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch;
	private readonly Texture2D _pixel;
	public ShopContainerDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em) { _batch = batch; _pixel = assets.GetPixel(Color.White); }
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbV2SectionPresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void Draw()
	{
		var section = GetRelevantEntities().FirstOrDefault(e => e.GetComponent<ClimbV2SectionPresentation>()?.Kind == ClimbV2SectionKind.Shop);
		var rect = section?.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty;
		if (!rect.IsEmpty) _batch.Draw(_pixel, new Rectangle(rect.X - 5, rect.Y - 8, 5, rect.Height + 16), ClimbV2Draw.RedDark * 0.22f);
	}
}

[DebugTab("Climb V2 Encounter Container")]
public sealed class EncounterContainerDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch;
	private readonly Texture2D _pixel;
	public EncounterContainerDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em) { _batch = batch; _pixel = assets.GetPixel(Color.White); }
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbV2SectionPresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void Draw()
	{
		var section = GetRelevantEntities().FirstOrDefault(e => e.GetComponent<ClimbV2SectionPresentation>()?.Kind == ClimbV2SectionKind.Encounter);
		var rect = section?.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty;
		if (rect.IsEmpty) return;
		_batch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.12f);
		// Soft, winding road bands behind the waystones.
		for (int i = 0; i < 9; i++)
		{
			float t = i / 8f;
			int x = (int)MathHelper.Lerp(rect.X + 190, rect.Right - 300, t);
			int y = rect.Y + 90 + (int)(MathF.Sin(t * MathF.PI * 2f) * 90f) + (int)(t * 650f);
			_batch.Draw(_pixel, new Rectangle(x, y, 310, 34), ClimbV2Draw.Gold * 0.055f);
		}
	}
}

[DebugTab("Climb V2 Event Container")]
public sealed class EventContainerDisplaySystem : Core.System
{
	public EventContainerDisplaySystem(EntityManager em) : base(em) { }
	protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void Draw() { }
}

[DebugTab("Climb V2 Title")]
public sealed class ClimbV2TitleDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch;
	private readonly Texture2D _pixel;
	private readonly SpriteFont _font = FontSingleton.TitleFont;
	public ClimbV2TitleDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em) { _batch = batch; _pixel = assets.GetPixel(Color.White); }
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbV2TitlePresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void Draw()
	{
		var rect = GetRelevantEntities().FirstOrDefault()?.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty;
		if (rect.IsEmpty) return;
		var center = new Vector2(rect.X + 23, rect.Center.Y);
		_batch.Draw(_pixel, center + new Vector2(0, 3), null, Color.Black * 0.45f, MathHelper.PiOver4, new Vector2(.5f), new Vector2(45, 45), SpriteEffects.None, 0f);
		_batch.Draw(_pixel, center, null, ClimbV2Draw.RedDark * 0.92f, MathHelper.PiOver4, new Vector2(.5f), new Vector2(45, 45), SpriteEffects.None, 0f);
		ClimbV2Draw.Text(_batch, _font, "+", center + new Vector2(-9, -23), 0.28f, Color.White);
		ClimbV2Draw.Text(_batch, _font, "The Climb", new Vector2(rect.X + 61, rect.Y + 15), 0.17f, Color.White);
	}
}

[DebugTab("Climb V2 Timeline")]
public sealed class DistanceClimbedTimelineDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch;
	private readonly Texture2D _pixel;
	private readonly Texture2D _circle;
	private readonly SpriteFont _body = FontSingleton.ChakraPetchFont;
	private readonly SpriteFont _title = FontSingleton.TitleFont;
	public DistanceClimbedTimelineDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em)
	{
		_batch = batch;
		_pixel = assets.GetPixel(Color.White);
		_circle = assets.GetAntiAliasedCircle(10);
	}
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<DistanceClimbedTimelinePresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void Draw()
	{
		var rect = GetRelevantEntities().FirstOrDefault()?.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty;
		if (rect.IsEmpty) return;
		var climb = SaveCache.GetClimbState();
		var preview = EntityManager.GetEntity(ClimbV2LayoutSystem.RootName)?.GetComponent<ClimbPreviewState>();
		int maxTime = ClimbRuleService.GetMaxTime(climb);
		int used = ClimbRuleService.ClampTime(climb, climb?.time ?? 0);
		int projected = preview?.IsActive == true ? preview.ProjectedUsedTime : used;
		ClimbV2Draw.Text(_batch, _body, "DISTANCE CLIMBED", new Vector2(rect.X, rect.Y + 1), 0.085f, Color.White * 0.92f);
		string value = $"{projected} / {maxTime}";
		Vector2 valueSize = _title.MeasureString(value) * 0.13f;
		ClimbV2Draw.Text(_batch, _title, value, new Vector2(rect.Right - valueSize.X, rect.Y - 2), 0.13f, ClimbV2Draw.Paper);
		int startX = rect.X + 10;
		int endX = rect.Right - 45;
		float step = (endX - startX) / (float)(maxTime - 1);
		int y = rect.Y + 41;
		int shopRefreshInterval = ClimbRuleService.GetShopRefreshInterval(climb);
		for (int i = 1; i <= maxTime; i++)
		{
			int x = startX + (int)MathF.Round((i - 1) * step);
			bool active = i <= used;
			bool projectedStep = i > used && i <= projected;
			bool projectedLine = i >= used && i < projected;
			bool refresh = i % shopRefreshInterval == 0 && i < maxTime;
			if (i < maxTime)
			{
				Color line = i < used ? ClimbV2Draw.Paper : projectedLine ? ClimbV2Draw.Red : Color.White * 0.22f;
				var lineRect = new Rectangle(x + (refresh ? 9 : 5), y - 1, Math.Max(1, (int)step - (refresh ? 9 : 5)), 2);
				if (projectedLine) _batch.Draw(_pixel, new Rectangle(lineRect.X, lineRect.Y - 3, lineRect.Width, 8), ClimbV2Draw.Red * 0.12f);
				_batch.Draw(_pixel, lineRect, line);
			}
			Color fill = projectedStep ? ClimbV2Draw.Red : active ? ClimbV2Draw.Paper : new Color(21, 19, 19);
			if (projectedStep) DrawCircle(x, y, refresh ? 14 : 10, ClimbV2Draw.Red * 0.18f);
			if (refresh)
			{
				DrawCircle(x, y, 10, ClimbV2Draw.Ink * 0.82f);
				DrawCircle(x, y, 8, projectedStep ? new Color(255, 213, 139) : ClimbV2Draw.Gold);
				DrawCircle(x, y, 6, fill);
				ClimbV2Draw.Text(_batch, _body, "SHOP", new Vector2(x - 13, y + 11), 0.055f, ClimbV2Draw.Gold);
			}
			else
			{
				DrawCircle(x, y, 5, projectedStep ? new Color(255, 123, 142) : Color.White * 0.5f);
				DrawCircle(x, y, 4, fill);
			}
		}
		var destination = new Rectangle(rect.Right - 31, y - 15, 30, 30);
		for (int size = 42; size >= 34; size -= 4)
			_batch.Draw(_pixel, destination.Center.ToVector2(), null, ClimbV2Draw.Red * 0.055f, MathHelper.PiOver4, new Vector2(.5f), new Vector2(size, size), SpriteEffects.None, 0f);
		_batch.Draw(_pixel, destination.Center.ToVector2(), null, ClimbV2Draw.Paper, MathHelper.PiOver4, new Vector2(.5f), new Vector2(34, 34), SpriteEffects.None, 0f);
		_batch.Draw(_pixel, destination.Center.ToVector2(), null, new Color(117, 22, 41), MathHelper.PiOver4, new Vector2(.5f), new Vector2(30, 30), SpriteEffects.None, 0f);
		ClimbV2Draw.Text(_batch, _title, "+", new Vector2(destination.X + 7, destination.Y - 2), 0.18f, Color.White);
	}

	private void DrawCircle(int x, int y, int radius, Color color)
	{
		_batch.Draw(_circle, new Rectangle(x - radius, y - radius, radius * 2, radius * 2), color);
	}
}

[DebugTab("Climb V2 Resources")]
public sealed class PlayerResourcesDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch;
	private readonly GraphicsDevice _graphicsDevice;
	private readonly Texture2D _pixel;
	private readonly SpriteFont _font = FontSingleton.ChakraPetchFont;
	private readonly Action<ClimbResourceHeaderPulseRequested> _pulseHandler;
	private ClimbResourceSave _pulse = new();
	private float _pulseElapsed = float.MaxValue;
	public PlayerResourcesDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch batch, ImageAssetService assets) : base(em)
	{
		_graphicsDevice = gd; _batch = batch; _pixel = assets.GetPixel(Color.White);
		_pulseHandler = evt => { _pulse = Clone(evt?.Resources); _pulseElapsed = 0f; };
		EventManager.Subscribe(_pulseHandler);
		ClimbSceneDrawHelpers.EnsureResourceTextures(assets);
	}
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<PlayerResourcesPresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public override void Update(GameTime gameTime) { base.Update(gameTime); _pulseElapsed += Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds); }
	public void Draw()
	{
		var rect = GetRelevantEntities().FirstOrDefault()?.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty;
		if (rect.IsEmpty) return;
		var current = SaveCache.GetClimbState()?.resources ?? new ClimbResourceSave();
		var preview = EntityManager.GetEntity(ClimbV2LayoutSystem.RootName)?.GetComponent<ClimbPreviewState>();
		var shown = preview?.IsActive == true ? preview.ProjectedResources : current;
		DrawResource(rect.X, rect.Y + 5, ClimbResourceType.Red, current.red, shown.red, _pulse.red);
		DrawResource(rect.X + 84, rect.Y + 5, ClimbResourceType.White, current.white, shown.white, _pulse.white);
		DrawResource(rect.X + 168, rect.Y + 5, ClimbResourceType.Black, current.black, shown.black, _pulse.black);
	}
	private void DrawResource(int x, int y, ClimbResourceType type, int current, int shown, int pulseAmount)
	{
		float pulse = pulseAmount > 0 && _pulseElapsed < 0.28f ? MathF.Sin(MathF.PI * _pulseElapsed / 0.28f) : 0f;
		var rect = new Rectangle(x, y, 72, 42);
		_batch.Draw(_pixel, rect, new Color(7, 8, 9) * 0.78f);
		ClimbV2Draw.Border(_batch, _pixel, rect, Color.White * (0.12f + 0.45f * pulse));
		ClimbSceneDrawHelpers.DrawResourceIcon(_batch, _graphicsDevice, _pixel, new Vector2(x + 7, y + 7), type, 27, Color.White);
		ClimbV2Draw.Text(_batch, _font, shown.ToString(), new Vector2(x + 39, y + 8), 0.14f, Color.White);
		int delta = shown - current;
		if (delta == 0) return;
		string value = delta > 0 ? $"+{delta}" : delta.ToString();
		var badge = new Rectangle(x + 51, y - 9, 29, 20);
		Color color = delta > 0 ? ClimbV2Draw.Green : new Color(255, 130, 146);
		_batch.Draw(_pixel, badge, (delta > 0 ? new Color(16, 36, 25) : new Color(48, 16, 24)) * 0.96f);
		ClimbV2Draw.Border(_batch, _pixel, badge, color * 0.78f);
		ClimbV2Draw.Text(_batch, _font, value, new Vector2(badge.X + 4, badge.Y + 2), 0.075f, color);
	}
	internal void SetResourcePulseForSnapshot(ClimbResourceSave resources, float progress) { _pulse = Clone(resources); _pulseElapsed = MathHelper.Clamp(progress, 0f, 1f) * 0.28f; }
	internal void SetResourcePreviewForSnapshot(ClimbResourceSave resources, float alpha)
	{
		var root = EntityManager.GetEntity(ClimbV2LayoutSystem.RootName);
		var preview = root?.GetComponent<ClimbPreviewState>();
		if (preview == null) return;
		preview.IsActive = alpha > 0f;
		preview.ProjectedResources = Clone(resources);
	}
	public void Shutdown() => EventManager.Unsubscribe(_pulseHandler);
	private static ClimbResourceSave Clone(ClimbResourceSave value) => new() { red = value?.red ?? 0, white = value?.white ?? 0, black = value?.black ?? 0 };
}

[DebugTab("Climb V2 Overview")]
public sealed class ClimbOverviewButtonDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch;
	private readonly Texture2D _pixel;
	private readonly Texture2D _cardBack;
	private readonly SpriteFont _font = FontSingleton.ChakraPetchFont;
	public ClimbOverviewButtonDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em) { _batch = batch; _pixel = assets.GetPixel(Color.White); _cardBack = assets.TryGetTexture("card_back"); }
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbOverviewButton>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void Draw()
	{
		var entity = GetRelevantEntities().FirstOrDefault();
		var rect = ClimbV2Draw.ResolveBounds(EntityManager, entity);
		if (rect.IsEmpty) return;
		bool hover = entity.GetComponent<UIElement>()?.IsHovered == true;
		Color color = hover ? ClimbV2Draw.Red : Color.White;
		_batch.Draw(_pixel, rect, new Color(8, 9, 10) * 0.78f);
		ClimbV2Draw.Border(_batch, _pixel, rect, color * 0.72f);
		var rear = new Rectangle(rect.X + 17, rect.Y + 14, 17, 24);
		var front = new Rectangle(rect.X + 21, rect.Y + 11, 17, 24);
		if (_cardBack != null) { _batch.Draw(_cardBack, rear, color * 0.55f); _batch.Draw(_cardBack, front, color); }
		else { ClimbV2Draw.Border(_batch, _pixel, rear, color * 0.55f); ClimbV2Draw.Border(_batch, _pixel, front, color); }
		if (hover) ClimbV2Draw.Text(_batch, _font, "CLIMB OVERVIEW", new Vector2(rect.X - 26, rect.Bottom + 7), 0.055f, Color.White);
	}
}

[DebugTab("Climb V2 Shop Items")]
public sealed class ShopItemDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch; private readonly Texture2D _pixel; private readonly ImageAssetService _assets;
	private readonly SpriteFont _title = FontSingleton.ChakraPetchFont; private readonly SpriteFont _body = FontSingleton.ChakraPetchFont;
	public ShopItemDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em) { _batch = batch; _assets = assets; _pixel = assets.GetPixel(Color.White); }
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbShopItemPresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void DrawEntity(Entity entity, float opacityMultiplier = 1f)
	{
		var ui = entity?.GetComponent<UIElement>(); if (ui == null || ui.IsHidden || ui.Bounds.IsEmpty) return;
		var slot = entity.GetComponent<ClimbSlotPresentation>(); var item = entity.GetComponent<ClimbShopItemPresentation>(); var (offset, alpha) = ClimbV2Draw.Motion(entity, opacityMultiplier);
		var rect = ClimbV2Draw.Shift(ClimbV2Draw.ResolveBounds(EntityManager, entity), offset);
		rect = ClimbV2Draw.ApplyChoiceHover(rect, slot.Kind, ui.IsHovered);
		ClimbV2Draw.SoftRoundedShadow(_batch, _assets, rect, 7, 2, 2, 7, 10, 12, 0.42f, alpha);
		ClimbV2Draw.SoftRoundedShadow(_batch, _assets, rect, 7, 2, 2, 7, 3, 5, 0.28f, alpha);
		_batch.Draw(_assets.GetRoundedRectPerCorner(rect.Width, rect.Height, 7, 2, 2, 7), rect, new Color(16, 17, 18) * (0.92f * alpha));
		Color artColor = string.Equals(item.ItemKind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase) ? ClimbV2Draw.Paper
			: string.Equals(item.ItemKind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase) ? new Color(7, 8, 9) : ClimbV2Draw.RedDark;
		var art = new Rectangle(rect.X, rect.Y, 78, rect.Height); _batch.Draw(_assets.GetRoundedRectPerCorner(art.Width, art.Height, 7, 0, 0, 7), art, artColor * alpha);
		var texture = _assets.TryGetTexture(item.ItemAsset); if (texture != null)
		{
			var target = new Rectangle(art.X + 7, art.Y + 10, 64, 84); var contained = EquipmentArtService.GetContainedBounds(texture, target); _batch.Draw(texture, contained, Color.White * alpha);
		}
		else if (string.Equals(item.ItemKind, ClimbShopSlotKinds.Boon, StringComparison.OrdinalIgnoreCase))
		{
			const string glyph = "?";
			const float glyphScale = 0.42f;
			var size = _title.MeasureString(glyph) * glyphScale;
			ClimbV2Draw.Text(_batch, _title, glyph,
				new Vector2(art.Center.X - size.X * 0.5f, art.Center.Y - size.Y * 0.5f),
				glyphScale, Color.White * alpha);
		}
		ClimbV2Draw.Text(_batch, _body, slot.Label.ToUpperInvariant(), new Vector2(rect.X + 86, rect.Y + 12), 0.064f, new Color(255, 135, 151) * alpha);
		ClimbV2Draw.Text(_batch, _title, slot.Title, new Vector2(rect.X + 86, rect.Y + 31), 0.105f, Color.White * alpha);
		var preview = EntityManager.GetEntity(ClimbV2LayoutSystem.RootName)?.GetComponent<ClimbPreviewState>();
		bool affordable = preview?.IsActive == true ? preview.AffordableShopSlotIds.Contains(slot.SlotId) : slot.IsAffordable;
		if (!affordable) _batch.Draw(_pixel, rect, Color.Black * (0.45f * alpha));
	}
}

[DebugTab("Climb V2 Encounters")]
public sealed class EncounterDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch; private readonly Texture2D _pixel; private readonly ImageAssetService _assets; private readonly SpriteFont _font = FontSingleton.TitleFont;
	private readonly Action<CursorStateEvent> _cursorHandler;
	private Vector2 _cursorPosition;
	private bool _hasCursorPosition;
	private Vector2 _portraitParallaxOffset;
	[DebugEditable(DisplayName = "Portrait Parallax Multiplier X", Step = 0.01f, Min = 0f, Max = 0.25f)]
	public float PortraitParallaxMultiplierX { get; set; } = 0.01f;
	[DebugEditable(DisplayName = "Portrait Parallax Multiplier Y", Step = 0.01f, Min = 0f, Max = 0.25f)]
	public float PortraitParallaxMultiplierY { get; set; } = 0.01f;
	[DebugEditable(DisplayName = "Portrait Parallax Max Offset", Step = 1f, Min = 0f, Max = 200f)]
	public float PortraitParallaxMaxOffset { get; set; } = 150f;
	[DebugEditable(DisplayName = "Portrait Parallax Smooth Time", Step = 0.01f, Min = 0f, Max = 0.5f)]
	public float PortraitParallaxSmoothTime { get; set; }
	[DebugEditable(DisplayName = "Portrait Parallax Zoom", Step = 0.01f, Min = 0.5f, Max = 1.5f)]
	public float PortraitParallaxZoom { get; set; } = 0.8f;
	[DebugEditable(DisplayName = "Portrait Crop Top Bias", Step = 0.01f, Min = 0f, Max = 1f)]
	public float PortraitCropTopBias { get; set; } = 0f;
	public EncounterDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em)
	{
		_batch = batch; _assets = assets; _pixel = assets.GetPixel(Color.White);
		_cursorHandler = evt => { _cursorPosition = evt.Position; _hasCursorPosition = true; };
		EventManager.Subscribe(_cursorHandler);
	}
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbEncounterPresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		Vector2 target = ComputePortraitParallaxTarget(
			_cursorPosition,
			_hasCursorPosition,
			PortraitParallaxMultiplierX,
			PortraitParallaxMultiplierY,
			PortraitParallaxMaxOffset);
		float dt = Math.Max(0f, (float)(gameTime?.ElapsedGameTime.TotalSeconds ?? 0d));
		float smooth = Math.Max(0f, PortraitParallaxSmoothTime);
		float alpha = smooth <= 0f ? 1f : 1f - MathF.Exp(-dt / smooth);
		_portraitParallaxOffset = Vector2.Lerp(_portraitParallaxOffset, target, MathHelper.Clamp(alpha, 0f, 1f));
	}
	public void DrawEntity(Entity entity, float opacityMultiplier = 1f)
	{
		var ui = entity?.GetComponent<UIElement>(); if (ui == null || ui.IsHidden || ui.Bounds.IsEmpty) return;
		var slot = entity.GetComponent<ClimbSlotPresentation>(); var (offset, alpha) = ClimbV2Draw.Motion(entity, opacityMultiplier);
		var rect = ClimbV2Draw.Shift(ClimbV2Draw.ResolveBounds(EntityManager, entity), offset);
		rect = ClimbV2Draw.ApplyChoiceHover(rect, slot.Kind, ui.IsHovered);
		if (opacityMultiplier >= 0.999f)
		{
			ClimbV2Draw.SoftRoundedShadow(_batch, _assets, rect, 150, 150, 18, 18, 20, 21, 0.48f, alpha);
			ClimbV2Draw.SoftRoundedShadow(_batch, _assets, rect, 150, 150, 18, 18, 5, 8, 0.32f, alpha);
			_batch.Draw(_assets.GetRoundedRectPerCorner(rect.Width, rect.Height, 150, 150, 18, 18), rect, new Color(8, 9, 10) * (0.82f * alpha));
		}
		var art = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height - 108);
		var background = _assets.TryGetTexture(BattleLocationAssetService.GetBackgroundAsset(slot.BattleLocation));
		if (background != null) ClimbV2Draw.Cover(_batch, background, art, Color.White * alpha);
		var portrait = _assets.TryGetTexture(slot.PortraitAsset);
		if (portrait != null)
		{
			ClimbSceneDrawHelpers.DrawPortraitCropped(
				_batch, portrait, art, PortraitCropTopBias, _portraitParallaxOffset, PortraitParallaxZoom, alpha);
		}
		_batch.Draw(_pixel, art, Color.Black * (0.08f * alpha));
		Color body = slot.BattleLocation switch { BattleLocation.Gothic => new Color(38, 22, 48), BattleLocation.Jungle => new Color(18, 42, 28), BattleLocation.Volcano => new Color(58, 18, 25), BattleLocation.Tundra => new Color(22, 37, 60), _ => new Color(53, 41, 27) };
		var bodyRect = new Rectangle(rect.X, rect.Bottom - 136, rect.Width, 136); _batch.Draw(_pixel, bodyRect, body * (0.96f * alpha));
		ClimbV2Draw.Text(_batch, _font, slot.Title, new Vector2(rect.X + 18, bodyRect.Y + 18), 0.20f, Color.White * alpha);
		ClimbV2Draw.Border(_batch, _pixel, rect, Color.Black * (0.4f * alpha));
	}

	internal static Vector2 ComputePortraitParallaxTarget(Vector2 cursor, bool hasCursor, float multiplierX, float multiplierY, float maxOffset)
	{
		if (!hasCursor) return Vector2.Zero;
		var center = new Vector2(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f);
		var target = new Vector2((center.X - cursor.X) * multiplierX, (center.Y - cursor.Y) * multiplierY);
		float max = Math.Max(0f, maxOffset);
		float length = target.Length();
		return length <= max || length <= 0f ? target : target * (max / length);
	}

	public void Shutdown() => EventManager.Unsubscribe(_cursorHandler);
}

[DebugTab("Climb V2 Events")]
public sealed class EventDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch; private readonly Texture2D _pixel; private readonly Texture2D _glyphCircle; private readonly ImageAssetService _assets; private readonly SpriteFont _title = FontSingleton.TitleFont; private readonly SpriteFont _body = FontSingleton.ChakraPetchFont;
	public EventDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em) { _batch = batch; _assets = assets; _pixel = assets.GetPixel(Color.White); _glyphCircle = assets.GetAntiAliasedCircle(25); }
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbEventPresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void DrawEntity(Entity entity, float opacityMultiplier = 1f)
	{
		var ui = entity?.GetComponent<UIElement>(); if (ui == null || ui.IsHidden || ui.Bounds.IsEmpty) return;
		var slot = entity.GetComponent<ClimbSlotPresentation>(); var item = entity.GetComponent<ClimbEventPresentation>(); var (offset, alpha) = ClimbV2Draw.Motion(entity, opacityMultiplier);
		var rect = ClimbV2Draw.Shift(ClimbV2Draw.ResolveBounds(EntityManager, entity), offset);
		rect = ClimbV2Draw.ApplyChoiceHover(rect, slot.Kind, ui.IsHovered);
		ClimbV2Draw.SoftRoundedShadow(_batch, _assets, rect, 3, 3, 3, 3, 14, 18, 0.48f, alpha);
		ClimbV2Draw.SoftRoundedShadow(_batch, _assets, rect, 3, 3, 3, 3, 4, 7, 0.26f, alpha);
		Color fill = slot.EventKind == ClimbEventKind.Character ? new Color(63, 15, 29) : new Color(47, 33, 22);
		_batch.Draw(_assets.GetRoundedRect(rect.Width, rect.Height, 3), rect, fill * (0.94f * alpha));
		int textX;
		if (slot.EventKind == ClimbEventKind.Character)
		{
			var portrait = _assets.TryGetTexture(slot.PortraitAsset);
			if (portrait != null)
			{
				var portraitBounds = ClimbV2Draw.Contain(portrait, new Rectangle(rect.X + 7, rect.Y + 18, 100, 155));
				_batch.Draw(portrait, portraitBounds, Color.White * alpha);
			}
			textX = rect.X + 115;
		}
		else
		{
			var glyph = new Rectangle(rect.X + 24, rect.Y + 24, 50, 50);
			_batch.Draw(_glyphCircle, glyph, Color.White * (0.28f * alpha));
			_batch.Draw(_glyphCircle, new Rectangle(glyph.X + 1, glyph.Y + 1, glyph.Width - 2, glyph.Height - 2), fill * alpha);
			ClimbV2Draw.Text(_batch, _title, "?", new Vector2(glyph.X + 13, glyph.Y - 3), 0.27f, Color.White * alpha); textX = rect.X + 95;
		}
		ClimbV2Draw.FittedText(_batch, _title, slot.Title, new Vector2(textX, rect.Y + 19), 0.18f, rect.Right - textX - 16, Color.White * alpha);
		if (slot.EventKind == ClimbEventKind.Hazard)
		{
			ClimbV2Draw.Text(_batch, _body, "Something waits beyond the", new Vector2(textX, rect.Y + 62), 0.085f, ClimbV2Draw.Paper * alpha);
			ClimbV2Draw.Text(_batch, _body, "road. Its nature", new Vector2(textX, rect.Y + 83), 0.085f, ClimbV2Draw.Paper * alpha);
			float prefix = _body.MeasureString("road. Its nature").X * 0.085f;
			_batch.Draw(_pixel, new Rectangle((int)(textX + prefix + 2), rect.Y + 93, 10, 2), ClimbV2Draw.Paper * alpha);
			ClimbV2Draw.Text(_batch, _body, "and its price", new Vector2(textX + prefix + 14, rect.Y + 83), 0.085f, ClimbV2Draw.Paper * alpha);
			ClimbV2Draw.Text(_batch, _body, "remain hidden.", new Vector2(textX, rect.Y + 104), 0.085f, ClimbV2Draw.Paper * alpha);
		}
		else ClimbV2Draw.Wrapped(_batch, _body, item.Description, new Rectangle(textX, rect.Y + 62, rect.Right - textX - 16, 55), 0.082f, ClimbV2Draw.Paper * alpha, 3);
	}
}

[DebugTab("Climb V2 Choice Rail")]
public sealed class ChoiceStatsRailDisplaySystem : Core.System
{
	private readonly SpriteBatch _batch; private readonly GraphicsDevice _gd; private readonly Texture2D _pixel; private readonly Texture2D _pipCircle; private readonly SpriteFont _font = FontSingleton.ChakraPetchFont;
	public ChoiceStatsRailDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch batch, ImageAssetService assets) : base(em) { _batch = batch; _gd = gd; _pixel = assets.GetPixel(Color.White); _pipCircle = assets.GetAntiAliasedCircle(4); ClimbSceneDrawHelpers.EnsureResourceTextures(assets); }
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbChoiceRailPresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	public void DrawForSource(Entity source, float opacityMultiplier = 1f)
	{
		var slot = source?.GetComponent<ClimbSlotPresentation>(); if (slot == null) return;
		var railEntity = GetRelevantEntities().FirstOrDefault(e => string.Equals(e.GetComponent<ClimbChoiceRailPresentation>()?.SourceSlotId, slot.SlotId, StringComparison.OrdinalIgnoreCase));
		var ui = railEntity?.GetComponent<UIElement>(); if (ui == null || ui.IsHidden || ui.Bounds.IsEmpty) return;
		var rail = railEntity.GetComponent<ClimbChoiceRailPresentation>(); var (offset, alpha) = ClimbV2Draw.Motion(source, opacityMultiplier);
		var rect = ClimbV2Draw.Shift(ClimbV2Draw.ResolveBounds(EntityManager, railEntity), offset);
		rect = ClimbV2Draw.ApplyChoiceHover(rect, slot.Kind, source.GetComponent<UIElement>()?.IsHovered == true);
		_batch.Draw(_pixel, rect, new Color(5, 6, 7) * (0.76f * alpha)); ClimbV2Draw.Border(_batch, _pixel, rect, Color.White * (0.17f * alpha));
		int timeWidth = rail.ShowTime ? 58 : 0;
		int staysWidth = rail.Stays >= 0 ? 64 : 0;
		int outcomeWidth = rect.Width - timeWidth - staysWidth;
		if (rail.OutcomeKind == ClimbChoiceRailOutcomeKind.None)
		{
			outcomeWidth = 0;
			if (rail.ShowTime && rail.Stays >= 0)
			{
				timeWidth = rect.Width / 2;
				staysWidth = rect.Width - timeWidth;
			}
			else if (rail.ShowTime) timeWidth = rect.Width;
			else if (rail.Stays >= 0) staysWidth = rect.Width;
		}
		if (rail.OutcomeKind != ClimbChoiceRailOutcomeKind.None) DrawOutcome(new Rectangle(rect.X, rect.Y, outcomeWidth, rect.Height), rail, alpha);
		int timeX = rect.X + Math.Max(0, outcomeWidth);
		if (rail.ShowTime) DrawPips(new Rectangle(timeX, rect.Y, timeWidth, rect.Height), "TIME", rail.Time, rail.Time, Color.White, alpha);
		if (rail.Stays >= 0) DrawPips(new Rectangle(timeX + timeWidth, rect.Y, staysWidth, rect.Height), "STAYS", rail.Stays, rail.ProjectedStays, ClimbV2Draw.Red, alpha);
	}
	private void DrawOutcome(Rectangle rect, ClimbChoiceRailPresentation rail, float alpha)
	{
		ClimbV2Draw.Text(_batch, _font, rail.OutcomeKind == ClimbChoiceRailOutcomeKind.Price ? "PRICE" : "REWARD", new Vector2(rect.X + 7, rect.Y + 4), 0.062f, ClimbV2Draw.Muted * alpha);
		int x = rect.X + 7; int y = rect.Y + 20;
		ClimbV2Draw.Text(_batch, _font, rail.OutcomeKind == ClimbChoiceRailOutcomeKind.Price ? "-" : "+", new Vector2(x, y), 0.082f, (rail.OutcomeKind == ClimbChoiceRailOutcomeKind.Price ? new Color(255, 172, 184) : ClimbV2Draw.Green) * alpha); x += 11;
		DrawResource(ref x, y, ClimbResourceType.Red, rail.Resources.red, alpha); DrawResource(ref x, y, ClimbResourceType.White, rail.Resources.white, alpha); DrawResource(ref x, y, ClimbResourceType.Black, rail.Resources.black, alpha);
	}
	private void DrawResource(ref int x, int y, ClimbResourceType type, int amount, float alpha)
	{
		if (amount <= 0) return; ClimbSceneDrawHelpers.DrawResourceIcon(_batch, _gd, _pixel, new Vector2(x, y - 1), type, 19, Color.White, opacity: alpha); x += 20; ClimbV2Draw.Text(_batch, _font, amount.ToString(), new Vector2(x, y), 0.082f, Color.White * alpha); x += 14;
	}
	private void DrawPips(Rectangle rect, string label, int count, int active, Color color, float alpha)
	{
		_batch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Color.White * (0.13f * alpha)); ClimbV2Draw.Text(_batch, _font, label, new Vector2(rect.X + 7, rect.Y + 4), 0.062f, ClimbV2Draw.Muted * alpha);
		for (int i = 0; i < Math.Max(0, count); i++)
		{
			var pip = new Rectangle(rect.X + 7 + i * 9, rect.Y + 24, 8, 8);
			if (i < active)
			{
				_batch.Draw(_pipCircle, pip, color * (0.18f * alpha));
				_batch.Draw(_pipCircle, new Rectangle(pip.X + 1, pip.Y + 1, 6, 6), color * alpha);
			}
			else
			{
				_batch.Draw(_pipCircle, pip, color * (0.62f * alpha));
				_batch.Draw(_pipCircle, new Rectangle(pip.X + 2, pip.Y + 2, 4, 4), new Color(5, 6, 7) * alpha);
			}
		}
	}
}

[DebugTab("Climb V2 Choice Preview")]
public sealed class ClimbChoicePreviewDisplaySystem : Core.System
{
	[DebugEditable(DisplayName = "Expiry Pulse Seconds", Step = 0.01f, Min = 0.2f, Max = 4f)]
	public float PulseSeconds { get; set; } = 1.5f;
	[DebugEditable(DisplayName = "Expiry Minimum Opacity", Step = 0.01f, Min = 0f, Max = 1f)]
	public float MinimumOpacity { get; set; } = 0.7f;
	[DebugEditable(DisplayName = "Expiry Maximum Grayscale", Step = 0.01f, Min = 0f, Max = 1f)]
	public float MaximumGrayscale { get; set; } = 0.8f;
	[DebugEditable(DisplayName = "Expiry Restore Seconds", Step = 0.01f, Min = 0.01f, Max = 1f)]
	public float RestoreSeconds { get; set; } = 0.01f;

	public ClimbChoicePreviewDisplaySystem(EntityManager em, SpriteBatch batch, ImageAssetService assets) : base(em) { }
	protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<ClimbSlotPresentation>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime)
	{
		var preview = EntityManager.GetEntity(ClimbV2LayoutSystem.RootName)?.GetComponent<ClimbPreviewState>();
		var slot = entity.GetComponent<ClimbSlotPresentation>();
		var visual = entity.GetComponent<ClimbChoiceExpiryPreviewPresentation>();
		if (visual == null)
		{
			visual = new ClimbChoiceExpiryPreviewPresentation();
			EntityManager.AddComponent(entity, visual);
		}

		bool targeted = preview?.IsActive == true
			&& slot != null
			&& !string.Equals(slot.SlotId, preview.SourceSlotId, StringComparison.OrdinalIgnoreCase)
			&& preview.WouldVanishSlotIds.Contains(slot.SlotId);
		float dt = Math.Max(0f, (float)(gameTime?.ElapsedGameTime.TotalSeconds ?? 0d));
		if (targeted)
		{
			if (!visual.IsActive) visual.PulseElapsedSeconds = 0f;
			visual.IsActive = true;
			visual.PulseElapsedSeconds += dt;
			float period = Math.Max(0.01f, PulseSeconds);
			float phase = MathHelper.TwoPi * (visual.PulseElapsedSeconds % period) / period;
			visual.Strength = 0.5f - 0.5f * MathF.Cos(phase);
		}
		else
		{
			visual.IsActive = false;
			visual.PulseElapsedSeconds = 0f;
			visual.Strength = Math.Max(0f, visual.Strength - dt / Math.Max(0.01f, RestoreSeconds));
		}

		visual.OpacityMultiplier = MathHelper.Lerp(1f, MathHelper.Clamp(MinimumOpacity, 0f, 1f), visual.Strength);
		visual.Grayscale = MathHelper.Clamp(MaximumGrayscale, 0f, 1f) * visual.Strength;
	}
}
