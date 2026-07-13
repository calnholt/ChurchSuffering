#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Enemies;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Content.Enemies;

public sealed class EnemyBehaviorParityTests
{
    public static TheoryData<EnemyId, int, string> FrozenPlanningTraces => new()
    {
        { EnemyId.Demon, 1, "RazorMaw|RazorMaw|RazorMaw|RazorMaw|RazorMaw|RazorMaw" },
        { EnemyId.Horde, 1, "Pounce|Pounce|Pounce|Pounce|Pounce|Pounce" },
        { EnemyId.Mummy, 1, "Mummify|Entomb|Mummify|Mummify|Leprosy|Mummify" },
        { EnemyId.Ninja, 1, "Slice,Dice,SharpenBlade,NightveilGuillotine|CloakedReaver,Slice,SilencingStab,ShadowStep,Dice,HaveNoMercy|ShadowStep,Dice,Slice,SilencingStab,ShadowStep,HaveNoMercy|Slice,ShadowStep,Dice,DuskFlick,HaveNoMercy|Dice,CloakedReaver,Slice,CloakedReaver,CloakedReaver,NightveilGuillotine|SharpenBlade,Slice,DuskFlick,SilencingStab" },
        { EnemyId.Ogre, 1, "PummelIntoSubmission|PummelIntoSubmission|SlamTrunk,HaveNoMercy|SlamTrunk,HaveNoMercy|TreeStomp|SlamTrunk,HaveNoMercy" },
        { EnemyId.SandCorpse, 1, "SandBlast,SandStorm|SandStorm,SandBlast|SandStorm,SandBlast|SandStorm,SandBlast|SandStorm,SandBlast|SandBlast,SandStorm" },
        { EnemyId.SandGolem, 1, "SandPound|SandSlam|SandPound|SandSlam|SandPound|SandSlam" },
        { EnemyId.Skeleton, 1, "SkullCrusher|SkullCrusher|SkullCrusher|SkullCrusher|Calcify,Sweep,BoneStrike|SkullCrusher" },
        { EnemyId.SkeletalArcher, 1, "QuickShot,WeatheringShot|Snipe|Snipe|QuickShot,WeatheringShot|Snipe|Snipe" },
        { EnemyId.Spider, 1, "MandibleBreaker|MandibleBreaker|MandibleBreaker|MandibleBreaker|SuffocatingSilk|MandibleBreaker" },
        { EnemyId.Succubus, 1, "CrushingAdoration,EnthrallingGaze,SoulSiphon|CrushingAdoration,SoulSiphon,TeasingNip|CrushingAdoration,EnthrallingGaze,SoulSiphon|SoulSiphon,CrushingAdoration,EnthrallingGaze|CrushingAdoration,CrushingAdoration,SoulSiphon|EnthrallingGaze,EnthrallingGaze,SoulSiphon" },
        { EnemyId.Thornreaver, 1, "SawtoothRend|SawtoothRend|SawtoothRend|SawtoothRend|SawtoothRend|SawtoothRend" },
        { EnemyId.DustWuurm, 1, "DustStorm|DustStorm|DustStorm|DustStorm|DustStorm|DustStorm" },
        { EnemyId.Sorcerer, 1, "StrangeForce|StrangeForce|StrangeForce|StrangeForce|StrangeForce|StrangeForce" },
        { EnemyId.IceDemon, 1, "IcyBlade|FrozenClaw|FrozenClaw|FrozenClaw|FrozenClaw|IcyBlade" },
        { EnemyId.GlacialGuardian, 1, "GlacialStrike|GlacialBlast|GlacialBlast|GlacialBlast|GlacialBlast|GlacialStrike" },
        { EnemyId.CinderboltDemon, 1, "Cinderbolt|Cinderbolt|Cinderbolt|InsidiousBolt|Cinderbolt|Cinderbolt" },
        { EnemyId.FireSkeleton, 1, "SkullCrusher|SkullCrusher|SkullCrusher|SkullCrusher|Calcify,Sweep,BoneStrike|SkullCrusher" },
        { EnemyId.Berserker, 1, "Rage|Rage|Rage|Rage|Rage|Rage" },
        { EnemyId.Shadow, 1, "ShadowStrike|SnuffOutTheLight,UmbraSlice,NightFall|DissipatingDarkness|UmbraSlice,NightFall,SnuffOutTheLight|DissipatingDarkness|FromTheShadows,NightFall,SnuffOutTheLight" },
        { EnemyId.EarthDemon, 1, "EarthenWall|EarthenWall|EarthenWall|EarthenWall|EarthenWall|TremorStrike" },
        { EnemyId.Medusa, 1, "VipersCurse,Gaze,VipersCurse|VipersCurse,PetrifyingGaze,Gaze|VipersCurse,Gaze,VipersCurse|VipersCurse,Gaze,PetrifyingGaze|VipersCurse,VipersCurse,PetrifyingGaze|Gaze,Gaze,VipersCurse" },
        { EnemyId.Wyvern, 1, "WyvernStrike|WyvernThreat|WyvernStrike|WyvernThreat|WyvernStrike|WyvernThreat" },
        { EnemyId.FallenShepherd, 1, "FallenShepherdPhase1|FallenShepherdCrooksScar,FallenShepherdCowTheFlock,FallenShepherdBreakFaith|FallenShepherdPhase1|FallenShepherdCowTheFlock,FallenShepherdBreakFaith,FallenShepherdCrooksScar|FallenShepherdPhase1|FallenShepherdBloodletting,FallenShepherdBreakFaith,FallenShepherdCrooksScar" },
        { EnemyId.FallenShepherd, 2, "FallenShepherdPhase2|FallenShepherdCowTheFlock,FallenShepherdShepherdsVigil,FallenShepherdHush|FallenShepherdPhase2|FallenShepherdCrooksScar,FallenShepherdCowTheFlock,FallenShepherdShepherdsVigil|FallenShepherdPhase2|FallenShepherdCrooksScar,FallenShepherdCowTheFlock,FallenShepherdShepherdsVigil" },
        { EnemyId.FallenShepherd, 3, "FallenShepherdPhase3|FallenShepherdPhase3|FallenShepherdPhase3|FallenShepherdPurgeTheHeretic|FallenShepherdFinalSermon|FallenShepherdPurgeTheHeretic" },
        { EnemyId.TrainingDemon, 1, "TrainingStrike|TrainingStrike|TrainingStrike|TrainingStrike|TrainingStrike|TrainingStrike" },
    };

    [Theory]
    [MemberData(nameof(FrozenPlanningTraces))]
    public void Every_enemy_has_a_frozen_six_turn_planning_trace(EnemyId enemy, int phase, string expected)
    {
        Assert.Equal(expected, PlanTurns(enemy, phase, seed: 0xC30UL, turns: 6));
    }

    [Fact]
    public void Every_planned_attack_belongs_to_the_enemy_authored_arsenal()
    {
        foreach (EnemyId enemy in Enum.GetValues<EnemyId>())
        {
            if (!GeneratedEnemyCatalog.IsDefined(enemy)) continue;
            foreach (EnemyPhaseDefinition phase in GeneratedEnemyCatalog.GetDefinition(enemy).Phases)
            {
                Assert.NotEmpty(phase.Arsenal);
                string trace = PlanTurns(enemy, phase.Phase, seed: 0xC30UL, turns: 6);
                foreach (string attackName in trace.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries))
                {
                    Assert.True(Enum.TryParse(attackName, out EnemyAttackId attack));
                    Assert.Contains(attack, phase.Arsenal);
                    Assert.True(GeneratedEnemyAttackCatalog.IsDefined(attack));
                }
            }
        }
    }

    [Fact]
    public void Horde_without_tutorial_facts_uses_pounce_and_section_eight_turn_two_uses_guided_attack()
    {
        Assert.Equal("Pounce", PlanTurns(EnemyId.Horde, phase: 1, seed: 4, turns: 1));

        RuleFact[] tutorialFacts =
        [
            new(RuleFactIds.TutorialSection, 8),
            new(RuleFactIds.TutorialTurn, 2),
        ];
        Assert.Equal(
            "TutorialHordeStrike6",
            PlanTurns(EnemyId.Horde, phase: 1, seed: 4, turns: 1, tutorialFacts));
    }

    private static string PlanTurns(
        EnemyId enemy,
        int phase,
        ulong seed,
        int turns,
        RuleFact[]? facts = null)
    {
        World world = new(GeneratedComponentRegistry.Create());
        var commands = new RuleCommandBuffer(32);
        var results = new RuleHandlerResult[16];
        var plan = new EnemyAttackId[8];
        EnemyPlanningMemory memory = new() { Phase = phase };
        var turnTraces = new string[turns];

        for (var turn = 1; turn <= turns; turn++)
        {
            commands.Clear();
            Array.Clear(results);
            Array.Clear(plan);
            var resultState = new RuleResultWriterState();
            var planState = new EnemyPlanWriterState();
            RuleRandomState random = RuleRandomState.FromSeed(seed + (ulong)(turn * 131 + memory.Phase * 17));
            var input = new EnemyHandlerInput(
                new RuleInvocationId(turn),
                new EntityId(1, 1),
                enemy,
                default,
                new RuleTriggerEnvelope(RuleTriggerKind.Passive, RuleTriggerIds.DefinitionLifecycle, default),
                EnemyHandlerFlags.Planning,
                RulePhase.EnemyStart,
                turn,
                1,
                default,
                memory,
                TargetHandle.Player);
            var context = new EnemyHandlerContext(
                world.AsReadOnly(),
                commands.Writer,
                in input,
                facts ?? [],
                ReadOnlySpan<EntityId>.Empty,
                results,
                plan,
                ref memory,
                ref resultState,
                ref planState,
                ref random);

            Assert.True(GeneratedEnemyCatalog.Dispatch(enemy, ref context));
            turnTraces[turn - 1] = string.Join(',', context.Plan.WrittenSpan.ToArray());
        }

        return string.Join('|', turnTraces);
    }
}
