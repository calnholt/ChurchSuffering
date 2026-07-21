using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Renders animated cathedral light beams when the current battle location is Cathedral.
	/// Draws after the background and under foreground elements.
	/// </summary>
	[DebugTab("CathedralLighting")] 
	public class CathedralLightingSystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;

		private bool _isActive;
		private float _elapsedSeconds;
		private Texture2D _beamTexture; // soft-edged rectangular gradient
		private readonly Random _random = new Random();

		private readonly List<Beam> _beams = new();
		private float _spawnAccumulator;

		// --- Runtime adjustable settings (debug menu) ---
		private int _numberOfBeams = 6;
		[DebugEditable(DisplayName = "Max Concurrent Beams", Step = 1f, Min = 1f, Max = 48f)]
		public int NumberOfBeams { get => _numberOfBeams; set => _numberOfBeams = Math.Max(1, value); }

		private float _spawnRatePerSecond = 0.8f;
		[DebugEditable(DisplayName = "Spawn Rate (per sec)", Step = 0.1f, Min = 0f, Max = 10f)]
		public float SpawnRatePerSecond { get => _spawnRatePerSecond; set => _spawnRatePerSecond = MathHelper.Clamp(value, 0f, 10f); }

		private float _beamBaseThicknessPx = 140f;
		[DebugEditable(DisplayName = "Beam Thickness (px)", Step = 1f, Min = 4f, Max = 2000f)]
		public float BeamBaseThicknessPx { get => _beamBaseThicknessPx; set => _beamBaseThicknessPx = MathHelper.Clamp(value, 4f, 2000f); }

		private float _beamSpreadPx = 420f;
		[DebugEditable(DisplayName = "Beam Spread (px)", Step = 5f, Min = 0f, Max = 5000f)]
		public float BeamSpreadPx { get => _beamSpreadPx; set => _beamSpreadPx = MathHelper.Clamp(value, 0f, 5000f); }

		private float _baseAlpha = 0.55f;
		[DebugEditable(DisplayName = "Base Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float BaseAlpha { get => _baseAlpha; set => _baseAlpha = MathHelper.Clamp(value, 0f, 1f); }

		private float _variationAmount = 0.75f;
		[DebugEditable(DisplayName = "Cloud Variation", Step = 0.01f, Min = 0f, Max = 2f)]
		public float VariationAmount { get => _variationAmount; set => _variationAmount = MathHelper.Clamp(value, 0f, 2f); }

		private float _cloudSpeedHz1 = 0.10f;
		private float _cloudSpeedHz2 = 0.23f;
		[DebugEditable(DisplayName = "Cloud Speed 1 (Hz)", Step = 0.01f, Min = 0f, Max = 2f)]
		public float CloudSpeedHz1 { get => _cloudSpeedHz1; set => _cloudSpeedHz1 = MathHelper.Clamp(value, 0f, 2f); }
		[DebugEditable(DisplayName = "Cloud Speed 2 (Hz)", Step = 0.01f, Min = 0f, Max = 2f)]
		public float CloudSpeedHz2 { get => _cloudSpeedHz2; set => _cloudSpeedHz2 = MathHelper.Clamp(value, 0f, 2f); }

		// Slight excess to ensure full coverage across the diagonal
		private float _lengthOverscan = 1.15f;
		[DebugEditable(DisplayName = "Length Overscan", Step = 0.01f, Min = 1f, Max = 2f)]
		public float LengthOverscan { get => _lengthOverscan; set => _lengthOverscan = MathHelper.Clamp(value, 1f, 2f); }

		private float _minLifetime = 2.5f;
		private float _maxLifetime = 6.0f;
		[DebugEditable(DisplayName = "Min Lifetime (s)", Step = 0.1f, Min = 0.1f, Max = 60f)]
		public float MinLifetime { get => _minLifetime; set => _minLifetime = MathHelper.Clamp(value, 0.1f, _maxLifetime); }
		[DebugEditable(DisplayName = "Max Lifetime (s)", Step = 0.1f, Min = 0.1f, Max = 60f)]
		public float MaxLifetime { get => _maxLifetime; set => _maxLifetime = Math.Max(value, _minLifetime); }

		private float _fadeInFraction = 0.25f;
		private float _fadeOutFraction = 0.35f;
		[DebugEditable(DisplayName = "Fade In Fraction", Step = 0.01f, Min = 0.01f, Max = 0.9f)]
		public float FadeInFraction { get => _fadeInFraction; set => _fadeInFraction = MathHelper.Clamp(value, 0.01f, 0.9f); }
		[DebugEditable(DisplayName = "Fade Out Fraction", Step = 0.01f, Min = 0.01f, Max = 0.9f)]
		public float FadeOutFraction { get => _fadeOutFraction; set => _fadeOutFraction = MathHelper.Clamp(value, 0.01f, 0.9f); }

		private struct Beam
		{
			public float OffsetPx;           // perpendicular offset from top-right anchor along normal
			public float ThicknessJitter;    // multiplicative jitter [0.85..1.15]
			public float Phase1;             // radians for cloud layer 1
			public float Phase2;             // radians for cloud layer 2
			public float IntensityBias;      // baseline multiplier per-beam
			public float Age;                // seconds
			public float Lifetime;           // seconds
		}

		public CathedralLightingSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// Presentation-only
			return Array.Empty<Entity>();
		}

		public override void Update(GameTime gameTime)
		{
			// Read current battlefield location from component and toggle activity accordingly
			Battlefield battlefield = null;
			foreach (var e in EntityManager.GetEntitiesWithComponent<Battlefield>())
			{
				battlefield = e.GetComponent<Battlefield>();
				if (battlefield != null) break;
			}
			bool shouldBeActive = battlefield != null && battlefield.Location == BattleLocation.Cathedral;
			if (_isActive && !shouldBeActive)
			{
				_beams.Clear();
				_spawnAccumulator = 0f;
			}
			_isActive = shouldBeActive;

			_elapsedSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
			EnsureBeamTexture();
			if (_isActive)
			{
				SpawnBeams((float)gameTime.ElapsedGameTime.TotalSeconds);
				// update existing beams and cull expired
				for (int i = _beams.Count - 1; i >= 0; i--)
				{
					var b = _beams[i];
					b.Age += (float)gameTime.ElapsedGameTime.TotalSeconds;
					if (b.Age >= b.Lifetime)
					{
						_beams.RemoveAt(i);
						continue;
					}
					_beams[i] = b;
				}
			}
			base.Update(gameTime);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			if (!_isActive || _beamTexture == null || _numberOfBeams <= 0) return;

			int viewportW = Game1.VirtualWidth;
			int viewportH = Game1.VirtualHeight;

			// Diagonal from top-right to bottom-left
			var start = new Vector2(viewportW, 0f);
			var dir = new Vector2(-viewportW, viewportH);
			float diagLength = dir.Length() * _lengthOverscan;
			dir.Normalize();
			var normal = new Vector2(-dir.Y, dir.X); // 90° cw
			float rotation = MathF.Atan2(dir.Y, dir.X);

			float texW = _beamTexture.Width;
			float texH = _beamTexture.Height;
			var origin = new Vector2(0f, texH / 2f); // left-center so X-scale stretches along the beam

			for (int i = 0; i < _beams.Count; i++)
			{
				var b = _beams[i];
				float t = _elapsedSeconds;
				float layer1 = 0.5f + 0.5f * MathF.Sin(MathHelper.TwoPi * _cloudSpeedHz1 * t + b.Phase1);
				float layer2 = 0.5f + 0.5f * MathF.Sin(MathHelper.TwoPi * _cloudSpeedHz2 * t + b.Phase2 + b.OffsetPx * 0.01f);
				float clouds = layer1 * layer2; // simple 2-layer interference
				float intensity = _baseAlpha * (b.IntensityBias * (0.5f + 0.5f * (1f - _variationAmount + _variationAmount * clouds)));
				// lifetime envelope: ease-in then ease-out
				float lifeT = b.Lifetime > 0f ? MathHelper.Clamp(b.Age / b.Lifetime, 0f, 1f) : 1f;
				float aIn = MathF.Min(1f, _fadeInFraction > 0f ? lifeT / _fadeInFraction : 1f);
				float aOut = MathF.Min(1f, _fadeOutFraction > 0f ? (1f - lifeT) / _fadeOutFraction : 1f);
				float lifeAlpha = MathF.Pow(aIn * aOut, 1.2f);
				intensity *= lifeAlpha;
				intensity = MathHelper.Clamp(intensity, 0f, 1f);

				float thickness = _beamBaseThicknessPx * b.ThicknessJitter * (0.9f + 0.2f * clouds);
				var position = start + normal * b.OffsetPx;
				var scale = new Vector2(diagLength / texW, thickness / texH);

				// warm sunlight color
				var color = new Color(255, 245, 210) * intensity;
				_spriteBatch.Draw(_beamTexture, position, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
			}
		}

		// Event handler removed; system now reads Battlefield component each Update

		private void SpawnBeams(float dt)
		{
			if (_spawnRatePerSecond <= 0f || _numberOfBeams <= 0) return;
			_spawnAccumulator += _spawnRatePerSecond * dt;
			int toSpawn = (int)_spawnAccumulator;
			if (toSpawn <= 0) return;
			_spawnAccumulator -= toSpawn;
			for (int i = 0; i < toSpawn && _beams.Count < _numberOfBeams; i++)
			{
				SpawnOneBeam();
			}
		}

		private void SpawnOneBeam()
		{
			// random offset across spread with slight jitter
			float offset = MathHelper.Lerp(-_beamSpreadPx, _beamSpreadPx, (float)_random.NextDouble());
			offset += (float)(_random.NextDouble() * 40.0 - 20.0);
			float thicknessJitter = 0.9f + 0.2f * (float)_random.NextDouble();
			float phase1 = MathHelper.TwoPi * (float)_random.NextDouble();
			float phase2 = MathHelper.TwoPi * (float)_random.NextDouble();
			float bias = 0.85f + 0.3f * (float)_random.NextDouble();
			float lifetime = MathHelper.Lerp(_minLifetime, _maxLifetime, (float)_random.NextDouble());

			_beams.Add(new Beam
			{
				OffsetPx = offset,
				ThicknessJitter = thicknessJitter,
				Phase1 = phase1,
				Phase2 = phase2,
				IntensityBias = bias,
				Age = 0f,
				Lifetime = lifetime
			});
		}

		private void EnsureBeamTexture()
		{
			if (_beamTexture != null) return;

			int texWidth = 512;  // length axis
			int texHeight = 64;  // thickness axis
			_beamTexture = new Texture2D(_graphicsDevice, texWidth, texHeight, false, SurfaceFormat.Color);
			var data = new Color[texWidth * texHeight];

			for (int y = 0; y < texHeight; y++)
			{
				float v = (y + 0.5f) / texHeight; // 0..1
				float dy = MathF.Abs(v - 0.5f) / 0.5f; // 0 at center, 1 at edges
				// Soft edge across thickness using smooth step
				float edge = 1f - (dy * dy * (3f - 2f * dy));

				for (int x = 0; x < texWidth; x++)
				{
					float u = (x + 0.5f) / texWidth; // along length
					// Gentle head fade-in and tail fade-out to avoid harsh caps
					float head = u; // 0..1
					float tail = 1f - u;
					float cap = MathF.Min(1f, MathF.Min(head * 2.5f, tail * 1.2f));
					float a = MathHelper.Clamp(edge * cap, 0f, 1f);
					// store as premultiplied-friendly white; tint applied at draw
					data[y * texWidth + x] = Color.FromNonPremultiplied(255, 255, 255, (int)(a * 255));
				}
			}

			_beamTexture.SetData(data);
		}
	}
}


