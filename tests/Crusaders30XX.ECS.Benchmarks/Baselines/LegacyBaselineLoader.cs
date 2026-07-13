using System.Text.Json;
using Crusaders30XX.ECS.Benchmarks.Benchmarking;

namespace Crusaders30XX.ECS.Benchmarks.Baselines;

public static class LegacyBaselineLoader
{
    public static LegacyPerformanceArtifact Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The legacy ECS baseline was not found.", fullPath);
        }

        using FileStream stream = File.OpenRead(fullPath);
        LegacyPerformanceArtifact? artifact = JsonSerializer.Deserialize<LegacyPerformanceArtifact>(
            stream,
            JsonOptions);
        if (artifact is null)
        {
            throw new InvalidDataException($"Legacy ECS baseline '{fullPath}' is empty.");
        }

        Validate(artifact, fullPath);
        return artifact;
    }

    public static LegacyBaselineReference ToReference(LegacyPerformanceArtifact artifact) => new(
        artifact.SchemaVersion,
        artifact.Runtime,
        artifact.BuildConfiguration,
        artifact.FixedSeed,
        artifact.WarmupFrames,
        artifact.SampleFrames,
        artifact.Workloads.Select(workload => new LegacyBaselineWorkloadReference(
            workload.Name,
            workload.EntityCount,
            workload.ComponentCount,
            workload.ProcessedRows,
            workload.AllocatedBytes,
            workload.CpuScopes.EcsUpdate.MedianMilliseconds,
            workload.CpuScopes.EcsUpdate.P95Milliseconds)).ToArray());

    public static string? FindDefaultPath(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "tests",
                "PerformanceBaselines",
                "legacy-ecs-initial.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void Validate(LegacyPerformanceArtifact artifact, string path)
    {
        if (artifact.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Legacy ECS baseline '{path}' uses unsupported schema version {artifact.SchemaVersion}; expected 1.");
        }

        if (!string.Equals(artifact.Runtime, "legacy-object-ecs", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Legacy ECS baseline '{path}' has runtime '{artifact.Runtime}', expected 'legacy-object-ecs'.");
        }

        if (!string.Equals(artifact.BuildConfiguration, "Release", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Legacy ECS baseline '{path}' was not captured in Release.");
        }

        if (artifact.SampleFrames <= 0 || artifact.WarmupFrames < 0)
        {
            throw new InvalidDataException(
                $"Legacy ECS baseline '{path}' has invalid warm-up or sample counts.");
        }

        if (artifact.Workloads is null || artifact.Workloads.Count == 0)
        {
            throw new InvalidDataException($"Legacy ECS baseline '{path}' contains no workloads.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (LegacyWorkloadResult workload in artifact.Workloads)
        {
            if (string.IsNullOrWhiteSpace(workload.Name) || !names.Add(workload.Name))
            {
                throw new InvalidDataException(
                    $"Legacy ECS baseline '{path}' contains an empty or duplicate workload name.");
            }

            if (workload.EntityCount < 0 ||
                workload.ComponentCount < 0 ||
                workload.ProcessedRows < 0 ||
                workload.AllocatedBytes < 0 ||
                workload.CpuScopes is null ||
                workload.CpuScopes.EcsUpdate is null ||
                workload.CpuScopes.EcsUpdate.SampleCount != artifact.SampleFrames)
            {
                throw new InvalidDataException(
                    $"Legacy ECS baseline '{path}' workload '{workload.Name}' has invalid counters.");
            }
        }
    }

    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

public sealed record LegacyPerformanceArtifact(
    int SchemaVersion,
    string Runtime,
    DateTimeOffset CapturedAtUtc,
    string BuildConfiguration,
    int FixedSeed,
    int WarmupFrames,
    int SampleFrames,
    string LogicalResolution,
    bool DiagnosticsClosed,
    bool ShadersEnabled,
    LegacyMachineMetadata Machine,
    IReadOnlyList<LegacyWorkloadResult> Workloads);

public sealed record LegacyMachineMetadata(
    string MachineName,
    string OperatingSystem,
    string OperatingSystemArchitecture,
    string ProcessArchitecture,
    string Framework,
    int LogicalProcessorCount);

public sealed record LegacyWorkloadResult(
    string Name,
    int EntityCount,
    int ComponentCount,
    int SystemCalls,
    int QueryCalls,
    long ProcessedRows,
    long AllocatedBytes,
    LegacyGcCounts GarbageCollections,
    LegacyCpuScopeResults CpuScopes);

public sealed record LegacyGcCounts(int Gen0, int Gen1, int Gen2);

public sealed record LegacyCpuScopeResults(
    LegacyScopeStatistics EcsUpdate,
    LegacyScopeStatistics CpuDrawSubmission);

public sealed record LegacyScopeStatistics(
    bool Supported,
    int SampleCount,
    double AverageMilliseconds,
    double MedianMilliseconds,
    double P95Milliseconds,
    double MaximumMilliseconds);
