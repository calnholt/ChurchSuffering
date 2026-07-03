using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Location Map Display")]
	public class LocationMapDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private readonly Texture2D _backgroundTexture;

		private int _lastViewportW = -1;
		private int _lastViewportH = -1;
		private Vector2 _cameraCenter;
		private bool _locked;
		private bool _hasPannedOnLoad = false;
		private bool _isDragging;

		// Map configuration
		private const int BaseMapWidth = 6000;
		private const int BaseMapHeight = 3000;

		[DebugEditable(DisplayName = "Map Scale", Step = 0.1f, Min = 0.1f, Max = 5f)]
		public float MapScale { get; set; } = 0.75f;

		public float MapWidth => BaseMapWidth * MapScale;
		public float MapHeight => BaseMapHeight * MapScale;

		// Camera feel
		[DebugEditable(DisplayName = "Pan Speed (px/s)", Step = 10f, Min = 50f, Max = 5000f)]
		public float BasePanSpeed { get; set; } = 500f;

		[DebugEditable(DisplayName = "Right Stick Deadzone", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float Deadzone { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Speed Exponent", Step = 0.1f, Min = 0.1f, Max = 5f)]
		public float SpeedExponent { get; set; } = 1.2f;

		[DebugEditable(DisplayName = "Max Multiplier", Step = 0.1f, Min = 1f, Max = 10f)]
		public float MaxMultiplier { get; set; } = 3f;

		// Zoom configuration
		[DebugEditable(DisplayName = "Zoom Speed", Step = 0.1f, Min = 0.1f, Max = 5f)]
		public float ZoomSpeed { get; set; } = 0.4f;

		[DebugEditable(DisplayName = "Min Zoom", Step = 0.1f, Min = 0.1f, Max = 1f)]
		public float MinZoom { get; set; } = 0.5f;

		[DebugEditable(DisplayName = "Max Zoom", Step = 0.1f, Min = 1f, Max = 5f)]
		public float MaxZoom { get; set; } = 1.0f;

		public LocationMapDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = imageAssets.GetPixel(Color.White);
			_backgroundTexture = imageAssets.GetRequiredTexture("desert_background_location");
			// Restore zoom state from singleton on system creation
			MapScale = StateSingleton.LocationMapZoom;
			EventManager.Subscribe<LockLocationCameraEvent>(_ => { _locked = _.Locked; });
			EventManager.Subscribe<FocusLocationCameraEvent>(_ => {
				int w = Game1.VirtualWidth;
				int h = Game1.VirtualHeight;
				// Coordinates are provided in unscaled world space; convert to scaled world space
				_cameraCenter = _.WorldPos * MapScale;
				ClampCamera(ref _cameraCenter, w, h);
				PublishCameraState(w, h);
			});
			EventManager.Subscribe<LoadSceneEvent>(_ => {
				if (_.Scene != SceneId.Location)
				{
					_hasPannedOnLoad = false;
					_isDragging = false;
					return;
				}
				// Reset flag when location scene loads
				_hasPannedOnLoad = false;
				_isDragging = false;
			});
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// Update once per frame via SceneState presence
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.Location) return;
			int w = Game1.VirtualWidth;
			int h = Game1.VirtualHeight;
			if (_lastViewportW != w || _lastViewportH != h)
			{
				_lastViewportW = w;
				_lastViewportH = h;
				_cameraCenter = new Vector2(MapWidth / 2f, MapHeight / 2f);
				ClampCamera(ref _cameraCenter, w, h);
			}

			if (!_hasPannedOnLoad)
			{
				if (StateSingleton.HasPendingLocationPoiReveal &&
					!string.IsNullOrEmpty(StateSingleton.PendingPoiId) &&
					SaveCache.TryGetRunNode(StateSingleton.PendingPoiId, out var pendingNode, out _))
				{
					EventManager.Publish(new FocusLocationCameraEvent
					{
						WorldPos = new Vector2(pendingNode.worldX, pendingNode.worldY),
					});
				}
				else if (!StateSingleton.HasPendingLocationPoiReveal)
				{
					TryAutoPanCamera();
				}

				_hasPannedOnLoad = true;
			}

			if (!Game1.WindowIsActive)
			{
				_isDragging = false;
				PublishCameraState(w, h);
				return;
			}

			if (_locked)
			{
				_isDragging = false;
				PublishCameraState(w, h);
				return;
			}

			// Gather combined input (keyboard + gamepad)
			Vector2 velocity = Vector2.Zero;

			PlayerInputFrame input = PlayerInputService.GetFrame(EntityManager);
			int xAxis = (input.IsDown(PlayerButton.MoveRight) ? 1 : 0)
				- (input.IsDown(PlayerButton.MoveLeft) ? 1 : 0);
			int yAxis = (input.IsDown(PlayerButton.MoveDown) ? 1 : 0)
				- (input.IsDown(PlayerButton.MoveUp) ? 1 : 0);
			if (xAxis != 0 || yAxis != 0)
			{
				var kbDir = new Vector2(xAxis, yAxis);
				if (kbDir.LengthSquared() > 1f) kbDir.Normalize();
				velocity += kbDir * BasePanSpeed;
			}

			// Gamepad right stick (keep existing feel)
			if (input.IsGamepadConnected)
			{
				Vector2 stick = input.RightStick;
				float mag = stick.Length();
				if (mag >= Deadzone)
				{
					Vector2 dir = stick / mag;
					float normalized = MathHelper.Clamp((mag - Deadzone) / (1f - Deadzone), 0f, 1f);
					float speedMultiplier = MathHelper.Clamp((float)Math.Pow(normalized, SpeedExponent) * MaxMultiplier, 0f, 10f);
					// In screen space, up is negative Y
					velocity += new Vector2(dir.X, -dir.Y) * BasePanSpeed * speedMultiplier;
				}
			}

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

			// Handle zoom controls (LB zoom out, RB zoom in, mouse wheel)
			float oldScale = MapScale;
			bool zoomChanged = false;
			
			// Gamepad zoom
			if (input.IsGamepadConnected)
			{
				if (input.IsDown(PlayerButton.LeftShoulder))
				{
					MapScale = MathHelper.Clamp(MapScale - ZoomSpeed * dt, MinZoom, MaxZoom);
					zoomChanged = true;
				}
				if (input.IsDown(PlayerButton.RightShoulder))
				{
					MapScale = MathHelper.Clamp(MapScale + ZoomSpeed * dt, MinZoom, MaxZoom);
					zoomChanged = true;
				}
			}
			
			// Mouse wheel zoom
			if (input.ScrollDelta != 0f)
			{
				float zoomDelta = input.ScrollDelta * ZoomSpeed * dt * 10f;
				MapScale = MathHelper.Clamp(MapScale + zoomDelta, MinZoom, MaxZoom);
				zoomChanged = true;
			}

			// Right mouse button drag-to-pan
			bool rmbPressed = input.Device == PlayerInputDevice.KeyboardMouse
				&& input.IsDown(PlayerButton.Secondary);
			bool rmbEdge = input.Device == PlayerInputDevice.KeyboardMouse
				&& input.WasPressed(PlayerButton.Secondary);

			if (rmbEdge)
			{
				// Start drag
				_isDragging = true;
			}
			else if (_isDragging && rmbPressed)
			{
				// Move camera opposite to drag direction (natural map dragging feel)
				_cameraCenter -= input.PointerDelta;
			}
			else if (!rmbPressed && _isDragging)
			{
				// Stop dragging
				_isDragging = false;
			}
			
			// Preserve viewport center world position across zoom changes
			if (zoomChanged && oldScale != MapScale)
			{
				// Convert camera center to map-relative ratio (0-1)
				float ratioX = _cameraCenter.X / (BaseMapWidth * oldScale);
				float ratioY = _cameraCenter.Y / (BaseMapHeight * oldScale);

				// Restore camera center using new map size
				_cameraCenter.X = ratioX * (BaseMapWidth * MapScale);
				_cameraCenter.Y = ratioY * (BaseMapHeight * MapScale);

				// Save zoom state to singleton
				StateSingleton.LocationMapZoom = MapScale;
			}

			if (velocity != Vector2.Zero)
			{
				_cameraCenter += velocity * dt;
			}

			// Always clamp camera and publish state (zoom may have changed even without velocity)
			ClampCamera(ref _cameraCenter, w, h);
			PublishCameraState(w, h);

		}

		private void PublishCameraState(int viewportW, int viewportH)
		{
			var camEntity = EntityManager.GetEntity("LocationCamera");
			if (camEntity == null)
			{
				camEntity = EntityManager.CreateEntity("LocationCamera");
			}
			var state = camEntity.GetComponent<LocationCameraState>();
			if (state == null)
			{
				state = new LocationCameraState();
				EntityManager.AddComponent(camEntity, state);
			}
			float w = viewportW;
			float h = viewportH;
			var origin = new Vector2(_cameraCenter.X - w / 2f, _cameraCenter.Y - h / 2f);
			origin.X = Math.Max(0f, origin.X);
			origin.Y = Math.Max(0f, origin.Y);
			state.Center = _cameraCenter;
			state.Origin = origin;
			state.ViewportW = viewportW;
			state.ViewportH = viewportH;
			state.MapScale = MapScale;
		}

		private void TryAutoPanCamera()
		{
			var nodes = SaveCache.GetRunMapNodes();
			if (nodes == null || nodes.Count == 0) return;

			RunMapNode target = null;
			string lastLoc = SaveCache.GetAll().lastLocation;
			if (!string.IsNullOrEmpty(lastLoc))
			{
				SaveCache.TryGetRunNode(lastLoc, out target, out _);
			}

			if (target == null)
			{
				for (int i = nodes.Count - 1; i >= 0; i--)
				{
					if (nodes[i] != null && nodes[i].isCompleted)
					{
						target = nodes[i];
						break;
					}
				}
			}

			if (target == null)
			{
				target = nodes.FirstOrDefault(n => n != null && n.isRevealed);
			}

			if (target != null)
			{
				EventManager.Publish(new FocusLocationCameraEvent { WorldPos = new Vector2(target.worldX, target.worldY) });
			}
		}

		private void ClampCamera(ref Vector2 center, int viewportW, int viewportH)
		{
			float halfW = viewportW / 2f;
			float halfH = viewportH / 2f;
			float minX = halfW;
			float minY = halfH;
			float maxX = Math.Max(halfW, MapWidth - halfW);
			float maxY = Math.Max(halfH, MapHeight - halfH);
			center.X = MathHelper.Clamp(center.X, minX, maxX);
			center.Y = MathHelper.Clamp(center.Y, minY, maxY);
		}

		public void Draw()
		{
			int w = Game1.VirtualWidth;
			int h = Game1.VirtualHeight;
			if (_lastViewportW < 0 || _lastViewportH < 0)
			{
				_lastViewportW = w;
				_lastViewportH = h;
				_cameraCenter = new Vector2(MapWidth / 2f, MapHeight / 2f);
				ClampCamera(ref _cameraCenter, w, h);
			}

			Vector2 origin = new Vector2(_cameraCenter.X - w / 2f, _cameraCenter.Y - h / 2f);
			origin.X = Math.Max(0f, origin.X);
			origin.Y = Math.Max(0f, origin.Y);

			float scaledMapWidth = MapWidth;
			float scaledMapHeight = MapHeight;

			// Calculate the visible portion of the map
			float visibleLeft = Math.Max(0f, origin.X);
			float visibleTop = Math.Max(0f, origin.Y);
			float visibleRight = Math.Min(scaledMapWidth, origin.X + w);
			float visibleBottom = Math.Min(scaledMapHeight, origin.Y + h);
			float visibleWidth = visibleRight - visibleLeft;
			float visibleHeight = visibleBottom - visibleTop;

			if (visibleWidth > 0 && visibleHeight > 0)
			{
				// Calculate source rectangle (proportional to texture size)
				float sourceLeft = visibleLeft / scaledMapWidth;
				float sourceTop = visibleTop / scaledMapHeight;
				float sourceWidth = visibleWidth / scaledMapWidth;
				float sourceHeight = visibleHeight / scaledMapHeight;

				var sourceRect = new Rectangle(
					(int)(sourceLeft * _backgroundTexture.Width),
					(int)(sourceTop * _backgroundTexture.Height),
					(int)(sourceWidth * _backgroundTexture.Width),
					(int)(sourceHeight * _backgroundTexture.Height)
				);

				// Draw the visible portion of the background texture
				var destRect = new Rectangle(
					(int)(visibleLeft - origin.X),
					(int)(visibleTop - origin.Y),
					(int)visibleWidth,
					(int)visibleHeight
				);

				_spriteBatch.Draw(_backgroundTexture, destRect, sourceRect, Color.White);
			}
		}
	}
}
