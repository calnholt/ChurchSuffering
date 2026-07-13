namespace Crusaders30XX.ECS.Benchmarks.Benchmarking;

public sealed record BenchmarkArtifact(
    int SchemaVersion,
    string Runtime,
    DateTimeOffset CapturedAtUtc,
    string BuildConfiguration,
    BenchmarkRunConfiguration Configuration,
    BenchmarkMachineMetadata Machine,
    LegacyBaselineReference? LegacyBaseline,
    IReadOnlyList<BenchmarkResult> Benchmarks);

public sealed record BenchmarkRunConfiguration(
    int Seed,
    int WarmupBatches,
    int SampleBatches,
    int IterationsPerBatch,
    string HarnessOverheadPolicy);

public sealed record BenchmarkMachineMetadata(
    string MachineName,
    string OperatingSystem,
    string OperatingSystemArchitecture,
    string ProcessArchitecture,
    string Framework,
    int LogicalProcessorCount,
    long StopwatchFrequency);

public sealed record LegacyBaselineReference(
    int SchemaVersion,
    string Runtime,
    string BuildConfiguration,
    int FixedSeed,
    int WarmupFrames,
    int SampleFrames,
    IReadOnlyList<LegacyBaselineWorkloadReference> Workloads);

public sealed record LegacyBaselineWorkloadReference(
    string Name,
    int EntityCount,
    int ComponentCount,
    long ProcessedRows,
    long AllocatedBytes,
    double MedianMilliseconds,
    double P95Milliseconds);

public sealed record BenchmarkResult(
    string Name,
    string Category,
    int EntityCount,
    int ComponentCount,
    long OperationsPerSample,
    long ProcessedRowsPerSample,
    bool StableChecksum,
    MeasurementStatistics Raw,
    MeasurementStatistics HarnessOverhead,
    MeasurementStatistics Net);

public sealed record MeasurementStatistics(
    int SampleCount,
    double MedianNanoseconds,
    double P95Nanoseconds,
    double MaximumNanoseconds,
    long MedianAllocatedBytes,
    long MaximumAllocatedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    double MedianOperationsPerSecond);
