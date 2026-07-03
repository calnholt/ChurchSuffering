using System;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Displays an animated indicator icon with trapezoid backdrop above the active character
	/// (player or enemy) based on whose turn it is.
	/// </summary>
	[DebugTab("Active Character Indicator")]
	public class ActiveCharacterIndicatorDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private Texture2D _iconTexture;
		private Texture2D _trapezoidTexture;

		// Animation state
		private enum AnimPhase { Rise, Drop, Pause }
		private AnimPhase _animPhase = AnimPhase.Pause;
		private float _animTimer;
		private float _animOffset; // current Y offset from base position

		// --- Debug Editable Fields ---

		[DebugEditable(DisplayName = "Vertical Offset from Head", Step = 1f, Min = -200f, Max = 200f)]
		public float VerticalOffset { get; set; } = -105f;

		[DebugEditable(DisplayName = "Icon Scale", Step = 0.05f, Min = 0.1f, Max = 3f)]
		public float IconScale { get; set; } = 0.1f;

		// Trapezoid settings
		[DebugEditable(DisplayName = "Trapezoid Width", Step = 5f, Min = 10f, Max = 300f)]
		public float TrapezoidWidth { get; set; } = 40f;

		[DebugEditable(DisplayName = "Trapezoid Height", Step = 5f, Min = 10f, Max = 200f)]
		public float TrapezoidHeight { get; set; } = 15f;

		[DebugEditable(DisplayName = "Trapezoid Left Offset", Step = 1f, Min = -50f, Max = 50f)]
		public float TrapezoidLeftOffset { get; set; } = 10f;

		[DebugEditable(DisplayName = "Trapezoid Top Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float TrapezoidTopAngle { get; set; } = 11f;

		[DebugEditable(DisplayName = "Trapezoid Right Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float TrapezoidRightAngle { get; set; } = -25f;

		[DebugEditable(DisplayName = "Trapezoid Bottom Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float TrapezoidBottomAngle { get; set; } = 10f;

		[DebugEditable(DisplayName = "Trapezoid Left Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
		public float TrapezoidLeftAngle { get; set; } = -31f;

		[DebugEditable(DisplayName = "Trapezoid Color R", Step = 5, Min = 0, Max = 255)]
		public int TrapezoidColorR { get; set; } = 0;

		[DebugEditable(DisplayName = "Trapezoid Color G", Step = 5, Min = 0, Max = 255)]
		public int TrapezoidColorG { get; set; } = 0;

		[DebugEditable(DisplayName = "Trapezoid Color B", Step = 5, Min = 0, Max = 255)]
		public int TrapezoidColorB { get; set; } = 0;

		[DebugEditable(DisplayName = "Trapezoid Alpha", Step = 5, Min = 0, Max = 255)]
		public int TrapezoidAlpha { get; set; } = 220;

		// Animation timing
		[DebugEditable(DisplayName = "Rise Duration (s)", Step = 0.05f, Min = 0.05f, Max = 2f)]
		public float RiseDuration { get; set; } = 0.8f;

		[DebugEditable(DisplayName = "Drop Duration (s)", Step = 0.05f, Min = 0.05f, Max = 1f)]
		public float DropDuration { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Pause Duration (s)", Step = 0.05f, Min = 0f, Max = 2f)]
		public float PauseDuration { get; set; } = 0.5f;

		[DebugEditable(DisplayName = "Animation Distance (px)", Step = 1f, Min = 1f, Max = 50f)]
		public float AnimationDistance { get; set; } = 10f;

		// Cached trapezoid params to detect changes
		private float _lastTrapWidth, _lastTrapHeight, _lastTrapLeftOffset;
		private float _lastTrapTopAngle, _lastTrapRightAngle, _lastTrapBottomAngle, _lastTrapLeftAngle;

		public ActiveCharacterIndicatorDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
      _iconTexture = imageAssets.GetRequiredTexture("active_icon");
			RegenerateTrapezoid();
		}

		private void RegenerateTrapezoid()
		{
			_trapezoidTexture = PrimitiveTextureFactory.GetAntialiasedTrapezoidMask(
				_graphicsDevice,
				TrapezoidWidth,
				TrapezoidHeight,
				TrapezoidLeftOffset,
				TrapezoidTopAngle,
				TrapezoidRightAngle,
				TrapezoidBottomAngle,
				TrapezoidLeftAngle
			);
			_lastTrapWidth = TrapezoidWidth;
			_lastTrapHeight = TrapezoidHeight;
			_lastTrapLeftOffset = TrapezoidLeftOffset;
			_lastTrapTopAngle = TrapezoidTopAngle;
			_lastTrapRightAngle = TrapezoidRightAngle;
			_lastTrapBottomAngle = TrapezoidBottomAngle;
			_lastTrapLeftAngle = TrapezoidLeftAngle;
		}

		private bool TrapezoidParamsChanged()
		{
			return Math.Abs(_lastTrapWidth - TrapezoidWidth) > 0.01f ||
				   Math.Abs(_lastTrapHeight - TrapezoidHeight) > 0.01f ||
				   Math.Abs(_lastTrapLeftOffset - TrapezoidLeftOffset) > 0.01f ||
				   Math.Abs(_lastTrapTopAngle - TrapezoidTopAngle) > 0.01f ||
				   Math.Abs(_lastTrapRightAngle - TrapezoidRightAngle) > 0.01f ||
				   Math.Abs(_lastTrapBottomAngle - TrapezoidBottomAngle) > 0.01f ||
				   Math.Abs(_lastTrapLeftAngle - TrapezoidLeftAngle) > 0.01f;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<PhaseState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

			// Regenerate trapezoid if debug params changed
			if (TrapezoidParamsChanged())
			{
				RegenerateTrapezoid();
			}

			// Update animation
			_animTimer += dt;
			switch (_animPhase)
			{
				case AnimPhase.Rise:
					if (_animTimer >= RiseDuration)
					{
						_animTimer = 0f;
						_animPhase = AnimPhase.Drop;
						_animOffset = -AnimationDistance; // at peak
					}
					else
					{
						// Ease out for slow rise
						float t = _animTimer / RiseDuration;
						float eased = 1f - (1f - t) * (1f - t); // ease out quad
						_animOffset = -AnimationDistance * eased;
					}
					break;

				case AnimPhase.Drop:
					if (_animTimer >= DropDuration)
					{
						_animTimer = 0f;
						_animPhase = AnimPhase.Pause;
						_animOffset = 0f;
					}
					else
					{
						// Quick drop - ease in
						float t = _animTimer / DropDuration;
						float eased = t * t; // ease in quad
						_animOffset = -AnimationDistance * (1f - eased);
					}
					break;

				case AnimPhase.Pause:
					_animOffset = 0f;
					if (_animTimer >= PauseDuration)
					{
						_animTimer = 0f;
						_animPhase = AnimPhase.Rise;
					}
					break;
			}
		}

		public void Draw()
		{
			if (_iconTexture == null) return;

			// Determine current phase
			var phaseEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			var phaseState = phaseEntity?.GetComponent<PhaseState>();
			if (phaseState == null) return;

			// Don't show during StartBattle
			if (phaseState.Main == MainPhase.StartBattle) return;

			// Find target entity based on phase
			Entity targetEntity = null;
			if (phaseState.Main == MainPhase.PlayerTurn)
			{
				targetEntity = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			}
			else if (phaseState.Main == MainPhase.EnemyTurn)
			{
				if (BattleInputGate.ShouldSuppressEnemyAttackDisplay(EntityManager)) return;
				targetEntity = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
			}

			if (targetEntity == null) return;

			var transform = targetEntity.GetComponent<Transform>();
			var portraitInfo = targetEntity.GetComponent<PortraitInfo>();
			if (transform == null) return;

			// Calculate position above the character's head
			Vector2 basePos = transform.Position;
			float headOffset = 0f;
			if (portraitInfo != null)
			{
				// Offset by half the scaled texture height to get to top of portrait
				headOffset = -(portraitInfo.TextureHeight * portraitInfo.CurrentScale) / 2f;
			}

			Vector2 indicatorPos = new Vector2(
				basePos.X,
				basePos.Y + headOffset + VerticalOffset + _animOffset
			);

			// Draw trapezoid backdrop (centered)
			if (_trapezoidTexture != null)
			{
				var trapOrigin = new Vector2(_trapezoidTexture.Width / 2f, _trapezoidTexture.Height / 2f);
				var trapColor = new Color(TrapezoidColorR, TrapezoidColorG, TrapezoidColorB, TrapezoidAlpha);
				_spriteBatch.Draw(
					_trapezoidTexture,
					indicatorPos,
					sourceRectangle: null,
					color: trapColor,
					rotation: 0f,
					origin: trapOrigin,
					scale: 1f,
					effects: SpriteEffects.None,
					layerDepth: 0f
				);
			}

			// Draw icon (centered on same position)
			var iconOrigin = new Vector2(_iconTexture.Width / 2f, _iconTexture.Height / 2f);
			_spriteBatch.Draw(
				_iconTexture,
				indicatorPos,
				sourceRectangle: null,
				color: Color.White,
				rotation: 0f,
				origin: iconOrigin,
				scale: IconScale,
				effects: SpriteEffects.None,
				layerDepth: 0f
			);
		}
	}
}
