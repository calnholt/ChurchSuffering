#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;

public sealed class TextRenderExtractionSystem : IGameSystem
{
    private readonly World world;
    private readonly Query<Transform, TextPresentation> query;
    private readonly TextRenderPacketStore packets;

    public TextRenderExtractionSystem(World world, TextRenderPacketStore packets)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.packets = packets ?? throw new ArgumentNullException(nameof(packets));
        query = world.Query<Transform, TextPresentation>(new QueryFilter(DebugName: "ECS052.TextExtraction"));
        ComponentSignature reads = ComponentSignature.Empty
            .With(ComponentType<Transform>.Id)
            .With(ComponentType<TextPresentation>.Id);
        Descriptor = new SystemDescriptor(
            PresentationSystemIds.TextRenderExtraction,
            nameof(TextRenderExtractionSystem),
            SystemPhase.RenderExtraction,
            SceneGroup.Global,
            readComponents: reads,
            runsAfter: [PresentationSystemIds.RenderExtraction]);
    }

    public SystemDescriptor Descriptor { get; }
    public TextRenderPacketStore Packets => packets;

    public void Update(ref SystemContext context) => Extract();

    public void Extract()
    {
        packets.BeginExtraction();
        ReadOnlyWorld readOnly = world.AsReadOnly();
        foreach (QueryChunk<Transform, TextPresentation> chunk in query)
        {
            ReadOnlySpan<Transform> transforms = chunk.Component1;
            ReadOnlySpan<TextPresentation> texts = chunk.Component2;
            foreach (int row in chunk.Rows)
            {
                ref readonly TextPresentation text = ref texts[row];
                if (!text.IsVisible || text.Content.IsNull || text.Style.IsNull) continue;
                EntityId entity = chunk.Entities[row];
                Transform transform = TransformResolver.Resolve(readOnly, entity, in transforms[row]);
                packets.Add(new TextRenderPacket(
                    entity,
                    text.Content,
                    text.Style,
                    transform.Position + text.Offset,
                    text.Scale,
                    text.Tint,
                    transform.Rotation,
                    transform.ZOrder + text.ZOffset,
                    entity.Index,
                    text.Layer,
                    text.Alignment,
                    text.LetterSpacing,
                    text.Flags));
            }
        }
        packets.EndExtraction();
    }
}
