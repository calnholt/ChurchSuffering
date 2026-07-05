using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation Camera")]
	public class WayStationCameraSystem : Core.System
	{
		private readonly Texture2D _background;
		private Vector2 _centerWorldPosition;
		private float _zoom = 1f;
		private bool _hasInitializedCenter;

		[DebugEditable(DisplayName = "Pan Speed", Step = 10f, Min = 50f, Max = 4000f)]
		public float PanSpeed { get; set; } = 500f;
		[DebugEditable(DisplayName = "Right Stick Deadzone", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float RightStickDeadzone { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Zoom Speed", Step = 0.01f, Min = 0.05f, Max = 4f)]
		public float ZoomSpeed { get; set; } = 0.3f;
		[DebugEditable(DisplayName = "Min Zoom", Step = 0.05f, Min = 0.5f, Max = 4f)]
		public float MinZoom { get; set; } = 1f;
		[DebugEditable(DisplayName = "Max Zoom", Step = 0.05f, Min = 0.5f, Max = 6f)]
		public float MaxZoom { get; set; } = 2.4f;

		public WayStationCameraSystem(
			EntityManager entityManager,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_background = imageAssets.GetRequiredTexture("waystation");
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		}

		private void OnLoadScene(LoadSceneEvent e)
		{
			if (e.Scene != SceneId.WayStation) return;
			InitializeCenter();
			_zoom = MathHelper.Clamp(_zoom, MinZoomValue, MaxZoomValue);
			ClampCenterToSource();
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (!IsWayStationScene(scene)) return;

			InitializeCenter();
			if (scene.Current == SceneId.WayStation)
			{
				UpdateCameraInput(gameTime);
			}

			SyncMapView();
		}

		private void UpdateCameraInput(GameTime gameTime)
		{
			var input = EntityManager.GetEntitiesWithComponent<PlayerInputState>()
				.FirstOrDefault()
				?.GetComponent<PlayerInputState>()
				?.Frame;
			if (input == null) return;
			if (IsClimbModalOpen()) return;

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			UpdateZoom(input.Value, dt);
			UpdatePan(input.Value, dt);
			ClampCenterToSource();
		}

		private void UpdateZoom(PlayerInputFrame input, float dt)
		{
			if (input.IsDown(PlayerButton.LeftShoulder))
			{
				_zoom = MathHelper.Clamp(_zoom - ZoomSpeed * dt, MinZoomValue, MaxZoomValue);
			}

			if (input.IsDown(PlayerButton.RightShoulder))
			{
				_zoom = MathHelper.Clamp(_zoom + ZoomSpeed * dt, MinZoomValue, MaxZoomValue);
			}

			if (System.Math.Abs(input.ScrollDelta) > 0.001f)
			{
				_zoom = MathHelper.Clamp(_zoom + input.ScrollDelta * ZoomSpeed * dt * 10f, MinZoomValue, MaxZoomValue);
			}
		}

		private void UpdatePan(PlayerInputFrame input, float dt)
		{
			Vector2 direction = Vector2.Zero;
			if (input.IsDown(PlayerButton.MoveLeft)) direction.X -= 1f;
			if (input.IsDown(PlayerButton.MoveRight)) direction.X += 1f;
			if (input.IsDown(PlayerButton.MoveUp)) direction.Y -= 1f;
			if (input.IsDown(PlayerButton.MoveDown)) direction.Y += 1f;

			Vector2 rightStick = input.RightStick;
			float rightStickMagnitude = rightStick.Length();
			if (rightStickMagnitude > RightStickDeadzone)
			{
				float normalized = MathHelper.Clamp(
					(rightStickMagnitude - RightStickDeadzone) / System.Math.Max(0.001f, 1f - RightStickDeadzone),
					0f,
					1f);
				Vector2 stickDirection = rightStick / rightStickMagnitude;
				direction += new Vector2(stickDirection.X, -stickDirection.Y) * normalized;
			}

			if (direction.LengthSquared() <= 0.0001f) return;

			if (direction.LengthSquared() > 1f)
			{
				direction.Normalize();
			}

			_centerWorldPosition += direction * PanSpeed * dt / System.Math.Max(0.001f, _zoom);
		}

		private void SyncMapView()
		{
			ClampCenterToSource();

			var entity = EntityManager.GetEntity(WayStationSceneConstants.MapViewName);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(WayStationSceneConstants.MapViewName);
				EntityManager.AddComponent(entity, new WayStationMapView());
			}

			var view = entity.GetComponent<WayStationMapView>();
			view.Source = ComputeMapSource(Game1.VirtualWidth, Game1.VirtualHeight);
			view.TargetWidth = Game1.VirtualWidth;
			view.TargetHeight = Game1.VirtualHeight;
			view.Zoom = MathHelper.Clamp(_zoom, MinZoomValue, MaxZoomValue);
			view.CenterWorldPosition = _centerWorldPosition;
			view.TextureWidth = _background.Width;
			view.TextureHeight = _background.Height;
		}

		private Rectangle ComputeMapSource(int targetWidth, int targetHeight)
		{
			var sourceSize = ComputeSourceSize(targetWidth, targetHeight);
			int sourceWidth = sourceSize.X;
			int sourceHeight = sourceSize.Y;
			int x = (int)System.Math.Round(_centerWorldPosition.X - sourceWidth / 2f);
			int y = (int)System.Math.Round(_centerWorldPosition.Y - sourceHeight / 2f);
			x = System.Math.Clamp(x, 0, System.Math.Max(0, _background.Width - sourceWidth));
			y = System.Math.Clamp(y, 0, System.Math.Max(0, _background.Height - sourceHeight));
			return new Rectangle(x, y, sourceWidth, sourceHeight);
		}

		private Point ComputeSourceSize(int targetWidth, int targetHeight)
		{
			float targetAspect = System.Math.Max(1, targetWidth) / (float)System.Math.Max(1, targetHeight);
			float textureAspect = _background.Width / (float)_background.Height;
			int coverWidth = _background.Width;
			int coverHeight = _background.Height;
			if (textureAspect > targetAspect)
			{
				coverWidth = (int)System.Math.Round(_background.Height * targetAspect);
			}
			else
			{
				coverHeight = (int)System.Math.Round(_background.Width / targetAspect);
			}

			float zoom = MathHelper.Clamp(_zoom, MinZoomValue, MaxZoomValue);
			int sourceWidth = System.Math.Clamp(
				(int)System.Math.Round(coverWidth / zoom),
				1,
				_background.Width);
			int sourceHeight = System.Math.Clamp(
				(int)System.Math.Round(coverHeight / zoom),
				1,
				_background.Height);
			return new Point(sourceWidth, sourceHeight);
		}

		private void ClampCenterToSource()
		{
			var sourceSize = ComputeSourceSize(Game1.VirtualWidth, Game1.VirtualHeight);
			float halfWidth = sourceSize.X / 2f;
			float halfHeight = sourceSize.Y / 2f;
			_centerWorldPosition.X = ClampCenterAxis(_centerWorldPosition.X, halfWidth, _background.Width);
			_centerWorldPosition.Y = ClampCenterAxis(_centerWorldPosition.Y, halfHeight, _background.Height);
		}

		private static float ClampCenterAxis(float value, float halfSize, int textureSize)
		{
			if (textureSize <= halfSize * 2f) return textureSize / 2f;
			return MathHelper.Clamp(value, halfSize, textureSize - halfSize);
		}

		private void InitializeCenter()
		{
			if (_hasInitializedCenter) return;

			_centerWorldPosition = new Vector2(_background.Width / 2f, _background.Height / 2f);
			_hasInitializedCenter = true;
		}

		private bool IsClimbModalOpen()
		{
			var animation = EntityManager.GetEntity(WayStationSceneConstants.ModalRootName)
				?.GetComponent<ModalAnimation>();
			return animation != null && (animation.RequestedVisible || animation.Phase != ModalAnimationPhase.Hidden);
		}

		private static bool IsWayStationScene(SceneState scene)
		{
			return scene != null && (scene.Current == SceneId.WayStation || scene.Current == SceneId.Snapshot);
		}

		private float MinZoomValue => System.Math.Min(MinZoom, MaxZoom);
		private float MaxZoomValue => System.Math.Max(MinZoom, MaxZoom);
	}
}
