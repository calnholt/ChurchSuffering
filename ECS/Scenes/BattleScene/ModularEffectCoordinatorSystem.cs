using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Input;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Modular Effect Coordinator")]
	public sealed class ModularEffectCoordinatorSystem : Core.System
	{
		private List<VisualEffectRequested> _pendingRejectedGameplayRequests = new();
		private List<VisualEffectRequested> _processingRejectedGameplayRequests = new();
		private readonly List<Entity> _updateEntityBuffer = new();
		private readonly List<Entity> _cleanupEntityBuffer = new();

		[DebugEditable(DisplayName = "Global Intensity Multiplier", Step = 0.01f, Min = 0f, Max = 4f)]
		public float GlobalIntensityMultiplier { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Global Particle Multiplier", Step = 0.01f, Min = 0f, Max = 4f)]
		public float GlobalParticleMultiplier { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Max Concurrent Effects", Step = 1f, Min = 1f, Max = 64f)]
		public int MaxConcurrentEffects { get; set; } = 16;

		public ModularEffectCoordinatorSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<VisualEffectRequested>(OnVisualEffectRequested);
			EventManager.Subscribe<EnemyDamageAppliedEvent>(OnEnemyDamageApplied);
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
			_updateEntityBuffer.Clear();
			_updateEntityBuffer.AddRange(GetRelevantEntities());
			foreach (var entity in _updateEntityBuffer)
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
			if (active.Recipe.StartSfx != SfxTrack.None)
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = active.Recipe.StartSfx,
					Volume = active.Recipe.StartSfxVolume,
					Pitch = active.Recipe.StartSfxPitch
				});
			}

			PublishModuleStartSfx(active);
		}

		private static void PublishModuleStartSfx(ActiveVisualEffect active)
		{
			if (active.Recipe.HasModule(VisualEffectModule.Beam))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.LightSpellCast,
					Volume = 0.5f
				});
			}

			if (active.Recipe.HasModule(VisualEffectModule.SoulSiphon))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.RaySpellCast,
					Volume = 0.5f
				});
			}

			if (active.Recipe.HasModule(VisualEffectModule.Halo))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.HealStinger,
					Volume = 0.5f
				});
			}
		}

		private static void PublishModuleImpactSfx(ActiveVisualEffect active)
		{
			if (active.Recipe.HasModule(VisualEffectModule.ShadowTendrils))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.RaySpellImpact,
					Volume = 0.5f
				});
			}

			if (active.Recipe.HasModule(VisualEffectModule.Bite))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.EatenBite,
					Volume = 0.5f
				});
			}

			if (active.Recipe.HasModule(VisualEffectModule.RockBlast))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.EarthSpell,
					Volume = 0.5f
				});
			}

			if (active.Recipe.HasModule(VisualEffectModule.FrostBurst))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.IceSpellLayer,
					Volume = 0.5f
				});
			}

			if (active.Recipe.HasModule(VisualEffectModule.FlameBurst))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.FireSpellLayer,
					Volume = 0.5f
				});
			}

			if (active.Recipe.HasModule(VisualEffectModule.CrossBloom))
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.BellLowMagical,
					Volume = 0.5f
				});
			}
		}

		private bool TryReserveCapacity(VisualEffectRequested request)
		{
			int activeCount = 0;
			Entity oldestPreviewEntity = null;
			float oldestPreviewElapsed = float.MaxValue;
			foreach (var entity in GetRelevantEntities())
			{
				var effect = entity.GetComponent<ActiveVisualEffect>();
				if (effect == null) continue;
				activeCount++;
				if (effect.IsPreview && effect.ElapsedSeconds < oldestPreviewElapsed)
				{
					oldestPreviewEntity = entity;
					oldestPreviewElapsed = effect.ElapsedSeconds;
				}
			}
			if (activeCount < Math.Max(1, MaxConcurrentEffects)) return true;
			if (request.IsPreview) return false;
			if (oldestPreviewEntity != null)
			{
				EntityManager.DestroyEntity(oldestPreviewEntity.Id);
			}
			return true;
		}

		private void PublishImpact(ActiveVisualEffect active)
		{
			active.ImpactPublished = true;
			bool drivesEnemyDamage = !active.IsPreview
				&& active.SourceKind == VisualEffectSourceKind.EnemyAttack
				&& active.DrivesGameplayImpact;
			if (drivesEnemyDamage)
			{
				EventManager.Publish(new EnemyAttackImpactNow());
			}

			PublishImpactRumble(active);

			if (!active.SuppressImpactSfx && active.Recipe.ImpactSfx != SfxTrack.None)
			{
				EventManager.Publish(new PlaySfxEvent
				{
					Track = active.Recipe.ImpactSfx,
					Volume = active.Recipe.ImpactSfxVolume,
					Pitch = active.Recipe.ImpactSfxPitch
				});
			}

			if (!active.SuppressImpactSfx)
			{
				PublishModuleImpactSfx(active);
			}

			if (active.Recipe.HasModule(VisualEffectModule.Shockwave))
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

		}

		private static void PublishImpactRumble(ActiveVisualEffect active)
		{
			if (active == null || active.IsPreview || active.Recipe == null
				|| active.Recipe.ImpactRumbleProfile == RumbleProfile.None)
			{
				return;
			}

			EventManager.Publish(new RumbleRequested
			{
				Profile = active.Recipe.ImpactRumbleProfile,
				Scale = Math.Max(0f, active.Recipe.ImpactRumbleScale * active.Recipe.Intensity),
				Group = RumbleGroup.Gameplay,
			});
		}

		private void OnEnemyDamageApplied(EnemyDamageAppliedEvent evt)
		{
			if (evt == null || evt.TotalDamage <= 0 || evt.FinalDamage > 0) return;
			ActiveVisualEffect active = null;
			foreach (var entity in GetRelevantEntities())
			{
				var candidate = entity.GetComponent<ActiveVisualEffect>();
				if (candidate == null
					|| candidate.SourceKind != VisualEffectSourceKind.EnemyAttack
					|| !candidate.DrivesGameplayImpact
					|| !candidate.ImpactPublished
					|| candidate.CompletionPublished)
				{
					continue;
				}
				if (active == null || candidate.ElapsedSeconds > active.ElapsedSeconds) active = candidate;
			}
			if (active == null) return;

			active.SuppressImpactSfx = true;
			active.Recipe = active.Recipe.WithModules(
				active.Recipe.Modules.Where(module => module != VisualEffectModule.Shake).ToArray())
				.WithImpactRumble(RumbleProfile.None);

			EventManager.Publish(new VisualEffectRequested
			{
				Recipe = VisualEffectPresets.BlockedAttack(),
				Source = active.Source,
				Target = active.Target,
				SourceKind = VisualEffectSourceKind.EnemyAttack,
				SourceId = $"{active.SourceId}_blocked",
				DisplayName = active.DisplayName,
				DrivesGameplayImpact = false
			});
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
			(_processingRejectedGameplayRequests, _pendingRejectedGameplayRequests) =
				(_pendingRejectedGameplayRequests, _processingRejectedGameplayRequests);
			_pendingRejectedGameplayRequests.Clear();
			foreach (var request in _processingRejectedGameplayRequests)
			{
				if (request.SourceKind == VisualEffectSourceKind.EnemyAttack && request.DrivesGameplayImpact)
				{
					EventManager.Publish(new EnemyAttackImpactNow());
				}
				if (request.Recipe?.ImpactRumbleProfile != RumbleProfile.None)
				{
					EventManager.Publish(new RumbleRequested
					{
						Profile = request.Recipe.ImpactRumbleProfile,
						Scale = Math.Max(0f, request.Recipe.ImpactRumbleScale * request.Recipe.Intensity),
						Group = RumbleGroup.Gameplay,
					});
				}
				EventManager.Publish(new VisualEffectImpactReached { RequestId = request.RequestId, IsPreview = false });
				EventManager.Publish(new VisualEffectCompleted { RequestId = request.RequestId, IsPreview = false });
			}
			_processingRejectedGameplayRequests.Clear();
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
			_cleanupEntityBuffer.Clear();
			_cleanupEntityBuffer.AddRange(GetRelevantEntities());
			foreach (var entity in _cleanupEntityBuffer)
			{
				EntityManager.DestroyEntity(entity.Id);
			}
		}
	}
}
