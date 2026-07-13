#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Rendering;

public readonly record struct TextRenderPacket(
    EntityId Entity,
    StringId Content,
    TextStyleId Style,
    Vector2 Position,
    Vector2 Scale,
    Color Tint,
    float Rotation,
    int ZOrder,
    int StableOrder,
    RenderLayer Layer,
    TextAlignment Alignment,
    float LetterSpacing,
    TextPresentationFlags Flags);

/// <summary>Reusable, allocation-free-after-warm-up storage sorted by layer, Z, and stable order.</summary>
public sealed class TextRenderPacketStore
{
    private const int LayerCount = 8;
    private readonly LayerBuffer[] layers;

    public TextRenderPacketStore(int initialCapacityPerLayer = 16)
    {
        if (initialCapacityPerLayer < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacityPerLayer));
        layers = new LayerBuffer[LayerCount];
        for (var index = 0; index < layers.Length; index++) layers[index] = new LayerBuffer(initialCapacityPerLayer);
    }

    public int Count { get; private set; }
    public long ExtractionVersion { get; private set; }
    public int SortCount { get; private set; }

    public void BeginExtraction()
    {
        Count = 0;
        for (var index = 0; index < layers.Length; index++) layers[index].Begin();
    }

    public void Add(in TextRenderPacket packet)
    {
        Validate(packet.Layer);
        layers[(int)packet.Layer].Add(in packet);
        Count++;
    }

    public void EndExtraction()
    {
        for (var index = 0; index < layers.Length; index++)
            if (layers[index].FinishAndSort()) SortCount++;
        ExtractionVersion++;
    }

    public ReadOnlySpan<TextRenderPacket> GetLayer(RenderLayer layer)
    {
        Validate(layer);
        return layers[(int)layer].Packets;
    }

    private static void Validate(RenderLayer layer)
    {
        if ((uint)layer >= LayerCount) throw new ArgumentOutOfRangeException(nameof(layer));
    }

    private sealed class LayerBuffer
    {
        private TextRenderPacket[] packets;
        private int count;
        private int previousCount;
        private bool orderDirty;

        public LayerBuffer(int capacity) => packets = new TextRenderPacket[capacity];
        public ReadOnlySpan<TextRenderPacket> Packets => packets.AsSpan(0, count);

        public void Begin()
        {
            previousCount = count;
            count = 0;
            orderDirty = false;
        }

        public void Add(in TextRenderPacket packet)
        {
            Ensure(count + 1);
            if (count >= previousCount)
            {
                orderDirty = true;
            }
            else
            {
                ref readonly TextRenderPacket old = ref packets[count];
                if (old.Entity != packet.Entity || old.ZOrder != packet.ZOrder || old.StableOrder != packet.StableOrder)
                    orderDirty = true;
            }
            packets[count++] = packet;
        }

        public bool FinishAndSort()
        {
            orderDirty |= count != previousCount;
            if (!orderDirty) return false;
            for (var index = 1; index < count; index++)
            {
                TextRenderPacket value = packets[index];
                var cursor = index - 1;
                while (cursor >= 0 && Compare(in value, in packets[cursor]) < 0)
                {
                    packets[cursor + 1] = packets[cursor];
                    cursor--;
                }
                packets[cursor + 1] = value;
            }
            return true;
        }

        private void Ensure(int required)
        {
            if (required <= packets.Length) return;
            Array.Resize(ref packets, Math.Max(required, Math.Max(4, packets.Length * 2)));
            orderDirty = true;
        }

        private static int Compare(in TextRenderPacket left, in TextRenderPacket right)
        {
            int z = left.ZOrder.CompareTo(right.ZOrder);
            return z != 0 ? z : left.StableOrder.CompareTo(right.StableOrder);
        }
    }
}

public readonly record struct TextStyleDefinition(FontAssetId Font, float Scale, float CharacterSpacing);

public interface ITextPresentationCatalog
{
    bool TryResolve(StringId id, out string? text);
    bool TryResolve(TextStyleId id, out TextStyleDefinition style);
}

public interface ITextRenderPacketSink
{
    void Draw(in TextRenderPacket packet, string text, in TextStyleDefinition style);
}

/// <summary>Read-only external draw consumer; catalog strings and host fonts never enter ECS storage.</summary>
public sealed class TextRenderPacketDrawConsumer
{
    public int Draw(TextRenderPacketStore packets, ITextPresentationCatalog catalog, ITextRenderPacketSink sink)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sink);
        var count = 0;
        for (var layer = RenderLayer.Background; layer <= RenderLayer.Debug; layer++)
        {
            ReadOnlySpan<TextRenderPacket> values = packets.GetLayer(layer);
            for (var index = 0; index < values.Length; index++)
            {
                ref readonly TextRenderPacket packet = ref values[index];
                if (!catalog.TryResolve(packet.Content, out string? text) || text is null)
                    throw new KeyNotFoundException($"No text is registered for StringId {packet.Content.Value}.");
                if (!catalog.TryResolve(packet.Style, out TextStyleDefinition style))
                    throw new KeyNotFoundException($"No text style is registered for TextStyleId {packet.Style.Value}.");
                sink.Draw(in packet, text, in style);
                count++;
            }
        }
        return count;
    }
}
