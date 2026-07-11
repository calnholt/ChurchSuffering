using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Modular FX Particles")]
	public sealed class ModularEffectParticleDisplaySystem : Core.System
	{
		private enum ParticleKind
		{
			Shard,
			Debris,
			Smoke
		}

		private static readonly Color RedBright = new(239, 52, 72);
		private static readonly Color Cream = new(255, 245, 223);
		private static readonly Color DebrisColor = new(29, 23, 25);
		private static readonly Color SmokeColor = new(130, 116, 116);

		private static readonly Vector2[] JaggedParticleMask =
		{
			new(0.50f, 0.00f),
			new(1.00f, 0.76f),
			new(0.62f, 1.00f),
			new(0.00f, 0.42f)
		};

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private readonly Dictionary<Guid, List<Particle>> _particlesByRequest = new();

		[DebugEditable(DisplayName = "Particle Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float ParticleAlpha { get; set; } = 1f;

		[DebugEditable(DisplayName = "Particle Size", Step = 1f, Min = 1f, Max = 80f)]
		public float ParticleSize { get; set; } = 1.45f;

		[DebugEditable(DisplayName = "Scatter Distance", Step = 1f, Min = 0f, Max = 500f)]
		public float ScatterDistance { get; set; } = 1.25f;

		[DebugEditable(DisplayName = "Smoke Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
		public float SmokeAlpha { get; set; } = 0.82f;

		public ModularEffectParticleDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsActive) return;
			var active = EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>()
				.Select(e => e.GetComponent<ActiveVisualEffect>())
				.Where(e => e != null && e.ElapsedSeconds >= 0f)
				.ToDictionary(e => e.RequestId, e => e);

			foreach (var effect in active.Values)
			{
				if (!_particlesByRequest.ContainsKey(effect.RequestId))
				{
					_particlesByRequest[effect.RequestId] = Spawn(effect);
				}
			}

			foreach (var stale in _particlesByRequest.Keys.Where(id => !active.ContainsKey(id)).ToList())
			{
				_particlesByRequest.Remove(stale);
			}
		}

		public void Draw()
		{
			foreach (var active in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>().Select(e => e.GetComponent<ActiveVisualEffect>()).Where(e => e != null && e.ElapsedSeconds >= 0f))
			{
				if (!_particlesByRequest.TryGetValue(active.RequestId, out var particles)) continue;
				float t = VisualEffectDisplayMath.RecoveryProgress(active);
				foreach (var p in particles)
				{
					if (p.Kind == ParticleKind.Smoke) DrawSmokeParticle(p, t, active.Recipe.Intensity);
					else DrawJaggedParticle(p, t, active.Recipe.Intensity);
				}
			}
		}

		private List<Particle> Spawn(ActiveVisualEffect effect)
		{
			var particles = new List<Particle>();
			var random = new Random(new VisualEffectVariation(effect).Seed);
			AddJaggedParticles(effect, VisualEffectModule.Shards, ParticleKind.Shard, 16, particles, random);
			AddJaggedParticles(effect, VisualEffectModule.Debris, ParticleKind.Debris, 18, particles, random);
			AddSmokeParticles(effect, 11, particles, random);
			return particles;
		}

		private void AddJaggedParticles(ActiveVisualEffect effect, VisualEffectModule module, ParticleKind kind, int baseCount, List<Particle> particles, Random random)
		{
			if (!effect.Recipe.Modules.Contains(module)) return;
			int count = Math.Max(0, (int)Math.Round(baseCount * effect.Recipe.ParticleMultiplier));
			bool targetRight = effect.DirectionSign >= 0;
			float startDeg = targetRight ? -175f : 165f;
			float endDeg = targetRight ? 15f : 355f;
			float minDistance = kind == ParticleKind.Shard ? 100f : 120f;
			float maxDistance = kind == ParticleKind.Shard ? 330f : 390f;

			for (int i = 0; i < count; i++)
			{
				float angle = MathHelper.ToRadians(RandomRange(random, startDeg, endDeg));
				float distance = RandomRange(random, minDistance, maxDistance);
				var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
				offset.Y += RandomRange(random, -70f, 55f);
				particles.Add(new Particle
				{
					Kind = kind,
					Start = effect.ImpactAnchor + new Vector2(RandomRange(random, -10f, 10f), RandomRange(random, -10f, 10f)),
					Offset = offset,
					Rotation = MathHelper.ToRadians(RandomRange(random, -240f, 240f)),
					Scale = RandomRange(random, 0.72f, 1.75f),
					Color = kind == ParticleKind.Shard ? RedBright : DebrisColor
				});
			}
		}

		private void AddSmokeParticles(ActiveVisualEffect effect, int baseCount, List<Particle> particles, Random random)
		{
			if (!effect.Recipe.Modules.Contains(VisualEffectModule.SmokeBlobs)) return;
			int count = Math.Max(0, (int)Math.Round(baseCount * effect.Recipe.ParticleMultiplier));
			bool targetRight = effect.DirectionSign >= 0;
			for (int i = 0; i < count; i++)
			{
				particles.Add(new Particle
				{
					Kind = ParticleKind.Smoke,
					Start = effect.ImpactAnchor + new Vector2(RandomRange(random, -16f, 16f), RandomRange(random, -10f, 10f)),
					Offset = new Vector2(
						RandomRange(random, targetRight ? -110f : 20f, targetRight ? 90f : 140f),
						RandomRange(random, -150f, -48f)),
					Rotation = 0f,
					Scale = RandomRange(random, 1.0f, 1.72f),
					Color = SmokeColor
				});
			}
		}

		private void DrawJaggedParticle(Particle particle, float t, float intensity)
		{
			float alpha = ParticleAlpha * intensity * VisualEffectDisplayMath.Window(t, 0f, 0.08f, 0.36f, 1f);
			if (alpha <= 0f) return;
			float eased = VisualEffectDisplayMath.EaseOutCubic(t);
			float gravity = particle.Kind == ParticleKind.Debris ? 150f : 70f;
			var pos = particle.Start + particle.Offset * eased * ScatterDistance + new Vector2(0f, gravity * t * t);
			float scale = MathHelper.Lerp(0.5f, particle.Scale, eased) * ParticleSize;
			float rotation = particle.Rotation * eased;
			var mask = PrimitiveTextureFactory.GetAntialiasedPolygonMask(_graphicsDevice, 22, 34, "modular_fx_jagged_particle", JaggedParticleMask);
			if (particle.Kind == ParticleKind.Debris)
			{
				DrawMask(mask, pos, Cream * (alpha * 0.14f), rotation, new Vector2(scale * 1.16f));
			}
			DrawMask(mask, pos, particle.Color * alpha, rotation, new Vector2(scale));
			if (particle.Kind == ParticleKind.Shard)
			{
				DrawMask(mask, pos, Cream * (alpha * 0.52f), rotation, new Vector2(scale * 0.48f));
			}
		}

		private void DrawSmokeParticle(Particle particle, float t, float intensity)
		{
			float alpha = SmokeAlpha * intensity * VisualEffectDisplayMath.Window(t, 0f, 0.18f, 0.68f, 1f);
			if (alpha <= 0f) return;
			float eased = VisualEffectDisplayMath.EaseOutCubic(t);
			var pos = particle.Start + new Vector2(0f, 10f) * (1f - eased) + particle.Offset * eased;
			float scale = MathHelper.Lerp(0.48f, 1.28f, eased) * particle.Scale * ParticleSize;
			int diameter = Math.Max(1, (int)MathF.Round(48f * scale));
			var smoke = PrimitiveTextureFactory.GetSoftRadialCircle(_graphicsDevice, diameter, 0f, 0.86f);
			DrawMask(smoke, pos, particle.Color * alpha, 0f, Vector2.One);
			DrawMask(smoke, pos, new Color(116, 18, 32) * (alpha * 0.28f), 0f, new Vector2(1.18f));
		}

		private void DrawMask(Texture2D texture, Vector2 center, Color color, float rotation, Vector2 scale)
		{
			_spriteBatch.Draw(texture, center, null, color, rotation, new Vector2(texture.Width * 0.5f, texture.Height * 0.5f), scale, SpriteEffects.None, 0f);
		}

		private static float RandomRange(Random random, float min, float max)
		{
			return min + (float)random.NextDouble() * (max - min);
		}

		private struct Particle
		{
			public ParticleKind Kind;
			public Vector2 Start;
			public Vector2 Offset;
			public float Rotation;
			public float Scale;
			public Color Color;
		}
	}
}
