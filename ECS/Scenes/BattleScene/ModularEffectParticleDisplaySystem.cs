using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Modular FX Particles")]
	public sealed class ModularEffectParticleDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private readonly Dictionary<Guid, List<Particle>> _particlesByRequest = new();
		private readonly Random _random = new(1337);

		[DebugEditable(DisplayName = "Particle Speed", Step = 1f, Min = 0f, Max = 600f)]
		public float ParticleSpeed { get; set; } = 220f;

		[DebugEditable(DisplayName = "Particle Size", Step = 1f, Min = 1f, Max = 40f)]
		public float ParticleSize { get; set; } = 10f;

		public ModularEffectParticleDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : base(entityManager)
		{
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
				.Where(e => e != null)
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
			foreach (var active in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>().Select(e => e.GetComponent<ActiveVisualEffect>()).Where(e => e != null))
			{
				if (!_particlesByRequest.TryGetValue(active.RequestId, out var particles)) continue;
				float t = MathHelper.Clamp((active.ElapsedSeconds - active.Timing.ImpactTimeSeconds) / Math.Max(0.0001f, active.Timing.DurationSeconds - active.Timing.ImpactTimeSeconds), 0f, 1f);
				foreach (var p in particles)
				{
					var pos = p.Start + p.Velocity * t;
					float size = ParticleSize * p.Size * (1f - t * 0.45f);
					var rect = new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size / 2f), (int)Math.Max(1f, size), (int)Math.Max(1f, size));
					_spriteBatch.Draw(_pixel, rect, p.Color * (1f - t));
				}
			}
		}

		private List<Particle> Spawn(ActiveVisualEffect effect)
		{
			var particles = new List<Particle>();
			AddModuleParticles(effect, VisualEffectModule.Shards, 11, Color.LightGoldenrodYellow, particles);
			AddModuleParticles(effect, VisualEffectModule.Debris, 12, new Color(65, 55, 45), particles);
			AddModuleParticles(effect, VisualEffectModule.SmokeBlobs, 7, new Color(45, 45, 45), particles);
			return particles;
		}

		private void AddModuleParticles(ActiveVisualEffect effect, VisualEffectModule module, int baseCount, Color color, List<Particle> particles)
		{
			if (!effect.Recipe.Modules.Contains(module)) return;
			int count = Math.Max(0, (int)Math.Round(baseCount * effect.Recipe.ParticleMultiplier));
			for (int i = 0; i < count; i++)
			{
				float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
				var dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
				if (module == VisualEffectModule.SmokeBlobs) dir.Y -= 1.4f;
				if (dir.LengthSquared() > 0.0001f) dir.Normalize();
				particles.Add(new Particle
				{
					Start = effect.ImpactAnchor,
					Velocity = dir * (ParticleSpeed * (0.4f + (float)_random.NextDouble() * 0.8f)),
					Size = 0.65f + (float)_random.NextDouble() * 1.2f,
					Color = color
				});
			}
		}

		private struct Particle
		{
			public Vector2 Start;
			public Vector2 Velocity;
			public float Size;
			public Color Color;
		}
	}
}
