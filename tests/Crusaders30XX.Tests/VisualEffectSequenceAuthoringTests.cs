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
		var cards = CardFactory.GetAllCards().Values.Append(CardFactory.Create(CardId.Curse)).ToList();
		Assert.Equal(68, cards.Count);
		Assert.All(cards, card => Assert.NotEmpty(card.VisualEffectSequence.Beats));
		Assert.Equal(cards.Count, cards.Select(Signature).Distinct(StringComparer.Ordinal).Count());
		Assert.All(cards.Where(card => card.Type == CardType.Attack), card =>
		{
			Assert.DoesNotContain(card.VisualEffectSequence.Beats, beat => beat.Modules.Contains(VisualEffectModule.ActorSquashStretch));
			Assert.DoesNotContain(card.VisualEffectSequence.Beats, beat => beat.StartSfx == SfxTrack.Prayer || beat.StartSfx == SfxTrack.GainAegis);
		});
	}

	[Fact]
	public void Every_registered_enemy_attack_has_one_gameplay_driving_beat()
	{
		var attacks = EnemyAttackFactory.GetAllAttacks().Values.ToList();
		Assert.Equal(91, attacks.Count);
		Assert.All(attacks, attack =>
		{
			Assert.NotEmpty(attack.AttackEffectSequence.Beats);
			Assert.Single(attack.AttackEffectSequence.Beats.Where(beat => beat.DrivesGameplayImpact));
		});
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
	private static string Signature(CardBase card) => Signature(card.VisualEffectSequence.Beats);
	private static string Signature(System.Collections.Generic.IReadOnlyList<Crusaders30XX.ECS.Data.VisualEffects.VisualEffectBeat> beats)
	{
		return string.Join("|", beats.Select(beat => $"{beat.Id}:{beat.DelaySeconds:F3}:{beat.DurationSeconds:F3}:{beat.Intensity:F3}:{beat.ParticleMultiplier:F3}:{string.Join(',', beat.Modules)}"));
	}
}
