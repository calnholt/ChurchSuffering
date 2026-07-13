namespace Crusaders30XX.ECS.Benchmarks.Benchmarking;

public interface IMicrobenchmark
{
    string Name { get; }

    string Category { get; }

    int EntityCount { get; }

    int ComponentCount { get; }

    long OperationsPerIteration { get; }

    long ProcessedRowsPerIteration { get; }

    void Initialize(int seed);

    long RunBatch(int iterations);
}
