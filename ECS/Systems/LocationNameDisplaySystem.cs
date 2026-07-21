using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Location Name Display")]
	public class LocationNameDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.TitleFont;
		private string _locationName = "";
		
		private enum AnimationPhase { Idle, EntryWaiting, Entering, Exiting, Complete }
		private AnimationPhase _phase = AnimationPhase.Idle;
		private float _animationTime = 0f;
		private float _trapezoidX = 0f;
		private float _textX = 0f;
		private float _startTrapezoidX = 0f;
		private float _startTextX = 0f;
		private float _targetTrapezoidX = 0f;
		private float _targetTextX = 0f;
		private int _viewportWidth = 0;
		private bool _modalSuppressed = false;

		// Trapezoid parameters
		[DebugEditable(DisplayName = "Trapezoid Width", Step = 10f, Min = 100f, Max = 1000f)]
		public float TrapezoidWidth { get; set; } = 700f;

		[DebugEditable(DisplayName = "Trapezoid Height", Step = 10f, Min = 50f, Max = 300f)]
		public float TrapezoidHeight { get; set; } = 110f;

		[DebugEditable(DisplayName = "Left Side Offset", Step = 5f, Min = 0f, Max = 100f)]
		public float LeftSideOffset { get; set; } = 20f;

		[DebugEditable(DisplayName = "Top Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float TopEdgeAngleDegrees { get; set; } = 2f;

		[DebugEditable(DisplayName = "Right Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float RightEdgeAngleDegrees { get; set; } = -26f;

		[DebugEditable(DisplayName = "Bottom Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float BottomEdgeAngleDegrees { get; set; } = -2f;

		[DebugEditable(DisplayName = "Left Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float LeftEdgeAngleDegrees { get; set; } = 9f;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.01f, Min = 0.1f, Max = 3f)]
		public float TextScale { get; set; } = .47f;

		[DebugEditable(DisplayName = "Animation Duration (s)", Step = 0.1f, Min = 0.1f, Max = 3f)]
		public float AnimationDurationSeconds { get; set; } = 0.3f;

		[DebugEditable(DisplayName = "Text Delay (s)", Step = 0.01f, Min = 0f, Max = 2f)]
		public float TextDelaySeconds { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Entry Delay (s)", Step = 0.01f, Min = 0f, Max = 5f)]
		public float EntryDelaySeconds { get; set; } = 0f;

		[DebugEditable(DisplayName = "Text Padding X", Step = 5f, Min = 0f, Max = 100f)]
		public float TextPaddingX { get; set; } = 15f;

		[DebugEditable(DisplayName = "Text Padding Y", Step = 5f, Min = 0f, Max = 100f)]
		public float TextPaddingY { get; set; } = 20f;

		public LocationNameDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;

			// Event-driven control
			EventManager.Subscribe<UpdateLocationNameEvent>(_ => SetTitle(_?.Title ?? ""));
			EventManager.Subscribe<LoadSceneEvent>(_ =>
			{
				if (_?.Scene == SceneId.WayStation)
				{
					SetTitle("Waystation");
				}
			});

			EventManager.Subscribe<HideLocationNameEvent>(_ =>
			{
				ClearTitle();
			});
			EventManager.Subscribe<DeleteCachesEvent>(_ =>
			{
				Console.WriteLine("[LocationNameDisplaySystem] DeleteCachesEvent");
				ClearTitle();
			});
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// We just need to tick once per frame; SceneState is guaranteed to exist
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			_viewportWidth = Game1.VirtualWidth;
			_targetTrapezoidX = 0f;
			_targetTextX = TextPaddingX;

			float offScreenX = _viewportWidth + TrapezoidWidth;
			SyncModalSuppression(scene);
			if (scene?.Current == SceneId.WayStation && string.IsNullOrEmpty(_locationName))
			{
				SetTitle("Waystation");
			}

			_animationTime += dt;

			switch (_phase)
			{
				case AnimationPhase.Idle:
					_trapezoidX = offScreenX;
					_textX = offScreenX;
					break;

				case AnimationPhase.EntryWaiting:
					if (_animationTime >= EntryDelaySeconds)
					{
						_phase = AnimationPhase.Entering;
						_animationTime = 0f;
						_startTrapezoidX = offScreenX;
						_startTextX = offScreenX;
					}
					_trapezoidX = offScreenX;
					_textX = offScreenX;
					break;

				case AnimationPhase.Entering:
					// Update trapezoid position
					if (_animationTime >= AnimationDurationSeconds)
					{
						_trapezoidX = _targetTrapezoidX;
					}
					else
					{
						float progress = _animationTime / AnimationDurationSeconds;
						float eased = EaseOutCubic(progress);
						_trapezoidX = MathHelper.Lerp(_startTrapezoidX, _targetTrapezoidX, eased);
					}

					// Update text position - starts after TextDelaySeconds from when trapezoid starts
					float textTime = _animationTime - TextDelaySeconds;
					if (textTime <= 0f)
					{
						_textX = _startTextX;
					}
					else if (textTime >= AnimationDurationSeconds)
					{
						_textX = _targetTextX;
					}
					else
					{
						float textProgress = textTime / AnimationDurationSeconds;
						float textEased = EaseOutCubic(textProgress);
						_textX = MathHelper.Lerp(_startTextX, _targetTextX, textEased);
					}

					// Transition to Complete when both animations are done
					float totalTextDuration = TextDelaySeconds + AnimationDurationSeconds;
					if (_animationTime >= Math.Max(AnimationDurationSeconds, totalTextDuration))
					{
						_trapezoidX = _targetTrapezoidX;
						_textX = _targetTextX;
						_phase = AnimationPhase.Complete;
					}
					break;

				case AnimationPhase.Complete:
					_trapezoidX = _targetTrapezoidX;
					_textX = _targetTextX;
					break;

				case AnimationPhase.Exiting:
					float exitTrapezoidX = -TrapezoidWidth;
					float exitTextX = GetTextOffScreenLeftX();
					if (_animationTime >= AnimationDurationSeconds)
					{
						_trapezoidX = exitTrapezoidX;
						_textX = exitTextX;
						_phase = AnimationPhase.Idle;
					}
					else
					{
						float progress = _animationTime / AnimationDurationSeconds;
						float eased = EaseOutCubic(progress);
						_trapezoidX = MathHelper.Lerp(_startTrapezoidX, exitTrapezoidX, eased);
						_textX = MathHelper.Lerp(_startTextX, exitTextX, eased);
					}
					break;
			}
		}

		private void SetTitle(string title)
		{
			_locationName = title ?? "";
			if (string.IsNullOrEmpty(_locationName))
			{
				ClearTitle();
				return;
			}

			if (!_modalSuppressed)
			{
				StartEntryAnimation();
			}
		}

		private void ClearTitle()
		{
			_locationName = "";
			_modalSuppressed = false;
			_phase = AnimationPhase.Idle;
			_animationTime = 0f;
		}

		private void SyncModalSuppression(SceneState scene)
		{
			bool shouldSuppress = scene?.Current == SceneId.WayStation && IsAnyWayStationModalOpen();
			if (shouldSuppress == _modalSuppressed) return;

			_modalSuppressed = shouldSuppress;
			if (_modalSuppressed)
			{
				StartExitAnimation();
				return;
			}

			if (!string.IsNullOrEmpty(_locationName))
			{
				StartEntryAnimation();
			}
		}

		private bool IsAnyWayStationModalOpen()
		{
			return IsModalOpen(WayStationSceneConstants.ModalRootName)
				|| IsModalOpen(WayStationSceneConstants.SaintsMedalsModalRootName);
		}

		private bool IsModalOpen(string entityName)
		{
			var animation = EntityManager.GetEntity(entityName)?.GetComponent<ModalAnimation>();
			return animation != null && (animation.RequestedVisible || animation.Phase != ModalAnimationPhase.Hidden);
		}

		private void StartEntryAnimation()
		{
			float offScreenX = Game1.VirtualWidth + TrapezoidWidth;
			_startTrapezoidX = offScreenX;
			_startTextX = offScreenX;
			_phase = EntryDelaySeconds > 0f ? AnimationPhase.EntryWaiting : AnimationPhase.Entering;
			_animationTime = 0f;
		}

		private void StartExitAnimation()
		{
			if (_phase == AnimationPhase.Idle || string.IsNullOrEmpty(_locationName))
			{
				_phase = AnimationPhase.Idle;
				_animationTime = 0f;
				return;
			}

			_startTrapezoidX = _trapezoidX;
			_startTextX = _textX;
			_phase = AnimationPhase.Exiting;
			_animationTime = 0f;
		}

		private float GetTextOffScreenLeftX()
		{
			if (_font == null || string.IsNullOrEmpty(_locationName)) return -TrapezoidWidth;
			return -_font.MeasureString(_locationName).X * TextScale;
		}

		private float EaseOutCubic(float t)
		{
			float f = t - 1f;
			return f * f * f + 1f;
		}

		[DebugAction("Retrigger Animation")]
		public void Debug_RetriggerAnimation()
		{
			if (!string.IsNullOrEmpty(_locationName) && !_modalSuppressed)
			{
				StartEntryAnimation();
			}
		}

		public void Draw()
		{
			if (string.IsNullOrEmpty(_locationName)) return;

			var trapezoidTexture = PrimitiveTextureFactory.GetAntialiasedTrapezoid(
				_graphicsDevice,
				TrapezoidWidth,
				TrapezoidHeight,
				LeftSideOffset,
				TopEdgeAngleDegrees,
				RightEdgeAngleDegrees,
				BottomEdgeAngleDegrees,
				LeftEdgeAngleDegrees
			);
			if (trapezoidTexture == null) return;

			// Draw trapezoid - scale down from supersampled resolution
			Vector2 trapezoidPos = new Vector2(_trapezoidX, TextPaddingY);
			Rectangle destRect = new Rectangle(
				(int)trapezoidPos.X,
				(int)trapezoidPos.Y,
				(int)TrapezoidWidth,
				(int)TrapezoidHeight
			);
			_spriteBatch.Draw(trapezoidTexture, destRect, Color.White);

			// Draw text - show during all phases except Idle
			if (_font != null && _phase != AnimationPhase.Idle)
			{
				Vector2 textSize = _font.MeasureString(_locationName) * TextScale;
				Vector2 textPos = new Vector2(_textX, TextPaddingY + (TrapezoidHeight - textSize.Y) / 2f);
				_spriteBatch.DrawString(_font, _locationName, textPos, Color.White, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
			}
		}
	}
}
