using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using System;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Transition")] 
	public class TransitionDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private readonly SpriteFont _font = FontSingleton.TitleFont;

		// Animation state
		private enum Phase { Idle, WipeIn, Hold, WipeOut }
		private Phase _phase = Phase.Idle;
		private float _t = 0f; // time within current phase
		private bool _suppressLoadScene = false; // one-shot debug preview flag
		private bool _skipHold = false;
		private bool _endRunOnLoad = false;
        private SceneId _nextScene;

        [DebugEditable(DisplayName = "Wipe Duration (s)", Step = 0.05f, Min = 0.05f, Max = 3f)]
		public float WipeDurationSeconds { get; set; } = 0.55f;
		[DebugEditable(DisplayName = "Hold Black (s)", Step = 0.05f, Min = 0f, Max = 2f)]
		public float HoldSeconds { get; set; } = 0.5f;
		[DebugEditable(DisplayName = "Angle Degrees", Step = 1f, Min = -90f, Max = 90f)]
		public float AngleDegrees { get; set; } = 40f; // diagonal like Star Wars
		[DebugEditable(DisplayName = "Color Alpha", Step = 5, Min = 0, Max = 255)]
		public int Alpha { get; set; } = 255;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.1f, Min = 0.5f, Max = 6f)]
		public float TextScale { get; set; } = 1f;

		[DebugEditable(DisplayName = "Pulse Amplitude", Step = 0.05f, Min = 0f, Max = 1f)]
		public float PulseAmplitude { get; set; } = 0.05f;

		public TransitionDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			EventManager.Subscribe<ShowTransition>(BeginWipeIn);
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCachesEvent);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		private void OnDeleteCachesEvent(DeleteCachesEvent evt)
		{
			// DeleteEntities(evt.Scene);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (_phase == Phase.Idle) return;
			// While transition is active, publish a global flag entity for other systems to consult
			EnsureTransitionFlag(true);
			_t += dt;
			switch (_phase)
			{
				case Phase.WipeIn:
					if (_t >= WipeDurationSeconds)
					{
						if (_skipHold)
						{
							// Skip hold phase — fire scene load immediately and go straight to wipe out
							if (!_suppressLoadScene)
							{
								LoggingService.Append("TransitionDisplaySystem.Update.Phase.WipeIn", new System.Text.Json.Nodes.JsonObject { ["nextScene"] = _nextScene.ToString() });
								LoadTargetScene(_nextScene, publishComplete: false);
							}
							else
							{
								_suppressLoadScene = false;
							}
							_phase = Phase.WipeOut; _t = 0f;
						}
						else
						{
							_phase = Phase.Hold;
							_t = 0f;
						}
					}
					break;
				case Phase.Hold:
					if (_t >= HoldSeconds)
					{
						if (!_suppressLoadScene)
						{
							LoggingService.Append("TransitionDisplaySystem.Update.Phase.Hold", new System.Text.Json.Nodes.JsonObject { ["nextScene"] = _nextScene.ToString() });
							LoadTargetScene(_nextScene, publishComplete: false);
						}
						else
						{
							// reset the one-shot flag after a preview run
							_suppressLoadScene = false;
						}
						_phase = Phase.WipeOut; _t = 0f;
					}
					break;
				case Phase.WipeOut:
					if (_t >= WipeDurationSeconds)
					{
						_phase = Phase.Idle; _t = 0f;
						EventManager.Publish(new TransitionCompleteEvent { Scene = _nextScene });
						EnsureTransitionFlag(false);
					}
					break;
			}
		}

		public void Draw()
		{
			if (_phase == Phase.Idle) return;
			int vw = Game1.VirtualWidth;
			int vh = Game1.VirtualHeight;
			float angle = MathHelper.ToRadians(AngleDegrees);
			// progress 0..1 for wipe-in, 1..0 for wipe-out
			float p = MathHelper.Clamp(_t / Math.Max(0.0001f, WipeDurationSeconds), 0f, 1f);
			if (_phase == Phase.WipeOut) p = 1f - p;
			if (_phase == Phase.Hold) p = 1f;

			// Build a covering parallelogram whose width expands with p
			// Ensure full coverage for any angle by computing total length considering diagonal span
			int stripes = 128;
			int stripeThickness = (int)Math.Ceiling(vh / (float)stripes) + 2;
			byte a = (byte)Math.Clamp(Alpha, 0, 255);
			var color = Color.DarkRed;
			float tan = (float)Math.Tan(angle);
			float dxSpan = tan * vh; // total horizontal offset from top to bottom
			float dxMin = Math.Min(0f, dxSpan);
			float dxMax = Math.Max(0f, dxSpan);
			float margin = 1000f; // extra overdraw on both sides
			float startBase = dxMin - margin;
			float totalLen = vw + (dxMax - dxMin) + 2f * margin;
			float fillLen = totalLen * p;
			// For wipe-out, retract from the top-left by advancing the start position
			float startShift = (_phase == Phase.WipeOut) ? (totalLen - fillLen) : 0f;
			for (int i = -2; i < stripes + 2; i++)
			{
				float y0 = i * stripeThickness;
				float dx = tan * y0;
				var rect = new Rectangle((int)(startBase + startShift + dx), (int)y0, (int)Math.Ceiling(fillLen), stripeThickness);
				_spriteBatch.Draw(_pixel, rect, color);
			}

			// Reveal centered white text when the wipe overlaps the screen center (or during Hold)
			bool coveredAtCenter = false;
			{
				float centerY = vh * 0.5f;
				float centerX = vw * 0.5f;
				float dxCenter = tan * centerY;
				float startX = startBase + startShift + dxCenter;
				float endX = startX + fillLen;
				coveredAtCenter = centerX >= startX && centerX <= endX;
			}
			if (_phase == Phase.Hold) coveredAtCenter = true;

			if (coveredAtCenter && !_skipHold && _font != null)
			{
				string text = "Deus Vult!";
				float scale = TextScale;
				if (_phase == Phase.Hold && HoldSeconds > 0f && PulseAmplitude > 0f)
				{
					float n = MathHelper.Clamp(_t / Math.Max(0.0001f, HoldSeconds), 0f, 1f);
					// Single quick pulse over the hold window
					scale *= 1f + PulseAmplitude * (float)Math.Sin(Math.PI * n);
				}
				var size = _font.MeasureString(text) * scale;
				var pos = new Vector2((vw - size.X) * 0.5f, (vh - size.Y) * 0.5f);
				_spriteBatch.DrawString(_font, text, pos, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			}
		}

		private void BeginWipeIn(ShowTransition transition)
		{
			_suppressLoadScene = transition.Scene == SceneId.None;
			_skipHold = transition.SkipHold;
			_endRunOnLoad = transition.EndRunOnLoad;
			_nextScene = transition.Scene;

			if (transition.SkipWipe)
			{
				if (!_suppressLoadScene)
					LoadTargetScene(_nextScene, publishComplete: true);
				else
					_suppressLoadScene = false;
				_phase = Phase.Idle;
				_t = 0f;
				return;
			}

			_phase = Phase.WipeIn;
			_t = 0f;
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.Transition, Volume = 0.5f });
			EnsureTransitionFlag(true);
		}

		private void LoadTargetScene(SceneId nextScene, bool publishComplete)
		{
			if (_endRunOnLoad)
			{
				RunLifecycleService.EndCurrentRun(EntityManager);
				_endRunOnLoad = false;
			}

			var sceneEntity = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault();
			var previous = sceneEntity?.GetComponent<SceneState>()?.Current ?? SceneId.None;
			EventManager.Publish(new DeleteCachesEvent { Scene = nextScene });
			DeleteEntities(nextScene);
			EventManager.Publish(new LoadSceneEvent { Scene = nextScene, PreviousScene = previous });
			if (publishComplete)
				EventManager.Publish(new TransitionCompleteEvent { Scene = nextScene });
		}

		private void DeleteEntities(SceneId nextScene)
		{
			var sceneEntity = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault();
			var scene = sceneEntity.GetComponent<SceneState>();
			var previous = scene.Current;
			LoggingService.Append("TransitionDisplaySystem.DeleteEntities", new System.Text.Json.Nodes.JsonObject { ["from"] = previous.ToString(), ["to"] = nextScene.ToString() });
			scene.Current = nextScene;
			// Destroy previous scene's entities except those marked DontDestroyOnLoad
			var isReload = previous == nextScene;
			LoggingService.Append("TransitionDisplaySystem.DeleteEntities.isReload", new System.Text.Json.Nodes.JsonObject { ["isReload"] = isReload });
			var toDestroy = EntityManager.GetAllEntities()
				.Where(e =>
					e.HasComponent<OwnedByScene>() &&
					e.GetComponent<OwnedByScene>().Scene == previous &&
					!isReload &&
					!e.HasComponent<DontDestroyOnLoad>()                   // never destroy if DontDestroyOnLoad is present
					// (!isReload || !e.HasComponent<DontDestroyOnReload>())     // during reload, also keep DontDestroyOnReload
				)
				.ToList();
			foreach (var e in toDestroy)
			{
				LoggingService.Append("TransitionDisplaySystem.DeleteEntities.destroying", new System.Text.Json.Nodes.JsonObject { ["entityId"] = e.Id, ["entityName"] = e.Name });
				EntityManager.DestroyEntity(e.Id);
			}
		}

		private void EnsureTransitionFlag(bool active)
		{
			StateSingleton.IsActive = active;
		}

		[DebugAction("Preview Wipe (visual only)")]
		private void Debug_PreviewWipe()
		{
			_suppressLoadScene = true;
			BeginWipeIn(new ShowTransition { Scene = SceneId.None });
		}
		[DebugAction("Preview Wipe (actually proceeds)")]
		private void Debug_PreviewWipeRestart()
		{
			_suppressLoadScene = true;
			// is this the last enemy?
			var queuedEntity = EntityManager.GetEntity("QueuedEvents");
			var queued = queuedEntity.GetComponent<QueuedEvents>();
			if (queued.CurrentIndex == queued.Events.Count - 1)
			{
				BeginWipeIn(new ShowTransition { Scene = SceneId.Climb });
			}	
			else
			{
				BeginWipeIn(new ShowTransition { Scene = SceneId.Battle });
			}
		}
		[DebugAction("Back to Climb")]
		private void Debug_BackToClimb()
		{
			_suppressLoadScene = true;
			BeginWipeIn(new ShowTransition { Scene = SceneId.Climb });
		}
	}
}

