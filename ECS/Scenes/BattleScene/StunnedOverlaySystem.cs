using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Singletons;

namespace ChurchSuffering.ECS.Systems
{
	public class StunnedOverlaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteFont _font = FontSingleton.ContentFont;
		private float _timer;

		public StunnedOverlaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			EventManager.Subscribe<ShowStunnedOverlay>(e => OnShowStunnedOverlay(e));
		}

		private void OnShowStunnedOverlay(ShowStunnedOverlay evt)
		{
			LoggingService.Append("StunnedOverlaySystem.OnShowStunnedOverlay", new System.Text.Json.Nodes.JsonObject());
			_timer = 1.0f;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Enemy>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			_timer = System.Math.Max(0f, _timer - (float)gameTime.ElapsedGameTime.TotalSeconds);
		}

		public void Draw()
		{
			if (_timer <= 0f || _font == null) return;
			var enemy = GetRelevantEntities().FirstOrDefault();
			if (enemy == null) return;
			var t = enemy.GetComponent<Transform>();
			var info = enemy.GetComponent<PortraitInfo>();
			if (t == null || info == null) return;
			string s = "Stunned!";
			var size = _font.MeasureString(s) * 0.8f;
			var textPos = new Vector2(t.Position.X - size.X / 2f, t.Position.Y - info.TextureHeight * info.CurrentScale / 2f - 40);
			_spriteBatch.DrawString(_font, s, textPos + new Vector2(2, 2), Color.Black * 0.6f, 0f, Vector2.Zero, 0.2f, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_font, s, textPos, Color.White, 0f, Vector2.Zero, 0.2f, SpriteEffects.None, 0f);
		}
	}
}


