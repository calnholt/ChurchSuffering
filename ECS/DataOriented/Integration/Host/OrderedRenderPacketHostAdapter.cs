#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Rendering;

namespace Crusaders30XX.ECS.DataOriented.Integration.Host;

/// <summary>
/// Host text boundary with explicit batch ownership. Implementations begin and end their own
/// SpriteBatch-compatible alpha pass; contiguous text packets are submitted within one batch.
/// </summary>
public interface IHostTextRenderDevice : ITextRenderPacketSink
{
    void BeginText();
    void EndText();
}

/// <summary>
/// Read-only host boundary that merges sprite and text extraction streams by layer, Z order,
/// and stable order. Sprite and text devices own explicit, mutually exclusive batch lifecycles.
/// </summary>
public sealed class OrderedRenderPacketHostAdapter<TTexture> where TTexture : class
{
    public int Draw(
        RenderPacketStore sprites,
        TextRenderPacketStore text,
        IHostTextureResolver<TTexture> textures,
        ITextPresentationCatalog textCatalog,
        IHostRenderDevice<TTexture> spriteDevice,
        IHostTextRenderDevice textDevice)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(textCatalog);
        ArgumentNullException.ThrowIfNull(spriteDevice);
        ArgumentNullException.ThrowIfNull(textDevice);

        HostRenderPass? activePass = null;
        var textActive = false;
        var drawn = 0;
        try
        {
            for (var layer = RenderLayer.Background; layer <= RenderLayer.Debug; layer++)
            {
                ReadOnlySpan<RenderPacket> spriteLayer = sprites.GetLayer(layer);
                ReadOnlySpan<TextRenderPacket> textLayer = text.GetLayer(layer);
                var spriteIndex = 0;
                var textIndex = 0;
                while (spriteIndex < spriteLayer.Length || textIndex < textLayer.Length)
                {
                    bool drawSprite = ShouldDrawSprite(spriteLayer, spriteIndex, textLayer, textIndex);
                    if (drawSprite)
                    {
                        ref readonly RenderPacket packet = ref spriteLayer[spriteIndex++];
                        EndTextPass(textDevice, ref textActive);
                        HostRenderPass required = RequiredPass(in packet);
                        if (activePass != required)
                        {
                            EndSpritePass(spriteDevice, ref activePass);
                            spriteDevice.Begin(required);
                            activePass = required;
                        }

                        TTexture? texture = ResolveTexture(in packet, textures);
                        spriteDevice.Draw(in packet, texture);
                    }
                    else
                    {
                        ref readonly TextRenderPacket packet = ref textLayer[textIndex++];
                        ResolveText(in packet, textCatalog, out string resolvedText, out TextStyleDefinition style);
                        EndSpritePass(spriteDevice, ref activePass);
                        if (!textActive)
                        {
                            textDevice.BeginText();
                            textActive = true;
                        }
                        textDevice.Draw(in packet, resolvedText, in style);
                    }

                    drawn++;
                }
            }
        }
        finally
        {
            EndSpritePass(spriteDevice, ref activePass);
            EndTextPass(textDevice, ref textActive);
        }

        return drawn;
    }

    private static bool ShouldDrawSprite(
        ReadOnlySpan<RenderPacket> sprites,
        int spriteIndex,
        ReadOnlySpan<TextRenderPacket> text,
        int textIndex)
    {
        if (spriteIndex >= sprites.Length) return false;
        if (textIndex >= text.Length) return true;

        ref readonly RenderPacket sprite = ref sprites[spriteIndex];
        ref readonly TextRenderPacket label = ref text[textIndex];
        int z = sprite.ZOrder.CompareTo(label.ZOrder);
        if (z != 0) return z < 0;
        int stable = sprite.StableOrder.CompareTo(label.StableOrder);
        if (stable != 0) return stable < 0;

        // A label authored on the same entity should overlay its sprite.
        return true;
    }

    private static HostRenderPass RequiredPass(in RenderPacket packet) =>
        (packet.Flags & RenderPacketFlags.Additive) != 0
            ? HostRenderPass.Additive
            : HostRenderPass.Alpha;

    private static TTexture? ResolveTexture(
        in RenderPacket packet,
        IHostTextureResolver<TTexture> textures)
    {
        if (packet.Texture.IsNull) return null;
        if (textures.TryResolve(packet.Texture, out TTexture? texture) && texture is not null)
            return texture;
        throw new KeyNotFoundException($"No host texture is registered for TextureAssetId {packet.Texture.Value}.");
    }

    private static void ResolveText(
        in TextRenderPacket packet,
        ITextPresentationCatalog catalog,
        out string text,
        out TextStyleDefinition style)
    {
        if (!catalog.TryResolve(packet.Content, out string? resolved) || resolved is null)
            throw new KeyNotFoundException($"No text is registered for StringId {packet.Content.Value}.");
        if (!catalog.TryResolve(packet.Style, out style))
            throw new KeyNotFoundException($"No text style is registered for TextStyleId {packet.Style.Value}.");
        text = resolved;
    }

    private static void EndSpritePass(
        IHostRenderDevice<TTexture> spriteDevice,
        ref HostRenderPass? activePass)
    {
        if (!activePass.HasValue) return;
        activePass = null;
        spriteDevice.End();
    }

    private static void EndTextPass(IHostTextRenderDevice textDevice, ref bool active)
    {
        if (!active) return;
        active = false;
        textDevice.EndText();
    }
}
