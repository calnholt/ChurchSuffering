using Crusaders30XX.ECS.Benchmarks.Model;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.Benchmarks.Adapters;

/// <summary>
/// Adapter over the real ECS-013/ECS-014 runtime. HarnessAuxiliary is an internal
/// always-present component that permits entity-only filtered queries without changing
/// the deliberately small reference-model signature.
/// </summary>
public sealed class DataOrientedWorldModelAdapter : IModelWorld
{
    private readonly World world = new(FoundationRegistryFactory.Create());
    private readonly Dictionary<ModelEntityHandle, DynamicBufferHandle<int>> buffers = [];
    private readonly CommandBuffer commandBuffer = new();

    public string Name => "data-oriented-world";

    public ModelWorldCapabilities Capabilities => ModelWorldCapabilities.All;

    public ModelEntityHandle Create()
    {
        var bundle = new SpawnBundle(componentCapacity: 1);
        var auxiliary = new HarnessAuxiliary();
        bundle.Add(in auxiliary);
        EntityId runtimeEntity = world.Create(in bundle);
        var modelEntity = ToModel(runtimeEntity);
        buffers.Add(modelEntity, world.CreateDynamicBuffer<int>(runtimeEntity, initialCapacity: 8));
        return modelEntity;
    }

    public bool IsAlive(ModelEntityHandle entity) => world.IsAlive(ToRuntime(entity));

    public void Destroy(ModelEntityHandle entity)
    {
        world.Destroy(ToRuntime(entity));
        buffers.Remove(entity);
    }

    public void Enable(ModelEntityHandle entity) => world.Enable(ToRuntime(entity));

    public void Disable(ModelEntityHandle entity) => world.Disable(ToRuntime(entity));

    public void AddComponent(
        ModelEntityHandle entity,
        ModelComponentKind component,
        ModelComponentValue value)
    {
        EntityId runtimeEntity = ToRuntime(entity);
        switch (component)
        {
            case ModelComponentKind.Position:
                var position = ToPosition(value);
                world.Add(runtimeEntity, in position);
                break;
            case ModelComponentKind.Velocity:
                var velocity = ToVelocity(value);
                world.Add(runtimeEntity, in velocity);
                break;
            case ModelComponentKind.Health:
                var health = ToHealth(value);
                world.Add(runtimeEntity, in health);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, null);
        }
    }

    public void RemoveComponent(ModelEntityHandle entity, ModelComponentKind component)
    {
        EntityId runtimeEntity = ToRuntime(entity);
        switch (component)
        {
            case ModelComponentKind.Position:
                world.Remove<HarnessPosition>(runtimeEntity);
                break;
            case ModelComponentKind.Velocity:
                world.Remove<HarnessVelocity>(runtimeEntity);
                break;
            case ModelComponentKind.Health:
                world.Remove<HarnessHealth>(runtimeEntity);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, null);
        }
    }

    public void SetComponent(
        ModelEntityHandle entity,
        ModelComponentKind component,
        ModelComponentValue value)
    {
        EntityId runtimeEntity = ToRuntime(entity);
        switch (component)
        {
            case ModelComponentKind.Position:
                var position = ToPosition(value);
                world.Set(runtimeEntity, in position);
                break;
            case ModelComponentKind.Velocity:
                var velocity = ToVelocity(value);
                world.Set(runtimeEntity, in velocity);
                break;
            case ModelComponentKind.Health:
                var health = ToHealth(value);
                world.Set(runtimeEntity, in health);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, null);
        }
    }

    public void AddTag(ModelEntityHandle entity, ModelTagKind tag)
    {
        EntityId runtimeEntity = ToRuntime(entity);
        if (tag == ModelTagKind.Primary)
        {
            world.AddTag<HarnessPrimaryTag>(runtimeEntity);
        }
        else
        {
            world.AddTag<HarnessSecondaryTag>(runtimeEntity);
        }
    }

    public void RemoveTag(ModelEntityHandle entity, ModelTagKind tag)
    {
        EntityId runtimeEntity = ToRuntime(entity);
        if (tag == ModelTagKind.Primary)
        {
            world.Remove<HarnessPrimaryTag>(runtimeEntity);
        }
        else
        {
            world.Remove<HarnessSecondaryTag>(runtimeEntity);
        }
    }

    public void AppendBuffer(ModelEntityHandle entity, int value)
    {
        world.GetDynamicBuffer(GetBuffer(entity)).Add(value);
    }

    public void RemoveBufferAt(ModelEntityHandle entity, int index)
    {
        world.GetDynamicBuffer(GetBuffer(entity)).RemoveAt(index);
    }

    public void ClearBuffer(ModelEntityHandle entity)
    {
        world.GetDynamicBuffer(GetBuffer(entity)).Clear();
    }

    public IReadOnlyList<ModelEntityHandle> Query(in ModelQuery query)
    {
        var filter = new QueryFilter(
            ToSignature(query.All),
            ToSignature(query.Any),
            ToSignature(query.None),
            query.IncludeDisabled,
            "model-adapter-query");
        Query<HarnessAuxiliary> runtimeQuery = world.Query<HarnessAuxiliary>(filter);
        var result = new List<ModelEntityHandle>();
        foreach (QueryChunk<HarnessAuxiliary> chunk in runtimeQuery)
        {
            foreach (int row in chunk.Rows)
            {
                result.Add(ToModel(chunk.Entities[row]));
            }
        }

        return result;
    }

    public ModelEntityObservation Observe(ModelEntityHandle entity)
    {
        EntityId runtimeEntity = ToRuntime(entity);
        if (!world.IsAlive(runtimeEntity))
        {
            return ModelEntityObservation.Dead(entity.Generation);
        }

        ModelTypeMask signature = ModelTypeMask.None;
        ModelComponentValue? position = Read<HarnessPosition>(
            runtimeEntity,
            ModelTypeMask.Position,
            static value => new ModelComponentValue(value.X, value.Y),
            ref signature);
        ModelComponentValue? velocity = Read<HarnessVelocity>(
            runtimeEntity,
            ModelTypeMask.Velocity,
            static value => new ModelComponentValue(value.X, value.Y),
            ref signature);
        ModelComponentValue? health = Read<HarnessHealth>(
            runtimeEntity,
            ModelTypeMask.Health,
            static value => new ModelComponentValue(value.Current, value.Maximum),
            ref signature);
        if (world.Has<HarnessPrimaryTag>(runtimeEntity))
        {
            signature |= ModelTypeMask.PrimaryTag;
        }

        if (world.Has<HarnessSecondaryTag>(runtimeEntity))
        {
            signature |= ModelTypeMask.SecondaryTag;
        }

        return new ModelEntityObservation(
            Alive: true,
            Enabled: world.IsEnabled(runtimeEntity),
            entity.Generation,
            signature,
            position,
            velocity,
            health,
            world.GetDynamicBuffer(GetBuffer(entity)).AsReadOnlySpan().ToArray());
    }

    public void Playback(IReadOnlyList<ModelCommand> commands)
    {
        var destroyed = new List<ModelEntityHandle>();
        IDynamicBufferCommandHandler<DynamicBufferMutation<int>> bufferHandler =
            world.GetDynamicBufferMutationHandler<int>();
        foreach (ModelCommand command in commands)
        {
            EntityId entity = ToRuntime(command.Entity);
            switch (command.Kind)
            {
                case ModelCommandKind.Destroy:
                    commandBuffer.Destroy(entity);
                    destroyed.Add(command.Entity);
                    break;
                case ModelCommandKind.Enable:
                    commandBuffer.Enable(entity);
                    break;
                case ModelCommandKind.Disable:
                    commandBuffer.Disable(entity);
                    break;
                case ModelCommandKind.AddComponent:
                    RecordAdd(entity, command.Component, command.Value);
                    break;
                case ModelCommandKind.RemoveComponent:
                    RecordRemove(entity, command.Component);
                    break;
                case ModelCommandKind.SetComponent:
                    RecordSet(entity, command.Component, command.Value);
                    break;
                case ModelCommandKind.AddTag:
                    RecordAddTag(entity, command.Tag);
                    break;
                case ModelCommandKind.RemoveTag:
                    RecordRemoveTag(entity, command.Tag);
                    break;
                case ModelCommandKind.AppendBuffer:
                    DynamicBufferMutation<int> append = DynamicBufferMutation<int>.Add(
                        GetBuffer(command.Entity),
                        command.BufferValueOrIndex);
                    commandBuffer.RecordDynamicBufferMutation(bufferHandler, in append);
                    break;
                case ModelCommandKind.RemoveBufferAt:
                    DynamicBufferMutation<int> remove = DynamicBufferMutation<int>.RemoveAt(
                        GetBuffer(command.Entity),
                        command.BufferValueOrIndex);
                    commandBuffer.RecordDynamicBufferMutation(bufferHandler, in remove);
                    break;
                case ModelCommandKind.ClearBuffer:
                    DynamicBufferMutation<int> clear = DynamicBufferMutation<int>.Clear(GetBuffer(command.Entity));
                    commandBuffer.RecordDynamicBufferMutation(bufferHandler, in clear);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command.Kind, null);
            }
        }

        commandBuffer.Playback(world);
        foreach (ModelEntityHandle entity in destroyed)
        {
            buffers.Remove(entity);
        }
    }

    private void RecordAdd(EntityId entity, ModelComponentKind component, ModelComponentValue value)
    {
        switch (component)
        {
            case ModelComponentKind.Position:
                var position = ToPosition(value);
                commandBuffer.Add(entity, in position);
                break;
            case ModelComponentKind.Velocity:
                var velocity = ToVelocity(value);
                commandBuffer.Add(entity, in velocity);
                break;
            case ModelComponentKind.Health:
                var health = ToHealth(value);
                commandBuffer.Add(entity, in health);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, null);
        }
    }

    private void RecordSet(EntityId entity, ModelComponentKind component, ModelComponentValue value)
    {
        switch (component)
        {
            case ModelComponentKind.Position:
                var position = ToPosition(value);
                commandBuffer.Set(entity, in position);
                break;
            case ModelComponentKind.Velocity:
                var velocity = ToVelocity(value);
                commandBuffer.Set(entity, in velocity);
                break;
            case ModelComponentKind.Health:
                var health = ToHealth(value);
                commandBuffer.Set(entity, in health);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, null);
        }
    }

    private void RecordRemove(EntityId entity, ModelComponentKind component)
    {
        switch (component)
        {
            case ModelComponentKind.Position:
                commandBuffer.Remove<HarnessPosition>(entity);
                break;
            case ModelComponentKind.Velocity:
                commandBuffer.Remove<HarnessVelocity>(entity);
                break;
            case ModelComponentKind.Health:
                commandBuffer.Remove<HarnessHealth>(entity);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component, null);
        }
    }

    private void RecordAddTag(EntityId entity, ModelTagKind tag)
    {
        if (tag == ModelTagKind.Primary)
        {
            commandBuffer.AddTag<HarnessPrimaryTag>(entity);
        }
        else
        {
            commandBuffer.AddTag<HarnessSecondaryTag>(entity);
        }
    }

    private void RecordRemoveTag(EntityId entity, ModelTagKind tag)
    {
        if (tag == ModelTagKind.Primary)
        {
            commandBuffer.RemoveTag<HarnessPrimaryTag>(entity);
        }
        else
        {
            commandBuffer.RemoveTag<HarnessSecondaryTag>(entity);
        }
    }

    private ModelComponentValue? Read<T>(
        EntityId entity,
        ModelTypeMask mask,
        Func<T, ModelComponentValue> convert,
        ref ModelTypeMask signature)
        where T : unmanaged, IComponent
    {
        if (!world.TryGet(entity, out T value))
        {
            return null;
        }

        signature |= mask;
        return convert(value);
    }

    private DynamicBufferHandle<int> GetBuffer(ModelEntityHandle entity)
    {
        if (!buffers.TryGetValue(entity, out DynamicBufferHandle<int> handle))
        {
            throw new InvalidOperationException($"Entity {entity} has no live model buffer.");
        }

        return handle;
    }

    private static ComponentSignature ToSignature(ModelTypeMask mask)
    {
        var signature = ComponentSignature.Empty;
        if (mask.HasFlag(ModelTypeMask.Position))
        {
            signature = signature.With(ComponentType<HarnessPosition>.Id);
        }

        if (mask.HasFlag(ModelTypeMask.Velocity))
        {
            signature = signature.With(ComponentType<HarnessVelocity>.Id);
        }

        if (mask.HasFlag(ModelTypeMask.Health))
        {
            signature = signature.With(ComponentType<HarnessHealth>.Id);
        }

        if (mask.HasFlag(ModelTypeMask.PrimaryTag))
        {
            signature = signature.With(ComponentType<HarnessPrimaryTag>.Id);
        }

        if (mask.HasFlag(ModelTypeMask.SecondaryTag))
        {
            signature = signature.With(ComponentType<HarnessSecondaryTag>.Id);
        }

        return signature;
    }

    private static HarnessPosition ToPosition(ModelComponentValue value) =>
        new() { X = value.First, Y = value.Second };

    private static HarnessVelocity ToVelocity(ModelComponentValue value) =>
        new() { X = value.First, Y = value.Second };

    private static HarnessHealth ToHealth(ModelComponentValue value) =>
        new() { Current = value.First, Maximum = value.Second };

    private static EntityId ToRuntime(ModelEntityHandle entity) => new(entity.Index, entity.Generation);

    private static ModelEntityHandle ToModel(EntityId entity) => new(entity.Index, entity.Generation);
}
