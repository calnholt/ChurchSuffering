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

		[DebugEditable(DisplayName = "Lunge Anticipation", Step = 1f, Min = 0f, Max = 80f)]
		public float LungeAnticipationDistance { get; set; } = 18f;

		[DebugEditable(DisplayName = "Lunge Recoil", Step = 1f, Min = 0f, Max = 80f)]
		public float LungeRecoilDistance { get; set; } = 14f;

		[DebugEditable(DisplayName = "Target Shake Pixels", Step = 1f, Min = 0f, Max = 80f)]
		public float TargetShakePixels { get; set; } = 18f;

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
			foreach (var actor in EntityManager.GetAllEntities())
			{
				if (actor.GetComponent<Player>() == null
					&& actor.GetComponent<Enemy>() == null
					&& actor.GetComponent<ActorPresentationState>() == null)
				{
					continue;
				}
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

			foreach (var entity in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>())
			{
				var active = entity.GetComponent<ActiveVisualEffect>();
				if (active == null) continue;
				ApplyActiveEffect(active);
			}
		}

		private void ApplyActiveEffect(ActiveVisualEffect effect)
		{
			if (effect.ElapsedSeconds < 0f) return;
			if (effect.Recipe.HasModule(VisualEffectModule.ActorLunge))
			{
				var actor = ResolveLungeActor(effect);
				var state = EnsureState(actor);
				state.DrawOffset += ComputeLungeOffset(effect);
			}

			if (effect.Recipe.HasModule(VisualEffectModule.ActorSquashStretch))
			{
				var state = EnsureState(effect.Target);
				var scale = ComputeBuffScale(effect);
				state.ScaleMultiplier = new Vector2(
					state.ScaleMultiplier.X * scale.X,
					state.ScaleMultiplier.Y * scale.Y);
			}

			if (effect.Recipe.HasModule(VisualEffectModule.TargetShake))
			{
				var state = EnsureState(effect.Target);
				if (state != null)
				{
					state.DrawOffset += ComputeTargetShake(effect)
						* TargetShakePixels
						* Math.Max(0f, effect.Recipe.Intensity);
				}
			}
		}

		private static Vector2 ComputeTargetShake(ActiveVisualEffect effect)
		{
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			if (recovery <= 0f)
			{
				return approach < 0.82f
					? Vector2.Zero
					: new Vector2(-0.22f, 0.12f) * ((approach - 0.82f) / 0.18f);
			}

			float envelope = MathF.Pow(1f - recovery, 1.7f);
			float x = MathF.Sin(recovery * MathHelper.Pi * 13f);
			float y = MathF.Cos(recovery * MathHelper.Pi * 17f) * 0.68f;
			return new Vector2(x, y) * envelope;
		}

		private Vector2 ComputeLungeOffset(ActiveVisualEffect effect)
		{
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			var dir = effect.TargetAnchor - effect.SourceAnchor;
			if (dir.LengthSquared() > 0.0001f)
			{
				dir.Normalize();
			}
			else
			{
				dir = new Vector2(effect.DirectionSign, 0f);
			}
			float intensity = Math.Max(0f, effect.Recipe.Intensity);
			var peak = dir * LungeDistance * intensity;
			var overshoot = peak * (1f + Math.Max(0f, LungeOvershoot));
			if (recovery <= 0f)
			{
				const float anticipationEnd = 0.28f;
				if (approach <= anticipationEnd)
				{
					float anticipation = VisualEffectDisplayMath.EaseInOutQuad(approach / anticipationEnd);
					return -dir * LungeAnticipationDistance * intensity * anticipation;
				}
				float strike = (approach - anticipationEnd) / (1f - anticipationEnd);
				return Vector2.Lerp(-dir * LungeAnticipationDistance * intensity, overshoot, VisualEffectDisplayMath.EaseOutCubic(strike));
			}

			const float recoilEnd = 0.24f;
			if (recovery <= recoilEnd)
			{
				return Vector2.Lerp(overshoot, -dir * LungeRecoilDistance * intensity, VisualEffectDisplayMath.EaseOutCubic(recovery / recoilEnd));
			}
			return Vector2.Lerp(-dir * LungeRecoilDistance * intensity, Vector2.Zero, VisualEffectDisplayMath.EaseInOutQuad((recovery - recoilEnd) / (1f - recoilEnd)));
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

		private static Vector2 ComputeBuffScale(ActiveVisualEffect effect)
		{
			float approach = VisualEffectDisplayMath.ApproachProgress(effect);
			float recovery = VisualEffectDisplayMath.RecoveryProgress(effect);
			float intensity = Math.Max(0f, effect.Recipe.Intensity);
			if (recovery <= 0f)
			{
				var compressed = new Vector2(1f + 0.22f * intensity, 1f - 0.20f * intensity);
				return Vector2.Lerp(Vector2.One, compressed, VisualEffectDisplayMath.EaseOutCubic(approach));
			}
			if (recovery < 0.26f)
			{
				var compressed = new Vector2(1f + 0.22f * intensity, 1f - 0.20f * intensity);
				var released = new Vector2(1f - 0.16f * intensity, 1f + 0.24f * intensity);
				return Vector2.Lerp(compressed, released, VisualEffectDisplayMath.EaseOutCubic(recovery / 0.26f));
			}
			float settle = (recovery - 0.26f) / 0.74f;
			float wobble = MathF.Sin(settle * MathHelper.Pi * 3f) * (1f - settle) * 0.08f * intensity;
			return new Vector2(1f + wobble, 1f - wobble);
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
