using System;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class VisualEffectSequenceAuthoringTests
{
	[Fact]
	public void Every_registered_card_has_a_non_empty_bespoke_sequence()
	{
		var registered = CardFactory.GetAllCards().Append(new System.Collections.Generic.KeyValuePair<CardId, CardBase>(CardId.Curse, CardFactory.Create(CardId.Curse))).ToList();
		var cards = registered.Select(pair => pair.Value).ToList();
		Assert.Equal(68, cards.Count);
		Assert.All(registered, pair => Assert.True(VisualEffectSequenceAuthoring.HasExplicitCardChoreography(pair.Key), $"Missing explicit choreography for {pair.Key}."));
		Assert.All(cards, card => Assert.NotEmpty(card.VisualEffectSequence.Beats));
		Assert.Equal(cards.Count, cards.Select(VisualSignature).Distinct(StringComparer.Ordinal).Count());
		Assert.All(cards.Where(card => card.Type == CardType.Attack), card =>
		{
			Assert.DoesNotContain(card.VisualEffectSequence.Beats, beat => beat.Modules.Contains(VisualEffectModule.ActorSquashStretch));
			Assert.DoesNotContain(card.VisualEffectSequence.Beats, beat => beat.StartSfx == SfxTrack.Prayer || beat.StartSfx == SfxTrack.GainAegis);
		});
		Assert.All(cards.SelectMany(card => card.VisualEffectSequence.Beats).Where(beat => beat.TargetRole == VisualEffectTargetRole.Enemy), beat =>
			Assert.Contains(VisualEffectModule.TargetShake, beat.Modules));
	}

	[Fact]
	public void Every_registered_enemy_attack_has_one_gameplay_driving_beat()
	{
		var attacks = EnemyAttackFactory.GetAllAttacks().Values.ToList();
		Assert.Equal(91, attacks.Count);
		Assert.All(EnemyAttackFactory.GetAllAttacks().Keys, id => Assert.True(VisualEffectSequenceAuthoring.HasExplicitEnemyAttackChoreography(id), $"Missing explicit choreography for {id}."));
		Assert.All(attacks, attack =>
		{
			Assert.NotEmpty(attack.AttackEffectSequence.Beats);
			Assert.Single(attack.AttackEffectSequence.Beats, beat => beat.DrivesGameplayImpact);
		});
		var duplicateSignatures = EnemyAttackFactory.GetAllAttacks()
			.GroupBy(pair => VisualSignature(pair.Value.AttackEffectSequence.Beats), StringComparer.Ordinal)
			.Where(group => group.Count() > 1)
			.Select(group => string.Join(", ", group.Select(pair => pair.Key)))
			.ToList();
		Assert.True(duplicateSignatures.Count == 0, $"Duplicate enemy choreography: {string.Join(" | ", duplicateSignatures)}");
	}

	[Fact]
	public void Display_wording_does_not_change_authored_choreography()
	{
		var originalCard = new Seize();
		var rewrittenCard = new Seize
		{
			Name = "Completely Different Display Name",
			Text = "Gain 999 courage, heal, and resurrect."
		};
		Assert.Equal(VisualSignature(originalCard.VisualEffectSequence.Beats), VisualSignature(rewrittenCard.VisualEffectSequence.Beats));

		var originalAttack = EnemyAttackFactory.Create(EnemyAttackId.VelvetFangs);
		var rewrittenAttack = EnemyAttackFactory.Create(EnemyAttackId.VelvetFangs);
		rewrittenAttack.Name = "Completely Different Attack Name";
		rewrittenAttack.Text = "The monster loses everything and never heals.";
		Assert.Equal(VisualSignature(originalAttack.AttackEffectSequence.Beats), VisualSignature(rewrittenAttack.AttackEffectSequence.Beats));
	}

	[Fact]
	public void Smite_is_less_intense_than_absolution()
	{
		Assert.True(MaxIntensity(new Smite()) < MaxIntensity(new Absolution()));
		Assert.True(MaxParticles(new Smite()) < MaxParticles(new Absolution()));
	}

	[Fact]
	public void Shield_of_faith_is_more_epic_than_divine_protection()
	{
		Assert.True(MaxIntensity(new ShieldOfFaith()) > MaxIntensity(new DivineProtection()));
		Assert.True(MaxParticles(new ShieldOfFaith()) > MaxParticles(new DivineProtection()));
	}

	private static float MaxIntensity(CardBase card) => card.VisualEffectSequence.Beats.Max(beat => beat.Intensity);
	private static float MaxParticles(CardBase card) => card.VisualEffectSequence.Beats.Max(beat => beat.ParticleMultiplier);
	private static string VisualSignature(CardBase card) => VisualSignature(card.VisualEffectSequence.Beats);
	private static string VisualSignature(System.Collections.Generic.IReadOnlyList<Crusaders30XX.ECS.Data.VisualEffects.VisualEffectBeat> beats)
	{
		return string.Join("|", beats.Select(beat => $"{beat.TargetRole}:{beat.DelaySeconds:F3}:{beat.DurationSeconds:F3}:{beat.ImpactTimeSeconds:F3}:{beat.Intensity:F3}:{beat.ParticleMultiplier:F3}:{beat.Palette}:{beat.StartSfxPitch:F3}:{beat.ImpactSfxPitch:F3}:{string.Join(',', beat.Modules)}"));
	}
}
