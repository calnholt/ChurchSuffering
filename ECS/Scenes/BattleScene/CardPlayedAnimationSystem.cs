using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Listens for played-card events and spawns a brief particle burst at the card's position.
	/// Purely presentational; does not affect gameplay.
	/// </summary>
	[DebugTab("Card Played FX")]
	public class CardPlayedAnimationSystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;

		// Runtime-tunable settings
		[DebugEditable(DisplayName = "Burst Particles", Step = 1, Min = 0, Max = 500)]
		public int BurstCount { get; set; } = 260;

		[DebugEditable(DisplayName = "Particle Min Speed", Step = 5f, Min = 0f, Max = 2000f)]
		public float SpeedMin { get; set; } = 220f;

		[DebugEditable(DisplayName = "Particle Max Speed", Step = 5f, Min = 0f, Max = 2000f)]
		public float SpeedMax { get; set; } = 655f;

		[DebugEditable(DisplayName = "Particle Lifetime (s)", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float ParticleLifetimeSeconds { get; set; } = 0.7f;

		[DebugEditable(DisplayName = "Particle Size Min (px)", Step = 0.5f, Min = 0.5f, Max = 64f)]
		public float SizeMinPx { get; set; } = 2f;

		[DebugEditable(DisplayName = "Particle Size Max (px)", Step = 0.5f, Min = 0.5f, Max = 64f)]
		public float SizeMaxPx { get; set; } = 3f;

		[DebugEditable(DisplayName = "Gravity (px/s^2)", Step = 10f, Min = -4000f, Max = 4000f)]
		public float GravityY { get; set; } = 420f;

		[DebugEditable(DisplayName = "Fade Power", Step = 0.05f, Min = 0.1f, Max = 4f)]
		public float FadePower { get; set; } = 0.75f;



		private struct Particle
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Age;
			public float Lifetime;
			public float Size;
			public Color Color;
		}

		private class Burst
		{
			public Vector2 Origin;
			public float Age;
			public readonly List<Particle> Particles = new List<Particle>();
		}

		private readonly List<Burst> _bursts = new List<Burst>();
		private Texture2D _px;
		private static readonly System.Random _rand = new System.Random();

		public CardPlayedAnimationSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			EventManager.Subscribe<CardMoveRequested>(OnCardMoveRequested);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			for (int b = _bursts.Count - 1; b >= 0; b--)
			{
				var burst = _bursts[b];
				burst.Age += dt;
				for (int i = burst.Particles.Count - 1; i >= 0; i--)
				{
					var p = burst.Particles[i];
					p.Age += dt;
					p.Velocity.Y += GravityY * dt;
					p.Position += p.Velocity * dt;
					if (p.Age >= p.Lifetime)
					{
						burst.Particles.RemoveAt(i);
					}
					else
					{
						burst.Particles[i] = p;
					}
				}
				// Remove empty bursts after particles finish
				if (burst.Particles.Count == 0)
				{
					_bursts.RemoveAt(b);
				}
			}
			base.Update(gameTime);
		}

		public void Draw()
		{
			EnsurePixel();
			if (_bursts.Count == 0) return;
			foreach (var burst in _bursts)
			{
				// Particles
				for (int i = 0; i < burst.Particles.Count; i++)
				{
					var p = burst.Particles[i];
					float t = MathHelper.Clamp(p.Age / Math.Max(0.0001f, p.Lifetime), 0f, 1f);
					float alpha = MathF.Pow(1f - t, FadePower);
					var color = p.Color * alpha;
					var size = new Vector2(p.Size, p.Size);
					_spriteBatch.Draw(_px, position: p.Position, sourceRectangle: null, color: color, rotation: 0f, origin: new Vector2(0.5f, 0.5f), scale: size, effects: SpriteEffects.None, layerDepth: 0f);
				}
			}
		}

		private void OnCardMoveRequested(CardMoveRequested evt)
		{
			try
			{
				if (evt == null || evt.Card == null) return;
				if (evt.Reason != "PlayCard") return; // only trigger when card is actually being played
				var t = evt.Card.GetComponent<Transform>();
				var cd = evt.Card.GetComponent<CardData>();
				if (t == null || cd == null) return;
				var color = ResolveCardBgColor(cd.Color);
				SpawnBurst(t.Position);
			}
			catch { }
		}

		private void SpawnBurst(Vector2 origin)
		{
			EnsurePixel();
			LoggingService.Append("CardPlayedAnimationSystem.SpawnBurst", new System.Text.Json.Nodes.JsonObject { ["action"] = "Spawn burst", ["originX"] = (decimal)origin.X, ["originY"] = (decimal)origin.Y });
			var burst = new Burst { Origin = origin, Age = 0f };
			int count = Math.Max(0, BurstCount);
			for (int i = 0; i < count; i++)
			{
				float ang = (float)(_rand.NextDouble() * Math.PI * 2);
				float spd = MathHelper.Lerp(SpeedMin, SpeedMax, (float)_rand.NextDouble());
				float size = MathHelper.Lerp(SizeMinPx, SizeMaxPx, (float)_rand.NextDouble());
				float life = ParticleLifetimeSeconds * (0.7f + 0.6f * (float)_rand.NextDouble());
				var vel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * spd;
				burst.Particles.Add(new Particle
				{
					Position = origin,
					Velocity = vel,
					Age = 0f,
					Lifetime = life,
					Size = size,
					Color = ResolveCardBgColor(CardData.CardColor.White)
				});
			}
			_bursts.Add(burst);
		}

		private void EnsurePixel()
		{
			if (_px != null) return;
			_px = new Texture2D(_graphicsDevice, 1, 1);
			_px.SetData(new[] { Color.White });
		}

		private Color ResolveCardBgColor(CardData.CardColor color)
		{
			switch (color)
			{
				case CardData.CardColor.Red: return Color.DarkRed;
				case CardData.CardColor.Black: return Color.Black;
				case CardData.CardColor.White:
				default: return Color.White;
			}
		}
	}
}


