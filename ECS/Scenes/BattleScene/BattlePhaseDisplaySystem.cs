using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Animates a cinematic phase transition with converging trapezoids when the phase changes.
	/// </summary>
	[DebugTab("Battle Phase Display")]
	public class BattlePhaseDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.TitleFont;

		// --- Animation Timing ---
		[DebugEditable(DisplayName = "Phase In Duration (s)", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float PhaseInDuration { get; set; } = 0.2f;
		[DebugEditable(DisplayName = "Phase Hold Duration (s)", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float PhaseHoldDuration { get; set; } = 0.45f;
		[DebugEditable(DisplayName = "Phase Out Duration (s)", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float PhaseOutDuration { get; set; } = 0.2f;

		// --- Text Animation ---
		[DebugEditable(DisplayName = "Text Spawn Offset X", Step = 10f, Min = -2000f, Max = 2000f)]
		public float TextSpawnOffsetX { get; set; } = 400f;
		[DebugEditable(DisplayName = "Text Spawn Offset Y", Step = 10f, Min = -2000f, Max = 2000f)]
		public float TextSpawnOffsetY { get; set; } = -200f;
		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.2f, Max = 4f)]
		public float TextScale { get; set; } = 0.6f;
		[DebugEditable(DisplayName = "Text Fade In %", Step = 0.05f, Min = 0.1f, Max = 1f)]
		public float TextFadeInPercent { get; set; } = 1f;
		[DebugEditable(DisplayName = "Text Landing Overshoot", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float TextLandingOvershoot { get; set; } = 0.1f;
		[DebugEditable(DisplayName = "Text Exit Distance", Step = 10f, Min = 0f, Max = 1000f)]
		public float TextExitDistance { get; set; } = 120f;
		[DebugEditable(DisplayName = "Text Extrusion X", Step = 1f, Min = -50f, Max = 50f)]
		public float TextExtrusionX { get; set; } = 9f;
		[DebugEditable(DisplayName = "Text Extrusion Y", Step = 1f, Min = -50f, Max = 50f)]
		public float TextExtrusionY { get; set; } = 7f;
		[DebugEditable(DisplayName = "Text Outline Thickness", Step = 1f, Min = 0f, Max = 10f)]
		public float TextOutlineThickness { get; set; } = 2f;

		// --- Title Plate ---
		[DebugEditable(DisplayName = "Plate Padding X", Step = 5f, Min = 0f, Max = 300f)]
		public float PlatePaddingX { get; set; } = 72f;
		[DebugEditable(DisplayName = "Plate Padding Y", Step = 5f, Min = 0f, Max = 150f)]
		public float PlatePaddingY { get; set; } = 22f;
		[DebugEditable(DisplayName = "Plate Slant Angle", Step = 1f, Min = 0f, Max = 60f)]
		public float PlateSlantAngle { get; set; } = 18f;
		[DebugEditable(DisplayName = "Plate Opacity", Step = 0.05f, Min = 0f, Max = 1f)]
		public float PlateOpacity { get; set; } = 0.92f;
		[DebugEditable(DisplayName = "Plate Accent Thickness", Step = 1f, Min = 1f, Max = 40f)]
		public float PlateAccentThickness { get; set; } = 7f;

		// --- Strip Configuration ---
		[DebugEditable(DisplayName = "Base Strip Length", Step = 50f, Min = 100f, Max = 3000f)]
		public float BaseStripLength { get; set; } = 300f;
		[DebugEditable(DisplayName = "Base Strip Thickness", Step = 10f, Min = 10f, Max = 500f)]
		public float BaseStripThickness { get; set; } = 20f;
		[DebugEditable(DisplayName = "Strip Angle (Deg)", Step = 1f, Min = -180f, Max = 180f)]
		public float StripAngleDeg { get; set; } = -45f; // / Shape
		[DebugEditable(DisplayName = "Strip Slant Angle", Step = 1f, Min = 0f, Max = 89f)]
		public float StripSlantAngle { get; set; } = 45f; 

		// --- Strip Motion ---
		[DebugEditable(DisplayName = "Spawn Distance", Step = 50f, Min = 0f, Max = 4000f)]
		public float SpawnDistance { get; set; } = 2500f;
		[DebugEditable(DisplayName = "Converge Overshoot", Step = 10f, Min = -500f, Max = 500f)]
		public float ConvergeOvershoot { get; set; } = 40f; // How far past center they go
		[DebugEditable(DisplayName = "Lateral Spread", Step = 10f, Min = 0f, Max = 1000f)]
		public float LateralSpread { get; set; } = 160f; // Spread perpendicular to motion
		[DebugEditable(DisplayName = "Hold Move Dist", Step = 10f, Min = 0f, Max = 1000f)]
		public float HoldMoveDistance { get; set; } = 100f; // Distance moved during hold phase
		[DebugEditable(DisplayName = "Strip Count", Step = 1f, Min = 2f, Max = 40f)]
		public int StripCount { get; set; } = 12;
		[DebugEditable(DisplayName = "Strip Longitudinal Variance", Step = 10f, Min = 0f, Max = 1000f)]
		public float StripLongitudinalVariance { get; set; } = 200f;
		[DebugEditable(DisplayName = "Strip Exit Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
		public float StripExitAlpha { get; set; } = 0.5f;

		private enum AnimState
		{
			None,
			Entering,
			Holding,
			Exiting
		}

		private AnimState _animState = AnimState.None;
		private float _animTimer = 0f;
		private string _transitionText = string.Empty;
		
		private SubPhase _lastPhase = SubPhase.StartBattle;
		private int _lastTurn = 0;
		private bool _shownBlockAnimationForTurn = false;
		private bool _isVictoryAnimation = false;
		private Texture2D _plateTexture;
		private Vector2 _plateSize;
		private TransitionPresentation _presentation;

		private static readonly Color InkColor = new Color(7, 7, 9);
		private static readonly Color IvoryColor = new Color(244, 239, 225);
		private static readonly Color CrimsonColor = new Color(174, 24, 44);

		private enum AccentStyle
		{
			Start,
			Block,
			Action,
			Pledge,
			Victory
		}

		private readonly struct TransitionPresentation
		{
			public AccentStyle Style { get; init; }
			public Color Primary { get; init; }
			public Color Secondary { get; init; }
			public float Scale { get; init; }
		}

		// Strip Definition
		private struct Strip
		{
			public float Length;
			public float Thickness;
			public Color Color;
			public float LateralOffset; // Perpendicular offset
			public float LongitudinalOffset; // Offset along movement (delay)
			public bool FromBottomLeft; // True = BL->Center, False = TR->Center
			public Texture2D Texture;
		}

		private List<Strip> _strips = new List<Strip>();

		public BattlePhaseDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;

			EventManager.Subscribe<ShowStartOfBattleAnimationEvent>(_ => {
				LoggingService.Append("BattlePhaseDisplaySystem.OnShowStartOfBattleAnimation", new System.Text.Json.Nodes.JsonObject { ["event"] = "ShowStartOfBattleAnimationEvent" });
				StartAnimation(SubPhaseToString(SubPhase.StartBattle), ResolvePresentation(SubPhase.StartBattle));
			});
			EventManager.Subscribe<ShowVictoryAnimationEvent>(_ => {
				LoggingService.Append("BattlePhaseDisplaySystem.OnShowVictoryAnimation", new System.Text.Json.Nodes.JsonObject { ["event"] = "ShowVictoryAnimationEvent" });
				StartVictoryAnimation();
			});
			EventManager.Subscribe<DeleteCachesEvent>(_ => {
				_animState = AnimState.None;
				_animTimer = 0f;
				_transitionText = string.Empty;
				_lastPhase = SubPhase.StartBattle;
				_lastTurn = 0;
				_shownBlockAnimationForTurn = false;
				_isVictoryAnimation = false;
				_strips.Clear();
				_plateTexture = null;
				_plateSize = Vector2.Zero;
			});
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<PhaseState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var phase = entity.GetComponent<PhaseState>();

			bool phaseChanged = false;
			if (_lastTurn != phase.TurnNumber)
			{
				phaseChanged = true;
			}
			else if (_lastPhase != phase.Sub)
			{
				phaseChanged = true;
			}

		if (phaseChanged)
		{
			var prev = _lastPhase;
			_lastPhase = phase.Sub;
			
			if (_lastTurn != phase.TurnNumber)
			{
				_lastTurn = phase.TurnNumber;
				_shownBlockAnimationForTurn = false;
			}

			// Skip StartBattle - only triggered via ShowStartOfBattleAnimationEvent
			if (phase.Sub == SubPhase.StartBattle)
				return;

			string newText = SubPhaseToString(_lastPhase);
			if (!string.IsNullOrWhiteSpace(newText))
			{
				// Suppress animation if we have already shown Block phase animation this turn
				if (phase.Sub == SubPhase.Block)
				{
					if (_shownBlockAnimationForTurn)
					{
						// Already shown for this turn, suppress
					}
					else
					{
						_shownBlockAnimationForTurn = true;
						StartAnimation(newText, ResolvePresentation(phase.Sub));
					}
				}
				else
				{
					StartAnimation(newText, ResolvePresentation(phase.Sub));
				}
			}
		}

			if (_animState != AnimState.None)
			{
				_animTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
				switch (_animState)
				{
					case AnimState.Entering:
						if (_animTimer >= PhaseInDuration)
						{
							_animTimer = 0f;
							_animState = AnimState.Holding;
						}
						break;
					case AnimState.Holding:
						if (_animTimer >= PhaseHoldDuration)
						{
							_animTimer = 0f;
							_animState = AnimState.Exiting;
						}
						break;
					case AnimState.Exiting:
						if (_animTimer >= PhaseOutDuration)
						{
							if (_isVictoryAnimation)
							{
								EventManager.Publish(new VictoryAnimationCompleteEvent());
							}
							else
							{
								EventManager.Publish(new BattlePhaseAnimationCompleteEvent{ SubPhase = _lastPhase });
							}
							StopAnimation();
						}
						break;
				}
			}
		}

		private void StartAnimation(string text, TransitionPresentation presentation)
		{
			_isVictoryAnimation = false;
			_transitionText = text;
			_presentation = presentation;
			_animState = AnimState.Entering;
			_animTimer = 0f;
			PrepareVisuals();
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.PhaseChange, Volume = 0.5f });
		}

		private void StartVictoryAnimation()
		{
			_isVictoryAnimation = true;
			_transitionText = "Victory!";
			_presentation = ResolveVictoryPresentation();
			_animState = AnimState.Entering;
			_animTimer = 0f;
			PrepareVisuals();
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.PhaseChange, Volume = 0.5f });
		}

		private void StopAnimation()
		{
			_animState = AnimState.None;
			_animTimer = 0f;
			_isVictoryAnimation = false;
		}

		private void PrepareVisuals()
		{
			GenerateStrips();
			GeneratePlateTexture();
		}

		private void GeneratePlateTexture()
		{
			Vector2 textSize = _font.MeasureString(_transitionText) * TextScale * _presentation.Scale;
			float width = Math.Max(1f, textSize.X + PlatePaddingX * 2f);
			float height = Math.Max(1f, textSize.Y + PlatePaddingY * 2f);
			_plateSize = new Vector2(width, height);
			_plateTexture = PrimitiveTextureFactory.GetAntialiasedTrapezoidMask(
				_graphicsDevice,
				width,
				height,
				0f,
				0f,
				-PlateSlantAngle,
				0f,
				PlateSlantAngle);
		}

		private void GenerateStrips()
		{
			_strips.Clear();
			var rng = new Random(12345); // Fixed seed for consistent look, or use Time for random

			// Alternate strips from both corners to preserve the opposing convergence.
			for (int i = 0; i < StripCount; i++)
			{
				bool fromBL = i % 2 == 0;
				
				// Varied size
				float lenMult = 0.8f + (float)rng.NextDouble() * 0.6f; // 0.8x to 1.4x
				float thickMult = 0.5f + (float)rng.NextDouble() * 1.0f; // 0.5x to 1.5x
				
				float w = BaseStripLength * lenMult;
				float h = BaseStripThickness * thickMult;

				// Varied color: Black and DarkRed only
				Color c;
				double r = rng.NextDouble();
				if (r < 0.5) c = Color.Black;
				else c = Color.DarkRed;

				// Varied offsets
				float latOff = ((float)rng.NextDouble() * 2f - 1f) * LateralSpread; // -Spread to +Spread
				float longOff = ((float)rng.NextDouble() * 2f - 1f) * StripLongitudinalVariance;

				// Create Texture
				var tex = PrimitiveTextureFactory.GetAntialiasedTrapezoidMask(
					_graphicsDevice,
					w, h,
					0f, 0f, -StripSlantAngle, 0f, StripSlantAngle
				);

				_strips.Add(new Strip
				{
					Length = w,
					Thickness = h,
					Color = c,
					LateralOffset = latOff,
					LongitudinalOffset = longOff,
					FromBottomLeft = fromBL,
					Texture = tex
				});
			}
		}

		public void Draw()
		{
			DrawTransition();
		}

		private void DrawTransition()
		{
			if (_animState == AnimState.None) return;
			if (_strips.Count == 0 || _plateTexture == null) return;

			Vector2 centerScreen = new Vector2(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f);
			
			// Calculate global progress
			float travelPos = 0f; // 0=Start, 1=Converged, 2=End
			
			// Calculate how much 'travelPos' corresponds to the HoldMoveDistance
			// Base distance covered in exit phase is (ConvergeOvershoot - (-SpawnDistance))
			float exitPhaseLength = Math.Abs(ConvergeOvershoot - (-SpawnDistance));
			float holdProgress = HoldMoveDistance / Math.Max(1f, exitPhaseLength);

			if (_animState == AnimState.Entering)
			{
				float t = MathHelper.Clamp(_animTimer / PhaseInDuration, 0f, 1f);
				travelPos = EaseOutCubic(t); 
			}
			else if (_animState == AnimState.Holding)
			{
				float t = MathHelper.Clamp(_animTimer / PhaseHoldDuration, 0f, 1f);
				// Slowly drift
				travelPos = 1f + t * holdProgress;
			}
			else if (_animState == AnimState.Exiting)
			{
				float t = MathHelper.Clamp(_animTimer / PhaseOutDuration, 0f, 1f);
				// Resume from where hold left off
				travelPos = (1f + holdProgress) + EaseInCubic(t) * (1f - holdProgress); 
			}

			// Direction vectors
			Vector2 dirBL = new Vector2(1, -1); 
			dirBL.Normalize();
			Vector2 dirTR = new Vector2(-1, 1);
			dirTR.Normalize();
			
			float rotRad = MathHelper.ToRadians(StripAngleDeg);
			Vector2 stripDir = new Vector2((float)Math.Cos(rotRad), (float)Math.Sin(rotRad));
			Vector2 perpDir = new Vector2(-stripDir.Y, stripDir.X); // Perpendicular for lateral spread

			foreach (var strip in _strips)
			{
				Vector2 moveDir = strip.FromBottomLeft ? dirBL : dirTR;
				
				// Distances
				// Start: Far away. End: Center (plus overshoot).
				float startDist = SpawnDistance + strip.LongitudinalOffset;
				float endDist = ConvergeOvershoot + strip.LongitudinalOffset * 0.1f; // Compress offset at target
				float throughDist = -SpawnDistance; // Go past

				float currentDist = 0f;
				float alpha = 1f;

				if (travelPos <= 1f)
				{
					currentDist = MathHelper.Lerp(startDist, endDist, travelPos);
				}
				else
				{
					currentDist = MathHelper.Lerp(endDist, throughDist, travelPos - 1f);
					alpha = 1f - (travelPos - 1f) * (1f - StripExitAlpha);
				}

				Vector2 pos = centerScreen + perpDir * strip.LateralOffset - moveDir * currentDist;
				
				Vector2 origin = new Vector2(strip.Texture.Width / 2f, strip.Texture.Height / 2f);
				
				_spriteBatch.Draw(strip.Texture, pos, null, strip.Color * alpha, rotRad, origin, Vector2.One, SpriteEffects.None, 0f);
			}

			DrawText(centerScreen);
		}

		private void DrawText(Vector2 center)
		{
			if (string.IsNullOrEmpty(_transitionText)) return;

			float alpha = 1f;
			Vector2 offset = Vector2.Zero;
			float landingScale = 1f;
			float plateWidthScale = 1f;

			if (_animState == AnimState.Entering)
			{
				float t = MathHelper.Clamp(_animTimer / PhaseInDuration, 0f, 1f);
				
				float fadeEnd = TextFadeInPercent;
				if (t < fadeEnd)
					alpha = t / fadeEnd;
				else
					alpha = 1f;

				float moveProgress = EaseOutCubic(t);
				offset = Vector2.Lerp(new Vector2(TextSpawnOffsetX, TextSpawnOffsetY), Vector2.Zero, moveProgress);
				plateWidthScale = MathHelper.Lerp(0.58f, 1f, moveProgress);
				landingScale = CalculateLandingScale(t);
			}
			else if (_animState == AnimState.Holding)
			{
				alpha = 1f;
				offset = Vector2.Zero;
			}
			else if (_animState == AnimState.Exiting)
			{
				float t = MathHelper.Clamp(_animTimer / PhaseOutDuration, 0f, 1f);
				alpha = 1f - t;
				float exitProgress = EaseInCubic(t);
				offset = new Vector2(-TextExitDistance, TextExitDistance * 0.45f) * exitProgress;
				plateWidthScale = MathHelper.Lerp(1f, 0.72f, exitProgress);
			}

			if (alpha <= 0.01f) return;

			Vector2 textSize = _font.MeasureString(_transitionText);
			Vector2 textOrigin = textSize / 2f;
			Vector2 textPos = center + offset;
			float scale = TextScale * _presentation.Scale * landingScale;

			DrawTitlePlate(textPos, alpha, plateWidthScale, landingScale);
			DrawLayeredTitle(textPos, textOrigin, scale, alpha);
		}

		private float CalculateLandingScale(float t)
		{
			const float overshootTime = 0.72f;
			if (t <= overshootTime)
			{
				float rise = EaseOutCubic(t / overshootTime);
				return MathHelper.Lerp(0.92f, 1f + TextLandingOvershoot, rise);
			}

			float settle = (t - overshootTime) / (1f - overshootTime);
			return MathHelper.Lerp(1f + TextLandingOvershoot, 1f, EaseInOutCubic(settle));
		}

		private void DrawTitlePlate(Vector2 center, float alpha, float widthScale, float landingScale)
		{
			float width = _plateSize.X * widthScale * landingScale;
			float height = _plateSize.Y * landingScale;
			float accent = PlateAccentThickness * landingScale;
			float plateAlpha = alpha * PlateOpacity;

			if (_presentation.Style == AccentStyle.Victory)
			{
				DrawPlateLayer(center, width + accent * 3f, height + accent * 2f, _presentation.Primary * plateAlpha);
			}
			else if (_presentation.Style == AccentStyle.Start)
			{
				DrawPlateLayer(center + new Vector2(-accent, accent), width + accent * 2f, height + accent, _presentation.Secondary * plateAlpha);
			}

			DrawPlateLayer(center, width, height, InkColor * plateAlpha);

			switch (_presentation.Style)
			{
				case AccentStyle.Start:
					DrawPlateLayer(center + new Vector2(-width * 0.08f, -height * 0.5f + accent * 0.5f), width * 0.72f, accent, _presentation.Primary * alpha);
					DrawPlateLayer(center + new Vector2(width * 0.08f, height * 0.5f - accent * 0.5f), width * 0.9f, accent, _presentation.Secondary * alpha);
					break;
				case AccentStyle.Block:
					DrawPlateLayer(center + new Vector2(0f, -height * 0.5f + accent * 0.5f), width * 0.46f, accent, _presentation.Primary * alpha);
					DrawPlateLayer(center + new Vector2(0f, height * 0.5f - accent * 0.5f), width * 0.62f, accent, _presentation.Secondary * alpha);
					break;
				case AccentStyle.Action:
					DrawPlateLayer(center + new Vector2(width * 0.05f, height * 0.5f - accent * 0.5f), width * 0.88f, accent, _presentation.Primary * alpha);
					break;
				case AccentStyle.Pledge:
					DrawPlateLayer(center + new Vector2(-width * 0.05f, -height * 0.5f + accent * 0.5f), width * 0.8f, accent, _presentation.Primary * alpha);
					DrawPlateLayer(center + new Vector2(width * 0.05f, height * 0.5f - accent * 0.5f), width * 0.8f, accent, _presentation.Secondary * alpha);
					break;
				case AccentStyle.Victory:
					DrawPlateLayer(center + new Vector2(0f, height * 0.5f - accent * 0.5f), width * 0.92f, accent, _presentation.Secondary * alpha);
					DrawPlateLayer(center + new Vector2(0f, height * 0.5f - accent * 2f), width * 0.68f, accent * 0.65f, _presentation.Primary * alpha);
					break;
			}
		}

		private void DrawPlateLayer(Vector2 center, float width, float height, Color color)
		{
			Vector2 origin = new Vector2(_plateTexture.Width / 2f, _plateTexture.Height / 2f);
			Vector2 scale = new Vector2(
				Math.Max(0.001f, width / _plateTexture.Width),
				Math.Max(0.001f, height / _plateTexture.Height));
			_spriteBatch.Draw(_plateTexture, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
		}

		private void DrawLayeredTitle(Vector2 position, Vector2 origin, float scale, float alpha)
		{
			Vector2 extrusion = new Vector2(TextExtrusionX, TextExtrusionY);
			_spriteBatch.DrawString(_font, _transitionText, position + extrusion, _presentation.Primary * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_font, _transitionText, position + extrusion * 0.48f, InkColor * alpha, 0f, origin, scale, SpriteEffects.None, 0f);

			if (TextOutlineThickness > 0f)
			{
				float o = TextOutlineThickness;
				DrawTitleOutline(position + new Vector2(-o, 0f), origin, scale, alpha);
				DrawTitleOutline(position + new Vector2(o, 0f), origin, scale, alpha);
				DrawTitleOutline(position + new Vector2(0f, -o), origin, scale, alpha);
				DrawTitleOutline(position + new Vector2(0f, o), origin, scale, alpha);
			}

			_spriteBatch.DrawString(_font, _transitionText, position, IvoryColor * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
		}

		private void DrawTitleOutline(Vector2 position, Vector2 origin, float scale, float alpha)
		{
			_spriteBatch.DrawString(_font, _transitionText, position, InkColor * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
		}

		private static TransitionPresentation ResolvePresentation(SubPhase phase)
		{
			return phase switch
			{
				SubPhase.StartBattle => new TransitionPresentation { Style = AccentStyle.Start, Primary = IvoryColor, Secondary = CrimsonColor, Scale = 1f },
				SubPhase.Block => new TransitionPresentation { Style = AccentStyle.Block, Primary = IvoryColor, Secondary = CrimsonColor, Scale = 1f },
				SubPhase.Action => new TransitionPresentation { Style = AccentStyle.Action, Primary = CrimsonColor, Secondary = InkColor, Scale = 1f },
				SubPhase.Pledge => new TransitionPresentation { Style = AccentStyle.Pledge, Primary = IvoryColor, Secondary = CrimsonColor, Scale = 1f },
				_ => new TransitionPresentation { Style = AccentStyle.Action, Primary = CrimsonColor, Secondary = InkColor, Scale = 1f }
			};
		}

		private static TransitionPresentation ResolveVictoryPresentation()
		{
			return new TransitionPresentation
			{
				Style = AccentStyle.Victory,
				Primary = IvoryColor,
				Secondary = CrimsonColor,
				Scale = 1.08f
			};
		}

		private static string SubPhaseToString(SubPhase sp)
		{
			return sp switch
			{
				SubPhase.StartBattle => "Start of Battle",
				SubPhase.Block => "Block Phase",
				SubPhase.Action => "Action Phase",
				SubPhase.Pledge => "Pledge Phase",
				_ => ""
			};
		}

		private static float EaseOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float u = 1f - t;
			return 1f - u * u * u;
		}

		private static float EaseInCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * t;
		}

		private static float EaseInOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t < 0.5f
				? 4f * t * t * t
				: 1f - (float)Math.Pow(-2f * t + 2f, 3f) / 2f;
		}

		[DebugAction("Replay Transition Animation")]
		public void Debug_ReplayTransitionAnimation()
		{
			var stateEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			if (stateEntity != null)
			{
				var phase = stateEntity.GetComponent<PhaseState>();
				string text = SubPhaseToString(phase.Sub);
				if (!string.IsNullOrWhiteSpace(text))
				{
					StartAnimation(text, ResolvePresentation(phase.Sub));
				}
			}
		}

		[DebugAction("Regenerate Transition Visuals")]
		public void Debug_RegenerateStrips()
		{
			PrepareVisuals();
		}
	}
}
