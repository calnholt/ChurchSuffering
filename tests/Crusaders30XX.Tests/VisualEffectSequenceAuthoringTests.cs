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
		var registered = CardFactory.GetAllCards()
			.Append(new System.Collections.Generic.KeyValuePair<CardId, CardBase>(CardId.Curse, CardFactory.Create(CardId.Curse)))
			.Append(new System.Collections.Generic.KeyValuePair<CardId, CardBase>(CardId.Hex, CardFactory.Create(CardId.Hex)))
			.ToList();
		var cards = registered.Select(pair => pair.Value).ToList();
		Assert.Equal(71, cards.Count);
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
		Assert.Equal(90, attacks.Count);
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

	[Theory]
	[InlineData(false, 2)]
	[InlineData(true, 3)]
	public void Multi_hit_card_metadata_repeats_the_authored_beat_at_gameplay_hit_times(bool upgraded, int expectedHits)
	{
		var entityManager = new Crusaders30XX.ECS.Core.EntityManager();
		var cardEntity = entityManager.CreateEntity("RazorStorm");
		var card = new RazorStorm { IsUpgraded = upgraded };
		card.Initialize(entityManager, cardEntity);

		Assert.Equal(expectedHits, card.MultiHitCount);
		Assert.Equal(expectedHits, card.VisualEffectSequence.Beats.Count);
		for (int hitIndex = 0; hitIndex < expectedHits; hitIndex++)
		{
			var beat = card.VisualEffectSequence.Beats[hitIndex];
			float actualImpact = beat.DelaySeconds + beat.ImpactTimeSeconds;
			float expectedImpact = card.FirstHitDelaySeconds + hitIndex * card.HitIntervalSeconds;
			Assert.Equal(expectedImpact, actualImpact, 3);
		}
	}

	[Fact]
	public void Changing_multi_hit_metadata_invalidates_cached_choreography()
	{
		var card = new Whirlwind();
		Assert.Equal(2, card.VisualEffectSequence.Beats.Count);

		card.MultiHitCount = 4;

		Assert.Equal(4, card.VisualEffectSequence.Beats.Count);
	}

	[Fact]
	public void Unregistered_future_multi_hit_attack_automatically_receives_repeated_choreography()
	{
		var card = new FutureMultiHitCard();

		Assert.Equal(3, card.VisualEffectSequence.Beats.Count);
		Assert.All(card.VisualEffectSequence.Beats, beat =>
			Assert.Equal(VisualEffectTargetRole.Enemy, beat.TargetRole));
	}

	private static float MaxIntensity(CardBase card) => card.VisualEffectSequence.Beats.Max(beat => beat.Intensity);
	private static float MaxParticles(CardBase card) => card.VisualEffectSequence.Beats.Max(beat => beat.ParticleMultiplier);
	private static string VisualSignature(CardBase card) => VisualSignature(card.VisualEffectSequence.Beats);
	private static string VisualSignature(System.Collections.Generic.IReadOnlyList<Crusaders30XX.ECS.Data.VisualEffects.VisualEffectBeat> beats)
	{
		return string.Join("|", beats.Select(beat => $"{beat.TargetRole}:{beat.DelaySeconds:F3}:{beat.DurationSeconds:F3}:{beat.ImpactTimeSeconds:F3}:{beat.Intensity:F3}:{beat.ParticleMultiplier:F3}:{beat.Palette}:{beat.StartSfxPitch:F3}:{beat.ImpactSfxPitch:F3}:{string.Join(',', beat.Modules)}"));
	}

	private sealed class FutureMultiHitCard : CardBase
	{
		public FutureMultiHitCard()
		{
			CardId = "future_multi_hit_card";
			Target = "Enemy";
			Damage = 2;
			MultiHitCount = 3;
			FirstHitDelaySeconds = 0.4f;
			HitIntervalSeconds = 0.3f;
		}
	}
}
