#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Medals;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Content.EquipmentMedals;

public sealed class MedalProviderParityTests
{
    [Fact]
    public void George_matches_legacy_alternate_play_eligibility_and_profile()
    {
        ProviderSource george = Source(MedalId.StGeorge, entityIndex: 7);
        var eligible = new AlternatePlayQuery(
            george.EquippedOwner, new EntityId(40, 1), CardId.ShieldOfFaith,
            RulePhase.Action, RuleCardTraits.Block);
        AlternatePlayResult result = StGeorgeMedalModule.GetAlternatePlay(george, in eligible);

        Assert.True(result.IsApplicable);
        Assert.True(result.Allowed);
        Assert.True(result.FreeAction);
        Assert.True(result.AsAttack);
        Assert.Equal(3, result.AttackDamage);

        Assert.False(EvaluateGeorge(george, eligible with { Phase = RulePhase.PlayerStart }).IsApplicable);
        Assert.False(EvaluateGeorge(george, eligible with { Traits = RuleCardTraits.Block | RuleCardTraits.Weapon }).IsApplicable);
        Assert.False(EvaluateGeorge(george, eligible with { Traits = RuleCardTraits.Block | RuleCardTraits.Token }).IsApplicable);
        Assert.False(EvaluateGeorge(george, eligible with { Traits = RuleCardTraits.Attack }).IsApplicable);
    }

    [Fact]
    public void Christopher_and_lawrence_match_stat_kind_mode_traits_and_payment_count()
    {
        ProviderSource christopher = Source(MedalId.StChristopher, entityIndex: 2);
        var block = new CardStatQuery(
            christopher.EquippedOwner, new EntityId(40, 1), christopher.EquippedOwner, EntityId.Null,
            CardId.ShieldOfFaith, CardStatKind.Block, CardStatQueryMode.Preview,
            RuleCardTraits.Block | RuleCardTraits.Brittle, BaseValue: 3, PaymentCardCount: 0);
        CardStatModifierResult blockResult = StChristopherMedalModule.GetCardStatModifier(christopher, in block);

        Assert.True(blockResult.IsApplicable);
        Assert.Equal(RuleStatIds.CardBlock, blockResult.Stat);
        Assert.Equal(1, blockResult.Delta);
        Assert.False(EvaluateStat(christopher, block with { Traits = RuleCardTraits.Block }).IsApplicable);
        Assert.False(EvaluateStat(christopher, block with { Kind = CardStatKind.Damage }).IsApplicable);

        ProviderSource lawrence = Source(MedalId.StLawrence, entityIndex: 3);
        var damage = new CardStatQuery(
            lawrence.EquippedOwner, new EntityId(41, 1), lawrence.EquippedOwner, EntityId.Null,
            CardId.Strike, CardStatKind.Damage, CardStatQueryMode.Resolution,
            RuleCardTraits.Attack | RuleCardTraits.Scorched, BaseValue: 5, PaymentCardCount: 2);
        CardStatModifierResult damageResult = StLawrenceMedalModule.GetCardStatModifier(lawrence, in damage);

        Assert.True(damageResult.IsApplicable);
        Assert.Equal(RuleStatIds.AttackDamage, damageResult.Stat);
        Assert.Equal(2, damageResult.Delta);
        Assert.False(EvaluateStat(lawrence, damage with { Mode = CardStatQueryMode.Preview }).IsApplicable);
        Assert.False(EvaluateStat(lawrence, damage with { PaymentCardCount = 0 }).IsApplicable);
        Assert.False(EvaluateStat(lawrence, damage with { Traits = RuleCardTraits.Attack }).IsApplicable);
    }

    [Fact]
    public void Olaf_redirects_frostbite_damage_or_suppresses_it_when_no_enemy_exists()
    {
        ProviderSource olaf = Source(MedalId.StOlaf, entityIndex: 4);
        var query = new ReplacementQuery(
            RuleReplacementKind.EffectThresholdDamage,
            olaf.EquippedOwner,
            olaf.EquippedOwner,
            new EntityId(90, 1),
            RuleEffectIds.Frostbite,
            RuleDamageKind.Effect,
            OriginalDelta: -3,
            PrimaryEnemyHasRequiredState: 1);
        Span<ReplacementAction> actions = stackalloc ReplacementAction[1];
        var writer = new ReplacementPlanWriter(actions);

        Assert.True(StOlafMedalModule.TryBuildReplacement(olaf, in query, ref writer));
        ReplacementPlan plan = writer.BuildPlan();
        Assert.True(plan.IsHandled);
        Assert.Equal(1, plan.ActionCount);
        Assert.Equal(ReplacementActionKind.ModifyHp, writer.Actions[0].Kind);
        Assert.Equal(query.OriginalTarget, writer.Actions[0].Source);
        Assert.Equal(query.PrimaryEnemy, writer.Actions[0].Target);
        Assert.Equal(-3, writer.Actions[0].Amount);

        var noEnemy = query with
        {
            PrimaryEnemy = EntityId.Null,
            PrimaryEnemyHasRequiredState = 0,
        };
        var suppressWriter = new ReplacementPlanWriter(Span<ReplacementAction>.Empty);
        Assert.True(StOlafMedalModule.TryBuildReplacement(olaf, in noEnemy, ref suppressWriter));
        Assert.True(suppressWriter.BuildPlan().IsHandled);
        Assert.Equal(0, suppressWriter.BuildPlan().ActionCount);

        var wrongTarget = query with { OriginalTarget = new EntityId(91, 1) };
        var rejectedWriter = new ReplacementPlanWriter(actions);
        Assert.False(StOlafMedalModule.TryBuildReplacement(olaf, in wrongTarget, ref rejectedWriter));
    }

    [Fact]
    public void Content_provider_results_follow_shared_stable_entity_precedence_and_lifetime()
    {
        ProviderSource later = Source(MedalId.StGeorge, entityIndex: 9);
        ProviderSource earlier = Source(MedalId.StGeorge, entityIndex: 2);
        var query = new AlternatePlayQuery(
            earlier.EquippedOwner, new EntityId(40, 1), CardId.ShieldOfFaith,
            RulePhase.Action, RuleCardTraits.Block);
        AlternatePlayResult laterResult = StGeorgeMedalModule.GetAlternatePlay(later, in query);
        AlternatePlayResult earlierResult = StGeorgeMedalModule.GetAlternatePlay(earlier, in query);
        ProviderCandidateResult[] candidates =
        [
            new(later, laterResult.Applies),
            new(earlier, earlierResult.Applies),
        ];

        Assert.True(ProviderPrecedence.TrySelectFirst(candidates, earlier.EquippedOwner, out ProviderCandidateResult selected));
        Assert.Equal(earlier.Entity, selected.Source.Entity);
        Assert.Equal(ProviderLifetime.WhileEquipped, GeneratedMedalCatalog.GetDefinition(MedalId.StGeorge).Provider.Lifetime);
        Assert.Equal(ProviderLifetime.WhileEquipped, GeneratedMedalCatalog.GetDefinition(MedalId.StChristopher).Provider.Lifetime);
        Assert.Equal(ProviderLifetime.WhileEquipped, GeneratedMedalCatalog.GetDefinition(MedalId.StLawrence).Provider.Lifetime);
        Assert.Equal(ProviderLifetime.WhileEquipped, GeneratedMedalCatalog.GetDefinition(MedalId.StOlaf).Provider.Lifetime);
    }

    private static AlternatePlayResult EvaluateGeorge(ProviderSource source, AlternatePlayQuery query) =>
        StGeorgeMedalModule.GetAlternatePlay(source, in query);

    private static CardStatModifierResult EvaluateStat(ProviderSource source, CardStatQuery query) =>
        MedalProviderRules.GetCardStatModifier(source, in query);

    private static ProviderSource Source(MedalId id, int entityIndex) => new(
        new EntityId(entityIndex, 1),
        new EntityId(20, 1),
        id,
        ProviderLifetime.WhileEquipped,
        IsActive: 1);
}
