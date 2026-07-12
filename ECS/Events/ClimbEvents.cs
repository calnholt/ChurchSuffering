using Crusaders30XX.ECS.Data.Save;
using System;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.Events
{
	public class ClimbShopSlotSelectedEvent
	{
		public int SlotIndex { get; set; } = -1;
	}

	public class ClimbEncounterSlotSelectedEvent
	{
		public string SlotId { get; set; } = string.Empty;
	}

	public class ClimbEventSlotSelectedEvent
	{
		public string SlotId { get; set; } = string.Empty;
	}

	public class NarrativeModalContent
	{
		public string Title { get; set; } = string.Empty;
		public string Body { get; set; } = string.Empty;
		public string ConfirmLabel { get; set; } = string.Empty;
	}

	public class NarrativeModalChoiceRequested
	{
		public string ResolutionContextId { get; set; } = string.Empty;
		public int ChoiceIndex { get; set; }
		public bool Handled { get; set; }
	}

	public class DialogueSequenceRequested
	{
		public string DefinitionId { get; set; } = string.Empty;
		public string SegmentId { get; set; } = string.Empty;
		public Guid RequestId { get; set; }
		public bool BackgroundOnly { get; set; }
	}

	public class DialogueSequenceCompleted
	{
		public string DefinitionId { get; set; } = string.Empty;
		public string SegmentId { get; set; } = string.Empty;
		public Guid RequestId { get; set; }
	}

	public static class ClimbEventContextIds
	{
		private const string HazardPrefix = "climb_event/hazard/";
		private const string CharacterPrefix = "climb_event/character/";

		public static string Hazard(string eventSlotId) => $"{HazardPrefix}{eventSlotId ?? string.Empty}";
		public static string Character(string eventSlotId) => $"{CharacterPrefix}{eventSlotId ?? string.Empty}";

		public static bool TryParseHazard(string contextId, out string eventSlotId)
		{
			return TryParse(contextId, HazardPrefix, out eventSlotId);
		}

		public static bool TryParseCharacter(string contextId, out string eventSlotId)
		{
			return TryParse(contextId, CharacterPrefix, out eventSlotId);
		}

		private static bool TryParse(string contextId, string prefix, out string eventSlotId)
		{
			eventSlotId = string.Empty;
			if (string.IsNullOrWhiteSpace(contextId) || !contextId.StartsWith(prefix, StringComparison.Ordinal)) return false;
			eventSlotId = contextId.Substring(prefix.Length);
			return !string.IsNullOrWhiteSpace(eventSlotId);
		}
	}

	public class ClimbPreviewStartedEvent
	{
		public string SourceSlotId { get; set; } = string.Empty;
		public int Amount { get; set; }
		public ClimbResourceSave ProjectedResources { get; set; }
	}

	public class ClimbPreviewClearedEvent
	{
	}

	public class ClimbLoadoutOpenRequestedEvent
	{
	}

	public class ClimbCardUpgradeAnimationRequested
	{
		public string BaseCardKey { get; set; } = string.Empty;
		public string UpgradedCardKey { get; set; } = string.Empty;
	}

	public class ClimbCardMutationAnimationRequested
	{
		public string DeckEntryId { get; set; } = string.Empty;
		public string CardKey { get; set; } = string.Empty;
		public string RestrictionName { get; set; } = string.Empty;
		public List<string> CurrentRestrictionNames { get; set; } = new();
		public bool TransitionToBattleOnComplete { get; set; }
	}

	public class ClimbResourceAcquisitionAnimationRequested
	{
		public ClimbResourceSave Resources { get; set; } = new();
	}

	public class ClimbResourceHeaderPulseRequested
	{
		public ClimbResourceSave Resources { get; set; } = new();
	}
	}
