using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Singletons;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Archival player HUD renderer. PlayerHudActionPointDisplaySystem is the registered implementation.
	///
	/// Draws the player's Action Points as text "{number}AP" centered inside a black AA circle
	/// positioned to the right of the discard pile panel.
	/// </summary>
	[DebugTab("Action Points")]
	public class ActionPointDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.ContentFont;

		// Layout settings
		[DebugEditable(DisplayName = "Circle Radius", Step = 1, Min = 8, Max = 400)]
		public int CircleRadius { get; set; } = 40;

		[DebugEditable(DisplayName = "Right Padding From Discard", Step = 1, Min = -200, Max = 400)]
		public int RightPaddingFromDiscard { get; set; } = 15;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.1f, Max = 5f)]
		public float TextScale { get; set; } = 0.2f;

		public ActionPointDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;

			EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
			EventManager.Subscribe<ModifyActionPointsEvent>(OnModifyActionPoints);
		}

		private void OnModifyActionPoints(ModifyActionPointsEvent evt)
		{
			LoggingService.Append("ActionPointDisplaySystem.OnModifyActionPoints", new JsonObject {
				{ "Delta", evt.Delta }
			});
			var apHover = EntityManager.GetEntity("UI_APTooltip");
			if (apHover != null)
			{
				EventManager.Publish(new JigglePulseEvent { Target = apHover, Config = JigglePulseConfig.Default });
			}
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Player>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) 
		{ 

		}

		private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
		{
			LoggingService.Append("ActionPointDisplaySystem.OnChangeBattlePhaseEvent", new JsonObject {
				{ "Current", evt.Current.ToString() },
				{ "Previous", evt.Previous.ToString() }
			});
			var apHover = EntityManager.GetEntity("UI_APTooltip");
			var ui = apHover.GetComponent<UIElement>();
			if (evt.Current == SubPhase.EnemyStart)
			{
				ui.IsHidden = true;
			}
			else
			{
				ui.IsHidden = false;
			}
		}

		public void Draw()
		{
			// Only render during Action phase
			var phaseEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			if (phaseEntity == null) return;
			var phase = phaseEntity.GetComponent<PhaseState>();
			if (phase.Main != MainPhase.PlayerTurn) return;
			var player = GetRelevantEntities().FirstOrDefault();
			if (player == null) return;
			var t = player.GetComponent<Transform>();
			var ap = player.GetComponent<ActionPoints>();
			if (t == null || ap == null) return;

			int count = System.Math.Max(0, ap.Current);

			// Position relative to discard pile
			var discardRoot = EntityManager.GetEntity("UI_DiscardPileRoot");
			Rectangle? discardRect = null;
			if (discardRoot != null)
			{
				var uiDP = discardRoot.GetComponent<UIElement>();
				if (uiDP != null && uiDP.Bounds.Width > 0 && uiDP.Bounds.Height > 0) discardRect = uiDP.Bounds;
			}
			// Update hoverable UI element for tooltip (entity pre-created in factory as UI_APTooltip)
			var apHover = EntityManager.GetEntity("UI_APTooltip");
			float rotation = 0f;
			Vector2 scale = Vector2.One;
			if (apHover != null)
			{
				var ht = apHover.GetComponent<Transform>();
				if (ht != null)
				{
					rotation = ht.Rotation;
					scale = ht.Scale;
				}
			}

			int r = System.Math.Max(4, CircleRadius);
			var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, r);
			Vector2 center;
			if (discardRect.HasValue)
			{
				var dr = discardRect.Value;
				center = new Vector2(dr.Right + System.Math.Max(-200, RightPaddingFromDiscard) + r, dr.Y + dr.Height / 2f);
			}
			else
			{
				center = new Vector2(120, Game1.VirtualHeight - 120);
			}

			// Draw black filled circle
			_spriteBatch.Draw(
				circle, 
				position: center, 
				sourceRectangle: null, 
				color: Color.Black, 
				rotation: rotation, 
				origin: new Vector2(r, r), 
				scale: scale, 
				effects: SpriteEffects.None, 
				layerDepth: 0f
			);

			// Draw text {number}AP centered
			if (_font != null)
			{
				string label = $"{count}AP";
				float ts = System.Math.Max(0.1f, TextScale);
				Vector2 origin = _font.MeasureString(label) / 2f;
				_spriteBatch.DrawString(_font, label, center, Color.White, rotation, origin, ts * scale, SpriteEffects.None, 0f);
			}

			if (apHover != null)
			{
				var ui = apHover.GetComponent<UIElement>();
				var ht = apHover.GetComponent<Transform>();
				var hitRect = new Rectangle((int)(center.X - r), (int)(center.Y - r), r * 2, r * 2);
				if (ui != null)
				{
					ui.Bounds = hitRect;
					ui.Tooltip = $"{count} Action Point{(count == 1 ? "" : "s")}";
				}
				if (ht != null)
				{
					ht.Position = new Vector2(hitRect.X, hitRect.Y);
				}
			}
		}
	}
}


