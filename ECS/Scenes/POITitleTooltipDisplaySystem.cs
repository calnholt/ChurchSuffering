using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("POI Title Tooltip")]
	public class POITitleTooltipDisplaySystem : Core.System
	{
		private const string TooltipEntityName = "UI_POITitleTooltip";
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.TitleFont;
		private Entity _tooltipEntity;

		[DebugEditable(DisplayName = "Padding", Step = 1, Min = 0, Max = 40)]
		public int Padding { get; set; } = 10;
		[DebugEditable(DisplayName = "Gap", Step = 1, Min = 0, Max = 120)]
		public int Gap { get; set; } = 18;
		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float TextScale { get; set; } = 0.25f;
		[DebugEditable(DisplayName = "Trapezoid Height", Step = 2, Min = 20, Max = 300)]
		public int TrapezoidHeight { get; set; } = 64;
		[DebugEditable(DisplayName = "Left Side Offset", Step = 1, Min = 0, Max = 120)]
		public int LeftSideOffset { get; set; } = 16;
		[DebugEditable(DisplayName = "Top Edge Angle", Step = 1f, Min = -45f, Max = 45f)]
		public float TopEdgeAngleDegrees { get; set; } = 2f;
		[DebugEditable(DisplayName = "Right Edge Angle", Step = 1f, Min = -45f, Max = 45f)]
		public float RightEdgeAngleDegrees { get; set; } = -26f;
		[DebugEditable(DisplayName = "Bottom Edge Angle", Step = 1f, Min = -45f, Max = 45f)]
		public float BottomEdgeAngleDegrees { get; set; } = -2f;
		[DebugEditable(DisplayName = "Left Edge Angle", Step = 1f, Min = -45f, Max = 45f)]
		public float LeftEdgeAngleDegrees { get; set; } = 9f;

		public POITitleTooltipDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene == null || scene.Current == SceneId.Battle || scene.Current == SceneId.TitleMenu)
			{
				DestroyTooltip();
				return;
			}

			if (StateSingleton.IsActive || StateSingleton.PreventClicking)
			{
				DestroyTooltip();
				return;
			}

			var hovered = EntityManager.GetEntitiesWithComponent<UIElement>()
				.Select(e => new
				{
					Entity = e,
					UI = e.GetComponent<UIElement>(),
					Transform = e.GetComponent<Transform>(),
					Source = e.GetComponent<POITitleTooltipSource>()
				})
				.Where(x => x.UI != null
					&& !x.UI.IsHidden
					&& x.UI.IsHovered
					&& x.Source != null
					&& !string.IsNullOrWhiteSpace(x.Source.Title))
				.OrderByDescending(x => x.Transform?.ZOrder ?? 0)
				.FirstOrDefault();

			if (hovered == null)
			{
				DestroyTooltip();
				return;
			}

			string title = hovered.Source.Title;
			int pad = System.Math.Max(0, Padding);
			var size = _font.MeasureString(title) * TextScale;
			int width = (int)System.Math.Ceiling(size.X) + pad * 2 + System.Math.Max(0, LeftSideOffset);
			int height = System.Math.Max(24, TrapezoidHeight);
			var r = TransformResolverService.ResolveUIBounds(EntityManager, hovered.Entity, hovered.UI);
			int viewportW = Game1.VirtualWidth;
			int viewportH = Game1.VirtualHeight;

			int rightSpace = viewportW - (r.Right + Gap);
			int leftSpace = r.Left - Gap;
			bool canPlaceRight = rightSpace >= width;
			bool canPlaceLeft = leftSpace >= width;
			bool preferRight = r.Center.X < viewportW / 2;
			bool placeRight = canPlaceRight || (!canPlaceLeft && preferRight);
			if (!canPlaceRight && canPlaceLeft) placeRight = false;

			int rx = placeRight ? (r.Right + Gap + 35) : (r.Left - Gap - width - 35);
			int ry = r.Y + (r.Height - height) / 2;
			rx = System.Math.Max(0, System.Math.Min(rx, viewportW - width));
			ry = System.Math.Max(0, System.Math.Min(ry, viewportH - height));
			var rect = new Rectangle(rx, ry, width, height);

			if (_tooltipEntity == null)
			{
				_tooltipEntity = EntityManager.CreateEntity(TooltipEntityName);
				EntityManager.AddComponent(_tooltipEntity, new Transform { Position = new Vector2(rect.X, rect.Y), ZOrder = 10001 });
				EntityManager.AddComponent(_tooltipEntity, new UIElement
				{
					Bounds = rect,
					IsInteractable = false,
					IsHidden = false,
					TooltipType = TooltipType.None,
					ShowHoverHighlight = false
				});
				EntityManager.AddComponent(_tooltipEntity, new Hint { Text = title });
			}
			else
			{
				var t = _tooltipEntity.GetComponent<Transform>();
				if (t != null)
				{
					t.Position = new Vector2(rect.X, rect.Y);
					t.ZOrder = 10001;
				}
				var ui = _tooltipEntity.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.Bounds = rect;
					ui.IsHidden = false;
				}
				var hint = _tooltipEntity.GetComponent<Hint>();
				if (hint != null) hint.Text = title;
			}
		}

		public void Draw()
		{
			if (_tooltipEntity == null) return;

			var ui = _tooltipEntity.GetComponent<UIElement>();
			var hint = _tooltipEntity.GetComponent<Hint>();
			if (ui == null || hint == null || ui.IsHidden) return;

			string title = string.IsNullOrEmpty(hint.Text) ? "POI" : hint.Text;
			var rect = ui.Bounds;
			var trap = PrimitiveTextureFactory.GetAntialiasedTrapezoid(
				_graphicsDevice,
				rect.Width,
				rect.Height,
				LeftSideOffset,
				TopEdgeAngleDegrees,
				RightEdgeAngleDegrees,
				BottomEdgeAngleDegrees,
				LeftEdgeAngleDegrees);
			_spriteBatch.Draw(trap, rect, Color.White);

			var size = _font.MeasureString(title) * TextScale;
			var pos = new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f);
			_spriteBatch.DrawString(_font, title, pos, Color.White, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
		}

		private void DestroyTooltip()
		{
			if (_tooltipEntity == null) return;
			EntityManager.DestroyEntity(_tooltipEntity.Id);
			_tooltipEntity = null;
		}
	}
}
