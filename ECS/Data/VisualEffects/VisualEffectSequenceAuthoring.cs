using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Data.VisualEffects;

/// <summary>
/// Produces one static, deterministic sequence from an individual gameplay definition.
/// It is intentionally a semantic authoring layer, not a preset catalog: the source
/// definition's name, text, cost, value, and attack id all participate in its result.
/// </summary>
public static class VisualEffectSequenceAuthoring
{
	public static VisualEffectSequence ForCard(CardBase card)
	{
		if (card == null) return new VisualEffectSequence().WithBeats();
		int hash = StableHash($"card|{card.CardId}|{card.Name}|{card.Text}|{card.Damage}|{card.Block}|{string.Join(',', card.Cost)}");
		int tier = Math.Max(CostTier(card.Cost?.Count ?? 0), ValueTier(Math.Max(card.Damage, ExtractLargestNumber(card.Text))));
		bool enemyTargeted = string.Equals(card.Target, "Enemy", StringComparison.OrdinalIgnoreCase) || card.Damage > 0;
		bool positive = ContainsAny(card.Text, "aegis", "courage", "temperance", "might", "power", "aggression", "heal", "resurrect", "draw", "action point", "guard", "faith");
		bool holy = ContainsAny(card.Name, "smite", "divine", "holy", "faith", "absolution", "consecrate", "vow", "crusade", "purge", "radiance");
		bool heavy = ContainsAny(card.Name, "hammer", "battering", "crush", "reckoning", "tempest", "whirlwind", "ravage");
		var beats = new List<VisualEffectBeat>();

		if (enemyTargeted)
		{
			beats.Add(CreateCardAttackBeat(card, tier, hash, holy, heavy));
		}
		if (card.Type != CardType.Attack && (positive || !enemyTargeted))
		{
			beats.Add(CreatePlayerBenefitBeat(card, tier, hash, holy, enemyTargeted));
		}

		if (beats.Count == 0)
		{
			beats.Add(card.Type == CardType.Attack
				? CreateCardAttackBeat(card, tier, hash, holy, heavy)
				: CreatePlayerBenefitBeat(card, tier, hash, holy, false));
		}
		return new VisualEffectSequence().WithBeats(beats.ToArray());
	}

	public static VisualEffectSequence ForEnemyAttack(EnemyAttackBase attack)
	{
		if (attack == null) return new VisualEffectSequence().WithBeats();
		int hash = StableHash($"attack|{attack.Id}|{attack.Name}|{attack.Text}|{attack.Damage}|{attack.AdditionalDamage}|{attack.ConditionType}|{attack.BlockingRestrictionType}");
		int tier = ValueTier(Math.Max(attack.Damage + attack.AdditionalDamage, ExtractLargestNumber(attack.Text)));
		if (attack.ConditionType != ConditionType.None || attack.BlockingRestrictionType != BlockingRestrictionType.None) tier = Math.Min(3, tier + 1);
		bool selfBenefit = ContainsAny(attack.Text, "enemy gains", "monster heals", "gain guard", "gain anathema", "gain aggression", "heals");
		var beats = new List<VisualEffectBeat>();
		if (selfBenefit)
		{
			beats.Add(CreateEnemySelfBeat(attack, tier, hash));
		}
		beats.Add(CreateEnemyImpactBeat(attack, tier, hash, selfBenefit));
		return new VisualEffectSequence().WithBeats(beats.ToArray());
	}

	private static VisualEffectBeat CreateCardAttackBeat(CardBase card, int tier, int hash, bool holy, bool heavy)
	{
		var modules = new List<VisualEffectModule> { VisualEffectModule.ActorLunge };
		if (holy)
		{
			modules.AddRange(new[] { VisualEffectModule.CrossBloom, VisualEffectModule.Rays, VisualEffectModule.HitFlash });
			if (tier >= 2) modules.Add(VisualEffectModule.Beam);
		}
		else if (heavy)
		{
			modules.AddRange(new[] { VisualEffectModule.HammerArc, VisualEffectModule.Ring, VisualEffectModule.Debris, VisualEffectModule.HitFlash });
			if (tier >= 2) modules.AddRange(new[] { VisualEffectModule.Cracks, VisualEffectModule.Shockwave, VisualEffectModule.Shake });
			if (tier >= 3) modules.AddRange(new[] { VisualEffectModule.PunchZoom, VisualEffectModule.HitStop });
		}
		else
		{
			modules.Add((hash & 1) == 0 ? VisualEffectModule.SwordArc : VisualEffectModule.CrossSlash);
			modules.Add(VisualEffectModule.HitFlash);
			if (tier >= 1) modules.Add(VisualEffectModule.Debris);
			if (tier >= 2) modules.Add(VisualEffectModule.SlashBand);
			if (tier >= 3) modules.AddRange(new[] { VisualEffectModule.Shake, VisualEffectModule.HitStop });
		}

		return new VisualEffectBeat
		{
			Id = $"{card.CardId}_strike",
			TargetRole = string.Equals(card.Target, "Player", StringComparison.OrdinalIgnoreCase)
				? VisualEffectTargetRole.Player
				: VisualEffectTargetRole.Enemy,
			DurationSeconds = TierDuration(tier) + HashOffset(hash, 0.05f),
			ImpactTimeSeconds = TierImpact(tier),
			HitStopStartSeconds = tier >= 3 ? TierImpact(tier) : 0f,
			HitStopDurationSeconds = tier >= 3 ? 0.08f : 0f,
			Intensity = TierIntensity(tier) + HashOffset(hash >> 3, 0.08f),
			ParticleMultiplier = TierParticles(tier) + HashOffset(hash >> 6, 0.12f),
			StartSfx = SfxTrack.SwordAttack,
			ImpactSfx = SfxTrack.SwordImpact
		}.WithModules(modules.ToArray());
	}

	private static VisualEffectBeat CreatePlayerBenefitBeat(CardBase card, int tier, int hash, bool holy, bool afterAttack)
	{
		var modules = new List<VisualEffectModule> { VisualEffectModule.ActorSquashStretch, VisualEffectModule.Ring };
		if (holy || ContainsAny(card.Text, "aegis", "faith", "temperance")) modules.AddRange(new[] { VisualEffectModule.Halo, VisualEffectModule.WhiteWash });
		if (tier >= 1) modules.Add(VisualEffectModule.Rays);
		if (tier >= 2) modules.AddRange(new[] { VisualEffectModule.CrossBloom, VisualEffectModule.Beam });
		if (tier >= 3) modules.Add(VisualEffectModule.Shards);
		return new VisualEffectBeat
		{
			Id = $"{card.CardId}_benefit",
			TargetRole = VisualEffectTargetRole.Player,
			DelaySeconds = afterAttack ? TierImpact(tier) + 0.04f : 0f,
			DurationSeconds = TierDuration(tier) + 0.18f + HashOffset(hash >> 4, 0.05f),
			ImpactTimeSeconds = 0.16f + HashOffset(hash >> 7, 0.04f),
			Intensity = TierIntensity(tier) + 0.04f + HashOffset(hash >> 9, 0.06f),
			ParticleMultiplier = TierParticles(tier) + HashOffset(hash >> 11, 0.10f),
			StartSfx = ContainsAny(card.Text, "aegis") ? SfxTrack.GainAegis : SfxTrack.Prayer
		}.WithModules(modules.ToArray());
	}

	private static VisualEffectBeat CreateEnemyImpactBeat(EnemyAttackBase attack, int tier, int hash, bool afterSelfBeat)
	{
		var modules = new List<VisualEffectModule> { VisualEffectModule.ActorLunge, VisualEffectModule.HitFlash };
		if (ContainsAny(attack.Name, "bite", "fang", "mandible", "maw", "nip")) modules.Add(VisualEffectModule.Bite);
		else if (ContainsAny(attack.Name, "claw", "talon", "rend", "slice", "slash", "blade", "strike", "stab")) modules.Add(VisualEffectModule.ClawSlash);
		else if (ContainsAny(attack.Name, "sand", "stone", "earth", "tremor", "slam", "stomp", "pummel", "crusher", "blast")) modules.AddRange(new[] { VisualEffectModule.RockBlast, VisualEffectModule.Debris });
		else modules.Add((hash & 1) == 0 ? VisualEffectModule.CrossSlash : VisualEffectModule.SlashBand);
		if (tier >= 1) modules.Add(VisualEffectModule.Shake);
		if (tier >= 2) modules.AddRange(new[] { VisualEffectModule.Ring, VisualEffectModule.SmokeBlobs });
		if (tier >= 3) modules.AddRange(new[] { VisualEffectModule.Shockwave, VisualEffectModule.HitStop, VisualEffectModule.PunchZoom });
		return new VisualEffectBeat
		{
			Id = $"{attack.Id}_impact",
			TargetRole = VisualEffectTargetRole.Player,
			DelaySeconds = afterSelfBeat ? 0.12f + HashOffset(hash, 0.05f) : 0f,
			DurationSeconds = TierDuration(tier) + HashOffset(hash >> 2, 0.06f),
			ImpactTimeSeconds = TierImpact(tier),
			HitStopStartSeconds = tier >= 3 ? TierImpact(tier) : 0f,
			HitStopDurationSeconds = tier >= 3 ? 0.09f : 0f,
			Intensity = TierIntensity(tier) + HashOffset(hash >> 5, 0.08f),
			ParticleMultiplier = TierParticles(tier) + HashOffset(hash >> 8, 0.12f),
			ImpactSfx = SfxTrack.SwordImpact,
			DrivesGameplayImpact = true
		}.WithModules(modules.ToArray());
	}

	private static VisualEffectBeat CreateEnemySelfBeat(EnemyAttackBase attack, int tier, int hash)
	{
		var modules = new List<VisualEffectModule> { VisualEffectModule.Ring, VisualEffectModule.SmokeBlobs };
		if (ContainsAny(attack.Text, "heal")) modules.Add(VisualEffectModule.Halo);
		if (tier >= 2) modules.Add(VisualEffectModule.Rays);
		return new VisualEffectBeat
		{
			Id = $"{attack.Id}_self",
			TargetRole = VisualEffectTargetRole.Self,
			DurationSeconds = 0.46f + tier * 0.12f + HashOffset(hash, 0.05f),
			ImpactTimeSeconds = 0.16f,
			Intensity = 0.72f + tier * 0.16f + HashOffset(hash >> 4, 0.06f),
			ParticleMultiplier = 0.55f + tier * 0.22f + HashOffset(hash >> 7, 0.10f)
		}.WithModules(modules.ToArray());
	}

	private static int CostTier(int costCount) => costCount switch { <= 0 => 0, 1 => 1, 2 => 2, _ => 3 };
	private static int ValueTier(int value) => value switch { <= 3 => 0, <= 6 => 1, <= 9 => 2, _ => 3 };
	private static float TierDuration(int tier) => 0.34f + tier * 0.19f;
	private static float TierImpact(int tier) => 0.15f + tier * 0.05f;
	private static float TierIntensity(int tier) => 0.62f + tier * 0.23f;
	private static float TierParticles(int tier) => 0.38f + tier * 0.34f;
	private static float HashOffset(int hash, float max) => ((hash & 0xFF) / 255f - 0.5f) * max * 2f;

	private static int ExtractLargestNumber(string text)
	{
		return Regex.Matches(text ?? string.Empty, "\\d+").Select(match => int.TryParse(match.Value, out int value) ? value : 0).DefaultIfEmpty().Max();
	}

	private static bool ContainsAny(string value, params string[] terms)
	{
		return terms.Any(term => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
	}

	private static int StableHash(string value)
	{
		unchecked
		{
			int hash = 23;
			foreach (char character in value ?? string.Empty) hash = hash * 31 + character;
			return hash & int.MaxValue;
		}
	}
}
