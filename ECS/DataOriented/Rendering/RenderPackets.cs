#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Rendering;

public enum RenderLayer : byte
{
    Background,
    World,
    Actor,
    Card,
    Hud,
    Overlay,
    Tooltip,
    Debug,
}

public enum RenderPacketKind : byte
{
    Sprite,
    Card,
    HandCard,
    Player,
    Enemy,
    Hud,
    Tooltip,
    Modal,
    Overlay,
    Highlight,
    VisualEffect,
    Shader,
    Diagnostic,
}

[Flags]
public enum RenderPacketFlags : byte
{
    None = 0,
    HasSourceRectangle = 1 << 0,
    Clip = 1 << 1,
    Additive = 1 << 2,
    PixelAlignedDestination = 1 << 3,
}

/// <summary>Fully resolved, engine-resource-free draw submission data.</summary>
public readonly record struct RenderPacket(
    EntityId Entity,
    TextureAssetId Texture,
    Rectangle SourceRectangle,
    Vector2 Position,
    Vector2 Origin,
    Vector2 Scale,
    Color Tint,
    float Rotation,
    float Effect0,
    float Effect1,
    int ZOrder,
    int StableOrder,
    RenderLayer Layer,
    RenderPacketKind Kind,
    RenderPacketFlags Flags);

/// <summary>
/// Reusable packet storage. Extraction overwrites animation values in place and only
/// re-sorts a layer when membership or an ordering key changes.
/// </summary>
public sealed class RenderPacketStore
{
    private const int LayerCount = 8;
    private readonly RenderLayerBuffer[] layers;

    public RenderPacketStore(int initialCapacityPerLayer = 32)
    {
        if (initialCapacityPerLayer < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacityPerLayer));

        layers = new RenderLayerBuffer[LayerCount];
        for (var index = 0; index < layers.Length; index++)
            layers[index] = new RenderLayerBuffer(initialCapacityPerLayer);
    }

    public long ExtractionVersion { get; private set; }
    public int Count { get; private set; }
    public int SortCount { get; private set; }

    public void BeginExtraction()
    {
        Count = 0;
        for (var index = 0; index < layers.Length; index++) layers[index].Begin();
    }

    public void Add(in RenderPacket packet)
    {
        ValidateLayer(packet.Layer);
        layers[(int)packet.Layer].Add(in packet);
        Count++;
    }

    public void EndExtraction()
    {
        for (var index = 0; index < layers.Length; index++)
        {
            if (layers[index].FinishAndSort()) SortCount++;
        }

        ExtractionVersion++;
    }

    public ReadOnlySpan<RenderPacket> GetLayer(RenderLayer layer)
    {
        ValidateLayer(layer);
        return layers[(int)layer].Packets;
    }

    private static void ValidateLayer(RenderLayer layer)
    {
        if ((uint)layer >= LayerCount)
            throw new ArgumentOutOfRangeException(nameof(layer));
    }

    private sealed class RenderLayerBuffer
    {
        private RenderPacket[] packets;
        private int count;
        private int previousCount;
        private bool orderDirty;

        public RenderLayerBuffer(int capacity) => packets = new RenderPacket[capacity];

        public ReadOnlySpan<RenderPacket> Packets => packets.AsSpan(0, count);

        public void Begin()
        {
            previousCount = count;
            count = 0;
            orderDirty = false;
        }

        public void Add(in RenderPacket packet)
        {
            EnsureCapacity(count + 1);
            if (count >= previousCount)
            {
                orderDirty = true;
            }
            else
            {
                ref readonly RenderPacket old = ref packets[count];
                if (old.Entity != packet.Entity || old.Kind != packet.Kind ||
                    old.ZOrder != packet.ZOrder || old.StableOrder != packet.StableOrder)
                    orderDirty = true;
            }

            packets[count++] = packet;
        }

        public bool FinishAndSort()
        {
            orderDirty |= count != previousCount;
            if (!orderDirty) return false;

            // Stable insertion sort is allocation-free and presentation layers are small.
            for (var index = 1; index < count; index++)
            {
                RenderPacket value = packets[index];
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

        private void EnsureCapacity(int required)
        {
            if (required <= packets.Length) return;
            Array.Resize(ref packets, Math.Max(required, Math.Max(4, packets.Length * 2)));
            orderDirty = true;
        }

        private static int Compare(in RenderPacket left, in RenderPacket right)
        {
            int z = left.ZOrder.CompareTo(right.ZOrder);
            return z != 0 ? z : left.StableOrder.CompareTo(right.StableOrder);
        }
    }
}

/// <summary>External MonoGame adapter boundary. Implementations issue draw calls only.</summary>
public interface IRenderPacketSink
{
    void Draw(in RenderPacket packet);
}

public sealed class RenderPacketDrawConsumer
{
    public void Draw(RenderPacketStore packets, IRenderPacketSink sink)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(sink);
        for (var layer = RenderLayer.Background; layer <= RenderLayer.Debug; layer++)
        {
            ReadOnlySpan<RenderPacket> values = packets.GetLayer(layer);
            for (var index = 0; index < values.Length; index++) sink.Draw(in values[index]);
        }
    }
}
