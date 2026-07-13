#nullable enable

using System.Linq;
using Crusaders30XX.ECS.DataOriented.Authoring.Meta;
using Crusaders30XX.ECS.DataOriented.Authoring.Text;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Integration.Host.Resources;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rendering;

public sealed class Ecs052PauseHotKeySnapshotAuthoringTests
{
    [Theory]
    [InlineData(0, 42, 10)]
    [InlineData(1, 35, 9)]
    [InlineData(2, 44, 10)]
    [InlineData(3, 42, 10)]
    [InlineData(4, 44, 12)]
    [InlineData(5, 6, 2)]
    public void Player_hud_variants_author_the_approved_resource_and_health_states(
        int variant,
        int expectedSprites,
        int expectedTexts)
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = new SnapshotFixtureMaterializer()
            .Materialize(world, "player-hud", variant);
        RenderPacket[] sprites = ExtractSprites(world);
        TextRenderPacket[] texts = ExtractText(world);

        Assert.Equal(SceneGroup.Battle, authored.Scene);
        Assert.Equal(expectedSprites, sprites.Length);
        Assert.Equal(expectedTexts, texts.Length);
        Assert.Contains(sprites, packet =>
            packet.Position == new Vector2(960f, 540f) &&
            packet.Scale == new Vector2(1920f, 1080f) &&
            packet.Tint == new Color(64, 38, 26));
        Assert.Contains(sprites, packet => packet.Texture == Secondary("player-hud", variant));
        Assert.Contains(sprites, packet =>
            (packet.Flags & RenderPacketFlags.PixelAlignedDestination) != 0);
        Assert.Equal(variant == 5 ? 1 : variant == 1 ? 4 : 5,
            texts.Count(packet => packet.LetterSpacing > 0f));
    }

    [Theory]
    [InlineData(0, "crusader_sword")]
    [InlineData(1, "crusader_sword")]
    [InlineData(2, "crusader_sword")]
    [InlineData(3, "crusader_sword")]
    [InlineData(4, "crusader_sword")]
    [InlineData(5, "Skeleton")]
    public void Player_hud_variants_bind_real_portraits_and_pledge_art(
        int variant,
        string expectedPortrait)
    {
        HostTextureAssetCatalog catalog = ProductionHostTextureCatalog.Create();

        Assert.True(catalog.TryGet(Primary("player-hud", variant), out var pixel));
        Assert.Equal(GeneratedTextureRecipeKind.SolidPanel, pixel.GeneratedRecipe.Kind);
        Assert.True(catalog.TryGet(Secondary("player-hud", variant), out var portrait));
        Assert.Equal(expectedPortrait, portrait.ContentAssetName);
        Assert.True(catalog.TryGet(Tertiary("player-hud", variant), out var pledge));
        Assert.Equal("pledge", pledge.ContentAssetName);
    }

    [Theory]
    [InlineData(0, 8, 2)]
    [InlineData(1, 10, 4)]
    [InlineData(2, 10, 4)]
    [InlineData(3, 10, 0)]
    public void Enemy_damage_meter_variants_author_the_approved_segment_states(
        int variant,
        int expectedSprites,
        int expectedTexts)
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = new SnapshotFixtureMaterializer()
            .Materialize(world, "enemy-damage-meter", variant);
        RenderPacket[] sprites = ExtractSprites(world);
        TextRenderPacket[] texts = ExtractText(world);

        Assert.Equal(SceneGroup.Battle, authored.Scene);
        Assert.Equal(expectedSprites, sprites.Length);
        Assert.Equal(expectedTexts, texts.Length);
        Assert.All(sprites, packet =>
            Assert.Equal(Primary("enemy-damage-meter", variant), packet.Texture));
        Assert.Contains(sprites, packet =>
            packet.Position == new Vector2(960f, 540f) &&
            packet.Scale == new Vector2(1920f, 1080f) &&
            packet.Tint == new Color(32, 26, 25));
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(1, 4, 1)]
    [InlineData(2, 4, 1)]
    [InlineData(3, 4, 1)]
    [InlineData(4, 4, 1)]
    public void Guardian_angel_variants_author_the_real_backdrop_angel_and_optional_speech(
        int variant,
        int expectedSprites,
        int expectedTexts)
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = new SnapshotFixtureMaterializer()
            .Materialize(world, "guardian-angel", variant);
        RenderPacket[] sprites = ExtractSprites(world);
        TextRenderPacket[] texts = ExtractText(world);

        Assert.Equal(SceneGroup.Battle, authored.Scene);
        Assert.Equal(expectedSprites, sprites.Length);
        Assert.Equal(expectedTexts, texts.Length);
        Assert.Contains(sprites, packet => packet.Texture == Primary("guardian-angel", variant));
        Assert.Contains(sprites, packet => packet.Texture == Secondary("guardian-angel", variant));
        Assert.Contains(sprites, packet => packet.Texture == Tertiary("guardian-angel", variant));
        if (variant == 0) Assert.Empty(texts);
        else Assert.Single(texts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Guardian_angel_variants_bind_real_backdrop_and_angel_art(int variant)
    {
        HostTextureAssetCatalog catalog = ProductionHostTextureCatalog.Create();

        Assert.True(catalog.TryGet(Primary("guardian-angel", variant), out var backdrop));
        Assert.Equal(HostTextureSourceKind.ContentAsset, backdrop.Source);
        Assert.Equal("Battle_Backgrounds/gothic-battle-background", backdrop.ContentAssetName);
        Assert.True(catalog.TryGet(Secondary("guardian-angel", variant), out var angel));
        Assert.Equal(HostTextureSourceKind.ContentAsset, angel.Source);
        Assert.Equal("guardian_angel", angel.ContentAssetName);
        Assert.True(catalog.TryGet(Tertiary("guardian-angel", variant), out var pixel));
        Assert.Equal(HostTextureSourceKind.GeneratedPrimitive, pixel.Source);
        Assert.Equal(GeneratedTextureRecipeKind.SolidPanel, pixel.GeneratedRecipe.Kind);
    }

    [Theory]
    [InlineData(0, 11, 3, "Bulwark Plate")]
    [InlineData(1, 13, 9, "Knightly Grieves")]
    [InlineData(2, 11, 3, "Bulwark Plate")]
    public void Equipment_tooltip_variants_author_real_equipment_panels_and_content(
        int variant,
        int expectedSprites,
        int expectedTexts,
        string expectedTitle)
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = new SnapshotFixtureMaterializer()
            .Materialize(world, "equipment-tooltip", variant);
        RenderPacket[] sprites = ExtractSprites(world);
        TextRenderPacket[] texts = ExtractText(world);
        var catalog = new StaticTextPresentationCatalog();
        string[] resolved = texts.Select(packet =>
        {
            Assert.True(catalog.TryResolve(packet.Content, out string? text));
            return text!;
        }).ToArray();

        Assert.Equal(SceneGroup.Battle, authored.Scene);
        Assert.Equal(expectedSprites, sprites.Length);
        Assert.Equal(expectedTexts, texts.Length);
        Assert.Contains(expectedTitle, resolved);
        Assert.Contains(sprites, packet =>
            packet.Position == new Vector2(960f, 540f) &&
            packet.Scale == new Vector2(1920f, 1080f) &&
            packet.Tint == new Color(64, 38, 26));
        Assert.Contains(sprites, packet => packet.Texture == Secondary("equipment-tooltip", variant));
        Assert.All(sprites, packet => Assert.Contains(
            packet.Texture,
            new[] { Primary("equipment-tooltip", variant), Secondary("equipment-tooltip", variant) }));
    }

    [Theory]
    [InlineData(0, 68, 12)]
    [InlineData(1, 68, 12)]
    public void Pause_menu_variants_author_the_approved_legacy_compositions(
        int variant,
        int expectedSprites,
        int expectedTexts)
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = new SnapshotFixtureMaterializer()
            .Materialize(world, "pause-menu", variant);
        RenderPacket[] sprites = ExtractSprites(world);
        TextRenderPacket[] texts = ExtractText(world);

        Assert.Equal(SceneGroup.Snapshot, authored.Scene);
        Assert.Equal(expectedSprites, sprites.Length);
        Assert.Equal(expectedTexts, texts.Length);
        Assert.All(sprites, packet => Assert.Contains(
            packet.Texture,
            new[] { Primary("pause-menu", variant), Secondary("pause-menu", variant) }));

        var catalog = new StaticTextPresentationCatalog();
        string[] resolved = texts.Select(packet =>
        {
            Assert.True(catalog.TryResolve(packet.Content, out string? text));
            return text!;
        }).ToArray();
        if (variant == 0)
        {
            Assert.Contains("Paused", resolved);
            Assert.Contains("Audio and haptics", resolved);
            Assert.Contains("ON", resolved);
            Assert.Contains("Abandon Climb", resolved);
            Assert.Contains(sprites, packet => packet.Position == new Vector2(234f, 260f));
            Assert.Contains(sprites, packet => packet.Position == new Vector2(478.5f, 540f));
        }
        else
        {
            Assert.Contains("Paused", resolved);
            Assert.Contains("Audio and haptics", resolved);
            Assert.Contains("OFF", resolved);
            Assert.Contains("Abandon Climb", resolved);
            Assert.Contains(sprites, packet => packet.Position == new Vector2(234f, 260f));
            Assert.Contains(sprites, packet => packet.Position == new Vector2(478.5f, 540f));
        }
    }

    [Theory]
    [InlineData(0, 17, 9, "Keyboard hotkey hints")]
    [InlineData(1, 37, 7, "Xbox hotkey hints")]
    [InlineData(2, 17, 10, "PlayStation hotkey hints")]
    public void Hotkey_variants_author_device_specific_galleries_and_four_position_samples(
        int variant,
        int expectedSprites,
        int expectedTexts,
        string expectedTitle)
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = new SnapshotFixtureMaterializer()
            .Materialize(world, "hotkey-hints", variant);
        RenderPacket[] sprites = ExtractSprites(world);
        TextRenderPacket[] texts = ExtractText(world);
        var catalog = new StaticTextPresentationCatalog();
        string[] resolved = texts.Select(packet =>
        {
            Assert.True(catalog.TryResolve(packet.Content, out string? text));
            return text!;
        }).ToArray();

        Assert.Equal(SceneGroup.Snapshot, authored.Scene);
        Assert.Equal(expectedSprites, sprites.Length);
        Assert.Equal(expectedTexts, texts.Length);
        Assert.Contains(expectedTitle, resolved);
        Assert.Contains("Sample action", resolved);
        Assert.Contains(sprites, packet =>
            packet.Position == new Vector2(960f, 540f) &&
            packet.Scale == new Vector2(1920f, 1080f) &&
            packet.Tint == new Color(18, 20, 26));
        Assert.Contains(sprites, packet => packet.Position == new Vector2(960f, 682f));
        Assert.Contains(sprites, packet => packet.Position == new Vector2(960f, 848f));
        Assert.Contains(sprites, packet => packet.Position.X < 810f && packet.Position.Y == 765f);
        Assert.Contains(sprites, packet => packet.Position.X > 1110f && packet.Position.Y == 765f);
        Assert.All(sprites, packet => Assert.Contains(
            packet.Texture,
            new[] { Primary("hotkey-hints", variant), Secondary("hotkey-hints", variant) }));
    }

    [Theory]
    [InlineData("pause-menu", 0, 28)]
    [InlineData("pause-menu", 1, 28)]
    [InlineData("hotkey-hints", 0, 10)]
    [InlineData("hotkey-hints", 1, 28)]
    [InlineData("hotkey-hints", 2, 28)]
    public void Fixture_specific_texture_ids_bind_to_the_white_pixel_and_rounded_glyph_primitive(
        string fixtureId,
        int variant,
        int expectedCornerRadius)
    {
        HostTextureAssetCatalog catalog = ProductionHostTextureCatalog.Create();

        Assert.True(catalog.TryGet(Primary(fixtureId, variant), out HostTextureAssetBinding pixel));
        Assert.Equal(HostTextureSourceKind.GeneratedPrimitive, pixel.Source);
        Assert.Equal(GeneratedTextureRecipeKind.SolidPanel, pixel.GeneratedRecipe.Kind);
        Assert.Equal((1, 1), (pixel.GeneratedRecipe.Width, pixel.GeneratedRecipe.Height));
        Assert.Equal(Color.White, pixel.GeneratedRecipe.Fill);

        Assert.True(catalog.TryGet(Secondary(fixtureId, variant), out HostTextureAssetBinding rounded));
        Assert.Equal(HostTextureSourceKind.GeneratedPrimitive, rounded.Source);
        Assert.Equal(GeneratedTextureRecipeKind.RoundedPanel, rounded.GeneratedRecipe.Kind);
        Assert.Equal((56, 56, expectedCornerRadius),
            (rounded.GeneratedRecipe.Width, rounded.GeneratedRecipe.Height, rounded.GeneratedRecipe.CornerRadius));
    }

    [Theory]
    [InlineData(0, "Equipment/bulwark_plate")]
    [InlineData(1, "Equipment/knightly_grieves")]
    [InlineData(2, "Equipment/bulwark_plate")]
    public void Equipment_tooltip_variants_bind_their_real_equipment_art(
        int variant,
        string expectedAsset)
    {
        HostTextureAssetCatalog catalog = ProductionHostTextureCatalog.Create();

        Assert.True(catalog.TryGet(Primary("equipment-tooltip", variant), out var pixel));
        Assert.Equal(HostTextureSourceKind.GeneratedPrimitive, pixel.Source);
        Assert.Equal(GeneratedTextureRecipeKind.SolidPanel, pixel.GeneratedRecipe.Kind);
        Assert.True(catalog.TryGet(Secondary("equipment-tooltip", variant), out var equipment));
        Assert.Equal(HostTextureSourceKind.ContentAsset, equipment.Source);
        Assert.Equal(expectedAsset, equipment.ContentAssetName);
    }

    private static TextureAssetId Primary(string id, int variant)
    {
        int index = FixtureIndex(id);
        return SnapshotFixtureTextureIds.Primary(index, variant);
    }

    private static TextureAssetId Secondary(string id, int variant)
    {
        int index = FixtureIndex(id);
        return SnapshotFixtureTextureIds.Secondary(index, variant);
    }

    private static TextureAssetId Tertiary(string id, int variant)
    {
        int index = FixtureIndex(id);
        return SnapshotFixtureTextureIds.Tertiary(index, variant);
    }

    private static int FixtureIndex(string id)
    {
        NewWorldSnapshotFixture[] fixtures = new NewWorldSnapshotFixtureHost().Registered.ToArray();
        return System.Array.FindIndex(fixtures, fixture => fixture.Id == id);
    }

    private static RenderPacket[] ExtractSprites(World world)
    {
        var packets = new RenderPacketStore(96);
        new SpriteRenderExtractionSystem(world, packets).Extract();
        return Enumerable.Range((int)RenderLayer.Background, (int)RenderLayer.Debug + 1)
            .SelectMany(layer => packets.GetLayer((RenderLayer)layer).ToArray())
            .ToArray();
    }

    private static TextRenderPacket[] ExtractText(World world)
    {
        var packets = new TextRenderPacketStore(16);
        new TextRenderExtractionSystem(world, packets).Extract();
        return Enumerable.Range((int)RenderLayer.Background, (int)RenderLayer.Debug + 1)
            .SelectMany(layer => packets.GetLayer((RenderLayer)layer).ToArray())
            .ToArray();
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());
}
