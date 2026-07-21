using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures;

public sealed class QuestRewardSnapshotVariant
{
	public DeckRewardOfferSave DeckRewardOffer { get; init; }
	public string FileSlug { get; init; } = "deck-offer";
	public QuestRewardPresentationPhase PresentationPhase { get; init; } = QuestRewardPresentationPhase.Visible;
	public float PresentationElapsedSeconds { get; init; }
	public int SelectedOptionIndex { get; init; } = -1;

	public static QuestRewardSnapshotVariant Parse(string[] args)
	{
		var options = new List<DeckRewardOfferOptionSave>();
		QuestRewardPresentationPhase presentationPhase = QuestRewardPresentationPhase.Visible;
		for (int i = 0; i < args.Length; i++)
		{
			if (string.Equals(args[i], "--presentation", StringComparison.OrdinalIgnoreCase))
			{
				if (i + 1 >= args.Length)
					throw new DisplaySnapshotSetupException("Invalid --presentation value; expected entering, visible, claiming, or skipping");
				presentationPhase = ParsePresentationPhase(args[++i]);
			}
			else if (string.Equals(args[i], "--exchange", StringComparison.OrdinalIgnoreCase))
			{
				if (i + 2 >= args.Length) throw new DisplaySnapshotSetupException("Invalid --exchange value; expected outgoingKey incomingKey");
				string outgoing = args[i + 1];
				string incoming = args[i + 2];
				ValidateCardKey(outgoing);
				ValidateCardKey(incoming);
				options.Add(new DeckRewardOfferOptionSave { kind = DeckRewardOfferKinds.Exchange, loadoutIndex = options.Count, outgoingCardKey = outgoing, incomingCardKey = incoming });
				i += 2;
			}
			else if (string.Equals(args[i], "--upgrade", StringComparison.OrdinalIgnoreCase))
			{
				if (i + 1 >= args.Length) throw new DisplaySnapshotSetupException("Invalid --upgrade value; expected cardId|color");
				string outgoing = args[i + 1];
				ValidateCardKey(outgoing);
				string upgraded = RunDeckService.BuildUpgradedCardKey(outgoing);
				if (string.IsNullOrWhiteSpace(upgraded)) throw new DisplaySnapshotSetupException($"Invalid --upgrade key '{outgoing}'");
				options.Add(new DeckRewardOfferOptionSave { kind = DeckRewardOfferKinds.Upgrade, loadoutIndex = options.Count, outgoingCardKey = outgoing, upgradedCardKey = upgraded });
				i++;
			}
			else
			{
				throw new DisplaySnapshotSetupException($"Unknown argument: '{args[i]}'");
			}
		}

		if (options.Count == 0)
		{
			options.Add(new DeckRewardOfferOptionSave { kind = DeckRewardOfferKinds.Exchange, loadoutIndex = 0, outgoingCardKey = "strike|white", incomingCardKey = "smite|red" });
			options.Add(new DeckRewardOfferOptionSave { kind = DeckRewardOfferKinds.Exchange, loadoutIndex = 1, outgoingCardKey = "reckoning|white", incomingCardKey = "unburdened_strike|black" });
			options.Add(new DeckRewardOfferOptionSave { kind = DeckRewardOfferKinds.Upgrade, loadoutIndex = 2, outgoingCardKey = "smite|white", upgradedCardKey = "smite|white|Upgraded" });
		}

		var offer = new DeckRewardOfferSave { options = options.Take(3).ToList() };
		return new QuestRewardSnapshotVariant
		{
			DeckRewardOffer = offer,
			FileSlug = BuildSlug(offer.options, presentationPhase),
			PresentationPhase = presentationPhase,
			PresentationElapsedSeconds = GetPresentationElapsedSeconds(presentationPhase),
			SelectedOptionIndex = presentationPhase == QuestRewardPresentationPhase.Claiming ? 1 : -1,
		};
	}

	private static QuestRewardPresentationPhase ParsePresentationPhase(string value)
	{
		if (string.Equals(value, "entering", StringComparison.OrdinalIgnoreCase)) return QuestRewardPresentationPhase.Entering;
		if (string.Equals(value, "visible", StringComparison.OrdinalIgnoreCase)) return QuestRewardPresentationPhase.Visible;
		if (string.Equals(value, "claiming", StringComparison.OrdinalIgnoreCase)) return QuestRewardPresentationPhase.Claiming;
		if (string.Equals(value, "skipping", StringComparison.OrdinalIgnoreCase)) return QuestRewardPresentationPhase.Skipping;
		throw new DisplaySnapshotSetupException($"Invalid --presentation value '{value}'; expected entering, visible, claiming, or skipping");
	}

	private static float GetPresentationElapsedSeconds(QuestRewardPresentationPhase phase) => phase switch
	{
		QuestRewardPresentationPhase.Entering => 0.50f,
		QuestRewardPresentationPhase.Claiming => 0.25f,
		QuestRewardPresentationPhase.Skipping => 0.50f,
		_ => 0f,
	};

	private static void ValidateCardKey(string cardKey)
	{
		if (!RunDeckService.TryParseCardKey(cardKey, out string cardId, out _, out _))
			throw new DisplaySnapshotSetupException($"Invalid card key '{cardKey}'; expected cardId|color or cardId|color|Upgraded");
		if (CardFactory.Create(cardId) == null)
			throw new DisplaySnapshotSetupException($"Unknown card id in card key: '{cardId}'");
	}

	private static string BuildSlug(
		IEnumerable<DeckRewardOfferOptionSave> options,
		QuestRewardPresentationPhase presentationPhase)
	{
		var parts = new List<string> { "deck-offer" };
		foreach (DeckRewardOfferOptionSave option in options ?? Enumerable.Empty<DeckRewardOfferOptionSave>())
		{
			string key = string.Equals(option.kind, DeckRewardOfferKinds.Upgrade, StringComparison.OrdinalIgnoreCase) ? option.upgradedCardKey : option.incomingCardKey;
			parts.Add(string.IsNullOrWhiteSpace(key) ? "empty" : key.Replace("|", "-", StringComparison.Ordinal).Trim().ToLowerInvariant());
		}
		if (presentationPhase != QuestRewardPresentationPhase.Visible)
			parts.Add(presentationPhase.ToString().ToLowerInvariant());
		return string.Join("-", parts);
	}
}
