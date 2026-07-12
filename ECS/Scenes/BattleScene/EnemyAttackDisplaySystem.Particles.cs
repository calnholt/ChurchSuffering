using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public partial class EnemyAttackDisplaySystem
	{
		private enum BannerParticleKind { FrameShard, BoneChip, Ember }

		private struct BannerParticle
		{
			public BannerParticleKind Kind;
			public Vector2 Position;
			public Vector2 Velocity;
			public float Rotation;
			public float AngularVelocity;
			public float Age;
			public float Lifetime;
			public float Size;
			public Color Color;
		}

		private struct AbsorbEmber
		{
			public Vector2 Start;
			public Vector2 Target;
			public Vector2 Arc;
			public float Delay;
			public float Lifetime;
		}

		private readonly List<BannerParticle> _particles = new();
		private readonly List<AbsorbEmber> _absorbEmbers = new();

		internal int ActiveParticleCount => _particles.Count;

		private void SpawnImpactParticles(float intensity, int sequence, int panelWidth, int panelHeight)
		{
			_particles.Clear();
			var random = new Random(unchecked(sequence * 397) ^ 0x30CC);
			int total = (int)MathF.Round(MathHelper.Lerp(
				Math.Max(0, ParticleCountMin),
				Math.Max(ParticleCountMin, ParticleCountMax),
				intensity));
			int shardCount = (int)MathF.Round(total * 0.36f);
			int boneCount = (int)MathF.Round(total * 0.20f);
			int emberCount = Math.Max(0, total - shardCount - boneCount);
			float halfWidth = panelWidth * 0.5f;
			float halfHeight = panelHeight * 0.5f;

			for (int i = 0; i < shardCount; i++)
			{
				float side = i % 2 == 0 ? -1f : 1f;
				float speed = RandomRange(random, 190f, 420f) * MathHelper.Lerp(0.75f, 1.2f, intensity);
				_particles.Add(new BannerParticle
				{
					Kind = BannerParticleKind.FrameShard,
					Position = new Vector2(side * halfWidth, RandomRange(random, -halfHeight * 0.75f, halfHeight * 0.85f)),
					Velocity = new Vector2(side * speed, RandomRange(random, -150f, 80f)),
					Rotation = RandomRange(random, -0.5f, 0.5f),
					AngularVelocity = RandomRange(random, -9f, 9f),
					Age = 0f,
					Lifetime = RandomRange(random, 0.42f, 0.82f),
					Size = RandomRange(random, 4f, 8f),
					Color = random.NextDouble() < 0.68 ? new Color(178, 20, 31) : new Color(34, 27, 29),
				});
			}

			for (int i = 0; i < boneCount; i++)
			{
				float angle = RandomRange(random, -2.75f, -0.38f);
				float speed = RandomRange(random, 120f, 270f) * MathHelper.Lerp(0.8f, 1.15f, intensity);
				_particles.Add(new BannerParticle
				{
					Kind = BannerParticleKind.BoneChip,
					Position = new Vector2(0f, -halfHeight * 0.42f),
					Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
					Rotation = RandomRange(random, -0.4f, 0.4f),
					AngularVelocity = RandomRange(random, -12f, 12f),
					Age = 0f,
					Lifetime = RandomRange(random, 0.35f, 0.7f),
					Size = RandomRange(random, 2.5f, 5.5f),
					Color = new Color(222, 207, 178),
				});
			}

			for (int i = 0; i < emberCount; i++)
			{
				float edgeX = RandomRange(random, -halfWidth, halfWidth);
				float edgeY = random.NextDouble() < 0.5 ? -halfHeight : halfHeight;
				_particles.Add(new BannerParticle
				{
					Kind = BannerParticleKind.Ember,
					Position = new Vector2(edgeX, edgeY),
					Velocity = new Vector2(RandomRange(random, -60f, 60f), RandomRange(random, -190f, -70f)),
					Age = 0f,
					Lifetime = RandomRange(random, 0.55f, 1.05f),
					Size = RandomRange(random, 1.5f, 3.5f),
					Color = random.NextDouble() < 0.6 ? new Color(225, 35, 29) : new Color(244, 105, 35),
				});
			}
		}

		private void UpdateImpactParticles(float dt)
		{
			int write = 0;
			for (int read = 0; read < _particles.Count; read++)
			{
				var particle = _particles[read];
				particle.Age += dt;
				if (particle.Age >= particle.Lifetime) continue;
				float drag = particle.Kind == BannerParticleKind.Ember ? Math.Min(ParticleDrag, 0.965f) : ParticleDrag;
				float gravity = particle.Kind == BannerParticleKind.Ember ? -22f : ParticleGravity;
				particle.Velocity.Y += gravity * dt;
				particle.Velocity *= MathF.Pow(drag, dt * 60f);
				particle.Position += particle.Velocity * dt;
				particle.Rotation += particle.AngularVelocity * dt;
				_particles[write++] = particle;
			}
			if (write < _particles.Count) _particles.RemoveRange(write, _particles.Count - write);
		}

		private void SpawnAbsorbEmbers(Rectangle startBounds, Vector2 target, int sequence)
		{
			_absorbEmbers.Clear();
			var random = new Random(unchecked(sequence * 613) ^ 0x5A17);
			for (int i = 0; i < 12; i++)
			{
				var start = new Vector2(
					RandomRange(random, startBounds.Left, startBounds.Right),
					RandomRange(random, startBounds.Top, startBounds.Bottom));
				_absorbEmbers.Add(new AbsorbEmber
				{
					Start = start,
					Target = target,
					Arc = new Vector2(RandomRange(random, -80f, 80f), RandomRange(random, -70f, 20f)),
					Delay = i * 0.018f,
					Lifetime = Math.Max(0.12f, AbsorbDurationSeconds - i * 0.012f),
				});
			}
		}

		private static float RandomRange(Random random, float min, float max)
		{
			return min + (float)random.NextDouble() * (max - min);
		}
	}
}
