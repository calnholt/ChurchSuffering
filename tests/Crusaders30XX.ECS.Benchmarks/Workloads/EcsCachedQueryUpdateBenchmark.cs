using Crusaders30XX.ECS.Benchmarks.Adapters;
using Crusaders30XX.ECS.Benchmarks.Benchmarking;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.Benchmarks.Workloads;

/// <summary>
/// Canonical ECS-013 cached chunk/span query workload. Query construction and archetype
/// matching occur during initialization, outside every measured batch.
/// </summary>
public sealed class EcsCachedQueryUpdateBenchmark : IMicrobenchmark
{
    private const int Rows = 10_000;
    private readonly int returnedComponentCount;
    private World world = null!;
    private Query<HarnessPosition, HarnessVelocity>? query2;
    private Query<HarnessPosition, HarnessVelocity, HarnessHealth, HarnessAuxiliary>? query4;

    public EcsCachedQueryUpdateBenchmark(int returnedComponentCount)
    {
        if (returnedComponentCount is not (2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(returnedComponentCount));
        }

        this.returnedComponentCount = returnedComponentCount;
    }

    public string Name => $"ecs-013-cached-query-{returnedComponentCount}-component-update";
    public string Category => "data-oriented-cached-query";
    public int EntityCount => Rows;
    public int ComponentCount => Rows * returnedComponentCount;
    public long OperationsPerIteration => Rows;
    public long ProcessedRowsPerIteration => Rows;

    public void Initialize(int seed)
    {
        world = new World(FoundationRegistryFactory.Create(), Rows);
        var random = new Random(seed);
        var bundle = new SpawnBundle(returnedComponentCount);
        for (var index = 0; index < Rows; index++)
        {
            bundle.Clear();
            var position = new HarnessPosition
            {
                X = random.Next(-10_000, 10_001),
                Y = random.Next(-10_000, 10_001),
            };
            var velocity = new HarnessVelocity
            {
                X = random.Next(-10, 11),
                Y = random.Next(-10, 11),
            };
            bundle.Add(in position);
            bundle.Add(in velocity);
            if (returnedComponentCount == 4)
            {
                var health = new HarnessHealth { Current = random.Next(10, 80), Maximum = 80 };
                var auxiliary = new HarnessAuxiliary { First = index, Second = index % 17 };
                bundle.Add(in health);
                bundle.Add(in auxiliary);
            }

            world.Create(in bundle);
        }

        var filter = new QueryFilter(DebugName: Name);
        if (returnedComponentCount == 2)
        {
            query2 = world.Query<HarnessPosition, HarnessVelocity>(filter);
            query4 = null;
        }
        else
        {
            query4 = world.Query<HarnessPosition, HarnessVelocity, HarnessHealth, HarnessAuxiliary>(filter);
            query2 = null;
        }
    }

    public long RunBatch(int iterations)
    {
        return returnedComponentCount == 2
            ? RunTwoComponent(iterations)
            : RunFourComponent(iterations);
    }

    private long RunTwoComponent(int iterations)
    {
        long checksum = 0;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            foreach (QueryChunk<HarnessPosition, HarnessVelocity> chunk in query2!)
            {
                Span<HarnessPosition> positions = chunk.Component1;
                Span<HarnessVelocity> velocities = chunk.Component2;
                foreach (int row in chunk.Rows)
                {
                    int originalX = positions[row].X;
                    int originalY = positions[row].Y;
                    positions[row].X += velocities[row].X;
                    positions[row].Y += velocities[row].Y;
                    checksum = unchecked(checksum + positions[row].X + positions[row].Y);
                    positions[row].X = originalX;
                    positions[row].Y = originalY;
                }
            }
        }

        return checksum;
    }

    private long RunFourComponent(int iterations)
    {
        long checksum = 0;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            foreach (QueryChunk<HarnessPosition, HarnessVelocity, HarnessHealth, HarnessAuxiliary> chunk in query4!)
            {
                Span<HarnessPosition> positions = chunk.Component1;
                Span<HarnessVelocity> velocities = chunk.Component2;
                Span<HarnessHealth> health = chunk.Component3;
                Span<HarnessAuxiliary> auxiliary = chunk.Component4;
                foreach (int row in chunk.Rows)
                {
                    int originalX = positions[row].X;
                    int originalY = positions[row].Y;
                    int originalHealth = health[row].Current;
                    int originalAuxiliary = auxiliary[row].Second;
                    positions[row].X += velocities[row].X;
                    positions[row].Y += velocities[row].Y;
                    health[row].Current = Math.Clamp(
                        health[row].Current + ((auxiliary[row].First & 1) == 0 ? 1 : -1),
                        0,
                        health[row].Maximum);
                    auxiliary[row].Second++;
                    checksum = unchecked(
                        checksum + positions[row].X + positions[row].Y + health[row].Current + auxiliary[row].Second);
                    positions[row].X = originalX;
                    positions[row].Y = originalY;
                    health[row].Current = originalHealth;
                    auxiliary[row].Second = originalAuxiliary;
                }
            }
        }

        return checksum;
    }
}
