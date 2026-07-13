#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

public static class MedalProviderRules
{
    public static AlternatePlayResult GetAlternatePlay(ProviderSource source, in AlternatePlayQuery query)
    {
        bool applies = source.Definition == MedalId.StGeorge &&
            query.Phase == RulePhase.Action &&
            (query.Traits & RuleCardTraits.Block) != 0 &&
            (query.Traits & (RuleCardTraits.Weapon | RuleCardTraits.Token)) == 0;
        return new AlternatePlayResult(
            source, applies ? (byte)1 : (byte)0, applies ? (byte)1 : (byte)0,
            applies ? (byte)1 : (byte)0, applies ? (byte)1 : (byte)0, applies ? 3 : 0);
    }

    public static CardStatModifierResult GetCardStatModifier(ProviderSource source, in CardStatQuery query)
    {
        bool christopher = source.Definition == MedalId.StChristopher &&
            query.Kind == CardStatKind.Block &&
            (query.Traits & RuleCardTraits.Brittle) != 0;
        bool lawrence = source.Definition == MedalId.StLawrence &&
            query.Kind == CardStatKind.Damage &&
            query.Mode == CardStatQueryMode.Resolution &&
            (query.Traits & RuleCardTraits.Scorched) != 0 &&
            query.PaymentCardCount > 0;
        return christopher
            ? new CardStatModifierResult(source, RuleStatIds.CardBlock, 1, 1)
            : lawrence
                ? new CardStatModifierResult(source, RuleStatIds.AttackDamage, query.PaymentCardCount, 1)
                : default;
    }

    public static bool TryBuildReplacement(
        ProviderSource source,
        in ReplacementQuery query,
        ref ReplacementPlanWriter writer)
    {
        if (source.Definition != MedalId.StOlaf ||
            query.Kind != RuleReplacementKind.EffectThresholdDamage ||
            query.Effect != RuleEffectIds.Frostbite ||
            query.OriginalTarget != source.EquippedOwner)
            return false;

        writer.MarkHandled(source);
        if (query.HasEligiblePrimaryEnemy)
        {
            var action = new ReplacementAction(
                ReplacementActionKind.ModifyHp,
                query.OriginalTarget,
                query.PrimaryEnemy,
                StatId.Null,
                EffectId.Null,
                RuleDamageKind.Effect,
                Amount: -3,
                RuleValueFlags.None);
            writer.TryAppend(in action);
        }
        return true;
    }
}
