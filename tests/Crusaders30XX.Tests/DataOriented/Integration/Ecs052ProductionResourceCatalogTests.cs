#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Authoring.Combat;
using Crusaders30XX.ECS.DataOriented.Authoring.Meta;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Integration.Host.Resources;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Integration;

public sealed class Ecs052ProductionResourceCatalogTests
{
    [Fact]
    public void Production_catalog_covers_every_authored_texture_id_exactly_once()
    {
        HostTextureAssetCatalog catalog = ProductionHostTextureCatalog.Create();
        TextureAssetId[] expected = AuthoredTextureIds().ToArray();

        Assert.Equal(expected.Length, expected.Select(id => id.Value).Distinct().Count());
        Assert.Equal(expected.Length, catalog.Bindings.Length);
        foreach (TextureAssetId id in expected)
            Assert.True(catalog.TryGet(id, out _), $"TextureAssetId {id.Value} is unbound.");
    }

    [Fact]
    public void Every_content_binding_names_an_asset_built_by_the_content_pipeline()
    {
        HostTextureAssetBinding[] content = ProductionHostTextureCatalog.Create().Bindings.ToArray()
            .Where(binding => binding.Source == HostTextureSourceKind.ContentAsset)
            .ToArray();
        string mgcb = File.ReadAllText(FindRepositoryFile("Content/Content.mgcb"));

        foreach (HostTextureAssetBinding binding in content)
        {
            string asset = binding.ContentAssetName!;
            Assert.True(
                mgcb.Contains($"#begin {asset}.png", StringComparison.Ordinal),
                $"TextureAssetId {binding.Id.Value} names missing Content.mgcb asset '{asset}'.");
        }
    }

    [Fact]
    public void Real_card_and_enemy_art_is_used_and_only_truly_missing_art_uses_named_recipes()
    {
        HostTextureAssetCatalog catalog = ProductionHostTextureCatalog.Create();

        foreach (var metadata in GeneratedCardCatalog.DebugMetadata)
        {
            Assert.True(catalog.TryGet(CombatPresentationTextureIds.Card(metadata.Id), out var binding));
            if (metadata.Id is CardId.Colorless3Block or CardId.SwordIntoShield)
                Assert.Equal(GeneratedTextureRecipeKind.MissingCardArt, binding.GeneratedRecipe.Kind);
            else
                Assert.Equal(HostTextureSourceKind.ContentAsset, binding.Source);
        }

        foreach (var metadata in GeneratedEnemyCatalog.DebugMetadata)
        {
            Assert.True(catalog.TryGet(CombatPresentationTextureIds.Enemy(metadata.Id), out var binding));
            if (metadata.Id == EnemyId.SandCorpse)
                Assert.Equal(GeneratedTextureRecipeKind.MissingEnemyArt, binding.GeneratedRecipe.Kind);
            else
                Assert.Equal(HostTextureSourceKind.ContentAsset, binding.Source);
        }
    }

    [Fact]
    public void Catalog_validation_rejects_missing_duplicate_and_unowned_bindings()
    {
        var first = new TextureAssetId(1);
        var second = new TextureAssetId(2);
        var recipe = new GeneratedTextureRecipe(
            GeneratedTextureRecipeKind.SolidPanel, 1, 1, 0, Color.Black, Color.Black);

        Assert.Throws<InvalidOperationException>(() => new HostTextureAssetCatalog(
            [first, second], [HostTextureAssetBinding.Generated(first, in recipe)]));
        Assert.Throws<ArgumentException>(() => new HostTextureAssetCatalog(
            [first],
            [HostTextureAssetBinding.Generated(first, in recipe), HostTextureAssetBinding.Generated(first, in recipe)]));
        Assert.Throws<InvalidOperationException>(() => new HostTextureAssetCatalog(
            [first],
            [HostTextureAssetBinding.Generated(first, in recipe), HostTextureAssetBinding.Generated(second, in recipe)]));
    }

    [Fact]
    public void Generic_resolver_preloads_without_a_graphics_device_and_reuses_resources()
    {
        HostTextureAssetCatalog catalog = ProductionHostTextureCatalog.Create();
        var factory = new RecordingFactory();
        var resolver = new CatalogHostTextureResolver<string>(catalog, factory);

        resolver.Preload();
        int loads = factory.Calls.Count;
        int uniqueResources = catalog.Bindings.ToArray()
            .Select(binding => binding.Source == HostTextureSourceKind.ContentAsset
                ? $"content:{binding.ContentAssetName}"
                : $"generated:{binding.GeneratedRecipe}")
            .Distinct(StringComparer.Ordinal)
            .Count();
        Assert.Equal(uniqueResources, loads);
        Assert.True(loads < catalog.Bindings.Length);
        Assert.True(resolver.TryResolve(MetaStaticSceneTextureIds.TitleBackdrop, out string? first));
        Assert.True(resolver.TryResolve(MetaStaticSceneTextureIds.TitleBackdrop, out string? second));
        Assert.Same(first, second);
        Assert.Equal(loads, factory.Calls.Count);
        Assert.False(resolver.TryResolve(new TextureAssetId(int.MaxValue), out _));
    }

    [Fact]
    public void Emitted_shader_kinds_have_real_effect_assets_and_no_non_null_sound_is_emitted_yet()
    {
        string mgcb = File.ReadAllText(FindRepositoryFile("Content/Content.mgcb"));
        Assert.True(ProductionHostShaderCatalog.TryGet(ShaderRequestKind.Shockwave, out _));
        Assert.True(ProductionHostShaderCatalog.TryGet(ShaderRequestKind.RectangularShockwave, out _));
        Assert.True(ProductionHostShaderCatalog.TryGet(ShaderRequestKind.Poison, out _));
        foreach (HostShaderAssetBinding binding in ProductionHostShaderCatalog.Registered)
            Assert.Contains($"#begin {binding.ContentAssetName}.fx", mgcb, StringComparison.Ordinal);
        Assert.Empty(ProductionHostSoundCatalog.Emitted.ToArray());
    }

    private static IEnumerable<TextureAssetId> AuthoredTextureIds()
    {
        foreach (PlayerHudSnapshotMaskSpec spec in PlayerHudSnapshotMaskIds.CopySpecs())
            yield return spec.Id;
        foreach (PlayerHudPassiveMaskSpec spec in PlayerHudPassiveMaskIds.CopySpecs())
            yield return spec.Id;
        yield return MetaStaticSceneTextureIds.TitleBackdrop;
        yield return MetaStaticSceneTextureIds.TitleCrest;
        yield return MetaStaticSceneTextureIds.TitleMenuPlate;
        yield return MetaStaticSceneTextureIds.ClimbBackdrop;
        yield return MetaStaticSceneTextureIds.ClimbTimeline;
        yield return MetaStaticSceneTextureIds.ClimbHeader;
        yield return MetaStaticSceneTextureIds.WayStationBackdrop;
        yield return MetaStaticSceneTextureIds.WayStationBanner;
        yield return MetaStaticSceneTextureIds.WayStationPoiPlate;
        yield return MetaStaticSceneTextureIds.AchievementBackdrop;
        yield return MetaStaticSceneTextureIds.AchievementHeader;
        yield return MetaStaticSceneTextureIds.AchievementGrid;

        NewWorldSnapshotFixture[] fixtures = new NewWorldSnapshotFixtureHost().Registered.ToArray();
        for (var fixtureIndex = 0; fixtureIndex < fixtures.Length; fixtureIndex++)
        {
            for (var variantIndex = 0; variantIndex < fixtures[fixtureIndex].VariantCount; variantIndex++)
            {
                int first = 47001 + fixtureIndex * 16 + variantIndex * 2;
                yield return new TextureAssetId(first);
                yield return new TextureAssetId(first + 1);
                if (fixtures[fixtureIndex].Id is "guardian-angel" or "player-hud")
                    yield return SnapshotFixtureTextureIds.Tertiary(fixtureIndex, variantIndex);
            }
        }

        yield return CombatPresentationTextureIds.BattleBackdrop;
        yield return CombatPresentationTextureIds.Player;
        yield return CombatPresentationTextureIds.Health;
        yield return CombatPresentationTextureIds.ActionPoints;
        yield return CombatPresentationTextureIds.Courage;
        yield return CombatPresentationTextureIds.Temperance;
        yield return CombatPresentationTextureIds.DrawPile;
        yield return CombatPresentationTextureIds.Hand;
        yield return CombatPresentationTextureIds.DiscardPile;
        yield return CombatPresentationTextureIds.ExhaustPile;
        foreach (EnemyId enemy in Enum.GetValues<EnemyId>())
            if (GeneratedEnemyCatalog.IsDefined(enemy))
                yield return CombatPresentationTextureIds.Enemy(enemy);
        foreach (CardId card in Enum.GetValues<CardId>())
            if (GeneratedCardCatalog.IsDefined(card))
                yield return CombatPresentationTextureIds.Card(card);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.");
    }

    private sealed class RecordingFactory : IHostTextureResourceFactory<string>
    {
        public List<string> Calls { get; } = [];

        public string LoadContentAsset(string contentAssetName)
        {
            string result = $"content:{contentAssetName}";
            Calls.Add(result);
            return result;
        }

        public string CreateGeneratedPrimitive(in GeneratedTextureRecipe recipe)
        {
            string result = $"generated:{recipe.Kind}:{recipe.Width}x{recipe.Height}";
            Calls.Add(result);
            return result;
        }
    }
}
