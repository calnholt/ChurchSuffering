#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Authoring.Text;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Authoring.Meta;

/// <summary>Stable compact texture range reserved for data-oriented snapshot authoring.</summary>
public static class SnapshotFixtureTextureIds
{
    public const int Base = 47001;
    public const int PerFixture = 16;
    public const int PerVariant = 2;

    public static TextureAssetId Primary(int fixtureIndex, int variantIndex) =>
        new(Base + fixtureIndex * PerFixture + variantIndex * PerVariant);

    public static TextureAssetId Secondary(int fixtureIndex, int variantIndex) =>
        new(Base + fixtureIndex * PerFixture + variantIndex * PerVariant + 1);

    public static TextureAssetId Tertiary(int fixtureIndex, int variantIndex) =>
        new(57001 + fixtureIndex * PerFixture + variantIndex);
}

public readonly record struct PlayerHudSnapshotMaskSpec(
    TextureAssetId Id,
    int Width,
    int Height,
    int Slant);

/// <summary>
/// Host-generated masks used by the static player-HUD characterization fixture. The dimensions
/// are the same logical dimensions passed to the former production mask factory.
/// </summary>
public static class PlayerHudSnapshotMaskIds
{
    private const int Base = 59001;
    private static readonly PlayerHudSnapshotMaskSpec[] specs = BuildSpecs();

    public static ReadOnlySpan<PlayerHudSnapshotMaskSpec> Specs => specs;
    public static PlayerHudSnapshotMaskSpec[] CopySpecs() =>
        (PlayerHudSnapshotMaskSpec[])specs.Clone();

    public static TextureAssetId Resolve(float width, float height, float slant)
    {
        int roundedWidth = Math.Max(1, (int)MathF.Round(width));
        int roundedHeight = Math.Max(1, (int)MathF.Round(height));
        int roundedSlant = Math.Clamp((int)MathF.Round(slant), 0, roundedWidth);
        for (var index = 0; index < specs.Length; index++)
        {
            ref readonly PlayerHudSnapshotMaskSpec spec = ref specs[index];
            if (spec.Width == roundedWidth && spec.Height == roundedHeight && spec.Slant == roundedSlant)
                return spec.Id;
        }
        throw new InvalidOperationException(
            $"Player HUD mask {roundedWidth}x{roundedHeight} slant {roundedSlant} is not registered.");
    }

    private static PlayerHudSnapshotMaskSpec[] BuildSpecs()
    {
        var dimensions = new List<(int Width, int Height, int Slant)>(48);
        void Add(int width, int height, int slant)
        {
            var value = (width, height, slant);
            if (!dimensions.Contains(value)) dimensions.Add(value);
        }

        Add(559, 36, 14);
        Add(558, 36, 14);
        Add(491, 26, 6);
        Add(490, 26, 6);
        Add(487, 22, 5);
        Add(486, 22, 5);
        foreach (int width in new[] { 438, 73, 390, 311, 268 }) Add(width, 22, 5);
        foreach (int width in new[] { 110, 144, 85, 262 }) Add(width, 36, 14);
        Add(21, 26, 10);
        foreach (int width in new[] { 94, 98, 112, 124 }) Add(width, 32, 10);

        foreach (int baseWidth in new[] { 559, 110, 144, 85, 262 })
        foreach (int spread in new[] { 20, 16, 12, 8, 4 })
            Add(baseWidth + spread * 2, 36 + spread * 2, 14);

        var result = new PlayerHudSnapshotMaskSpec[dimensions.Count];
        for (var index = 0; index < dimensions.Count; index++)
        {
            (int width, int height, int slant) = dimensions[index];
            result[index] = new PlayerHudSnapshotMaskSpec(
                new TextureAssetId(Base + index), width, height, slant);
        }
        return result;
    }
}

public readonly record struct PlayerHudPassiveMaskSpec(TextureAssetId Id, int Width);

public static class PlayerHudPassiveMaskIds
{
    private static readonly PlayerHudPassiveMaskSpec[] specs =
    [
        new(new TextureAssetId(59101), 102),
        new(new TextureAssetId(59102), 104),
        new(new TextureAssetId(59103), 119),
        new(new TextureAssetId(59104), 130),
    ];

    public static ReadOnlySpan<PlayerHudPassiveMaskSpec> Specs => specs;
    public static PlayerHudPassiveMaskSpec[] CopySpecs() =>
        (PlayerHudPassiveMaskSpec[])specs.Clone();

    public static TextureAssetId Resolve(float width)
    {
        int rounded = (int)MathF.Round(width);
        foreach (PlayerHudPassiveMaskSpec spec in specs)
            if (spec.Width == rounded) return spec.Id;
        throw new InvalidOperationException($"Player HUD passive width {rounded} is not registered.");
    }
}

/// <summary>
/// Deterministic fixture materializer. ECS-052 fixture-specific definitions are dispatched here;
/// fixtures not yet ported continue to use the registered generic shell.
/// </summary>
public sealed class SnapshotFixtureMaterializer
{
    private readonly NewWorldSnapshotFixtureHost registry;

    public SnapshotFixtureMaterializer(NewWorldSnapshotFixtureHost? registry = null) =>
        this.registry = registry ?? new NewWorldSnapshotFixtureHost();

    public ReadOnlySpan<NewWorldSnapshotFixture> Registered => registry.Registered;

    public MetaAuthoredScene Materialize(
        Crusaders30XX.ECS.DataOriented.Core.World world,
        ReadOnlySpan<char> fixtureId,
        int variantIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!registry.TryResolve(fixtureId, out NewWorldSnapshotFixture fixture))
            throw new ArgumentException($"Unknown new-world snapshot fixture '{fixtureId.ToString()}'.", nameof(fixtureId));
        if ((uint)variantIndex >= (uint)fixture.VariantCount)
            throw new ArgumentOutOfRangeException(nameof(variantIndex), variantIndex,
                $"Fixture '{fixture.Id}' has {fixture.VariantCount} variants.");

        int fixtureIndex = FindFixtureIndex(fixture.Id);
        TextureAssetId primary = SnapshotFixtureTextureIds.Primary(fixtureIndex, variantIndex);
        TextureAssetId secondary = SnapshotFixtureTextureIds.Secondary(fixtureIndex, variantIndex);
        TextureAssetId tertiary = SnapshotFixtureTextureIds.Tertiary(fixtureIndex, variantIndex);
        MetaStaticSceneDefinition definition = fixture.Id switch
        {
            "pause-menu" => PauseMenuDefinition(fixture.Scene, variantIndex, primary, secondary),
            "hotkey-hints" => HotKeyDefinition(fixture.Scene, variantIndex, primary, secondary),
            "equipment-tooltip" => EquipmentTooltipDefinition(
                fixture.Scene, variantIndex, primary, secondary),
            "guardian-angel" => GuardianAngelDefinition(
                fixture.Scene, variantIndex, primary, secondary, tertiary),
            "enemy-damage-meter" => EnemyDamageMeterDefinition(
                fixture.Scene, variantIndex, primary),
            "player-hud" => PlayerHudDefinition(
                fixture.Scene, variantIndex, primary, secondary, tertiary),
            _ => GenericDefinition(fixture, fixtureIndex, variantIndex, primary, secondary),
        };
        return MetaStaticSceneMaterializer.Materialize(world, definition);
    }

    private static MetaStaticSceneDefinition PauseMenuDefinition(
        SceneGroup scene,
        int variantIndex,
        TextureAssetId pixel,
        TextureAssetId rounded)
    {
        var sprites = new List<MetaSceneSpriteDefinition>(80);
        var texts = new List<MetaSceneTextDefinition>(12);
        bool rumbleEnabled = variantIndex == 0;

        // The legacy fixture path drew the pause dimmer through both the normal display system
        // and its fixture callback, reducing the fixture backdrop to this approved frame color.
        AddRect(sprites, pixel, 960f, 540f, 1920f, 1080f, new Color(4, 3, 4), -100);
        AddRect(sprites, pixel, 240f, 540f, 480f, 1080f, new Color(7, 7, 7), 0);
        AddRect(sprites, pixel, 478.5f, 540f, 19f, 920f, new Color(47, 15, 19), 1);
        AddRect(sprites, pixel, 477f, 540f, 6f, 920f, new Color(99, 25, 34), 2);
        AddRect(sprites, pixel, 481.5f, 540f, 3f, 920f, new Color(110, 23, 35), 2);
        AddRect(sprites, pixel, 478.5f, 540f, 3f, 920f, new Color(255, 77, 98), 3);

        AddSlider(sprites, pixel, rounded, 258f);
        AddSlider(sprites, pixel, rounded, 410f);

        AddRect(sprites, pixel, 358f, 521f, 92f, 42f,
            rumbleEnabled ? new Color(255, 166, 177) : new Color(142, 142, 142), 20);
        AddRect(sprites, pixel, 358f, 521f, 88f, 38f,
            rumbleEnabled ? new Color(255, 77, 98) : new Color(30, 30, 30), 21);
        AddRect(sprites, pixel, 234f, 1006f, 340f, 52f, new Color(142, 142, 142), 20);
        AddRect(sprites, pixel, 234f, 1006f, 336f, 48f, new Color(30, 30, 30), 21);
        AddRound(sprites, rounded, 1771f, 1033f, 51f, 26f, new Color(105, 105, 105), 20);
        AddRound(sprites, rounded, 1771f, 1033f, 47f, 22f, new Color(62, 62, 62), 21);

        AddText(texts, SnapshotTextContentIds.Paused, SnapshotTextStyleIds.DisplayExact,
            64f, 76f, .41f, Color.Black * .9f, 999, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.Paused, SnapshotTextStyleIds.DisplayExact,
            64f, 72f, .41f, Color.White, 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.AudioAndHaptics, SnapshotTextStyleIds.HudExact,
            64f, 136f, .10f, new Color(200, 192, 184), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.Music, SnapshotTextStyleIds.HudExact,
            64f, 200f, .09f, new Color(200, 192, 184), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.FiftyPercent, SnapshotTextStyleIds.HudExact,
            341f, 194f, .17f, new Color(196, 30, 58), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.Sfx, SnapshotTextStyleIds.HudExact,
            64f, 352f, .09f, new Color(200, 192, 184), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.FiftyPercent, SnapshotTextStyleIds.HudExact,
            341f, 346f, .17f, new Color(196, 30, 58), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.Rumble, SnapshotTextStyleIds.HudExact,
            64f, 500f, .10f, new Color(200, 192, 184), 1000, TextAlignment.TopLeft);
        AddText(texts, rumbleEnabled ? SnapshotTextContentIds.On : SnapshotTextContentIds.Off,
            SnapshotTextStyleIds.HudExact,
            358f, 519f, .13f, new Color(240, 236, 230), 1000, TextAlignment.Center);
        AddText(texts, SnapshotTextContentIds.AbandonClimb, SnapshotTextStyleIds.HudExact,
            234f, 1004f, .13f, new Color(240, 236, 230), 1000, TextAlignment.Center);
        AddText(texts, SnapshotTextContentIds.Escape, SnapshotTextStyleIds.HudExact,
            1771f, 1031f, .10f, Color.White * .45f, 1000, TextAlignment.Center);
        AddText(texts, SnapshotTextContentIds.Resume, SnapshotTextStyleIds.HudExact,
            1806f, 1024f, .10f, Color.White * .45f, 1000, TextAlignment.TopLeft);
        return new MetaStaticSceneDefinition(scene, sprites.ToArray(), texts.ToArray());
    }

    private static void AddSlider(
        List<MetaSceneSpriteDefinition> sprites,
        TextureAssetId pixel,
        TextureAssetId rounded,
        float trackY)
    {
        AddRect(sprites, pixel, 234f, trackY + 2.5f, 340f, 5f, new Color(37, 37, 37), 10);
        const int fillWidth = 170;
        const int steps = 24;
        float stepWidth = fillWidth / (float)steps;
        for (var index = 0; index < steps; index++)
        {
            float amount = index / (float)(steps - 1);
            int x = 64 + (int)MathF.Round(index * stepWidth);
            int next = index == steps - 1
                ? 64 + fillWidth
                : 64 + (int)MathF.Round((index + 1) * stepWidth);
            AddRect(sprites, pixel, x + (next - x) / 2f, trackY + 2.5f,
                Math.Max(1, next - x), 5f,
                Color.Lerp(new Color(160, 0, 0), new Color(196, 30, 58), amount), 11);
        }
        AddRound(sprites, rounded, 234f, trackY + 2f, 48f, 48f, new Color(92, 17, 31), 12);
        AddRound(sprites, rounded, 234f, trackY + 2f, 30f, 30f, new Color(196, 30, 58), 13);
        AddRound(sprites, rounded, 234f, trackY + 2f, 24f, 24f, Color.White, 14);
    }

    private static MetaStaticSceneDefinition HotKeyDefinition(
        SceneGroup scene,
        int variantIndex,
        TextureAssetId pixel,
        TextureAssetId rounded)
    {
        var sprites = new List<MetaSceneSpriteDefinition>(52);
        var texts = new List<MetaSceneTextDefinition>(16);
        AddRect(sprites, pixel, 960f, 540f, 1920f, 1080f, new Color(18, 20, 26), -100);

        StringId title = variantIndex switch
        {
            0 => SnapshotTextContentIds.KeyboardHotKeyHints,
            1 => SnapshotTextContentIds.XboxHotKeyHints,
            _ => SnapshotTextContentIds.PlayStationHotKeyHints,
        };
        AddText(texts, title, SnapshotTextStyleIds.DisplayExact,
            120f, 90f, .35f, Color.White, 1000, TextAlignment.TopLeft);

        if (variantIndex == 0)
        {
            AddKeyboardBadge(sprites, texts, rounded, 520f, 350f, 76f, SnapshotTextContentIds.Escape);
            AddKeyboardBadge(sprites, texts, rounded, 960f, 350f, 116f, SnapshotTextContentIds.Enter);
            AddKeyboardBadge(sprites, texts, rounded, 1400f, 350f, 116f, SnapshotTextContentIds.Space);
        }
        else
        {
            bool playStation = variantIndex == 2;
            AddFaceButton(sprites, texts, rounded, 300f, 320f, SnapshotTextContentIds.B,
                playStation ? new Color(220, 60, 60) : new Color(220, 50, 50), playStation, pixel);
            AddFaceButton(sprites, texts, rounded, 740f, 320f, SnapshotTextContentIds.X,
                playStation ? new Color(200, 80, 200) : new Color(60, 120, 220), playStation, pixel);
            AddFaceButton(sprites, texts, rounded, 1180f, 320f, SnapshotTextContentIds.Y,
                playStation ? new Color(50, 200, 90) : new Color(220, 200, 60), playStation, pixel);
            AddSystemBadge(sprites, texts, pixel, rounded, 1620f, 320f, view: true, playStation);
            AddSystemBadge(sprites, texts, pixel, rounded, 300f, 470f, view: false, playStation);
            AddPill(sprites, texts, rounded, 740f, 470f,
                playStation ? SnapshotTextContentIds.L1 : SnapshotTextContentIds.Lb);
            AddPill(sprites, texts, rounded, 1180f, 470f,
                playStation ? SnapshotTextContentIds.R1 : SnapshotTextContentIds.Rb);
        }

        AddRect(sprites, pixel, 960f, 765f, 300f, 90f, new Color(150, 158, 180), 20);
        AddRect(sprites, pixel, 960f, 765f, 296f, 86f, new Color(54, 58, 70), 21);
        AddText(texts, SnapshotTextContentIds.SampleAction, SnapshotTextStyleIds.HudExact,
            960f, 763f, .16f, Color.White, 1000, TextAlignment.Center);

        if (variantIndex == 0)
        {
            AddKeyboardBadge(sprites, texts, rounded, 960f, 682f, 116f, SnapshotTextContentIds.Enter);
            AddKeyboardBadge(sprites, texts, rounded, 1186f, 765f, 116f, SnapshotTextContentIds.Enter);
            AddKeyboardBadge(sprites, texts, rounded, 734f, 765f, 116f, SnapshotTextContentIds.Enter);
            AddKeyboardBadge(sprites, texts, rounded, 960f, 848f, 116f, SnapshotTextContentIds.Enter);
        }
        else
        {
            bool playStation = variantIndex == 2;
            AddSystemBadge(sprites, texts, pixel, rounded, 960f, 682f, view: false, playStation);
            AddSystemBadge(sprites, texts, pixel, rounded, 1158f, 765f, view: false, playStation);
            AddSystemBadge(sprites, texts, pixel, rounded, 762f, 765f, view: false, playStation);
            AddSystemBadge(sprites, texts, pixel, rounded, 960f, 848f, view: false, playStation);
        }
        return new MetaStaticSceneDefinition(scene, sprites.ToArray(), texts.ToArray());
    }

    private static void AddKeyboardBadge(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        TextureAssetId rounded,
        float x,
        float y,
        float width,
        StringId label)
    {
        AddRound(sprites, rounded, x, y, width, 39f, new Color(215, 215, 215), 20);
        AddRound(sprites, rounded, x, y, width - 4f, 35f, new Color(62, 62, 62), 21);
        AddText(texts, label, SnapshotTextStyleIds.HudExact,
            x, y - 2f, .18f, Color.White, 1000, TextAlignment.Center);
    }

    private static void AddFaceButton(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        TextureAssetId rounded,
        float x,
        float y,
        StringId label,
        Color faceColor,
        bool playStation,
        TextureAssetId pixel)
    {
        if (!playStation)
        {
            AddRound(sprites, rounded, x, y, 56f, 56f, faceColor, 20);
            AddText(texts, label, SnapshotTextStyleIds.HudExact,
                x, y - 2f, .18f, Color.Black, 1000, TextAlignment.Center);
            return;
        }

        AddRound(sprites, rounded, x, y, 56f, 56f, new Color(36, 36, 36), 20);
        if (label == SnapshotTextContentIds.X)
            AddRect(sprites, pixel, x, y, 34f, 34f, faceColor, 21);
        else
            AddRound(sprites, rounded, x, y, label == SnapshotTextContentIds.Y ? 30f : 34f,
                label == SnapshotTextContentIds.Y ? 30f : 34f, faceColor, 21);
    }

    private static void AddPill(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        TextureAssetId rounded,
        float x,
        float y,
        StringId label)
    {
        AddRound(sprites, rounded, x, y, 76f, 41f, new Color(36, 36, 36), 20);
        AddText(texts, label, SnapshotTextStyleIds.HudExact,
            x, y - 2f, .18f, Color.White, 1000, TextAlignment.Center);
    }

    private static void AddSystemBadge(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        TextureAssetId pixel,
        TextureAssetId rounded,
        float x,
        float y,
        bool view,
        bool playStation)
    {
        AddRound(sprites, rounded, x, y, 59f, 41f, new Color(36, 36, 36), 20);
        if (playStation)
        {
            AddText(texts, view ? SnapshotTextContentIds.Create : SnapshotTextContentIds.Options,
                SnapshotTextStyleIds.HudExact, x, y - 2f, view ? .078f : .1125f,
                Color.White, 1000, TextAlignment.Center);
            return;
        }

        if (!view)
        {
            for (var line = -1; line <= 1; line++)
                AddRound(sprites, rounded, x, y + line * 7f, 25f, 4f, Color.White, 22);
            return;
        }

        AddOutline(sprites, pixel, x - 4f, y - 3f, 18f, 18f, 2f, Color.White, 22);
        AddOutline(sprites, pixel, x + 4f, y + 3f, 18f, 18f, 2f, Color.White, 23);
    }

    private static void AddOutline(
        List<MetaSceneSpriteDefinition> sprites,
        TextureAssetId pixel,
        float x,
        float y,
        float width,
        float height,
        float thickness,
        Color color,
        int z)
    {
        AddRect(sprites, pixel, x, y - height / 2f + thickness / 2f, width, thickness, color, z);
        AddRect(sprites, pixel, x, y + height / 2f - thickness / 2f, width, thickness, color, z);
        AddRect(sprites, pixel, x - width / 2f + thickness / 2f, y, thickness, height, color, z);
        AddRect(sprites, pixel, x + width / 2f - thickness / 2f, y, thickness, height, color, z);
    }

    private static MetaStaticSceneDefinition EquipmentTooltipDefinition(
        SceneGroup scene,
        int variantIndex,
        TextureAssetId pixel,
        TextureAssetId equipmentArt)
    {
        var sprites = new List<MetaSceneSpriteDefinition>(18);
        var texts = new List<MetaSceneTextDefinition>(8);
        bool passive = variantIndex == 1;
        bool used = variantIndex == 2;

        AddRect(sprites, pixel, 960f, 540f, 1920f, 1080f, new Color(64, 38, 26), -100);
        if (passive)
            AddPassiveEquipmentTooltip(sprites, texts, pixel, equipmentArt);
        else
            AddActiveEquipmentTooltip(sprites, texts, pixel, equipmentArt, used);
        return new MetaStaticSceneDefinition(scene, sprites.ToArray(), texts.ToArray());
    }

    private static void AddActiveEquipmentTooltip(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        TextureAssetId pixel,
        TextureAssetId equipmentArt,
        bool used)
    {
        Color panel = used ? new Color(116, 102, 93) : new Color(210, 205, 196);

        // Chest slot panel at row one (Head is row zero), followed by its hover tooltip.
        AddRect(sprites, pixel, 554f, 631.5f, 108f, 133f, panel, 0);
        AddRect(sprites, equipmentArt, 554f, 631f, 52f, 52f,
            used ? Color.White * .4f : Color.White, 10);
        AddRect(sprites, pixel, 511f, 576f, 10f, 8f,
            used ? new Color(148, 73, 79) : new Color(196, 30, 58), 20);

        AddRect(sprites, pixel, 778f, 631f, 300f, 148f, new Color(220, 215, 206), 0);
        AddRect(sprites, pixel, 655f, 631f, 54f, 148f, new Color(210, 205, 196), 1);
        AddRect(sprites, equipmentArt, 655f, 586f, 38f, 38f, Color.White, 10);
        AddRect(sprites, pixel, 803f, 604.5f, 222f, 2f, new Color(192, 184, 170), 10);
        AddRect(sprites, pixel, 803f, 627.5f, 222f, 31f, new Color(238, 233, 222), 10);
        AddRect(sprites, pixel, 738f, 682f, 92f, 22f, new Color(170, 170, 170), 10);
        AddRect(sprites, pixel, 738f, 682f, 90f, 20f, new Color(220, 215, 206), 11);

        AddText(texts, SnapshotTextContentIds.BulwarkPlate, SnapshotTextStyleIds.DisplayExact,
            692f, 569f, .14f, new Color(26, 26, 26), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.GainTwoAegis, SnapshotTextStyleIds.HudExact,
            698f, 616f, .10f, new Color(26, 26, 26), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.FreeAction, SnapshotTextStyleIds.HudExact,
            700f, 674f, .07f, new Color(136, 136, 136), 1000, TextAlignment.TopLeft);
    }

    private static void AddPassiveEquipmentTooltip(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        TextureAssetId pixel,
        TextureAssetId equipmentArt)
    {
        // Legs occupy row three. The panel socket and tooltip gutter share the black-card gutter.
        AddRect(sprites, pixel, 554f, 921.5f, 108f, 133f, new Color(19, 19, 19), 0);
        AddRect(sprites, pixel, 554f, 893f, 108f, 76f, new Color(25, 25, 25), 1);
        AddRect(sprites, equipmentArt, 554f, 893f, 52f, 52f, Color.White, 10);
        AddRect(sprites, pixel, 554f, 945.5f, 92f, 13f, new Color(31, 51, 67), 10);
        AddRect(sprites, pixel, 554f, 966f, 92f, 28f, new Color(42, 74, 94), 10);

        AddRect(sprites, pixel, 778f, 921.5f, 300f, 165f, new Color(19, 19, 19), 0);
        AddRect(sprites, pixel, 655f, 921.5f, 54f, 165f, new Color(25, 25, 25), 1);
        AddRect(sprites, equipmentArt, 655f, 868f, 38f, 38f, Color.White, 10);
        AddRect(sprites, pixel, 655f, 901.5f, 38f, 13f, new Color(35, 55, 71), 10);
        AddRect(sprites, pixel, 655f, 920f, 38f, 25f, new Color(42, 74, 94), 10);
        AddRect(sprites, pixel, 803f, 887.5f, 222f, 2f, new Color(51, 51, 51), 10);
        AddRect(sprites, pixel, 803f, 942.5f, 222f, 97f, new Color(8, 8, 8), 10);

        AddText(texts, SnapshotTextContentIds.KnightlyGrieves, SnapshotTextStyleIds.DisplayExact,
            692f, 851f, .14f, new Color(232, 228, 224), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.Block, SnapshotTextStyleIds.HudExact,
            554f, 941f, .06f, new Color(138, 184, 216), 1000, TextAlignment.TopCenter);
        AddText(texts, SnapshotTextContentIds.Two, SnapshotTextStyleIds.DisplayExact,
            554f, 950f, .13f, new Color(176, 212, 232), 1000, TextAlignment.TopCenter);
        AddText(texts, SnapshotTextContentIds.Block, SnapshotTextStyleIds.HudExact,
            655f, 897f, .06f, new Color(138, 184, 216), 1000, TextAlignment.TopCenter);
        AddText(texts, SnapshotTextContentIds.Two, SnapshotTextStyleIds.DisplayExact,
            655f, 906f, .13f, new Color(176, 212, 232), 1000, TextAlignment.TopCenter);
        AddText(texts, SnapshotTextContentIds.KnightlyFlavorLineOne,
            SnapshotTextStyleIds.HudBoldItalicExact,
            698f, 898f, .10f, new Color(232, 228, 224), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.KnightlyFlavorLineTwo,
            SnapshotTextStyleIds.HudBoldItalicExact,
            698f, 920f, .10f, new Color(232, 228, 224), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.KnightlyFlavorLineThree,
            SnapshotTextStyleIds.HudBoldItalicExact,
            698f, 943f, .10f, new Color(232, 228, 224), 1000, TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.KnightlyFlavorLineFour,
            SnapshotTextStyleIds.HudBoldItalicExact,
            698f, 965f, .10f, new Color(232, 228, 224), 1000, TextAlignment.TopLeft);
    }

    private static MetaStaticSceneDefinition GuardianAngelDefinition(
        SceneGroup scene,
        int variantIndex,
        TextureAssetId backdrop,
        TextureAssetId angel,
        TextureAssetId pixel)
    {
        var sprites = new List<MetaSceneSpriteDefinition>(4);
        var texts = new List<MetaSceneTextDefinition>(1);
        AddRect(sprites, backdrop, 960f, 540f, 1920f, 1080f, Color.White, -100);
        AddRect(sprites, pixel, 960f, 540f, 1920f, 1080f, new Color(10, 6, 12, 90), -90);

        Vector2 position;
        Vector2 size;
        switch (variantIndex)
        {
            case 1:
                position = new Vector2(1002.23f, 535.63f);
                size = new Vector2(69.45f, 88.17f);
                AddRect(sprites, pixel, 1157.5f, 455f, 245f, 80f, Color.White * .82f, 0);
                AddText(texts, SnapshotTextContentIds.GuardianStayClose,
                    SnapshotTextStyleIds.HudExact,
                    1043f, 423f, .14f, Color.Black, 1000, TextAlignment.TopLeft);
                break;
            case 2:
                position = new Vector2(983f, 522f);
                size = new Vector2(70.50f, 89.51f);
                AddRect(sprites, pixel, 1146.5f, 457f, 261f, 48f, Color.White * .82f, 0);
                AddText(texts, SnapshotTextContentIds.GuardianHonestStrike,
                    SnapshotTextStyleIds.HudExact,
                    1024f, 441f, .14f, Color.Black, 1000, TextAlignment.TopLeft);
                break;
            case 3:
                position = new Vector2(983f, 528f);
                size = new Vector2(70.50f, 89.51f);
                AddRect(sprites, pixel, 1140f, 448f, 248f, 80f, Color.White * .82f, 0);
                AddText(texts, SnapshotTextContentIds.GuardianSaintMichael,
                    SnapshotTextStyleIds.HudExact,
                    1024f, 416f, .14f, Color.Black, 1000, TextAlignment.TopLeft);
                break;
            case 4:
                position = new Vector2(953f, 550f);
                size = new Vector2(70.50f, 89.51f);
                AddRect(sprites, pixel, 1121.5f, 485.5f, 271f, 49f, Color.White * .82f, 0);
                AddText(texts, SnapshotTextContentIds.GuardianBigFoot,
                    SnapshotTextStyleIds.HudExact,
                    994f, 469f, .14f, Color.Black, 1000, TextAlignment.TopLeft);
                break;
            default:
                position = new Vector2(983f, 550f);
                size = new Vector2(65.28f, 82.88f);
                break;
        }

        AddRect(sprites, angel, position.X, position.Y, size.X, size.Y, Color.White, 10);
        return new MetaStaticSceneDefinition(scene, sprites.ToArray(), texts.ToArray());
    }

    private static MetaStaticSceneDefinition EnemyDamageMeterDefinition(
        SceneGroup scene,
        int variantIndex,
        TextureAssetId pixel)
    {
        var sprites = new List<MetaSceneSpriteDefinition>(14);
        var texts = new List<MetaSceneTextDefinition>(4);
        AddRect(sprites, pixel, 960f, 540f, 1920f, 1080f, new Color(32, 26, 25), -100);

        bool absorb = variantIndex == 3;
        float anchorX = absorb ? 960f : 960f;
        float anchorY = absorb ? 302.5f : 300f;
        float anchorWidth = absorb ? 500f : 660f;
        float anchorHeight = absorb ? 165f : 220f;
        AddRect(sprites, pixel, anchorX, anchorY, anchorWidth, anchorHeight,
            new Color(20, 20, 20, 220), -10);
        AddOutline(sprites, pixel, anchorX, anchorY, anchorWidth, anchorHeight, 2f, Color.White, -9);

        switch (variantIndex)
        {
            case 1:
                AddRect(sprites, pixel, 899.5f, 292f, 195f, 42f, new Color(209, 78, 78), 0);
                AddRect(sprites, pixel, 996f, 300f, 34f, 24f, new Color(36, 36, 36), 1);
                AddRect(sprites, pixel, 1048f, 300f, 72f, 40f, Color.White, 2);
                AddRect(sprites, pixel, 1096.5f, 300f, 37f, 24f, new Color(60, 122, 60), 3);
                AddDamageMeterText(texts, SnapshotTextContentIds.Six, 899f, 292f, Color.White, .20f);
                AddDamageMeterText(texts, SnapshotTextContentIds.Two, 995f, 300f, new Color(163, 163, 163), .12f);
                AddDamageMeterText(texts, SnapshotTextContentIds.Three, 1048f, 300f, Color.Black, .20f);
                AddDamageMeterText(texts, SnapshotTextContentIds.One, 1095f, 300f, new Color(173, 199, 173), .12f);
                break;
            case 2:
                AddRect(sprites, pixel, 847f, 292f, 68f, 40f, new Color(200, 40, 40), 0);
                AddRect(sprites, pixel, 939.5f, 300f, 169f, 40f, Color.Black, 1);
                AddRect(sprites, pixel, 1017.5f, 300f, 79f, 40f, Color.White, 2);
                AddRect(sprites, pixel, 1073f, 300f, 68f, 40f, new Color(50, 180, 50), 3);
                AddDamageMeterText(texts, SnapshotTextContentIds.Two, 847f, 292f, Color.White, .20f);
                AddDamageMeterText(texts, SnapshotTextContentIds.Five, 925f, 300f, Color.White, .20f);
                AddDamageMeterText(texts, SnapshotTextContentIds.Three, 1017f, 300f, Color.Black, .20f);
                AddDamageMeterText(texts, SnapshotTextContentIds.Two, 1073f, 300f, Color.White, .20f);
                break;
            case 3:
                AddRect(sprites, pixel, 946f, 232.5f, 8f, 5f, new Color(200, 40, 40), 0);
                AddRect(sprites, pixel, 956f, 233f, 12f, 4f, Color.Black, 1);
                AddRect(sprites, pixel, 965f, 233f, 8f, 4f, Color.White, 2);
                AddRect(sprites, pixel, 974f, 233f, 8f, 4f, new Color(50, 180, 50), 3);
                break;
            default:
                AddRect(sprites, pixel, 926.5f, 292f, 243f, 40f, new Color(200, 40, 40), 0);
                AddRect(sprites, pixel, 1072f, 300f, 86f, 40f, Color.White, 1);
                AddDamageMeterText(texts, SnapshotTextContentIds.Nine, 926f, 292f, Color.White, .20f);
                AddDamageMeterText(texts, SnapshotTextContentIds.Three, 1067f, 300f, Color.Black, .20f);
                break;
        }

        return new MetaStaticSceneDefinition(scene, sprites.ToArray(), texts.ToArray());
    }

    private static void AddDamageMeterText(
        List<MetaSceneTextDefinition> texts,
        StringId content,
        float x,
        float y,
        Color tint,
        float scale) => AddText(texts, content, SnapshotTextStyleIds.DisplayExact,
        x, y, scale, tint, 1000, TextAlignment.Center);

    private static MetaStaticSceneDefinition PlayerHudDefinition(
        SceneGroup scene,
        int variantIndex,
        TextureAssetId pixel,
        TextureAssetId portrait,
        TextureAssetId pledgeIcon)
    {
        var sprites = new List<MetaSceneSpriteDefinition>(2400);
        var texts = new List<MetaSceneTextDefinition>(14);
        AddRect(sprites, pixel, 960f, 540f, 1920f, 1080f, new Color(64, 38, 26), -100);

        if (variantIndex == 5)
        {
            AddRect(sprites, portrait, 960f, 260f, 258.5f, 388.8f, Color.White, 0);
            AddPlayerHealthBar(sprites, texts, pixel, 681f, 458f, 558f, 36f,
                .64f, SnapshotTextContentIds.ThirtyTwoOfFifty, incomingFraction: 0f);
            OffsetPlayerHud(sprites, texts, firstHudSprite: 2, new Vector2(-1f, 0f));
            return new MetaStaticSceneDefinition(scene, sprites.ToArray(), texts.ToArray());
        }

        AddRect(sprites, portrait, 960f, 260f, 344.88f, 492.12f, Color.White, 0);
        AddPlayerHudShadows(sprites, pixel, variantIndex != 1);
        float healthFraction = variantIndex switch
        {
            3 => .15f,
            4 => .8f,
            _ => .9f,
        };
        StringId healthText = variantIndex switch
        {
            3 => SnapshotTextContentIds.ThreeOfTwenty,
            4 => SnapshotTextContentIds.OneTwentyOfOneFifty,
            _ => SnapshotTextContentIds.EighteenOfTwenty,
        };
        AddPlayerHealthBar(sprites, texts, pixel, 688f, 510f, 559f, 36f,
            healthFraction, healthText, variantIndex == 2 ? .55f : 0f);

        AddParallelogram(sprites, pixel, 674f, 545f, 110f, 36f, 14f, new Color(196, 30, 58), 10);
        AddParallelogram(sprites, pixel, 770f, 545f, 144f, 36f, 14f, new Color(10, 10, 10), 10);
        AddParallelogram(sprites, pixel, 900f, 545f, 85f, 36f, 14f, Color.White, 10);
        if (variantIndex != 1)
            AddParallelogram(sprites, pixel, 971f, 545f, 262f, 36f, 14f, new Color(10, 10, 10), 10);

        AddText(texts, SnapshotTextContentIds.Cour, SnapshotTextStyleIds.HudExact,
            variantIndex == 4 ? 676f : 684f, 552f, .10f, Color.White, 1000,
            TextAlignment.TopLeft, letterSpacing: 2f);
        AddText(texts, variantIndex == 4 ? SnapshotTextContentIds.OneTwoThree : SnapshotTextContentIds.Twelve,
            SnapshotTextStyleIds.DisplayExact,
            variantIndex == 4 ? 736f : 746f, variantIndex == 4 ? 543f : 546f,
            .20f, Color.White, 1000,
            TextAlignment.TopLeft);
        AddText(texts, SnapshotTextContentIds.Temp, SnapshotTextStyleIds.HudExact,
            variantIndex == 4 ? 783f : 795f, variantIndex == 4 ? 552f : 554f,
            .10f, Color.White, 1000,
            TextAlignment.TopLeft, letterSpacing: 2f);
        AddTemperanceChunks(sprites, pixel, variantIndex == 4 ? 4 : 2);
        AddText(texts, SnapshotTextContentIds.Ap, SnapshotTextStyleIds.HudExact,
            variantIndex == 4 ? 911f : 920f, variantIndex == 4 ? 552f : 553f,
            .10f, new Color(10, 10, 10), 1000,
            TextAlignment.TopLeft, letterSpacing: 2f);
        AddText(texts, variantIndex == 4 ? SnapshotTextContentIds.Twelve : SnapshotTextContentIds.One,
            SnapshotTextStyleIds.DisplayExact,
            variantIndex == 4 ? 943f : 954f, variantIndex == 4 ? 543f : 546f,
            .20f, new Color(10, 10, 10), 1000,
            TextAlignment.TopLeft);
        if (variantIndex != 1)
        {
            AddText(texts, SnapshotTextContentIds.PledgeAvailable, SnapshotTextStyleIds.HudExact,
                994f, 552f, .10f, Color.White, 1000, TextAlignment.TopLeft, letterSpacing: 2f);
            AddRect(sprites, pledgeIcon, 1201f, 563f, 23f, 28f, Color.White, 100);
        }

        if (variantIndex == 4)
        {
            AddPassiveChip(sprites, texts, pixel, 776f, SnapshotTextContentIds.TwoAegis,
                102f, 614f, useLegacyTrapezoid: true);
            AddPassiveChip(sprites, texts, pixel, 885f, SnapshotTextContentIds.OneArmor,
                104f, 614f, useLegacyTrapezoid: true);
            AddPassiveChip(sprites, texts, pixel, 1002.5f, SnapshotTextContentIds.TenPower,
                119f, 614f, useLegacyTrapezoid: true);
            AddPassiveChip(sprites, texts, pixel, 1133f, SnapshotTextContentIds.TwentyFiveThorns,
                130f, 614f, useLegacyTrapezoid: true);
        }
        else
        {
            AddPassiveChip(sprites, texts, pixel, 906f, SnapshotTextContentIds.TwoAegis, 98f);
            AddPassiveChip(sprites, texts, pixel, 1016f, SnapshotTextContentIds.OneArmor, 98f);
        }

        OffsetPlayerHud(sprites, texts, firstHudSprite: 2, new Vector2(1f, 1f));
        return new MetaStaticSceneDefinition(scene, sprites.ToArray(), texts.ToArray());
    }

    private static void OffsetPlayerHud(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        int firstHudSprite,
        Vector2 offset)
    {
        for (var index = firstHudSprite; index < sprites.Count; index++)
            sprites[index] = sprites[index] with { Position = sprites[index].Position + offset };
        for (var index = 0; index < texts.Count; index++)
            texts[index] = texts[index] with { Position = texts[index].Position + offset };
    }

    private static void AddPlayerHealthBar(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        TextureAssetId pixel,
        float x,
        float y,
        float width,
        float height,
        float healthFraction,
        StringId fractionText,
        float incomingFraction)
    {
        AddParallelogram(sprites, pixel, x, y, width, height, 14f,
            new Color(10, 10, 10), 10);
        const float trackXOffset = 50f;
        float trackWidth = width - 68f;
        AddParallelogram(sprites, pixel, x + trackXOffset, y + 5f,
            trackWidth, 26f, 6f, new Color(196, 30, 58), 11);
        AddParallelogram(sprites, pixel, x + trackXOffset + 2f, y + 7f,
            trackWidth - 4f, 22f, 5f, new Color(10, 10, 10), 12);
        float fillWidth = MathF.Round((trackWidth - 4f) * healthFraction);
        Color fillColor = healthFraction <= .15f
            ? new Color(112, 21, 36)
            : new Color(196, 30, 58);
        AddParallelogram(sprites, pixel, x + trackXOffset + 2f, y + 7f,
            fillWidth, 22f, MathF.Min(5f, fillWidth), fillColor, 13);
        if (incomingFraction > 0f)
        {
            float incomingWidth = MathF.Round((trackWidth - 4f) * incomingFraction);
            AddParallelogram(sprites, pixel,
                x + trackXOffset + 2f + fillWidth - incomingWidth, y + 7f,
                incomingWidth, 22f, MathF.Min(5f, incomingWidth), new Color(217, 109, 127), 14);
            AddRect(sprites, pixel, 1046.5f, y + 7.5f,
                261f, 1f, new Color(186, 86, 103), 15);
        }
        AddText(texts, SnapshotTextContentIds.Hp, SnapshotTextStyleIds.HudExact,
            x + 27f, y + 18f, .10f, Color.White, 1000, TextAlignment.Center, letterSpacing: 2f);
        AddText(texts, fractionText, SnapshotTextStyleIds.HudExact,
            x + trackXOffset + trackWidth / 2f, y + 18f, .10f,
            Color.White, 1000, TextAlignment.Center);
    }

    private static void AddTemperanceChunks(
        List<MetaSceneSpriteDefinition> sprites,
        TextureAssetId pixel,
        int filled)
    {
        int threshold = filled == 4 ? 4 : 2;
        float startX = threshold == 4 ? 853f : 866f;
        for (var index = 0; index < threshold; index++)
            AddParallelogram(sprites, pixel, startX + index * 13f - 10.5f, 550f,
                21f, 26f, 10f, index < filled ? Color.White : new Color(255, 255, 255, 36), 20);
    }

    private static void AddPassiveChip(
        List<MetaSceneSpriteDefinition> sprites,
        List<MetaSceneTextDefinition> texts,
        TextureAssetId pixel,
        float x,
        StringId content,
        float width,
        float y = 614f,
        bool useLegacyTrapezoid = false)
    {
        if (useLegacyTrapezoid)
        {
            sprites.Add(new MetaSceneSpriteDefinition(
                PlayerHudPassiveMaskIds.Resolve(width), new Vector2(x, 613.5f),
                new Vector2(width, 35f), new Color(139, 0, 0), 20)
            {
                Flags = SpriteFlags.Visible | SpriteFlags.PixelAlignedDestination,
            });
        }
        else
        {
            AddParallelogram(sprites, pixel, x - width / 2f, y - 16f, width, 32f, 10f,
                new Color(139, 0, 0), 20);
            AddRect(sprites, pixel, x, y + 15.5f, width, 1f, new Color(64, 38, 26), 21);
        }
        AddText(texts, content, SnapshotTextStyleIds.HudExact,
            x - 4f, y, .13f, Color.White, 1000, TextAlignment.Center);
    }

    private static void AddPlayerHudShadows(
        List<MetaSceneSpriteDefinition> sprites,
        TextureAssetId pixel,
        bool includePledge)
    {
        (float X, float Y, float Width, float Height)[] regions = includePledge
            ?
            [
                (688f, 510f, 559f, 36f),
                (674f, 545f, 110f, 36f),
                (770f, 545f, 144f, 36f),
                (900f, 545f, 85f, 36f),
                (971f, 545f, 262f, 36f),
            ]
            :
            [
                (688f, 510f, 559f, 36f),
                (674f, 545f, 110f, 36f),
                (770f, 545f, 144f, 36f),
                (900f, 545f, 85f, 36f),
            ];
        for (var layer = 5; layer >= 1; layer--)
        {
            float fraction = layer / 5f;
            int spread = (int)MathF.Round(20f * fraction);
            byte alpha = (byte)MathF.Round(140f * (1f - fraction * .72f) / 5f);
            foreach ((float x, float y, float width, float height) in regions)
                AddParallelogram(sprites, pixel,
                    x - spread,
                    y + 6f - spread,
                    width + spread * 2f,
                    height + spread * 2f,
                    14f,
                    new Color((byte)0, (byte)0, (byte)0, alpha),
                    -20 + (5 - layer));
        }
    }

    private static void AddParallelogram(
        List<MetaSceneSpriteDefinition> sprites,
        TextureAssetId pixel,
        float x,
        float y,
        float width,
        float height,
        float slant,
        Color tint,
        int z)
    {
        _ = pixel;
        TextureAssetId mask = PlayerHudSnapshotMaskIds.Resolve(width, height, slant);
        sprites.Add(new MetaSceneSpriteDefinition(
            mask,
            new Vector2(x + width / 2f, y + height / 2f),
            new Vector2(width, height),
            tint,
            z)
        {
            Flags = SpriteFlags.Visible | SpriteFlags.PixelAlignedDestination,
        });
    }

    private static MetaStaticSceneDefinition GenericDefinition(
        NewWorldSnapshotFixture fixture,
        int fixtureIndex,
        int variantIndex,
        TextureAssetId primary,
        TextureAssetId secondary) => new(
        fixture.Scene,
        [
            new MetaSceneSpriteDefinition(
                primary,
                new Vector2(960f, 540f),
                new Vector2(1920f, 1080f),
                VariantBackdropTint(variantIndex),
                -100),
            new MetaSceneSpriteDefinition(
                secondary,
                new Vector2(960f, 540f),
                new Vector2(1440f, 760f),
                Color.White,
                0),
        ],
        [
            new MetaSceneTextDefinition(
                TextContentIds.SnapshotFixture(fixtureIndex),
                TextStyleIds.Snapshot,
                new Vector2(960f, 80f),
                Vector2.One,
                Color.White,
                1000,
                RenderLayer.Overlay,
                TextAlignment.TopCenter),
        ]);

    private int FindFixtureIndex(string id)
    {
        ReadOnlySpan<NewWorldSnapshotFixture> fixtures = registry.Registered;
        for (var index = 0; index < fixtures.Length; index++)
        {
            if (string.Equals(fixtures[index].Id, id, StringComparison.Ordinal)) return index;
        }
        throw new InvalidOperationException($"Resolved fixture '{id}' is absent from its registry.");
    }

    private static void AddRect(
        List<MetaSceneSpriteDefinition> sprites,
        TextureAssetId texture,
        float x,
        float y,
        float width,
        float height,
        Color tint,
        int z) => sprites.Add(new MetaSceneSpriteDefinition(
            texture, new Vector2(x, y), new Vector2(width, height), tint, z));

    private static void AddRound(
        List<MetaSceneSpriteDefinition> sprites,
        TextureAssetId texture,
        float x,
        float y,
        float width,
        float height,
        Color tint,
        int z) => AddRect(sprites, texture, x, y, width, height, tint, z);

    private static void AddText(
        List<MetaSceneTextDefinition> texts,
        StringId content,
        TextStyleId style,
        float x,
        float y,
        float scale,
        Color tint,
        int z,
        TextAlignment alignment,
        float letterSpacing = 0f) => texts.Add(new MetaSceneTextDefinition(
            content, style, new Vector2(x, y), new Vector2(scale), tint, z,
            RenderLayer.World, alignment)
        {
            LetterSpacing = letterSpacing,
        });

    private static Color VariantBackdropTint(int variantIndex)
    {
        int phase = variantIndex % 4;
        return phase switch
        {
            1 => new Color(236, 242, 255),
            2 => new Color(255, 239, 226),
            3 => new Color(232, 250, 235),
            _ => Color.White,
        };
    }
}
