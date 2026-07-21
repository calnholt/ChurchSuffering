using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Exhaust Smoke")]
	public class ExhaustOnBlockDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;

		private Texture2D _circle;
		private int _circleRadiusUsed;

		private static readonly System.Random _rng = new System.Random();

		private readonly Dictionary<int, List<Particle>> _particlesByEntity = new();
		private readonly Dictionary<int, float> _spawnAccumulatorByEntity = new();

		[DebugEditable(DisplayName = "Particles / Sec", Step = 0.5f, Min = 0f, Max = 200f)]
		public float ParticlesPerSecond { get; set; } = 12f;

		[DebugEditable(DisplayName = "Max Particles", Step = 1, Min = 0, Max = 500)]
		public int MaxParticlesPerCard { get; set; } = 60;

		[DebugEditable(DisplayName = "Lifetime (s)", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float LifetimeSeconds { get; set; } = 0.9f;

		[DebugEditable(DisplayName = "Rise Speed", Step = 1, Min = 0, Max = 400)]
		public float RiseSpeedPxPerSec { get; set; } = 70f;

		[DebugEditable(DisplayName = "Drift Speed", Step = 1, Min = 0, Max = 200)]
		public float DriftSpeedPxPerSec { get; set; } = 25f;

		[DebugEditable(DisplayName = "Start Scale", Step = 0.05f, Min = 0.1f, Max = 3f)]
		public float StartScale { get; set; } = 0.6f;

		[DebugEditable(DisplayName = "End Scale", Step = 0.05f, Min = 0.1f, Max = 4f)]
		public float EndScale { get; set; } = 1.4f;

		[DebugEditable(DisplayName = "Start Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
		public float StartAlpha { get; set; } = 0.5f;

		[DebugEditable(DisplayName = "End Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
		public float EndAlpha { get; set; } = 0.0f;

		[DebugEditable(DisplayName = "Base Radius", Step = 1, Min = 1, Max = 64)]
		public int BaseCircleRadiusPx { get; set; } = 8;

		[DebugEditable(DisplayName = "Color R", Step = 1, Min = 0, Max = 255)]
		public int R { get; set; } = 200;
		[DebugEditable(DisplayName = "Color G", Step = 1, Min = 0, Max = 255)]
		public int G { get; set; } = 200;
		[DebugEditable(DisplayName = "Color B", Step = 1, Min = 0, Max = 255)]
		public int B { get; set; } = 200;

		[DebugEditable(DisplayName = "Emitter Off X", Step = 1, Min = -200, Max = 200)]
		public int EmitterOffsetX { get; set; } = 0;
		[DebugEditable(DisplayName = "Emitter Off Y", Step = 1, Min = -200, Max = 200)]
		public int EmitterOffsetY { get; set; } = -6;

		[DebugEditable(DisplayName = "Spawn Jitter X", Step = 1, Min = 0, Max = 60)]
		public int SpawnJitterX { get; set; } = 10;
		[DebugEditable(DisplayName = "Spawn Jitter Y", Step = 1, Min = 0, Max = 60)]
		public int SpawnJitterY { get; set; } = 4;

		[DebugEditable(DisplayName = "Z-Order", Step = 1, Min = 0, Max = 20000)]
		public int ZOrder { get; set; } = 10005;

		[DebugEditable(DisplayName = "Fallback Card W", Step = 1, Min = 10, Max = 400)]
		public int FallbackCardW { get; set; } = 100;
		[DebugEditable(DisplayName = "Fallback Card H", Step = 1, Min = 10, Max = 400)]
		public int FallbackCardH { get; set; } = 130;

		public ExhaustOnBlockDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<ExhaustOnBlock>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (dt <= 0f) return;
			EnsureCircle();

			var ui = entity.GetComponent<UIElement>();
			var abc = entity.GetComponent<AssignedBlockCard>();
			var assignedPresentation = entity.GetComponent<AssignedBlockPresentation>();
			var tr = entity.GetComponent<Transform>();

			Vector2 origin;
			if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
			{
				origin = new Vector2(ui.Bounds.Center.X, ui.Bounds.Top);
			}
			else if (abc != null && assignedPresentation != null)
			{
				float topY = assignedPresentation.RenderPos.Y - (FallbackCardH * assignedPresentation.CurrentScale * 0.5f);
				origin = new Vector2(assignedPresentation.RenderPos.X, topY);
			}
			else
			{
				origin = tr?.Position ?? Vector2.Zero;
			}
			origin += new Vector2(EmitterOffsetX, EmitterOffsetY);

			if (!_particlesByEntity.TryGetValue(entity.Id, out var list))
			{
				list = new List<Particle>();
				_particlesByEntity[entity.Id] = list;
			}

			float acc = _spawnAccumulatorByEntity.TryGetValue(entity.Id, out var existing) ? existing : 0f;
			acc += ParticlesPerSecond * dt;
			int capacity = System.Math.Max(0, MaxParticlesPerCard - list.Count);
			int toSpawn = System.Math.Min((int)acc, capacity);
			acc -= toSpawn;
			_spawnAccumulatorByEntity[entity.Id] = acc;

			for (int i = 0; i < toSpawn; i++)
			{
				list.Add(Spawn(origin));
			}

			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (!list[i].Step(dt, RiseSpeedPxPerSec, DriftSpeedPxPerSec))
				{
					list.RemoveAt(i);
				}
			}

			// If the marker was removed and no particles remain, clean up the bookkeeping
			if (list.Count == 0 && entity.GetComponent<ExhaustOnBlock>() == null)
			{
				_particlesByEntity.Remove(entity.Id);
				_spawnAccumulatorByEntity.Remove(entity.Id);
			}
		}

		public override void Update(GameTime gameTime)
		{
			// Update active exhaust sources (spawning + stepping)
			base.Update(gameTime);

			// Continue animating any remaining particles for entities that no longer have ExhaustOnBlock
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (dt <= 0f) return;

			// Build current active id set
			var active = EntityManager.GetEntitiesWithComponent<ExhaustOnBlock>().Select(e => e.Id).ToHashSet();

			// Step particles for inactive ids without spawning new ones
			var keys = _particlesByEntity.Keys.ToList();
			for (int k = 0; k < keys.Count; k++)
			{
				int id = keys[k];
				if (active.Contains(id)) continue;
				if (!_particlesByEntity.TryGetValue(id, out var list) || list == null) continue;

				for (int i = list.Count - 1; i >= 0; i--)
				{
					if (!list[i].Step(dt, RiseSpeedPxPerSec, DriftSpeedPxPerSec))
					{
						list.RemoveAt(i);
					}
				}
				_spawnAccumulatorByEntity[id] = 0f;
				if (list.Count == 0)
				{
					_particlesByEntity.Remove(id);
					_spawnAccumulatorByEntity.Remove(id);
				}
			}
		}

		public void Draw()
		{
			if (_circle == null) EnsureCircle();
			foreach (var kv in _particlesByEntity)
			{
				var list = kv.Value;
				for (int i = 0; i < list.Count; i++)
				{
					var p = list[i];
					float a = MathHelper.Lerp(StartAlpha, EndAlpha, p.Age01);
					float s = MathHelper.Lerp(StartScale, EndScale, p.Age01);
					var color = Color.FromNonPremultiplied(
						(int)MathHelper.Clamp(R, 0, 255),
						(int)MathHelper.Clamp(G, 0, 255),
						(int)MathHelper.Clamp(B, 0, 255),
						(int)MathHelper.Clamp((int)System.Math.Round(a * 255f), 0, 255)
					);
					_spriteBatch.Draw(
						_circle,
						p.Pos,
						null,
						color,
						0f,
						new Vector2(_circle.Width / 2f, _circle.Height / 2f),
						s,
						SpriteEffects.None,
						0f
					);
				}
			}
		}

		private void EnsureCircle()
		{
			int radius = System.Math.Max(1, BaseCircleRadiusPx);
			if (_circle == null || _circleRadiusUsed != radius)
			{
				_circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, radius);
				_circleRadiusUsed = radius;
			}
		}

		private Particle Spawn(Vector2 origin)
		{
			float jx = (float)(_rng.NextDouble() * 2.0 - 1.0) * SpawnJitterX;
			float jy = (float)(_rng.NextDouble() * 2.0 - 1.0) * SpawnJitterY;
			return new Particle(new Vector2(origin.X + jx, origin.Y + jy), LifetimeSeconds);
		}

		private sealed class Particle
		{
			public Vector2 Pos;
			public float Life;
			public float Age;
			public float Phase;

			public Particle(Vector2 p, float life)
			{
				Pos = p;
				Life = System.Math.Max(0.05f, life);
				Age = 0f;
				Phase = 0f;
			}

			public bool Step(float dt, float rise, float drift)
			{
				Age += dt;
				if (Age >= Life) return false;
				Phase += dt;
				Pos += new Vector2((float)System.Math.Sin(Phase * 4f) * drift * dt, -rise * dt);
				return true;
			}

			public float Age01 => MathHelper.Clamp(Life <= 1e-4f ? 1f : Age / Life, 0f, 1f);
		}
	}
}

