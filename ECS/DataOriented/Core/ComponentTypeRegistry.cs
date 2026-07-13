#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace Crusaders30XX.ECS.DataOriented.Core;

public interface IComponent { }

public interface ITag { }

public readonly record struct ComponentTypeMetadata(
    int Id,
    int Size,
    bool IsTag,
    string DebugName);

public interface IComponentTypeRegistry
{
    int Count { get; }

    ComponentSignature RegisteredSignature { get; }

    bool TryGetMetadata(int typeId, out ComponentTypeMetadata metadata);
}

public static class ComponentType<T>
{
    private static int id = -1;
    private static bool isTag;

    public static int Id => id >= 0
        ? id
        : throw new InvalidOperationException(
            $"Type {typeof(T).FullName} is not registered with the data-oriented ECS.");

    public static bool IsTag
    {
        get
        {
            _ = Id;
            return isTag;
        }
    }

    internal static void Assign(int value, bool tag)
    {
        if (id >= 0 && (id != value || isTag != tag))
        {
            throw new InvalidOperationException(
                $"Type {typeof(T).FullName} was already registered as ID {id} and cannot be reassigned to ID {value}.");
        }

        id = value;
        isTag = tag;
    }
}

public sealed class ComponentTypeRegistry : IComponentTypeRegistry
{
    private readonly ComponentDescriptor?[] descriptors =
        new ComponentDescriptor?[ComponentSignature.MaximumTypeCount];
    private readonly HashSet<Type> registeredTypes = new();
    private int count;
    private bool isSealed;
    private ComponentSignature registeredSignature;

    public int Count => count;

    public bool IsSealed => isSealed;

    public ComponentSignature RegisteredSignature => registeredSignature;

    public void RegisterComponent<T>(int id)
        where T : unmanaged, IComponent
    {
        Register(
            id,
            isTag: false,
            Unsafe.SizeOf<T>(),
            typeof(T),
            static (typeId, capacity) => new ComponentColumn<T>(typeId, capacity));
        ComponentType<T>.Assign(id, tag: false);
    }

    public void RegisterTag<T>(int id)
        where T : unmanaged, ITag
    {
        if (typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length != 0)
        {
            throw new InvalidOperationException($"Tag {typeof(T).FullName} must not declare instance fields.");
        }

        Register(id, isTag: true, size: 0, typeof(T), columnFactory: null);
        ComponentType<T>.Assign(id, tag: true);
    }

    public void Seal()
    {
        isSealed = true;
    }

    public bool TryGetMetadata(int typeId, out ComponentTypeMetadata metadata)
    {
        if ((uint)typeId < descriptors.Length && descriptors[typeId] is { } descriptor)
        {
            metadata = descriptor.Metadata;
            return true;
        }

        metadata = default;
        return false;
    }

    internal ComponentDescriptor GetDescriptor(int typeId)
    {
        if ((uint)typeId >= descriptors.Length || descriptors[typeId] is not { } descriptor)
        {
            throw new InvalidOperationException($"Component or tag type ID {typeId} is not registered in this world.");
        }

        return descriptor;
    }

    internal void ValidateSignature(in ComponentSignature signature)
    {
        if (!registeredSignature.ContainsAll(signature))
        {
            throw new InvalidOperationException("A component signature contains one or more unregistered type IDs.");
        }
    }

    private void Register(
        int id,
        bool isTag,
        int size,
        Type type,
        Func<int, int, IComponentColumn>? columnFactory)
    {
        if (isSealed)
        {
            throw new InvalidOperationException("The component type registry is sealed and cannot be changed.");
        }

        if ((uint)id >= ComponentSignature.MaximumTypeCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                $"Component and tag type IDs must be between 0 and {ComponentSignature.MaximumTypeCount - 1}.");
        }

        if (descriptors[id] is not null)
        {
            throw new InvalidOperationException($"Component or tag type ID {id} is already registered.");
        }

        if (!registeredTypes.Add(type))
        {
            throw new InvalidOperationException($"Type {type.FullName} is already registered.");
        }

        descriptors[id] = new ComponentDescriptor(
            new ComponentTypeMetadata(id, size, isTag, type.FullName ?? type.Name),
            columnFactory);
        registeredSignature = registeredSignature.With(id);
        count++;
    }
}

internal sealed class ComponentDescriptor
{
    private readonly Func<int, int, IComponentColumn>? columnFactory;

    public ComponentDescriptor(
        ComponentTypeMetadata metadata,
        Func<int, int, IComponentColumn>? columnFactory)
    {
        Metadata = metadata;
        this.columnFactory = columnFactory;
    }

    public ComponentTypeMetadata Metadata { get; }

    public IComponentColumn CreateColumn(int capacity)
    {
        if (Metadata.IsTag || columnFactory is null)
        {
            throw new InvalidOperationException($"Tag {Metadata.DebugName} does not have a component column.");
        }

        return columnFactory(Metadata.Id, capacity);
    }
}
