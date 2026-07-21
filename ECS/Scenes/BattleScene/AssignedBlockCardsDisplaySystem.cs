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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Read-only renderer for the assigned-block rail and its card/equipment occupants.
	/// </summary>
	[DebugTab("Assigned Block Rail")]
	public sealed class AssignedBlockCardsDisplaySystem : Core.System
	{
		private static readonly Vector2[] RailShape =
		{
			new(0.025f, 0f), new(0.975f, 0f), new(1f, 0.20f), new(0.985f, 0.80f),
			new(0.96f, 1f), new(0.04f, 1f), new(0.015f, 0.80f), new(0f, 0.20f),
		};
		private static readonly Vector2[] ShieldShape =
		{
			new(0.08f, 0.08f), new(0.50f, 0f), new(0.92f, 0.08f), new(0.86f, 0.68f),
			new(0.50f, 1f), new(0.14f, 0.68f),
		};

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly SpriteFont _font = FontSingleton.ContentFont;
		private Texture2D _pixel;

		[DebugEditable(DisplayName = "Rail Border", Step = 1, Min = 1, Max = 12)]
		public int RailBorder { get; set; } = 3;
		[DebugEditable(DisplayName = "Seal Width", Step = 1, Min = 20, Max = 80)]
		public int SealWidth { get; set; } = 34;
		[DebugEditable(DisplayName = "Seal Height", Step = 1, Min = 20, Max = 100)]
		public int SealHeight { get; set; } = 40;
		[DebugEditable(DisplayName = "Seal Offset X", Step = 1, Min = -80, Max = 80)]
		public int SealOffsetX { get; set; } = 8;
		[DebugEditable(DisplayName = "Seal Offset Y", Step = 1, Min = -80, Max = 80)]
		public int SealOffsetY { get; set; } = 10;
		[DebugEditable(DisplayName = "Seal Text Fill", Step = 0.01f, Min = 0.1f, Max = 1f)]
		public float SealTextFill { get; set; } = 0.64f;
		[DebugEditable(DisplayName = "Equipment Icon Size", Step = 1, Min = 12, Max = 100)]
		public int EquipmentIconSize { get; set; } = 38;

		public AssignedBlockCardsDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			_pixel = CreatePixel();
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
		}

		protected override IEnumerable<Entity> GetRelevantEntities() =>
			EntityManager.GetEntitiesWithComponent<AssignedBlockCard>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			if (!EnemyAttackFlowService.HasCurrentAttack(EntityManager) || _pixel == null) return;
			var entities = GetRelevantEntities()
				.Where(entity => entity.GetComponent<AssignedBlockPresentation>() != null)
				.OrderBy(entity => entity.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.ToList();
			if (entities.Count == 0) return;

			DrawRail();
			foreach (var entity in entities)
			{
				var assignment = entity.GetComponent<AssignedBlockCard>();
				var presentation = entity.GetComponent<AssignedBlockPresentation>();
				if (assignment == null || presentation == null || presentation.RenderBounds == Rectangle.Empty) continue;
				if (!assignment.IsEquipment && presentation.Phase == AssignedBlockPresentation.PhaseState.Returning) continue;

				if (entity.GetComponent<UIElement>()?.IsHovered == true)
					DrawHoverFrame(presentation.RenderBounds, assignment.DisplayFgColor);

				if (!assignment.IsEquipment && entity.GetComponent<CardData>() != null)
				{
					EventManager.Publish(new CardRenderScaledEvent
					{
						Card = entity,
						Position = presentation.RenderPos,
						Scale = presentation.CurrentScale,
						Rotation = presentation.CurrentRotation,
					});
				}
				else
				{
					DrawEquipmentCrest(presentation.RenderBounds, entity, assignment);
				}
				DrawBlockSeal(presentation.RenderBounds, entity, assignment);
			}
		}

		private void DrawRail()
		{
			var rail = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>()
				.FirstOrDefault()?.GetComponent<AssignedBlockRailPresentation>();
			if (rail == null || rail.Bounds == Rectangle.Empty) return;

			var mask = PrimitiveTextureFactory.GetAntialiasedPolygonMask(
				_graphicsDevice, 320, 64, "assigned-block-gothic-rail-v1", RailShape);
			Color flash = Color.Lerp(new Color(74, 11, 20), new Color(196, 145, 52), MathHelper.Clamp(rail.Flash, 0f, 1f));
			_spriteBatch.Draw(mask, rail.Bounds, flash * 0.92f);
			var inner = Inflate(rail.Bounds, -Math.Max(1, RailBorder));
			_spriteBatch.Draw(mask, inner, new Color(12, 8, 13, 224));

			int y = rail.Bounds.Center.Y;
			_spriteBatch.Draw(_pixel, new Rectangle(rail.Bounds.X + 14, y - 1, Math.Max(1, rail.Bounds.Width - 28), 2), new Color(133, 21, 35, 210));
			_spriteBatch.Draw(_pixel, new Rectangle(rail.Bounds.X + 24, y + 3, Math.Max(1, rail.Bounds.Width - 48), 1), new Color(207, 156, 65, 125));
		}

		private void DrawEquipmentCrest(Rectangle bounds, Entity entity, AssignedBlockCard assignment)
		{
			var (background, foreground) = GetColors(entity, assignment);
			var shield = PrimitiveTextureFactory.GetAntialiasedPolygonMask(
				_graphicsDevice, 96, 120, "assigned-block-equipment-crest-v1", ShieldShape);
			_spriteBatch.Draw(shield, Inflate(bounds, 3), foreground * 0.95f);
			_spriteBatch.Draw(shield, bounds, Color.Lerp(background, new Color(18, 12, 17), 0.32f));

			var equipment = entity.GetComponent<EquippedEquipment>()?.Equipment;
			var icon = EquipmentArtService.GetTexture(_imageAssets, equipment);
			if (icon == null) return;
			int size = Math.Min(Math.Min(bounds.Width - 12, bounds.Height - 18), Math.Max(12, EquipmentIconSize));
			var destination = EquipmentArtService.GetContainedBounds(
				icon,
				new Rectangle(bounds.Center.X - size / 2, bounds.Center.Y - size / 2 - 4, size, size));
			_spriteBatch.Draw(icon, destination, Color.White);
		}

		private void DrawBlockSeal(Rectangle occupantBounds, Entity entity, AssignedBlockCard assignment)
		{
			if (_font == null) return;
			var (background, foreground) = GetColors(entity, assignment);
			var seal = new Rectangle(
				occupantBounds.Right - SealWidth + SealOffsetX,
				occupantBounds.Bottom - SealHeight + SealOffsetY,
				Math.Max(1, SealWidth),
				Math.Max(1, SealHeight));
			var shield = PrimitiveTextureFactory.GetAntialiasedPolygonMask(
				_graphicsDevice, 48, 56, "assigned-block-value-seal-v1", ShieldShape);
			_spriteBatch.Draw(shield, Inflate(seal, 2), foreground);
			_spriteBatch.Draw(shield, seal, background);

			string text = assignment.BlockAmount.ToString();
			Vector2 measured = _font.MeasureString(text);
			if (measured.X <= 0f || measured.Y <= 0f) return;
			float fill = MathHelper.Clamp(SealTextFill, 0.1f, 1f);
			float scale = Math.Min(seal.Width * fill / measured.X, seal.Height * fill / measured.Y);
			Vector2 size = measured * scale;
			var position = new Vector2(seal.Center.X - size.X * 0.5f, seal.Center.Y - size.Y * 0.55f);
			_spriteBatch.DrawString(_font, text, position, foreground, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void DrawHoverFrame(Rectangle bounds, Color color)
		{
			Color glow = Color.Lerp(color, new Color(226, 174, 72), 0.65f) * 0.8f;
			var frame = Inflate(bounds, 4);
			_spriteBatch.Draw(_pixel, new Rectangle(frame.X, frame.Y, frame.Width, 2), glow);
			_spriteBatch.Draw(_pixel, new Rectangle(frame.X, frame.Bottom - 2, frame.Width, 2), glow);
			_spriteBatch.Draw(_pixel, new Rectangle(frame.X, frame.Y, 2, frame.Height), glow);
			_spriteBatch.Draw(_pixel, new Rectangle(frame.Right - 2, frame.Y, 2, frame.Height), glow);
		}

		private (Color Background, Color Foreground) GetColors(Entity entity, AssignedBlockCard assignment)
		{
			if (entity.HasComponent<Colorless>())
				return (new Color(92, 96, 102), new Color(235, 235, 235));
			return (assignment.DisplayBgColor, assignment.DisplayFgColor);
		}

		private void OnDeleteCaches(DeleteCachesEvent evt)
		{
			_pixel?.Dispose();
			_pixel = CreatePixel();
		}

		private Texture2D CreatePixel()
		{
			var texture = new Texture2D(_graphicsDevice, 1, 1);
			texture.SetData(new[] { Color.White });
			return texture;
		}

		private static Rectangle Inflate(Rectangle rectangle, int amount)
		{
			return new Rectangle(
				rectangle.X - amount,
				rectangle.Y - amount,
				Math.Max(1, rectangle.Width + amount * 2),
				Math.Max(1, rectangle.Height + amount * 2));
		}
	}
}
