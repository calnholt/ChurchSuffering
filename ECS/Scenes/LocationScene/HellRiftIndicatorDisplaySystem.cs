using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Hellrift Indicator Display")]
	public class HellRiftIndicatorDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private readonly Texture2D _hellriftIconTexture;
		private readonly System.Collections.Generic.List<Entity> _incompleteHellrifts = new System.Collections.Generic.List<Entity>();

		[DebugEditable(DisplayName = "Icon Size", Step = 1f, Min = 10f, Max = 200f)]
		public float IconSize { get; set; } = 50f; // 50% of normal ~140px

		[DebugEditable(DisplayName = "Icon Scale", Step = 0.05f, Min = 0.1f, Max = 1f)]
		public float IconScale { get; set; } = 0.4f; // 50%

		[DebugEditable(DisplayName = "Edge Margin", Step = 1f, Min = 0f, Max = 100f)]
		public float EdgeMargin { get; set; } = 73f;

		[DebugEditable(DisplayName = "Chevron Size", Step = 1f, Min = 5f, Max = 50f)]
		public float ChevronSize { get; set; } = 20f;

		[DebugEditable(DisplayName = "Chevron Thickness", Step = 1f, Min = 1f, Max = 10f)]
		public float ChevronThickness { get; set; } = 5f;

		[DebugEditable(DisplayName = "Chevron Padding", Step = 1f, Min = 0f, Max = 50f)]
		public float ChevronPadding { get; set; } = 24f;

		[DebugEditable(DisplayName = "Distance Scale Mult", Step = 0.05f, Min = 0f, Max = 5f)]
		public float DistanceScaleMult { get; set; } = 5f;

		public HellRiftIndicatorDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = imageAssets.GetPixel(Color.White);
			_hellriftIconTexture = imageAssets.TryGetTexture("Hellrift_poi");
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.Location)
			{
				_incompleteHellrifts.Clear();
				return;
			}

			// Query all POIs and filter for incomplete hellrifts
			_incompleteHellrifts.Clear();
			var allPois = EntityManager.GetEntitiesWithComponent<PointOfInterest>();
			foreach (var poiEntity in allPois)
			{
				var poi = poiEntity.GetComponent<PointOfInterest>();
				if (poi != null
					&& poi.Type == PointOfInterestType.Hellrift
					&& poi.IsRevealed
					&& !poi.IsCompleted)
				{
					_incompleteHellrifts.Add(poiEntity);
				}
			}
		}

		public void Draw()
		{
			if (_incompleteHellrifts.Count == 0) return;

			var cam = EntityManager.GetEntity("LocationCamera")?.GetComponent<LocationCameraState>();
			if (cam == null) return;

			var origin = cam.Origin;
			var cameraCenter = cam.Center;
			float mapScale = cam.MapScale;
			int w = cam.ViewportW;
			int h = cam.ViewportH;

			foreach (var hellriftEntity in _incompleteHellrifts)
			{
				var poi = hellriftEntity.GetComponent<PointOfInterest>();
				if (poi == null) continue;

				// Scale world position by map scale to match scaled world space
				var scaledWorldPos = poi.WorldPosition * mapScale;
				var screenPos = scaledWorldPos - origin;

				// Check if in view - skip if visible
				if (screenPos.X >= 0 && screenPos.X <= w && screenPos.Y >= 0 && screenPos.Y <= h)
				{
					continue;
				}

				// Calculate direction from camera center to hellrift (in scaled world space)
				var direction = scaledWorldPos - cameraCenter;
				if (direction.LengthSquared() < 0.0001f) continue; // Avoid division by zero

				// Convert hellrift position to screen space for edge calculation
				var hellriftScreenPos = scaledWorldPos - origin;
				var screenCenter = new Vector2(w / 2f, h / 2f);
				var screenDirection = hellriftScreenPos - screenCenter;
				if (screenDirection.LengthSquared() < 0.0001f) continue;
				screenDirection.Normalize();

				// Determine which edge to place indicator on and calculate position
				var edgePos = CalculateEdgePosition(screenDirection, screenCenter, w, h);

				// Compute distance-based scale for the icon (farther = smaller)
				float dist = direction.Length();
				float maxRelevant = System.MathF.Max((float)w, (float)h) * 2f; // ~two viewports away
				float t = System.Math.Clamp(dist / maxRelevant, 0f, 1f);
				float distanceScale = (1f - 0.5f) * (1f - t) + 0.5f; // lerp 1.0 -> 0.5
				distanceScale *= DistanceScaleMult;

				float actualIconRadius = (IconSize * IconScale * distanceScale) / 2f;

				// Draw hellrift icon at edge position with distance-based scale
				DrawIcon(edgePos, distanceScale);

				// Draw chevron arrow pointing toward hellrift (keep size constant, offset by scaled icon radius)
				DrawChevron(edgePos, direction, w, h, actualIconRadius);
			}
		}

		private Vector2 CalculateEdgePosition(Vector2 screenDirection, Vector2 screenCenter, int viewportW, int viewportH)
		{
			// Find intersection with screen edges by checking which edge the ray hits first
			// Calculate intersections with all four edges
			float leftEdgeX = EdgeMargin;
			float rightEdgeX = viewportW - EdgeMargin;
			float topEdgeY = EdgeMargin;
			float bottomEdgeY = viewportH - EdgeMargin;

			// Calculate t values for each edge intersection
			float tLeft = float.MaxValue;
			float tRight = float.MaxValue;
			float tTop = float.MaxValue;
			float tBottom = float.MaxValue;

			if (System.Math.Abs(screenDirection.X) > 0.0001f)
			{
				tLeft = (leftEdgeX - screenCenter.X) / screenDirection.X;
				tRight = (rightEdgeX - screenCenter.X) / screenDirection.X;
			}

			if (System.Math.Abs(screenDirection.Y) > 0.0001f)
			{
				tTop = (topEdgeY - screenCenter.Y) / screenDirection.Y;
				tBottom = (bottomEdgeY - screenCenter.Y) / screenDirection.Y;
			}

			// Find which edge is hit first (smallest positive t)
			float minT = float.MaxValue;
			float edgeX = screenCenter.X;
			float edgeY = screenCenter.Y;

			// Check left edge
			if (tLeft > 0 && tLeft < minT)
			{
				minT = tLeft;
				edgeX = leftEdgeX;
				edgeY = screenCenter.Y + screenDirection.Y * tLeft;
			}

			// Check right edge
			if (tRight > 0 && tRight < minT)
			{
				minT = tRight;
				edgeX = rightEdgeX;
				edgeY = screenCenter.Y + screenDirection.Y * tRight;
			}

			// Check top edge
			if (tTop > 0 && tTop < minT)
			{
				minT = tTop;
				edgeX = screenCenter.X + screenDirection.X * tTop;
				edgeY = topEdgeY;
			}

			// Check bottom edge
			if (tBottom > 0 && tBottom < minT)
			{
				minT = tBottom;
				edgeX = screenCenter.X + screenDirection.X * tBottom;
				edgeY = bottomEdgeY;
			}

			// Clamp to screen bounds with margin (shouldn't be needed, but safety check)
			edgeX = System.Math.Clamp(edgeX, EdgeMargin, viewportW - EdgeMargin);
			edgeY = System.Math.Clamp(edgeY, EdgeMargin, viewportH - EdgeMargin);

			return new Vector2(edgeX, edgeY);
		}

		private void DrawIcon(Vector2 position, float distanceScale)
		{
			if (_hellriftIconTexture == null) return;

			// Apply IconScale to IconSize
			float iconWidth = IconSize * IconScale * distanceScale;
			float iconHeight = iconWidth;
			if (_hellriftIconTexture.Width > 0 && _hellriftIconTexture.Height > 0)
			{
				float aspectRatio = _hellriftIconTexture.Height / (float)_hellriftIconTexture.Width;
				iconHeight = iconWidth * aspectRatio;
			}

			var iconRect = new Rectangle(
				(int)System.Math.Round(position.X - iconWidth / 2f),
				(int)System.Math.Round(position.Y - iconHeight / 2f),
				(int)System.Math.Round(iconWidth),
				(int)System.Math.Round(iconHeight)
			);

			_spriteBatch.Draw(_hellriftIconTexture, iconRect, Color.White);
		}

		private void DrawChevron(Vector2 iconPosition, Vector2 directionToHellrift, int viewportW, int viewportH, float iconRadius)
		{
			// Normalize direction
			var dirNormalized = directionToHellrift;
			dirNormalized.Normalize();

			// Calculate chevron angle (pointing toward hellrift)
			float angle = System.MathF.Atan2(dirNormalized.Y, dirNormalized.X);

			// Calculate chevron tip position - further out from icon in direction of hellrift
			// Position tip further out in the direction of the hellrift, with padding
			var chevronOffset = dirNormalized * (iconRadius + ChevronPadding);
			var chevronTip = iconPosition + chevronOffset;

			// Draw chevron as two diagonal lines forming a V
			// Tip points toward POI, lines fan backward from tip
			float chevronHalfAngle = System.MathF.PI / 6f; // 30 degrees half-angle

			// Left line of chevron - starts at tip, extends backward (opposite direction)
			float leftAngle = angle - chevronHalfAngle + System.MathF.PI; // +PI to reverse direction
			DrawChevronLine(chevronTip, leftAngle, ChevronSize, ChevronThickness);

			// Right line of chevron - starts at tip, extends backward (opposite direction)
			float rightAngle = angle + chevronHalfAngle + System.MathF.PI; // +PI to reverse direction
			DrawChevronLine(chevronTip, rightAngle, ChevronSize, ChevronThickness);
		}

		private void DrawChevronLine(Vector2 tip, float angle, float length, float thickness)
		{
			// Calculate line from tip extending in the given angle direction
			var direction = new Vector2(System.MathF.Cos(angle), System.MathF.Sin(angle));
			var start = tip;
			var end = tip + direction * length;

			// Draw line using pixel texture
			float dx = end.X - start.X;
			float dy = end.Y - start.Y;
			float lineLength = System.MathF.Max(1f, System.MathF.Sqrt(dx * dx + dy * dy));
			float lineAngle = System.MathF.Atan2(dy, dx);

			_spriteBatch.Draw(
				_pixel,
				start,
				null,
				Color.White,
				lineAngle,
				Vector2.Zero,
				new Vector2(lineLength, thickness),
				SpriteEffects.None,
				0f
			);
		}
	}
}
