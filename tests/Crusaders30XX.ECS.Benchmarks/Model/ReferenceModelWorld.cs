namespace Crusaders30XX.ECS.Benchmarks.Model;

/// <summary>
/// Intentionally straightforward oracle. It favors explicit validation and simple
/// collections over sharing any archetype/chunk implementation strategy.
/// </summary>
public sealed class ReferenceModelWorld : IModelWorld
{
    private readonly List<Entry> entries = [new Entry { Generation = 0 }];
    private readonly Stack<int> freeIndexes = new();

    public string Name => "reference-model";

    public ModelWorldCapabilities Capabilities => ModelWorldCapabilities.All;

    public ModelEntityHandle Create()
    {
        int index;
        Entry entry;
        if (freeIndexes.TryPop(out index))
        {
            entry = entries[index];
            entry.Generation = checked(entry.Generation + 1);
        }
        else
        {
            index = entries.Count;
            entry = new Entry { Generation = 1 };
            entries.Add(entry);
        }

        entry.Alive = true;
        entry.Enabled = true;
        entry.Signature = ModelTypeMask.None;
        entry.Position = null;
        entry.Velocity = null;
        entry.Health = null;
        entry.Buffer.Clear();
        return new ModelEntityHandle(index, entry.Generation);
    }

    public bool IsAlive(ModelEntityHandle entity) =>
        entity.Index > 0 &&
        entity.Index < entries.Count &&
        entries[entity.Index].Alive &&
        entries[entity.Index].Generation == entity.Generation;

    public void Destroy(ModelEntityHandle entity)
    {
        Entry entry = RequireAlive(entity);
        entry.Alive = false;
        entry.Enabled = false;
        entry.Signature = ModelTypeMask.None;
        entry.Position = null;
        entry.Velocity = null;
        entry.Health = null;
        entry.Buffer.Clear();
        freeIndexes.Push(entity.Index);
    }

    public void Enable(ModelEntityHandle entity) => RequireAlive(entity).Enabled = true;

    public void Disable(ModelEntityHandle entity) => RequireAlive(entity).Enabled = false;

    public void AddComponent(
        ModelEntityHandle entity,
        ModelComponentKind component,
        ModelComponentValue value)
    {
        Entry entry = RequireAlive(entity);
        ModelTypeMask mask = ToMask(component);
        if ((entry.Signature & mask) != 0)
        {
            throw new InvalidOperationException($"Entity {entity} already has component {component}.");
        }

        entry.Signature |= mask;
        SetValue(entry, component, value);
    }

    public void RemoveComponent(ModelEntityHandle entity, ModelComponentKind component)
    {
        Entry entry = RequireAlive(entity);
        ModelTypeMask mask = ToMask(component);
        if ((entry.Signature & mask) == 0)
        {
            throw new InvalidOperationException($"Entity {entity} does not have component {component}.");
        }

        entry.Signature &= ~mask;
        SetValue(entry, component, null);
    }

    public void SetComponent(
        ModelEntityHandle entity,
        ModelComponentKind component,
        ModelComponentValue value)
    {
        Entry entry = RequireAlive(entity);
        if ((entry.Signature & ToMask(component)) == 0)
        {
            throw new InvalidOperationException($"Entity {entity} does not have component {component}.");
        }

        SetValue(entry, component, value);
    }

    public void AddTag(ModelEntityHandle entity, ModelTagKind tag)
    {
        Entry entry = RequireAlive(entity);
        ModelTypeMask mask = ToMask(tag);
        if ((entry.Signature & mask) != 0)
        {
            throw new InvalidOperationException($"Entity {entity} already has tag {tag}.");
        }

        entry.Signature |= mask;
    }

    public void RemoveTag(ModelEntityHandle entity, ModelTagKind tag)
    {
        Entry entry = RequireAlive(entity);
        ModelTypeMask mask = ToMask(tag);
        if ((entry.Signature & mask) == 0)
        {
            throw new InvalidOperationException($"Entity {entity} does not have tag {tag}.");
        }

        entry.Signature &= ~mask;
    }

    public void AppendBuffer(ModelEntityHandle entity, int value) =>
        RequireAlive(entity).Buffer.Add(value);

    public void RemoveBufferAt(ModelEntityHandle entity, int index)
    {
        Entry entry = RequireAlive(entity);
        entry.Buffer.RemoveAt(index);
    }

    public void ClearBuffer(ModelEntityHandle entity) => RequireAlive(entity).Buffer.Clear();

    public IReadOnlyList<ModelEntityHandle> Query(in ModelQuery query)
    {
        var matches = new List<ModelEntityHandle>();
        for (int index = 1; index < entries.Count; index++)
        {
            Entry entry = entries[index];
            if (!entry.Alive || (!query.IncludeDisabled && !entry.Enabled))
            {
                continue;
            }

            bool hasAll = (entry.Signature & query.All) == query.All;
            bool hasAny = query.Any == ModelTypeMask.None || (entry.Signature & query.Any) != 0;
            bool hasNone = (entry.Signature & query.None) == 0;
            if (hasAll && hasAny && hasNone)
            {
                matches.Add(new ModelEntityHandle(index, entry.Generation));
            }
        }

        return matches;
    }

    public ModelEntityObservation Observe(ModelEntityHandle entity)
    {
        if (!IsAlive(entity))
        {
            int generation = entity.Index > 0 && entity.Index < entries.Count
                ? entries[entity.Index].Generation
                : 0;
            return ModelEntityObservation.Dead(generation);
        }

        Entry entry = entries[entity.Index];
        return new ModelEntityObservation(
            entry.Alive,
            entry.Enabled,
            entry.Generation,
            entry.Signature,
            entry.Position,
            entry.Velocity,
            entry.Health,
            entry.Buffer.ToArray());
    }

    public void Playback(IReadOnlyList<ModelCommand> commands)
    {
        foreach (ModelCommand command in commands)
        {
            switch (command.Kind)
            {
                case ModelCommandKind.Destroy:
                    Destroy(command.Entity);
                    break;
                case ModelCommandKind.Enable:
                    Enable(command.Entity);
                    break;
                case ModelCommandKind.Disable:
                    Disable(command.Entity);
                    break;
                case ModelCommandKind.AddComponent:
                    AddComponent(command.Entity, command.Component, command.Value);
                    break;
                case ModelCommandKind.RemoveComponent:
                    RemoveComponent(command.Entity, command.Component);
                    break;
                case ModelCommandKind.SetComponent:
                    SetComponent(command.Entity, command.Component, command.Value);
                    break;
                case ModelCommandKind.AddTag:
                    AddTag(command.Entity, command.Tag);
                    break;
                case ModelCommandKind.RemoveTag:
                    RemoveTag(command.Entity, command.Tag);
                    break;
                case ModelCommandKind.AppendBuffer:
                    AppendBuffer(command.Entity, command.BufferValueOrIndex);
                    break;
                case ModelCommandKind.RemoveBufferAt:
                    RemoveBufferAt(command.Entity, command.BufferValueOrIndex);
                    break;
                case ModelCommandKind.ClearBuffer:
                    ClearBuffer(command.Entity);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command.Kind, null);
            }
        }
    }

    private Entry RequireAlive(ModelEntityHandle entity)
    {
        if (!IsAlive(entity))
        {
            throw new InvalidOperationException($"Entity handle {entity} is dead or stale.");
        }

        return entries[entity.Index];
    }

    private static ModelTypeMask ToMask(ModelComponentKind component) => component switch
    {
        ModelComponentKind.Position => ModelTypeMask.Position,
        ModelComponentKind.Velocity => ModelTypeMask.Velocity,
        ModelComponentKind.Health => ModelTypeMask.Health,
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
    };

    private static ModelTypeMask ToMask(ModelTagKind tag) => tag switch
    {
        ModelTagKind.Primary => ModelTypeMask.PrimaryTag,
        ModelTagKind.Secondary => ModelTypeMask.SecondaryTag,
        _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, null),
    };

    private static void SetValue(
        Entry entry,
        ModelComponentKind component,
        ModelComponentValue? value)
    {
        switch (component)
        {
            case ModelComponentKind.Position:
                entry.Position = value;
                break;
            case ModelComponentKind.Velocity:
                entry.Velocity = value;
                break;
            case ModelComponentKind.Health:
                entry.Health = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, null);
        }
    }

    private sealed class Entry
    {
        public int Generation;
        public bool Alive;
        public bool Enabled;
        public ModelTypeMask Signature;
        public ModelComponentValue? Position;
        public ModelComponentValue? Velocity;
        public ModelComponentValue? Health;
        public List<int> Buffer { get; } = [];
    }
}
