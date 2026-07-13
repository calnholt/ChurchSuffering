namespace Crusaders30XX.ECS.Benchmarks.Model;

public readonly record struct ModelEntityHandle(int Index, int Generation)
{
    public static ModelEntityHandle Null => default;
    public bool IsNull => Index == 0;
}

public readonly record struct ScenarioEntityId(int Value);

[Flags]
public enum ModelWorldCapabilities
{
    None = 0,
    EntityLifecycle = 1 << 0,
    ComponentsAndTags = 1 << 1,
    EnableDisable = 1 << 2,
    Queries = 1 << 3,
    CommandPlayback = 1 << 4,
    DynamicBuffers = 1 << 5,
    All = EntityLifecycle | ComponentsAndTags | EnableDisable | Queries | CommandPlayback | DynamicBuffers,
}

[Flags]
public enum ModelOperationCoverage
{
    None = 0,
    Lifecycle = 1 << 0,
    Components = 1 << 1,
    Tags = 1 << 2,
    EnableDisable = 1 << 3,
    Queries = 1 << 4,
    CommandPlayback = 1 << 5,
    DynamicBuffers = 1 << 6,
    All = Lifecycle | Components | Tags | EnableDisable | Queries | CommandPlayback | DynamicBuffers,
}

[Flags]
public enum ModelTypeMask : ushort
{
    None = 0,
    Position = 1 << 0,
    Velocity = 1 << 1,
    Health = 1 << 2,
    PrimaryTag = 1 << 3,
    SecondaryTag = 1 << 4,
}

public enum ModelComponentKind
{
    Position,
    Velocity,
    Health,
}

public enum ModelTagKind
{
    Primary,
    Secondary,
}

public readonly record struct ModelComponentValue(int First, int Second);

public readonly record struct ModelQuery(
    ModelTypeMask All,
    ModelTypeMask Any,
    ModelTypeMask None,
    bool IncludeDisabled = false);

public sealed record ModelEntityObservation(
    bool Alive,
    bool Enabled,
    int Generation,
    ModelTypeMask Signature,
    ModelComponentValue? Position,
    ModelComponentValue? Velocity,
    ModelComponentValue? Health,
    IReadOnlyList<int> BufferContents)
{
    public static ModelEntityObservation Dead(int generation) => new(
        Alive: false,
        Enabled: false,
        generation,
        ModelTypeMask.None,
        Position: null,
        Velocity: null,
        Health: null,
        Array.Empty<int>());
}

public enum ModelCommandKind
{
    Destroy,
    Enable,
    Disable,
    AddComponent,
    RemoveComponent,
    SetComponent,
    AddTag,
    RemoveTag,
    AppendBuffer,
    RemoveBufferAt,
    ClearBuffer,
}

public readonly record struct ModelCommand(
    ModelCommandKind Kind,
    ModelEntityHandle Entity,
    ModelComponentKind Component = default,
    ModelComponentValue Value = default,
    ModelTagKind Tag = default,
    int BufferValueOrIndex = default);

/// <summary>
/// Adapter boundary between the deliberately simple reference model and the runtime under
/// test. Runtime capabilities can expand without coupling the scenario engine to storage.
/// </summary>
public interface IModelWorld
{
    string Name { get; }

    ModelWorldCapabilities Capabilities { get; }

    ModelEntityHandle Create();

    bool IsAlive(ModelEntityHandle entity);

    void Destroy(ModelEntityHandle entity);

    void Enable(ModelEntityHandle entity);

    void Disable(ModelEntityHandle entity);

    void AddComponent(
        ModelEntityHandle entity,
        ModelComponentKind component,
        ModelComponentValue value);

    void RemoveComponent(ModelEntityHandle entity, ModelComponentKind component);

    void SetComponent(
        ModelEntityHandle entity,
        ModelComponentKind component,
        ModelComponentValue value);

    void AddTag(ModelEntityHandle entity, ModelTagKind tag);

    void RemoveTag(ModelEntityHandle entity, ModelTagKind tag);

    void AppendBuffer(ModelEntityHandle entity, int value);

    void RemoveBufferAt(ModelEntityHandle entity, int index);

    void ClearBuffer(ModelEntityHandle entity);

    IReadOnlyList<ModelEntityHandle> Query(in ModelQuery query);

    ModelEntityObservation Observe(ModelEntityHandle entity);

    void Playback(IReadOnlyList<ModelCommand> commands);
}
