#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Enemies;

internal static class EnemyContentBehavior
{
    public static void HandleEnemy(ref EnemyHandlerContext context, EnemyDefinitionData definition)
    {
        if ((context.Input.Flags & EnemyHandlerFlags.Planning) != 0)
        {
            BuildPlan(ref context);
            return;
        }

        if (context.Stage == RuleTriggerIds.BattleStart)
        {
            ApplyBattleStart(ref context, definition.Id);
        }
        else if (definition.Id == EnemyId.DustWuurm && context.Stage == RuleTriggerIds.EnemyTurnStart)
        {
            ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Power, 1);
        }
        else if (definition.Id == EnemyId.FireSkeleton && context.Stage == RuleTriggerIds.ActionPhaseEnd &&
                 context.Facts.GetOrDefault(RuleFactIds.ResultValue) >= 4)
        {
            int damage = context.Facts.GetOrDefault(RuleFactIds.PassiveStacks, 2);
            context.Append(RuleCommand.Damage(TargetHandle.Player, TargetHandle.Player, damage));
        }
    }

    public static void HandleAttack(ref EnemyHandlerContext context, EnemyAttackDefinitionData definition)
    {
        if (context.Stage == RuleTriggerIds.EnemyAttackChannelApplied && definition.Id == EnemyAttackId.InfernalExecution)
        {
            context.Results.ModifyStat(RuleStatIds.AttackAdditionalDamage, 0);
            context.Results.Record(RuleFactIds.PassiveStacks, 1 + context.Input.Combat.ChannelStacks);
            return;
        }

        if (context.Stage == RuleTriggerIds.EnemyAttackReveal)
        {
            ApplyRequirement(ref context, definition);
            ResolveDynamicAttackValues(ref context, definition);
            HandleReveal(ref context, definition.Id);
            return;
        }

        if (context.Stage == RuleTriggerIds.EnemyAttackHit)
        {
            HandleHit(ref context, definition.Id);
            return;
        }

        if (context.Stage == RuleTriggerIds.EnemyAttackDamageThresholdMet)
        {
            HandleThreshold(ref context, definition.Id);
            return;
        }

        if (context.Stage == RuleTriggerIds.EnemyAttackBlockProcessed)
        {
            HandleBlocker(ref context, definition.Id);
            return;
        }

        if (context.Stage == RuleTriggerIds.EnemyAttackBlocksConfirmed && definition.Id == EnemyAttackId.BasiliskGlare)
        {
            foreach (EntityId target in context.Targets)
            {
                context.Append(RuleCommand.RemoveEffect(
                    TargetHandle.ForEntity(context.Enemy),
                    TargetHandle.ForEntity(target),
                    RuleEffectIds.CannotBlockCurrentAttack));
            }

            return;
        }

        if (context.Stage == RuleTriggerIds.EnemyAttackProgressOverride)
        {
            int affected = context.Facts.GetOrDefault(RuleFactIds.CandidateCount);
            int effective = Math.Max(0, context.Input.Combat.TotalAssignedBlock - affected);
            context.Append(RuleCommand.SetResolvedValue(
                TargetHandle.ForEntity(context.Enemy),
                ResolvedValueKind.EffectiveBlock,
                effective));
            context.Results.Record(RuleFactIds.EffectiveBlockTotal, effective);
        }
    }

    private static void BuildPlan(ref EnemyHandlerContext context)
    {
        EnemyPlanWriter plan = context.Plan;
        EnemyPlanningMemory memory = plan.Memory;
        DeterministicRuleRandom random = context.Random;
        int turn = context.Input.Turn;
        switch (context.Definition)
        {
            case EnemyId.Demon:
                int demonRoll = random.NextPercent();
                Append(ref plan, demonRoll >= 60 ? EnemyAttackId.RazorMaw : demonRoll >= 20 ? EnemyAttackId.ScorchingClaw : EnemyAttackId.InfernalExecution);
                break;
            case EnemyId.Horde:
                Append(ref plan, TutorialAttack(context.Facts));
                break;
            case EnemyId.Mummy:
                if (turn == 5) Append(ref plan, EnemyAttackId.Leprosy);
                else Append(ref plan, random.NextPercent() <= 70 ? EnemyAttackId.Entomb : EnemyAttackId.Mummify);
                break;
            case EnemyId.Ninja:
                PlanNinja(ref plan, ref random, turn);
                break;
            case EnemyId.Ogre:
                PlanOgre(ref plan, ref random, ref memory);
                break;
            case EnemyId.SandCorpse:
                if (random.NextBool()) plan.AppendRange([EnemyAttackId.SandBlast, EnemyAttackId.SandStorm]);
                else plan.AppendRange([EnemyAttackId.SandStorm, EnemyAttackId.SandBlast]);
                break;
            case EnemyId.SandGolem:
                Append(ref plan, turn % 2 == 1 ? EnemyAttackId.SandPound : EnemyAttackId.SandSlam);
                break;
            case EnemyId.Skeleton:
            case EnemyId.FireSkeleton:
                PlanSkeleton(ref plan, ref random);
                break;
            case EnemyId.SkeletalArcher:
                PlanArcher(ref plan, ref random, turn, ref memory);
                break;
            case EnemyId.Spider:
                Append(ref plan, random.NextPercent() <= 65 ? EnemyAttackId.SuffocatingSilk : EnemyAttackId.MandibleBreaker);
                break;
            case EnemyId.Succubus:
                PlanSuccubus(ref plan, ref random, context.Facts.GetOrDefault(RuleFactIds.CourageLostThisBattle));
                break;
            case EnemyId.Thornreaver: Append(ref plan, EnemyAttackId.SawtoothRend); break;
            case EnemyId.DustWuurm: Append(ref plan, EnemyAttackId.DustStorm); break;
            case EnemyId.Sorcerer: Append(ref plan, EnemyAttackId.StrangeForce); break;
            case EnemyId.IceDemon:
                if (context.Facts.GetOrDefault(RuleFactIds.FrozenInHand) > 1 && random.NextPercent() <= 75) Append(ref plan, EnemyAttackId.FrostEater);
                else Append(ref plan, random.NextBool() ? EnemyAttackId.IcyBlade : EnemyAttackId.FrozenClaw);
                break;
            case EnemyId.GlacialGuardian:
                Append(ref plan, random.NextBool() ? EnemyAttackId.GlacialStrike : EnemyAttackId.GlacialBlast);
                break;
            case EnemyId.CinderboltDemon:
                bool used = (memory.Value0 & 1) != 0;
                if (!used && ((turn == 3 && random.NextPercent() < 50) || turn > 3))
                {
                    memory.Value0 |= 1;
                    Append(ref plan, EnemyAttackId.InsidiousBolt);
                }
                else Append(ref plan, EnemyAttackId.Cinderbolt);
                break;
            case EnemyId.Berserker: Append(ref plan, EnemyAttackId.Rage); break;
            case EnemyId.Shadow:
                if (turn % 2 == 0) AppendRandomDistinct(ref plan, ref random, [EnemyAttackId.SnuffOutTheLight, EnemyAttackId.NightFall, EnemyAttackId.FromTheShadows, EnemyAttackId.UmbraSlice], 3);
                else Append(ref plan, random.NextBool() ? EnemyAttackId.ShadowStrike : EnemyAttackId.DissipatingDarkness);
                break;
            case EnemyId.EarthDemon:
                Append(ref plan, Pick(ref random, [EnemyAttackId.TremorStrike, EnemyAttackId.StoneBarrage, EnemyAttackId.EarthenWall]));
                break;
            case EnemyId.Medusa:
                PlanMedusa(ref plan, ref random, context.Facts.GetOrDefault(RuleFactIds.SealedInHand) > 0);
                break;
            case EnemyId.Wyvern:
                Append(ref plan, turn % 2 == 0 ? EnemyAttackId.WyvernThreat : EnemyAttackId.WyvernStrike);
                break;
            case EnemyId.FallenShepherd:
                PlanFallenShepherd(ref plan, ref random, context.Input.PlanningMemory.Phase, turn, ref memory);
                break;
            case EnemyId.TrainingDemon: Append(ref plan, EnemyAttackId.TrainingStrike); break;
        }

        plan.Memory = memory;
    }

    private static void ApplyBattleStart(ref EnemyHandlerContext context, EnemyId id)
    {
        TargetHandle enemy = TargetHandle.ForEntity(context.Enemy);
        switch (id)
        {
            case EnemyId.Ninja: ApplyEffect(ref context, enemy, RuleEffectIds.Stealth, 1); break;
            case EnemyId.Skeleton:
            case EnemyId.SkeletalArcher: ApplyEffect(ref context, enemy, RuleEffectIds.Armor, 1); break;
            case EnemyId.Spider: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Fear, 2); break;
            case EnemyId.Succubus: ApplyEffect(ref context, enemy, RuleEffectIds.Siphon, 1); break;
            case EnemyId.DustWuurm: ApplyEffect(ref context, enemy, RuleEffectIds.Rage, 1); break;
            case EnemyId.Sorcerer: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.MindFog, 1); break;
            case EnemyId.GlacialGuardian: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Windchill, 1); break;
            case EnemyId.FireSkeleton:
                ApplyEffect(ref context, enemy, RuleEffectIds.Armor, 2);
                ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Enflamed, 2);
                break;
            case EnemyId.Berserker:
                ApplyEffect(ref context, enemy, RuleEffectIds.Wounded, 1);
                ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Shackled, 5);
                break;
            case EnemyId.Shadow: ApplyEffect(ref context, enemy, RuleEffectIds.Anathema, 4); break;
            case EnemyId.Wyvern: ApplyEffect(ref context, enemy, RuleEffectIds.Plunder, 1); break;
        }
    }

    private static void ApplyRequirement(ref EnemyHandlerContext context, EnemyAttackDefinitionData definition)
    {
        RequirementKind kind = definition.Condition switch
        {
            EnemyAttackCondition.MustBlockWithAtLeastOneCard or EnemyAttackCondition.MustBlockWithAtLeastTwoCards => RequirementKind.MinimumBlockers,
            EnemyAttackCondition.MustBlockWithExactlyOneCard or EnemyAttackCondition.MustBlockWithExactlyTwoCards => RequirementKind.ExactBlockers,
            _ => RequirementKind.None,
        };
        if (kind != RequirementKind.None)
        {
            int amount = definition.Condition is EnemyAttackCondition.MustBlockWithAtLeastTwoCards or EnemyAttackCondition.MustBlockWithExactlyTwoCards ? 2 : 1;
            context.Append(RuleCommand.SetRequirement(TargetHandle.ForEntity(context.Enemy), kind, amount));
        }

        if (definition.ColorRestriction != EnemyAttackColorRestriction.None)
        {
            RuleCardColor color = definition.ColorRestriction switch
            {
                EnemyAttackColorRestriction.NotRed or EnemyAttackColorRestriction.OnlyRed => RuleCardColor.Red,
                EnemyAttackColorRestriction.NotWhite or EnemyAttackColorRestriction.OnlyWhite => RuleCardColor.White,
                _ => RuleCardColor.Black,
            };
            RequirementKind colorKind = definition.ColorRestriction is EnemyAttackColorRestriction.NotRed or EnemyAttackColorRestriction.NotWhite or EnemyAttackColorRestriction.NotBlack
                ? RequirementKind.ExcludeCardColor : RequirementKind.OnlyCardColor;
            context.Append(RuleCommand.SetRequirement(TargetHandle.ForEntity(context.Enemy), colorKind, color: color));
        }
    }

    private static void ResolveDynamicAttackValues(ref EnemyHandlerContext context, EnemyAttackDefinitionData definition)
    {
        DeterministicRuleRandom random = context.Random;
        if (definition.MaximumDamage > definition.MinimumDamage)
        {
            int damage = definition.MinimumDamage + random.NextInt(definition.MaximumDamage - definition.MinimumDamage + 1);
            context.Append(RuleCommand.SetResolvedValue(TargetHandle.ForEntity(context.Enemy), ResolvedValueKind.Damage, damage));
            context.Results.Record(RuleFactIds.DamageDealt, damage);
        }

        if (definition.MaximumBlockThreshold > definition.MinimumBlockThreshold)
        {
            int threshold = definition.MinimumBlockThreshold + random.NextInt(definition.MaximumBlockThreshold - definition.MinimumBlockThreshold + 1);
            context.Results.Record(RuleFactIds.AssignedBlockTotal, threshold);
        }
    }

    private static void HandleReveal(ref EnemyHandlerContext context, EnemyAttackId id)
    {
        switch (id)
        {
            case EnemyAttackId.PummelIntoSubmission:
            case EnemyAttackId.SlamTrunk:
            case EnemyAttackId.MandibleBreaker:
            case EnemyAttackId.FrozenClaw:
            case EnemyAttackId.FromTheShadows:
            case EnemyAttackId.FallenShepherdCowTheFlock: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Intimidated, 1); break;
            case EnemyAttackId.TreeStomp:
            case EnemyAttackId.FakeOut: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Intimidated, 2); break;
            case EnemyAttackId.Sweep: ApplyRandomCardEffect(ref context, RuleEffectIds.Recoil, 1); break;
            case EnemyAttackId.PiercingShot: context.Append(RuleCommand.Damage(TargetHandle.ForEntity(context.Enemy), TargetHandle.Player, 2)); break;
            case EnemyAttackId.InfernalExecution: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Burn, 1 + context.Input.Combat.ChannelStacks); break;
            case EnemyAttackId.Leprosy: ApplyRandomCardEffect(ref context, RuleEffectIds.Brittle, 2); break;
            case EnemyAttackId.StrangeForce:
                ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Intimidated, 1);
                if (context.Targets.Length > 0)
                    context.Append(RuleCommand.RandomCardZone(context.Targets[0], CardZone.DrawPile, CardZone.Hand, RandomCardZoneOperation.MoveTop));
                break;
            case EnemyAttackId.Cinderbolt:
            case EnemyAttackId.InsidiousBolt:
            case EnemyAttackId.SawtoothRend:
            case EnemyAttackId.StoneBarrage:
                context.Results.Record(RuleFactIds.SelectedColor, context.Random.NextInt(3) + 1);
                break;
            case EnemyAttackId.NightveilGuillotine:
                if (context.Facts.GetOrDefault(RuleFactIds.PlanningCounter0) > 0 && context.Facts.GetOrDefault(RuleFactIds.PlanningCounter1) > 0)
                    context.Append(RuleCommand.SetResolvedValue(TargetHandle.ForEntity(context.Enemy), ResolvedValueKind.AdditionalDamage, 4));
                break;
            case EnemyAttackId.EarthenWall: ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Guard, 4); break;
            case EnemyAttackId.HaveNoMercy:
            case EnemyAttackId.FallenShepherdPhase3: ApplyRandomCardEffect(ref context, RuleEffectIds.MarkedForSpecificDiscard, 1); break;
            case EnemyAttackId.BasiliskGlare:
                foreach (EntityId target in context.Targets) ApplyEffect(ref context, TargetHandle.ForEntity(target), RuleEffectIds.CannotBlockCurrentAttack, 1);
                break;
            case EnemyAttackId.VipersCurse: ApplyRandomCardEffect(ref context, RuleEffectIds.Sealed, 1, 2); break;
            case EnemyAttackId.FallenShepherdPurgeTheHeretic: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Burn, 1); break;
            case EnemyAttackId.FallenShepherdFearTheShepherd: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Fear, 1); break;
            case EnemyAttackId.FallenShepherdFinalSermon: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Silenced, 1); break;
        }
    }

    private static void HandleHit(ref EnemyHandlerContext context, EnemyAttackId id)
    {
        switch (id)
        {
            case EnemyAttackId.PummelIntoSubmission:
            case EnemyAttackId.BoneStrike:
            case EnemyAttackId.UmbraSlice:
            case EnemyAttackId.FallenShepherdCrooksScar: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Scar, 1); break;
            case EnemyAttackId.Thud:
            case EnemyAttackId.DuskFlick: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Wounded, 1); break;
            case EnemyAttackId.Calcify: ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Guard, 2); break;
            case EnemyAttackId.WeatheringShot:
                foreach (EntityId target in context.Targets) ApplyEffect(ref context, TargetHandle.ForEntity(target), RuleEffectIds.Brittle, 1);
                break;
            case EnemyAttackId.SilencingStab: ApplyRandomCardEffect(ref context, RuleEffectIds.CardFrozen, 3); break;
            case EnemyAttackId.SharpenBlade: ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Aggression, 3); break;
            case EnemyAttackId.ScorchingClaw: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Burn, 1); break;
            case EnemyAttackId.MandibleBreaker: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Fear, 1); break;
            case EnemyAttackId.VelvetFangs: context.Append(RuleCommand.Heal(TargetHandle.ForEntity(context.Enemy), TargetHandle.ForEntity(context.Enemy), context.Facts.GetOrDefault(RuleFactIds.CourageLostThisBattle))); break;
            case EnemyAttackId.SoulSiphon: context.Append(RuleCommand.ModifyStat(TargetHandle.ForEntity(context.Enemy), TargetHandle.Player, RuleStatIds.Courage, -1)); break;
            case EnemyAttackId.CrushingAdoration: ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Aggression, 2); break;
            case EnemyAttackId.TeasingNip:
                if (context.Random.NextPercent() < 50) context.Append(RuleCommand.ModifyStat(TargetHandle.ForEntity(context.Enemy), TargetHandle.Player, RuleStatIds.Courage, -1));
                break;
            case EnemyAttackId.StrangeForce:
                if ((context.Input.Flags & EnemyHandlerFlags.AttackBlocked) == 0 && context.Targets.Length > 0)
                    context.Append(RuleCommand.RandomCardZone(context.Targets[0], CardZone.DrawPile, CardZone.DiscardPile, RandomCardZoneOperation.Mill));
                break;
            case EnemyAttackId.IcyBlade: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Frostbite, 2); break;
            case EnemyAttackId.DissipatingDarkness: ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Anathema, 1); break;
            case EnemyAttackId.SnuffOutTheLight:
            case EnemyAttackId.FallenShepherdHush: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Silenced, 1); break;
            case EnemyAttackId.NightFall: ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Anathema, -1); break;
            case EnemyAttackId.TremorStrike: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Shackled, 2); break;
            case EnemyAttackId.Gaze: ApplyRandomCardEffect(ref context, RuleEffectIds.Sealed, 1, 3); break;
            case EnemyAttackId.SerpentStrike:
                foreach (EntityId target in context.Targets) ApplyEffect(ref context, TargetHandle.ForEntity(target), RuleEffectIds.Sealed, 1);
                break;
            case EnemyAttackId.WyvernThreat: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Plunder, -1); break;
            case EnemyAttackId.FallenShepherdBloodletting: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Bleed, 3); break;
            case EnemyAttackId.FallenShepherdShepherdsVigil: ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Guard, 3); break;
        }
    }

    private static void HandleThreshold(ref EnemyHandlerContext context, EnemyAttackId id)
    {
        switch (id)
        {
            case EnemyAttackId.RazorMaw: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Burn, 1); break;
            case EnemyAttackId.SuffocatingSilk: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Slow, 4); break;
            case EnemyAttackId.Entomb: ApplyRandomCardEffect(ref context, RuleEffectIds.Brittle, 1); break;
            case EnemyAttackId.Mummify: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Scar, 2); break;
            case EnemyAttackId.FrozenClaw: ApplyRandomCardEffect(ref context, RuleEffectIds.CardFrozen, 1); break;
            case EnemyAttackId.ShadowStrike: ApplyEffect(ref context, TargetHandle.ForEntity(context.Enemy), RuleEffectIds.Anathema, -1); break;
            case EnemyAttackId.FallenShepherdPhase2: ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Shackled, 2); break;
            case EnemyAttackId.FallenShepherdPhase3:
                foreach (EntityId target in context.Targets)
                    context.Append(RuleCommand.RemoveEffect(TargetHandle.ForEntity(context.Enemy), TargetHandle.ForEntity(target), RuleEffectIds.MarkedForSpecificDiscard));
                break;
        }
    }

    private static void HandleBlocker(ref EnemyHandlerContext context, EnemyAttackId id)
    {
        TargetHandle card = context.PrimaryTarget;
        switch (id)
        {
            case EnemyAttackId.ShadowStep:
                if ((context.Input.Flags & EnemyHandlerFlags.FinalBattle) == 0)
                    context.Append(RuleCommand.MutateCard(card.Entity, CardMutationKind.ModifyBlock, -2, flags: RuleValueFlags.QuestPersistent));
                break;
            case EnemyAttackId.SawtoothRend:
            case EnemyAttackId.StoneBarrage:
                if (context.Facts.GetOrDefault(RuleFactIds.SourceColor) == context.Facts.GetOrDefault(RuleFactIds.SelectedColor))
                    ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Bleed, 2);
                break;
            case EnemyAttackId.Cinderbolt:
                if (context.Facts.GetOrDefault(RuleFactIds.SourceColor) == context.Facts.GetOrDefault(RuleFactIds.SelectedColor))
                    ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Burn, 1);
                break;
            case EnemyAttackId.InsidiousBolt:
                if (context.Facts.GetOrDefault(RuleFactIds.SourceColor) == context.Facts.GetOrDefault(RuleFactIds.SelectedColor))
                    ApplyEffect(ref context, TargetHandle.Player, RuleEffectIds.Scar, 2);
                break;
            case EnemyAttackId.PetrifyingGaze: ApplyEffect(ref context, card, RuleEffectIds.Sealed, 2); break;
            case EnemyAttackId.CrumblingStone: context.Append(RuleCommand.MutateCard(card.Entity, CardMutationKind.ModifySeals, -2)); break;
            case EnemyAttackId.FallenShepherdPhase1: ApplyEffect(ref context, card, RuleEffectIds.Colorless, 1, RuleValueFlags.QuestPersistent); break;
            case EnemyAttackId.FallenShepherdBreakFaith: ApplyEffect(ref context, card, RuleEffectIds.Brittle, 1, RuleValueFlags.QuestPersistent); break;
        }
    }

    private static void ApplyRandomCardEffect(ref EnemyHandlerContext context, EffectId effect, int selectionCount, int magnitude = 1)
    {
        if (context.Targets.IsEmpty) return;
        DeterministicRuleRandom random = context.Random;
        int count = Math.Min(selectionCount, context.Targets.Length);
        Span<int> indices = stackalloc int[context.Targets.Length];
        for (var index = 0; index < indices.Length; index++) indices[index] = index;
        Shuffle(ref random, indices);
        for (var index = 0; index < count; index++) ApplyEffect(ref context, TargetHandle.ForEntity(context.Targets[indices[index]]), effect, magnitude);
    }

    private static void ApplyEffect(ref EnemyHandlerContext context, TargetHandle target, EffectId effect, int amount, RuleValueFlags flags = RuleValueFlags.None)
    {
        var spec = new EffectSpec(effect, amount, 0, ConditionSpec.Always, flags);
        context.Append(RuleCommand.ApplyEffect(TargetHandle.ForEntity(context.Enemy), target, in spec));
    }

    private static void Append(ref EnemyPlanWriter plan, EnemyAttackId attack)
    {
        plan.Append(attack);
        plan.Remember(attack);
    }

    private static EnemyAttackId Pick(ref DeterministicRuleRandom random, ReadOnlySpan<EnemyAttackId> pool) => pool[random.NextInt(pool.Length)];

    private static void AppendRandomDistinct(ref EnemyPlanWriter plan, ref DeterministicRuleRandom random, ReadOnlySpan<EnemyAttackId> pool, int count)
    {
        Span<EnemyAttackId> copy = stackalloc EnemyAttackId[pool.Length];
        pool.CopyTo(copy);
        Shuffle(ref random, copy);
        for (var index = 0; index < count; index++) plan.Append(copy[index]);
        if (count > 0) plan.Remember(copy[count - 1]);
    }

    private static void PlanNinja(ref EnemyPlanWriter plan, ref DeterministicRuleRandom random, int turn)
    {
        if (turn == 1 || random.NextPercent() >= 90)
        {
            plan.AppendRange([EnemyAttackId.Slice, EnemyAttackId.Dice, EnemyAttackId.SharpenBlade, EnemyAttackId.NightveilGuillotine]);
            plan.Remember(EnemyAttackId.NightveilGuillotine);
            return;
        }

        Span<EnemyAttackId> values = stackalloc EnemyAttackId[6];
        int count = 0;
        values[count++] = EnemyAttackId.Slice;
        bool sliceAndDice = random.NextPercent() >= 50;
        if (sliceAndDice) values[count++] = EnemyAttackId.Dice;
        ReadOnlySpan<EnemyAttackId> linkers = [EnemyAttackId.DuskFlick, EnemyAttackId.CloakedReaver, EnemyAttackId.SilencingStab, EnemyAttackId.SharpenBlade, EnemyAttackId.ShadowStep, EnemyAttackId.ShadowStep, EnemyAttackId.ShadowStep];
        int linkerCount = random.NextPercent() >= 50 ? 3 : 2;
        for (var index = 0; index < linkerCount; index++) values[count++] = Pick(ref random, linkers);
        Shuffle(ref random, values[..count]);
        for (var index = 0; index < count; index++) plan.Append(values[index]);
        int ender = random.NextPercent();
        if (ender >= 80 && sliceAndDice) plan.Append(EnemyAttackId.NightveilGuillotine);
        else if (ender >= 60) plan.Append(EnemyAttackId.HaveNoMercy);
        plan.Remember(plan[plan.Count - 1]);
    }

    private static void PlanOgre(ref EnemyPlanWriter plan, ref DeterministicRuleRandom random, ref EnemyPlanningMemory memory)
    {
        int roll = random.NextPercent();
        if (roll <= 20) plan.AppendRange([EnemyAttackId.SlamTrunk, EnemyAttackId.FakeOut]);
        else if (roll <= 40) plan.AppendRange([EnemyAttackId.SlamTrunk, EnemyAttackId.Thud]);
        else if (roll <= 60) plan.Append(EnemyAttackId.TreeStomp);
        else if (roll <= 80 && memory.Value0 < 2) { memory.Value0++; plan.Append(EnemyAttackId.PummelIntoSubmission); }
        else plan.AppendRange([EnemyAttackId.SlamTrunk, EnemyAttackId.HaveNoMercy]);
        plan.Remember(plan[plan.Count - 1]);
    }

    private static void PlanSkeleton(ref EnemyPlanWriter plan, ref DeterministicRuleRandom random)
    {
        if (random.NextPercent() > 65) { Append(ref plan, EnemyAttackId.SkullCrusher); return; }
        ReadOnlySpan<EnemyAttackId> pool = [EnemyAttackId.BoneStrike, EnemyAttackId.Sweep, EnemyAttackId.Calcify];
        Span<EnemyAttackId> selected = stackalloc EnemyAttackId[3];
        do { for (var i = 0; i < 3; i++) selected[i] = Pick(ref random, pool); }
        while (selected[0] == EnemyAttackId.Sweep && selected[1] == EnemyAttackId.Sweep && selected[2] == EnemyAttackId.Sweep);
        if (random.NextPercent() <= 5)
        {
            selected[0] = Pick(ref random, pool);
            selected[1] = Pick(ref random, pool);
            selected[2] = EnemyAttackId.HaveNoMercy;
            Shuffle(ref random, selected);
        }
        for (var index = 0; index < selected.Length; index++) plan.Append(selected[index]);
        plan.Remember(selected[2]);
    }

    private static void PlanArcher(ref EnemyPlanWriter plan, ref DeterministicRuleRandom random, int turn, ref EnemyPlanningMemory memory)
    {
        if (random.NextPercent() <= 5 || memory.Value0 >= 2 || turn == 1)
        {
            if (memory.Value0 == 2) memory.Value0 = 0;
            AppendRandomDistinct(ref plan, ref random, [EnemyAttackId.PiercingShot, EnemyAttackId.WeatheringShot, EnemyAttackId.QuickShot], 2);
        }
        else { memory.Value0++; Append(ref plan, EnemyAttackId.Snipe); }
    }

    private static void PlanSuccubus(ref EnemyPlanWriter plan, ref DeterministicRuleRandom random, int courageLost)
    {
        ReadOnlySpan<EnemyAttackId> linkers = [EnemyAttackId.EnthrallingGaze, EnemyAttackId.TeasingNip, EnemyAttackId.CrushingAdoration];
        Span<EnemyAttackId> attacks = stackalloc EnemyAttackId[3];
        attacks[0] = Pick(ref random, linkers);
        attacks[1] = Pick(ref random, linkers);
        attacks[2] = courageLost > 0 && random.NextBool() ? EnemyAttackId.VelvetFangs : EnemyAttackId.SoulSiphon;
        Shuffle(ref random, attacks);
        if (attacks[2] == EnemyAttackId.CrushingAdoration)
        {
            int swap = attacks[0] != EnemyAttackId.CrushingAdoration ? 0 : 1;
            (attacks[2], attacks[swap]) = (attacks[swap], attacks[2]);
        }
        for (var index = 0; index < attacks.Length; index++) plan.Append(attacks[index]);
        plan.Remember(attacks[2]);
    }

    private static void PlanMedusa(ref EnemyPlanWriter plan, ref DeterministicRuleRandom random, bool sealedCards)
    {
        ReadOnlySpan<EnemyAttackId> basePool = [EnemyAttackId.Gaze, EnemyAttackId.PetrifyingGaze, EnemyAttackId.VipersCurse];
        ReadOnlySpan<EnemyAttackId> fullPool = [EnemyAttackId.Gaze, EnemyAttackId.PetrifyingGaze, EnemyAttackId.VipersCurse, EnemyAttackId.BasiliskGlare, EnemyAttackId.SerpentStrike, EnemyAttackId.StoneSkin, EnemyAttackId.CrumblingStone];
        ReadOnlySpan<EnemyAttackId> pool = sealedCards ? fullPool : basePool;
        for (var index = 0; index < 3; index++) plan.Append(Pick(ref random, pool));
        plan.Remember(plan[2]);
    }

    private static void PlanFallenShepherd(ref EnemyPlanWriter plan, ref DeterministicRuleRandom random, int phase, int turn, ref EnemyPlanningMemory memory)
    {
        bool heavy = turn <= 1 || turn % 2 == 1;
        if (phase == 2 && heavy) Append(ref plan, EnemyAttackId.FallenShepherdPhase2);
        else if (phase == 2) AppendRandomDistinct(ref plan, ref random, [EnemyAttackId.FallenShepherdShepherdsVigil, EnemyAttackId.FallenShepherdHush, EnemyAttackId.FallenShepherdCrooksScar, EnemyAttackId.FallenShepherdCowTheFlock], 3);
        else if (phase == 3)
        {
            Span<EnemyAttackId> pool = stackalloc EnemyAttackId[] { EnemyAttackId.FallenShepherdPurgeTheHeretic, EnemyAttackId.FallenShepherdFearTheShepherd, EnemyAttackId.FallenShepherdFinalSermon, EnemyAttackId.FallenShepherdPhase3 };
            int count = (memory.Value0 & 1) != 0 ? 3 : 4;
            if (count == 3) pool[1] = pool[3];
            EnemyAttackId selected = pool[random.NextInt(count)];
            if (selected == EnemyAttackId.FallenShepherdFearTheShepherd) memory.Value0 |= 1;
            Append(ref plan, selected);
        }
        else if (heavy) Append(ref plan, EnemyAttackId.FallenShepherdPhase1);
        else AppendRandomDistinct(ref plan, ref random, [EnemyAttackId.FallenShepherdCrooksScar, EnemyAttackId.FallenShepherdBreakFaith, EnemyAttackId.FallenShepherdBloodletting, EnemyAttackId.FallenShepherdCowTheFlock], 3);
    }

    private static EnemyAttackId TutorialAttack(RuleFactReader facts)
    {
        if (!facts.Contains(RuleFactIds.TutorialSection))
            return EnemyAttackId.Pounce;

        int section = facts.GetOrDefault(RuleFactIds.TutorialSection);
        int turn = facts.GetOrDefault(RuleFactIds.TutorialTurn, 1);
        return section switch
        {
            <= 3 => EnemyAttackId.TutorialHordeStrike3,
            4 or 5 => EnemyAttackId.TutorialHordeStrike8,
            6 or 7 => EnemyAttackId.TutorialHordeStrike6,
            8 when turn >= 2 => EnemyAttackId.TutorialHordeStrike6,
            8 => EnemyAttackId.TutorialHordeStrike8,
            _ => EnemyAttackId.Pounce,
        };
    }

    private static void Shuffle<T>(ref DeterministicRuleRandom random, scoped Span<T> values)
    {
        for (int index = values.Length - 1; index > 0; index--)
        {
            int swapIndex = random.NextInt(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
