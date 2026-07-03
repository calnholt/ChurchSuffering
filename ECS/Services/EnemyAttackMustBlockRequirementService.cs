using System.Text.RegularExpressions;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Services
{
	public static class EnemyAttackMustBlockRequirementService
	{
		private static readonly Regex MustBlockTextRegex = new(
			@"This attack must be blocked with (?:at least|exactly) \d+ cards?/equipment(?: if possible)?\.",
			RegexOptions.Compiled);

		public enum RequirementType
		{
			AtLeast,
			Exactly
		}

		public readonly struct MustBlockRequirement
		{
			public MustBlockRequirement(int threshold, RequirementType type)
			{
				Threshold = threshold;
				Type = type;
			}

			public int Threshold { get; }
			public RequirementType Type { get; }
		}

		public static bool NormalizeIfImpossible(EntityManager entityManager, PlannedAttack plannedAttack)
		{
			if (plannedAttack?.AttackDefinition == null) return false;
			if (!TryGetRequirement(plannedAttack.AttackDefinition.ConditionType, out var requirement)) return false;

			int eligibleBlockers = EnemyBlockerEligibilityService.CountEligibleBlockers(entityManager, plannedAttack);
			if (eligibleBlockers >= requirement.Threshold) return false;

			plannedAttack.AttackDefinition.ConditionType = ConditionType.None;
			plannedAttack.AttackDefinition.Text = StripMustBlockRequirementText(plannedAttack.AttackDefinition.Text);
			return true;
		}

		public static bool TryGetRequirement(ConditionType conditionType, out MustBlockRequirement requirement)
		{
			switch (conditionType)
			{
				case ConditionType.MustBeBlockedByAtLeast1Card:
					requirement = new MustBlockRequirement(1, RequirementType.AtLeast);
					return true;
				case ConditionType.MustBeBlockedByAtLeast2Cards:
					requirement = new MustBlockRequirement(2, RequirementType.AtLeast);
					return true;
				case ConditionType.MustBeBlockedByExactly1Card:
					requirement = new MustBlockRequirement(1, RequirementType.Exactly);
					return true;
				case ConditionType.MustBeBlockedByExactly2Cards:
					requirement = new MustBlockRequirement(2, RequirementType.Exactly);
					return true;
				default:
					requirement = default;
					return false;
			}
		}

		public static string StripMustBlockRequirementText(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return string.Empty;

			string normalized = text.Replace("\r\n", "\n");
			string stripped = MustBlockTextRegex.Replace(normalized, string.Empty);
			stripped = Regex.Replace(stripped, @"[ \t]+\n", "\n");
			stripped = Regex.Replace(stripped, @"\n{3,}", "\n\n");
			return stripped.Trim();
		}
	}
}
