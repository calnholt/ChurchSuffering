using System.Text.Json;
using Crusaders30XX.ECS.Benchmarks.Baselines;
using Crusaders30XX.ECS.Benchmarks.Benchmarking;
using Crusaders30XX.ECS.Benchmarks.Workloads;

try
{
    BenchmarkOptions options = BenchmarkOptions.Parse(args);
    string? baselinePath = options.LegacyBaselinePath ??
        LegacyBaselineLoader.FindDefaultPath(Environment.CurrentDirectory) ??
        LegacyBaselineLoader.FindDefaultPath(AppContext.BaseDirectory);
    LegacyBaselineReference? baseline = baselinePath is null
        ? null
        : LegacyBaselineLoader.ToReference(LegacyBaselineLoader.Load(baselinePath));

    IMicrobenchmark[] benchmarks =
    [
        new ContiguousArrayUpdateBenchmark(returnedComponentCount: 2),
        new ContiguousArrayUpdateBenchmark(returnedComponentCount: 4),
        new Ecs010StorageUpdateBenchmark(returnedComponentCount: 2),
        new Ecs010StorageUpdateBenchmark(returnedComponentCount: 4),
        new EcsCachedQueryUpdateBenchmark(returnedComponentCount: 2),
        new EcsCachedQueryUpdateBenchmark(returnedComponentCount: 4),
    ];
    BenchmarkArtifact artifact = new BenchmarkRunner(options).Run(benchmarks, baseline);
    string json = JsonSerializer.Serialize(artifact, ProgramSerialization.JsonOptions);

    if (string.IsNullOrWhiteSpace(options.OutputPath))
    {
        Console.WriteLine(json);
    }
    else
    {
        string fullPath = Path.GetFullPath(options.OutputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, json + Environment.NewLine);
        Console.Error.WriteLine($"ECS microbenchmark JSON: {fullPath}");
    }

    return 0;
}
catch (BenchmarkHelpRequestedException)
{
    PrintHelp();
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        Crusaders30XX data-oriented ECS microbenchmark harness

        Run with: dotnet run --project tests/Crusaders30XX.ECS.Benchmarks -c Release -- [options]

          --output <path>           Write machine-readable JSON; stdout is used by default.
          --legacy-baseline <path>  Load and validate an ECS-000 schema-v1 artifact.
          --seed <integer>          Deterministic initialization seed (default 1337).
          --warmup <count>          Warm-up batches (default 8).
          --samples <count>         Measured batches (default 21).
          --iterations <count>      Outer iterations per batch (default 64).
          --quick                   Use small smoke-verification counts.
          --allow-debug             Permit Debug only while developing the harness.
          --help                    Show this help.
        """);
}

internal static class ProgramSerialization
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
