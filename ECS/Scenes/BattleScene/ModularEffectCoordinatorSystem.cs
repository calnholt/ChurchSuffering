using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Modular Effect Coordinator")]
	public sealed class ModularEffectCoordinatorSystem : Core.System
	{
		private readonly List<VisualEffectRequested> _pendingRejectedGameplayRequests = new();

		[DebugEditable(DisplayName = "Global Intensity Multiplier", Step = 0.01f, Min = 0f, Max = 4f)]
		public float GlobalIntensityMultiplier { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Global Particle Multiplier", Step = 0.01f, Min = 0f, Max = 4f)]
		public float GlobalParticleMultiplier { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Max Concurrent Effects", Step = 1f, Min = 1f, Max = 64f)]
		public int MaxConcurrentEffects { get; set; } = 16;

		public ModularEffectCoordinatorSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<VisualEffectRequested>(OnVisualEffectRequested);
			EventManager.Subscribe<LoadSceneEvent>(_ => ClearActiveEffects());
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsActive) return;
			FlushRejectedGameplayRequests();
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			foreach (var entity in GetRelevantEntities().ToList())
			{
				var active = entity.GetComponent<ActiveVisualEffect>();
				if (active == null) continue;
				active.ElapsedSeconds += Math.Max(0f, dt);
				if (active.ElapsedSeconds < 0f) continue;
				if (!active.StartSfxPublished)
				{
					PublishStartSfx(active);
				}
				if (!active.ImpactPublished && active.ElapsedSeconds >= active.Timing.ImpactTimeSeconds)
				{
					PublishImpact(active);
				}
				if (!active.CompletionPublished && active.ElapsedSeconds >= active.Timing.DurationSeconds)
				{
					PublishCompletion(active);
					EntityManager.DestroyEntity(entity.Id);
				}
			}
		}

		private void OnVisualEffectRequested(VisualEffectRequested request)
		{
			if (request == null || request.Recipe == null) return;
			var recipe = request.Recipe.Clone()
				.WithIntensity(request.Recipe.Intensity * GlobalIntensityMultiplier)
				.WithParticleMultiplier(request.Recipe.ParticleMultiplier * GlobalParticleMultiplier);

			if (!TryResolveAnchor(request.Source, out var sourceAnchor)
				|| !TryResolveAnchor(request.Target, out var targetAnchor))
			{
				RejectRequest(request, "MissingAnchor");
				return;
			}

			if (!TryReserveCapacity(request))
			{
				RejectRequest(request, "Capacity");
				return;
			}

			var effectEntity = EntityManager.CreateEntity($"VisualEffect_{request.SourceKind}_{request.SourceId}");
			var timing = request.TimingOverride ?? VisualEffectTimingProfileResolver.Resolve(recipe.Timing);
			float delay = Math.Max(0f, request.DelaySeconds);
			EntityManager.AddComponent(effectEntity, new ActiveVisualEffect
			{
				RequestId = request.RequestId,
				Recipe = recipe,
				Timing = timing,
				Source = request.Source,
				Target = request.Target,
				SourceAnchor = sourceAnchor,
				TargetAnchor = targetAnchor,
				ImpactAnchor = targetAnchor,
				DirectionSign = targetAnchor.X >= sourceAnchor.X ? 1 : -1,
				IsPreview = request.IsPreview,
				SourceKind = request.SourceKind,
				SourceId = request.SourceId ?? string.Empty,
				ContextId = request.ContextId ?? string.Empty,
				DisplayName = request.DisplayName ?? string.Empty,
				ElapsedSeconds = -delay,
				DelaySeconds = delay,
				DrivesGameplayImpact = request.DrivesGameplayImpact,
				SequenceId = request.SequenceId,
				BeatIndex = request.BeatIndex
			});

			if (delay <= 0f)
			{
				var active = effectEntity.GetComponent<ActiveVisualEffect>();
				PublishStartSfx(active);
			}
		}

		private static void PublishStartSfx(ActiveVisualEffect active)
		{
			if (active == null || active.StartSfxPublished) return;
			active.StartSfxPublished = true;
			if (active.Recipe.StartSfx == SfxTrack.None) return;
			EventManager.Publish(new PlaySfxEvent
			{
				Track = active.Recipe.StartSfx,
				Volume = active.Recipe.StartSfxVolume,
				Pitch = active.Recipe.StartSfxPitch
			});
		}

		private bool TryReserveCapacity(VisualEffectRequested request)
		{
			var active = GetRelevantEntities()
				.Select(e => e.GetComponent<ActiveVisualEffect>())
				.Where(e => e != null)
				.ToList();
			if (active.Count < Math.Max(1, MaxConcurrentEffects)) return true;
			if (request.IsPreview) return false;

			var oldestPreview = GetRelevantEntities()
				.Select(e => (entity: e, effect: e.GetComponent<ActiveVisualEffect>()))
				.Where(pair => pair.effect?.IsPreview == true)
				.OrderBy(pair => pair.effect.ElapsedSeconds)
				.FirstOrDefault();
			if (oldestPreview.entity != null)
			{
				EntityManager.DestroyEntity(oldestPreview.entity.Id);
			}
			return true;
		}

		private void PublishImpact(ActiveVisualEffect active)
		{
			active.ImpactPublished = true;
			if (active.Recipe.ImpactSfx != SfxTrack.None)
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = active.Recipe.ImpactSfx,
					Volume = active.Recipe.ImpactSfxVolume,
					Pitch = active.Recipe.ImpactSfxPitch
				});
			}

			if (active.Recipe.Modules.Contains(VisualEffectModule.Shockwave))
			{
				EventManager.Publish(new ShockwaveEvent
				{
					CenterPx = active.ImpactAnchor,
					DurationSec = 0.45f,
					MaxRadiusPx = 160f * Math.Max(0.1f, active.Recipe.Intensity),
					RippleWidthPx = 24f,
					Strength = 0.012f * Math.Max(0f, active.Recipe.Intensity),
					ChromaticAberrationAmp = 0.006f,
					ChromaticAberrationFreq = 22f,
					ShadingIntensity = 0.18f
				});
			}

			EventManager.Publish(new VisualEffectImpactReached
			{
				RequestId = active.RequestId,
				IsPreview = active.IsPreview
			});

			if (!active.IsPreview
				&& active.SourceKind == VisualEffectSourceKind.EnemyAttack
				&& active.DrivesGameplayImpact)
			{
				EventManager.Publish(new EnemyAttackImpactNow());
			}
		}

		private void PublishCompletion(ActiveVisualEffect active)
		{
			active.CompletionPublished = true;
			EventManager.Publish(new VisualEffectCompleted
			{
				RequestId = active.RequestId,
				IsPreview = active.IsPreview
			});
		}

		private void RejectRequest(VisualEffectRequested request, string reason)
		{
			if (request.IsPreview)
			{
				LoggingService.Append("ModularEffectCoordinatorSystem.RejectPreview", new JsonObject
				{
					["reason"] = reason,
					["sourceKind"] = request.SourceKind.ToString(),
					["sourceId"] = request.SourceId ?? string.Empty
				});
				return;
			}

			LoggingService.Append("ModularEffectCoordinatorSystem.RejectGameplay", new JsonObject
			{
				["reason"] = reason,
				["sourceKind"] = request.SourceKind.ToString(),
				["sourceId"] = request.SourceId ?? string.Empty,
				["contextId"] = request.ContextId ?? string.Empty
			});

			_pendingRejectedGameplayRequests.Add(request);
		}

		private void FlushRejectedGameplayRequests()
		{
			if (_pendingRejectedGameplayRequests.Count == 0) return;
			foreach (var request in _pendingRejectedGameplayRequests.ToList())
			{
				if (request.SourceKind == VisualEffectSourceKind.EnemyAttack && request.DrivesGameplayImpact)
				{
					EventManager.Publish(new EnemyAttackImpactNow());
				}
				EventManager.Publish(new VisualEffectImpactReached { RequestId = request.RequestId, IsPreview = false });
				EventManager.Publish(new VisualEffectCompleted { RequestId = request.RequestId, IsPreview = false });
			}
			_pendingRejectedGameplayRequests.Clear();
		}

		private bool TryResolveAnchor(Entity entity, out Vector2 anchor)
		{
			anchor = Vector2.Zero;
			if (entity == null) return false;
			var portrait = entity.GetComponent<PortraitInfo>();
			if (portrait != null && portrait.LastDrawCenter != Vector2.Zero)
			{
				anchor = portrait.LastDrawCenter;
				return true;
			}

			var transform = entity.GetComponent<Transform>();
			if (transform == null) return false;
			anchor = transform.Position;
			return true;
		}

		private void ClearActiveEffects()
		{
			foreach (var entity in GetRelevantEntities().ToList())
			{
				EntityManager.DestroyEntity(entity.Id);
			}
		}
	}
}
