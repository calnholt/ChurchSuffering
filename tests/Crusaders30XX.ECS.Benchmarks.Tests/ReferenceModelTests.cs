using Crusaders30XX.ECS.Benchmarks.Adapters;
using Crusaders30XX.ECS.Benchmarks.Model;
using Xunit;

namespace Crusaders30XX.ECS.Benchmarks.Tests;

public sealed class ReferenceModelTests
{
    [Theory]
    [InlineData(1337)]
    [InlineData(7331)]
    [InlineData(8675309)]
    public void Long_randomized_sequences_match_handle_remapped_subject(int seed)
    {
        var runner = new RandomizedModelRunner(
            new ReferenceModelWorld(),
            new RemappedModelWorld());

        RandomizedModelRunResult result = runner.Run(seed, batchCount: 1_000);

        Assert.Equal(seed, result.Seed);
        Assert.Equal(1_000, result.BatchCount);
        Assert.True(result.OperationCount >= 1_000);
        Assert.True(result.LogicalEntityCount > 0);
    }

    [Theory]
    [InlineData(1337)]
    [InlineData(7331)]
    public void Data_oriented_world_matches_reference_for_all_foundation_capabilities(int seed)
    {
        var runner = new RandomizedModelRunner(
            new ReferenceModelWorld(),
            new DataOrientedWorldModelAdapter());

        RandomizedModelRunResult result = runner.Run(seed, batchCount: 2_000);

        Assert.Equal(2_000, result.BatchCount);
        Assert.True(result.OperationCount >= 2_000);
        Assert.Equal(ModelOperationCoverage.All, result.Coverage);
    }

    [Fact]
    public void Generation_increases_and_stale_handle_stays_dead()
    {
        var world = new ReferenceModelWorld();
        ModelEntityHandle first = world.Create();
        world.Destroy(first);
        ModelEntityHandle replacement = world.Create();

        Assert.Equal(first.Index, replacement.Index);
        Assert.True(replacement.Generation > first.Generation);
        Assert.False(world.IsAlive(first));
        Assert.True(world.IsAlive(replacement));
    }

    [Fact]
    public void Playback_preserves_record_order()
    {
        var world = new ReferenceModelWorld();
        ModelEntityHandle entity = world.Create();
        world.Playback(
        [
            new ModelCommand(ModelCommandKind.AppendBuffer, entity, BufferValueOrIndex: 10),
            new ModelCommand(ModelCommandKind.AppendBuffer, entity, BufferValueOrIndex: 20),
            new ModelCommand(ModelCommandKind.RemoveBufferAt, entity, BufferValueOrIndex: 0),
        ]);

        Assert.Equal([20], world.Observe(entity).BufferContents);
    }

    [Fact]
    public void Divergence_reports_seed_batch_and_recent_operations()
    {
        var runner = new RandomizedModelRunner(
            new ReferenceModelWorld(),
            new FaultyObservationWorld());

        ModelDivergenceException exception = Assert.Throws<ModelDivergenceException>(
            () => runner.Run(seed: 42, batchCount: 10));

        Assert.Equal(42, exception.Seed);
        Assert.Contains("Seed=42", exception.Message);
        Assert.Contains("Recent operations", exception.Message);
    }

    private sealed class RemappedModelWorld : IModelWorld
    {
        private readonly ReferenceModelWorld inner = new();
        private readonly Dictionary<ModelEntityHandle, ModelEntityHandle> outerToInner = [];
        private readonly Dictionary<ModelEntityHandle, ModelEntityHandle> innerToOuter = [];

        public string Name => "remapped-test-world";
        public ModelWorldCapabilities Capabilities => ModelWorldCapabilities.All;

        public ModelEntityHandle Create()
        {
            ModelEntityHandle innerEntity = inner.Create();
            var outerEntity = new ModelEntityHandle(innerEntity.Index + 10_000, innerEntity.Generation + 10);
            outerToInner[outerEntity] = innerEntity;
            innerToOuter[innerEntity] = outerEntity;
            return outerEntity;
        }

        public bool IsAlive(ModelEntityHandle entity) =>
            outerToInner.TryGetValue(entity, out ModelEntityHandle innerEntity) && inner.IsAlive(innerEntity);

        public void Destroy(ModelEntityHandle entity) => inner.Destroy(ToInner(entity));
        public void Enable(ModelEntityHandle entity) => inner.Enable(ToInner(entity));
        public void Disable(ModelEntityHandle entity) => inner.Disable(ToInner(entity));
        public void AddComponent(ModelEntityHandle entity, ModelComponentKind component, ModelComponentValue value) => inner.AddComponent(ToInner(entity), component, value);
        public void RemoveComponent(ModelEntityHandle entity, ModelComponentKind component) => inner.RemoveComponent(ToInner(entity), component);
        public void SetComponent(ModelEntityHandle entity, ModelComponentKind component, ModelComponentValue value) => inner.SetComponent(ToInner(entity), component, value);
        public void AddTag(ModelEntityHandle entity, ModelTagKind tag) => inner.AddTag(ToInner(entity), tag);
        public void RemoveTag(ModelEntityHandle entity, ModelTagKind tag) => inner.RemoveTag(ToInner(entity), tag);
        public void AppendBuffer(ModelEntityHandle entity, int value) => inner.AppendBuffer(ToInner(entity), value);
        public void RemoveBufferAt(ModelEntityHandle entity, int index) => inner.RemoveBufferAt(ToInner(entity), index);
        public void ClearBuffer(ModelEntityHandle entity) => inner.ClearBuffer(ToInner(entity));

        public IReadOnlyList<ModelEntityHandle> Query(in ModelQuery query) =>
            inner.Query(query).Select(entity => innerToOuter[entity]).ToArray();

        public ModelEntityObservation Observe(ModelEntityHandle entity)
        {
            ModelEntityObservation observation = inner.Observe(ToInner(entity));
            return observation with { Generation = entity.Generation };
        }

        public void Playback(IReadOnlyList<ModelCommand> commands) =>
            inner.Playback(commands.Select(command => command with { Entity = ToInner(command.Entity) }).ToArray());

        private ModelEntityHandle ToInner(ModelEntityHandle entity) =>
            outerToInner.TryGetValue(entity, out ModelEntityHandle innerEntity)
                ? innerEntity
                : entity;
    }

    private sealed class FaultyObservationWorld : IModelWorld
    {
        private readonly ReferenceModelWorld inner = new();
        public string Name => "faulty-observation-world";
        public ModelWorldCapabilities Capabilities => ModelWorldCapabilities.All;
        public ModelEntityHandle Create() => inner.Create();
        public bool IsAlive(ModelEntityHandle entity) => inner.IsAlive(entity);
        public void Destroy(ModelEntityHandle entity) => inner.Destroy(entity);
        public void Enable(ModelEntityHandle entity) => inner.Enable(entity);
        public void Disable(ModelEntityHandle entity) => inner.Disable(entity);
        public void AddComponent(ModelEntityHandle entity, ModelComponentKind component, ModelComponentValue value) => inner.AddComponent(entity, component, value);
        public void RemoveComponent(ModelEntityHandle entity, ModelComponentKind component) => inner.RemoveComponent(entity, component);
        public void SetComponent(ModelEntityHandle entity, ModelComponentKind component, ModelComponentValue value) => inner.SetComponent(entity, component, value);
        public void AddTag(ModelEntityHandle entity, ModelTagKind tag) => inner.AddTag(entity, tag);
        public void RemoveTag(ModelEntityHandle entity, ModelTagKind tag) => inner.RemoveTag(entity, tag);
        public void AppendBuffer(ModelEntityHandle entity, int value) => inner.AppendBuffer(entity, value);
        public void RemoveBufferAt(ModelEntityHandle entity, int index) => inner.RemoveBufferAt(entity, index);
        public void ClearBuffer(ModelEntityHandle entity) => inner.ClearBuffer(entity);
        public IReadOnlyList<ModelEntityHandle> Query(in ModelQuery query) => inner.Query(query);
        public void Playback(IReadOnlyList<ModelCommand> commands) => inner.Playback(commands);

        public ModelEntityObservation Observe(ModelEntityHandle entity)
        {
            ModelEntityObservation observation = inner.Observe(entity);
            return observation.Alive
                ? observation with { Enabled = !observation.Enabled }
                : observation;
        }
    }
}
