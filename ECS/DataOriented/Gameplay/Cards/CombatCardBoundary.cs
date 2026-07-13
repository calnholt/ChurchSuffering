#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Content.Equipment;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Cards;

/// <summary>
/// Card-owned implementation of the combat/card command boundary. Reads are compact snapshots;
/// all card-state writes remain on this side of the boundary.
/// </summary>
public sealed class CombatCardBoundary : ICombatCardBoundary
{
    private readonly World world;
    private readonly CardGameplayEventHub events;

    public CombatCardBoundary(World world, CardGameplayEventHub events)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public CombatCardFacts ReadFacts(EntityId deck, EntityId player)
    {
        if (!world.IsAlive(deck) || !world.Has<Deck>(deck)) return default;
        int frozen = 0;
        int red = 0;
        int white = 0;
        int black = 0;
        int colorless = 0;
        int handCount = CardZoneOperations.Count(world, deck, CardZone.Hand);
        for (var index = 0; index < handCount; index++)
        {
            EntityId card = CardZoneOperations.At(world, deck, CardZone.Hand, index);
            if (world.Has<Frozen>(card)) frozen++;
            if (!IsEligibleCardBlocker(card)) continue;
            if (world.Has<Colorless>(card)) colorless++;
            else
            {
                switch (world.Get<CardData>(card).RuntimeColor)
                {
                    case RuleCardColor.Red: red++; break;
                    case RuleCardColor.White: white++; break;
                    case RuleCardColor.Black: black++; break;
                    default: colorless++; break;
                }
            }
        }

        int equipmentRed = 0;
        int equipmentWhite = 0;
        int equipmentBlack = 0;
        foreach (QueryChunk<EquippedEquipment> chunk in world.Query<EquippedEquipment>())
        {
            foreach (int row in chunk.Rows)
            {
                EntityId entity = chunk.Entities[row];
                ref readonly EquippedEquipment equipment = ref chunk.Component1[row];
                if (equipment.Active == 0 || equipment.Owner != player) continue;
                if (world.TryGet(entity, out EquipmentZone zone) && zone.Kind != EquipmentZoneKind.Equipped) continue;
                ref readonly EquipmentDefinition definition = ref GeneratedEquipmentCatalog.GetDefinition(equipment.Definition);
                switch (definition.Color)
                {
                    case RuleCardColor.Red: equipmentRed++; break;
                    case RuleCardColor.White: equipmentWhite++; break;
                    case RuleCardColor.Black: equipmentBlack++; break;
                }
            }
        }

        return new CombatCardFacts(
            frozen, red, white, black, colorless,
            equipmentRed, equipmentWhite, equipmentBlack);
    }

    public int CopyCandidates(EntityId deck, CombatCardCandidateKind kind, Span<EntityId> destination)
    {
        if (!world.IsAlive(deck) || !world.Has<Deck>(deck) || destination.IsEmpty) return 0;
        CardZone zone = kind switch
        {
            CombatCardCandidateKind.Hand => CardZone.Hand,
            CombatCardCandidateKind.TopOfDrawPile => CardZone.DrawPile,
            _ => CardZone.None,
        };
        if (zone == CardZone.None) return 0;
        int count = CardZoneOperations.Count(world, deck, zone);
        if (kind == CombatCardCandidateKind.TopOfDrawPile)
        {
            if (count == 0) return 0;
            EntityId top = CardZoneOperations.At(world, deck, zone, 0);
            if (world.Get<CardData>(top).IsWeapon) return 0;
            destination[0] = top;
            return 1;
        }

        int written = 0;
        for (var index = 0; index < count && written < destination.Length; index++)
        {
            EntityId card = CardZoneOperations.At(world, deck, zone, index);
            if (world.Get<CardData>(card).IsWeapon) continue;
            destination[written++] = card;
        }
        return written;
    }

    public void Execute(in CombatCardCommand command, CommandBuffer structuralCommands)
    {
        switch (command.Kind)
        {
            case CombatCardCommandKind.ApplyEffect:
                ApplyEffect(command.Card, command.Effect, command.Amount, structuralCommands);
                break;
            case CombatCardCommandKind.RemoveEffect:
                RemoveEffect(command.Card, command.Effect, structuralCommands);
                break;
            case CombatCardCommandKind.Draw:
                DrawCards(command.Deck, command.Amount);
                break;
            case CombatCardCommandKind.Mill:
                Mill(command.Deck, command.Amount);
                break;
            case CombatCardCommandKind.ResolveMarkedDiscard:
                ResolveMarkedDiscard(command.Deck, command.ConditionSucceeded != 0, structuralCommands);
                break;
        }
    }

    private bool IsEligibleCardBlocker(EntityId card) =>
        world.IsAlive(card) && world.TryGet(card, out CardData data) && !data.IsWeapon &&
        !world.Has<FilteredFromHand>(card) && !world.Has<CannotBlockThisAttack>(card);

    private void DrawCards(EntityId deck, int requested)
    {
        int drawn = 0;
        int attempts = Math.Max(0, requested);
        while (drawn < attempts)
        {
            if (CardZoneOperations.Count(world, deck, CardZone.DrawPile) == 0)
            {
                events.DrawPileEmpty.Publish(new DrawPileEmptyEvent(deck));
                break;
            }
            EntityId card = CardZoneOperations.At(world, deck, CardZone.DrawPile, 0);
            if (world.Get<CardData>(card).IsWeapon)
            {
                CardZoneOperations.Move(world, deck, card, CardZone.DrawPile, CardZone.DiscardPile, -1, CardMoveReason.Draw, events);
                continue;
            }
            CardZoneOperations.Move(world, deck, card, CardZone.DrawPile, CardZone.Hand, -1, CardMoveReason.Draw, events);
            drawn++;
        }
        events.CardsDrawn.Publish(new CardsDrawnEvent(deck, requested, drawn));
    }

    private void Mill(EntityId deck, int requested)
    {
        for (var index = 0; index < Math.Max(0, requested); index++)
        {
            if (CardZoneOperations.Count(world, deck, CardZone.DrawPile) == 0) break;
            EntityId card = CardZoneOperations.At(world, deck, CardZone.DrawPile, 0);
            if (!CardZoneOperations.Move(world, deck, card, CardZone.DrawPile, CardZone.DiscardPile, -1, CardMoveReason.Mill, events)) break;
            world.Get<Deck>(deck).CardsMilled++;
            events.TopCardRemovedForMill.Publish(new TopCardRemovedForMillEvent(deck, card));
        }
    }

    private void ResolveMarkedDiscard(EntityId deck, bool discard, CommandBuffer structuralCommands)
    {
        for (int index = CardZoneOperations.Count(world, deck, CardZone.Hand) - 1; index >= 0; index--)
        {
            EntityId card = CardZoneOperations.At(world, deck, CardZone.Hand, index);
            if (!world.Has<MarkedForSpecificDiscard>(card)) continue;
            structuralCommands.Remove<MarkedForSpecificDiscard>(card);
            if (discard)
                CardZoneOperations.Move(world, deck, card, CardZone.Hand, CardZone.DiscardPile, -1, CardMoveReason.RuleCommand, events);
        }
    }

    private void ApplyEffect(EntityId card, EffectId effect, int amount, CommandBuffer structuralCommands)
    {
        if (!world.IsAlive(card) || !world.Has<CardData>(card) || amount <= 0) return;
        if (effect == RuleEffectIds.Brittle && !world.Has<Brittle>(card)) structuralCommands.AddTag<Brittle>(card);
        else if (effect == RuleEffectIds.CardFrozen && !world.Has<Frozen>(card)) structuralCommands.AddTag<Frozen>(card);
        else if (effect == RuleEffectIds.Colorless && !world.Has<Colorless>(card)) structuralCommands.AddTag<Colorless>(card);
        else if (effect == RuleEffectIds.MarkedForSpecificDiscard && !world.Has<MarkedForSpecificDiscard>(card)) structuralCommands.AddTag<MarkedForSpecificDiscard>(card);
        else if (effect == RuleEffectIds.Recoil)
        {
            if (world.TryGet(card, out Recoil recoil)) { recoil.Stacks += amount; structuralCommands.Set(card, recoil); }
            else structuralCommands.Add(card, new Recoil { Stacks = amount });
        }
        else if (effect == RuleEffectIds.Sealed)
        {
            if (world.TryGet(card, out Sealed sealedCard)) { sealedCard.Seals += amount; structuralCommands.Set(card, sealedCard); }
            else structuralCommands.Add(card, new Sealed { Seals = amount });
        }
    }

    private void RemoveEffect(EntityId card, EffectId effect, CommandBuffer structuralCommands)
    {
        if (!world.IsAlive(card)) return;
        if (effect == RuleEffectIds.Brittle && world.Has<Brittle>(card)) structuralCommands.Remove<Brittle>(card);
        else if (effect == RuleEffectIds.CardFrozen && world.Has<Frozen>(card)) structuralCommands.Remove<Frozen>(card);
        else if (effect == RuleEffectIds.Colorless && world.Has<Colorless>(card)) structuralCommands.Remove<Colorless>(card);
        else if (effect == RuleEffectIds.MarkedForSpecificDiscard && world.Has<MarkedForSpecificDiscard>(card)) structuralCommands.Remove<MarkedForSpecificDiscard>(card);
        else if (effect == RuleEffectIds.Recoil && world.Has<Recoil>(card)) structuralCommands.Remove<Recoil>(card);
        else if (effect == RuleEffectIds.Sealed && world.Has<Sealed>(card)) structuralCommands.Remove<Sealed>(card);
    }
}
