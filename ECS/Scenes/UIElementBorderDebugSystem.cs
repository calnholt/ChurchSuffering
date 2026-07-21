using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Singletons;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("UI Debug Borders")]
	public class UIElementBorderDebugSystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font;
		private readonly Texture2D _pixel;

		[DebugEditable(DisplayName = "Enabled", Step = 1)]
		public bool Enabled { get; set; } = false;

		[DebugEditable(DisplayName = "Inner Border Thickness", Step = 1, Min = 0, Max = 20)]
		public int InnerThickness { get; set; } = 0;

		[DebugEditable(DisplayName = "Outer Border Thickness", Step = 1, Min = 0, Max = 20)]
		public int OuterThickness { get; set; } = 2;

		[DebugEditable(DisplayName = "Show Entity Names", Step = 1)]
		public bool ShowNames { get; set; } = false;

		[DebugEditable(DisplayName = "Name Text Scale", Step = 0.01f, Min = 0.05f, Max = 2f)]
		public float NameTextScale { get; set; } = 0.12f;

		public UIElementBorderDebugSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_font = FontSingleton.ContentFont;
			_pixel = new Texture2D(_graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<UIElement>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			// draw-only system
		}

		public void Draw()
		{
			if (!Enabled) return;
			var entities = GetRelevantEntities()
				.Select(e => new { E = e, UI = e.GetComponent<UIElement>(), T = e.GetComponent<Transform>() })
				.Where(x => x.UI != null && x.UI.Bounds.Width > 0 && x.UI.Bounds.Height > 0 && x.UI.IsInteractable)
				.OrderBy(x => x.T?.ZOrder ?? 0)
				.ToList();

			foreach (var x in entities)
			{
				var r = TransformResolverService.ResolveUIBounds(EntityManager, x.E, x.UI);
				int inner = System.Math.Max(0, InnerThickness);
				int outer = System.Math.Max(0, OuterThickness);
				if (outer > 0)
				{
					DrawRect(r, Color.Red, outer);
				}
				if (inner > 0)
				{
					var inset = new Rectangle(r.X + outer + 1, r.Y + outer + 1, System.Math.Max(0, r.Width - (outer + 1) * 2), System.Math.Max(0, r.Height - (outer + 1) * 2));
					if (inset.Width > 0 && inset.Height > 0)
					{
						DrawRect(inset, Color.Red, inner);
					}
				}

				if (ShowNames && _font != null)
				{
					string name = x.E.Name ?? $"Entity_{x.E.Id}";
					var size = _font.MeasureString(name) * NameTextScale;
					int tx = r.X + (int)System.Math.Round((r.Width - size.X) / 2f);
					int ty = r.Y + (int)System.Math.Round((r.Height - size.Y) / 2f);
					_spriteBatch.DrawString(_font, name, new Vector2(tx, ty), Color.White, 0f, Vector2.Zero, NameTextScale, SpriteEffects.None, 0f);
				}
			}
		}

		private void DrawRect(Rectangle rect, Color color, int thickness)
		{
			if (thickness <= 0) return;
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}
	}
}

