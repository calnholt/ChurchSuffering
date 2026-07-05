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

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Location Name Display")]
	public class LocationNameDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.TitleFont;
		private string _locationName = "";
		
		private enum AnimationPhase { Idle, EntryWaiting, TrapezoidSliding, TextSliding, Complete }
		private AnimationPhase _phase = AnimationPhase.Idle;
		private float _animationTime = 0f;
		private float _trapezoidX = 0f;
		private float _textX = 0f;
		private float _targetTrapezoidX = 0f;
		private float _targetTextX = 0f;
		private int _viewportWidth = 0;

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
			if (scene?.Current == SceneId.WayStation && string.IsNullOrEmpty(_locationName))
			{
				SetTitle("Waystation");
			}

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			_viewportWidth = Game1.VirtualWidth;
			_targetTrapezoidX = 0f;
			_targetTextX = TextPaddingX;

			float offScreenX = _viewportWidth + TrapezoidWidth;

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
						_phase = AnimationPhase.TrapezoidSliding;
						_animationTime = 0f;
					}
					_trapezoidX = offScreenX;
					_textX = offScreenX;
					break;

				case AnimationPhase.TrapezoidSliding:
					// Update trapezoid position
					if (_animationTime >= AnimationDurationSeconds)
					{
						_trapezoidX = _targetTrapezoidX;
					}
					else
					{
						float progress = _animationTime / AnimationDurationSeconds;
						float eased = EaseOutCubic(progress);
						_trapezoidX = MathHelper.Lerp(offScreenX, _targetTrapezoidX, eased);
					}

					// Update text position - starts after TextDelaySeconds from when trapezoid starts
					float textTime = _animationTime - TextDelaySeconds;
					if (textTime <= 0f)
					{
						_textX = offScreenX;
					}
					else if (textTime >= AnimationDurationSeconds)
					{
						_textX = _targetTextX;
					}
					else
					{
						float textProgress = textTime / AnimationDurationSeconds;
						float textEased = EaseOutCubic(textProgress);
						_textX = MathHelper.Lerp(offScreenX, _targetTextX, textEased);
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

				case AnimationPhase.TextSliding:
					if (_animationTime >= AnimationDurationSeconds)
					{
						_textX = _targetTextX;
						_phase = AnimationPhase.Complete;
					}
					else
					{
						float progress = _animationTime / AnimationDurationSeconds;
						float eased = EaseOutCubic(progress);
						_textX = MathHelper.Lerp(offScreenX, _targetTextX, eased);
					}
					break;

				case AnimationPhase.Complete:
					_trapezoidX = _targetTrapezoidX;
					_textX = _targetTextX;
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

			_phase = EntryDelaySeconds > 0f ? AnimationPhase.EntryWaiting : AnimationPhase.TrapezoidSliding;
			_animationTime = 0f;
		}

		private void ClearTitle()
		{
			_locationName = "";
			_phase = AnimationPhase.Idle;
			_animationTime = 0f;
		}

		private float EaseOutCubic(float t)
		{
			float f = t - 1f;
			return f * f * f + 1f;
		}

		[DebugAction("Retrigger Animation")]
		public void Debug_RetriggerAnimation()
		{
			if (!string.IsNullOrEmpty(_locationName))
			{
				_phase = EntryDelaySeconds > 0f ? AnimationPhase.EntryWaiting : AnimationPhase.TrapezoidSliding;
				_animationTime = 0f;
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


