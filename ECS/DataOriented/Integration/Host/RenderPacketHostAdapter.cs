#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Integration.Host;

public enum HostRenderPass : byte
{
    Alpha,
    Additive,
}

/// <summary>External compact-ID lookup. GPU resources never enter ECS storage.</summary>
public interface IHostTextureResolver<TTexture> where TTexture : class
{
    bool TryResolve(TextureAssetId id, out TTexture? texture);
}

/// <summary>MonoGame-facing draw boundary. Implementations own SpriteBatch begin/end calls.</summary>
public interface IHostRenderDevice<TTexture> where TTexture : class
{
    void Begin(HostRenderPass pass);
    void Draw(in RenderPacket packet, TTexture? texture);
    void End();
}

/// <summary>
/// Reads extracted packets without world access and switches alpha/additive passes only when the
/// packet stream requires it.
/// </summary>
public sealed class RenderPacketHostAdapter<TTexture> where TTexture : class
{
    public int Draw(
        RenderPacketStore packets,
        IHostTextureResolver<TTexture> textures,
        IHostRenderDevice<TTexture> device)
    {
        ArgumentNullException.ThrowIfNull(packets);
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(device);

        HostRenderPass? activePass = null;
        var count = 0;
        try
        {
            for (var layer = RenderLayer.Background; layer <= RenderLayer.Debug; layer++)
            {
                ReadOnlySpan<RenderPacket> values = packets.GetLayer(layer);
                for (var index = 0; index < values.Length; index++)
                {
                    ref readonly RenderPacket packet = ref values[index];
                    HostRenderPass required = (packet.Flags & RenderPacketFlags.Additive) != 0
                        ? HostRenderPass.Additive
                        : HostRenderPass.Alpha;
                    if (activePass != required)
                    {
                        if (activePass.HasValue) device.End();
                        device.Begin(required);
                        activePass = required;
                    }

                    TTexture? texture = ResolveTexture(in packet, textures);
                    device.Draw(in packet, texture);
                    count++;
                }
            }
        }
        finally
        {
            if (activePass.HasValue) device.End();
        }
        return count;
    }

    private static TTexture? ResolveTexture(
        in RenderPacket packet,
        IHostTextureResolver<TTexture> textures)
    {
        if (packet.Texture.IsNull) return null;
        if (textures.TryResolve(packet.Texture, out TTexture? texture) && texture is not null)
            return texture;
        throw new KeyNotFoundException($"No host texture is registered for TextureAssetId {packet.Texture.Value}.");
    }
}
