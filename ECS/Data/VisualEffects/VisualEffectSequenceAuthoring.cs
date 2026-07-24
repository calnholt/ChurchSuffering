using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Input;

namespace ChurchSuffering.ECS.Data.VisualEffects;

/// <summary>
/// Compiles explicit per-definition visual direction into runtime sequences.
/// Display names and rules text are deliberately excluded from authoring decisions.
/// </summary>
public static class VisualEffectSequenceAuthoring
{
	private enum EffectWeight { Light, Medium, Heavy, Epic }
	private enum CardStyle { Slash, Thrust, Heavy, HolyStrike, FireStrike, FrostStrike, BloodStrike, ShadowStrike, Whirlwind, ThrownBlades, Support, HolySupport, Guard, Ritual, FireSupport, FrostSupport, ArcaneSupport }
	private enum EnemyStyle { Slash, Heavy, Claw, Bite, Rock, Arrow, Fire, Frost, Shadow, Poison, Arcane }

	private readonly record struct CardDirection(CardStyle Style, EffectWeight Weight, VisualEffectModule Accent, float TempoOffset);
	private readonly record struct EnemyDirection(EnemyStyle Style, EffectWeight Weight, VisualEffectModule Accent, float TempoOffset);

	public static VisualEffectSequence ForCard(CardBase card)
	{
		if (card == null) return new VisualEffectSequence().WithBeats();
		var sequence = !GameIdExtensions.TryParseCardId(card.CardId, out var id)
			? CreateStructuredCardFallback(card)
			: CreateCardSequence(id, CardDirectionFor(id));
		return RepeatPrimaryBeatForMultiHitCard(card, sequence);
	}

	private static VisualEffectSequence RepeatPrimaryBeatForMultiHitCard(CardBase card, VisualEffectSequence sequence)
	{
		if (card.MultiHitCount <= 1
			|| sequence?.Beats.Count != 1
			|| sequence.Beats[0].TargetRole != VisualEffectTargetRole.Enemy)
		{
			return sequence;
		}

		var primary = sequence.Beats[0];
		var beats = new VisualEffectBeat[card.MultiHitCount];
		for (int hitIndex = 0; hitIndex < beats.Length; hitIndex++)
		{
			float desiredImpact = card.FirstHitDelaySeconds + hitIndex * card.HitIntervalSeconds;
			beats[hitIndex] = new VisualEffectBeat
			{
				Id = $"{primary.Id}_hit_{hitIndex + 1}",
				TargetRole = primary.TargetRole,
				DelaySeconds = Math.Max(0f, desiredImpact - primary.ImpactTimeSeconds),
				DurationSeconds = primary.DurationSeconds,
				ImpactTimeSeconds = primary.ImpactTimeSeconds,
				HitStopStartSeconds = primary.HitStopStartSeconds,
				HitStopDurationSeconds = primary.HitStopDurationSeconds,
				Intensity = primary.Intensity,
				ParticleMultiplier = primary.ParticleMultiplier,
				Palette = primary.Palette,
				StartSfx = primary.StartSfx,
				ImpactSfx = primary.ImpactSfx,
				StartSfxVolume = primary.StartSfxVolume,
				ImpactSfxVolume = primary.ImpactSfxVolume,
				StartSfxPitch = primary.StartSfxPitch,
				ImpactSfxPitch = primary.ImpactSfxPitch,
				DrivesGameplayImpact = primary.DrivesGameplayImpact,
				ImpactRumbleProfile = primary.ImpactRumbleProfile,
				ImpactRumbleScale = primary.ImpactRumbleScale,
			}.WithModules(primary.Modules.ToArray());
		}

		return new VisualEffectSequence().WithBeats(beats);
	}

	public static VisualEffectSequence ForEnemyAttack(EnemyAttackBase attack)
	{
		if (attack == null) return new VisualEffectSequence().WithBeats();
		return CreateEnemySequence(attack.Id, EnemyDirectionFor(attack.Id));
	}

	internal static bool HasExplicitCardChoreography(CardId id) => id switch
	{
		CardId.Absolution or CardId.AboundingGrace or CardId.AnsweredPrayer or CardId.ArkOfTheCovenant or CardId.BatteringBlow or CardId.BattleScars or CardId.BlessedOnslaught or CardId.BloodPrice or CardId.Burn or CardId.CarpeDiem or CardId.Comeback or CardId.Consecrate or CardId.Courageous or CardId.CrimsonRite or CardId.Crusade or CardId.Curse or CardId.Hex or CardId.Dagger or CardId.DeusVult or CardId.DivineProtection or CardId.DowseWithHolyWater or CardId.EmberHarvest or CardId.EvenTemper or CardId.Exaltation or CardId.Excavate or CardId.Fervor or CardId.ForgeStrike or CardId.FullForce or CardId.Fury or CardId.Graveward or CardId.HoldTheLine or CardId.Hammer or CardId.HiddenKunai or CardId.Impale or CardId.IncreaseFaith or CardId.IronCovenant or CardId.Kunai or CardId.Lacerate or CardId.LitanyOfWrath or CardId.Mantlet or CardId.MaleficRite or CardId.MarkOfAnathema or CardId.OathGuard or CardId.QuickWit or CardId.RallyTheFaithful or CardId.RelentlessStrike or CardId.PierceThrough or CardId.PouchOfKunai or CardId.Purge or CardId.Ravage or CardId.RazorStorm or CardId.Reckoning or CardId.Reap or CardId.RecklessBarrage or CardId.RenounceAndHone or CardId.Retaliate or CardId.Sacrifice or CardId.SerpentCrush or CardId.Seize or CardId.ShieldbearersVigil or CardId.ShieldOfFaith or CardId.Smite or CardId.Stab or CardId.SteadfastResolve or CardId.Stalwart or CardId.SteelPrayer or CardId.SteelTheSpirit or CardId.StokedAssault or CardId.Strike or CardId.SuddenThrust or CardId.StokeTheFurnace or CardId.Sword or CardId.SwordIntoShield or CardId.TemperTheBlade or CardId.Tempest or CardId.Thaw or CardId.UnburdenedStrike or CardId.VanguardsPromise or CardId.Vindicate or CardId.WardingPledge or CardId.Whirlwind or CardId.ZealousVow => true,
		_ => false
	};

	internal static bool HasExplicitEnemyAttackChoreography(EnemyAttackId id) => id switch
	{
		EnemyAttackId.PummelIntoSubmission or EnemyAttackId.TreeStomp or EnemyAttackId.SlamTrunk or EnemyAttackId.FakeOut or EnemyAttackId.Thud or EnemyAttackId.BoneStrike or EnemyAttackId.BurialStrike or EnemyAttackId.SearingStrike or EnemyAttackId.RimeStrike or EnemyAttackId.DreadStrike or EnemyAttackId.Sweep or EnemyAttackId.Calcify or EnemyAttackId.SkullCrusher or EnemyAttackId.PiercingShot or EnemyAttackId.WeatheringShot or EnemyAttackId.QuickShot or EnemyAttackId.Snipe or EnemyAttackId.Slice or EnemyAttackId.Dice or EnemyAttackId.DuskFlick or EnemyAttackId.CloakedReaver or EnemyAttackId.SilencingStab or EnemyAttackId.SharpenBlade or EnemyAttackId.ShadowStep or EnemyAttackId.NightveilGuillotine or EnemyAttackId.RazorMaw or EnemyAttackId.ScorchingClaw or EnemyAttackId.InfernalExecution or EnemyAttackId.Pounce or EnemyAttackId.TutorialHordeStrike or EnemyAttackId.TutorialHordeStrike3 or EnemyAttackId.TutorialHordeStrike5 or EnemyAttackId.TutorialHordeStrike6 or EnemyAttackId.TutorialHordeStrike7 or EnemyAttackId.TutorialHordeStrike8 or EnemyAttackId.TutorialHordeStrike9 or EnemyAttackId.SandBlast or EnemyAttackId.SandStorm or EnemyAttackId.TutorialSandBlast or EnemyAttackId.TutorialSandStorm or EnemyAttackId.SandPound or EnemyAttackId.SandSlam or EnemyAttackId.SuffocatingSilk or EnemyAttackId.MandibleBreaker or EnemyAttackId.Entomb or EnemyAttackId.Mummify or EnemyAttackId.Leprosy or EnemyAttackId.VelvetFangs or EnemyAttackId.SoulSiphon or EnemyAttackId.EnthrallingGaze or EnemyAttackId.CrushingAdoration or EnemyAttackId.TeasingNip or EnemyAttackId.SawtoothRend or EnemyAttackId.DustStorm or EnemyAttackId.StrangeForce or EnemyAttackId.IcyBlade or EnemyAttackId.FrozenClaw or EnemyAttackId.FrostEater or EnemyAttackId.GlacialStrike or EnemyAttackId.GlacialBlast or EnemyAttackId.Cinderbolt or EnemyAttackId.InsidiousBolt or EnemyAttackId.Rage or EnemyAttackId.TrainingStrike or EnemyAttackId.ShadowStrike or EnemyAttackId.DissipatingDarkness or EnemyAttackId.SnuffOutTheLight or EnemyAttackId.NightFall or EnemyAttackId.FromTheShadows or EnemyAttackId.UmbraSlice or EnemyAttackId.TremorStrike or EnemyAttackId.StoneBarrage or EnemyAttackId.EarthenWall or EnemyAttackId.HaveNoMercy or EnemyAttackId.WardenSeal or EnemyAttackId.VenomLash or EnemyAttackId.ToxicDeluge or EnemyAttackId.WyvernStrike or EnemyAttackId.WyvernThreat or EnemyAttackId.FallenShepherdPhase1 or EnemyAttackId.FallenShepherdCrooksScar or EnemyAttackId.FallenShepherdBreakFaith or EnemyAttackId.FallenShepherdBloodletting or EnemyAttackId.FallenShepherdCowTheFlock or EnemyAttackId.FallenShepherdPhase2 or EnemyAttackId.FallenShepherdShepherdsVigil or EnemyAttackId.FallenShepherdHush or EnemyAttackId.FallenShepherdPhase3 or EnemyAttackId.FallenShepherdPurgeTheHeretic or EnemyAttackId.FallenShepherdFearTheShepherd or EnemyAttackId.FallenShepherdFinalSermon or EnemyAttackId.WritOfMalice or EnemyAttackId.ChronoSlice or EnemyAttackId.AeonWard => true,
		_ => false
	};

	private static CardDirection CardDirectionFor(CardId id) => id switch
	{
		CardId.Absolution => Card(CardStyle.HolyStrike, EffectWeight.Epic, VisualEffectModule.Beam, 0.06f),
		CardId.AboundingGrace => Card(CardStyle.HolySupport, EffectWeight.Medium, VisualEffectModule.Halo, 0.08f),
		CardId.AnsweredPrayer => Card(CardStyle.HolyStrike, EffectWeight.Heavy, VisualEffectModule.CrossBloom, 0.04f),
		CardId.ArkOfTheCovenant => Card(CardStyle.HolySupport, EffectWeight.Heavy, VisualEffectModule.ResourceMotes, 0.03f),
		CardId.BatteringBlow => Card(CardStyle.Heavy, EffectWeight.Heavy, VisualEffectModule.Shockwave, -0.02f),
		CardId.BattleScars => Card(CardStyle.BloodStrike, EffectWeight.Heavy, VisualEffectModule.Shards, 0.04f),
		CardId.BlessedOnslaught => Card(CardStyle.Whirlwind, EffectWeight.Epic, VisualEffectModule.Rays, 0.08f),
		CardId.BloodPrice => Card(CardStyle.BloodStrike, EffectWeight.Epic, VisualEffectModule.SoulSiphon, 0.08f),
		CardId.Burn => Card(CardStyle.FireStrike, EffectWeight.Medium, VisualEffectModule.FlameBurst, -0.01f),
		CardId.CarpeDiem => Card(CardStyle.HolySupport, EffectWeight.Heavy, VisualEffectModule.Rays, 0.07f),
		CardId.Comeback => Card(CardStyle.Thrust, EffectWeight.Medium, VisualEffectModule.Rays, -0.02f),
		CardId.Consecrate => Card(CardStyle.HolyStrike, EffectWeight.Heavy, VisualEffectModule.CrossBloom, 0.02f),
		CardId.Courageous => Card(CardStyle.Support, EffectWeight.Heavy, VisualEffectModule.Rays, 0.05f),
		CardId.CrimsonRite => Card(CardStyle.BloodStrike, EffectWeight.Medium, VisualEffectModule.SoulSiphon, 0.01f),
		CardId.Crusade => Card(CardStyle.HolyStrike, EffectWeight.Heavy, VisualEffectModule.Rays, 0.09f),
		CardId.Curse => Card(CardStyle.ShadowStrike, EffectWeight.Light, VisualEffectModule.ColorDrain, 0.11f),
		CardId.Hex => Card(CardStyle.ShadowStrike, EffectWeight.Medium, VisualEffectModule.ColorDrain, 0.03f),
		CardId.Dagger => Card(CardStyle.Thrust, EffectWeight.Light, VisualEffectModule.ThrownBladeVolley, -0.06f),
		CardId.DeusVult => Card(CardStyle.HolyStrike, EffectWeight.Epic, VisualEffectModule.PunchZoom, 0.12f),
		CardId.DivineProtection => Card(CardStyle.Guard, EffectWeight.Medium, VisualEffectModule.Halo, -0.02f),
		CardId.DowseWithHolyWater => Card(CardStyle.HolySupport, EffectWeight.Medium, VisualEffectModule.Beam, 0.01f),
		CardId.EmberHarvest => Card(CardStyle.FireStrike, EffectWeight.Heavy, VisualEffectModule.ResourceMotes, 0.04f),
		CardId.EvenTemper => Card(CardStyle.Guard, EffectWeight.Medium, VisualEffectModule.ShieldWard, 0.02f),
		CardId.Exaltation => Card(CardStyle.HolyStrike, EffectWeight.Heavy, VisualEffectModule.WhiteWash, -0.03f),
		CardId.Excavate => Card(CardStyle.Heavy, EffectWeight.Epic, VisualEffectModule.RockBlast, 0.05f),
		CardId.Fervor => Card(CardStyle.FireStrike, EffectWeight.Heavy, VisualEffectModule.Rays, -0.04f),
		CardId.ForgeStrike => Card(CardStyle.Heavy, EffectWeight.Heavy, VisualEffectModule.FlameBurst, 0.00f),
		CardId.FullForce => Card(CardStyle.Heavy, EffectWeight.Epic, VisualEffectModule.Shockwave, 0.06f),
		CardId.Fury => Card(CardStyle.FireSupport, EffectWeight.Medium, VisualEffectModule.RedVignette, 0.06f),
		CardId.Graveward => Card(CardStyle.ShadowStrike, EffectWeight.Heavy, VisualEffectModule.ShieldWard, 0.03f),
		CardId.HoldTheLine => Card(CardStyle.Guard, EffectWeight.Heavy, VisualEffectModule.PunchZoom, 0.08f),
		CardId.Hammer => Card(CardStyle.Heavy, EffectWeight.Medium, VisualEffectModule.Cracks, -0.05f),
		CardId.HiddenKunai => Card(CardStyle.ThrownBlades, EffectWeight.Light, VisualEffectModule.SmokeBlobs, -0.08f),
		CardId.Impale => Card(CardStyle.Thrust, EffectWeight.Heavy, VisualEffectModule.SlashBand, 0.02f),
		CardId.IncreaseFaith => Card(CardStyle.HolySupport, EffectWeight.Medium, VisualEffectModule.CrossBloom, -0.03f),
		CardId.IronCovenant => Card(CardStyle.Heavy, EffectWeight.Heavy, VisualEffectModule.ShieldShatter, 0.07f),
		CardId.Kunai => Card(CardStyle.ThrownBlades, EffectWeight.Light, VisualEffectModule.Debris, -0.07f),
		CardId.Lacerate => Card(CardStyle.Slash, EffectWeight.Medium, VisualEffectModule.RedVignette, 0.03f),
		CardId.LitanyOfWrath => Card(CardStyle.HolySupport, EffectWeight.Heavy, VisualEffectModule.RedVignette, 0.10f),
		CardId.Mantlet => Card(CardStyle.Guard, EffectWeight.Medium, VisualEffectModule.Debris, -0.01f),
		CardId.MaleficRite => Card(CardStyle.Ritual, EffectWeight.Heavy, VisualEffectModule.ShadowTendrils, 0.09f),
		CardId.MarkOfAnathema => Card(CardStyle.ShadowStrike, EffectWeight.Heavy, VisualEffectModule.ColorDrain, 0.05f),
		CardId.OathGuard => Card(CardStyle.Guard, EffectWeight.Heavy, VisualEffectModule.ShieldWard, 0.04f),
		CardId.QuickWit => Card(CardStyle.Thrust, EffectWeight.Light, VisualEffectModule.Rays, -0.09f),
		CardId.RallyTheFaithful => Card(CardStyle.HolySupport, EffectWeight.Epic, VisualEffectModule.CrossBloom, 0.13f),
		CardId.RelentlessStrike => Card(CardStyle.Slash, EffectWeight.Heavy, VisualEffectModule.SlashBand, 0.04f),
		CardId.PierceThrough => Card(CardStyle.Thrust, EffectWeight.Heavy, VisualEffectModule.ShieldShatter, -0.01f),
		CardId.PouchOfKunai => Card(CardStyle.Support, EffectWeight.Medium, VisualEffectModule.ThrownBladeVolley, 0.02f),
		CardId.Purge => Card(CardStyle.HolyStrike, EffectWeight.Heavy, VisualEffectModule.ColorDrain, 0.07f),
		CardId.Ravage => Card(CardStyle.Heavy, EffectWeight.Epic, VisualEffectModule.RedVignette, 0.10f),
		CardId.RazorStorm => Card(CardStyle.Whirlwind, EffectWeight.Epic, VisualEffectModule.ThrownBladeVolley, 0.12f),
		CardId.Reckoning => Card(CardStyle.Heavy, EffectWeight.Epic, VisualEffectModule.CrossBloom, 0.14f),
		CardId.Reap => Card(CardStyle.ShadowStrike, EffectWeight.Heavy, VisualEffectModule.SoulSiphon, 0.06f),
		CardId.RecklessBarrage => Card(CardStyle.Whirlwind, EffectWeight.Heavy, VisualEffectModule.Debris, 0.04f),
		CardId.RenounceAndHone => Card(CardStyle.Ritual, EffectWeight.Heavy, VisualEffectModule.ResourceMotes, 0.04f),
		CardId.Retaliate => Card(CardStyle.FireStrike, EffectWeight.Medium, VisualEffectModule.FlameBurst, 0.01f),
		CardId.Sacrifice => Card(CardStyle.Ritual, EffectWeight.Epic, VisualEffectModule.SoulSiphon, 0.11f),
		CardId.SerpentCrush => Card(CardStyle.Heavy, EffectWeight.Medium, VisualEffectModule.PoisonCloud, 0.00f),
		CardId.Seize => Card(CardStyle.Thrust, EffectWeight.Light, VisualEffectModule.HitFlash, -0.05f),
		CardId.ShieldbearersVigil => Card(CardStyle.HolyStrike, EffectWeight.Light, VisualEffectModule.ShieldWard, -0.07f),
		CardId.ShieldOfFaith => Card(CardStyle.Guard, EffectWeight.Heavy, VisualEffectModule.WhiteWash, 0.06f),
		CardId.Smite => Card(CardStyle.HolyStrike, EffectWeight.Light, VisualEffectModule.CrossBloom, -0.04f),
		CardId.Stab => Card(CardStyle.Thrust, EffectWeight.Medium, VisualEffectModule.RedVignette, -0.02f),
		CardId.SteadfastResolve => Card(CardStyle.Support, EffectWeight.Medium, VisualEffectModule.ShieldWard, 0.03f),
		CardId.Stalwart => Card(CardStyle.Guard, EffectWeight.Heavy, VisualEffectModule.Cracks, 0.05f),
		CardId.SteelPrayer => Card(CardStyle.HolySupport, EffectWeight.Heavy, VisualEffectModule.Rays, 0.06f),
		CardId.SteelTheSpirit => Card(CardStyle.Ritual, EffectWeight.Medium, VisualEffectModule.ShieldWard, 0.01f),
		CardId.StokedAssault => Card(CardStyle.FireStrike, EffectWeight.Medium, VisualEffectModule.SlashBand, -0.03f),
		CardId.Strike => Card(CardStyle.Slash, EffectWeight.Light, VisualEffectModule.Debris, -0.06f),
		CardId.SuddenThrust => Card(CardStyle.Thrust, EffectWeight.Light, VisualEffectModule.Rays, -0.10f),
		CardId.StokeTheFurnace => Card(CardStyle.FireStrike, EffectWeight.Medium, VisualEffectModule.RedVignette, 0.05f),
		CardId.Sword => Card(CardStyle.Slash, EffectWeight.Medium, VisualEffectModule.SwordArc, -0.03f),
		CardId.SwordIntoShield => Card(CardStyle.HolySupport, EffectWeight.Medium, VisualEffectModule.ShieldWard, 0.137f),
		CardId.TemperTheBlade => Card(CardStyle.Support, EffectWeight.Medium, VisualEffectModule.SwordArc, 0.00f),
		CardId.Tempest => Card(CardStyle.Whirlwind, EffectWeight.Medium, VisualEffectModule.Rays, 0.02f),
		CardId.Thaw => Card(CardStyle.FrostStrike, EffectWeight.Medium, VisualEffectModule.ResourceMotes, 0.08f),
		CardId.UnburdenedStrike => Card(CardStyle.Slash, EffectWeight.Epic, VisualEffectModule.WhiteWash, 0.07f),
		CardId.VanguardsPromise => Card(CardStyle.HolyStrike, EffectWeight.Light, VisualEffectModule.ResourceMotes, -0.02f),
		CardId.Vindicate => Card(CardStyle.HolyStrike, EffectWeight.Epic, VisualEffectModule.RedVignette, 0.15f),
		CardId.WardingPledge => Card(CardStyle.HolyStrike, EffectWeight.Medium, VisualEffectModule.ShieldWard, 0.04f),
		CardId.Whirlwind => Card(CardStyle.Whirlwind, EffectWeight.Heavy, VisualEffectModule.SlashBand, 0.06f),
		CardId.ZealousVow => Card(CardStyle.HolySupport, EffectWeight.Heavy, VisualEffectModule.SwordArc, 0.09f),
		_ => Card(CardStyle.Slash, EffectWeight.Medium, VisualEffectModule.HitFlash, 0f)
	};

	private static EnemyDirection EnemyDirectionFor(EnemyAttackId id) => id switch
	{
		EnemyAttackId.PummelIntoSubmission => Enemy(EnemyStyle.Heavy, EffectWeight.Epic, VisualEffectModule.Shockwave, 0.08f),
		EnemyAttackId.TreeStomp => Enemy(EnemyStyle.Rock, EffectWeight.Heavy, VisualEffectModule.Cracks, 0.05f),
		EnemyAttackId.SlamTrunk => Enemy(EnemyStyle.Heavy, EffectWeight.Heavy, VisualEffectModule.Debris, 0.02f),
		EnemyAttackId.FakeOut => Enemy(EnemyStyle.Slash, EffectWeight.Light, VisualEffectModule.SmokeBlobs, -0.08f),
		EnemyAttackId.Thud => Enemy(EnemyStyle.Heavy, EffectWeight.Medium, VisualEffectModule.Ring, -0.02f),
		EnemyAttackId.BoneStrike => Enemy(EnemyStyle.Slash, EffectWeight.Medium, VisualEffectModule.Shards, -0.01f),
		EnemyAttackId.BurialStrike => Enemy(EnemyStyle.Rock, EffectWeight.Medium, VisualEffectModule.Debris, -0.015f),
		EnemyAttackId.SearingStrike => Enemy(EnemyStyle.Fire, EffectWeight.Medium, VisualEffectModule.FlameBurst, -0.012f),
		EnemyAttackId.RimeStrike => Enemy(EnemyStyle.Frost, EffectWeight.Medium, VisualEffectModule.FrostBurst, -0.011f),
		EnemyAttackId.DreadStrike => Enemy(EnemyStyle.Shadow, EffectWeight.Medium, VisualEffectModule.ShadowTendrils, -0.013f),
		EnemyAttackId.Sweep => Enemy(EnemyStyle.Slash, EffectWeight.Heavy, VisualEffectModule.SlashBand, 0.04f),
		EnemyAttackId.Calcify => Enemy(EnemyStyle.Rock, EffectWeight.Medium, VisualEffectModule.ShieldWard, 0.03f),
		EnemyAttackId.SkullCrusher => Enemy(EnemyStyle.Heavy, EffectWeight.Epic, VisualEffectModule.Cracks, 0.09f),
		EnemyAttackId.PiercingShot => Enemy(EnemyStyle.Arrow, EffectWeight.Heavy, VisualEffectModule.ShieldShatter, -0.01f),
		EnemyAttackId.WeatheringShot => Enemy(EnemyStyle.Arrow, EffectWeight.Medium, VisualEffectModule.BrittleFracture, 0.01f),
		EnemyAttackId.QuickShot => Enemy(EnemyStyle.Arrow, EffectWeight.Light, VisualEffectModule.ArrowShot, -0.09f),
		EnemyAttackId.Snipe => Enemy(EnemyStyle.Arrow, EffectWeight.Epic, VisualEffectModule.PunchZoom, 0.12f),
		EnemyAttackId.Slice => Enemy(EnemyStyle.Slash, EffectWeight.Light, VisualEffectModule.SwordArc, -0.07f),
		EnemyAttackId.Dice => Enemy(EnemyStyle.Slash, EffectWeight.Medium, VisualEffectModule.CrossSlash, -0.04f),
		EnemyAttackId.DuskFlick => Enemy(EnemyStyle.Shadow, EffectWeight.Light, VisualEffectModule.ColorDrain, -0.05f),
		EnemyAttackId.CloakedReaver => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.SmokeScreen, 0.06f),
		EnemyAttackId.SilencingStab => Enemy(EnemyStyle.Shadow, EffectWeight.Medium, VisualEffectModule.CrossSlash, -0.02f),
		EnemyAttackId.SharpenBlade => Enemy(EnemyStyle.Slash, EffectWeight.Medium, VisualEffectModule.Rays, 0.02f),
		EnemyAttackId.ShadowStep => Enemy(EnemyStyle.Shadow, EffectWeight.Medium, VisualEffectModule.SmokeBlobs, 0.04f),
		EnemyAttackId.NightveilGuillotine => Enemy(EnemyStyle.Shadow, EffectWeight.Epic, VisualEffectModule.SlashBand, 0.14f),
		EnemyAttackId.RazorMaw => Enemy(EnemyStyle.Bite, EffectWeight.Heavy, VisualEffectModule.RedVignette, 0.05f),
		EnemyAttackId.ScorchingClaw => Enemy(EnemyStyle.Fire, EffectWeight.Heavy, VisualEffectModule.ClawSlash, 0.04f),
		EnemyAttackId.InfernalExecution => Enemy(EnemyStyle.Fire, EffectWeight.Epic, VisualEffectModule.Shockwave, 0.13f),
		EnemyAttackId.Pounce => Enemy(EnemyStyle.Claw, EffectWeight.Heavy, VisualEffectModule.PunchZoom, -0.01f),
		EnemyAttackId.TutorialHordeStrike => Enemy(EnemyStyle.Slash, EffectWeight.Light, VisualEffectModule.HitFlash, -0.10f),
		EnemyAttackId.TutorialHordeStrike3 => Enemy(EnemyStyle.Slash, EffectWeight.Light, VisualEffectModule.Debris, -0.08f),
		EnemyAttackId.TutorialHordeStrike5 => Enemy(EnemyStyle.Slash, EffectWeight.Medium, VisualEffectModule.CrossSlash, -0.06f),
		EnemyAttackId.TutorialHordeStrike6 => Enemy(EnemyStyle.Slash, EffectWeight.Medium, VisualEffectModule.SwordArc, -0.04f),
		EnemyAttackId.TutorialHordeStrike7 => Enemy(EnemyStyle.Heavy, EffectWeight.Medium, VisualEffectModule.WhiteWash, -0.025f),
		EnemyAttackId.TutorialHordeStrike8 => Enemy(EnemyStyle.Heavy, EffectWeight.Heavy, VisualEffectModule.Debris, 0.00f),
		EnemyAttackId.TutorialHordeStrike9 => Enemy(EnemyStyle.Heavy, EffectWeight.Heavy, VisualEffectModule.RedVignette, 0.025f),
		EnemyAttackId.SandBlast => Enemy(EnemyStyle.Rock, EffectWeight.Medium, VisualEffectModule.SmokeBlobs, -0.01f),
		EnemyAttackId.SandStorm => Enemy(EnemyStyle.Rock, EffectWeight.Heavy, VisualEffectModule.SmokeScreen, 0.06f),
		EnemyAttackId.TutorialSandBlast => Enemy(EnemyStyle.Rock, EffectWeight.Light, VisualEffectModule.Debris, -0.09f),
		EnemyAttackId.TutorialSandStorm => Enemy(EnemyStyle.Rock, EffectWeight.Medium, VisualEffectModule.SmokeScreen, -0.04f),
		EnemyAttackId.SandPound => Enemy(EnemyStyle.Rock, EffectWeight.Heavy, VisualEffectModule.Ring, 0.03f),
		EnemyAttackId.SandSlam => Enemy(EnemyStyle.Rock, EffectWeight.Epic, VisualEffectModule.Shockwave, 0.10f),
		EnemyAttackId.SuffocatingSilk => Enemy(EnemyStyle.Poison, EffectWeight.Heavy, VisualEffectModule.SmokeScreen, 0.07f),
		EnemyAttackId.MandibleBreaker => Enemy(EnemyStyle.Bite, EffectWeight.Heavy, VisualEffectModule.ShieldShatter, 0.02f),
		EnemyAttackId.Entomb => Enemy(EnemyStyle.Rock, EffectWeight.Heavy, VisualEffectModule.BrittleFracture, 0.06f),
		EnemyAttackId.Mummify => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.SmokeBlobs, 0.08f),
		EnemyAttackId.Leprosy => Enemy(EnemyStyle.Poison, EffectWeight.Medium, VisualEffectModule.BrittleFracture, 0.03f),
		EnemyAttackId.VelvetFangs => Enemy(EnemyStyle.Bite, EffectWeight.Heavy, VisualEffectModule.SoulSiphon, 0.07f),
		EnemyAttackId.SoulSiphon => Enemy(EnemyStyle.Shadow, EffectWeight.Epic, VisualEffectModule.SoulSiphon, 0.13f),
		EnemyAttackId.EnthrallingGaze => Enemy(EnemyStyle.Arcane, EffectWeight.Medium, VisualEffectModule.Rays, 0.05f),
		EnemyAttackId.CrushingAdoration => Enemy(EnemyStyle.Arcane, EffectWeight.Heavy, VisualEffectModule.RedVignette, 0.09f),
		EnemyAttackId.TeasingNip => Enemy(EnemyStyle.Bite, EffectWeight.Light, VisualEffectModule.RedVignette, -0.06f),
		EnemyAttackId.SawtoothRend => Enemy(EnemyStyle.Claw, EffectWeight.Heavy, VisualEffectModule.BrittleFracture, 0.04f),
		EnemyAttackId.DustStorm => Enemy(EnemyStyle.Rock, EffectWeight.Epic, VisualEffectModule.SmokeScreen, 0.12f),
		EnemyAttackId.StrangeForce => Enemy(EnemyStyle.Arcane, EffectWeight.Heavy, VisualEffectModule.ColorDrain, 0.08f),
		EnemyAttackId.IcyBlade => Enemy(EnemyStyle.Frost, EffectWeight.Medium, VisualEffectModule.SwordArc, -0.01f),
		EnemyAttackId.FrozenClaw => Enemy(EnemyStyle.Frost, EffectWeight.Heavy, VisualEffectModule.ClawSlash, 0.04f),
		EnemyAttackId.FrostEater => Enemy(EnemyStyle.Frost, EffectWeight.Heavy, VisualEffectModule.FrostBind, 0.06f),
		EnemyAttackId.GlacialStrike => Enemy(EnemyStyle.Frost, EffectWeight.Heavy, VisualEffectModule.Shards, 0.03f),
		EnemyAttackId.GlacialBlast => Enemy(EnemyStyle.Frost, EffectWeight.Epic, VisualEffectModule.Shockwave, 0.11f),
		EnemyAttackId.Cinderbolt => Enemy(EnemyStyle.Fire, EffectWeight.Medium, VisualEffectModule.EnergyBolt, -0.02f),
		EnemyAttackId.InsidiousBolt => Enemy(EnemyStyle.Fire, EffectWeight.Heavy, VisualEffectModule.ColorDrain, 0.05f),
		EnemyAttackId.Rage => Enemy(EnemyStyle.Heavy, EffectWeight.Heavy, VisualEffectModule.RedVignette, 0.09f),
		EnemyAttackId.TrainingStrike => Enemy(EnemyStyle.Slash, EffectWeight.Light, VisualEffectModule.Debris, -0.05f),
		EnemyAttackId.ShadowStrike => Enemy(EnemyStyle.Shadow, EffectWeight.Medium, VisualEffectModule.CrossSlash, 0.00f),
		EnemyAttackId.DissipatingDarkness => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.ColorDrain, 0.07f),
		EnemyAttackId.SnuffOutTheLight => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.SmokeScreen, 0.05f),
		EnemyAttackId.NightFall => Enemy(EnemyStyle.Shadow, EffectWeight.Epic, VisualEffectModule.RedVignette, 0.12f),
		EnemyAttackId.FromTheShadows => Enemy(EnemyStyle.Shadow, EffectWeight.Medium, VisualEffectModule.SmokeBlobs, -0.03f),
		EnemyAttackId.UmbraSlice => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.SlashBand, 0.02f),
		EnemyAttackId.TremorStrike => Enemy(EnemyStyle.Rock, EffectWeight.Heavy, VisualEffectModule.Shockwave, 0.04f),
		EnemyAttackId.StoneBarrage => Enemy(EnemyStyle.Rock, EffectWeight.Epic, VisualEffectModule.Shards, 0.10f),
		EnemyAttackId.EarthenWall => Enemy(EnemyStyle.Rock, EffectWeight.Heavy, VisualEffectModule.ShieldWard, 0.08f),
		EnemyAttackId.HaveNoMercy => Enemy(EnemyStyle.Slash, EffectWeight.Heavy, VisualEffectModule.RedVignette, 0.06f),
		EnemyAttackId.WardenSeal => Enemy(EnemyStyle.Arcane, EffectWeight.Heavy, VisualEffectModule.SealStamp, 0.06f),
		EnemyAttackId.VenomLash => Enemy(EnemyStyle.Poison, EffectWeight.Medium, VisualEffectModule.PoisonCloud, 0.03f),
		EnemyAttackId.ToxicDeluge => Enemy(EnemyStyle.Poison, EffectWeight.Heavy, VisualEffectModule.SmokeScreen, 0.08f),
		EnemyAttackId.WyvernStrike => Enemy(EnemyStyle.Claw, EffectWeight.Heavy, VisualEffectModule.Shards, 0.03f),
		EnemyAttackId.WyvernThreat => Enemy(EnemyStyle.Fire, EffectWeight.Heavy, VisualEffectModule.Rays, 0.09f),
		EnemyAttackId.FallenShepherdPhase1 => Enemy(EnemyStyle.Heavy, EffectWeight.Heavy, VisualEffectModule.ShieldWard, 0.06f),
		EnemyAttackId.FallenShepherdCrooksScar => Enemy(EnemyStyle.Slash, EffectWeight.Heavy, VisualEffectModule.RedVignette, 0.04f),
		EnemyAttackId.FallenShepherdBreakFaith => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.BrittleFracture, 0.08f),
		EnemyAttackId.FallenShepherdBloodletting => Enemy(EnemyStyle.Slash, EffectWeight.Heavy, VisualEffectModule.SoulSiphon, 0.07f),
		EnemyAttackId.FallenShepherdCowTheFlock => Enemy(EnemyStyle.Shadow, EffectWeight.Medium, VisualEffectModule.RedVignette, 0.03f),
		EnemyAttackId.FallenShepherdPhase2 => Enemy(EnemyStyle.Heavy, EffectWeight.Epic, VisualEffectModule.Shockwave, 0.11f),
		EnemyAttackId.FallenShepherdShepherdsVigil => Enemy(EnemyStyle.Arcane, EffectWeight.Heavy, VisualEffectModule.ShieldWard, 0.06f),
		EnemyAttackId.FallenShepherdHush => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.ColorDrain, 0.05f),
		EnemyAttackId.FallenShepherdPhase3 => Enemy(EnemyStyle.Heavy, EffectWeight.Epic, VisualEffectModule.Cracks, 0.13f),
		EnemyAttackId.FallenShepherdPurgeTheHeretic => Enemy(EnemyStyle.Fire, EffectWeight.Heavy, VisualEffectModule.FlameBurst, 0.08f),
		EnemyAttackId.FallenShepherdFearTheShepherd => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.SmokeScreen, 0.10f),
		EnemyAttackId.FallenShepherdFinalSermon => Enemy(EnemyStyle.Arcane, EffectWeight.Epic, VisualEffectModule.Rays, 0.15f),
		EnemyAttackId.WritOfMalice => Enemy(EnemyStyle.Shadow, EffectWeight.Heavy, VisualEffectModule.ColorDrain, 0.04f),
		EnemyAttackId.ChronoSlice => Enemy(EnemyStyle.Frost, EffectWeight.Heavy, VisualEffectModule.SwordArc, 0.03f),
		EnemyAttackId.AeonWard => Enemy(EnemyStyle.Frost, EffectWeight.Medium, VisualEffectModule.ShieldWard, 0.06f),
		_ => Enemy(EnemyStyle.Slash, EffectWeight.Medium, VisualEffectModule.HitFlash, 0f)
	};

	private static CardDirection Card(CardStyle style, EffectWeight weight, VisualEffectModule accent, float tempoOffset) => new(style, weight, accent, tempoOffset);
	private static EnemyDirection Enemy(EnemyStyle style, EffectWeight weight, VisualEffectModule accent, float tempoOffset) => new(style, weight, accent, tempoOffset);

	private static VisualEffectSequence CreateCardSequence(CardId id, CardDirection direction)
	{
		bool attack = IsAttack(direction.Style);
		var modules = CardModules(direction.Style).ToList();
		AddUnique(modules, direction.Accent);
		if (attack) AddUnique(modules, VisualEffectModule.TargetShake);
		var timing = WeightTiming(direction.Weight, direction.TempoOffset);
		var beat = new VisualEffectBeat
		{
			Id = $"{id.ToKey()}_primary",
			TargetRole = attack ? VisualEffectTargetRole.Enemy : VisualEffectTargetRole.Player,
			DurationSeconds = timing.duration,
			ImpactTimeSeconds = timing.impact,
			HitStopStartSeconds = timing.hitStop > 0f ? timing.impact : 0f,
			HitStopDurationSeconds = timing.hitStop,
			Intensity = timing.intensity,
			ParticleMultiplier = timing.particles,
			Palette = CardPalette(direction.Style),
			StartSfx = ResolveCardStartSfx(id, attack, direction.Style),
			ImpactSfx = attack ? SfxTrack.SwordImpact : SfxTrack.None,
			StartSfxPitch = Math.Clamp(direction.TempoOffset * 1.8f, -0.3f, 0.3f),
			ImpactRumbleProfile = attack ? WeightRumble(direction.Weight) : SupportRumble(direction.Style),
		}.WithModules(modules.ToArray());
		return new VisualEffectSequence().WithBeats(beat);
	}

	private static VisualEffectSequence CreateEnemySequence(EnemyAttackId id, EnemyDirection direction)
	{
		var modules = EnemyModules(direction.Style).ToList();
		AddUnique(modules, direction.Accent);
		AddUnique(modules, VisualEffectModule.Shake);
		var timing = WeightTiming(direction.Weight, direction.TempoOffset);
		var beat = new VisualEffectBeat
		{
			Id = $"{id.ToKey()}_impact",
			TargetRole = VisualEffectTargetRole.Player,
			DurationSeconds = timing.duration,
			ImpactTimeSeconds = timing.impact,
			HitStopStartSeconds = timing.hitStop > 0f ? timing.impact : 0f,
			HitStopDurationSeconds = timing.hitStop,
			Intensity = timing.intensity,
			ParticleMultiplier = timing.particles,
			Palette = EnemyPalette(direction.Style),
			ImpactSfx = SfxTrack.SwordImpact,
			ImpactSfxPitch = Math.Clamp(direction.TempoOffset * 1.6f, -0.3f, 0.3f),
			DrivesGameplayImpact = true,
			ImpactRumbleProfile = WeightRumble(direction.Weight),
		}.WithModules(modules.ToArray());
		return new VisualEffectSequence().WithBeats(beat);
	}

	private static VisualEffectSequence CreateStructuredCardFallback(CardBase card)
	{
		bool attacksEnemy = card.Type == CardType.Attack || card.Damage > 0 || string.Equals(card.Target, "Enemy", StringComparison.OrdinalIgnoreCase);
		var style = attacksEnemy ? CardStyle.Slash : CardStyle.Support;
		var weight = card.Damage switch { >= 9 => EffectWeight.Epic, >= 6 => EffectWeight.Heavy, >= 3 => EffectWeight.Medium, _ => EffectWeight.Light };
		var direction = Card(style, weight, attacksEnemy ? VisualEffectModule.HitFlash : VisualEffectModule.ResourceMotes, 0f);
		var fallbackId = attacksEnemy ? CardId.Strike : CardId.DivineProtection;
		return CreateCardSequence(fallbackId, direction);
	}

	private static bool IsAttack(CardStyle style) => style is CardStyle.Slash or CardStyle.Thrust or CardStyle.Heavy or CardStyle.HolyStrike or CardStyle.FireStrike or CardStyle.FrostStrike or CardStyle.BloodStrike or CardStyle.ShadowStrike or CardStyle.Whirlwind or CardStyle.ThrownBlades;

	private static RumbleProfile WeightRumble(EffectWeight weight) => weight switch
	{
		EffectWeight.Light => RumbleProfile.LightImpact,
		EffectWeight.Medium => RumbleProfile.MediumImpact,
		EffectWeight.Heavy => RumbleProfile.HeavyImpact,
		_ => RumbleProfile.EpicImpact,
	};

	private static RumbleProfile SupportRumble(CardStyle style) =>
		style == CardStyle.Guard ? RumbleProfile.Guard : RumbleProfile.Soft;

	private static IEnumerable<VisualEffectModule> CardModules(CardStyle style) => style switch
	{
		CardStyle.Slash => [VisualEffectModule.ActorLunge, VisualEffectModule.SwordArc, VisualEffectModule.HitFlash, VisualEffectModule.Debris],
		CardStyle.Thrust => [VisualEffectModule.ActorLunge, VisualEffectModule.CrossSlash, VisualEffectModule.HitFlash],
		CardStyle.Heavy => [VisualEffectModule.ActorLunge, VisualEffectModule.HammerArc, VisualEffectModule.Ring, VisualEffectModule.Debris, VisualEffectModule.Cracks, VisualEffectModule.HitFlash, VisualEffectModule.Shockwave, VisualEffectModule.PunchZoom, VisualEffectModule.HitStop],
		CardStyle.HolyStrike => [VisualEffectModule.ActorLunge, VisualEffectModule.CrossBloom, VisualEffectModule.Rays, VisualEffectModule.Ring, VisualEffectModule.WhiteWash, VisualEffectModule.HitFlash],
		CardStyle.FireStrike => [VisualEffectModule.ActorLunge, VisualEffectModule.EnergyBolt, VisualEffectModule.FlameBurst, VisualEffectModule.HitFlash],
		CardStyle.FrostStrike => [VisualEffectModule.ActorLunge, VisualEffectModule.EnergyBolt, VisualEffectModule.FrostBurst, VisualEffectModule.Shards, VisualEffectModule.HitFlash],
		CardStyle.BloodStrike => [VisualEffectModule.ActorLunge, VisualEffectModule.CrossSlash, VisualEffectModule.RedVignette, VisualEffectModule.HitFlash],
		CardStyle.ShadowStrike => [VisualEffectModule.ActorLunge, VisualEffectModule.ShadowTendrils, VisualEffectModule.ColorDrain, VisualEffectModule.HitFlash],
		CardStyle.Whirlwind => [VisualEffectModule.ActorLunge, VisualEffectModule.SpinSlash, VisualEffectModule.SlashBand, VisualEffectModule.HitFlash],
		CardStyle.ThrownBlades => [VisualEffectModule.ActorLunge, VisualEffectModule.ThrownBladeVolley, VisualEffectModule.HitFlash],
		CardStyle.HolySupport => [VisualEffectModule.ActorSquashStretch, VisualEffectModule.CrossBloom, VisualEffectModule.Halo, VisualEffectModule.Rays, VisualEffectModule.WhiteWash, VisualEffectModule.ResourceMotes],
		CardStyle.Guard => [VisualEffectModule.ActorSquashStretch, VisualEffectModule.ShieldWard, VisualEffectModule.Ring, VisualEffectModule.Halo],
		CardStyle.Ritual => [VisualEffectModule.ActorSquashStretch, VisualEffectModule.RedVignette, VisualEffectModule.Ring, VisualEffectModule.SmokeBlobs, VisualEffectModule.ResourceMotes],
		CardStyle.FireSupport => [VisualEffectModule.ActorSquashStretch, VisualEffectModule.FlameBurst, VisualEffectModule.ResourceMotes],
		CardStyle.FrostSupport => [VisualEffectModule.ActorSquashStretch, VisualEffectModule.FrostBurst, VisualEffectModule.Shards, VisualEffectModule.ResourceMotes],
		CardStyle.ArcaneSupport => [VisualEffectModule.ActorSquashStretch, VisualEffectModule.Ring, VisualEffectModule.Rays, VisualEffectModule.ResourceMotes],
		_ => [VisualEffectModule.ActorSquashStretch, VisualEffectModule.Ring, VisualEffectModule.ResourceMotes]
	};

	private static IEnumerable<VisualEffectModule> EnemyModules(EnemyStyle style) => style switch
	{
		EnemyStyle.Heavy => [VisualEffectModule.ActorLunge, VisualEffectModule.Ring, VisualEffectModule.Debris, VisualEffectModule.Cracks, VisualEffectModule.HitFlash, VisualEffectModule.Shockwave, VisualEffectModule.HitStop],
		EnemyStyle.Claw => [VisualEffectModule.ActorLunge, VisualEffectModule.ClawSlash, VisualEffectModule.SlashBand, VisualEffectModule.HitFlash, VisualEffectModule.Debris],
		EnemyStyle.Bite => [VisualEffectModule.ActorLunge, VisualEffectModule.Bite, VisualEffectModule.RedVignette, VisualEffectModule.HitFlash, VisualEffectModule.HitStop],
		EnemyStyle.Rock => [VisualEffectModule.ActorLunge, VisualEffectModule.RockBlast, VisualEffectModule.Ring, VisualEffectModule.Debris, VisualEffectModule.SmokeBlobs, VisualEffectModule.HitFlash],
		EnemyStyle.Arrow => [VisualEffectModule.ArrowShot, VisualEffectModule.HitFlash],
		EnemyStyle.Fire => [VisualEffectModule.ActorLunge, VisualEffectModule.EnergyBolt, VisualEffectModule.FlameBurst, VisualEffectModule.HitFlash],
		EnemyStyle.Frost => [VisualEffectModule.ActorLunge, VisualEffectModule.FrostBurst, VisualEffectModule.Shards, VisualEffectModule.HitFlash],
		EnemyStyle.Shadow => [VisualEffectModule.ActorLunge, VisualEffectModule.ShadowTendrils, VisualEffectModule.ColorDrain, VisualEffectModule.HitFlash],
		EnemyStyle.Poison => [VisualEffectModule.ActorLunge, VisualEffectModule.PoisonCloud, VisualEffectModule.SmokeBlobs, VisualEffectModule.HitFlash],
		EnemyStyle.Arcane => [VisualEffectModule.ActorLunge, VisualEffectModule.EnergyBolt, VisualEffectModule.Rays, VisualEffectModule.HitFlash],
		_ => [VisualEffectModule.ActorLunge, VisualEffectModule.CrossSlash, VisualEffectModule.SlashBand, VisualEffectModule.HitFlash]
	};

	private static (float duration, float impact, float hitStop, float intensity, float particles) WeightTiming(EffectWeight weight, float tempoOffset)
	{
		var baseValues = weight switch
		{
			EffectWeight.Light => (0.40f, 0.15f, 0.00f, 0.72f, 0.58f),
			EffectWeight.Medium => (0.54f, 0.18f, 0.00f, 0.94f, 0.88f),
			EffectWeight.Heavy => (0.70f, 0.22f, 0.06f, 1.18f, 1.20f),
			_ => (0.88f, 0.27f, 0.09f, 1.46f, 1.58f)
		};
		return (
			Math.Max(0.24f, baseValues.Item1 + tempoOffset),
			Math.Max(0.08f, baseValues.Item2 + tempoOffset * 0.28f),
			baseValues.Item3,
			Math.Max(0.2f, baseValues.Item4 + tempoOffset * 0.9f),
			Math.Max(0.2f, baseValues.Item5 + tempoOffset * 0.7f));
	}

	private static VisualEffectPalette CardPalette(CardStyle style) => style switch
	{
		CardStyle.HolyStrike or CardStyle.HolySupport or CardStyle.Guard => VisualEffectPalette.Holy,
		CardStyle.FireStrike or CardStyle.FireSupport => VisualEffectPalette.Fire,
		CardStyle.FrostStrike or CardStyle.FrostSupport => VisualEffectPalette.Ice,
		CardStyle.BloodStrike or CardStyle.Ritual => VisualEffectPalette.Blood,
		CardStyle.ShadowStrike => VisualEffectPalette.Shadow,
		CardStyle.ArcaneSupport => VisualEffectPalette.Arcane,
		CardStyle.Heavy => VisualEffectPalette.Earth,
		_ => VisualEffectPalette.Physical
	};

	private static VisualEffectPalette EnemyPalette(EnemyStyle style) => style switch
	{
		EnemyStyle.Fire => VisualEffectPalette.Fire,
		EnemyStyle.Frost => VisualEffectPalette.Ice,
		EnemyStyle.Shadow => VisualEffectPalette.Shadow,
		EnemyStyle.Poison => VisualEffectPalette.Poison,
		EnemyStyle.Arcane => VisualEffectPalette.Arcane,
		EnemyStyle.Rock => VisualEffectPalette.Earth,
		_ => VisualEffectPalette.Physical
	};

	private static SfxTrack SupportSfx(CardStyle style) => style == CardStyle.Guard ? SfxTrack.GainAegis : SfxTrack.Prayer;

	private static SfxTrack ResolveCardStartSfx(CardId id, bool attack, CardStyle style) => id switch
	{
		CardId.Courageous => SfxTrack.ChoirChord,
		_ => attack ? SfxTrack.SwordAttack : SupportSfx(style)
	};

	private static void AddUnique(List<VisualEffectModule> modules, VisualEffectModule module)
	{
		if (!modules.Contains(module)) modules.Add(module);
	}
}
