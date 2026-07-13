#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Systems;

public sealed class SystemDescriptor
{
    private readonly Type[] readDynamicBufferTypes;
    private readonly Type[] writeDynamicBufferTypes;
    private readonly int[] consumedEventTypeIds;
    private readonly int[] emittedEventTypeIds;
    private readonly SystemId[] runsBefore;
    private readonly SystemId[] runsAfter;

    public SystemDescriptor(
        SystemId id,
        string name,
        SystemPhase phase,
        SceneGroup sceneGroup,
        ComponentSignature readComponents = default,
        ComponentSignature writeComponents = default,
        Type[]? readDynamicBufferTypes = null,
        Type[]? writeDynamicBufferTypes = null,
        int[]? consumedEventTypeIds = null,
        int[]? emittedEventTypeIds = null,
        SystemId[]? runsBefore = null,
        SystemId[]? runsAfter = null,
        bool recordsStructuralCommands = false,
        EventBarrier eventBarrier = EventBarrier.None,
        bool requiresExclusiveWorldAccess = false)
    {
        if (!id.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "System IDs must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateEnum(phase, nameof(phase));
        ValidateEnum(sceneGroup, nameof(sceneGroup));
        ValidateEnum(eventBarrier, nameof(eventBarrier));

        Id = id;
        Name = name;
        Phase = phase;
        SceneGroup = sceneGroup;
        ReadComponents = readComponents;
        WriteComponents = writeComponents;
        this.readDynamicBufferTypes = CloneAndValidateTypes(readDynamicBufferTypes, nameof(readDynamicBufferTypes));
        this.writeDynamicBufferTypes = CloneAndValidateTypes(writeDynamicBufferTypes, nameof(writeDynamicBufferTypes));
        this.consumedEventTypeIds = CloneAndValidateIds(consumedEventTypeIds, nameof(consumedEventTypeIds));
        this.emittedEventTypeIds = CloneAndValidateIds(emittedEventTypeIds, nameof(emittedEventTypeIds));
        this.runsBefore = CloneDependencies(runsBefore, id, nameof(runsBefore));
        this.runsAfter = CloneDependencies(runsAfter, id, nameof(runsAfter));
        RecordsStructuralCommands = recordsStructuralCommands;
        EventBarrier = eventBarrier;
        RequiresExclusiveWorldAccess = requiresExclusiveWorldAccess;
    }

    public SystemId Id { get; }

    public string Name { get; }

    public SystemPhase Phase { get; }

    public SceneGroup SceneGroup { get; }

    public ComponentSignature ReadComponents { get; }

    public ComponentSignature WriteComponents { get; }

    public bool RecordsStructuralCommands { get; }

    public EventBarrier EventBarrier { get; }

    /// <summary>
    /// The system may inspect or mutate component/buffer types selected by queued runtime data,
    /// so the root composition must explicitly order it against every system in the same phase.
    /// </summary>
    public bool RequiresExclusiveWorldAccess { get; }

    public ReadOnlySpan<Type> ReadDynamicBufferTypes => readDynamicBufferTypes;

    public ReadOnlySpan<Type> WriteDynamicBufferTypes => writeDynamicBufferTypes;

    public ReadOnlySpan<int> ConsumedEventTypeIds => consumedEventTypeIds;

    public ReadOnlySpan<int> EmittedEventTypeIds => emittedEventTypeIds;

    public ReadOnlySpan<SystemId> RunsBefore => runsBefore;

    public ReadOnlySpan<SystemId> RunsAfter => runsAfter;

    private static T[] CloneOrEmpty<T>(T[]? values) => values is null ? [] : (T[])values.Clone();

    private static Type[] CloneAndValidateTypes(Type[]? values, string parameterName)
    {
        Type[] result = CloneOrEmpty(values);
        for (var index = 0; index < result.Length; index++)
        {
            if (result[index] is null)
            {
                throw new ArgumentException("Dynamic-buffer type metadata cannot contain null.", parameterName);
            }
        }

        return result;
    }

    private static int[] CloneAndValidateIds(int[]? values, string parameterName)
    {
        int[] result = CloneOrEmpty(values);
        for (var index = 0; index < result.Length; index++)
        {
            if (result[index] <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Generated event type IDs must be positive.");
            }
        }

        return result;
    }

    private static SystemId[] CloneDependencies(SystemId[]? values, SystemId owner, string parameterName)
    {
        SystemId[] result = CloneOrEmpty(values);
        for (var index = 0; index < result.Length; index++)
        {
            if (!result[index].IsValid || result[index] == owner)
            {
                throw new ArgumentException(
                    "System dependencies must contain valid IDs and cannot reference the owning system.",
                    parameterName);
            }
        }

        return result;
    }

    private static void ValidateEnum<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, null);
        }
    }
}
