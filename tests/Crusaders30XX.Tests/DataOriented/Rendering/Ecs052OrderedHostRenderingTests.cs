#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Integration.Host;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rendering;

public sealed class Ecs052OrderedHostRenderingTests
{
    [Fact]
    public void Draw_merges_sprite_and_text_by_layer_z_and_stable_order()
    {
        RenderPacketStore sprites = SpriteStore(
            Sprite(1, RenderLayer.Background, 10, 1),
            Sprite(2, RenderLayer.World, -2, 2),
            Sprite(3, RenderLayer.World, 20, 3));
        TextRenderPacketStore text = TextStore(
            Label(11, RenderLayer.Background, 5, 11),
            Label(12, RenderLayer.World, 0, 12),
            Label(13, RenderLayer.Overlay, -100, 13));
        var trace = new Trace();

        int drawn = Draw(sprites, text, trace);

        Assert.Equal(6, drawn);
        Assert.Equal(new[]
        {
            "text-begin", "text:11:content-11", "text-end",
            "begin:Alpha", "sprite:1:texture-1", "sprite:2:texture-2", "end",
            "text-begin", "text:12:content-12", "text-end",
            "begin:Alpha", "sprite:3:texture-3", "end",
            "text-begin", "text:13:content-13", "text-end",
        }, trace.Calls);
    }

    [Fact]
    public void Equal_z_uses_stable_order_and_exact_ties_draw_sprite_before_text()
    {
        RenderPacketStore sprites = SpriteStore(
            Sprite(1, RenderLayer.Hud, 10, 20),
            Sprite(2, RenderLayer.Hud, 10, 30));
        TextRenderPacketStore text = TextStore(
            Label(11, RenderLayer.Hud, 10, 10),
            Label(12, RenderLayer.Hud, 10, 20),
            Label(13, RenderLayer.Hud, 10, 40));
        var trace = new Trace();

        Draw(sprites, text, trace);

        Assert.Equal(new[]
        {
            "text-begin", "text:11:content-11", "text-end",
            "begin:Alpha", "sprite:1:texture-1", "end",
            "text-begin", "text:12:content-12", "text-end",
            "begin:Alpha", "sprite:2:texture-2", "end",
            "text-begin", "text:13:content-13", "text-end",
        }, trace.Calls);
    }

    [Fact]
    public void Interleaved_text_preserves_alpha_and_additive_sprite_pass_transitions()
    {
        RenderPacketStore sprites = SpriteStore(
            Sprite(1, RenderLayer.World, 0, 1),
            Sprite(2, RenderLayer.World, 1, 2, RenderPacketFlags.Additive),
            Sprite(3, RenderLayer.World, 3, 3, RenderPacketFlags.Additive),
            Sprite(4, RenderLayer.World, 4, 4));
        TextRenderPacketStore text = TextStore(Label(11, RenderLayer.World, 2, 11));
        var trace = new Trace();

        Draw(sprites, text, trace);

        Assert.Equal(new[]
        {
            "begin:Alpha", "sprite:1:texture-1", "end",
            "begin:Additive", "sprite:2:texture-2", "end",
            "text-begin", "text:11:content-11", "text-end",
            "begin:Additive", "sprite:3:texture-3", "end",
            "begin:Alpha", "sprite:4:texture-4", "end",
        }, trace.Calls);
    }

    [Fact]
    public void Contiguous_text_packets_share_one_explicit_text_batch()
    {
        TextRenderPacketStore text = TextStore(
            Label(11, RenderLayer.Hud, 1, 11),
            Label(12, RenderLayer.Hud, 2, 12),
            Label(13, RenderLayer.Overlay, 0, 13));
        var trace = new Trace();

        Draw(SpriteStore(), text, trace);

        Assert.Equal(new[]
        {
            "text-begin",
            "text:11:content-11",
            "text:12:content-12",
            "text:13:content-13",
            "text-end",
        }, trace.Calls);
    }

    [Fact]
    public void Missing_sprite_or_text_resources_fail_and_close_active_sprite_pass()
    {
        RenderPacketStore missingTexture = SpriteStore(Sprite(99, RenderLayer.World, 0, 1));
        var textureTrace = new Trace(missingTextureId: 99);
        KeyNotFoundException textureError = Assert.Throws<KeyNotFoundException>(() =>
            Draw(missingTexture, TextStore(), textureTrace));
        Assert.Contains("TextureAssetId 99", textureError.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { "begin:Alpha", "end" }, textureTrace.Calls);

        RenderPacketStore precedingSprite = SpriteStore(Sprite(1, RenderLayer.World, 0, 1));
        TextRenderPacketStore missingText = TextStore(Label(99, RenderLayer.World, 1, 99));
        var textTrace = new Trace(missingContentId: 99);
        KeyNotFoundException textError = Assert.Throws<KeyNotFoundException>(() =>
            Draw(precedingSprite, missingText, textTrace));
        Assert.Contains("StringId 99", textError.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { "begin:Alpha", "sprite:1:texture-1", "end" }, textTrace.Calls);

        TextRenderPacketStore missingStyle = TextStore(Label(98, RenderLayer.World, 0, 98));
        var styleTrace = new Trace(missingStyleId: 98);
        KeyNotFoundException styleError = Assert.Throws<KeyNotFoundException>(() =>
            Draw(SpriteStore(), missingStyle, styleTrace));
        Assert.Contains("TextStyleId 98", styleError.Message, StringComparison.Ordinal);
        Assert.Empty(styleTrace.Calls);
    }

    [Fact]
    public void Draw_does_not_mutate_either_packet_store()
    {
        RenderPacketStore sprites = SpriteStore(
            Sprite(1, RenderLayer.Card, 4, 1),
            Sprite(2, RenderLayer.Card, 5, 2, RenderPacketFlags.Additive));
        TextRenderPacketStore text = TextStore(Label(11, RenderLayer.Card, 4, 11));
        long spriteVersion = sprites.ExtractionVersion;
        long textVersion = text.ExtractionVersion;
        int spriteCount = sprites.Count;
        int textCount = text.Count;
        RenderPacket[] spriteBefore = AllSprites(sprites);
        TextRenderPacket[] textBefore = AllText(text);

        Draw(sprites, text, new Trace());

        Assert.Equal(spriteVersion, sprites.ExtractionVersion);
        Assert.Equal(textVersion, text.ExtractionVersion);
        Assert.Equal(spriteCount, sprites.Count);
        Assert.Equal(textCount, text.Count);
        Assert.Equal(spriteBefore, AllSprites(sprites));
        Assert.Equal(textBefore, AllText(text));
    }

    private static int Draw(RenderPacketStore sprites, TextRenderPacketStore text, Trace trace) =>
        new OrderedRenderPacketHostAdapter<string>().Draw(sprites, text, trace, trace, trace, trace);

    private static RenderPacketStore SpriteStore(params RenderPacket[] values)
    {
        var store = new RenderPacketStore(values.Length);
        store.BeginExtraction();
        foreach (RenderPacket value in values) store.Add(in value);
        store.EndExtraction();
        return store;
    }

    private static TextRenderPacketStore TextStore(params TextRenderPacket[] values)
    {
        var store = new TextRenderPacketStore(values.Length);
        store.BeginExtraction();
        foreach (TextRenderPacket value in values) store.Add(in value);
        store.EndExtraction();
        return store;
    }

    private static RenderPacket Sprite(
        int id,
        RenderLayer layer,
        int z,
        int stable,
        RenderPacketFlags flags = RenderPacketFlags.None) => new(
            new EntityId(id, 1), new TextureAssetId(id), default, Vector2.Zero, Vector2.Zero,
            Vector2.One, Color.White, 0f, 0f, 0f, z, stable, layer, RenderPacketKind.Sprite, flags);

    private static TextRenderPacket Label(int id, RenderLayer layer, int z, int stable) => new(
        new EntityId(id, 1), new StringId(id), new TextStyleId(id), Vector2.Zero, Vector2.One,
        Color.White, 0f, z, stable, layer, TextAlignment.TopLeft, 0f,
        TextPresentationFlags.Visible);

    private static RenderPacket[] AllSprites(RenderPacketStore packets) =>
        Enumerable.Range(0, 8).SelectMany(layer => packets.GetLayer((RenderLayer)layer).ToArray()).ToArray();

    private static TextRenderPacket[] AllText(TextRenderPacketStore packets) =>
        Enumerable.Range(0, 8).SelectMany(layer => packets.GetLayer((RenderLayer)layer).ToArray()).ToArray();

    private sealed class Trace :
        IHostTextureResolver<string>,
        IHostRenderDevice<string>,
        ITextPresentationCatalog,
        IHostTextRenderDevice
    {
        private readonly int missingTextureId;
        private readonly int missingContentId;
        private readonly int missingStyleId;

        public Trace(int missingTextureId = 0, int missingContentId = 0, int missingStyleId = 0)
        {
            this.missingTextureId = missingTextureId;
            this.missingContentId = missingContentId;
            this.missingStyleId = missingStyleId;
        }

        public List<string> Calls { get; } = [];

        public bool TryResolve(TextureAssetId id, out string? texture)
        {
            texture = id.Value == missingTextureId ? null : $"texture-{id.Value}";
            return texture is not null;
        }

        public bool TryResolve(StringId id, out string? text)
        {
            text = id.Value == missingContentId ? null : $"content-{id.Value}";
            return text is not null;
        }

        public bool TryResolve(TextStyleId id, out TextStyleDefinition style)
        {
            style = new TextStyleDefinition(new FontAssetId(id.Value), 1f, 0f);
            return id.Value != missingStyleId;
        }

        public void Begin(HostRenderPass pass) => Calls.Add($"begin:{pass}");

        public void Draw(in RenderPacket packet, string? texture) =>
            Calls.Add($"sprite:{packet.Entity.Index}:{texture ?? "null"}");

        public void Draw(in TextRenderPacket packet, string text, in TextStyleDefinition style) =>
            Calls.Add($"text:{packet.Entity.Index}:{text}");

        public void End() => Calls.Add("end");
        public void BeginText() => Calls.Add("text-begin");
        public void EndText() => Calls.Add("text-end");
    }
}
