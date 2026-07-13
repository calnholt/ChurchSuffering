using System;
using System.Reflection;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class AchievementSnapshotFixture : IDisplaySnapshotFixture
	{
		private readonly AchievementSnapshotVariant _variant;
		private AchievementSceneSystem _scene;
		private AchievementDescriptionDisplaySystem _description;
		private AchievementTitleDisplaySystem _title;
		private AchievementBackgroundDisplaySystem _background;
		private AchievementGridDisplaySystem _grid;
		private AchievementMeterDisplaySystem _meter;

		public AchievementSnapshotFixture(AchievementSnapshotVariant variant)
		{
			_variant = variant;
		}

		public string Id => _variant == AchievementSnapshotVariant.Detail
			? "achievement-detail"
			: "achievement-overview";
		public int WarmupFrames => 4;
		public string OutputFileName => Id;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			ConfigureState();
			ctx.SceneEntity.GetComponent<SceneState>().Current = SceneId.Achievement;
			EventManager.Publish(new LoadSceneEvent { Scene = SceneId.Achievement, PreviousScene = SceneId.Snapshot });

			_scene = ctx.World.GetSystem<AchievementSceneSystem>();
			_description = ctx.World.GetSystem<AchievementDescriptionDisplaySystem>();
			_title = ctx.World.GetSystem<AchievementTitleDisplaySystem>();
			_background = ctx.World.GetSystem<AchievementBackgroundDisplaySystem>();
			_grid = ctx.World.GetSystem<AchievementGridDisplaySystem>();
			_meter = ctx.World.GetSystem<AchievementMeterDisplaySystem>();
			if (_scene == null || _description == null || _title == null || _background == null || _grid == null || _meter == null)
				throw new DisplaySnapshotSetupException("Achievement scene systems were not registered.");

			_background.TimeSpeed = 0f;
			_background.RotationSpeed = 0f;
			_grid.HoverScale = 1f;
			_grid.ExclamationPulseSpeed = 0f;
			_meter.AnimationSpeed = 1000f;

			var settle = new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
			_title.Update(settle);
			if (_variant == AchievementSnapshotVariant.Detail)
			{
				_description.ShowImmediatelyForSnapshot("frozen_but_unbroken");
			}
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			foreach (var entity in ctx.World.EntityManager.GetEntitiesWithComponent<AchievementGridItem>())
			{
				var ui = entity.GetComponent<UIElement>();
				if (ui != null) ui.IsHovered = false;
			}
			if (_variant == AchievementSnapshotVariant.Detail)
			{
				_description.ShowImmediatelyForSnapshot("frozen_but_unbroken");
			}
			_scene.Draw();
		}

		private static void ConfigureState()
		{
			var save = SaveCache.GetAll();
			save.collection ??= new PlayerCollectionSave();
			var collection = save.collection;
			collection.totalPoints = 44;
			collection.pendingClimbPoints = 7;

			foreach (var achievement in AchievementManager.GetAll())
			{
				var progress = GetProgress(achievement);
				progress.CurrentValue = 0;
				progress.IsCompleted = false;
				progress.State = AchievementState.Hidden;
			}

			Set("slayer", AchievementState.CompleteSeen, 100, true);
			Set("skeleton_slayer", AchievementState.CompleteUnseen, 25, true);
			Set("first_victory", AchievementState.CompleteSeen, 1, true);
			Set("just_getting_started", AchievementState.Visible, 6, false);
			Set("card_player", AchievementState.Visible, 64, false);
			Set("red_card_apprentice", AchievementState.Visible, 38, false);
			Set("bold_investment", AchievementState.Visible, 7, false);
			Set("living_on_the_edge", AchievementState.Visible, 4, false);
		}

		private static void Set(string id, AchievementState state, int current, bool completed)
		{
			var achievement = AchievementManager.GetAchievement(id);
			if (achievement == null) return;
			var progress = GetProgress(achievement);
			progress.State = state;
			progress.CurrentValue = current;
			progress.IsCompleted = completed;
		}

		private static AchievementProgress GetProgress(AchievementBase achievement)
		{
			var property = typeof(AchievementBase).GetProperty("Progress", BindingFlags.NonPublic | BindingFlags.Instance);
			return property?.GetValue(achievement) as AchievementProgress
				?? throw new DisplaySnapshotSetupException($"Achievement '{achievement.Id}' has no progress state.");
		}
	}
}
