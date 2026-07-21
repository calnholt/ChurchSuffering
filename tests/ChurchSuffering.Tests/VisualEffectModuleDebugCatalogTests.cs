using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class VisualEffectModuleDebugCatalogTests
{
	[Fact]
	public void All_enum_modules_are_represented_in_catalog()
	{
		var catalogModules = VisualEffectModuleDebugCatalog.All
			.Select(entry => entry.Module)
			.ToHashSet();

		foreach (VisualEffectModule module in System.Enum.GetValues<VisualEffectModule>())
		{
			Assert.Contains(module, catalogModules);
		}
	}

	[Fact]
	public void ActorLunge_has_player_and_enemy_entries()
	{
		var actorLungeEntries = VisualEffectModuleDebugCatalog.All
			.Where(entry => entry.Module == VisualEffectModule.ActorLunge)
			.ToList();

		Assert.Equal(2, actorLungeEntries.Count);
		Assert.Equal(2, actorLungeEntries.Select(entry => entry.Label).Distinct().Count());

		var playerEntry = actorLungeEntries.Single(entry => entry.Label == "ActorLunge (player)");
		Assert.Equal(VisualEffectSourceKind.Card, playerEntry.SourceKind);
		Assert.Equal(VisualEffectTargetRole.Enemy, playerEntry.TargetRole);

		var enemyEntry = actorLungeEntries.Single(entry => entry.Label == "ActorLunge (enemy)");
		Assert.Equal(VisualEffectSourceKind.EnemyAttack, enemyEntry.SourceKind);
		Assert.Equal(VisualEffectTargetRole.Player, enemyEntry.TargetRole);
	}

	[Fact]
	public void BuildRecipe_returns_single_module_without_sfx()
	{
		var entry = VisualEffectModuleDebugCatalog.All.Single(e => e.Label == "SwordArc");
		var recipe = VisualEffectModuleDebugCatalog.BuildRecipe(entry);

		Assert.Single(recipe.Modules);
		Assert.Equal(VisualEffectModule.SwordArc, recipe.Modules[0]);
		Assert.Equal(SfxTrack.None, recipe.StartSfx);
		Assert.Equal(SfxTrack.None, recipe.ImpactSfx);
		Assert.Equal(entry.Timing, recipe.Timing);
		Assert.Equal(entry.TargetRole, recipe.TargetRole);
		Assert.Equal(entry.Palette, recipe.Palette);
	}

	[Fact]
	public void Elemental_modules_use_semantic_preview_palettes()
	{
		Assert.Equal(VisualEffectPalette.Fire, VisualEffectModuleDebugCatalog.All.Single(e => e.Module == VisualEffectModule.FlameBurst).Palette);
		Assert.Equal(VisualEffectPalette.Ice, VisualEffectModuleDebugCatalog.All.Single(e => e.Module == VisualEffectModule.FrostBurst).Palette);
		Assert.Equal(VisualEffectPalette.Shadow, VisualEffectModuleDebugCatalog.All.Single(e => e.Module == VisualEffectModule.ShadowTendrils).Palette);
		Assert.Equal(VisualEffectPalette.Poison, VisualEffectModuleDebugCatalog.All.Single(e => e.Module == VisualEffectModule.PoisonCloud).Palette);
	}

	[Fact]
	public void Catalog_labels_are_unique()
	{
		var labels = VisualEffectModuleDebugCatalog.All.Select(entry => entry.Label).ToList();
		Assert.Equal(labels.Count, labels.Distinct().Count());
	}

	[Fact]
	public void SwordArc_preview_uses_player_source_and_enemy_target()
	{
		var entityManager = BuildBattleActors(out var player, out var enemy);
		var entry = VisualEffectModuleDebugCatalog.All.Single(e => e.Label == "SwordArc");

		var request = VisualEffectRequestFactory.ForDebugPreview(
			entityManager,
			entry.SourceKind,
			entry.Label,
			entry.Label,
			VisualEffectModuleDebugCatalog.BuildRecipe(entry));

		Assert.NotNull(request);
		Assert.True(request.IsPreview);
		Assert.Same(player, request.Source);
		Assert.Same(enemy, request.Target);
		Assert.Equal(VisualEffectSourceKind.Card, request.SourceKind);
	}

	[Fact]
	public void Bite_preview_uses_enemy_source_and_player_target()
	{
		var entityManager = BuildBattleActors(out var player, out var enemy);
		var entry = VisualEffectModuleDebugCatalog.All.Single(e => e.Label == "Bite");

		var request = VisualEffectRequestFactory.ForDebugPreview(
			entityManager,
			entry.SourceKind,
			entry.Label,
			entry.Label,
			VisualEffectModuleDebugCatalog.BuildRecipe(entry));

		Assert.NotNull(request);
		Assert.True(request.IsPreview);
		Assert.Same(enemy, request.Source);
		Assert.Same(player, request.Target);
		Assert.Equal(VisualEffectSourceKind.EnemyAttack, request.SourceKind);
	}

	private static EntityManager BuildBattleActors(out Entity player, out Entity enemy)
	{
		var entityManager = new EntityManager();
		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Transform { Position = new Vector2(100f, 300f) });

		enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());
		entityManager.AddComponent(enemy, new Transform { Position = new Vector2(700f, 300f) });

		return entityManager;
	}
}
