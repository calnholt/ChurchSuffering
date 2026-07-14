using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Objects.Achievements
{
	/// <summary>Tracks completed climbs, optionally filtered by starting weapon and difficulty.</summary>
	public sealed class ClimbCompletionAchievement : AchievementBase
	{
		private readonly string _requiredWeaponId;
		private readonly RunDifficulty? _requiredDifficulty;
		private readonly int _requiredCompletions;

		public ClimbCompletionAchievement(
			string id,
			string name,
			string description,
			int row,
			int column,
			bool startsVisible,
			string requiredWeaponId = null,
			RunDifficulty? requiredDifficulty = null,
			int requiredCompletions = 1)
		{
			Id = id;
			Name = name;
			Description = description;
			Row = row;
			Column = column;
			StartsVisible = startsVisible;
			Points = 5;
			_requiredWeaponId = requiredWeaponId;
			_requiredDifficulty = requiredDifficulty;
			_requiredCompletions = Math.Max(1, requiredCompletions);
			TargetValue = _requiredCompletions > 1 ? _requiredCompletions : 0;
		}

		public override void RegisterListeners()
		{
			EventManager.Subscribe<ClimbCompletedEvent>(OnClimbCompleted);
		}

		public override void UnregisterListeners()
		{
			EventManager.Unsubscribe<ClimbCompletedEvent>(OnClimbCompleted);
		}

		private void OnClimbCompleted(ClimbCompletedEvent evt)
		{
			if (evt == null) return;
			if (!string.IsNullOrWhiteSpace(_requiredWeaponId)
				&& !string.Equals(evt.StartingWeaponId, _requiredWeaponId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			if (_requiredDifficulty.HasValue && evt.Difficulty != _requiredDifficulty.Value) return;

			IncrementProgress();
			if (!IsCompleted) AchievementManager.SaveProgress();
		}

		protected override void EvaluateCompletion()
		{
			if (GetProgress() >= _requiredCompletions) Complete();
		}
	}

	public static class ClimbAchievementCatalog
	{
		public static IEnumerable<AchievementBase> CreateAll()
		{
			yield return Create("first_ascent", "First Ascent", "Complete your first climb", 1, 4, true);
			yield return Create("veteran_climber", "Veteran Climber", "Complete 5 climbs", 1, 3, false, completions: 5);
			yield return Create("tempered_steel", "Tempered Steel", "Complete a climb with the Sword on Normal difficulty", 0, 4, false, "sword", RunDifficulty.Normal);
			yield return Create("by_the_sword", "By the Sword", "Complete a climb with the Sword on Hard difficulty", 0, 5, false, "sword", RunDifficulty.Hard);
			yield return Create("quick_work", "Quick Work", "Complete a climb with the Dagger on Easy difficulty", 1, 5, false, "dagger", RunDifficulty.Easy);
			yield return Create("knifes_edge", "Knife's Edge", "Complete a climb with the Dagger on Normal difficulty", 1, 6, false, "dagger", RunDifficulty.Normal);
			yield return Create("silent_execution", "Silent Execution", "Complete a climb with the Dagger on Hard difficulty", 1, 7, false, "dagger", RunDifficulty.Hard);
			yield return Create("first_strike", "First Strike", "Complete a climb with the Hammer on Easy difficulty", 2, 4, false, "hammer", RunDifficulty.Easy);
			yield return Create("judgment_falls", "Judgment Falls", "Complete a climb with the Hammer on Normal difficulty", 2, 5, false, "hammer", RunDifficulty.Normal);
			yield return Create("unbreakable_force", "Unbreakable Force", "Complete a climb with the Hammer on Hard difficulty", 2, 6, false, "hammer", RunDifficulty.Hard);
		}

		private static ClimbCompletionAchievement Create(
			string id,
			string name,
			string description,
			int row,
			int column,
			bool startsVisible,
			string weaponId = null,
			RunDifficulty? difficulty = null,
			int completions = 1)
		{
			return new ClimbCompletionAchievement(
				id,
				name,
				description,
				row,
				column,
				startsVisible,
				weaponId,
				difficulty,
				completions);
		}
	}
}
