using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Renders a subtle moving haze of round sand clouds across the screen
	/// when the current battlefield is Desert. Draws after the background
	/// and under foreground elements.
	/// </summary>
	[DebugTab("Desert Background")] 
	public class DesertBackgroundEffectSystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Random _random = new Random();

		private bool _isActive;
		private float _elapsed;
		private Texture2D _cloudTexture; // pill alpha texture (white), tinted at draw
		// cached params to know when to rebuild texture
		private int _cachedSoftEdgePx;
		private float _cachedCornerRadiusFraction;
		private float _cachedEdgeFalloffPower;
		private float _cachedEdgeNoiseAmount;
		private float _cachedEdgeNoiseScale;
		private float _cachedEndCapSoftness;
		private float _cachedEndCapPower;
		private readonly List<Cloud> _clouds = new();
		private float _spawnAccumulator;

		private struct Cloud
		{
			public Vector2 Position;
			public float LengthPx;
			public float HeightPx;
			public float SpeedX;
			public float Alpha;
			public float YDriftAmp;
			public float YDriftHz;
			public float Seed;
		}

		// Debug-editable settings
		[DebugEditable(DisplayName = "Max Clouds", Step = 1, Min = 0, Max = 400)]
		public int MaxClouds { get; set; } = 400;

		[DebugEditable(DisplayName = "Spawn Rate (per sec)", Step = 0.1f, Min = 0f, Max = 20f)]
		public float SpawnRatePerSecond { get; set; } = 20f;

		[DebugEditable(DisplayName = "Min Thickness (px)", Step = 1, Min = 1, Max = 1000)]
		public int MinThicknessPx { get; set; } = 300;

		[DebugEditable(DisplayName = "Max Thickness (px)", Step = 1, Min = 1, Max = 2000)]
		public int MaxThicknessPx { get; set; } = 800;

		[DebugEditable(DisplayName = "Min Length (px)", Step = 1, Min = 1, Max = 4000)]
		public int MinLengthPx { get; set; } = 220;

		[DebugEditable(DisplayName = "Max Length (px)", Step = 1, Min = 1, Max = 8000)]
		public int MaxLengthPx { get; set; } = 536;

		[DebugEditable(DisplayName = "Min Speed (px/s)", Step = 1, Min = -2000, Max = 2000)]
		public float MinSpeedPxPerSec { get; set; } = 30f;

		[DebugEditable(DisplayName = "Max Speed (px/s)", Step = 1, Min = -2000, Max = 2000)]
		public float MaxSpeedPxPerSec { get; set; } = 400f;

		[DebugEditable(DisplayName = "Base Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
		public float BaseAlpha { get; set; } = 0.3f;

		[DebugEditable(DisplayName = "Alpha Jitter", Step = 0.05f, Min = 0f, Max = 1f)]
		public float AlphaJitter { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Y Drift Amp (px)", Step = 1, Min = 0, Max = 200)]
		public int YDriftAmplitudePx { get; set; } = 16;

		[DebugEditable(DisplayName = "Y Drift Hz Min", Step = 0.01f, Min = 0f, Max = 2f)]
		public float YDriftHzMin { get; set; } = 0.05f;

		[DebugEditable(DisplayName = "Y Drift Hz Max", Step = 0.01f, Min = 0f, Max = 2f)]
		public float YDriftHzMax { get; set; } = 0.12f;

		[DebugEditable(DisplayName = "Corner Radius Fraction", Step = 0.02f, Min = 0f, Max = 0.5f)]
		public float CornerRadiusFraction { get; set; } = 0.5f; // fraction of height

		[DebugEditable(DisplayName = "Soft Edge Px", Step = 1, Min = 0, Max = 96)]
		public int SoftEdgePx { get; set; } = 46;

		[DebugEditable(DisplayName = "Edge Falloff Power", Step = 0.1f, Min = 0.1f, Max = 5f)]
		public float EdgeFalloffPower { get; set; } = 2.0f;

		[DebugEditable(DisplayName = "Edge Noise Amount", Step = 0.05f, Min = 0f, Max = 1f)]
		public float EdgeNoiseAmount { get; set; } = 0.25f;

		[DebugEditable(DisplayName = "Edge Noise Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float EdgeNoiseScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "End Cap Softness (0..0.5)", Step = 0.02f, Min = 0.02f, Max = 0.5f)]
		public float EndCapSoftnessFraction { get; set; } = 0.49f;

		[DebugEditable(DisplayName = "End Cap Power", Step = 0.1f, Min = 0.5f, Max = 4f)]
		public float EndCapPower { get; set; } = 1.2f;

		[DebugEditable(DisplayName = "Tint R", Step = 1, Min = 0, Max = 255)]
		public int TintR { get; set; } = 232;
		[DebugEditable(DisplayName = "Tint G", Step = 1, Min = 0, Max = 255)]
		public int TintG { get; set; } = 204;
		[DebugEditable(DisplayName = "Tint B", Step = 1, Min = 0, Max = 255)]
		public int TintB { get; set; } = 160;

		public DesertBackgroundEffectSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// Visual-only system
			return Array.Empty<Entity>();
		}

		public override void Update(GameTime gameTime)
		{
			// Determine active state from Battlefield component
			Battlefield battlefield = null;
			foreach (var e in EntityManager.GetEntitiesWithComponent<Battlefield>())
			{
				battlefield = e.GetComponent<Battlefield>();
				if (battlefield != null) break;
			}
			bool shouldBeActive = battlefield != null && battlefield.Location == BattleLocation.Desert;
			if (_isActive && !shouldBeActive)
			{
				_clouds.Clear();
				_spawnAccumulator = 0f;
			}
			// Detect activation edge to seed initial clouds so we enter in medias res
			bool wasActive = _isActive;
			_isActive = shouldBeActive;

			_elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
			EnsureCloudTexture();
			if (_isActive)
			{
				if (!wasActive)
				{
					SeedInitialClouds();
				}
				SpawnClouds((float)gameTime.ElapsedGameTime.TotalSeconds);
				float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
				int viewportW = Game1.VirtualWidth;
				for (int i = _clouds.Count - 1; i >= 0; i--)
				{
					var c = _clouds[i];
					float yDrift = c.YDriftAmp * (float)Math.Sin(MathHelper.TwoPi * c.YDriftHz * (_elapsed + c.Seed));
					c.Position.X += c.SpeedX * dt;
					c.Position.Y += yDrift * dt; // subtle vertical flutter
					// Cull when far off-screen to the right
					if (c.Position.X - (c.LengthPx * 0.5f) > viewportW + 100)
					{
						_clouds.RemoveAt(i);
						continue;
					}
					_clouds[i] = c;
				}
			}
			base.Update(gameTime);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			if (!_isActive || _cloudTexture == null) return;

			var tintBase = new Color((byte)Math.Clamp(TintR, 0, 255), (byte)Math.Clamp(TintG, 0, 255), (byte)Math.Clamp(TintB, 0, 255));

			for (int i = 0; i < _clouds.Count; i++)
			{
				var c = _clouds[i];
				float alpha = MathHelper.Clamp(c.Alpha, 0f, 1f);
				var color = tintBase * alpha;
				float scaleX = Math.Max(0.01f, c.LengthPx / Math.Max(1f, _cloudTexture.Width));
				float scaleY = Math.Max(0.01f, c.HeightPx / Math.Max(1f, _cloudTexture.Height));
				_spriteBatch.Draw(
					_cloudTexture,
					position: c.Position,
					sourceRectangle: null,
					color: color,
					rotation: 0f,
					origin: new Vector2(_cloudTexture.Width / 2f, _cloudTexture.Height / 2f),
					scale: new Vector2(scaleX, scaleY),
					effects: SpriteEffects.None,
					layerDepth: 0f
				);
			}
		}

		private void SpawnClouds(float dt)
		{
			if (MaxClouds <= 0 || SpawnRatePerSecond <= 0f) return;
			_spawnAccumulator += SpawnRatePerSecond * dt;
			int toSpawn = (int)_spawnAccumulator;
			if (toSpawn <= 0) return;
			_spawnAccumulator -= toSpawn;

			int viewportH = Game1.VirtualHeight;
			for (int i = 0; i < toSpawn && _clouds.Count < MaxClouds; i++)
			{
				float height = MathHelper.Clamp(MinThicknessPx + (float)_random.NextDouble() * (MaxThicknessPx - MinThicknessPx), MinThicknessPx, MaxThicknessPx);
				float length = MathHelper.Clamp(MinLengthPx + (float)_random.NextDouble() * (MaxLengthPx - MinLengthPx), MinLengthPx, MaxLengthPx);
				float speed = MathHelper.Clamp(MinSpeedPxPerSec + (float)_random.NextDouble() * (MaxSpeedPxPerSec - MinSpeedPxPerSec), -2000f, 2000f);
				float alpha = MathHelper.Clamp(BaseAlpha + ((float)_random.NextDouble() * 2f - 1f) * AlphaJitter, 0f, 1f);
				float y = (float)_random.NextDouble() * viewportH;
				float seed = (float)_random.NextDouble() * 1000f;
				_clouds.Add(new Cloud
				{
					Position = new Vector2(-length * 0.5f - 100f, y),
					LengthPx = length,
					HeightPx = height,
					SpeedX = Math.Max(5f, speed),
					Alpha = alpha,
					YDriftAmp = YDriftAmplitudePx * (0.6f + 0.8f * (float)_random.NextDouble()),
					YDriftHz = MathHelper.Lerp(YDriftHzMin, YDriftHzMax, (float)_random.NextDouble()),
					Seed = seed
				});
			}
		}

		private void SeedInitialClouds()
		{
			if (MaxClouds <= 0) return;
			int viewportW = Game1.VirtualWidth;
			int viewportH = Game1.VirtualHeight;
			int desired = Math.Min(MaxClouds, (int)(MaxClouds * 0.6f));
			for (int i = 0; i < desired && _clouds.Count < MaxClouds; i++)
			{
				float height = MathHelper.Clamp(MinThicknessPx + (float)_random.NextDouble() * (MaxThicknessPx - MinThicknessPx), MinThicknessPx, MaxThicknessPx);
				float length = MathHelper.Clamp(MinLengthPx + (float)_random.NextDouble() * (MaxLengthPx - MinLengthPx), MinLengthPx, MaxLengthPx);
				float speed = MathHelper.Clamp(MinSpeedPxPerSec + (float)_random.NextDouble() * (MaxSpeedPxPerSec - MinSpeedPxPerSec), -2000f, 2000f);
				float alpha = MathHelper.Clamp(BaseAlpha + ((float)_random.NextDouble() * 2f - 1f) * AlphaJitter, 0f, 1f);
				float y = (float)_random.NextDouble() * viewportH;
				float seed = (float)_random.NextDouble() * 1000f;
				// distribute X across a band spanning left off-screen to right edge so some are already on-screen
				float xMin = -viewportW - 200f;
				float xMax = viewportW + 100f;
				float x = MathHelper.Lerp(xMin, xMax, (float)_random.NextDouble());
				_clouds.Add(new Cloud
				{
					Position = new Vector2(x, y),
					LengthPx = length,
					HeightPx = height,
					SpeedX = Math.Max(5f, speed),
					Alpha = alpha,
					YDriftAmp = YDriftAmplitudePx * (0.6f + 0.8f * (float)_random.NextDouble()),
					YDriftHz = MathHelper.Lerp(YDriftHzMin, YDriftHzMax, (float)_random.NextDouble()),
					Seed = seed
				});
			}
		}

		private void EnsureCloudTexture()
		{
			// Rebuild if parameters affecting the shape changed
			if (_cloudTexture != null)
			{
				if (_cachedSoftEdgePx == SoftEdgePx &&
					Math.Abs(_cachedCornerRadiusFraction - CornerRadiusFraction) < 0.0001f &&
					Math.Abs(_cachedEdgeFalloffPower - EdgeFalloffPower) < 0.0001f &&
					Math.Abs(_cachedEdgeNoiseAmount - EdgeNoiseAmount) < 0.0001f &&
					Math.Abs(_cachedEdgeNoiseScale - EdgeNoiseScale) < 0.0001f &&
					Math.Abs(_cachedEndCapSoftness - EndCapSoftnessFraction) < 0.0001f &&
					Math.Abs(_cachedEndCapPower - EndCapPower) < 0.0001f)
				{
					return;
				}
				_cloudTexture.Dispose();
				_cloudTexture = null;
			}
			int texW = 256; // base pill texture; scaled per cloud
			int texH = 64;
			_cloudTexture = new Texture2D(_graphicsDevice, texW, texH, false, SurfaceFormat.Color);
			var data = new Color[texW * texH];
			float halfW = texW * 0.5f;
			float halfH = texH * 0.5f;
			float radius = Math.Max(2f, CornerRadiusFraction * texH);
			float soft = Math.Max(0f, SoftEdgePx);
			for (int y = 0; y < texH; y++)
			{
				float py = y - halfH + 0.5f;
				for (int x = 0; x < texW; x++)
				{
					float px = x - halfW + 0.5f;
					// signed distance to rounded rectangle (pill) centered at 0 with half-size (halfW, halfH)
					float qx = Math.Abs(px) - (halfW - radius);
					float qy = Math.Abs(py) - (halfH - radius);
					float ax = Math.Max(qx, 0f);
					float ay = Math.Max(qy, 0f);
					float outside = (float)Math.Sqrt(ax * ax + ay * ay);
					float inside = Math.Min(Math.Max(qx, qy), 0f);
					float dist = outside + inside - radius; // <0 inside
					float a = 1f - MathHelper.Clamp((dist + soft) / Math.Max(0.0001f, soft), 0f, 1f);
					// adjustable falloff shaping and softening
					a = (float)Math.Pow(MathHelper.Clamp(a, 0f, 1f), Math.Max(0.1f, EdgeFalloffPower));
					a = a * a * (3f - 2f * a);
					// edge noise to add hazy irregular border
					if (EdgeNoiseAmount > 0f)
					{
						float ns = Math.Max(0.01f, EdgeNoiseScale);
						float n = (float)(Math.Sin((x * 12.9898 * ns) + (y * 78.233 * ns)) * 43758.5453);
						n = n - (float)Math.Floor(n);
						float edge = 1f - a; // stronger near edge
						a *= 1f - MathHelper.Clamp(EdgeNoiseAmount * edge * n, 0f, 1f);
					}

					// additional end-cap softening to ensure rounded ends along the length
					float u = (x + 0.5f) / texW; // 0..1 along length
					float s = MathHelper.Clamp(EndCapSoftnessFraction, 0.02f, 0.5f);
					// smoothstep from both ends toward center
					float inL = MathHelper.Clamp(u / s, 0f, 1f); inL = inL * inL * (3f - 2f * inL);
					float inR = MathHelper.Clamp((1f - u) / s, 0f, 1f); inR = inR * inR * (3f - 2f * inR);
					float cap = inL * inR;
					cap = (float)Math.Pow(cap, Math.Max(0.5f, EndCapPower));
					a *= cap;
					data[y * texW + x] = Color.FromNonPremultiplied(255, 255, 255, (int)(a * 255));
				}
			}
			_cloudTexture.SetData(data);
			_cachedSoftEdgePx = SoftEdgePx;
			_cachedCornerRadiusFraction = CornerRadiusFraction;
			_cachedEdgeFalloffPower = EdgeFalloffPower;
			_cachedEdgeNoiseAmount = EdgeNoiseAmount;
			_cachedEdgeNoiseScale = EdgeNoiseScale;
			_cachedEndCapSoftness = EndCapSoftnessFraction;
			_cachedEndCapPower = EndCapPower;
		}
	}
}


