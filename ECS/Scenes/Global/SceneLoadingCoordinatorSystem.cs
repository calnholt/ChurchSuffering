using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Prepares the next scene over multiple covered transition frames. CPU-only
	/// preparation may eventually move off-thread; ContentManager and graphics work
	/// deliberately remain on the game thread.
	/// </summary>
	public sealed class SceneLoadingCoordinatorSystem : Core.System
	{
		private sealed class PreparationJob
		{
			public string Name { get; init; }
			public Action Execute { get; init; }
		}

		private const double DefaultFrameBudgetMilliseconds = 6.0;
		private readonly ContentManager _content;
		private readonly ImageAssetService _imageAssets;
		private readonly Queue<PreparationJob> _jobs = new();
		private readonly ScenePreparationState _state;

		public double FrameBudgetMilliseconds { get; set; } = DefaultFrameBudgetMilliseconds;

		public SceneLoadingCoordinatorSystem(
			EntityManager entityManager,
			ContentManager content,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_content = content;
			_imageAssets = imageAssets;
			var stateEntity = entityManager.CreateEntity("ScenePreparationState");
			_state = new ScenePreparationState();
			entityManager.AddComponent(stateEntity, _state);
			entityManager.AddComponent(stateEntity, new DontDestroyOnLoad());
			EventManager.Subscribe<SceneTransitionRequested>(OnTransitionRequested, priority: 100);
			EventManager.Subscribe<SceneActivated>(OnSceneActivated);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (_state.Status != ScenePreparationStatus.Preparing) return;

			var frameTimer = Stopwatch.StartNew();
			while (_jobs.Count > 0)
			{
				var job = _jobs.Dequeue();
				_state.CurrentJob = job.Name;
				try
				{
					var jobTimer = Stopwatch.StartNew();
					FrameProfiler.Measure($"ScenePreparation.{job.Name}", job.Execute);
					jobTimer.Stop();
					_state.SlowestJobMilliseconds = Math.Max(
						_state.SlowestJobMilliseconds,
						jobTimer.Elapsed.TotalMilliseconds);
					_state.CompletedJobs++;
				}
				catch (Exception ex)
				{
					_jobs.Clear();
					_state.Status = ScenePreparationStatus.Failed;
					_state.Error = $"{job.Name}: {ex.Message}";
					LoggingService.Append("SceneLoadingCoordinatorSystem.PreparationFailed", new System.Text.Json.Nodes.JsonObject
					{
						["scene"] = _state.TargetScene.ToString(),
						["job"] = job.Name,
						["error"] = ex.Message,
					});
					return;
				}

				if (frameTimer.Elapsed.TotalMilliseconds >= FrameBudgetMilliseconds) break;
			}

			if (_jobs.Count != 0) return;
			_state.Status = ScenePreparationStatus.Ready;
			_state.CurrentJob = string.Empty;
			EventManager.Publish(new ScenePreparationReady
			{
				PreparationId = _state.PreparationId,
				Scene = _state.TargetScene,
			});
		}

		private void OnTransitionRequested(SceneTransitionRequested request)
		{
			_jobs.Clear();
			_state.PreparationId = request.PreparationId;
			_state.TargetScene = request.To;
			_state.Status = ScenePreparationStatus.Preparing;
			_state.CompletedJobs = 0;
			_state.CurrentJob = string.Empty;
			_state.Error = string.Empty;
			_state.SlowestJobMilliseconds = 0;
			_imageAssets.BeginSceneTexturePreparation(request.PreparationId, request.To);

			foreach (string asset in ResolveScopedTextureAssets(request.To))
			{
				string captured = asset;
				AddJob("SceneTexture." + SanitizeName(captured), () =>
					_imageAssets.PrepareSceneTexture(request.PreparationId, captured));
			}

			AddJob("SceneBundle", () => EventManager.Publish(new PrepareSceneEvent
			{
				PreparationId = request.PreparationId,
				Scene = request.To,
			}));

			foreach (string asset in ResolveTextureAssets(request.To))
			{
				string captured = asset;
				AddJob("Texture." + SanitizeName(captured), () => _imageAssets.GetRequiredTexture(captured));
			}

			foreach (string asset in ResolveEffectAssets(request.To))
			{
				string captured = asset;
				AddJob("Effect." + SanitizeName(captured), () => _content.Load<Effect>(captured));
			}

			MusicTrack track = ResolveMusicTrack(request.To);
			if (track != MusicTrack.None)
			{
				AddJob("Music." + track, () => EventManager.Publish(new PrepareMusicTrackEvent { Track = track }));
			}

			_state.TotalJobs = _jobs.Count;
			if (_state.TotalJobs == 0)
			{
				_state.Status = ScenePreparationStatus.Ready;
			}
		}

		private void OnSceneActivated(SceneActivated activated)
		{
			if (activated.PreparationId != _state.PreparationId) return;
			_imageAssets.ActivateSceneTextureScope(activated.PreparationId);
			LoggingService.Append("SceneLoadingCoordinatorSystem.SceneActivated", new System.Text.Json.Nodes.JsonObject
			{
				["scene"] = activated.Scene.ToString(),
				["jobs"] = _state.CompletedJobs,
				["slowestJobMs"] = _state.SlowestJobMilliseconds,
			});
		}

		private void AddJob(string name, Action execute)
		{
			_jobs.Enqueue(new PreparationJob { Name = name, Execute = execute });
		}

		private IEnumerable<string> ResolveTextureAssets(SceneId scene)
		{
			if (scene == SceneId.Battle)
			{
				yield return "card_back";
				yield return "shackles";
				yield return "pledge";
				yield return "active_icon";
				yield break;
			}

			if (scene == SceneId.Climb)
			{
				yield return "Climb_UI/red_gem";
				yield return "Climb_UI/white_gem";
				yield return "Climb_UI/black_gem";
				yield return "Climb_UI/gem_pouch";
			}
		}

		private IEnumerable<string> ResolveScopedTextureAssets(SceneId scene)
		{
			if (scene != SceneId.Battle) yield break;
			var queued = EntityManager.GetEntitiesWithComponent<QueuedEvents>()
				.FirstOrDefault()?.GetComponent<QueuedEvents>();
			string background = BattleLocationAssetService.GetBackgroundAsset(
				queued?.BattleLocation ?? BattleLocation.Desert);
			if (!string.IsNullOrEmpty(background)) yield return background;
		}

		private static IEnumerable<string> ResolveEffectAssets(SceneId scene)
		{
			if (!ShaderRuntimeOptions.ShadersEnabled) yield break;
			if (scene == SceneId.Climb)
			{
				yield return "Shaders/LayeredHoles";
				yield return "Shaders/GaussianBlur";
				yield return "Shaders/ClimbChoiceFilter";
			}
			if (scene == SceneId.Achievement) yield return "Shaders/AchievementBackground";
		}

		private MusicTrack ResolveMusicTrack(SceneId scene)
		{
			if (scene == SceneId.Climb) return MusicTrack.Climb;
			if (scene == SceneId.TitleMenu) return MusicTrack.Title;
			if (scene == SceneId.WayStation) return MusicTrack.WayStation;
			if (scene != SceneId.Battle) return MusicTrack.None;
			var queued = EntityManager.GetEntitiesWithComponent<QueuedEvents>()
				.FirstOrDefault()?.GetComponent<QueuedEvents>();
			return BattleLocationAssetService.GetMusicTrack(queued?.BattleLocation ?? BattleLocation.Desert);
		}

		private static string SanitizeName(string name)
		{
			return name.Replace('/', '.').Replace('\\', '.');
		}
	}
}
