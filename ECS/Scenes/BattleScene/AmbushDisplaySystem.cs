using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using System;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Shows an "Ambush!" intro animation and a timed meter for ambush attacks.
	/// Auto-confirms the enemy attack when the timer expires by publishing ConfirmBlocksRequested.
	/// </summary>
	[DebugTab("Ambush Display")]
	public class AmbushDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.TitleFont;
		private readonly Texture2D _pixel;

	private int _lastAttackSequence = -1;
	private float _introElapsed = 0f;
	private readonly System.Random _rand = new System.Random();
	private Entity _textAnchorEntity;
	private Entity _timerAnchorEntity;
	private bool _waitingForPhaseAnimation = false;
	private int _pendingAttackSequence = -1;

		// Debug-editable tuning
		[DebugEditable(DisplayName = "Intro Drop Duration (s)", Step = 0.05f, Min = 0.05f, Max = 3f)]
		public float IntroDropDurationSeconds { get; set; } = 0.3f;

		[DebugEditable(DisplayName = "Intro Hold Duration (s)", Step = 0.05f, Min = 0f, Max = 3f)]
		public float IntroHoldDurationSeconds { get; set; } = 0.3f;

		[DebugEditable(DisplayName = "Intro Shrink Duration (s)", Step = 0.05f, Min = 0.05f, Max = 3f)]
		public float IntroShrinkDurationSeconds { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Text Scale Base", Step = 0.05f, Min = 0.2f, Max = 2f)]
		public float TextScaleBase { get; set; } = 0.6f;

		[DebugEditable(DisplayName = "Text Scale Overshoot", Step = 0.05f, Min = 0f, Max = 1f)]
		public float TextScaleOvershoot { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Text Start Y %", Step = 1, Min = -50, Max = 50)]
		public int TextStartYPercent { get; set; } = -15;

		[DebugEditable(DisplayName = "Text End Y %", Step = 1, Min = 0, Max = 80)]
		public int TextEndYPercent { get; set; } = 30;

		[DebugEditable(DisplayName = "Intro Shake Amplitude (px)", Step = 1, Min = 0, Max = 30)]
		public int IntroShakeAmplitudePx { get; set; } = 1;


		[DebugEditable(DisplayName = "Meter Width", Step = 5, Min = 100, Max = 800)]
		public int MeterWidth { get; set; } = 500;

		[DebugEditable(DisplayName = "Meter Height", Step = 1, Min = 10, Max = 80)]
		public int MeterHeight { get; set; } = 24;

		[DebugEditable(DisplayName = "Meter Skew", Step = 1, Min = 0, Max = 80)]
		public int MeterSkew { get; set; } = 29;

		[DebugEditable(DisplayName = "Meter Y %", Step = 1, Min = 0, Max = 90)]
		public int MeterYPercent { get; set; } = 55;

		[DebugEditable(DisplayName = "Meter BG Alpha", Step = 5, Min = 0, Max = 255)]
		public int MeterBgAlpha { get; set; } = 220;

		[DebugEditable(DisplayName = "Meter Fill Alpha", Step = 5, Min = 0, Max = 255)]
		public int MeterFillAlpha { get; set; } = 240;

		[DebugEditable(DisplayName = "Default Timer (s)", Step = 1, Min = 1, Max = 60)]
		public int DefaultTimerSeconds { get; set; } = 30;

		public AmbushDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_pixel = new Texture2D(gd, 1, 1);
			_pixel.SetData(new[] { Color.White });

		EventManager.Subscribe<ChangeBattlePhaseEvent>(evt =>
		{
			LoggingService.Append("AmbushDisplaySystem.OnChangeBattlePhaseEvent", new JsonObject {
				{ "Current", evt.Current.ToString() },
				{ "Previous", evt.Previous.ToString() }
			});
			// Reset intro when leaving block/attack phases
			if (evt.Current != SubPhase.Block && evt.Current != SubPhase.EnemyAttack)
			{
				var st = GetOrCreateAmbushState();
				st.IsActive = false;
				st.IntroActive = false;
				st.TimerRemainingSeconds = 0f;
				st.FiredAutoConfirm = false;
				_introElapsed = 0f;
				_lastAttackSequence = -1;
				_waitingForPhaseAnimation = false;
				_pendingAttackSequence = -1;
			}
		});

		EventManager.Subscribe<BattlePhaseAnimationCompleteEvent>(evt =>
		{
			LoggingService.Append("AmbushDisplaySystem.OnBattlePhaseAnimationCompleteEvent", new JsonObject {
				{ "SubPhase", evt.SubPhase.ToString() }
			});
			// When phase animation completes, activate the ambush intro if we were waiting
			if (_waitingForPhaseAnimation && _pendingAttackSequence >= 0)
			{
				var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>()?.Sub ?? SubPhase.Action;
				if (phase == SubPhase.Block)
				{
					var st = GetOrCreateAmbushState();
					_lastAttackSequence = _pendingAttackSequence;
					st.IsActive = true;
					st.IntroActive = true;
					st.ActiveAttackSequence = _pendingAttackSequence;
					st.TimerDurationSeconds = Math.Max(1f, DefaultTimerSeconds - GetSlowStacks());
					st.TimerRemainingSeconds = st.TimerDurationSeconds;
					_introElapsed = 0f;
					UpdateAnchorTransforms();
				}
				_waitingForPhaseAnimation = false;
				_pendingAttackSequence = -1;
			}
		});

		EnsureAnchors();
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<AttackIntent>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>()?.Sub ?? SubPhase.Action;
			var intent = entity.GetComponent<AttackIntent>();
			if (intent == null || intent.Planned.Count == 0)
			{
				Deactivate();
				return;
			}
			var pa = intent.Planned[0];
			var st = GetOrCreateAmbushState();

		// Detect context changes. Only commit activation during Block so ambush triggers at phase entry.
		if (_lastAttackSequence != intent.ActiveAttackSequence)
		{
			_introElapsed = 0f;
			st.FiredAutoConfirm = false;
			if (pa.IsAmbush && phase == SubPhase.Block)
			{
				_waitingForPhaseAnimation = true;
				_pendingAttackSequence = intent.ActiveAttackSequence;
				st.ActiveAttackSequence = intent.ActiveAttackSequence;
				st.TimerDurationSeconds = Math.Max(1f, DefaultTimerSeconds - GetSlowStacks());
				st.TimerRemainingSeconds = st.TimerDurationSeconds;
			}
			else
			{
				// Wait for Block to activate; keep ambush inactive for now
				Deactivate();
			}
		}
		// Fallback: if we are in Block and haven't activated for this context yet, wait for animation
		else if (pa.IsAmbush && phase == SubPhase.Block && (!st.IsActive || st.ActiveAttackSequence != intent.ActiveAttackSequence))
		{
			if (!_waitingForPhaseAnimation)
			{
				_waitingForPhaseAnimation = true;
				_pendingAttackSequence = intent.ActiveAttackSequence;
				_introElapsed = 0f;
				st.FiredAutoConfirm = false;
				st.ActiveAttackSequence = intent.ActiveAttackSequence;
				st.TimerDurationSeconds = Math.Max(1f, DefaultTimerSeconds - GetSlowStacks());
				st.TimerRemainingSeconds = st.TimerDurationSeconds;
			}
		}

			// Maintain state while in Block
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (st.IsActive && phase == SubPhase.Block)
			{
				_introElapsed += dt;
				float dropDurChk = Math.Max(0.0001f, IntroDropDurationSeconds);
				float holdDurChk = Math.Max(0f, IntroHoldDurationSeconds);
				float shrinkDurChk = Math.Max(0.0001f, IntroShrinkDurationSeconds);
				float totalChk = dropDurChk + holdDurChk + shrinkDurChk;
				if (st.IntroActive && _introElapsed >= totalChk)
				{
					st.IntroActive = false;
				}
				if (!st.IntroActive)
				{
					st.TimerRemainingSeconds = Math.Max(0f, st.TimerRemainingSeconds - dt);
					if (st.TimerRemainingSeconds <= 0f && !st.FiredAutoConfirm)
					{
						st.FiredAutoConfirm = true;
						// Notify listeners (e.g., MustBeBlockedSystem) that the ambush timer expired for this context
						EventManager.Publish(new AmbushTimerExpired { AttackSequence = st.ActiveAttackSequence });
						EventManager.Publish(new ConfirmBlocksRequested());
					}
				}
				UpdateAnchorTransforms();
			}
			else if (phase != SubPhase.Block)
			{
				// Hide during non-block phases
				st.IntroActive = false;
			} 
		}

		public void Draw()
		{
			var st = EntityManager.GetEntitiesWithComponent<AmbushState>().FirstOrDefault()?.GetComponent<AmbushState>();
			if (st == null || !st.IsActive || _font == null) return;
			var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>()?.Sub ?? SubPhase.Action;
			if (phase != SubPhase.Block) return;

			int vx = Game1.VirtualWidth;
			int vy = Game1.VirtualHeight;

			if (st.IntroActive)
			{
				// Big red "Ambush!" with drop-in + shake + overshoot scale
				string text = "Ambush!";
				float dropDur = Math.Max(0.0001f, IntroDropDurationSeconds);
				float holdDur = Math.Max(0f, IntroHoldDurationSeconds);
				float shrinkDur = Math.Max(0.0001f, IntroShrinkDurationSeconds);
				float total = dropDur + holdDur + shrinkDur;
				float t = MathHelper.Clamp(_introElapsed / total, 0f, 1f);
				float tSecs = _introElapsed;
				float scale;
				float easeDrop = 1f - (float)Math.Pow(1f - MathHelper.Clamp(tSecs / dropDur, 0f, 1f), 3);
				float appearScale = TextScaleBase + TextScaleOvershoot * (1f - (float)Math.Cos(easeDrop * MathF.PI));
				if (tSecs <= dropDur)
				{
					scale = appearScale;
				}
				else if (tSecs <= dropDur + holdDur)
				{
					scale = appearScale;
				}
				else
				{
					float stp = MathHelper.Clamp((tSecs - dropDur - holdDur) / shrinkDur, 0f, 1f);
					float shrinkEase = 1f - (float)Math.Pow(1f - stp, 2);
					scale = MathHelper.Lerp(appearScale, 0f, shrinkEase);
				}
				// Drop from top
				float yStart = vy * (TextStartYPercent / 100f);
				float yEnd = vy * (TextEndYPercent / 100f);
				float yPos;
				if (tSecs <= dropDur)
				{
					yPos = MathHelper.Lerp(yStart, yEnd, easeDrop);
				}
				else
				{
					yPos = yEnd;
				}
				int shakeAmp = IntroShakeAmplitudePx;
				int sx = _rand.Next(-shakeAmp, shakeAmp + 1);
				int sy = _rand.Next(-shakeAmp, shakeAmp + 1);
				var size = _font.MeasureString(text) * scale;
				var textCenter = _textAnchorEntity?.GetComponent<Transform>()?.Position ?? new Vector2(vx / 2f, yPos);
				var pos = new Vector2(textCenter.X - size.X / 2f + sx, textCenter.Y - size.Y / 2f + sy);
				// Shadow
				_spriteBatch.DrawString(_font, text, pos + new Vector2(2, 2), new Color(0, 0, 0, 200), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				// Text
				_spriteBatch.DrawString(_font, text, pos, Color.OrangeRed, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			}

			// Timer meter (trapezoid/parallelogram-like), shown at top center
			int meterWidth = Math.Max(10, MeterWidth);
			int meterHeight = Math.Max(4, MeterHeight);
			int skew = Math.Max(0, MeterSkew); // pixels of top-edge inward skew
			var timerCenter = _timerAnchorEntity?.GetComponent<Transform>()?.Position ?? new Vector2(vx / 2f, (int)(vy * (MeterYPercent / 100f)));
			var rect = new Rectangle((int)(timerCenter.X - meterWidth / 2f), (int)(timerCenter.Y - meterHeight / 2f), meterWidth, meterHeight);
			float ratio = st.TimerDurationSeconds <= 0.0001f ? 0f : MathHelper.Clamp(st.TimerRemainingSeconds / st.TimerDurationSeconds, 0f, 1f);

		DrawTrapezoid(rect, skew, new Color(10, 10, 10, Math.Clamp(MeterBgAlpha, 0, 255))); // background
		var fillRect = rect;
		DrawTrapezoidFill(fillRect, skew, ratio, new Color(180, 30, 30, Math.Clamp(MeterFillAlpha, 0, 255)));
		DrawTrapezoidBorder(rect, skew, Color.Black, 2);

		// Timer countdown text centered in meter
		string timerText = st.TimerRemainingSeconds.ToString("F1");
		float timerScale = 0.18f;
		var timerSize = _font.MeasureString(timerText) * timerScale;
		var timerPos = new Vector2(timerCenter.X - timerSize.X / 2f, timerCenter.Y - timerSize.Y / 2f);
		// Shadow
		_spriteBatch.DrawString(_font, timerText, timerPos + new Vector2(1, 1), new Color(0, 0, 0, 180), 0f, Vector2.Zero, timerScale, SpriteEffects.None, 0f);
		// White text
		_spriteBatch.DrawString(_font, timerText, timerPos, Color.White, 0f, Vector2.Zero, timerScale, SpriteEffects.None, 0f);
	}

		[DebugAction("Reset Ambush Intro")]
		public void Debug_ResetAmbushIntro()
		{
			var st = EntityManager.GetEntitiesWithComponent<AmbushState>().FirstOrDefault()?.GetComponent<AmbushState>();
			if (st != null && st.IsActive)
			{
				st.IntroActive = true;
				_introElapsed = 0f;
			}
		}

		[DebugAction("Auto Confirm Now")]
		public void Debug_AutoConfirmNow()
		{
			EventManager.Publish(new ConfirmBlocksRequested());
		}

	private void Deactivate()
	{
		var st = GetOrCreateAmbushState();
		st.IsActive = false;
		st.IntroActive = false;
		st.TimerRemainingSeconds = 0f;
		st.FiredAutoConfirm = false;
		_introElapsed = 0f;
		_waitingForPhaseAnimation = false;
		_pendingAttackSequence = -1;
	}

		private AmbushState GetOrCreateAmbushState()
		{
			var e = EntityManager.GetEntitiesWithComponent<AmbushState>().FirstOrDefault();
			if (e == null)
			{
				e = EntityManager.CreateEntity("AmbushStateEntity");
				EntityManager.AddComponent(e, new AmbushState { IsActive = false, IntroActive = false, TimerDurationSeconds = 20f, TimerRemainingSeconds = 0f });
			}
			return e.GetComponent<AmbushState>();
		}

		private void EnsureAnchors()
		{
			if (_textAnchorEntity == null)
			{
				_textAnchorEntity = EntityManager.CreateEntity("AmbushTextAnchor");
				EntityManager.AddComponent(_textAnchorEntity, new AmbushTextAnchor());
				EntityManager.AddComponent(_textAnchorEntity, new Transform { Position = Vector2.Zero, ZOrder = 10001 });
			}
		if (_timerAnchorEntity == null)
		{
			_timerAnchorEntity = EntityManager.CreateEntity("AmbushTimerAnchor");
			EntityManager.AddComponent(_timerAnchorEntity, new AmbushTimerAnchor());
			EntityManager.AddComponent(_timerAnchorEntity, new Transform { Position = Vector2.Zero, ZOrder = 10001 });
			EntityManager.AddComponent(_timerAnchorEntity, ParallaxLayer.GetUIParallaxLayer());
		}
		}

		private void UpdateAnchorTransforms()
		{
			int vx = Game1.VirtualWidth;
			int vy = Game1.VirtualHeight;
			float dropDur = Math.Max(0.0001f, IntroDropDurationSeconds);
			float holdDur = Math.Max(0f, IntroHoldDurationSeconds);
			float shrinkDur = Math.Max(0.0001f, IntroShrinkDurationSeconds);
			float total = dropDur + holdDur + shrinkDur;
			float tSecs = _introElapsed;
			float yStart = vy * (TextStartYPercent / 100f);
			float yEnd = vy * (TextEndYPercent / 100f);
			float easeDrop = 1f - (float)Math.Pow(1f - MathHelper.Clamp(tSecs / dropDur, 0f, 1f), 3);
			float yPos = (tSecs <= dropDur) ? MathHelper.Lerp(yStart, yEnd, easeDrop) : yEnd;
			var textT = _textAnchorEntity?.GetComponent<Transform>();
			if (textT != null)
			{
				textT.Position = new Vector2(vx / 2f, yPos);
			}
			var timerT = _timerAnchorEntity?.GetComponent<Transform>();
			if (timerT != null)
			{
				timerT.Position = new Vector2(vx / 2f, (int)(vy * (MeterYPercent / 100f)));
			}
		}

		private void DrawTrapezoid(Rectangle bounds, int skew, Color color)
		{
			for (int y = 0; y < bounds.Height; y++)
			{
				float t = 1f - (y / (float)Math.Max(1, bounds.Height - 1));
				int offset = (int)Math.Round(skew * t);
				int x = bounds.X + offset;
				int w = Math.Max(0, bounds.Width - offset * 2);
				_spriteBatch.Draw(_pixel, new Rectangle(x, bounds.Y + y, w, 1), color);
			}
		}

		private void DrawTrapezoidFill(Rectangle bounds, int skew, float ratio, Color color)
		{
			for (int y = 0; y < bounds.Height; y++)
			{
				float t = 1f - (y / (float)Math.Max(1, bounds.Height - 1));
				int offset = (int)Math.Round(skew * t);
				int x = bounds.X + offset;
				int w = Math.Max(0, bounds.Width - offset * 2);
				int fw = (int)Math.Round(w * ratio);
				if (fw > 0)
				{
					// Right-justify fill so it depletes right-to-left
					int xFill = x + (w - fw);
					_spriteBatch.Draw(_pixel, new Rectangle(xFill, bounds.Y + y, fw, 1), color);
				}
			}
		}

		private void DrawTrapezoidBorder(Rectangle bounds, int skew, Color color, int thickness)
		{
			thickness = Math.Max(1, thickness);
			for (int y = 0; y < bounds.Height; y++)
			{
				float t = 1f - (y / (float)Math.Max(1, bounds.Height - 1));
				int offset = (int)Math.Round(skew * t);
				int x = bounds.X + offset;
				int w = Math.Max(0, bounds.Width - offset * 2);
				int yy = bounds.Y + y;
				if (y < thickness || y >= bounds.Height - thickness)
				{
					// Top/bottom horizontal edges
					_spriteBatch.Draw(_pixel, new Rectangle(x, yy, w, 1), color);
				}
				else
				{
					// Left vertical edge
					_spriteBatch.Draw(_pixel, new Rectangle(x, yy, thickness, 1), color);
					// Right vertical edge
					_spriteBatch.Draw(_pixel, new Rectangle(x + Math.Max(0, w - thickness), yy, thickness, 1), color);
				}
			}
		}

		private int GetSlowStacks()
		{
			var ap = EntityManager.GetEntitiesWithComponent<AppliedPassives>().FirstOrDefault()?.GetComponent<AppliedPassives>();
			if (ap == null) return 0;
			return ap.Passives.TryGetValue(AppliedPassiveType.Slow, out int stacks) ? stacks : 0;
		}
	}
}


