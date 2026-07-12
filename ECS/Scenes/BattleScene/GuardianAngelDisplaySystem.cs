using System.Linq;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.Systems
{
    [DebugTab("Guardian Angel")]
    public class GuardianAngelDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
        private Texture2D _angelTexture;
        private Texture2D _pixel;
        private SpriteFont _font = FontSingleton.ChakraPetchFont;

        private float _t;
		private Vector2 _pos;
		private Vector2 _lastMotionVelocity;
		private float _renderScaleMultiplier = 1f;
		private int _loopIndex = int.MinValue;
		private FlightLoopPreset _currentLoopPreset;
		private FlightLoopPreset _nextLoopPreset;
		private float _spawnAccumulator;
		private static readonly Random _rand = new Random();
		private struct FlightLoopPreset
		{
			public float RadiusXScale;
			public float RadiusYScale;
			public float PhaseOffset;
			public float BobScale;
			public float BobPhase;
			public float WobbleX;
			public float WobbleY;
			public float WobbleSpeed;
			public float WobblePhase;
			public float PathMix;
		}
		private struct SparkleParticle
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Age;
			public float Lifetime;
			public float Size;
			public float TwinkleSpeed;
			public float TwinklePhase;
			public float DriftPhase;
			public float DriftSpeed;
			public float DriftStrength;
		}
		private readonly List<SparkleParticle> _sparkles = new List<SparkleParticle>();

		private const string GuardianEntityName = "GuardianAngel";

		// Speech bubble state
		private bool _bubbleActive;
		private float _bubbleElapsed;
		private float _activeBubbleDuration;
		private float _speechGapRemaining;
		private string _bubbleText = "";
		private Texture2D _bubbleTexture;
		private int _bubbleTexW;
		private int _bubbleTexH;
		private readonly GuardianAngelSpeechQueue _speechQueue = new();
		private GuardianSpeechRequest _activeSpeech;
		private GuardianFlightGesture _flightGesture;
		private float _gestureElapsed;
		private float _gestureDuration;
		internal Vector2? DebugMotionOffsetOverride { get; set; }

        // Placement relative to player
        [DebugEditable(DisplayName = "Offset X", Step = 5, Min = -2000, Max = 2000)]
		public int OffsetX { get; set; } = 215;
        [DebugEditable(DisplayName = "Offset Y", Step = 5, Min = -2000, Max = 2000)]
        public int OffsetY { get; set; } = -135;

        // Motion settings
        [DebugEditable(DisplayName = "Base Radius X", Step = 5, Min = 0, Max = 2000)]
		public int RadiusX { get; set; } = 70;
        [DebugEditable(DisplayName = "Base Radius Y", Step = 5, Min = 0, Max = 2000)]
		public int RadiusY { get; set; } = 25;
        [DebugEditable(DisplayName = "Figure Eight Mix", Step = 0.01f, Min = 0f, Max = 1f)]
		public float FigureEightMix { get; set; } = 0.73f; // 0: ellipse, 1: classic 8
        [DebugEditable(DisplayName = "Angular Speed", Step = 0.05f, Min = 0.05f, Max = 10f)]
		public float AngularSpeed { get; set; } = 1.15f; // radians per second multiplier
        [DebugEditable(DisplayName = "Vertical Bob Amplitude", Step = 1, Min = 0, Max = 1000)]
        public int VerticalBob { get; set; } = 7;
        [DebugEditable(DisplayName = "Vertical Bob Speed", Step = 0.05f, Min = 0.05f, Max = 10f)]
        public float VerticalBobSpeed { get; set; } = 1.15f;
		[DebugEditable(DisplayName = "Motion Bounds X", Step = 5, Min = 1, Max = 2000)]
		public int MotionBoundsX { get; set; } = 95;
		[DebugEditable(DisplayName = "Motion Bounds Y", Step = 5, Min = 1, Max = 2000)]
		public int MotionBoundsY { get; set; } = 48;
		[DebugEditable(DisplayName = "Loop Variation", Step = 0.01f, Min = 0f, Max = 1f)]
		public float LoopVariation { get; set; } = 0.22f;
		[DebugEditable(DisplayName = "Loop Wobble", Step = 1, Min = 0, Max = 500)]
		public int LoopWobble { get; set; } = 8;

        // Appearance
        [DebugEditable(DisplayName = "Scale", Step = 0.01f, Min = 0.05f, Max = 4f)]
        public float Scale { get; set; } = 0.08f;
        [DebugEditable(DisplayName = "Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
		public float Alpha { get; set; } = 1f;
		[DebugEditable(DisplayName = "Rotation Magnitude", Step = 0.01f, Min = 0f, Max = 1f)]
		public float RotationMagnitude { get; set; } = 0.13f;
		[DebugEditable(DisplayName = "Rotation Follow", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float RotationFollow { get; set; } = 0.18f;

		// Sparkle settings
		[DebugEditable(DisplayName = "Sparkle Spawn Rate (per sec)", Step = 1, Min = 0, Max = 200)]
		public int SparkleSpawnRate { get; set; } = 18;
		[DebugEditable(DisplayName = "Sparkle Speed Min", Step = 1, Min = 0, Max = 2000)]
		public int SparkleSpeedMin { get; set; } = 8;
		[DebugEditable(DisplayName = "Sparkle Speed Max", Step = 1, Min = 0, Max = 2000)]
		public int SparkleSpeedMax { get; set; } = 26;
		[DebugEditable(DisplayName = "Sparkle Motion Influence", Step = 0.01f, Min = 0f, Max = 1f)]
		public float SparkleMotionInfluence { get; set; } = 0.18f;
		[DebugEditable(DisplayName = "Sparkle Spawn Jitter X", Step = 1, Min = 0, Max = 200)]
		public int SparkleSpawnJitterX { get; set; } = 14;
		[DebugEditable(DisplayName = "Sparkle Spawn Jitter Y", Step = 1, Min = 0, Max = 200)]
		public int SparkleSpawnJitterY { get; set; } = 10;
		[DebugEditable(DisplayName = "Sparkle Gravity", Step = 1, Min = -500, Max = 500)]
		public int SparkleGravity { get; set; } = 12;
		[DebugEditable(DisplayName = "Sparkle Lift", Step = 1, Min = -500, Max = 500)]
		public int SparkleLift { get; set; } = 5;
		[DebugEditable(DisplayName = "Sparkle Drag", Step = 0.01f, Min = 0f, Max = 20f)]
		public float SparkleDrag { get; set; } = 1.4f;
		[DebugEditable(DisplayName = "Sparkle Drift", Step = 1, Min = 0, Max = 500)]
		public int SparkleDrift { get; set; } = 14;
		[DebugEditable(DisplayName = "Sparkle Lifetime Min (s)", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public float SparkleLifetimeMin { get; set; } = 0.85f;
		[DebugEditable(DisplayName = "Sparkle Lifetime Max (s)", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public float SparkleLifetimeMax { get; set; } = 1.65f;
		[DebugEditable(DisplayName = "Sparkle Size Min", Step = 0.01f, Min = 0.01f, Max = 8f)]
		public float SparkleSizeMin { get; set; } = 1.1f;
		[DebugEditable(DisplayName = "Sparkle Size Max", Step = 0.01f, Min = 0.01f, Max = 8f)]
		public float SparkleSizeMax { get; set; } = 3.2f;
		[DebugEditable(DisplayName = "Twinkle Speed Min", Step = 0.05f, Min = 0.05f, Max = 30f)]
		public float TwinkleSpeedMin { get; set; } = 5.2f;
		[DebugEditable(DisplayName = "Twinkle Speed Max", Step = 0.05f, Min = 0.05f, Max = 30f)]
		public float TwinkleSpeedMax { get; set; } = 11.5f;

		// Bubble settings
		[DebugEditable(DisplayName = "Bubble Text Scale", Step = 0.01f, Min = 0.01f, Max = 3f)]
		public float BubbleTextScale { get; set; } = 0.14f;
		[DebugEditable(DisplayName = "Bubble Padding X", Step = 1, Min = 0, Max = 200)]
		public int BubblePadX { get; set; } = 8;
		[DebugEditable(DisplayName = "Bubble Padding Y", Step = 1, Min = 0, Max = 200)]
		public int BubblePadY { get; set; } = 8;
		[DebugEditable(DisplayName = "Bubble Corner Radius", Step = 1, Min = 0, Max = 60)]
		public int BubbleCornerRadius { get; set; } = 12;
		[DebugEditable(DisplayName = "Bubble Max Width (px)", Step = 10, Min = 50, Max = 2000)]
		public int BubbleMaxWidth { get; set; } = 260;
		[DebugEditable(DisplayName = "Bubble Offset X", Step = 1, Min = -1000, Max = 1000)]
		public int BubbleOffsetX { get; set; } = 0;
		[DebugEditable(DisplayName = "Bubble Offset Y", Step = 1, Min = -1000, Max = 1000)]
		public int BubbleOffsetY { get; set; } = 0;
		[DebugEditable(DisplayName = "Bubble Max Duration (s)", Step = 0.05f, Min = 1.6f, Max = 10f)]
		public float BubbleDuration { get; set; } = 3.2f;
		[DebugEditable(DisplayName = "Speech Gap (s)", Step = 0.01f, Min = 0f, Max = 2f)]
		public float SpeechGapSeconds { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Bubble BG Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
		public float BubbleBgAlpha { get; set; } = 0.82f;
		[DebugEditable(DisplayName = "Bubble Fade In (s)", Step = 0.01f, Min = 0f, Max = 2f)]
		public float BubbleFadeInSeconds { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Bubble Fade Out (s)", Step = 0.01f, Min = 0f, Max = 2f)]
		public float BubbleFadeOutSeconds { get; set; } = 0.25f;
		[DebugEditable(DisplayName = "Bubble Text", Step = 1)]
		public string DefaultStartBattleText { get; set; } = "You don't scare us!";

        public GuardianAngelDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			_pixel = _imageAssets.GetPixel(Color.White);
            // Load shared UI font
            // Listen for phase changes to show speech bubbles
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<TriggerTemperance>(OnTriggerTemperance);
			EventManager.Subscribe<CardPlayedEvent>(OnCardPlayed);
			EventManager.Subscribe<MedalTriggered>(OnMedalTriggered);
            EventManager.Subscribe<LoadSceneEvent>(OnLoadSceneEvent);
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			_t += dt;
			UpdateSpeech(dt);
			UpdateFlightGesture(dt);

			// Ensure guardian entity exists with Transform + Parallax
			EnsureGuardianEntity();

			// Compute current guardian base position relative to player
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			var pt = player?.GetComponent<Transform>();
			var guardian = EntityManager.GetEntity(GuardianEntityName);
			var gt = guardian?.GetComponent<Transform>();
			if (pt != null && gt != null)
			{
				Vector2 oldPos = _pos;
				Vector2 baseRight = pt.Position + new Vector2(OffsetX, OffsetY);
				Vector2 motion = GuardianAngelFlightService.ClampToPlayerSideBounds(
					(DebugMotionOffsetOverride ?? GetBoundedMotionOffset()) + GetFlightGestureSample().Offset,
					MotionBoundsX,
					MotionBoundsY);
				_pos = baseRight + motion;
				_lastMotionVelocity = dt > 0.0001f ? (_pos - oldPos) / dt : Vector2.Zero;

				float targetRotation = GetTargetRotation(_lastMotionVelocity);
				gt.Rotation = MathHelper.Lerp(gt.Rotation, targetRotation, MathHelper.Clamp(RotationFollow, 0.01f, 1f));

				var tween = guardian.GetComponent<PositionTween>();
				if (tween != null)
				{
					tween.Target = _pos;
				}
				else
				{
					gt.Position = _pos;
				}
			}

			// Spawn sparkles based on accumulator
			float gestureSparkleBoost = GetFlightGestureSample().SparkleMultiplier;
			_spawnAccumulator += SparkleSpawnRate * gestureSparkleBoost * dt;
			while (_spawnAccumulator >= 1f)
			{
				_spawnAccumulator -= 1f;
				SpawnSparkle(_pos);
			}

			// Update sparkle particles
			for (int i = _sparkles.Count - 1; i >= 0; i--)
			{
				var p = _sparkles[i];
				p.Age += dt;
				if (p.Age >= p.Lifetime)
				{
					_sparkles.RemoveAt(i);
					continue;
				}

				p.DriftPhase += p.DriftSpeed * dt;
				Vector2 drift = new Vector2(MathF.Sin(p.DriftPhase), MathF.Cos(p.DriftPhase * 0.73f)) * p.DriftStrength;
				p.Position += (p.Velocity + drift) * dt;
				p.Velocity.Y += (SparkleGravity - SparkleLift) * dt;
				float drag = MathHelper.Clamp(1f - SparkleDrag * dt, 0f, 1f);
				p.Velocity *= drag;
				_sparkles[i] = p;
			}

            base.Update(gameTime);
        }

		private Vector2 GetBoundedMotionOffset()
		{
			float safeSpeed = MathF.Max(0.05f, AngularSpeed);
			float ang = _t * safeSpeed;
			float cycle = ang / MathF.Tau;
			int loopIndex = (int)MathF.Floor(cycle);
			if (_loopIndex == int.MinValue)
			{
				_loopIndex = loopIndex;
				_currentLoopPreset = CreateLoopPreset();
				_nextLoopPreset = CreateLoopPreset();
			}
			else if (loopIndex != _loopIndex)
			{
				_loopIndex = loopIndex;
				_currentLoopPreset = _nextLoopPreset;
				_nextLoopPreset = CreateLoopPreset();
			}

			float loopT = cycle - MathF.Floor(cycle);
			float blendT = SmoothStep(loopT);
			FlightLoopPreset preset = Blend(_currentLoopPreset, _nextLoopPreset, blendT);
			float variedAng = ang + preset.PhaseOffset;

			float pathMix = MathHelper.Clamp(preset.PathMix, 0f, 1f);
			float xEllipse = MathF.Cos(ang);
			float yEllipse = MathF.Sin(ang);
			float xEight = MathF.Sin(variedAng);
			float yEight = MathF.Sin(2f * variedAng);
			float x = xEllipse * (1f - pathMix) + xEight * pathMix;
			float y = yEllipse * (1f - pathMix) + yEight * pathMix;
			Vector2 motion = new Vector2(x * RadiusX * preset.RadiusXScale, y * RadiusY * preset.RadiusYScale);
			float bob = MathF.Sin(_t * VerticalBobSpeed + preset.BobPhase) * VerticalBob * preset.BobScale;
			float wobbleAng = _t * preset.WobbleSpeed + preset.WobblePhase;
			motion += new Vector2(MathF.Sin(wobbleAng) * preset.WobbleX, MathF.Cos(wobbleAng * 1.31f) * preset.WobbleY);
			motion.Y += bob;

			float boundsX = MathF.Max(1f, MotionBoundsX);
			float boundsY = MathF.Max(1f, MotionBoundsY);
			motion.X = MathHelper.Clamp(motion.X, -boundsX, boundsX);
			motion.Y = MathHelper.Clamp(motion.Y, -boundsY, boundsY);
			return motion;
		}

		private FlightLoopPreset CreateLoopPreset()
		{
			float variation = MathHelper.Clamp(LoopVariation, 0f, 1f);
			float scaleSpan = 0.35f * variation;
			float wobble = MathF.Max(0f, LoopWobble) * variation;
			return new FlightLoopPreset
			{
				RadiusXScale = Lerp(1f - scaleSpan, 1f + scaleSpan, NextFloat()),
				RadiusYScale = Lerp(1f - scaleSpan, 1f + scaleSpan, NextFloat()),
				PhaseOffset = Lerp(-0.32f, 0.32f, NextFloat()) * variation,
				BobScale = Lerp(0.75f, 1.25f, NextFloat()),
				BobPhase = Lerp(0f, MathF.Tau, NextFloat()),
				WobbleX = Lerp(-wobble, wobble, NextFloat()),
				WobbleY = Lerp(-wobble * 0.65f, wobble * 0.65f, NextFloat()),
				WobbleSpeed = Lerp(0.7f, 1.6f, NextFloat()) * MathF.Max(0.05f, AngularSpeed),
				WobblePhase = Lerp(0f, MathF.Tau, NextFloat()),
				PathMix = MathHelper.Clamp(FigureEightMix + Lerp(-0.45f, 0.27f, NextFloat()) * variation, 0f, 1f)
			};
		}

		private static FlightLoopPreset Blend(FlightLoopPreset a, FlightLoopPreset b, float t)
		{
			return new FlightLoopPreset
			{
				RadiusXScale = Lerp(a.RadiusXScale, b.RadiusXScale, t),
				RadiusYScale = Lerp(a.RadiusYScale, b.RadiusYScale, t),
				PhaseOffset = Lerp(a.PhaseOffset, b.PhaseOffset, t),
				BobScale = Lerp(a.BobScale, b.BobScale, t),
				BobPhase = Lerp(a.BobPhase, b.BobPhase, t),
				WobbleX = Lerp(a.WobbleX, b.WobbleX, t),
				WobbleY = Lerp(a.WobbleY, b.WobbleY, t),
				WobbleSpeed = Lerp(a.WobbleSpeed, b.WobbleSpeed, t),
				WobblePhase = Lerp(a.WobblePhase, b.WobblePhase, t),
				PathMix = Lerp(a.PathMix, b.PathMix, t)
			};
		}

		private float GetTargetRotation(Vector2 velocity)
		{
			if (velocity.LengthSquared() < 0.01f) return 0f;
			float speed = velocity.Length();
			float speedT = MathHelper.Clamp(speed / 140f, 0f, 1f);
			float direction = MathF.Atan2(velocity.Y, MathF.Max(1f, MathF.Abs(velocity.X)));
			float horizontalBank = MathF.Sign(velocity.X) * speedT;
			float verticalBank = MathHelper.Clamp(direction / MathHelper.PiOver2, -1f, 1f) * 0.25f;
			float target = (horizontalBank + verticalBank) * MathF.Max(0f, RotationMagnitude);
			return MathHelper.Clamp(target, -RotationMagnitude, RotationMagnitude);
		}

		private void OnChangeBattlePhase(ChangeBattlePhaseEvent e)
		{
			LoggingService.Append("GuardianAngelDisplaySystem.OnChangeBattlePhase", new JsonObject {
				{ "Current", e.Current.ToString() },
				{ "Previous", e.Previous.ToString() }
			});
			if (e.Current == SubPhase.StartBattle)
			{
				EnqueueSpeech(
					"phase:start",
					GuardianAngelMessageService.GetMessage(GuardianAmbientMessageType.StartOfBattle),
					GuardianSpeechCategory.Phase,
					50,
					2f,
					GuardianFlightGesture.Flourish);
			}
			else if (e.Current == SubPhase.Action)
			{
				EnqueueSpeech(
					"phase:action",
					GuardianAngelMessageService.GetMessage(GuardianAmbientMessageType.ActionPhase),
					GuardianSpeechCategory.Phase,
					50,
					2f,
					GuardianFlightGesture.Flourish);
			}
			else if (e.Current == SubPhase.PreBlock
				&& EnemyAttackFlowService.TryGetCurrentEnemyAttack(EntityManager, out _, out _, out var planned))
			{
				EnqueueSpeech(
					$"attack:{planned.AttackId}",
					GuardianAngelMessageService.GetEnemyAttackMessage(planned.AttackId),
					GuardianSpeechCategory.EnemyAttack,
					300,
					6f,
					GuardianFlightGesture.EnemyBrace);
			}
		}

		private void OnTriggerTemperance(TriggerTemperance e)
		{
			LoggingService.Append("GuardianAngelDisplaySystem.OnTriggerTemperance", new JsonObject {
				{ "AbilityId", e.AbilityId }
			});
			EnqueueSpeech(
				"temperance",
				GuardianAngelMessageService.GetMessage(GuardianAmbientMessageType.Temperance),
				GuardianSpeechCategory.Medal,
				250,
				5f,
				GuardianFlightGesture.MedalLoop);
		}

		private void OnCardPlayed(CardPlayedEvent e)
		{
			var card = e?.Card?.GetComponent<CardData>()?.Card;
			if (card == null || !GameIdExtensions.TryParseCardId(card.CardId, out var cardId)) return;
			EnqueueSpeech(
				$"card:{cardId}",
				GuardianAngelMessageService.GetCardMessage(cardId),
				GuardianSpeechCategory.Card,
				100,
				3f,
				GuardianFlightGesture.CardHop);
		}

		private void OnMedalTriggered(MedalTriggered e)
		{
			if (e == null || !GameIdExtensions.TryParseMedalId(e.MedalId, out var medalId)) return;
			EnqueueSpeech(
				$"medal:{medalId}",
				GuardianAngelMessageService.GetMedalMessage(medalId),
				GuardianSpeechCategory.Medal,
				250,
				5f,
				GuardianFlightGesture.MedalLoop);
		}

		private void OnLoadSceneEvent(LoadSceneEvent @event)
		{
			LoggingService.Append("GuardianAngelDisplaySystem.OnLoadSceneEvent", new JsonObject {
				{ "Scene", @event.Scene.ToString() }
			});
			_bubbleActive = false;
			_activeSpeech = null;
			_speechQueue.Clear();
			_speechGapRemaining = 0f;
			_flightGesture = GuardianFlightGesture.None;
		}

		private void EnqueueSpeech(
			string sourceKey,
			string text,
			GuardianSpeechCategory category,
			int priority,
			float maxAge,
			GuardianFlightGesture gesture)
		{
			var request = new GuardianSpeechRequest(sourceKey, text, category, priority, _t, maxAge, gesture);
			if (GuardianAngelSpeechQueue.ShouldInterrupt(_activeSpeech, request))
			{
				StartSpeech(request);
				return;
			}

			_speechQueue.Enqueue(request, _t);
			if (!_bubbleActive && _speechGapRemaining <= 0f)
				StartNextSpeech();
		}

		private void StartSpeech(GuardianSpeechRequest request)
		{
			_activeSpeech = request;
			_bubbleText = request.Text ?? string.Empty;
			_bubbleElapsed = 0f;
			_activeBubbleDuration = MathF.Min(BubbleDuration, GuardianAngelSpeechQueue.GetDisplayDuration(_bubbleText));
			_bubbleActive = true;
			_speechGapRemaining = 0f;
			StartFlightGesture(request.Gesture);
			_bubbleTexW = 0; _bubbleTexH = 0;
		}

		private void StartNextSpeech()
		{
			if (_speechQueue.TryDequeue(_t, out var request)) StartSpeech(request);
		}

		private void UpdateSpeech(float dt)
		{
			if (_bubbleActive)
			{
				_bubbleElapsed += dt;
				if (_bubbleElapsed < _activeBubbleDuration) return;
				_bubbleActive = false;
				_activeSpeech = null;
				_speechGapRemaining = SpeechGapSeconds;
			}

			if (_speechGapRemaining > 0f)
			{
				_speechGapRemaining = MathF.Max(0f, _speechGapRemaining - dt);
				if (_speechGapRemaining > 0f) return;
			}
			StartNextSpeech();
		}

		private void StartFlightGesture(GuardianFlightGesture gesture)
		{
			_flightGesture = gesture;
			_gestureElapsed = 0f;
			_gestureDuration = gesture switch
			{
				GuardianFlightGesture.EnemyBrace => 0.8f,
				GuardianFlightGesture.MedalLoop => 1.15f,
				GuardianFlightGesture.CardHop => 0.65f,
				GuardianFlightGesture.Flourish => 0.85f,
				_ => 0f,
			};
		}

		private void UpdateFlightGesture(float dt)
		{
			if (_flightGesture == GuardianFlightGesture.None) return;
			_gestureElapsed += dt;
			if (_gestureElapsed >= _gestureDuration)
			{
				_flightGesture = GuardianFlightGesture.None;
				_renderScaleMultiplier = 1f;
				return;
			}
			_renderScaleMultiplier = GetFlightGestureSample().ScaleMultiplier;
		}

		private GuardianFlightSample GetFlightGestureSample() =>
			GuardianAngelFlightService.SampleGesture(_flightGesture, _gestureElapsed, _gestureDuration);

		internal void DebugShowSpeech(string text, GuardianFlightGesture gesture)
		{
			StartSpeech(new GuardianSpeechRequest(
				"debug",
				text,
				GuardianSpeechCategory.Phase,
				50,
				_t,
				10f,
				gesture));
		}

		private void SpawnSparkle(Vector2 origin)
		{
			if (_pixel == null) return;
			float speed = Lerp(SparkleSpeedMin, SparkleSpeedMax, NextFloat());
			float rad = Lerp(0f, MathF.Tau, NextFloat());
			Vector2 dir = new Vector2(MathF.Cos(rad), MathF.Sin(rad));
			Vector2 jitter = new Vector2(
				Lerp(-SparkleSpawnJitterX, SparkleSpawnJitterX, NextFloat()),
				Lerp(-SparkleSpawnJitterY, SparkleSpawnJitterY, NextFloat())
			);
			Vector2 motionInfluence = -_lastMotionVelocity * MathHelper.Clamp(SparkleMotionInfluence, 0f, 1f);
			float lifetime = Lerp(SparkleLifetimeMin, SparkleLifetimeMax, NextFloat());
			float size = Lerp(SparkleSizeMin, SparkleSizeMax, NextFloat());
			_sparkles.Add(new SparkleParticle
			{
				Position = origin + jitter,
				Velocity = dir * speed + motionInfluence,
				Age = 0f,
				Lifetime = lifetime,
				Size = size,
				TwinkleSpeed = Lerp(TwinkleSpeedMin, TwinkleSpeedMax, NextFloat()),
				TwinklePhase = Lerp(0f, MathF.Tau, NextFloat()),
				DriftPhase = Lerp(0f, MathF.Tau, NextFloat()),
				DriftSpeed = Lerp(0.6f, 1.8f, NextFloat()),
				DriftStrength = Lerp(0f, SparkleDrift, NextFloat())
			});
		}

		private static float Lerp(float a, float b, float t) => a + (b - a) * t;

		private static float SmoothStep(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		private static float NextFloat() => (float)_rand.NextDouble();

		private static string WrapText(SpriteFont font, string text, int maxWidthPx, float scale)
		{
			if (font == null || string.IsNullOrEmpty(text) || maxWidthPx <= 0) return text ?? string.Empty;
			string[] words = text.Replace("\r", "").Split(' ');
			string line = string.Empty;
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			float scaledMax = maxWidthPx / Math.Max(0.0001f, scale);
			for (int i = 0; i < words.Length; i++)
			{
				string w = words[i];
				string testLine = string.IsNullOrEmpty(line) ? w : line + " " + w;
				if (font.MeasureString(testLine).X <= scaledMax)
				{
					line = testLine;
				}
				else
				{
					if (line.Length > 0)
					{
						sb.AppendLine(line);
						line = w;
					}
					else
					{
						// single word longer than max, hard-break
						int charCount = w.Length;
						int start = 0;
						while (start < charCount)
						{
							int len = 1;
							while (start + len <= charCount && font.MeasureString(w.Substring(start, len)).X <= scaledMax) len++;
							len = Math.Max(1, len - 1);
							sb.AppendLine(w.Substring(start, len));
							start += len;
						}
						line = string.Empty;
					}
				}
			}
			if (!string.IsNullOrEmpty(line)) sb.Append(line);
			return sb.ToString();
		}

        public void Draw()
        {
			if (_angelTexture == null)
            {
                _angelTexture = _imageAssets.TryGetTexture("guardian_angel");
                if (_angelTexture == null) return;
            }

			// Use the guardian entity's parallax-adjusted position for drawing
			var guardian = EntityManager.GetEntity(GuardianEntityName);
			var gt = guardian?.GetComponent<Transform>();
			if (gt == null) return;
			Vector2 pos = gt.Position;

			var origin = new Vector2(_angelTexture.Width / 2f, _angelTexture.Height / 2f);
			float rotation = gt.Rotation;

            var color = Color.White * Alpha;
			// Draw sparkles behind the angel
			for (int i = 0; i < _sparkles.Count; i++)
			{
				var p = _sparkles[i];
				float lifeT = MathF.Min(1f, p.Age / MathF.Max(0.0001f, p.Lifetime));
				float twinkle = 0.5f + 0.5f * MathF.Sin(p.TwinklePhase + p.Age * p.TwinkleSpeed);
				float fadeIn = SmoothStep(MathHelper.Clamp(lifeT * 5f, 0f, 1f));
				float fadeOut = 1f - SmoothStep(MathHelper.Clamp((lifeT - 0.62f) / 0.38f, 0f, 1f));
				float alpha = fadeIn * fadeOut * (0.32f + 0.68f * twinkle);
				var c = new Color(255, 248, 214) * MathHelper.Clamp(alpha, 0f, 1f);
				float s = p.Size * (0.75f + 0.55f * twinkle);
				Vector2 sparkleOrigin = new Vector2(0.5f, 0.5f);
				_spriteBatch.Draw(_pixel, p.Position, null, c, 0f, sparkleOrigin, new Vector2(s, s), SpriteEffects.None, 0f);
				Color glintColor = c * 0.55f;
				_spriteBatch.Draw(_pixel, p.Position, null, glintColor, 0f, sparkleOrigin, new Vector2(s * 2.2f, 1f), SpriteEffects.None, 0f);
				_spriteBatch.Draw(_pixel, p.Position, null, glintColor, 0f, sparkleOrigin, new Vector2(1f, s * 2.2f), SpriteEffects.None, 0f);
			}

			// Draw speech bubble (follows angel)
			if (_bubbleActive && _font != null)
			{
				string wrapped = WrapText(_font, _bubbleText, BubbleMaxWidth, BubbleTextScale);
				var size = _font.MeasureString(wrapped) * BubbleTextScale;
				int bw = (int)Math.Ceiling(size.X) + BubblePadX * 2;
				int bh = (int)Math.Ceiling(size.Y) + BubblePadY * 2;
				if (bw <= 0) bw = 1; if (bh <= 0) bh = 1;
				if (_bubbleTexture == null || bw != _bubbleTexW || bh != _bubbleTexH)
				{
					_bubbleTexture?.Dispose();
					_bubbleTexture = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, bw, bh, BubbleCornerRadius);
					_bubbleTexW = bw; _bubbleTexH = bh;
				}
				// Anchor bubble bottom-left to sprite top-right (plus offsets)
				Vector2 spriteTopRight = pos + new Vector2(_angelTexture.Width * 0.5f * Scale, -_angelTexture.Height * 0.5f * Scale);
				Vector2 bubbleBottomLeft = spriteTopRight + new Vector2(BubbleOffsetX, BubbleOffsetY);
				Vector2 bubbleTopLeft = bubbleBottomLeft - new Vector2(0, bh);
				// Compute fade alpha based on elapsed time
				float aIn = BubbleFadeInSeconds <= 0f ? 1f : MathHelper.Clamp(_bubbleElapsed / BubbleFadeInSeconds, 0f, 1f);
				float aOut = 1f;
				if (BubbleFadeOutSeconds > 0f)
				{
					float tRemain = Math.Max(0f, _activeBubbleDuration - _bubbleElapsed);
					aOut = MathHelper.Clamp(tRemain / BubbleFadeOutSeconds, 0f, 1f);
				}
				float a = MathHelper.Clamp(aIn * aOut, 0f, 1f);
				Color bg = Color.White * (BubbleBgAlpha * a);
				_spriteBatch.Draw(_bubbleTexture, bubbleTopLeft, bg);
				// Text with wrapping
				Vector2 textPos = bubbleTopLeft + new Vector2(BubblePadX, BubblePadY);
				var textColor = new Color(0, 0, 0) * a;
				_spriteBatch.DrawString(_font, wrapped, textPos, textColor, 0f, Vector2.Zero, BubbleTextScale, SpriteEffects.None, 0f);
			}

			// Draw angel
			_spriteBatch.Draw(_angelTexture, pos, null, color, rotation, origin, Scale * _renderScaleMultiplier, SpriteEffects.None, 0f);
		}

		private void EnsureGuardianEntity()
		{
			var e = EntityManager.GetEntity(GuardianEntityName);
			if (e == null)
			{
				e = EntityManager.CreateEntity(GuardianEntityName);
				// Seed initial transform; will be updated each frame
				EntityManager.AddComponent(e, new Transform { Position = Vector2.Zero, ZOrder = 0 });
				EntityManager.AddComponent(e, ParallaxLayer.GetCharacterParallaxLayer());
				EntityManager.AddComponent(e, new PositionTween { Speed = 10f });
			}
        }
    }
}
