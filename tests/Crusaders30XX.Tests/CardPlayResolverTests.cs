using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardPlayResolverTests
{
	[Fact]
	public void Empty_cost_has_one_trivial_solution_regardless_of_pool()
	{
		var entityManager = new EntityManager();
		var card = CreateCard(entityManager, CardData.CardColor.Red);

		var analysis = CardPlayResolver.AnalyzeCostSolutions([], [card]);

		Assert.Equal(1, analysis.SolutionCount);
		Assert.Empty(analysis.FirstSolution);
	}

	[Fact]
	public void Solver_returns_zero_one_or_many_capped_at_two()
	{
		var entityManager = new EntityManager();
		var redOne = CreateCard(entityManager, CardData.CardColor.Red);
		var redTwo = CreateCard(entityManager, CardData.CardColor.Red);
		var white = CreateCard(entityManager, CardData.CardColor.White);

		Assert.Equal(0, CardPlayResolver.AnalyzeCostSolutions(["Black"], [redOne, redTwo, white]).SolutionCount);
		Assert.Equal(1, CardPlayResolver.AnalyzeCostSolutions(["White"], [redOne, redTwo, white]).SolutionCount);
		Assert.Equal(2, CardPlayResolver.AnalyzeCostSolutions(["Red"], [redOne, redTwo, white]).SolutionCount);
	}

	[Fact]
	public void Duplicate_assignments_for_the_same_entity_set_count_once()
	{
		var entityManager = new EntityManager();
		var redOne = CreateCard(entityManager, CardData.CardColor.Red);
		var redTwo = CreateCard(entityManager, CardData.CardColor.Red);

		var analysis = CardPlayResolver.AnalyzeCostSolutions(["Red", "Any"], [redOne, redTwo]);

		Assert.Equal(1, analysis.SolutionCount);
		Assert.Equal([redOne.Id, redTwo.Id], analysis.FirstSolution.Select(card => card.Id).OrderBy(id => id));
	}

	[Fact]
	public void Solver_handles_the_largest_current_five_pip_cost()
	{
		var entityManager = new EntityManager();
		var red = CreateCard(entityManager, CardData.CardColor.Red);
		var black = CreateCard(entityManager, CardData.CardColor.Black);
		var whiteOne = CreateCard(entityManager, CardData.CardColor.White);
		var whiteTwo = CreateCard(entityManager, CardData.CardColor.White);
		var whiteThree = CreateCard(entityManager, CardData.CardColor.White);

		var analysis = CardPlayResolver.AnalyzeCostSolutions(
			["Red", "Black", "Any", "Any", "Any"],
			[red, black, whiteOne, whiteTwo, whiteThree]);

		Assert.Equal(1, analysis.SolutionCount);
		Assert.Equal(5, analysis.FirstSolution.Count);
	}

	[Fact]
	public void Payment_eligibility_excludes_invalid_yellow_weapon_token_and_pledged_cards()
	{
		var entityManager = new EntityManager();
		var valid = CreateCard(entityManager, CardData.CardColor.Red);
		var yellow = CreateCard(entityManager, CardData.CardColor.Yellow);
		var weapon = CreateCard(entityManager, CardData.CardColor.Red, new CardBase { CardId = "weapon", IsWeapon = true });
		var token = CreateCard(entityManager, CardData.CardColor.Red, new CardBase { CardId = "token", IsToken = true });
		var pledged = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(pledged, new Pledge());
		var malformed = entityManager.CreateEntity("MalformedCard");
		entityManager.AddComponent(malformed, new CardData { Owner = malformed, Color = CardData.CardColor.Red });

		Assert.True(CardPlayResolver.IsEligiblePaymentCard(valid));
		Assert.All([yellow, weapon, token, pledged, malformed], card =>
			Assert.False(CardPlayResolver.IsEligiblePaymentCard(card)));
		Assert.Equal(
			1,
			CardPlayResolver.AnalyzeCostSolutions(
				["Red"],
				[valid, yellow, weapon, token, pledged, malformed]).SolutionCount);
	}

	[Fact]
	public void Colorless_card_pays_any_but_not_a_specific_color()
	{
		var entityManager = new EntityManager();
		var colorless = CreateCard(entityManager, CardData.CardColor.Red);
		entityManager.AddComponent(colorless, new Colorless());

		Assert.Equal(1, CardPlayResolver.AnalyzeCostSolutions(["Any"], [colorless]).SolutionCount);
		Assert.Equal(0, CardPlayResolver.AnalyzeCostSolutions(["Red"], [colorless]).SolutionCount);
	}

	[Fact]
	public void Resolve_applies_vigor_and_excludes_the_card_being_played()
	{
		var entityManager = new EntityManager();
		var definition = new CardBase { CardId = "costly", Cost = ["Red", "Any"] };
		var cardToPlay = CreateCard(entityManager, CardData.CardColor.Red, definition);
		var redPayment = CreateCard(entityManager, CardData.CardColor.Red);

		var plan = CardPlayResolver.Resolve(Context(
			cardToPlay,
			definition,
			vigorStacks: 1,
			paymentPool: [cardToPlay, redPayment]));

		Assert.True(plan.IsPlayable);
		Assert.Equal(CardPaymentDecision.AutoPay, plan.PaymentDecision);
		Assert.Equal(["Red"], plan.RequiredCosts);
		Assert.Equal(redPayment, Assert.Single(plan.AutoPayment));
	}

	[Fact]
	public void Resolve_reports_rejections_in_runtime_precedence_order()
	{
		var entityManager = new EntityManager();
		var relic = new CardBase { CardId = "relic", Type = CardType.Relic };
		var card = CreateCard(entityManager, CardData.CardColor.Red, relic);

		var wrongPhase = CardPlayResolver.Resolve(Context(
			card,
			relic,
			phase: SubPhase.Block,
			actionPoints: 0,
			hasPledge: true,
			pledgeAllowsPlay: false,
			isSilenced: true,
			cardSpecificCanPlay: false));
		Assert.Equal(CardPlayRejection.WrongPhase, wrongPhase.Rejection);

		var relicRejection = CardPlayResolver.Resolve(Context(
			card,
			relic,
			actionPoints: 0,
			hasPledge: true,
			pledgeAllowsPlay: false));
		Assert.Equal(CardPlayRejection.IsRelic, relicRejection.Rejection);
	}

	[Theory]
	[InlineData(true, false, false, 1, true, (int)CardPlayRejection.Pledged)]
	[InlineData(true, true, true, 1, true, (int)CardPlayRejection.Silenced)]
	[InlineData(false, true, false, 0, true, (int)CardPlayRejection.NoActionPoints)]
	[InlineData(false, true, false, 1, false, (int)CardPlayRejection.CanPlayFalse)]
	public void Resolve_rejects_pledge_silence_ap_and_card_specific_failures(
		bool hasPledge,
		bool pledgeAllowsPlay,
		bool isSilenced,
		int actionPoints,
		bool cardSpecificCanPlay,
		int expected)
	{
		var entityManager = new EntityManager();
		var definition = new CardBase { CardId = "card" };
		var card = CreateCard(entityManager, CardData.CardColor.Red, definition);

		var plan = CardPlayResolver.Resolve(Context(
			card,
			definition,
			actionPoints: actionPoints,
			hasPledge: hasPledge,
			pledgeAllowsPlay: pledgeAllowsPlay,
			isSilenced: isSilenced,
			cardSpecificCanPlay: cardSpecificCanPlay));

		Assert.Equal((CardPlayRejection)expected, plan.Rejection);
	}

	[Fact]
	public void Alternate_block_play_is_allowed_as_a_free_attack()
	{
		var entityManager = new EntityManager();
		var definition = new CardBase { CardId = "block", Type = CardType.Block };
		var card = CreateCard(entityManager, CardData.CardColor.White, definition);
		var profile = new AlternateCardPlayProfile
		{
			AllowsPlay = true,
			IsFreeAction = true,
			TreatsAsAttack = true,
			AttackDamage = 3,
		};

		var plan = CardPlayResolver.Resolve(Context(
			card,
			definition,
			actionPoints: 0,
			cardSpecificCanPlay: false,
			alternateProfile: profile));

		Assert.True(plan.IsPlayable);
		Assert.True(plan.IsFreeAction);
		Assert.Equal(CardPlayMode.AlternateAttack, plan.Mode);
	}

	[Fact]
	public void Paid_costs_bypass_payment_solver_but_keep_legality_gates()
	{
		var entityManager = new EntityManager();
		var definition = new CardBase { CardId = "costly", Cost = ["Black"] };
		var card = CreateCard(entityManager, CardData.CardColor.Red, definition);

		var paidPlan = CardPlayResolver.Resolve(Context(
			card,
			definition,
			costsAlreadyPaid: true,
			paymentPool: []));
		var unpaidPlan = CardPlayResolver.Resolve(Context(
			card,
			definition,
			paymentPool: []));

		Assert.True(paidPlan.IsPlayable);
		Assert.Equal(CardPaymentDecision.None, paidPlan.PaymentDecision);
		Assert.Equal(CardPlayRejection.CostUnsatisfiable, unpaidPlan.Rejection);
	}

	[Fact]
	public void Legacy_select_one_action_routes_to_its_overlay_decision()
	{
		var entityManager = new EntityManager();
		var definition = new CardBase
		{
			CardId = "select",
			SpecialAction = "SelectOneCardFromHand",
		};
		var card = CreateCard(entityManager, CardData.CardColor.White, definition);

		var plan = CardPlayResolver.Resolve(Context(card, definition));

		Assert.True(plan.IsPlayable);
		Assert.Equal(CardPaymentDecision.SelectOneCard, plan.PaymentDecision);
		Assert.Equal(["Any"], plan.RequiredCosts);
	}

	private static CardPlayContext Context(
		Entity card,
		CardBase definition,
		SubPhase phase = SubPhase.Action,
		int actionPoints = 1,
		int vigorStacks = 0,
		bool costsAlreadyPaid = false,
		bool hasPledge = false,
		bool pledgeAllowsPlay = true,
		bool isSilenced = false,
		bool cardSpecificCanPlay = true,
		AlternateCardPlayProfile alternateProfile = null,
		IReadOnlyList<Entity> paymentPool = null)
	{
		return new CardPlayContext(
			card,
			definition,
			phase,
			actionPoints,
			vigorStacks,
			costsAlreadyPaid,
			hasPledge,
			pledgeAllowsPlay,
			isSilenced,
			cardSpecificCanPlay,
			alternateProfile,
			paymentPool ?? Array.Empty<Entity>());
	}

	private static Entity CreateCard(
		EntityManager entityManager,
		CardData.CardColor color,
		CardBase definition = null)
	{
		var card = entityManager.CreateEntity("Card");
		entityManager.AddComponent(card, new CardData
		{
			Owner = card,
			Card = definition ?? new CardBase { CardId = $"card_{card.Id}" },
			Color = color,
		});
		return card;
	}
}
