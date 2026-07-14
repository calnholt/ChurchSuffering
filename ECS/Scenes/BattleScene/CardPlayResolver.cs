using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	internal enum CardPlayMode
	{
		Normal,
		AlternateAttack,
	}

	internal enum CardPlayRejection
	{
		None,
		WrongPhase,
		IsRelic,
		BlockWithoutAlternate,
		Pledged,
		Silenced,
		NoActionPoints,
		CanPlayFalse,
		CostUnsatisfiable,
	}

	internal enum CardPaymentDecision
	{
		None,
		AutoPay,
		ChooseCostCards,
		SelectOneCard,
	}

	internal sealed class CardPlayContext
	{
		public CardPlayContext(
			Entity card,
			CardBase definition,
			SubPhase phase,
			int actionPoints,
			int vigorStacks,
			bool costsAlreadyPaid,
			bool hasPledge,
			bool pledgeAllowsPlay,
			bool isSilenced,
			bool cardSpecificCanPlay,
			AlternateCardPlayProfile alternateProfile,
			IReadOnlyList<Entity> paymentPool)
		{
			Card = card;
			Definition = definition;
			Phase = phase;
			ActionPoints = actionPoints;
			VigorStacks = vigorStacks;
			CostsAlreadyPaid = costsAlreadyPaid;
			HasPledge = hasPledge;
			PledgeAllowsPlay = pledgeAllowsPlay;
			IsSilenced = isSilenced;
			CardSpecificCanPlay = cardSpecificCanPlay;
			AlternateProfile = alternateProfile;
			PaymentPool = Array.AsReadOnly(paymentPool?.ToArray() ?? Array.Empty<Entity>());
		}

		public Entity Card { get; }
		public CardBase Definition { get; }
		public SubPhase Phase { get; }
		public int ActionPoints { get; }
		public int VigorStacks { get; }
		public bool CostsAlreadyPaid { get; }
		public bool HasPledge { get; }
		public bool PledgeAllowsPlay { get; }
		public bool IsSilenced { get; }
		public bool CardSpecificCanPlay { get; }
		public AlternateCardPlayProfile AlternateProfile { get; }
		public IReadOnlyList<Entity> PaymentPool { get; }
	}

	internal sealed class CardPlayPlan
	{
		public CardPlayPlan(
			CardPlayRejection rejection,
			CardPaymentDecision paymentDecision,
			CardPlayMode mode,
			bool isFreeAction,
			IReadOnlyList<string> requiredCosts,
			IReadOnlyList<Entity> autoPayment)
		{
			Rejection = rejection;
			PaymentDecision = paymentDecision;
			Mode = mode;
			IsFreeAction = isFreeAction;
			RequiredCosts = Array.AsReadOnly(requiredCosts?.ToArray() ?? Array.Empty<string>());
			AutoPayment = Array.AsReadOnly(autoPayment?.ToArray() ?? Array.Empty<Entity>());
		}

		public bool IsPlayable => Rejection == CardPlayRejection.None;
		public CardPlayRejection Rejection { get; }
		public CardPaymentDecision PaymentDecision { get; }
		public CardPlayMode Mode { get; }
		public bool IsFreeAction { get; }
		public IReadOnlyList<string> RequiredCosts { get; }
		public IReadOnlyList<Entity> AutoPayment { get; }
	}

	internal sealed class CardCostSolutionAnalysis
	{
		public CardCostSolutionAnalysis(int solutionCount, IReadOnlyList<Entity> firstSolution)
		{
			SolutionCount = solutionCount;
			FirstSolution = Array.AsReadOnly(firstSolution?.ToArray() ?? Array.Empty<Entity>());
		}

		public int SolutionCount { get; }
		public IReadOnlyList<Entity> FirstSolution { get; }
	}

	internal static class CardPlayResolver
	{
		private const string SelectOneCardFromHand = "SelectOneCardFromHand";

		public static CardPlayPlan Resolve(CardPlayContext context)
		{
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (context.Card == null) throw new ArgumentException("A card entity is required.", nameof(context));
			if (context.Definition == null) throw new ArgumentException("A card definition is required.", nameof(context));

			var mode = context.AlternateProfile?.TreatsAsAttack == true
				? CardPlayMode.AlternateAttack
				: CardPlayMode.Normal;
			bool isFreeAction = context.Definition.IsFreeAction
				|| context.AlternateProfile?.IsFreeAction == true;
			var effectiveCosts = VigorService.GetEffectiveCost(context.Definition, context.VigorStacks);

			CardPlayPlan Reject(CardPlayRejection rejection) =>
				new(rejection, CardPaymentDecision.None, mode, isFreeAction, effectiveCosts, Array.Empty<Entity>());

			if (context.Phase != SubPhase.Action)
				return Reject(CardPlayRejection.WrongPhase);
			if (context.Definition.Type == CardType.Relic)
				return Reject(CardPlayRejection.IsRelic);
			if (context.Definition.Type == CardType.Block && context.AlternateProfile?.AllowsPlay != true)
				return Reject(CardPlayRejection.BlockWithoutAlternate);
			if (context.HasPledge && !context.PledgeAllowsPlay)
				return Reject(CardPlayRejection.Pledged);
			if (context.HasPledge && context.IsSilenced)
				return Reject(CardPlayRejection.Silenced);
			if (!isFreeAction && context.ActionPoints <= 0)
				return Reject(CardPlayRejection.NoActionPoints);

			bool skipBlockCanPlay = context.Definition.Type == CardType.Block
				&& context.AlternateProfile?.AllowsPlay == true;
			if (!skipBlockCanPlay && !context.CardSpecificCanPlay)
				return Reject(CardPlayRejection.CanPlayFalse);

			if (context.CostsAlreadyPaid)
				return new CardPlayPlan(
					CardPlayRejection.None,
					CardPaymentDecision.None,
					mode,
					isFreeAction,
					effectiveCosts,
					Array.Empty<Entity>());

			if (string.Equals(context.Definition.SpecialAction, SelectOneCardFromHand, StringComparison.Ordinal))
			{
				return new CardPlayPlan(
					CardPlayRejection.None,
					CardPaymentDecision.SelectOneCard,
					mode,
					isFreeAction,
					new[] { "Any" },
					Array.Empty<Entity>());
			}

			if (effectiveCosts.Count == 0)
			{
				return new CardPlayPlan(
					CardPlayRejection.None,
					CardPaymentDecision.None,
					mode,
					isFreeAction,
					effectiveCosts,
					Array.Empty<Entity>());
			}

			var candidates = context.PaymentPool.Where(card => card != context.Card).ToArray();
			var analysis = AnalyzeCostSolutions(effectiveCosts, candidates);
			if (analysis.SolutionCount == 0)
				return Reject(CardPlayRejection.CostUnsatisfiable);

			var paymentDecision = analysis.SolutionCount == 1
				? CardPaymentDecision.AutoPay
				: CardPaymentDecision.ChooseCostCards;
			return new CardPlayPlan(
				CardPlayRejection.None,
				paymentDecision,
				mode,
				isFreeAction,
				effectiveCosts,
				paymentDecision == CardPaymentDecision.AutoPay
					? analysis.FirstSolution
					: Array.Empty<Entity>());
		}

		public static bool IsEligiblePaymentCard(Entity card)
		{
			var data = card?.GetComponent<CardData>();
			return data?.Card != null
				&& data.Color != CardData.CardColor.Yellow
				&& data.Card.CanDiscardForCost
				&& !card.HasComponent<Pledge>();
		}

		public static CardCostSolutionAnalysis AnalyzeCostSolutions(
			IReadOnlyList<string> requiredCosts,
			IReadOnlyList<Entity> paymentPool,
			int solutionCap = 2)
		{
			if (solutionCap < 1) throw new ArgumentOutOfRangeException(nameof(solutionCap));

			var costs = requiredCosts?.ToArray() ?? Array.Empty<string>();
			if (costs.Length == 0)
				return new CardCostSolutionAnalysis(1, Array.Empty<Entity>());

			var candidates = (paymentPool ?? Array.Empty<Entity>())
				.Where(IsEligiblePaymentCard)
				.ToArray();
			int solutionCount = 0;
			Entity[] firstSolution = Array.Empty<Entity>();
			var seenSolutions = new HashSet<string>(StringComparer.Ordinal);
			var usedCards = new HashSet<Entity>();
			var currentSolution = new List<Entity>();

			void Search(int costIndex)
			{
				if (solutionCount >= solutionCap) return;
				if (costIndex == costs.Length)
				{
					string key = string.Join(",", currentSolution
						.Select(entity => entity.Id)
						.OrderBy(id => id));
					if (!seenSolutions.Add(key)) return;

					if (solutionCount == 0)
						firstSolution = currentSolution.ToArray();
					solutionCount++;
					return;
				}

				string cost = costs[costIndex];
				foreach (var candidate in candidates)
				{
					if (usedCards.Contains(candidate)) continue;
					if (!CardColorQualificationService.IsEligibleForCost(candidate, cost)) continue;

					usedCards.Add(candidate);
					currentSolution.Add(candidate);
					Search(costIndex + 1);
					currentSolution.RemoveAt(currentSolution.Count - 1);
					usedCards.Remove(candidate);

					if (solutionCount >= solutionCap) return;
				}
			}

			Search(0);
			return new CardCostSolutionAnalysis(solutionCount, firstSolution);
		}
	}
}
