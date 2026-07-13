#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Crusaders30XX.ECS.DataOriented.Core;

public readonly record struct DeferredEntity(int BufferId, int Version, int Slot);

public readonly struct CommandEntity
{
    private readonly EntityId entity;
    private readonly DeferredEntity deferred;

    private CommandEntity(EntityId entity)
    {
        this.entity = entity;
        deferred = default;
        IsDeferred = false;
    }

    private CommandEntity(DeferredEntity deferred)
    {
        entity = default;
        this.deferred = deferred;
        IsDeferred = true;
    }

    internal bool IsDeferred { get; }

    internal EntityId Entity => entity;

    internal DeferredEntity Deferred => deferred;

    public static implicit operator CommandEntity(EntityId entity) => new(entity);

    public static implicit operator CommandEntity(DeferredEntity deferred) => new(deferred);
}

public sealed class CommandBuffer
{
    private static int nextBufferId;

    private CommandEntry[] commands;
    private byte[] payload;
    private SpawnBundle[] bundles;
    private EntityId[] createdEntities;
    private int commandCount;
    private int payloadLength;
    private int bundleCount;
    private int createdEntityCount;
    private int version = 1;
    private bool playbackCompleted;
    private bool isPlaying;

    public CommandBuffer(
        int initialCommandCapacity = 32,
        int initialPayloadCapacity = 256,
        int initialBundleCapacity = 8)
    {
        if (initialCommandCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCommandCapacity));
        }

        if (initialPayloadCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialPayloadCapacity));
        }

        if (initialBundleCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialBundleCapacity));
        }

        BufferId = Interlocked.Increment(ref nextBufferId);
        commands = new CommandEntry[Math.Max(1, initialCommandCapacity)];
        payload = new byte[Math.Max(1, initialPayloadCapacity)];
        bundles = new SpawnBundle[Math.Max(1, initialBundleCapacity)];
        createdEntities = new EntityId[Math.Max(1, initialBundleCapacity)];
    }

    public int BufferId { get; }

    public int Count => commandCount;

    public DeferredEntity Create(in SpawnBundle bundle)
    {
        PrepareRecording();
        var bundleIndex = StoreBundle(in bundle);
        var slot = createdEntityCount++;
        EnsureCreatedEntityCapacity(createdEntityCount);
        createdEntities[slot] = default;
        AddCommand(new CommandEntry(CommandKind.Create, default, bundleIndex: bundleIndex, deferredSlot: slot));
        return new DeferredEntity(BufferId, version, slot);
    }

    public void Destroy(CommandEntity target) => RecordTargetCommand(CommandKind.Destroy, target);

    public void Enable(CommandEntity target) => RecordTargetCommand(CommandKind.Enable, target);

    public void Disable(CommandEntity target) => RecordTargetCommand(CommandKind.Disable, target);

    public void Add<T>(CommandEntity target, in T component)
        where T : unmanaged, IComponent
    {
        PrepareRecording();
        var (offset, size) = StorePayload(in component);
        AddCommand(new CommandEntry(
            CommandKind.AddComponent,
            target,
            ComponentType<T>.Id,
            offset,
            size));
    }

    public void Set<T>(CommandEntity target, in T component)
        where T : unmanaged, IComponent
    {
        PrepareRecording();
        var (offset, size) = StorePayload(in component);
        AddCommand(new CommandEntry(
            CommandKind.SetComponent,
            target,
            ComponentType<T>.Id,
            offset,
            size));
    }

    public void Remove<T>(CommandEntity target)
    {
        PrepareRecording();
        AddCommand(new CommandEntry(CommandKind.RemoveType, target, ComponentType<T>.Id));
    }

    public void AddTag<T>(CommandEntity target)
        where T : unmanaged, ITag
    {
        PrepareRecording();
        AddCommand(new CommandEntry(CommandKind.AddTag, target, ComponentType<T>.Id));
    }

    public void RemoveTag<T>(CommandEntity target)
        where T : unmanaged, ITag => Remove<T>(target);

    public void Transition(
        CommandEntity target,
        in SpawnBundle additions,
        in ComponentSignature removals)
    {
        PrepareRecording();
        var bundleIndex = StoreBundle(in additions);
        AddCommand(new CommandEntry(
            CommandKind.Transition,
            target,
            bundleIndex: bundleIndex,
            signature: removals));
    }

    public void RecordDynamicBufferMutation<TCommand>(
        IDynamicBufferCommandHandler<TCommand> handler,
        in TCommand command)
        where TCommand : unmanaged
    {
        ArgumentNullException.ThrowIfNull(handler);
        PrepareRecording();
        var (offset, size) = StorePayload(in command);
        AddCommand(new CommandEntry(
            CommandKind.DynamicBufferMutation,
            default,
            payloadOffset: offset,
            payloadSize: size,
            dynamicHandler: handler,
            dynamicPlayback: DynamicPlayback<TCommand>.Callback));
    }

    public void Playback(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (isPlaying)
        {
            throw new InvalidOperationException("A command buffer cannot play back recursively.");
        }

        isPlaying = true;
        try
        {
            for (var index = 0; index < commandCount; index++)
            {
                ref readonly var command = ref commands[index];
                switch (command.Kind)
                {
                    case CommandKind.Create:
                        createdEntities[command.DeferredSlot] = world.Create(in bundles[command.BundleIndex]);
                        break;
                    case CommandKind.Destroy:
                        world.Destroy(ResolveTarget(command.Target));
                        break;
                    case CommandKind.Enable:
                        world.Enable(ResolveTarget(command.Target));
                        break;
                    case CommandKind.Disable:
                        world.Disable(ResolveTarget(command.Target));
                        break;
                    case CommandKind.AddComponent:
                        world.AddComponentBytes(
                            ResolveTarget(command.Target),
                            command.TypeId,
                            GetPayload(command));
                        break;
                    case CommandKind.SetComponent:
                        world.SetComponentBytes(
                            ResolveTarget(command.Target),
                            command.TypeId,
                            GetPayload(command));
                        break;
                    case CommandKind.RemoveType:
                        world.RemoveType(ResolveTarget(command.Target), command.TypeId);
                        break;
                    case CommandKind.AddTag:
                        world.AddTag(ResolveTarget(command.Target), command.TypeId);
                        break;
                    case CommandKind.Transition:
                        var removals = command.Signature;
                        world.Transition(
                            ResolveTarget(command.Target),
                            in bundles[command.BundleIndex],
                            in removals);
                        break;
                    case CommandKind.DynamicBufferMutation:
                        command.DynamicPlayback!(
                            command.DynamicHandler!,
                            world,
                            GetPayload(command));
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown command kind {command.Kind}.");
                }
            }
        }
        finally
        {
            isPlaying = false;
            ClearRecordedCommands();
            playbackCompleted = true;
        }
    }

    public EntityId Resolve(DeferredEntity deferred)
    {
        ValidateDeferred(deferred);
        var resolved = createdEntities[deferred.Slot];
        if (resolved.IsNull)
        {
            throw new InvalidOperationException("The deferred entity has not been created by playback yet.");
        }

        return resolved;
    }

    public void Clear()
    {
        if (isPlaying)
        {
            throw new InvalidOperationException("A command buffer cannot be cleared during playback.");
        }

        ClearRecordedCommands();
        Array.Clear(createdEntities, 0, createdEntityCount);
        createdEntityCount = 0;
        version = NextVersion(version);
        playbackCompleted = false;
    }

    private void RecordTargetCommand(CommandKind kind, CommandEntity target)
    {
        PrepareRecording();
        AddCommand(new CommandEntry(kind, target));
    }

    private void PrepareRecording()
    {
        if (isPlaying)
        {
            throw new InvalidOperationException("Commands cannot be recorded during playback.");
        }

        if (!playbackCompleted)
        {
            return;
        }

        Array.Clear(createdEntities, 0, createdEntityCount);
        createdEntityCount = 0;
        version = NextVersion(version);
        playbackCompleted = false;
    }

    private int StoreBundle(in SpawnBundle bundle)
    {
        EnsureBundleCapacity(bundleCount + 1);
        bundle.CopyTo(ref bundles[bundleCount]);
        return bundleCount++;
    }

    private (int Offset, int Size) StorePayload<T>(in T value)
        where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        EnsurePayloadCapacity(payloadLength + size);
        var copy = value;
        MemoryMarshal.Write(payload.AsSpan(payloadLength, size), in copy);
        var offset = payloadLength;
        payloadLength += size;
        return (offset, size);
    }

    private ReadOnlySpan<byte> GetPayload(in CommandEntry command) =>
        payload.AsSpan(command.PayloadOffset, command.PayloadSize);

    private EntityId ResolveTarget(CommandEntity target)
    {
        if (!target.IsDeferred)
        {
            return target.Entity;
        }

        ValidateDeferred(target.Deferred);
        var resolved = createdEntities[target.Deferred.Slot];
        if (resolved.IsNull)
        {
            throw new InvalidOperationException(
                "A deferred entity cannot be referenced before its create command plays back.");
        }

        return resolved;
    }

    private void ValidateDeferred(DeferredEntity deferred)
    {
        if (deferred.BufferId != BufferId || deferred.Version != version ||
            deferred.Slot < 0 || deferred.Slot >= createdEntityCount)
        {
            throw new InvalidOperationException("The deferred entity does not belong to this command-buffer recording.");
        }
    }

    private void AddCommand(in CommandEntry command)
    {
        if (commandCount == commands.Length)
        {
            Array.Resize(ref commands, commands.Length * 2);
        }

        commands[commandCount++] = command;
    }

    private void EnsurePayloadCapacity(int required)
    {
        if (payload.Length >= required)
        {
            return;
        }

        Array.Resize(ref payload, Math.Max(required, payload.Length * 2));
    }

    private void EnsureBundleCapacity(int required)
    {
        if (bundles.Length >= required)
        {
            return;
        }

        Array.Resize(ref bundles, Math.Max(required, bundles.Length * 2));
    }

    private void EnsureCreatedEntityCapacity(int required)
    {
        if (createdEntities.Length >= required)
        {
            return;
        }

        Array.Resize(ref createdEntities, Math.Max(required, createdEntities.Length * 2));
    }

    private void ClearRecordedCommands()
    {
        Array.Clear(commands, 0, commandCount);
        for (var index = 0; index < bundleCount; index++)
        {
            bundles[index].Clear();
        }

        commandCount = 0;
        payloadLength = 0;
        bundleCount = 0;
    }

    private static int NextVersion(int current)
    {
        var next = unchecked(current + 1);
        return next <= 0 ? 1 : next;
    }

    private enum CommandKind : byte
    {
        Create,
        Destroy,
        Enable,
        Disable,
        AddComponent,
        SetComponent,
        RemoveType,
        AddTag,
        Transition,
        DynamicBufferMutation,
    }

    private delegate void DynamicCommandPlayback(
        object handler,
        World world,
        ReadOnlySpan<byte> payload);

    private readonly struct CommandEntry
    {
        public CommandEntry(
            CommandKind kind,
            CommandEntity target,
            int typeId = 0,
            int payloadOffset = 0,
            int payloadSize = 0,
            int bundleIndex = 0,
            int deferredSlot = 0,
            ComponentSignature signature = default,
            object? dynamicHandler = null,
            DynamicCommandPlayback? dynamicPlayback = null)
        {
            Kind = kind;
            Target = target;
            TypeId = typeId;
            PayloadOffset = payloadOffset;
            PayloadSize = payloadSize;
            BundleIndex = bundleIndex;
            DeferredSlot = deferredSlot;
            Signature = signature;
            DynamicHandler = dynamicHandler;
            DynamicPlayback = dynamicPlayback;
        }

        public CommandKind Kind { get; }
        public CommandEntity Target { get; }
        public int TypeId { get; }
        public int PayloadOffset { get; }
        public int PayloadSize { get; }
        public int BundleIndex { get; }
        public int DeferredSlot { get; }
        public ComponentSignature Signature { get; }
        public object? DynamicHandler { get; }
        public DynamicCommandPlayback? DynamicPlayback { get; }
    }

    private static class DynamicPlayback<TCommand>
        where TCommand : unmanaged
    {
        public static readonly DynamicCommandPlayback Callback = Play;

        private static void Play(object handler, World world, ReadOnlySpan<byte> payload)
        {
            var command = MemoryMarshal.Read<TCommand>(payload);
            ((IDynamicBufferCommandHandler<TCommand>)handler).Playback(world, in command);
        }
    }
}
