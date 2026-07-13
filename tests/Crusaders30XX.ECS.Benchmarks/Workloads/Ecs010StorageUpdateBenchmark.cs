using Crusaders30XX.ECS.Benchmarks.Adapters;
using Crusaders30XX.ECS.Benchmarks.Benchmarking;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.Benchmarks.Workloads;

/// <summary>
/// Measures ECS-010's stable direct storage API. It deliberately does not stand in for
/// the cached chunk/span query loop owned by ECS-013.
/// </summary>
public sealed class Ecs010StorageUpdateBenchmark : IMicrobenchmark
{
    private const int Rows = 10_000;
    private readonly int returnedComponentCount;
    private World world = null!;
    private EntityId[] entities = [];

    public Ecs010StorageUpdateBenchmark(int returnedComponentCount)
    {
        if (returnedComponentCount is not (2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(returnedComponentCount));
        }

        this.returnedComponentCount = returnedComponentCount;
    }

    public string Name => $"ecs-010-direct-storage-{returnedComponentCount}-component-update";
    public string Category => "ecs-010-storage-api";
    public int EntityCount => Rows;
    public int ComponentCount => Rows * returnedComponentCount;
    public long OperationsPerIteration => Rows;
    public long ProcessedRowsPerIteration => Rows;

    public void Initialize(int seed)
    {
        world = new World(FoundationRegistryFactory.Create(), Rows);
        entities = new EntityId[Rows];
        var random = new Random(seed);
        var bundle = new SpawnBundle(returnedComponentCount);
        for (int i = 0; i < Rows; i++)
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
                var auxiliary = new HarnessAuxiliary { First = i, Second = i % 17 };
                bundle.Add(in health);
                bundle.Add(in auxiliary);
            }

            entities[i] = world.Create(in bundle);
        }
    }

    public long RunBatch(int iterations)
    {
        long checksum = 0;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                EntityId entity = entities[i];
                ref HarnessPosition position = ref world.Get<HarnessPosition>(entity);
                ref HarnessVelocity velocity = ref world.Get<HarnessVelocity>(entity);
                int originalX = position.X;
                int originalY = position.Y;
                position.X += velocity.X;
                position.Y += velocity.Y;
                if (returnedComponentCount == 4)
                {
                    ref HarnessHealth health = ref world.Get<HarnessHealth>(entity);
                    ref HarnessAuxiliary auxiliary = ref world.Get<HarnessAuxiliary>(entity);
                    int originalHealth = health.Current;
                    health.Current = Math.Clamp(
                        health.Current + ((auxiliary.First & 1) == 0 ? 1 : -1),
                        0,
                        health.Maximum);
                    checksum = unchecked(checksum + position.X + position.Y + health.Current + auxiliary.Second);
                    health.Current = originalHealth;
                }
                else
                {
                    checksum = unchecked(checksum + position.X + position.Y);
                }

                position.X = originalX;
                position.Y = originalY;
            }
        }

        return checksum;
    }
}
