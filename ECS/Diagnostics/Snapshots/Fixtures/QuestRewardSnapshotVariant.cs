using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures;

public sealed class QuestRewardSnapshotVariant
{
	public DeckRewardOfferSave DeckRewardOffer { get; init; }
	public string FileSlug { get; init; } = "deck-offer";

	public static QuestRewardSnapshotVariant Parse(string[] args)
	{
		var options = new List<DeckRewardOfferOptionSave>();
		for (int i = 0; i < args.Length; i++)
		{
			if (string.Equals(args[i], "--exchange", StringComparison.OrdinalIgnoreCase))
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
		return new QuestRewardSnapshotVariant { DeckRewardOffer = offer, FileSlug = BuildSlug(offer.options) };
	}

	private static void ValidateCardKey(string cardKey)
	{
		if (!RunDeckService.TryParseCardKey(cardKey, out string cardId, out _, out _))
			throw new DisplaySnapshotSetupException($"Invalid card key '{cardKey}'; expected cardId|color or cardId|color|Upgraded");
		if (CardFactory.Create(cardId) == null)
			throw new DisplaySnapshotSetupException($"Unknown card id in card key: '{cardId}'");
	}

	private static string BuildSlug(IEnumerable<DeckRewardOfferOptionSave> options)
	{
		var parts = new List<string> { "deck-offer" };
		foreach (DeckRewardOfferOptionSave option in options ?? Enumerable.Empty<DeckRewardOfferOptionSave>())
		{
			string key = string.Equals(option.kind, DeckRewardOfferKinds.Upgrade, StringComparison.OrdinalIgnoreCase) ? option.upgradedCardKey : option.incomingCardKey;
			parts.Add(string.IsNullOrWhiteSpace(key) ? "empty" : key.Replace("|", "-", StringComparison.Ordinal).Trim().ToLowerInvariant());
		}
		return string.Join("-", parts);
	}
}
