using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Modular FX Actor Presentation")]
	public sealed class ModularEffectActorPresentationSystem : Core.System
	{
		[DebugEditable(DisplayName = "Lunge Distance", Step = 1f, Min = 0f, Max = 160f)]
		public float LungeDistance { get; set; } = 54f;

		[DebugEditable(DisplayName = "Lunge Overshoot", Step = 0.01f, Min = 0f, Max = 2f)]
		public float LungeOvershoot { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Damage Flash Duration (s)", Step = 0.05f, Min = 0.05f, Max = 2f)]
		public float DamageFlashDurationSec { get; set; } = 0.3f;

		[DebugEditable(DisplayName = "Damage Flash R", Step = 1f, Min = 0f, Max = 255f)]
		public float DamageFlashColorR { get; set; } = 255f;

		[DebugEditable(DisplayName = "Damage Flash G", Step = 1f, Min = 0f, Max = 255f)]
		public float DamageFlashColorG { get; set; } = 80f;

		[DebugEditable(DisplayName = "Damage Flash B", Step = 1f, Min = 0f, Max = 255f)]
		public float DamageFlashColorB { get; set; } = 80f;

		public ModularEffectActorPresentationSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ModifyHpEvent>(OnModifyHp);
			EventManager.Subscribe<StartDebuffAnimation>(OnStartDebuffAnimation);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetAllEntities()
				.Where(e => e.GetComponent<Player>() != null || e.GetComponent<Enemy>() != null || e.GetComponent<ActorPresentationState>() != null);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsActive) return;
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			var actors = GetRelevantEntities().ToList();
			foreach (var actor in actors)
			{
				var state = EnsureState(actor);
				state.DrawOffset = Vector2.Zero;
				state.ScaleMultiplier = Vector2.One;
				state.TintColor = Color.White;
				if (state.DamageFlashTimer > 0f)
				{
					state.DamageFlashTimer = Math.Max(0f, state.DamageFlashTimer - dt);
					float progress = state.DamageFlashTimer / Math.Max(0.0001f, DamageFlashDurationSec);
					var flashColor = new Color((int)DamageFlashColorR, (int)DamageFlashColorG, (int)DamageFlashColorB);
					state.TintColor = Color.Lerp(Color.White, flashColor, progress);
				}
			}

			foreach (var active in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>()
				.Select(e => e.GetComponent<ActiveVisualEffect>())
				.Where(e => e != null))
			{
				ApplyActiveEffect(active);
			}
		}

		private void ApplyActiveEffect(ActiveVisualEffect effect)
		{
			if (effect.ElapsedSeconds < 0f) return;
			if (effect.Recipe.Modules.Contains(VisualEffectModule.ActorLunge))
			{
				var actor = ResolveLungeActor(effect);
				var state = EnsureState(actor);
				state.DrawOffset += ComputeLungeOffset(effect);
			}

			if (effect.Recipe.Modules.Contains(VisualEffectModule.ActorSquashStretch))
			{
				var state = EnsureState(effect.Target);
				var scale = ComputeBuffScale(VisualEffectDisplayMath.SampleElapsed(effect));
				state.ScaleMultiplier = new Vector2(
					state.ScaleMultiplier.X * scale.X,
					state.ScaleMultiplier.Y * scale.Y);
			}
		}

		private Vector2 ComputeLungeOffset(ActiveVisualEffect effect)
		{
			float duration = Math.Max(0.0001f, effect.Timing.DurationSeconds);
			float elapsed = VisualEffectDisplayMath.SampleElapsed(effect);
			float impact = MathHelper.Clamp(effect.Timing.ImpactTimeSeconds / duration, 0.08f, 0.82f);
			float t = MathHelper.Clamp(elapsed / duration, 0f, 1f);
			float outPhase = MathHelper.Clamp(t / impact, 0f, 1f);
			float backPhase = MathHelper.Clamp((t - impact) / Math.Max(0.0001f, 1f - impact), 0f, 1f);
			var dir = effect.TargetAnchor - effect.SourceAnchor;
			if (dir.LengthSquared() > 0.0001f)
			{
				dir.Normalize();
			}
			else
			{
				dir = new Vector2(effect.DirectionSign, 0f);
			}
			var peak = dir * LungeDistance * Math.Max(0f, effect.Recipe.Intensity);
			var overshoot = peak * (1f + Math.Max(0f, LungeOvershoot));
			var outOffset = Vector2.Lerp(Vector2.Zero, overshoot, VisualEffectDisplayMath.EaseOutCubic(outPhase));
			if (backPhase <= 0f) return outOffset;
			return Vector2.Lerp(peak, Vector2.Zero, VisualEffectDisplayMath.EaseInOutQuad(backPhase));
		}

		private Entity ResolveLungeActor(ActiveVisualEffect effect)
		{
			if (effect.Source?.GetComponent<Player>() != null || effect.Source?.GetComponent<Enemy>() != null)
			{
				return effect.Source;
			}
			if (effect.SourceKind == VisualEffectSourceKind.EnemyAttack)
			{
				return EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
			}
			return EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
		}

		private static Vector2 ComputeBuffScale(float elapsed)
		{
			var keyframes = new[]
			{
				(scale: new Vector2(1.25f, 0.75f), duration: 0.288f),
				(scale: new Vector2(0.75f, 1.25f), duration: 0.096f),
				(scale: new Vector2(1.15f, 0.85f), duration: 0.096f),
				(scale: new Vector2(0.95f, 1.05f), duration: 0.144f),
				(scale: new Vector2(1.05f, 0.95f), duration: 0.096f),
				(scale: new Vector2(1f, 1f), duration: 0.240f),
			};

			var from = Vector2.One;
			float remaining = Math.Max(0f, elapsed);
			foreach (var keyframe in keyframes)
			{
				float duration = Math.Max(0.0001f, keyframe.duration);
				if (remaining <= duration)
				{
					float t = MathHelper.Clamp(remaining / duration, 0f, 1f);
					float eased = t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
					return Vector2.Lerp(from, keyframe.scale, eased);
				}
				remaining -= duration;
				from = keyframe.scale;
			}
			return Vector2.One;
		}

		private void OnModifyHp(ModifyHpEvent evt)
		{
			if (evt == null || evt.Delta >= 0 || evt.Target == null) return;
			var state = EnsureState(evt.Target);
			state.DamageFlashTimer = DamageFlashDurationSec;
		}

		private void OnStartDebuffAnimation(StartDebuffAnimation evt)
		{
			if (evt == null) return;
			var target = evt.TargetIsPlayer
				? EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault()
				: EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
			if (target == null) return;
			var state = EnsureState(target);
			state.ScaleMultiplier = new Vector2(0.9f, 0.9f);
		}

		private ActorPresentationState EnsureState(Entity entity)
		{
			if (entity == null) return null;
			var state = entity.GetComponent<ActorPresentationState>();
			if (state == null)
			{
				state = new ActorPresentationState();
				EntityManager.AddComponent(entity, state);
			}
			return state;
		}
	}
}
