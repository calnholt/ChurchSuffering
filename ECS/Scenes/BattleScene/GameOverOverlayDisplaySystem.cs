using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Displays a game-over overlay on run failure or abandon: centered text, fade to black,
	/// then clears run state (preserving meta), persists an inactive run, and returns to WayStation.
	/// </summary>
	[DebugTab("Game Over Overlay")]
	public class GameOverOverlayDisplaySystem : Core.System
	{
		private const string OverlayStateEntityName = "GameOverOverlayState";
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.TitleFont;
		private readonly Texture2D _pixel;
		private RunEndCause _pendingRunEndCause = RunEndCause.Death;

		// Configurable timings
		[DebugEditable(DisplayName = "Overlay Fade In (s)", Step = 0.05f, Min = 0.01f, Max = 5f)]
		public float OverlayFadeInSeconds { get; set; } = 0.35f;

		[DebugEditable(DisplayName = "Text Fade Out (s)", Step = 0.05f, Min = 0.05f, Max = 10f)]
		public float TextFadeOutSeconds { get; set; } = 2.75f;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.2f, Max = 3f)]
		public float TextScale { get; set; } = 0.625f;

		[DebugEditable(DisplayName = "Text Offset X", Step = 1f, Min = -1000f, Max = 1000f)]
		public float TextOffsetX { get; set; } = 0f;

		[DebugEditable(DisplayName = "Text Offset Y", Step = 1f, Min = -1000f, Max = 1000f)]
		public float TextOffsetY { get; set; } = 0f;

		public string MessageText { get; set; } = "Thy Will Be Done";

		public GameOverOverlayDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_pixel = new Texture2D(gd, 1, 1);
			_pixel.SetData(new[] { Color.White });
			EventManager.Subscribe<PlayerDied>(_ => BeginRunEndOverlay(RunEndCause.Death));
			EventManager.Subscribe<RunEndSequenceRequested>(evt => BeginRunEndOverlay(evt?.Cause ?? RunEndCause.Abandon));
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCachesEvent);
		}

		private void OnDeleteCachesEvent(DeleteCachesEvent evt)
		{
			LoggingService.Append("GameOverOverlayDisplaySystem.OnDeleteCachesEvent", new System.Text.Json.Nodes.JsonObject());
			ResetOverlayState();
			StateSingleton.PreventClicking = false;
		}

		private void BeginRunEndOverlay(RunEndCause cause)
		{
			if (TestFightRuntime.IsActive) return;
			var state = EnsureOverlayState();
			if (state.IsActive) return;
			LoggingService.Append("GameOverOverlayDisplaySystem.BeginRunEndOverlay", new System.Text.Json.Nodes.JsonObject());
			_pendingRunEndCause = cause;
			state.IsActive = true;
			state.Elapsed = 0f;
			state.SceneSwitched = false;
			StateSingleton.PreventClicking = true;
		}

		public static bool IsOverlayActive(EntityManager entityManager)
		{
			return entityManager.GetEntitiesWithComponent<GameOverOverlayState>()
				.Any(entity => entity.GetComponent<GameOverOverlayState>()?.IsActive == true);
		}

		private GameOverOverlayState EnsureOverlayState()
		{
			var entity = EntityManager.GetEntity(OverlayStateEntityName);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(OverlayStateEntityName);
				EntityManager.AddComponent(entity, new GameOverOverlayState());
			}
			return entity.GetComponent<GameOverOverlayState>();
		}

		private GameOverOverlayState GetOverlayState()
		{
			return EntityManager.GetEntity(OverlayStateEntityName)?.GetComponent<GameOverOverlayState>();
		}

		private void ResetOverlayState()
		{
			var state = GetOverlayState();
			if (state == null) return;
			state.IsActive = false;
			state.Elapsed = 0f;
			state.SceneSwitched = false;
			_pendingRunEndCause = RunEndCause.Death;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			// Tie updates to scene presence; we only need one entity to tick per frame
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var state = GetOverlayState();
			if (state?.IsActive != true) return;
			state.Elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
			float total = System.Math.Max(0.0001f, OverlayFadeInSeconds) + System.Math.Max(0.0001f, TextFadeOutSeconds);
			if (!state.SceneSwitched && state.Elapsed >= total)
			{
				if (GuidedTutorialService.IsActive(EntityManager))
				{
					GuidedTutorialService.RestartSection(EntityManager);
				}
				else
				{
					var arrivalKind = _pendingRunEndCause == RunEndCause.Abandon
						? WayStationArrivalKind.ReturnedFromAbandonedClimb
						: WayStationArrivalKind.ReturnedFromFailedClimb;
					SaveCache.RecordWayStationClimbReturn(arrivalKind);
					WayStationArrivalContextService.Set(EntityManager, arrivalKind);
					RunLifecycleService.EndCurrentRun(EntityManager);
					EventManager.Publish(new ChangeMusicTrack { Track = MusicTrack.Menu });
					EventManager.Publish(new ShowTransition { Scene = SceneId.WayStation, SkipWipe = true });
				}
				state.SceneSwitched = true;
				state.IsActive = false;
				_pendingRunEndCause = RunEndCause.Death;
			}
		}

		public void Draw()
		{
			var state = GetOverlayState();
			if (state?.IsActive != true || _font == null) return;
			int w = Game1.VirtualWidth;
			int h = Game1.VirtualHeight;

			// Phase timings
			float tOverlay = MathHelper.Clamp(state.Elapsed / System.Math.Max(0.0001f, OverlayFadeInSeconds), 0f, 1f);
			float tTextFade = MathHelper.Clamp((state.Elapsed - OverlayFadeInSeconds) / System.Math.Max(0.0001f, TextFadeOutSeconds), 0f, 1f);

			// 1) Draw background overlay first; it reaches full opacity and stays black
			_spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(0f, 0f, 0f, tOverlay));

			// 2) Draw centered text on top; fade its color from red to black after overlay completes
			var text = MessageText ?? string.Empty;
			var baseSize = _font.MeasureString(text);
			float scale = System.Math.Max(0.01f, TextScale);
			var size = baseSize * scale;
			var pos = new Vector2(w / 2f - size.X / 2f + TextOffsetX, h / 2f - size.Y / 2f + TextOffsetY);
			// Lerp text color from Red -> Black over tTextFade (alpha stays 1)
			byte r = (byte)(255 * (1f - tTextFade));
			var textColor = new Color(r, 0, 0);
			var shadow = Color.Black * (0.6f * (1f - tTextFade));
			_spriteBatch.DrawString(_font, text, pos + new Vector2(2, 2), shadow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_font, text, pos, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}
	}
}
