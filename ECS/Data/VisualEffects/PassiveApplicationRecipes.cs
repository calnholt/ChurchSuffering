using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Data.VisualEffects
{
	public enum PassiveApplicationMotif
	{
		Ring,
		Chevrons,
		Droplets,
		TrailingArcs,
		Ward,
		Sparks,
		Plates,
		SplitMark,
		Threads,
		FlameCrown,
		Crescents,
		Bubbles,
		Iris,
		Braces,
		Eye,
		Motes,
		Thorns,
		BloodDrops,
		JaggedAura,
		Nodes,
		PressureBars,
		Wisps,
		Scar,
		Stream,
		IceNeedles,
		Lattice,
		Ribbons,
		FrostSeal,
		HeatRing,
		ChainLinks,
		BrokenHalo,
		MuteBar,
		Stamp,
		CoinOrbit,
		BladeGlint,
		Pillars,
		HeartDiamond,
		Clock,
		ElectricChain,
	}

	public enum PassiveApplicationMotion
	{
		Assemble,
		Rise,
		Descend,
		Constrict,
		Stamp,
		Orbit,
		Sweep,
		Pulse,
	}

	public sealed record PassiveApplicationRecipe(
		PassiveApplicationMotif Motif,
		PassiveApplicationMotion Motion,
		VisualEffectPalette Palette,
		float DurationSeconds,
		int ElementCount,
		float RotationDegrees,
		float Aspect,
		bool IsHarmful)
	{
		public string Signature => string.Join("|",
			Motif,
			Motion,
			Palette,
			DurationSeconds.ToString("0.000"),
			ElementCount,
			RotationDegrees.ToString("0.0"),
			Aspect.ToString("0.00"),
			IsHarmful);
	}

	public static class PassiveApplicationRecipeCatalog
	{
		private static readonly IReadOnlyDictionary<AppliedPassiveType, PassiveApplicationRecipe> Recipes =
			new Dictionary<AppliedPassiveType, PassiveApplicationRecipe>
			{
				[AppliedPassiveType.Burn] = R(PassiveApplicationMotif.FlameCrown, PassiveApplicationMotion.Rise, VisualEffectPalette.Fire, .58f, 7, -8f, 1.00f, true),
				[AppliedPassiveType.Power] = R(PassiveApplicationMotif.Chevrons, PassiveApplicationMotion.Rise, VisualEffectPalette.Holy, .56f, 3, 0f, .90f, false),
				[AppliedPassiveType.DowseWithHolyWater] = R(PassiveApplicationMotif.Droplets, PassiveApplicationMotion.Descend, VisualEffectPalette.Holy, .64f, 5, 0f, 1.05f, false),
				[AppliedPassiveType.Slow] = R(PassiveApplicationMotif.TrailingArcs, PassiveApplicationMotion.Descend, VisualEffectPalette.Arcane, .68f, 3, -18f, 1.20f, true),
				[AppliedPassiveType.Aegis] = R(PassiveApplicationMotif.Ward, PassiveApplicationMotion.Assemble, VisualEffectPalette.Holy, .62f, 6, 0f, 1.00f, false),
				[AppliedPassiveType.Stun] = R(PassiveApplicationMotif.Sparks, PassiveApplicationMotion.Orbit, VisualEffectPalette.Holy, .55f, 5, 12f, 1.00f, true),
				[AppliedPassiveType.Armor] = R(PassiveApplicationMotif.Plates, PassiveApplicationMotion.Constrict, VisualEffectPalette.Earth, .60f, 4, 0f, 1.10f, false),
				[AppliedPassiveType.Wounded] = R(PassiveApplicationMotif.SplitMark, PassiveApplicationMotion.Stamp, VisualEffectPalette.Blood, .52f, 2, -24f, .86f, true),
				[AppliedPassiveType.Webbing] = R(PassiveApplicationMotif.Threads, PassiveApplicationMotion.Constrict, VisualEffectPalette.Physical, .70f, 7, 8f, 1.00f, true),
				[AppliedPassiveType.Inferno] = R(PassiveApplicationMotif.FlameCrown, PassiveApplicationMotion.Orbit, VisualEffectPalette.Fire, .66f, 10, 14f, 1.18f, true),
				[AppliedPassiveType.Aggression] = R(PassiveApplicationMotif.Chevrons, PassiveApplicationMotion.Sweep, VisualEffectPalette.Blood, .50f, 4, -6f, 1.18f, false),
				[AppliedPassiveType.Stealth] = R(PassiveApplicationMotif.Crescents, PassiveApplicationMotion.Constrict, VisualEffectPalette.Shadow, .72f, 3, 20f, 1.15f, false),
				[AppliedPassiveType.Poison] = R(PassiveApplicationMotif.Bubbles, PassiveApplicationMotion.Orbit, VisualEffectPalette.Poison, .70f, 7, 0f, 1.00f, true),
				[AppliedPassiveType.Shield] = R(PassiveApplicationMotif.Iris, PassiveApplicationMotion.Constrict, VisualEffectPalette.Holy, .64f, 8, 0f, 1.00f, false),
				[AppliedPassiveType.Guard] = R(PassiveApplicationMotif.Braces, PassiveApplicationMotion.Constrict, VisualEffectPalette.Earth, .58f, 4, 0f, 1.20f, false),
				[AppliedPassiveType.Fear] = R(PassiveApplicationMotif.Eye, PassiveApplicationMotion.Constrict, VisualEffectPalette.Shadow, .74f, 1, 0f, 1.30f, true),
				[AppliedPassiveType.Siphon] = R(PassiveApplicationMotif.Motes, PassiveApplicationMotion.Constrict, VisualEffectPalette.Blood, .70f, 9, -15f, 1.00f, true),
				[AppliedPassiveType.Thorns] = R(PassiveApplicationMotif.Thorns, PassiveApplicationMotion.Assemble, VisualEffectPalette.Poison, .60f, 9, 10f, 1.06f, false),
				[AppliedPassiveType.Bleed] = R(PassiveApplicationMotif.BloodDrops, PassiveApplicationMotion.Descend, VisualEffectPalette.Blood, .66f, 3, 0f, .92f, true),
				[AppliedPassiveType.Rage] = R(PassiveApplicationMotif.JaggedAura, PassiveApplicationMotion.Rise, VisualEffectPalette.Fire, .60f, 8, -5f, 1.12f, false),
				[AppliedPassiveType.Intellect] = R(PassiveApplicationMotif.Nodes, PassiveApplicationMotion.Orbit, VisualEffectPalette.Arcane, .72f, 3, 0f, 1.00f, false),
				[AppliedPassiveType.Intimidated] = R(PassiveApplicationMotif.PressureBars, PassiveApplicationMotion.Descend, VisualEffectPalette.Shadow, .66f, 4, 0f, 1.16f, true),
				[AppliedPassiveType.MindFog] = R(PassiveApplicationMotif.Wisps, PassiveApplicationMotion.Sweep, VisualEffectPalette.Shadow, .76f, 6, -12f, 1.25f, true),
				[AppliedPassiveType.Scar] = R(PassiveApplicationMotif.Scar, PassiveApplicationMotion.Stamp, VisualEffectPalette.Blood, .62f, 1, -32f, 1.20f, true),
				[AppliedPassiveType.Channel] = R(PassiveApplicationMotif.Stream, PassiveApplicationMotion.Rise, VisualEffectPalette.Arcane, .72f, 6, 0f, .78f, false),
				[AppliedPassiveType.Frostbite] = R(PassiveApplicationMotif.IceNeedles, PassiveApplicationMotion.Constrict, VisualEffectPalette.Ice, .58f, 3, 0f, 1.00f, true),
				[AppliedPassiveType.Frozen] = R(PassiveApplicationMotif.Lattice, PassiveApplicationMotion.Assemble, VisualEffectPalette.Ice, .68f, 6, 0f, 1.00f, true),
				[AppliedPassiveType.Windchill] = R(PassiveApplicationMotif.Ribbons, PassiveApplicationMotion.Sweep, VisualEffectPalette.Ice, .70f, 4, -8f, 1.30f, true),
				[AppliedPassiveType.SubZero] = R(PassiveApplicationMotif.FrostSeal, PassiveApplicationMotion.Descend, VisualEffectPalette.Ice, .72f, 6, 0f, 1.08f, true),
				[AppliedPassiveType.Enflamed] = R(PassiveApplicationMotif.HeatRing, PassiveApplicationMotion.Pulse, VisualEffectPalette.Fire, .64f, 3, 0f, 1.00f, true),
				[AppliedPassiveType.Shackled] = R(PassiveApplicationMotif.ChainLinks, PassiveApplicationMotion.Constrict, VisualEffectPalette.Earth, .66f, 2, -18f, 1.22f, true),
				[AppliedPassiveType.Anathema] = R(PassiveApplicationMotif.BrokenHalo, PassiveApplicationMotion.Stamp, VisualEffectPalette.Shadow, .70f, 5, 180f, 1.00f, true),
				[AppliedPassiveType.Silenced] = R(PassiveApplicationMotif.MuteBar, PassiveApplicationMotion.Constrict, VisualEffectPalette.Shadow, .60f, 3, 0f, 1.18f, true),
				[AppliedPassiveType.Sealed] = R(PassiveApplicationMotif.Stamp, PassiveApplicationMotion.Stamp, VisualEffectPalette.Arcane, .62f, 8, 15f, 1.00f, true),
				[AppliedPassiveType.Plunder] = R(PassiveApplicationMotif.CoinOrbit, PassiveApplicationMotion.Orbit, VisualEffectPalette.Holy, .68f, 5, 18f, 1.14f, false),
				[AppliedPassiveType.Sharpen] = R(PassiveApplicationMotif.BladeGlint, PassiveApplicationMotion.Sweep, VisualEffectPalette.Physical, .48f, 2, -20f, 1.24f, false),
				[AppliedPassiveType.Might] = R(PassiveApplicationMotif.Pillars, PassiveApplicationMotion.Rise, VisualEffectPalette.Holy, .64f, 2, 0f, 1.10f, false),
				[AppliedPassiveType.Vigor] = R(PassiveApplicationMotif.HeartDiamond, PassiveApplicationMotion.Pulse, VisualEffectPalette.Holy, .66f, 4, 45f, 1.00f, false),
				[AppliedPassiveType.CarpeDiem] = R(PassiveApplicationMotif.Clock, PassiveApplicationMotion.Assemble, VisualEffectPalette.Holy, .70f, 8, -90f, 1.00f, false),
				[AppliedPassiveType.Galvanize] = R(PassiveApplicationMotif.ElectricChain, PassiveApplicationMotion.Assemble, VisualEffectPalette.Arcane, .58f, 6, 12f, 1.14f, false),
				[AppliedPassiveType.SwordIntoShield] = R(PassiveApplicationMotif.BladeGlint, PassiveApplicationMotion.Assemble, VisualEffectPalette.Holy, .61f, 4, 30f, 1.15f, false),
			};

		static PassiveApplicationRecipeCatalog()
		{
			var allTypes = Enum.GetValues<AppliedPassiveType>();
			if (Recipes.Count != allTypes.Length || allTypes.Any(type => !Recipes.ContainsKey(type)))
				throw new InvalidOperationException("Every AppliedPassiveType must have a passive-application recipe.");
			if (Recipes.Values.Select(recipe => recipe.Signature).Distinct(StringComparer.Ordinal).Count() != Recipes.Count)
				throw new InvalidOperationException("Passive-application recipe signatures must be unique.");
		}

		public static IReadOnlyDictionary<AppliedPassiveType, PassiveApplicationRecipe> All => Recipes;

		public static PassiveApplicationRecipe Get(AppliedPassiveType type) => Recipes[type];

		private static PassiveApplicationRecipe R(
			PassiveApplicationMotif motif,
			PassiveApplicationMotion motion,
			VisualEffectPalette palette,
			float duration,
			int count,
			float rotation,
			float aspect,
			bool harmful)
		{
			return new PassiveApplicationRecipe(motif, motion, palette, duration, count, rotation, aspect, harmful);
		}
	}
}
