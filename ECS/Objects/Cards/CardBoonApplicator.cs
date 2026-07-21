using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Loadouts;

namespace ChurchSuffering.ECS.Objects.Cards
{
	public static class CardBoonApplicator
	{
		public static void Synchronize(
			EntityManager entityManager,
			Entity cardEntity,
			IReadOnlyList<CardBoonSave> savedBoons)
		{
			var card = cardEntity?.GetComponent<CardData>()?.Card;
			if (card == null) return;

			var target = Normalize(savedBoons);
			var existingComponent = cardEntity.GetComponent<CardBoonComponent>();
			if (target.Count == 0)
			{
				if (existingComponent != null) entityManager?.RemoveComponent<CardBoonComponent>(cardEntity);
				return;
			}
			var current = Normalize((existingComponent?.Boons ?? new List<CardBoonState>())
				.Select(boon => new CardBoonSave { type = boon.Type, amount = boon.Amount })
				.ToList());
			ApplyDeltas(card, current, target);

			var component = existingComponent;
			if (component == null)
			{
				component = new CardBoonComponent();
				entityManager?.AddComponent(cardEntity, component);
			}
			component.Boons = target
				.Select(boon => new CardBoonState { Type = boon.type, Amount = boon.amount })
				.ToList();
		}

		public static void ApplyToDefinition(CardBase card, IReadOnlyList<CardBoonSave> savedBoons)
		{
			if (card == null) return;
			ApplyDeltas(card, Array.Empty<CardBoonSave>(), Normalize(savedBoons));
		}

		public static int GetAmount(IEnumerable<CardBoonSave> boons, string type)
		{
			if (string.IsNullOrWhiteSpace(type)) return 0;
			return (boons ?? Array.Empty<CardBoonSave>())
				.Where(boon => boon != null && string.Equals(boon.type, type, StringComparison.OrdinalIgnoreCase))
				.Sum(boon => Math.Max(0, boon.amount));
		}

		public static List<CardBoonSave> Normalize(IEnumerable<CardBoonSave> boons)
		{
			return (boons ?? Array.Empty<CardBoonSave>())
				.Where(boon => boon != null
					&& CardBoonKinds.All.Contains(boon.type, StringComparer.OrdinalIgnoreCase)
					&& boon.amount > 0)
				.GroupBy(boon => boon.type, StringComparer.OrdinalIgnoreCase)
				.Select(group => new CardBoonSave
				{
					type = CardBoonKinds.All.First(type => string.Equals(type, group.Key, StringComparison.OrdinalIgnoreCase)),
					amount = group.Sum(boon => Math.Max(0, boon.amount)),
				})
				.OrderBy(boon => Array.FindIndex(CardBoonKinds.All, type => string.Equals(type, boon.type, StringComparison.OrdinalIgnoreCase)))
				.ToList();
		}

		private static void ApplyDeltas(
			CardBase card,
			IReadOnlyList<CardBoonSave> current,
			IReadOnlyList<CardBoonSave> target)
		{
			int overchargedDelta = Math.Max(0,
				GetAmount(target, CardBoonKinds.Overcharged) - GetAmount(current, CardBoonKinds.Overcharged));
			if (overchargedDelta > 0)
			{
				card.Damage += overchargedDelta * 5;
				card.Cost ??= new List<string>();
				for (int i = 0; i < overchargedDelta; i++) card.Cost.Add("Any");
			}

			int honedDelta = Math.Max(0,
				GetAmount(target, CardBoonKinds.Honed) - GetAmount(current, CardBoonKinds.Honed));
			if (honedDelta > 0) card.Damage += honedDelta;

			int guardedDelta = Math.Max(0,
				GetAmount(target, CardBoonKinds.Guarded) - GetAmount(current, CardBoonKinds.Guarded));
			if (guardedDelta > 0) card.Block += guardedDelta;

			if (GetAmount(target, CardBoonKinds.Quickened) > 0)
			{
				card.IsFreeAction = true;
			}

			// Wild is intentionally last so costs added by authored upgrades or Overcharged
			// remain Any whenever the card object is recreated.
			if (GetAmount(target, CardBoonKinds.Wild) > 0 && card.Cost != null)
			{
				for (int i = 0; i < card.Cost.Count; i++) card.Cost[i] = "Any";
			}
		}
	}
}
