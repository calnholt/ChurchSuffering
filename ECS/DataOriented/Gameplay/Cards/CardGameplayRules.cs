#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Cards;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Cards;

public readonly record struct CardPlayValidation(
    bool Allowed,
    CardPaymentFailure Failure,
    AlternatePlayResult AlternatePlay,
    int RequiredPaymentCount);

public static class CardCostRules
{
    public static bool IsColorQualified(World world, EntityId card, CardCostColor requirement)
    {
        if (!world.IsAlive(card) || !world.TryGet(card, out CardData data))
            return false;
        if (requirement == CardCostColor.Any)
            return true;
        if (world.Has<Colorless>(card))
            return false;
        return requirement switch
        {
            CardCostColor.Red => data.RuntimeColor == RuleCardColor.Red,
            CardCostColor.White => data.RuntimeColor == RuleCardColor.White,
            CardCostColor.Black => data.RuntimeColor == RuleCardColor.Black,
            _ => false,
        };
    }

    public static bool CanSatisfy(
        World world,
        in CardData card,
        ReadOnlySpan<EntityId> candidates,
        EntityId excludedCard = default,
        EntityId expectedDeck = default)
    {
        Span<EntityId> selected = stackalloc EntityId[4];
        return TrySelectPayment(
            world,
            in card,
            candidates,
            excludedCard,
            expectedDeck,
            selected,
            out _);
    }

    /// <summary>
    /// Selects the first valid payment in candidate order, backtracking when an Any requirement
    /// would otherwise consume a card required by a later colored slot.
    /// </summary>
    public static bool TrySelectPayment(
        World world,
        in CardData card,
        ReadOnlySpan<EntityId> candidates,
        EntityId excludedCard,
        EntityId expectedDeck,
        Span<EntityId> destination,
        out int selectedCount)
    {
        selectedCount = 0;
        if (card.CostCount > 4 || candidates.Length < card.CostCount || candidates.Length > 128)
            return false;
        if (destination.Length < card.CostCount)
            throw new ArgumentException("The payment destination is smaller than the card cost.", nameof(destination));
        if (card.CostCount == 0)
            return true;

        if (expectedDeck.IsNull && world.TryGet(excludedCard, out CardData playedCard))
            expectedDeck = playedCard.Deck;

        Span<byte> used = stackalloc byte[candidates.Length];
        if (!Match(world, in card, candidates, used, destination, 0, excludedCard, expectedDeck))
            return false;
        selectedCount = card.CostCount;
        return true;
    }

    public static CardPaymentSnapshot BuildSnapshot(World world, ReadOnlySpan<EntityId> payment)
    {
        var red = 0;
        var white = 0;
        var black = 0;
        var any = 0;
        var scorched = 0;
        for (var index = 0; index < payment.Length; index++)
        {
            EntityId card = payment[index];
            if (!world.TryGet(card, out CardData data))
                continue;
            if (world.Has<Colorless>(card)) any++;
            else if (data.RuntimeColor == RuleCardColor.Red) red++;
            else if (data.RuntimeColor == RuleCardColor.White) white++;
            else if (data.RuntimeColor == RuleCardColor.Black) black++;
            if (world.Has<Scorched>(card)) scorched++;
        }
        return new CardPaymentSnapshot(payment.Length, red, white, black, any, scorched);
    }

    public static bool IsEligiblePaymentCard(
        World world,
        EntityId card,
        EntityId cardBeingPlayed,
        EntityId expectedDeck = default)
    {
        if (card == cardBeingPlayed || !world.IsAlive(card) || !world.TryGet(card, out CardData data))
            return false;
        if (!expectedDeck.IsNull &&
            (data.Deck != expectedDeck ||
             !world.TryGet(card, out CardZoneLocation location) ||
             location.Deck != expectedDeck ||
             location.Zone != CardZone.Hand))
            return false;
        return !data.IsWeapon && !world.Has<Pledge>(card);
    }

    private static bool Match(
        World world,
        in CardData card,
        ReadOnlySpan<EntityId> candidates,
        Span<byte> used,
        Span<EntityId> destination,
        int requirementIndex,
        EntityId excludedCard,
        EntityId expectedDeck)
    {
        if (requirementIndex >= card.CostCount)
            return true;
        CardCostColor requirement = CostAt(in card, requirementIndex);
        for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            EntityId candidate = candidates[candidateIndex];
            if (used[candidateIndex] != 0 ||
                AlreadySelected(candidates, used, candidate) ||
                !IsEligiblePaymentCard(world, candidate, excludedCard, expectedDeck))
                continue;
            if (!IsColorQualified(world, candidate, requirement))
                continue;
            used[candidateIndex] = 1;
            destination[requirementIndex] = candidate;
            if (Match(world, in card, candidates, used, destination, requirementIndex + 1, excludedCard, expectedDeck))
                return true;
            used[candidateIndex] = 0;
            destination[requirementIndex] = EntityId.Null;
        }
        return false;
    }

    private static bool AlreadySelected(ReadOnlySpan<EntityId> candidates, ReadOnlySpan<byte> used, EntityId candidate)
    {
        for (var index = 0; index < candidates.Length; index++)
            if (used[index] != 0 && candidates[index] == candidate) return true;
        return false;
    }

    private static CardCostColor CostAt(in CardData card, int index) => index switch
    {
        0 => card.Cost0,
        1 => card.Cost1,
        2 => card.Cost2,
        3 => card.Cost3,
        _ => CardCostColor.Any,
    };
}

public static class PledgeRules
{
    public static PledgeFailure Evaluate(
        World world,
        EntityId deck,
        EntityId card,
        in PledgeAvailabilityState state)
    {
        if (state.Enabled == 0) return PledgeFailure.Disabled;
        if (state.PledgedThisActionPhase != 0) return PledgeFailure.AlreadyPledgedThisAction;
        if (CardZoneOperations.IndexOf(world, deck, CardZone.Hand, card) < 0) return PledgeFailure.NotInHand;
        if (world.Has<Pledge>(card)) return PledgeFailure.AlreadyPledged;

        ref Deck deckData = ref world.Get<Deck>(deck);
        ReadOnlySpan<HandCard> hand = world.GetDynamicBuffer<HandCard>(deckData.Hand).AsReadOnlySpan();
        for (var index = 0; index < hand.Length; index++)
            if (world.Has<Pledge>(hand[index].Card)) return PledgeFailure.ExistingPledge;

        ref CardData data = ref world.Get<CardData>(card);
        if (world.Has<Sealed>(card)) return PledgeFailure.Sealed;
        if (data.IsWeapon) return PledgeFailure.Weapon;
        if (data.Type == RuleCardType.Block) return PledgeFailure.Block;
        if (data.Type == RuleCardType.Relic) return PledgeFailure.Relic;
        if (data.IsToken) return PledgeFailure.Token;
        return PledgeFailure.None;
    }
}

public static class CardLifecycleDispatcher
{
    public static bool Dispatch(
        World world,
        in CardHandlerInput input,
        RuleCommandBuffer commands,
        ReadOnlySpan<RuleFact> facts,
        ReadOnlySpan<EntityId> paymentCards,
        ReadOnlySpan<EntityId> candidateTargets,
        Span<RuleHandlerResult> resultStorage,
        ref RuleResultWriterState resultState,
        ref RuleRandomState randomState,
        out RuleValidationDecision validation)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(commands);
        var context = new CardHandlerContext(
            world.AsReadOnly(),
            commands.Writer,
            in input,
            facts,
            paymentCards,
            candidateTargets,
            resultStorage,
            ref resultState,
            ref randomState);
        bool dispatched = GeneratedCardCatalog.Dispatch(input.Definition, ref context);
        validation = context.Results.Validation;
        return dispatched;
    }
}

public static class CardPlayRules
{
    public static CardPlayValidation Validate(
        World world,
        EntityId player,
        EntityId deck,
        EntityId card,
        ReadOnlySpan<EntityId> paymentCandidates,
        ReadOnlySpan<AlternatePlayResult> alternateResults)
    {
        if (CardZoneOperations.IndexOf(world, deck, CardZone.Hand, card) < 0)
            return Failure(CardPaymentFailure.CardNotInHand);
        ref CardData data = ref world.Get<CardData>(card);
        if (world.Has<Pledge>(card) && world.Get<Pledge>(card).CanPlay == 0)
            return Failure(CardPaymentFailure.PledgeLocked);
        if (world.Has<Sealed>(card))
            return Failure(CardPaymentFailure.Sealed);

        AlternatePlayResult alternate = SelectAlternate(alternateResults, player);
        bool alternateAllows = alternate.IsApplicable && alternate.Allowed;
        if (data.Type == RuleCardType.Block && !alternateAllows)
            return Failure(CardPaymentFailure.CardCannotBePlayed);
        if (!data.IsFreeAction && !alternate.FreeAction && world.Get<Player>(player).ActionPoints <= 0)
            return Failure(CardPaymentFailure.InsufficientActionPoints);
        if (!CardCostRules.CanSatisfy(world, in data, paymentCandidates, card, deck))
            return Failure(CardPaymentFailure.InsufficientPaymentCards);
        return new CardPlayValidation(true, CardPaymentFailure.None, alternate, data.CostCount);
    }

    public static int ApplyModifiers(
        int baseValue,
        EntityId owner,
        ReadOnlySpan<CardStatModifierResult> modifiers)
    {
        Span<ProviderCandidateResult> candidates = modifiers.Length <= 64
            ? stackalloc ProviderCandidateResult[modifiers.Length]
            : throw new ArgumentOutOfRangeException(nameof(modifiers), "At most 64 providers may modify one card stat.");
        for (var index = 0; index < modifiers.Length; index++)
            candidates[index] = new ProviderCandidateResult(modifiers[index].Source, modifiers[index].Applies);
        Span<ProviderCandidateResult> ordered = stackalloc ProviderCandidateResult[candidates.Length];
        int count = ProviderPrecedence.CollectApplicableInOrder(candidates, owner, ordered);
        int result = baseValue;
        for (var orderedIndex = 0; orderedIndex < count; orderedIndex++)
        {
            EntityId source = ordered[orderedIndex].Source.Entity;
            for (var sourceIndex = 0; sourceIndex < modifiers.Length; sourceIndex++)
                if (modifiers[sourceIndex].Source.Entity == source && modifiers[sourceIndex].IsApplicable)
                    result += modifiers[sourceIndex].Delta;
        }
        return result;
    }

    private static AlternatePlayResult SelectAlternate(ReadOnlySpan<AlternatePlayResult> results, EntityId owner)
    {
        if (results.Length == 0) return default;
        Span<ProviderCandidateResult> candidates = results.Length <= 64
            ? stackalloc ProviderCandidateResult[results.Length]
            : throw new ArgumentOutOfRangeException(nameof(results), "At most 64 alternate-play providers are supported.");
        for (var index = 0; index < results.Length; index++)
            candidates[index] = new ProviderCandidateResult(results[index].Source, results[index].Applies);
        if (!ProviderPrecedence.TrySelectFirst(candidates, owner, out ProviderCandidateResult selected))
            return default;
        for (var index = 0; index < results.Length; index++)
            if (results[index].Source.Entity == selected.Source.Entity) return results[index];
        return default;
    }

    private static CardPlayValidation Failure(CardPaymentFailure failure) => new(false, failure, default, 0);
}
