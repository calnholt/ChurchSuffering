#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Cards;

internal static class CardBehaviorHandlers
{
    private static readonly StringId InsufficientStatMessage = new(2001);
    private static readonly StringId InvalidPhaseMessage = new(2002);
    private static readonly StringId MissingRequiredCardMessage = new(2003);
    private static readonly StringId MissingWeaponMessage = new(2004);

    public static void Absolution(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, 10);
        else if (Stage(c, RuleTriggerIds.CardPledged) && c.IsUpgraded) ModifyPlayer(ref c, RuleStatIds.Courage, 2);
    }

    public static void ArkOfTheCovenant(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardDiscardedForCost)) HealPlayer(ref c, c.IsUpgraded ? 3 : 2);
    }

    public static void BatteringBlow(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        if (c.Input.Payment.IsEmpty) ModifyPlayer(ref c, RuleStatIds.Courage, 3);
        Damage(ref c, 6);
    }

    public static void BattleScars(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        if (c.Input.Resources.Scar >= 2) ApplyPlayer(ref c, RuleEffectIds.Vigor, c.IsUpgraded ? 3 : 2);
        Damage(ref c, 7);
    }

    public static void BloodPrice(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardConditionalDamage))
            ConditionalDamage(ref c, Math.Min(c.Input.Resources.Scar * 2, 10));
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            if (c.IsUpgraded) ApplyPlayer(ref c, RuleEffectIds.Scar, 1);
            Damage(ref c, Math.Min((c.Input.Resources.Scar + (c.IsUpgraded ? 1 : 0)) * 2, 10));
        }
    }

    public static void Burn(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        int amount = c.IsUpgraded
            ? ((c.Input.Flags & CardHandlerFlags.Scorched) != 0 ? 3 : 2)
            : (c.Input.Battle.EnemyBurn > 0 ? 2 : 1);
        ApplyPrimary(ref c, RuleEffectIds.Burn, amount);
    }

    public static void CarpeDiem(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        ModifyPlayer(ref c, RuleStatIds.Courage, 5);
        ApplyPlayer(ref c, RuleEffectIds.CarpeDiem, 1);
    }

    public static void Colorless3Block(ref CardHandlerContext c) { }

    public static void Consecrate(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardConditionalDamage))
            ConditionalDamage(ref c, IsPledged(c) ? 2 : 0);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            if (IsPledged(c)) ModifyPlayer(ref c, RuleStatIds.Courage, 1);
            Damage(ref c, 6);
        }
    }

    public static void Courageous(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        ModifyPlayer(ref c, RuleStatIds.Courage, c.IsUpgraded ? 4 : 3);
        c.Append(RuleCommand.RequestEndTurn(Source(c), delayMilliseconds: 100));
    }

    public static void CrimsonRite(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, 3);
        int dealt = Math.Max(0, c.Input.ResolvedDamage);
        if (c.IsUpgraded) ApplyPlayer(ref c, RuleEffectIds.Aegis, dealt);
        else HealPlayer(ref c, dealt);
    }

    public static void Crusade(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        if (IsPledged(c))
        {
            ModifyPlayer(ref c, RuleStatIds.ActionPoints, 1);
            ApplyPlayer(ref c, RuleEffectIds.Might, 2);
        }
        Damage(ref c, c.IsUpgraded ? 3 : 5);
    }

    public static void Curse(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay))
            c.Append(RuleCommand.RemoveEffect(Source(c), Source(c), RuleEffectIds.Cursed));
    }

    public static void Dagger(ref CardHandlerContext c) => CourageAttack(ref c, 2, 2, 2);

    public static void DeusVult(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardValidate))
        {
            bool allowed = c.IsUpgraded || (c.Input.Flags & CardHandlerFlags.WeaponUsedThisAction) != 0;
            Validate(ref c, allowed, RuleRejectionReason.MissingWeaponAttack, MissingWeaponMessage);
        }
        else if (Stage(c, RuleTriggerIds.CardConditionalDamage))
            ConditionalDamage(ref c, c.Input.Resources.Courage);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            ModifyPlayer(ref c, RuleStatIds.Courage, 1);
            Damage(ref c, c.Input.Resources.Courage + 1);
        }
    }

    public static void DivineProtection(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) ApplyPlayer(ref c, RuleEffectIds.Aegis, c.IsUpgraded ? 5 : 4);
    }

    public static void DowseWithHolyWater(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay) && c.Input.Resources.Courage >= 5)
            ApplyPlayer(ref c, RuleEffectIds.Might, c.IsUpgraded ? 4 : 3);
    }

    public static void EmberHarvest(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, c.IsUpgraded ? 8 : 7);
        if (c.Input.Payment.HasScorchedCard) ApplyPlayer(ref c, RuleEffectIds.Might, c.IsUpgraded ? 3 : 2);
    }

    public static void Exaltation(ref CardHandlerContext c)
    {
        int required = c.IsUpgraded ? 2 : 3;
        if (Stage(c, RuleTriggerIds.CardValidate)) ValidateMinimumStat(ref c, RuleStatIds.Courage, c.Input.Resources.Courage, required);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            ModifyPlayer(ref c, RuleStatIds.Courage, -3);
            Damage(ref c, 7);
        }
    }

    public static void Excavate(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardConditionalDamage))
            ConditionalDamage(ref c, c.Input.Battle.CardsMilled >= 2 ? (c.IsUpgraded ? 5 : 3) : 0);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, 9);
    }

    public static void Fervor(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardConditionalDamage))
            ConditionalDamage(ref c, c.Input.Resources.Courage >= 5 ? 3 : 0);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, 6);
    }

    public static void ForgeStrike(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, 7);
        ApplyPlayer(ref c, RuleEffectIds.Might, 2);
    }

    public static void Fury(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        if (c.IsUpgraded)
        {
            ModifyPlayer(ref c, RuleStatIds.Courage, -c.Input.Resources.Courage);
            ApplyPlayer(ref c, RuleEffectIds.Aggression, c.Input.Resources.Courage * 2);
        }
        else
        {
            ApplyPlayer(ref c, RuleEffectIds.Aggression, 1);
            ApplyPlayer(ref c, RuleEffectIds.Aggression, c.Input.Resources.Aggression + 1);
        }
    }

    public static void Graveward(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, 6);
        else if (Stage(c, RuleTriggerIds.CardReactive) && c.Trigger.Kind == RuleTriggerKind.Mill)
            ApplyPlayer(ref c, RuleEffectIds.Aegis, c.IsUpgraded ? 4 : 2);
    }

    public static void HoldTheLine(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolveBlock)) ModifyPlayer(ref c, RuleStatIds.Courage, 1);
    }

    public static void Hammer(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, 3);
        ApplyPlayer(ref c, RuleEffectIds.Vigor, 1);
    }

    public static void HiddenKunai(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolveBlock))
            SpawnKunai(ref c, isUpgraded: c.IsUpgraded, count: 1);
    }

    public static void Impale(ref CardHandlerContext c) => CourageAttack(ref c, c.IsUpgraded ? 2 : 3, 6, 7);

    public static void IncreaseFaith(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) ApplyPlayer(ref c, RuleEffectIds.Power, 1);
        else if (Stage(c, RuleTriggerIds.CardPledged) && c.IsUpgraded) ApplyPlayer(ref c, RuleEffectIds.Aegis, 2);
    }

    public static void IronCovenant(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, c.IsUpgraded ? 21 : 15);
        else if (Stage(c, RuleTriggerIds.CardPledged)) ApplyPlayer(ref c, RuleEffectIds.Vigor, 1);
    }

    public static void Kunai(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        int required = c.IsUpgraded ? 3 : 4;
        if (c.Input.Battle.ActionAttackHits >= required) ApplyPrimary(ref c, RuleEffectIds.Wounded, 1);
        Damage(ref c, 1);
        c.Append(RuleCommand.MutateCard(c.Card, CardMutationKind.SetExhaust, amount: 1));
    }

    public static void Lacerate(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, 4);
        if (c.Input.ResolvedDamage >= 7) ApplyPrimary(ref c, RuleEffectIds.Wounded, 1);
    }

    public static void LitanyOfWrath(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) ApplyPlayer(ref c, RuleEffectIds.Aggression, c.IsUpgraded ? 8 : 3);
    }

    public static void Mantlet(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardDiscardedForCost)) ApplyPlayer(ref c, RuleEffectIds.Aegis, 1);
    }

    public static void MaleficRite(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        int multiplier = c.IsUpgraded ? 3 : 2;
        ApplyPlayer(ref c, RuleEffectIds.Aggression, 4 + c.Input.Battle.CursesRemovedThisClimb * multiplier);
        ApplyRandomCandidate(ref c, RuleEffectIds.Cursed, 1);
    }

    public static void QuickWit(ref CardHandlerContext c)
    {
        int cost = c.IsUpgraded ? 2 : 1;
        if (Stage(c, RuleTriggerIds.CardValidate)) ValidateMinimumStat(ref c, RuleStatIds.Courage, c.Input.Resources.Courage, cost);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            ModifyPlayer(ref c, RuleStatIds.Courage, -cost);
            Damage(ref c, 2);
            Resurrect(ref c, c.IsUpgraded ? 2 : 1);
        }
    }

    public static void RallyTheFaithful(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        ApplyPlayer(ref c, RuleEffectIds.Might, 1);
        if (c.IsUpgraded) ModifyPlayer(ref c, RuleStatIds.Courage, 1);
    }

    public static void RelentlessStrike(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, 9);
        if ((c.Input.Flags & CardHandlerFlags.FirstPlayThisBattle) == 0) return;
        c.Append(RuleCommand.MoveCard(c.Card, c.Input.Deck.Deck, CardZone.Hand, CardZone.DrawPile));
        c.Append(RuleCommand.MutateCard(c.Card, CardMutationKind.ModifyDamage, amount: 4, flags: RuleValueFlags.BattleOnly));
    }

    public static void PierceThrough(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        c.Append(RuleCommand.RemoveEffect(Source(c), c.PrimaryTarget, RuleEffectIds.Guard));
        if (c.IsUpgraded) c.Append(RuleCommand.RemoveEffect(Source(c), c.PrimaryTarget, RuleEffectIds.Armor));
        Damage(ref c, 8);
    }

    public static void PouchOfKunai(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        int minimum = c.IsUpgraded ? 3 : 2;
        int maximum = 4;
        int count = minimum + c.Random.NextInt(maximum - minimum + 1);
        SpawnKunai(ref c, isUpgraded: false, count);
    }

    public static void Purge(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            Damage(ref c, 3);
            return;
        }

        if (!Stage(c, RuleTriggerIds.CardReactive) || c.Trigger.Kind != RuleTriggerKind.Card) return;
        CardTriggerPayload payload = c.Trigger.Payload.Card;
        int removed = 0;
        removed += RemoveTrait(ref c, payload.Card, payload.Traits, RuleCardTraits.Frozen, RuleEffectIds.CardFrozen);
        removed += RemoveTrait(ref c, payload.Card, payload.Traits, RuleCardTraits.Brittle, RuleEffectIds.Brittle);
        removed += RemoveTrait(ref c, payload.Card, payload.Traits, RuleCardTraits.Scorched, RuleEffectIds.Scorched);
        removed += RemoveTrait(ref c, payload.Card, payload.Traits, RuleCardTraits.Thorned, RuleEffectIds.Thorned);
        removed += RemoveTrait(ref c, payload.Card, payload.Traits, RuleCardTraits.Curse, RuleEffectIds.Cursed);
        if (c.IsUpgraded && removed > 0) ApplyPlayer(ref c, RuleEffectIds.Might, removed);
    }

    public static void Ravage(ref CardHandlerContext c)
    {
        int mill = c.IsUpgraded ? 4 : 1;
        if (Stage(c, RuleTriggerIds.CardValidate))
            Validate(ref c, c.Input.Deck.DrawCount >= mill, RuleRejectionReason.InvalidCardCount, MissingRequiredCardMessage);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            c.Append(RuleCommand.RandomCardZone(c.Input.Deck.Deck, CardZone.DrawPile, CardZone.DiscardPile, RandomCardZoneOperation.Mill, mill));
            Damage(ref c, c.IsUpgraded ? 11 : 8);
        }
    }

    public static void RazorStorm(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) ScheduleHits(ref c, c.IsUpgraded ? 3 : 2, 1, 500, 500);
    }

    public static void Reckoning(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, c.IsUpgraded ? 9 : 8);
    }

    public static void Reap(ref CardHandlerContext c)
    {
        bool paidTwoRed = c.Input.Payment.Count == 2 && c.Input.Payment.RedCount == 2;
        if (Stage(c, RuleTriggerIds.CardConditionalDamage)) ConditionalDamage(ref c, paidTwoRed ? 2 : 0);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            Damage(ref c, 8);
            if (c.IsUpgraded && paidTwoRed) ModifyPlayer(ref c, RuleStatIds.Courage, 2);
        }
    }

    public static void RenounceAndHone(ref CardHandlerContext c)
    {
        bool hasPriorPledge = !c.Input.Deck.PreviousTurnPledgedCard.IsNull;
        if (Stage(c, RuleTriggerIds.CardValidate))
            Validate(ref c, hasPriorPledge, RuleRejectionReason.MissingRequiredCard, MissingRequiredCardMessage);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            c.Append(RuleCommand.RemovePledge(c.Input.Deck.PreviousTurnPledgedCard, Player(c), CardZone.DiscardPile, PledgeRemovalReason.DiscardedForCost));
            ApplyPlayer(ref c, RuleEffectIds.Vigor, 2);
            ModifyPlayer(ref c, RuleStatIds.Courage, c.IsUpgraded ? 4 : 2);
        }
    }

    public static void Sacrifice(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        ApplyPlayer(ref c, RuleEffectIds.Scar, 1);
        ModifyPlayer(ref c, RuleStatIds.Temperance, 1);
        Resurrect(ref c, 2);
    }

    public static void SerpentCrush(ref CardHandlerContext c)
    {
        int cost = c.IsUpgraded ? 1 : 2;
        if (Stage(c, RuleTriggerIds.CardValidate)) ValidateMinimumStat(ref c, RuleStatIds.Courage, c.Input.Resources.Courage, cost);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            Damage(ref c, 3);
            ModifyPlayer(ref c, RuleStatIds.Courage, -cost);
            ModifyPlayer(ref c, RuleStatIds.ActionPoints, 1);
            Resurrect(ref c, 1);
        }
    }

    public static void Seize(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardConditionalDamage))
            ConditionalDamage(ref c, c.Input.Phase.Phase == RulePhase.Action && c.Input.Battle.CourageLostThisPhase > 0 ? 2 : 0);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, 2);
    }

    public static void ShieldOfFaith(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) ApplyPlayer(ref c, RuleEffectIds.Aegis, c.IsUpgraded ? 12 : 8);
    }

    public static void Smite(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, 3);
        else if (Stage(c, RuleTriggerIds.CardPledged) && c.IsUpgraded) ModifyPlayer(ref c, RuleStatIds.Temperance, 1);
    }

    public static void Stab(ref CardHandlerContext c) => CourageAttack(ref c, c.IsUpgraded ? 1 : 2, 5, 5);

    public static void SteadfastResolve(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) ApplyPlayer(ref c, RuleEffectIds.Vigor, c.IsUpgraded ? 4 : 1);
    }

    public static void Stalwart(ref CardHandlerContext c)
    {
        int cost = c.IsUpgraded ? 0 : 1;
        if (Stage(c, RuleTriggerIds.CardValidate))
        {
            if (c.Input.Phase.Phase != RulePhase.EnemyAction)
                Validate(ref c, false, RuleRejectionReason.InvalidPhase, InvalidPhaseMessage);
            else ValidateMinimumStat(ref c, RuleStatIds.Courage, c.Input.Resources.Courage, cost);
        }
        else if (Stage(c, RuleTriggerIds.CardResolveBlock)) ModifyPlayer(ref c, RuleStatIds.Courage, -cost);
    }

    public static void SteelTheSpirit(ref CardHandlerContext c)
    {
        int cost = c.IsUpgraded ? 2 : 3;
        if (Stage(c, RuleTriggerIds.CardValidate)) ValidateMinimumStat(ref c, RuleStatIds.Courage, c.Input.Resources.Courage, cost);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            ModifyPlayer(ref c, RuleStatIds.Courage, -cost);
            ApplyPlayer(ref c, RuleEffectIds.Vigor, 2);
        }
    }

    public static void StokedAssault(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardValidate)) Validate(ref c, c.Input.Resources.Vigor >= 2, RuleRejectionReason.InsufficientStat, InsufficientStatMessage);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, c.IsUpgraded ? 5 : 4);
    }

    public static void Strike(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, 3);
        if (c.Random.NextPercent() <= 50) ModifyPlayer(ref c, RuleStatIds.Courage, 2);
    }

    public static void SuddenThrust(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, c.IsUpgraded ? 3 : 2);
        ModifyPlayer(ref c, RuleStatIds.Courage, 1);
    }

    public static void StokeTheFurnace(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        int repeats = Math.Min(3, c.Input.Resources.Courage / 2);
        for (int index = 0; index < repeats; index++)
        {
            ModifyPlayer(ref c, RuleStatIds.Courage, -2);
            ApplyPlayer(ref c, RuleEffectIds.Vigor, 1);
        }
        Damage(ref c, 2);
    }

    public static void Sword(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, 5);
        ModifyPlayer(ref c, RuleStatIds.Courage, 1);
    }

    public static void SwordIntoShield(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        ApplyPlayer(ref c, RuleEffectIds.SwordIntoShield, 1);
        c.Append(RuleCommand.SetCardType(c.Card, RuleCardType.Block));
        c.Append(RuleCommand.MutateCard(c.Card, CardMutationKind.ClearDisplayText));
    }

    public static void TemperTheBlade(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) ApplyPlayer(ref c, RuleEffectIds.Sharpen, 4);
    }

    public static void Tempest(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, 2);
        ModifyPlayer(ref c, RuleStatIds.Temperance, 5);
    }

    public static void Thaw(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        int lost = Math.Max(0, c.Input.Resources.Frostbite);
        if (lost > 0)
        {
            c.Append(RuleCommand.RemoveEffect(Source(c), Player(c), RuleEffectIds.Frostbite));
            ModifyPlayer(ref c, RuleStatIds.Temperance, lost);
            if (c.IsUpgraded) ModifyPlayer(ref c, RuleStatIds.Courage, lost);
        }
        Damage(ref c, 3);
    }

    public static void UnburdenedStrike(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardConditionalDamage)) ConditionalDamage(ref c, c.Input.Payment.IsEmpty ? (c.IsUpgraded ? 4 : 3) : 0);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay)) Damage(ref c, 8);
    }

    public static void VanguardsPromise(ref CardHandlerContext c)
    {
        if (!Stage(c, RuleTriggerIds.CardResolvePlay)) return;
        Damage(ref c, c.IsUpgraded ? 3 : 2);
        if ((c.Input.Flags & CardHandlerFlags.HasPledgedCard) == 0) ApplyRandomCandidate(ref c, RuleEffectIds.Pledged, 1);
    }

    public static void Vindicate(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardConditionalDamage)) ConditionalDamage(ref c, c.Input.Resources.Courage >= 5 ? 7 : 0);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            Damage(ref c, c.IsUpgraded ? 11 : 8);
            ModifyPlayer(ref c, RuleStatIds.Courage, -c.Input.Resources.Courage);
        }
    }

    public static void Whirlwind(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay)) ScheduleHits(ref c, c.IsUpgraded ? 3 : 2, 3, 500, 500);
    }

    public static void ZealousVow(ref CardHandlerContext c)
    {
        if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            if (c.IsUpgraded) c.Append(RuleCommand.Damage(Player(c), TargetHandle.PrimaryEnemy, 1));
            ApplyPlayer(ref c, RuleEffectIds.Aggression, 2);
        }
        else if (Stage(c, RuleTriggerIds.CardPledged)) ApplyPlayer(ref c, RuleEffectIds.Sharpen, 2);
    }

    private static void CourageAttack(ref CardHandlerContext c, int courageCost, int baseDamage, int upgradedDamage)
    {
        if (Stage(c, RuleTriggerIds.CardValidate)) ValidateMinimumStat(ref c, RuleStatIds.Courage, c.Input.Resources.Courage, courageCost);
        else if (Stage(c, RuleTriggerIds.CardResolvePlay))
        {
            ModifyPlayer(ref c, RuleStatIds.Courage, -courageCost);
            Damage(ref c, c.IsUpgraded ? upgradedDamage : baseDamage);
        }
    }

    private static bool Stage(in CardHandlerContext c, TriggerId stage) => c.Stage == stage;
    private static bool IsPledged(in CardHandlerContext c) => (c.Input.Flags & CardHandlerFlags.Pledged) != 0;
    private static TargetHandle Source(in CardHandlerContext c) => TargetHandle.ForEntity(c.Card);
    private static TargetHandle Player(in CardHandlerContext c) => TargetHandle.ForEntity(c.Player);

    private static void Damage(ref CardHandlerContext c, int fallback)
    {
        int amount = c.Input.DerivedDamage != 0 ? c.Input.DerivedDamage : fallback;
        c.Append(RuleCommand.Damage(Source(c), c.PrimaryTarget, amount));
    }

    private static void HealPlayer(ref CardHandlerContext c, int amount) =>
        c.Append(RuleCommand.Heal(Source(c), Player(c), amount));

    private static void ModifyPlayer(ref CardHandlerContext c, StatId stat, int amount) =>
        c.Append(RuleCommand.ModifyStat(Source(c), Player(c), stat, amount));

    private static void ApplyPlayer(ref CardHandlerContext c, EffectId effect, int magnitude) =>
        Apply(ref c, Player(c), effect, magnitude);

    private static void ApplyPrimary(ref CardHandlerContext c, EffectId effect, int magnitude) =>
        Apply(ref c, c.PrimaryTarget, effect, magnitude);

    private static void Apply(ref CardHandlerContext c, TargetHandle target, EffectId effect, int magnitude)
    {
        var spec = new EffectSpec(effect, magnitude, Duration: 0, ConditionSpec.Always, RuleValueFlags.None);
        c.Append(RuleCommand.ApplyEffect(Source(c), target, in spec));
    }

    private static void ConditionalDamage(ref CardHandlerContext c, int amount) =>
        c.Results.ModifyStat(RuleStatIds.AttackAdditionalDamage, amount);

    private static void ValidateMinimumStat(ref CardHandlerContext c, StatId stat, int actual, int required)
    {
        c.Append(RuleCommand.SetRequirement(Player(c), RequirementKind.MinimumStat, required, stat));
        Validate(ref c, actual >= required, RuleRejectionReason.InsufficientStat, InsufficientStatMessage);
    }

    private static void Validate(
        ref CardHandlerContext c,
        bool allowed,
        RuleRejectionReason reason,
        StringId message)
    {
        if (allowed)
        {
            c.Results.Allow();
            return;
        }

        c.Results.Reject(message);
        c.Append(RuleCommand.Reject(Source(c), Player(c), reason, message));
    }

    private static void Resurrect(ref CardHandlerContext c, int count) =>
        c.Append(RuleCommand.RandomCardZone(
            c.Input.Deck.Deck,
            CardZone.DiscardPile,
            CardZone.Hand,
            RandomCardZoneOperation.Resurrect,
            count));

    private static void SpawnKunai(ref CardHandlerContext c, bool isUpgraded, int count) =>
        c.Append(RuleCommand.SpawnCard(
            Source(c), Player(c), c.Input.Deck.Deck, CardId.Kunai,
            CardZone.Hand, RuleCardColor.White, isUpgraded, count));

    private static void ApplyRandomCandidate(ref CardHandlerContext c, EffectId effect, int magnitude)
    {
        if (c.CandidateTargets.IsEmpty) return;
        EntityId chosen = c.CandidateTargets[c.Random.NextInt(c.CandidateTargets.Length)];
        Apply(ref c, TargetHandle.ForEntity(chosen), effect, magnitude);
    }

    private static int RemoveTrait(
        ref CardHandlerContext c,
        EntityId card,
        RuleCardTraits traits,
        RuleCardTraits trait,
        EffectId effect)
    {
        if ((traits & trait) == 0) return 0;
        c.Append(RuleCommand.RemoveEffect(Source(c), TargetHandle.ForEntity(card), effect));
        return 1;
    }

    private static void ScheduleHits(
        ref CardHandlerContext c,
        int count,
        int fallbackDamage,
        int firstDelay,
        int interval)
    {
        int damage = c.Input.DerivedDamage != 0 ? c.Input.DerivedDamage : fallbackDamage;
        for (int index = 0; index < count; index++)
        {
            c.Append(RuleCommand.Schedule(
                Source(c), c.PrimaryTarget, TriggerId.Null, RuleHandlerIds.ResolveDelayedRule,
                firstDelay + index * interval, value1: damage, value2: (int)RuleCommandKind.Damage));
        }
    }
}
