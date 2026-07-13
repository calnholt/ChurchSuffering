using System.Text;

namespace Crusaders30XX.ECS.Benchmarks.Model;

public sealed record RandomizedModelRunResult(
    int Seed,
    int BatchCount,
    int OperationCount,
    int LogicalEntityCount,
    ModelOperationCoverage Coverage);

public sealed class RandomizedModelRunner
{
    private static readonly ModelQuery[] ComparisonQueries =
    [
        new(ModelTypeMask.Position, ModelTypeMask.None, ModelTypeMask.None),
        new(
            ModelTypeMask.None,
            ModelTypeMask.Velocity | ModelTypeMask.Health,
            ModelTypeMask.PrimaryTag),
        new(
            ModelTypeMask.None,
            ModelTypeMask.None,
            ModelTypeMask.SecondaryTag,
            IncludeDisabled: true),
    ];

    private readonly IModelWorld reference;
    private readonly IModelWorld subject;
    private readonly Dictionary<ScenarioEntityId, EntityPair> entities = [];
    private readonly Queue<string> recentOperations = [];
    private readonly GenerationTracker referenceGenerations = new();
    private readonly GenerationTracker subjectGenerations = new();
    private readonly ModelWorldCapabilities capabilities;
    private int nextLogicalEntity = 1;
    private int operationCount;
    private ModelOperationCoverage coverage;

    public RandomizedModelRunner(IModelWorld reference, IModelWorld subject)
    {
        this.reference = reference ?? throw new ArgumentNullException(nameof(reference));
        this.subject = subject ?? throw new ArgumentNullException(nameof(subject));
        if (ReferenceEquals(reference, subject))
        {
            throw new ArgumentException("The reference and subject worlds must be separate instances.");
        }

        capabilities = reference.Capabilities & subject.Capabilities;
        ModelWorldCapabilities required =
            ModelWorldCapabilities.EntityLifecycle |
            ModelWorldCapabilities.ComponentsAndTags |
            ModelWorldCapabilities.EnableDisable;
        if ((capabilities & required) != required)
        {
            throw new ArgumentException(
                $"Both model worlds must support the core capabilities {required}.");
        }
    }

    public RandomizedModelRunResult Run(int seed, int batchCount, int maximumOperationsPerBatch = 8)
    {
        if (batchCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchCount));
        }

        if (maximumOperationsPerBatch <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOperationsPerBatch));
        }

        var random = new Random(seed);
        for (int batch = 0; batch < batchCount; batch++)
        {
            int operationsInBatch = random.Next(1, maximumOperationsPerBatch + 1);
            for (int operation = 0; operation < operationsInBatch; operation++)
            {
                ApplyRandomOperation(random, seed, batch);
            }

            CompareWorlds(seed, batch);
        }

        return new RandomizedModelRunResult(seed, batchCount, operationCount, entities.Count, coverage);
    }

    private void ApplyRandomOperation(Random random, int seed, int batch)
    {
        ScenarioEntityId[] live = entities
            .Where(pair => reference.IsAlive(pair.Value.Reference))
            .Select(pair => pair.Key)
            .ToArray();

        if (live.Length == 0 || random.Next(100) < 22)
        {
            Create(seed, batch);
            return;
        }

        ScenarioEntityId logical = live[random.Next(live.Length)];
        EntityPair pair = entities[logical];
        ModelEntityObservation observation = reference.Observe(pair.Reference);
        int choice = random.Next(100);
        try
        {
            switch (choice)
            {
                case < 10:
                    coverage |= ModelOperationCoverage.Lifecycle;
                    Record($"destroy L{logical.Value}");
                    reference.Destroy(pair.Reference);
                    subject.Destroy(pair.Subject);
                    break;
                case < 20:
                    Record($"{(observation.Enabled ? "disable" : "enable")} L{logical.Value}");
                    SetEnabled(pair, observation.Enabled);
                    break;
                case < 52:
                    MutateComponent(random, logical, pair, observation);
                    break;
                case < 67:
                    MutateTag(random, logical, pair, observation);
                    break;
                case < 87:
                    if (capabilities.HasFlag(ModelWorldCapabilities.DynamicBuffers))
                    {
                        MutateBuffer(random, logical, pair, observation);
                    }
                    else
                    {
                        MutateComponent(random, logical, pair, observation);
                    }
                    break;
                default:
                    if (capabilities.HasFlag(ModelWorldCapabilities.CommandPlayback))
                    {
                        PlaybackOrderedMutation(random, logical, pair, observation);
                    }
                    else
                    {
                        MutateTag(random, logical, pair, observation);
                    }
                    break;
            }

            operationCount++;
        }
        catch (Exception exception) when (exception is not ModelDivergenceException)
        {
            throw Divergence(seed, batch, $"Operation failed: {exception.Message}", exception);
        }
    }

    private void Create(int seed, int batch)
    {
        var logical = new ScenarioEntityId(nextLogicalEntity++);
        try
        {
            ModelEntityHandle referenceEntity = reference.Create();
            ModelEntityHandle subjectEntity = subject.Create();
            referenceGenerations.ObserveCreate(referenceEntity, reference.Name);
            subjectGenerations.ObserveCreate(subjectEntity, subject.Name);
            entities.Add(logical, new EntityPair(referenceEntity, subjectEntity));
            coverage |= ModelOperationCoverage.Lifecycle;
            Record($"create L{logical.Value} ref={referenceEntity} subject={subjectEntity}");
            operationCount++;
        }
        catch (Exception exception) when (exception is not ModelDivergenceException)
        {
            throw Divergence(seed, batch, $"Create failed: {exception.Message}", exception);
        }
    }

    private void SetEnabled(EntityPair pair, bool wasEnabled)
    {
        coverage |= ModelOperationCoverage.EnableDisable;
        if (wasEnabled)
        {
            reference.Disable(pair.Reference);
            subject.Disable(pair.Subject);
        }
        else
        {
            reference.Enable(pair.Reference);
            subject.Enable(pair.Subject);
        }
    }

    private void MutateComponent(
        Random random,
        ScenarioEntityId logical,
        EntityPair pair,
        ModelEntityObservation observation)
    {
        coverage |= ModelOperationCoverage.Components;
        ModelComponentKind component = (ModelComponentKind)random.Next(3);
        ModelTypeMask mask = ToMask(component);
        var value = new ModelComponentValue(random.Next(-10_000, 10_001), random.Next(-10_000, 10_001));
        if ((observation.Signature & mask) == 0)
        {
            Record($"add {component}={value} L{logical.Value}");
            reference.AddComponent(pair.Reference, component, value);
            subject.AddComponent(pair.Subject, component, value);
        }
        else if (random.Next(3) == 0)
        {
            Record($"remove {component} L{logical.Value}");
            reference.RemoveComponent(pair.Reference, component);
            subject.RemoveComponent(pair.Subject, component);
        }
        else
        {
            Record($"set {component}={value} L{logical.Value}");
            reference.SetComponent(pair.Reference, component, value);
            subject.SetComponent(pair.Subject, component, value);
        }
    }

    private void MutateTag(
        Random random,
        ScenarioEntityId logical,
        EntityPair pair,
        ModelEntityObservation observation)
    {
        coverage |= ModelOperationCoverage.Tags;
        ModelTagKind tag = (ModelTagKind)random.Next(2);
        ModelTypeMask mask = ToMask(tag);
        if ((observation.Signature & mask) == 0)
        {
            Record($"add-tag {tag} L{logical.Value}");
            reference.AddTag(pair.Reference, tag);
            subject.AddTag(pair.Subject, tag);
        }
        else
        {
            Record($"remove-tag {tag} L{logical.Value}");
            reference.RemoveTag(pair.Reference, tag);
            subject.RemoveTag(pair.Subject, tag);
        }
    }

    private void MutateBuffer(
        Random random,
        ScenarioEntityId logical,
        EntityPair pair,
        ModelEntityObservation observation)
    {
        coverage |= ModelOperationCoverage.DynamicBuffers;
        if (observation.BufferContents.Count == 0 || random.Next(3) == 0)
        {
            int value = random.Next(-10_000, 10_001);
            Record($"buffer-append {value} L{logical.Value}");
            reference.AppendBuffer(pair.Reference, value);
            subject.AppendBuffer(pair.Subject, value);
        }
        else if (random.Next(4) == 0)
        {
            Record($"buffer-clear L{logical.Value}");
            reference.ClearBuffer(pair.Reference);
            subject.ClearBuffer(pair.Subject);
        }
        else
        {
            int index = random.Next(observation.BufferContents.Count);
            Record($"buffer-remove [{index}] L{logical.Value}");
            reference.RemoveBufferAt(pair.Reference, index);
            subject.RemoveBufferAt(pair.Subject, index);
        }
    }

    private void PlaybackOrderedMutation(
        Random random,
        ScenarioEntityId logical,
        EntityPair pair,
        ModelEntityObservation observation)
    {
        coverage |= ModelOperationCoverage.CommandPlayback;
        int commandKind = random.Next(capabilities.HasFlag(ModelWorldCapabilities.DynamicBuffers) ? 4 : 3);
        switch (commandKind)
        {
            case 0:
                PlaybackToggle(logical, pair, observation);
                break;
            case 1:
                PlaybackComponent(random, logical, pair, observation);
                break;
            case 2:
                PlaybackTag(logical, pair, observation);
                break;
            default:
                PlaybackBuffer(random, logical, pair, observation);
                break;
        }
    }

    private void PlaybackToggle(
        ScenarioEntityId logical,
        EntityPair pair,
        ModelEntityObservation observation)
    {
        ModelCommandKind first = observation.Enabled ? ModelCommandKind.Disable : ModelCommandKind.Enable;
        ModelCommandKind second = observation.Enabled ? ModelCommandKind.Enable : ModelCommandKind.Disable;
        Record($"playback-enabled {first},{second} L{logical.Value}");
        reference.Playback(CreateToggleCommands(pair.Reference, first, second));
        subject.Playback(CreateToggleCommands(pair.Subject, first, second));
    }

    private void PlaybackComponent(
        Random random,
        ScenarioEntityId logical,
        EntityPair pair,
        ModelEntityObservation observation)
    {
        coverage |= ModelOperationCoverage.Components;
        ModelComponentKind component = (ModelComponentKind)random.Next(3);
        var firstValue = new ModelComponentValue(random.Next(-1000, 1001), random.Next(-1000, 1001));
        var secondValue = new ModelComponentValue(random.Next(-1000, 1001), random.Next(-1000, 1001));
        bool exists = (observation.Signature & ToMask(component)) != 0;
        Record($"playback-component {component} exists={exists} L{logical.Value}");
        reference.Playback(CreateComponentCommands(pair.Reference, component, firstValue, secondValue, exists));
        subject.Playback(CreateComponentCommands(pair.Subject, component, firstValue, secondValue, exists));
    }

    private void PlaybackTag(
        ScenarioEntityId logical,
        EntityPair pair,
        ModelEntityObservation observation)
    {
        coverage |= ModelOperationCoverage.Tags;
        ModelTagKind tag = (logical.Value & 1) == 0 ? ModelTagKind.Primary : ModelTagKind.Secondary;
        bool exists = (observation.Signature & ToMask(tag)) != 0;
        ModelCommandKind first = exists ? ModelCommandKind.RemoveTag : ModelCommandKind.AddTag;
        ModelCommandKind second = exists ? ModelCommandKind.AddTag : ModelCommandKind.RemoveTag;
        Record($"playback-tag {first},{second} {tag} L{logical.Value}");
        reference.Playback(CreateTagCommands(pair.Reference, tag, first, second));
        subject.Playback(CreateTagCommands(pair.Subject, tag, first, second));
    }

    private void PlaybackBuffer(
        Random random,
        ScenarioEntityId logical,
        EntityPair pair,
        ModelEntityObservation observation)
    {
        coverage |= ModelOperationCoverage.DynamicBuffers;
        int firstValue = random.Next(-1000, 1001);
        int secondValue = random.Next(-1000, 1001);
        int appendedIndex = observation.BufferContents.Count;
        Record($"playback-buffer append {firstValue},{secondValue},remove [{appendedIndex}] L{logical.Value}");
        reference.Playback(CreateBufferCommands(pair.Reference, firstValue, secondValue, appendedIndex));
        subject.Playback(CreateBufferCommands(pair.Subject, firstValue, secondValue, appendedIndex));
    }

    private static ModelCommand[] CreateToggleCommands(
        ModelEntityHandle entity,
        ModelCommandKind first,
        ModelCommandKind second) =>
    [
        new ModelCommand(first, entity),
        new ModelCommand(second, entity),
    ];

    private static ModelCommand[] CreateComponentCommands(
        ModelEntityHandle entity,
        ModelComponentKind component,
        ModelComponentValue firstValue,
        ModelComponentValue secondValue,
        bool exists) =>
    [
        new ModelCommand(
            exists ? ModelCommandKind.SetComponent : ModelCommandKind.AddComponent,
            entity,
            component,
            firstValue),
        new ModelCommand(ModelCommandKind.SetComponent, entity, component, secondValue),
    ];

    private static ModelCommand[] CreateTagCommands(
        ModelEntityHandle entity,
        ModelTagKind tag,
        ModelCommandKind first,
        ModelCommandKind second) =>
    [
        new ModelCommand(first, entity, Tag: tag),
        new ModelCommand(second, entity, Tag: tag),
    ];

    private static ModelCommand[] CreateBufferCommands(
        ModelEntityHandle entity,
        int firstValue,
        int secondValue,
        int appendedIndex) =>
    [
        new ModelCommand(ModelCommandKind.AppendBuffer, entity, BufferValueOrIndex: firstValue),
        new ModelCommand(ModelCommandKind.AppendBuffer, entity, BufferValueOrIndex: secondValue),
        new ModelCommand(ModelCommandKind.RemoveBufferAt, entity, BufferValueOrIndex: appendedIndex),
    ];

    private void CompareWorlds(int seed, int batch)
    {
        try
        {
            foreach ((ScenarioEntityId logical, EntityPair pair) in entities.OrderBy(pair => pair.Key.Value))
            {
                bool referenceAlive = reference.IsAlive(pair.Reference);
                bool subjectAlive = subject.IsAlive(pair.Subject);
                if (referenceAlive != subjectAlive)
                {
                    throw new InvalidOperationException(
                        $"L{logical.Value} alive mismatch: reference={referenceAlive}, subject={subjectAlive}.");
                }

                ModelEntityObservation expected = reference.Observe(pair.Reference);
                ModelEntityObservation actual = subject.Observe(pair.Subject);
                AssertEquivalent(logical, expected, actual);
            }

            if (capabilities.HasFlag(ModelWorldCapabilities.Queries))
            {
                coverage |= ModelOperationCoverage.Queries;
                foreach (ModelQuery query in ComparisonQueries)
                {
                    int[] expected = QueryLogicalIds(reference, query, useReferenceHandles: true);
                    int[] actual = QueryLogicalIds(subject, query, useReferenceHandles: false);
                    if (!expected.SequenceEqual(actual))
                    {
                        throw new InvalidOperationException(
                            $"Query {query} mismatch: reference=[{string.Join(',', expected)}], subject=[{string.Join(',', actual)}].");
                    }
                }
            }
        }
        catch (Exception exception) when (exception is not ModelDivergenceException)
        {
            throw Divergence(seed, batch, exception.Message, exception);
        }
    }

    private int[] QueryLogicalIds(IModelWorld world, ModelQuery query, bool useReferenceHandles)
    {
        var lookup = new Dictionary<ModelEntityHandle, int>();
        foreach ((ScenarioEntityId logical, EntityPair pair) in entities)
        {
            ModelEntityHandle handle = useReferenceHandles ? pair.Reference : pair.Subject;
            if (world.IsAlive(handle))
            {
                lookup.Add(handle, logical.Value);
            }
        }

        return world.Query(query)
            .Select(handle => lookup.TryGetValue(handle, out int logical)
                ? logical
                : throw new InvalidOperationException(
                    $"{world.Name} query returned unknown or duplicate handle {handle}."))
            .Order()
            .ToArray();
    }

    private static void AssertEquivalent(
        ScenarioEntityId logical,
        ModelEntityObservation expected,
        ModelEntityObservation actual)
    {
        // Numeric handle generations are validated independently because free-index
        // selection order is intentionally not part of the frozen world contract.
        bool equal = expected.Alive == actual.Alive &&
                     expected.Enabled == actual.Enabled &&
                     expected.Signature == actual.Signature &&
                     expected.Position == actual.Position &&
                     expected.Velocity == actual.Velocity &&
                     expected.Health == actual.Health &&
                     expected.BufferContents.SequenceEqual(actual.BufferContents);
        if (!equal)
        {
            throw new InvalidOperationException(
                $"L{logical.Value} state mismatch. Reference={Format(expected)} Subject={Format(actual)}");
        }
    }

    private void Record(string operation)
    {
        recentOperations.Enqueue(operation);
        while (recentOperations.Count > 32)
        {
            recentOperations.Dequeue();
        }
    }

    private ModelDivergenceException Divergence(
        int seed,
        int batch,
        string reason,
        Exception innerException)
    {
        var message = new StringBuilder()
            .Append("Randomized ECS model divergence. Seed=")
            .Append(seed)
            .Append(", batch=")
            .Append(batch)
            .Append(", operation=")
            .Append(operationCount)
            .Append(". ")
            .Append(reason)
            .AppendLine()
            .AppendLine("Recent operations:");
        foreach (string operation in recentOperations)
        {
            message.Append("  ").AppendLine(operation);
        }

        return new ModelDivergenceException(seed, batch, operationCount, message.ToString(), innerException);
    }

    private static string Format(ModelEntityObservation observation) =>
        $"alive={observation.Alive},enabled={observation.Enabled},signature={observation.Signature}," +
        $"position={observation.Position},velocity={observation.Velocity},health={observation.Health}," +
        $"buffer=[{string.Join(',', observation.BufferContents)}]";

    private static ModelTypeMask ToMask(ModelComponentKind component) =>
        (ModelTypeMask)(1 << (int)component);

    private static ModelTypeMask ToMask(ModelTagKind tag) =>
        (ModelTypeMask)(1 << (3 + (int)tag));

    private readonly record struct EntityPair(
        ModelEntityHandle Reference,
        ModelEntityHandle Subject);

    private sealed class GenerationTracker
    {
        private readonly Dictionary<int, int> highestGenerationByIndex = [];
        private readonly HashSet<ModelEntityHandle> activeHandles = [];

        public void ObserveCreate(ModelEntityHandle entity, string worldName)
        {
            if (entity.IsNull || entity.Generation <= 0)
            {
                throw new InvalidOperationException(
                    $"{worldName} created invalid entity handle {entity}.");
            }

            if (!activeHandles.Add(entity))
            {
                throw new InvalidOperationException(
                    $"{worldName} returned duplicate live entity handle {entity}.");
            }

            if (highestGenerationByIndex.TryGetValue(entity.Index, out int previous) &&
                entity.Generation <= previous)
            {
                throw new InvalidOperationException(
                    $"{worldName} reused entity index {entity.Index} without increasing generation " +
                    $"(previous={previous}, current={entity.Generation}).");
            }

            highestGenerationByIndex[entity.Index] = entity.Generation;
            activeHandles.RemoveWhere(handle =>
                handle.Index == entity.Index && handle.Generation != entity.Generation);
        }
    }
}

public sealed class ModelDivergenceException : Exception
{
    public ModelDivergenceException(
        int seed,
        int batch,
        int operation,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Seed = seed;
        Batch = batch;
        Operation = operation;
    }

    public int Seed { get; }
    public int Batch { get; }
    public int Operation { get; }
}
