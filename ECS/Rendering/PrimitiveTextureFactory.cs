using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering
{
	/// <summary>
	/// Factory for primitive textures (e.g., anti-aliased circles) with caching.
	/// </summary>
	public static class PrimitiveTextureFactory
	{
		// Cache AA circles by GraphicsDevice pointer and radius
		private static readonly Dictionary<(int deviceId, int radius), Texture2D> _aaCircleCache = new();
		// Cache AA pie slices by device, radius, and angle range
		private static readonly Dictionary<(int deviceId, int radius, float startDeg, float endDeg), Texture2D> _pieSliceCache = new();
		// Cache rounded squares by device, size, and corner radius
		private static readonly Dictionary<(int deviceId, int size, int radius), Texture2D> _roundedSquareCache = new();
		// Cache equilateral triangles by device and size
		private static readonly Dictionary<(int deviceId, int size), Texture2D> _triangleCache = new();
		// Cache trapezoids by device and all parameters (black-filled by default)
		private static readonly Dictionary<(int deviceId, float width, float height, float leftOffset, float topAngle, float rightAngle, float bottomAngle, float leftAngle), Texture2D> _trapezoidCache = new();
		// Cache trapezoid masks (white-filled) separately for color-tinting use-cases
		private static readonly Dictionary<(int deviceId, float width, float height, float leftOffset, float topAngle, float rightAngle, float bottomAngle, float leftAngle), Texture2D> _trapezoidMaskCache = new();
		// Cache chevrons by device, width, height, thickness, count, and gap
		private static readonly Dictionary<(int deviceId, float width, float height, float thickness, int count, float gap), Texture2D> _chevronCache = new();
		// Cache "any" cost pip textures (tri-color circle: white/black/red thirds)
		private static readonly Dictionary<(int deviceId, int radius), Texture2D> _anyCostPipCache = new();
		// Cache diamond textures by device and size
		private static readonly Dictionary<(int deviceId, int size), Texture2D> _diamondCache = new();
		// Cache "any" cost diamond pip textures (tri-color diamond)
		private static readonly Dictionary<(int deviceId, int size), Texture2D> _anyDiamondPipCache = new();
		// Cache arbitrary polygon masks by device, dimensions, and caller-provided shape key
		private static readonly Dictionary<(int deviceId, int width, int height, string shapeKey), Texture2D> _polygonMaskCache = new();
		// Cache antialiased ring masks by device, dimensions, and thickness
		private static readonly Dictionary<(int deviceId, int width, int height, float thickness), Texture2D> _ringMaskCache = new();
		// Cache soft radial circle masks by device, diameter, and normalized stops
		private static readonly Dictionary<(int deviceId, int diameter, float innerStop, float outerStop), Texture2D> _softRadialCircleCache = new();

		public static Texture2D GetAntiAliasedCircle(GraphicsDevice device, int radius)
		{
			if (radius < 1) radius = 1;
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (deviceId, radius);
			if (_aaCircleCache.TryGetValue(key, out var existing) && existing != null) return existing;
			int d = radius * 2;
			var circle = new Texture2D(device, d, d);
			var data = new Color[d * d];
			// Subpixel AA: inner and outer radii for linear falloff
			float rInner = radius - 0.5f;
			float rOuter = radius + 0.5f;
			float r2Inner = rInner * rInner;
			float r2Outer = rOuter * rOuter;
			for (int y = 0; y < d; y++)
			{
				float dy = y - radius + 0.5f;
				for (int x = 0; x < d; x++)
				{
					float dx = x - radius + 0.5f;
					float dist2 = dx * dx + dy * dy;
					float alpha;
					if (dist2 <= r2Inner) alpha = 1f;
					else if (dist2 >= r2Outer) alpha = 0f;
					else
					{
						float dist = (float)System.Math.Sqrt(dist2);
						alpha = 1f - (dist - rInner) / (rOuter - rInner);
					}
					byte A = (byte)MathHelper.Clamp((int)System.Math.Round(alpha * 255f), 0, 255);
					data[y * d + x] = Color.FromNonPremultiplied(255, 255, 255, A);
				}
			}
			circle.SetData(data);
			_aaCircleCache[key] = circle;
			return circle;
		}

		public static Texture2D GetAntialiasedPolygonMask(
			GraphicsDevice device,
			int width,
			int height,
			string shapeKey,
			IReadOnlyList<Vector2> normalizedPoints)
		{
			width = System.Math.Max(1, width);
			height = System.Math.Max(1, height);
			shapeKey ??= string.Empty;
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (deviceId, width, height, shapeKey);
			if (_polygonMaskCache.TryGetValue(key, out var existing) && existing != null) return existing;

			var tex = new Texture2D(device, width, height);
			var data = new Color[width * height];
			var points = new Vector2[normalizedPoints?.Count ?? 0];
			for (int i = 0; i < points.Length; i++)
			{
				points[i] = new Vector2(
					MathHelper.Clamp(normalizedPoints[i].X, 0f, 1f) * width,
					MathHelper.Clamp(normalizedPoints[i].Y, 0f, 1f) * height);
			}

			float[] offsets = { 0.25f, 0.75f };
			const float sampleCount = 4f;
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int hits = 0;
					for (int sy = 0; sy < offsets.Length; sy++)
					{
						for (int sx = 0; sx < offsets.Length; sx++)
						{
							if (IsPointInPolygon(x + offsets[sx], y + offsets[sy], points))
							{
								hits++;
							}
						}
					}

					byte alpha = (byte)MathHelper.Clamp((int)System.Math.Round(hits / sampleCount * 255f), 0, 255);
					data[y * width + x] = Color.FromNonPremultiplied(255, 255, 255, alpha);
				}
			}

			tex.SetData(data);
			_polygonMaskCache[key] = tex;
			return tex;
		}

		public static Texture2D GetAntialiasedRingMask(GraphicsDevice device, int width, int height, float thicknessPx)
		{
			width = System.Math.Max(1, width);
			height = System.Math.Max(1, height);
			thicknessPx = System.Math.Max(1f, thicknessPx);
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (
				deviceId,
				width,
				height,
				(float)System.Math.Round(thicknessPx, 2)
			);
			if (_ringMaskCache.TryGetValue(key, out var existing) && existing != null) return existing;

			var tex = new Texture2D(device, width, height);
			var data = new Color[width * height];
			float rx = width * 0.5f;
			float ry = height * 0.5f;
			float halfThickness = thicknessPx * 0.5f;
			float radius = System.Math.Min(rx, ry);
			for (int y = 0; y < height; y++)
			{
				float py = y + 0.5f - ry;
				for (int x = 0; x < width; x++)
				{
					float px = x + 0.5f - rx;
					float nx = rx <= 0.001f ? 0f : px / rx;
					float ny = ry <= 0.001f ? 0f : py / ry;
					float dist = (float)System.Math.Sqrt(nx * nx + ny * ny) * radius;
					float edgeDistance = System.Math.Abs(dist - radius + halfThickness);
					float alpha;
					if (edgeDistance <= halfThickness - 0.5f) alpha = 1f;
					else if (edgeDistance >= halfThickness + 0.5f) alpha = 0f;
					else alpha = 1f - (edgeDistance - (halfThickness - 0.5f));
					byte alphaByte = (byte)MathHelper.Clamp((int)System.Math.Round(alpha * 255f), 0, 255);
					data[y * width + x] = Color.FromNonPremultiplied(255, 255, 255, alphaByte);
				}
			}

			tex.SetData(data);
			_ringMaskCache[key] = tex;
			return tex;
		}

		public static Texture2D GetSoftRadialCircle(GraphicsDevice device, int diameter, float innerStop, float outerStop)
		{
			diameter = System.Math.Max(1, diameter);
			innerStop = MathHelper.Clamp(innerStop, 0f, 1f);
			outerStop = MathHelper.Clamp(outerStop, innerStop + 0.001f, 1f);
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (
				deviceId,
				diameter,
				(float)System.Math.Round(innerStop, 3),
				(float)System.Math.Round(outerStop, 3)
			);
			if (_softRadialCircleCache.TryGetValue(key, out var existing) && existing != null) return existing;

			var tex = new Texture2D(device, diameter, diameter);
			var data = new Color[diameter * diameter];
			float radius = diameter * 0.5f;
			for (int y = 0; y < diameter; y++)
			{
				float dy = y + 0.5f - radius;
				for (int x = 0; x < diameter; x++)
				{
					float dx = x + 0.5f - radius;
					float d = (float)System.Math.Sqrt(dx * dx + dy * dy) / radius;
					float alpha = d <= innerStop
						? 1f
						: d >= outerStop
							? 0f
							: 1f - (d - innerStop) / (outerStop - innerStop);
					alpha = alpha * alpha * (3f - 2f * alpha);
					byte alphaByte = (byte)MathHelper.Clamp((int)System.Math.Round(alpha * 255f), 0, 255);
					data[y * diameter + x] = Color.FromNonPremultiplied(255, 255, 255, alphaByte);
				}
			}

			tex.SetData(data);
			_softRadialCircleCache[key] = tex;
			return tex;
		}

		private static bool IsPointInPolygon(float x, float y, IReadOnlyList<Vector2> points)
		{
			if (points == null || points.Count < 3) return false;
			bool inside = false;
			for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
			{
				var pi = points[i];
				var pj = points[j];
				bool crosses = (pi.Y > y) != (pj.Y > y);
				if (crosses)
				{
					float atX = (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X;
					if (x < atX) inside = !inside;
				}
			}
			return inside;
		}

		/// <summary>
		/// Returns a white antialiased pie-slice mask texture for tinting.
		/// Angles are in degrees, where 0 is up (12 o'clock) and increase clockwise.
		/// </summary>
		public static Texture2D GetAntialiasedPieSliceMask(GraphicsDevice device, int radius, float startAngleDeg, float endAngleDeg)
		{
			if (radius < 1) radius = 1;
			int deviceId = device?.GetHashCode() ?? 0;
			float start = NormalizeAngleDeg(startAngleDeg);
			float end = NormalizeAngleDeg(endAngleDeg);
			var key = (
				deviceId,
				radius,
				(float)System.Math.Round(start, 2),
				(float)System.Math.Round(end, 2)
			);
			if (_pieSliceCache.TryGetValue(key, out var existing) && existing != null) return existing;

			int d = radius * 2;
			var tex = new Texture2D(device, d, d);
			var data = new Color[d * d];

			float radiusSq = radius * radius;
			float[] sampleOffsets = { 0.25f, 0.75f };
			const int samplesPerPixel = 4;

			for (int y = 0; y < d; y++)
			{
				for (int x = 0; x < d; x++)
				{
					int hits = 0;
					for (int sy = 0; sy < sampleOffsets.Length; sy++)
					{
						float py = y + sampleOffsets[sy];
						for (int sx = 0; sx < sampleOffsets.Length; sx++)
						{
							float px = x + sampleOffsets[sx];
							float dx = px - radius;
							float dy = py - radius;
							float distSq = dx * dx + dy * dy;
							if (distSq > radiusSq) continue;
							float angle = AngleFromUpClockwiseDeg(dx, dy);
							if (IsAngleBetweenDeg(angle, start, end)) hits++;
						}
					}

					float alpha = hits / (float)samplesPerPixel;
					byte alphaByte = (byte)MathHelper.Clamp((int)System.Math.Round(alpha * 255f), 0, 255);
					data[y * d + x] = Color.FromNonPremultiplied(255, 255, 255, alphaByte);
				}
			}

			tex.SetData(data);
			_pieSliceCache[key] = tex;
			return tex;
		}

		private static float NormalizeAngleDeg(float angle)
		{
			angle %= 360f;
			if (angle < 0f) angle += 360f;
			return angle;
		}

		private static bool IsAngleBetweenDeg(float angle, float start, float end)
		{
			if (start <= end)
			{
				return angle >= start && angle <= end;
			}
			return angle >= start || angle <= end;
		}

		private static float AngleFromUpClockwiseDeg(float dx, float dy)
		{
			// atan2 with swapped args makes 0° at up, increasing clockwise
			float angle = MathHelper.ToDegrees((float)System.Math.Atan2(dx, -dy));
			if (angle < 0f) angle += 360f;
			return angle;
		}

		public static Texture2D GetRoundedSquare(GraphicsDevice device, int size, int radius)
		{
			if (size < 1) size = 1;
			if (radius < 0) radius = 0;
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (deviceId, size, radius);
			if (_roundedSquareCache.TryGetValue(key, out var existing) && existing != null) return existing;
			var tex = RoundedRectTextureFactory.CreateRoundedRect(device, size, size, radius);
			_roundedSquareCache[key] = tex;
			return tex;
		}

		public static Texture2D GetEquilateralTriangle(GraphicsDevice device, int size)
		{
			if (size < 1) size = 1;
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (deviceId, size);
			if (_triangleCache.TryGetValue(key, out var existing) && existing != null) return existing;

			var tex = new Texture2D(device, size, size);
			var data = new Color[size * size];

			// Define an equilateral triangle centered horizontally; slight margins for better AA
			float s = size - 2f; // side length with 1px margin
			float h = 0.8660254f * s; // sqrt(3)/2 * s
			float apexY = (size - h) * 0.5f;
			var v0 = new Vector2(size * 0.5f, apexY);           // apex (top)
			var v1 = new Vector2(v0.X - s * 0.5f, apexY + h);  // left base
			var v2 = new Vector2(v0.X + s * 0.5f, apexY + h);  // right base

			// Barycentric inside test
			for (int y = 0; y < size; y++)
			{
				float py = y + 0.5f;
				for (int x = 0; x < size; x++)
				{
					float px = x + 0.5f;
					var p = new Vector2(px, py);
					float denom = (v1.Y - v2.Y) * (v0.X - v2.X) + (v2.X - v1.X) * (v0.Y - v2.Y);
					if (System.Math.Abs(denom) < 1e-5f)
					{
						data[y * size + x] = Color.Transparent;
						continue;
					}
					float w0 = ((v1.Y - v2.Y) * (p.X - v2.X) + (v2.X - v1.X) * (p.Y - v2.Y)) / denom;
					float w1 = ((v2.Y - v0.Y) * (p.X - v2.X) + (v0.X - v2.X) * (p.Y - v2.Y)) / denom;
					float w2 = 1f - w0 - w1;
					bool inside = (w0 >= 0f) && (w1 >= 0f) && (w2 >= 0f);
					if (inside)
					{
						data[y * size + x] = Color.FromNonPremultiplied(255, 255, 255, 255);
					}
					else
					{
						data[y * size + x] = Color.Transparent;
					}
				}
			}

			tex.SetData(data);
			_triangleCache[key] = tex;
			return tex;
		}

		public static Texture2D GetAntialiasedTrapezoid(
			GraphicsDevice device,
			float width,
			float height,
			float leftSideOffset,
			float topEdgeAngleDegrees,
			float rightEdgeAngleDegrees,
			float bottomEdgeAngleDegrees,
			float leftEdgeAngleDegrees)
		{
			// Backwards-compatible: black-filled trapezoid by default
			return GetAntialiasedTrapezoidInternal(
				device,
				width,
				height,
				leftSideOffset,
				topEdgeAngleDegrees,
				rightEdgeAngleDegrees,
				bottomEdgeAngleDegrees,
				leftEdgeAngleDegrees,
				useWhiteMask: false
			);
		}

		/// <summary>
		/// Returns a white-filled antialiased trapezoid suitable for color-tinting via SpriteBatch.
		/// </summary>
		public static Texture2D GetAntialiasedTrapezoidMask(
			GraphicsDevice device,
			float width,
			float height,
			float leftSideOffset,
			float topEdgeAngleDegrees,
			float rightEdgeAngleDegrees,
			float bottomEdgeAngleDegrees,
			float leftEdgeAngleDegrees)
		{
			return GetAntialiasedTrapezoidInternal(
				device,
				width,
				height,
				leftSideOffset,
				topEdgeAngleDegrees,
				rightEdgeAngleDegrees,
				bottomEdgeAngleDegrees,
				leftEdgeAngleDegrees,
				useWhiteMask: true
			);
		}

		private static Texture2D GetAntialiasedTrapezoidInternal(
			GraphicsDevice device,
			float width,
			float height,
			float leftSideOffset,
			float topEdgeAngleDegrees,
			float rightEdgeAngleDegrees,
			float bottomEdgeAngleDegrees,
			float leftEdgeAngleDegrees,
			bool useWhiteMask)
		{
			if (width < 1) width = 1;
			if (height < 1) height = 1;
			int deviceId = device?.GetHashCode() ?? 0;

			// Use rounded values for cache key to avoid floating point precision issues
			var key = (
				deviceId,
				(float)System.Math.Round(width, 2),
				(float)System.Math.Round(height, 2),
				(float)System.Math.Round(leftSideOffset, 2),
				(float)System.Math.Round(topEdgeAngleDegrees, 2),
				(float)System.Math.Round(rightEdgeAngleDegrees, 2),
				(float)System.Math.Round(bottomEdgeAngleDegrees, 2),
				(float)System.Math.Round(leftEdgeAngleDegrees, 2)
			);

			if (useWhiteMask)
			{
				if (_trapezoidMaskCache.TryGetValue(key, out var maskExisting) && maskExisting != null)
				{
					return maskExisting;
				}
			}
			else
			{
				if (_trapezoidCache.TryGetValue(key, out var existing) && existing != null)
				{
					return existing;
				}
			}

			// Render at higher resolution for antialiasing, then scale down
			const int superSampleFactor = 2; // Render at 2x resolution
			int texWidth = (int)System.Math.Ceiling(width * superSampleFactor);
			int texHeight = (int)System.Math.Ceiling(height * superSampleFactor);
			if (texWidth < 1) texWidth = 1;
			if (texHeight < 1) texHeight = 1;

			var tex = new Texture2D(device, texWidth, texHeight);
			var data = new Color[texWidth * texHeight];

			// Convert angles to radians
			float topAngleRad = MathHelper.ToRadians(topEdgeAngleDegrees);
			float rightAngleRad = MathHelper.ToRadians(rightEdgeAngleDegrees);
			float bottomAngleRad = MathHelper.ToRadians(bottomEdgeAngleDegrees);
			float leftAngleRad = MathHelper.ToRadians(leftEdgeAngleDegrees);

			// Scale parameters for supersampling
			float scaledLeftXBase = System.Math.Max(0f, leftSideOffset * superSampleFactor);
			float scaledBottomYBase = (texHeight - 1f);
			float scaledWidth = texWidth;
			float scaledHeight = texHeight;

			// Calculate edge positions at each Y level
			float leftXAtTop = scaledLeftXBase;
			float leftXAtBottom = scaledLeftXBase + (float)System.Math.Tan(leftAngleRad) * scaledHeight;
			float rightXAtTop = scaledWidth - 1f;
			float rightXAtBottom = scaledWidth - 1f + (float)System.Math.Tan(rightAngleRad) * scaledHeight;

			// Antialiasing threshold - distance in pixels for smooth edge
			const float aaThreshold = 1.0f;

			// For each pixel, check if inside trapezoid with antialiasing
			for (int y = 0; y < texHeight; y++)
			{
				float py = y + 0.5f; // Center of pixel
				
				// Interpolate left and right edges based on Y position
				float t = (texHeight <= 1) ? 0f : (y / (float)(texHeight - 1)); // 0 at top, 1 at bottom
				float leftXAtY = MathHelper.Lerp(leftXAtTop, leftXAtBottom, t);
				float rightXAtY = MathHelper.Lerp(rightXAtTop, rightXAtBottom, t);

				for (int x = 0; x < texWidth; x++)
				{
					int idx = y * texWidth + x;
					float px = x + 0.5f; // Center of pixel
					
					// Calculate distances to edges for antialiasing
					float distToLeft = px - leftXAtY;
					float distToRight = rightXAtY - px;
					
					// Top edge equation: y = tan(topAngleRad) * (x - leftXAtTop)
					float topEdgeYAtX = (float)System.Math.Tan(topAngleRad) * (px - leftXAtTop);
					float distToTop = py - topEdgeYAtX;
					
					// Bottom edge equation: y = bottomYBase + tan(bottomAngleRad) * (x - leftXAtBottom)
					float bottomEdgeYAtX = scaledBottomYBase + (float)System.Math.Tan(bottomAngleRad) * (px - leftXAtBottom);
					float distToBottom = bottomEdgeYAtX - py;

					// Check if pixel is inside trapezoid
					bool insideX = distToLeft >= 0 && distToRight >= 0;
					bool insideY = distToTop >= 0 && distToBottom >= 0;
					bool inside = insideX && insideY;

					if (!inside)
					{
						data[idx] = Color.Transparent;
						continue;
					}

					// Calculate alpha for antialiasing based on distance to nearest edge
					float minDist = System.Math.Min(System.Math.Min(distToLeft, distToRight), System.Math.Min(distToTop, distToBottom));
					float alpha = 1.0f;
					
					if (minDist < aaThreshold)
					{
						// Smooth transition at edges
						alpha = MathHelper.Clamp(minDist / aaThreshold, 0f, 1f);
					}

					// Set pixel with alpha for antialiasing.
					// Black base for legacy callers, white mask for color-tinting.
					byte alphaByte = (byte)MathHelper.Clamp((int)System.Math.Round(alpha * 255f), 0, 255);
					if (useWhiteMask)
					{
						data[idx] = Color.FromNonPremultiplied(255, 255, 255, alphaByte); // White with alpha
					}
					else
					{
						data[idx] = Color.FromNonPremultiplied(0, 0, 0, alphaByte); // Black with alpha
					}
				}
			}

			tex.SetData(data);
			if (useWhiteMask)
			{
				_trapezoidMaskCache[key] = tex;
			}
			else
			{
				_trapezoidCache[key] = tex;
			}
			return tex;
		}

		/// <summary>
		/// Returns a white-filled parallelogram mask with the specified skew percentage.
		/// The shape leans right: the top-left corner is inset by skew%, bottom-right by skew%.
		/// </summary>
		public static Texture2D GetParallelogramMask(
			GraphicsDevice device,
			float width,
			float height,
			float skewPercent)
		{
			float skew = width * skewPercent / 100f;
			// Both edges slant the same way: top-left inset by skew, bottom-left at 0
			// Edge angle: tan(angle) = -skew / height (edges lean left going downward)
			float edgeAngleDeg = MathHelper.ToDegrees((float)System.Math.Atan2(-skew, height));
			return GetAntialiasedTrapezoidMask(
				device,
				width,
				height,
				skew,
				0f,
				edgeAngleDeg,
				0f,
				edgeAngleDeg
			);
		}

		/// <summary>
		/// Returns a white-filled antialiased upward-pointing chevron mask (or stack of chevrons).
		/// </summary>
		public static Texture2D GetAntialiasedChevronMask(
			GraphicsDevice device,
			float width,
			float height,
			float thickness,
			int count = 1,
			float gap = 0f)
		{
			if (width < 1) width = 1;
			if (height < 1) height = 1;
			if (thickness < 1) thickness = 1;
			if (count < 1) count = 1;
			int deviceId = device?.GetHashCode() ?? 0;

			var key = (deviceId, 
				(float)System.Math.Round(width, 2), 
				(float)System.Math.Round(height, 2), 
				(float)System.Math.Round(thickness, 2),
				count,
				(float)System.Math.Round(gap, 2));
			
			if (_chevronCache.TryGetValue(key, out var existing) && existing != null) return existing;

			int singleChevronHeight = (int)System.Math.Ceiling(height);
			int texWidth = (int)System.Math.Ceiling(width);
			// Total height: height for the first chevron + (count-1) * (height + gap)
			int texHeight = (int)System.Math.Ceiling(height + (count - 1) * (height + gap));
			
			var tex = new Texture2D(device, texWidth, texHeight);
			var data = new Color[texWidth * texHeight];

			float centerX = width / 2f;
			// Slope of the chevron legs: rise/run = height/centerX
			float m = height / centerX;
			// cos(theta) = 1 / sqrt(1 + m^2) where tan(theta) = m
			float cosTheta = 1f / (float)System.Math.Sqrt(1 + m * m);
			float halfThickness = thickness * 0.5f;

			for (int y = 0; y < texHeight; y++)
			{
				float py = y + 0.5f;
				for (int x = 0; x < texWidth; x++)
				{
					float px = x + 0.5f;
					float maxAlpha = 0f;

					// Check distance to each chevron in the stack
					for (int i = 0; i < count; i++)
					{
						// Offset the targetY for each stacked chevron
						float yOffset = i * (height + gap);
						// Center line: y = yOffset + m * |x - centerX|
						float targetY = yOffset + m * System.Math.Abs(px - centerX);
						
						// Distance in Y direction from current pixel to the center line
						float dy = System.Math.Abs(py - targetY);
						
						// Approximate perpendicular distance
						float dist = dy * cosTheta;

						float alpha = 0f;
						if (dist < halfThickness - 0.5f)
						{
							alpha = 1f;
						}
						else if (dist < halfThickness + 0.5f)
						{
							alpha = 1f - (dist - (halfThickness - 0.5f));
						}

						// Sharp bottom edge for each chevron if it exceeds its "cell" height
						// (Match the behavior from the loop in DisplaySystem where each is drawn at a separate Y)
						if (py > yOffset + height) alpha = 0f;
						if (py < yOffset) alpha = 0f;

						if (alpha > maxAlpha) maxAlpha = alpha;
						if (maxAlpha >= 1f) break; 
					}

					byte alphaByte = (byte)MathHelper.Clamp((int)System.Math.Round(maxAlpha * 255f), 0, 255);
					data[y * texWidth + x] = Color.FromNonPremultiplied(255, 255, 255, alphaByte);
				}
			}

			tex.SetData(data);
			_chevronCache[key] = tex;
			return tex;
		}

		/// <summary>
		/// Returns a cached tri-color "any" cost pip texture (white/black/red thirds) for the given radius.
		/// </summary>
		/// <summary>
		/// Returns a cached anti-aliased diamond (rotated square) texture of the given size.
		/// </summary>
		public static Texture2D GetDiamondTexture(GraphicsDevice device, int size)
		{
			if (size < 1) size = 1;
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (deviceId, size);
			if (_diamondCache.TryGetValue(key, out var existing) && existing != null) return existing;

			var tex = new Texture2D(device, size, size);
			var data = new Color[size * size];
			float half = size / 2f;

			for (int y = 0; y < size; y++)
			{
				float dy = System.Math.Abs(y + 0.5f - half);
				for (int x = 0; x < size; x++)
				{
					float dx = System.Math.Abs(x + 0.5f - half);
					// Diamond: |dx| + |dy| <= half
					float dist = dx + dy;
					float alpha;
					if (dist <= half - 0.7f) alpha = 1f;
					else if (dist >= half + 0.3f) alpha = 0f;
					else alpha = 1f - (dist - (half - 0.7f)) / 1f;

					byte A = (byte)MathHelper.Clamp((int)System.Math.Round(alpha * 255f), 0, 255);
					data[y * size + x] = Color.FromNonPremultiplied(255, 255, 255, A);
				}
			}

			tex.SetData(data);
			_diamondCache[key] = tex;
			return tex;
		}

		/// <summary>
		/// Returns a cached tri-color "any" cost diamond pip texture (white/black/red thirds).
		/// </summary>
		public static Texture2D GetAnyDiamondCostPipTexture(GraphicsDevice device, int size)
		{
			if (size < 1) size = 1;
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (deviceId, size);
			if (_anyDiamondPipCache.TryGetValue(key, out var existing) && existing != null) return existing;

			var tex = new Texture2D(device, size, size);
			var data = new Color[size * size];
			float half = size / 2f;

			var c0 = Color.White;
			var c1 = Color.Black;
			var c2 = Color.DarkRed;

			for (int y = 0; y < size; y++)
			{
				float py = y + 0.5f;
				float dy = System.Math.Abs(py - half);
				for (int x = 0; x < size; x++)
				{
					float px = x + 0.5f;
					float dx = System.Math.Abs(px - half);
					float dist = dx + dy;
					float alpha;
					if (dist <= half - 0.7f) alpha = 1f;
					else if (dist >= half + 0.3f) alpha = 0f;
					else alpha = 1f - (dist - (half - 0.7f)) / 1f;

					if (alpha <= 0f)
					{
						data[y * size + x] = Color.Transparent;
						continue;
					}

					// Determine which third (120-degree sectors) this pixel falls in
					float angle = MathHelper.ToDegrees((float)System.Math.Atan2(px - half, -(py - half)));
					if (angle < 0f) angle += 360f;

					Color tint;
					if (angle < 120f) tint = c0;
					else if (angle < 240f) tint = c1;
					else tint = c2;

					byte A = (byte)MathHelper.Clamp((int)System.Math.Round(alpha * 255f), 0, 255);
					data[y * size + x] = Color.FromNonPremultiplied(tint.R, tint.G, tint.B, A);
				}
			}

			tex.SetData(data);
			_anyDiamondPipCache[key] = tex;
			return tex;
		}

		public static Texture2D GetAnyCostPipTexture(GraphicsDevice device, int radius)
		{
			if (radius < 1) radius = 1;
			int deviceId = device?.GetHashCode() ?? 0;
			var key = (deviceId, radius);
			if (_anyCostPipCache.TryGetValue(key, out var existing) && existing != null) return existing;

			int d = radius * 2;
			var tex = new Texture2D(device, d, d);
			var data = new Color[d * d];

			// Build composite from three pie slices
			var slice0 = GetAntialiasedPieSliceMask(device, radius, 0f, 120f);
			var slice1 = GetAntialiasedPieSliceMask(device, radius, 120f, 240f);
			var slice2 = GetAntialiasedPieSliceMask(device, radius, 240f, 360f);

			var s0 = new Color[d * d];
			var s1 = new Color[d * d];
			var s2 = new Color[d * d];
			slice0.GetData(s0);
			slice1.GetData(s1);
			slice2.GetData(s2);

			// Tint colors for each third
			var c0 = Color.White;
			var c1 = Color.Black;
			var c2 = Color.DarkRed;

			for (int i = 0; i < data.Length; i++)
			{
				float a0 = s0[i].A / 255f;
				float a1 = s1[i].A / 255f;
				float a2 = s2[i].A / 255f;
				float totalA = a0 + a1 + a2;
				if (totalA <= 0f)
				{
					data[i] = Color.Transparent;
					continue;
				}
				float r = (c0.R * a0 + c1.R * a1 + c2.R * a2) / totalA;
				float g = (c0.G * a0 + c1.G * a1 + c2.G * a2) / totalA;
				float b = (c0.B * a0 + c1.B * a1 + c2.B * a2) / totalA;
				float a = System.Math.Min(totalA, 1f);
				data[i] = Color.FromNonPremultiplied(
					(int)MathHelper.Clamp(r, 0, 255),
					(int)MathHelper.Clamp(g, 0, 255),
					(int)MathHelper.Clamp(b, 0, 255),
					(int)MathHelper.Clamp(a * 255f, 0, 255)
				);
			}

			tex.SetData(data);
			_anyCostPipCache[key] = tex;
			return tex;
		}
	}
}

