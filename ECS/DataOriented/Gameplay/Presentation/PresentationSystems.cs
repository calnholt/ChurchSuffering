#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;

public static class PresentationSystemIds
{
    public static readonly SystemId PositionTween = new(4501);
    public static readonly SystemId Parallax = new(4502);
    public static readonly SystemId JigglePulse = new(4503);
    public static readonly SystemId VisualEffect = new(4504);
    public static readonly SystemId RenderExtraction = new(4505);
    public static readonly SystemId Diagnostics = new(4506);
    public static readonly SystemId TextRenderExtraction = new(4507);
}

public readonly record struct PresentationFrameInput(Vector2 CursorPosition, Vector2 CanvasSize)
{
    public static PresentationFrameInput Centered => new(new Vector2(960f, 540f), new Vector2(1920f, 1080f));
}

public sealed class PositionTweenPresentationSystem : IGameSystem
{
    private readonly Query<Transform, PositionTween> query;

    public PositionTweenPresentationSystem(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        query = world.Query<Transform, PositionTween>(new QueryFilter(DebugName: "ECS045.PositionTween"));
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<Transform>.Id).With(ComponentType<PositionTween>.Id);
        Descriptor = new SystemDescriptor(PresentationSystemIds.PositionTween, nameof(PositionTweenPresentationSystem),
            SystemPhase.Presentation, SceneGroup.Global, writeComponents: writes);
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context) => Update((float)context.Elapsed.TotalSeconds);

    public void Update(float elapsedSeconds)
    {
        float dt = Math.Max(0f, elapsedSeconds);
        foreach (QueryChunk<Transform, PositionTween> chunk in query)
        {
            Span<Transform> transforms = chunk.Component1;
            Span<PositionTween> tweens = chunk.Component2;
            foreach (int row in chunk.Rows)
            {
                ref Transform transform = ref transforms[row];
                ref PositionTween tween = ref tweens[row];
                if (!tween.Initialized)
                {
                    tween.Current = transform.Position;
                    tween.Initialized = true;
                }

                Vector2 delta = tween.Target - tween.Current;
                float distance = delta.Length();
                float step = Math.Max(0f, tween.Speed) * dt;
                tween.Current = distance <= step || distance <= 0.0001f
                    ? tween.Target
                    : tween.Current + delta * (step / distance);
                transform.Position = tween.Current;
            }
        }
    }
}

/// <summary>Owns parallax anchor detection; callers only write Transform.Position.</summary>
public sealed class ParallaxPresentationSystem : IGameSystem
{
    private readonly Query<Transform, ParallaxLayer, ParallaxPresentationState> query;

    public ParallaxPresentationSystem(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        query = world.Query<Transform, ParallaxLayer, ParallaxPresentationState>(
            new QueryFilter(DebugName: "ECS045.Parallax"));
        ComponentSignature reads = default;
        reads = reads.With(ComponentType<ParallaxLayer>.Id);
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<Transform>.Id).With(ComponentType<ParallaxPresentationState>.Id);
        Descriptor = new SystemDescriptor(PresentationSystemIds.Parallax, nameof(ParallaxPresentationSystem),
            SystemPhase.LatePresentation, SceneGroup.Global, readComponents: reads, writeComponents: writes);
    }

    public SystemDescriptor Descriptor { get; }
    public PresentationFrameInput Input { get; set; } = PresentationFrameInput.Centered;

    public void Update(ref SystemContext context)
    {
        PresentationFrameInput input = Input;
        Update((float)context.Elapsed.TotalSeconds, in input);
    }

    public void Update(float elapsedSeconds, in PresentationFrameInput input)
    {
        float dt = Math.Max(0f, elapsedSeconds);
        Vector2 canvas = new(Math.Max(1f, input.CanvasSize.X), Math.Max(1f, input.CanvasSize.Y));
        Vector2 normalized = (input.CursorPosition - canvas * 0.5f) / (canvas * 0.5f);
        normalized.X = Math.Clamp(normalized.X, -1f, 1f);
        normalized.Y = Math.Clamp(normalized.Y, -1f, 1f);

        foreach (QueryChunk<Transform, ParallaxLayer, ParallaxPresentationState> chunk in query)
        {
            Span<Transform> transforms = chunk.Component1;
            Span<ParallaxLayer> settings = chunk.Component2;
            Span<ParallaxPresentationState> states = chunk.Component3;
            foreach (int row in chunk.Rows)
            {
                ref Transform transform = ref transforms[row];
                ref readonly ParallaxLayer layer = ref settings[row];
                ref ParallaxPresentationState state = ref states[row];
                if (state.Initialized == 0)
                {
                    state.Anchor = transform.Position;
                    state.LastWrittenPosition = transform.Position;
                    state.Initialized = 1;
                }
                else if (Vector2.DistanceSquared(transform.Position, state.LastWrittenPosition) > 0.0001f)
                {
                    // An external layout write establishes a new base position.
                    state.Anchor = transform.Position;
                }

                Vector2 desired = new(
                    normalized.X * layer.MultiplierX * layer.MaxOffset,
                    normalized.Y * layer.MultiplierY * layer.MaxOffset);
                float blend = layer.SmoothTime <= 0f ? 1f : 1f - MathF.Exp(-dt / layer.SmoothTime);
                state.Offset = Vector2.Lerp(state.Offset, desired, Math.Clamp(blend, 0f, 1f));
                transform.Position = state.Anchor + state.Offset;
                state.LastWrittenPosition = transform.Position;
            }
        }
    }
}

public sealed class JigglePulsePresentationSystem : IGameSystem, IEventConsumer<JigglePulseEvent>
{
    private readonly World world;
    private readonly Query<Transform, JigglePulseState> query;

    public JigglePulsePresentationSystem(World world)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        query = world.Query<Transform, JigglePulseState>(new QueryFilter(DebugName: "ECS045.JigglePulse"));
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<Transform>.Id).With(ComponentType<JigglePulseState>.Id);
        Descriptor = new SystemDescriptor(PresentationSystemIds.JigglePulse, nameof(JigglePulsePresentationSystem),
            SystemPhase.Presentation, SceneGroup.Global, writeComponents: writes,
            runsAfter: [PresentationSystemIds.PositionTween]);
    }

    public SystemDescriptor Descriptor { get; }

    public void Consume(in JigglePulseEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Entity, out JigglePulseState state)) return;
        state.ElapsedSeconds = 0f;
        state.DurationSeconds = Math.Max(0f, value.Config.DurationSeconds);
        state.Magnitude = value.Config.Magnitude;
        state.Sequence = value.Sequence;
        world.Set(value.Entity, state);
    }

    public void Update(ref SystemContext context)
    {
        float dt = Math.Max(0f, (float)context.Elapsed.TotalSeconds);
        foreach (QueryChunk<Transform, JigglePulseState> chunk in query)
        {
            Span<Transform> transforms = chunk.Component1;
            Span<JigglePulseState> pulses = chunk.Component2;
            foreach (int row in chunk.Rows)
            {
                ref JigglePulseState pulse = ref pulses[row];
                if (pulse.ElapsedSeconds >= pulse.DurationSeconds || pulse.DurationSeconds <= 0f) continue;
                pulse.ElapsedSeconds = Math.Min(pulse.DurationSeconds, pulse.ElapsedSeconds + dt);
                float remaining = 1f - pulse.ElapsedSeconds / pulse.DurationSeconds;
                transforms[row].Rotation = MathF.Sin(pulse.ElapsedSeconds * 47f + pulse.Sequence) * pulse.Magnitude * remaining;
            }
        }
    }
}

public sealed class VisualEffectPresentationSystem : IGameSystem
{
    private readonly Query<ActiveVisualEffect> effects;
    private readonly EventStream<VisualEffectImpactReached>? impacts;
    private readonly EventStream<VisualEffectCompleted>? completed;

    public VisualEffectPresentationSystem(
        World world,
        EventStream<VisualEffectImpactReached>? impacts = null,
        EventStream<VisualEffectCompleted>? completed = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        effects = world.Query<ActiveVisualEffect>(new QueryFilter(DebugName: "ECS045.VisualEffects"));
        this.impacts = impacts;
        this.completed = completed;
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<ActiveVisualEffect>.Id);
        Descriptor = new SystemDescriptor(PresentationSystemIds.VisualEffect, nameof(VisualEffectPresentationSystem),
            SystemPhase.Presentation, SceneGroup.Global, writeComponents: writes, eventBarrier: EventBarrier.AfterPhase);
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context) => Update((float)context.Elapsed.TotalSeconds);

    public void Update(float elapsedSeconds)
    {
        float dt = Math.Max(0f, elapsedSeconds);
        foreach (QueryChunk<ActiveVisualEffect> chunk in effects)
        {
            Span<ActiveVisualEffect> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                ref ActiveVisualEffect effect = ref values[row];
                if ((effect.Flags & PresentationFlags.Active) == 0) continue;
                float previous = effect.ElapsedSeconds;
                effect.ElapsedSeconds = Math.Min(effect.DurationSeconds, previous + dt);
                if (previous < effect.DurationSeconds * 0.5f && effect.ElapsedSeconds >= effect.DurationSeconds * 0.5f)
                    impacts?.Publish(new VisualEffectImpactReached(chunk.Entities[row], effect.Recipe, effect.Sequence));
                if (effect.ElapsedSeconds < effect.DurationSeconds) continue;
                effect.Flags = effect.Flags & ~PresentationFlags.Active | PresentationFlags.Completed;
                completed?.Publish(new VisualEffectCompleted(chunk.Entities[row], effect.Recipe, effect.Sequence));
            }
        }
    }
}

public sealed class SpriteRenderExtractionSystem : IGameSystem
{
    private readonly World world;
    private readonly Query<Transform, Sprite> sprites;
    private readonly Query<Transform, ActiveVisualEffect> effects;
    private readonly Query<ShaderRequest> shaders;
    private readonly RenderPacketStore packets;

    public SpriteRenderExtractionSystem(World world, RenderPacketStore packets)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.packets = packets ?? throw new ArgumentNullException(nameof(packets));
        sprites = world.Query<Transform, Sprite>(new QueryFilter(DebugName: "ECS045.SpriteExtraction"));
        effects = world.Query<Transform, ActiveVisualEffect>(new QueryFilter(DebugName: "ECS045.EffectExtraction"));
        shaders = world.Query<ShaderRequest>(new QueryFilter(DebugName: "ECS045.ShaderExtraction"));
        ComponentSignature reads = default;
        reads = reads.With(ComponentType<Transform>.Id).With(ComponentType<Sprite>.Id);
        Descriptor = new SystemDescriptor(PresentationSystemIds.RenderExtraction, nameof(SpriteRenderExtractionSystem),
            SystemPhase.RenderExtraction, SceneGroup.Global, readComponents: reads);
    }

    public SystemDescriptor Descriptor { get; }
    public RenderPacketStore Packets => packets;

    public void Update(ref SystemContext context) => Extract();

    public void Extract()
    {
        packets.BeginExtraction();
        ReadOnlyWorld readOnly = world.AsReadOnly();
        foreach (QueryChunk<Transform, Sprite> chunk in sprites)
        {
            ReadOnlySpan<Transform> transforms = chunk.Component1;
            ReadOnlySpan<Sprite> spriteValues = chunk.Component2;
            foreach (int row in chunk.Rows)
            {
                ref readonly Sprite sprite = ref spriteValues[row];
                if (!sprite.IsVisible || sprite.Texture.IsNull) continue;
                EntityId entity = chunk.Entities[row];
                Transform resolved = TransformResolver.Resolve(readOnly, entity, in transforms[row]);
                RenderPacketKind kind = Classify(world, entity);
                RenderLayer layer = LayerFor(kind, resolved.ZOrder);
                Vector2 scale = resolved.Scale;
                Color tint = sprite.Tint;
                if (world.TryGet(entity, out ActorPresentationState actor))
                {
                    resolved.Position += actor.DrawOffset;
                    scale *= actor.ScaleMultiplier;
                    tint = Multiply(tint, actor.TintColor);
                }

                RenderPacketFlags flags = sprite.HasSourceRectangle
                    ? RenderPacketFlags.HasSourceRectangle
                    : RenderPacketFlags.None;
                if (sprite.UsesPixelAlignedDestination)
                    flags |= RenderPacketFlags.PixelAlignedDestination;
                packets.Add(new RenderPacket(entity, sprite.Texture, sprite.SourceRectangle,
                    resolved.Position, Vector2.Zero, scale, tint, resolved.Rotation, 0f, 0f,
                    resolved.ZOrder, entity.Index, layer, kind, flags));
            }
        }

        foreach (QueryChunk<Transform, ActiveVisualEffect> chunk in effects)
        {
            ReadOnlySpan<Transform> transforms = chunk.Component1;
            ReadOnlySpan<ActiveVisualEffect> values = chunk.Component2;
            foreach (int row in chunk.Rows)
            {
                ref readonly ActiveVisualEffect effect = ref values[row];
                if ((effect.Flags & PresentationFlags.Active) == 0) continue;
                ref readonly Transform transform = ref transforms[row];
                float progress = effect.DurationSeconds <= 0f ? 1f : effect.ElapsedSeconds / effect.DurationSeconds;
                packets.Add(new RenderPacket(chunk.Entities[row], TextureAssetId.Null, default,
                    transform.Position, Vector2.Zero, transform.Scale, Color.White, transform.Rotation,
                    effect.Recipe.Value, Math.Clamp(progress, 0f, 1f), transform.ZOrder, chunk.Entities[row].Index,
                    RenderLayer.Overlay, RenderPacketKind.VisualEffect, RenderPacketFlags.None));
            }
        }

        foreach (QueryChunk<ShaderRequest> chunk in shaders)
        {
            ReadOnlySpan<ShaderRequest> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                ref readonly ShaderRequest shader = ref values[row];
                packets.Add(new RenderPacket(chunk.Entities[row], TextureAssetId.Null, default,
                    shader.Position, Vector2.Zero, shader.Size, Color.White, 0f, shader.Recipe.Value,
                    shader.Progress, shader.Sequence, chunk.Entities[row].Index, RenderLayer.Overlay,
                    RenderPacketKind.Shader, RenderPacketFlags.None));
            }
        }
        packets.EndExtraction();
    }

    private static RenderPacketKind Classify(World world, EntityId entity)
    {
        if (world.TryGet(entity, out CardZoneLocation location) && location.Zone == CardZone.Hand)
            return RenderPacketKind.HandCard;
        if (world.Has<CardData>(entity)) return RenderPacketKind.Card;
        if (world.Has<Player>(entity) ||
            world.Has<HP>(entity) && world.Has<Courage>(entity) &&
            world.Has<Temperance>(entity) && world.Has<ActionPoints>(entity) &&
            !world.Has<Enemy>(entity))
            return RenderPacketKind.Player;
        if (world.Has<Enemy>(entity)) return RenderPacketKind.Enemy;
        if (world.Has<PlayerHudRegion>(entity) || world.Has<PlayerHudAnchor>(entity)) return RenderPacketKind.Hud;
        if (world.Has<PauseMenuOverlay>(entity)) return RenderPacketKind.Modal;
        if (world.Has<POITitleTooltipSource>(entity) || world.Has<TooltipMetadata>(entity)) return RenderPacketKind.Tooltip;
        if (world.Has<ActiveVisualEffect>(entity)) return RenderPacketKind.VisualEffect;
        return RenderPacketKind.Sprite;
    }

    private static RenderLayer LayerFor(RenderPacketKind kind, int z) => kind switch
    {
        RenderPacketKind.Card or RenderPacketKind.HandCard => RenderLayer.Card,
        RenderPacketKind.Player or RenderPacketKind.Enemy => RenderLayer.Actor,
        RenderPacketKind.Hud => RenderLayer.Hud,
        RenderPacketKind.Tooltip => RenderLayer.Tooltip,
        RenderPacketKind.Modal or RenderPacketKind.Overlay => RenderLayer.Overlay,
        RenderPacketKind.VisualEffect or RenderPacketKind.Shader => RenderLayer.Overlay,
        _ => z < 0 ? RenderLayer.Background : RenderLayer.World,
    };

    private static Color Multiply(Color left, Color right) => new(
        (byte)(left.R * right.R / 255),
        (byte)(left.G * right.G / 255),
        (byte)(left.B * right.B / 255),
        (byte)(left.A * right.A / 255));
}

public static class TransformResolver
{
    public static Transform Resolve(ReadOnlyWorld world, EntityId entity, in Transform local)
    {
        Transform resolved = local;
        EntityId cursor = entity;
        for (var depth = 0; depth < 32 && world.TryGet(cursor, out ParentTransform parent); depth++)
        {
            if (!world.TryGet(parent.Parent, out Transform parentTransform)) break;
            resolved.Position += parentTransform.Position;
            resolved.ZOrder += parentTransform.ZOrder;
            cursor = parent.Parent;
        }
        return resolved;
    }
}
