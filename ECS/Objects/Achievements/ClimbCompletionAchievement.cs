using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
{
	/// <summary>Tracks completed climbs, optionally filtered by starting weapon and minimum Penance.</summary>
	public sealed class ClimbCompletionAchievement : AchievementBase
	{
		private readonly string _requiredWeaponId;
		private readonly int? _requiredPenanceLevel;
		private readonly int _requiredCompletions;

		public ClimbCompletionAchievement(
			string id,
			string name,
			string description,
			int row,
			int column,
			bool startsVisible,
			string requiredWeaponId = null,
			int? requiredPenanceLevel = null,
			int requiredCompletions = 1)
		{
			Id = id;
			Name = name;
			Description = description;
			Row = row;
			Column = column;
			StartsVisible = startsVisible;
			_requiredWeaponId = requiredWeaponId;
			_requiredPenanceLevel = requiredPenanceLevel;
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
			if (_requiredPenanceLevel.HasValue && evt.PenanceLevel < _requiredPenanceLevel.Value) return;

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
			foreach (var achievement in CreateWeaponMilestones("sword", "Sword", 0, 3)) yield return achievement;
			foreach (var achievement in CreateWeaponMilestones("dagger", "Dagger", 1, 5)) yield return achievement;
			foreach (var achievement in CreateWeaponMilestones("hammer", "Hammer", 2, 3)) yield return achievement;
		}

		private static IEnumerable<AchievementBase> CreateWeaponMilestones(
			string weaponId,
			string weaponName,
			int row,
			int firstColumn)
		{
			int[] levels = { 0, 6, 12, 18, 24 };
			for (int index = 0; index < levels.Length; index++)
			{
				int level = levels[index];
				string numeral = ToRoman(level);
				yield return Create(
					$"{weaponId}_penance_{level}",
					$"{weaponName} Penance {numeral}",
					$"Complete a climb with the {weaponName} at Penance {numeral}",
					row,
					firstColumn + index,
					false,
					weaponId,
					level);
			}
		}

		private static string ToRoman(int value)
		{
			if (value <= 0) return "0";
			var numerals = new (int Value, string Text)[]
			{
				(10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
			};
			string result = string.Empty;
			foreach (var numeral in numerals)
			{
				while (value >= numeral.Value)
				{
					result += numeral.Text;
					value -= numeral.Value;
				}
			}
			return result;
		}

		private static ClimbCompletionAchievement Create(
			string id,
			string name,
			string description,
			int row,
			int column,
			bool startsVisible,
			string weaponId = null,
			int? penanceLevel = null,
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
				penanceLevel,
				completions);
		}
	}
}
