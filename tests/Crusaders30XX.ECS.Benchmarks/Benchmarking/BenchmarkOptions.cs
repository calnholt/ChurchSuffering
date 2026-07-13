using System.Globalization;

namespace Crusaders30XX.ECS.Benchmarks.Benchmarking;

public sealed record BenchmarkOptions(
    int Seed,
    int WarmupBatches,
    int SampleBatches,
    int IterationsPerBatch,
    string? OutputPath,
    string? LegacyBaselinePath,
    bool AllowDebug)
{
    public static BenchmarkOptions Defaults => new(
        Seed: 1337,
        WarmupBatches: 8,
        SampleBatches: 21,
        IterationsPerBatch: 64,
        OutputPath: null,
        LegacyBaselinePath: null,
        AllowDebug: false);

    public static BenchmarkOptions Parse(string[] args)
    {
        var options = Defaults;
        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];
            options = argument switch
            {
                "--quick" => options with
                {
                    WarmupBatches = 2,
                    SampleBatches = 5,
                    IterationsPerBatch = 8,
                },
                "--allow-debug" => options with { AllowDebug = true },
                "--seed" => options with { Seed = ParsePositiveInt(args, ref i, argument, allowZero: true) },
                "--warmup" => options with { WarmupBatches = ParsePositiveInt(args, ref i, argument, allowZero: true) },
                "--samples" => options with { SampleBatches = ParsePositiveInt(args, ref i, argument) },
                "--iterations" => options with { IterationsPerBatch = ParsePositiveInt(args, ref i, argument) },
                "--output" => options with { OutputPath = ReadValue(args, ref i, argument) },
                "--legacy-baseline" => options with { LegacyBaselinePath = ReadValue(args, ref i, argument) },
                "--help" or "-h" => throw new BenchmarkHelpRequestedException(),
                _ => throw new ArgumentException($"Unknown benchmark argument '{argument}'."),
            };
        }

        return options;
    }

    private static int ParsePositiveInt(
        string[] args,
        ref int index,
        string argument,
        bool allowZero = false)
    {
        string value = ReadValue(args, ref index, argument);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ||
            parsed < (allowZero ? 0 : 1))
        {
            string range = allowZero ? "a non-negative integer" : "a positive integer";
            throw new ArgumentException($"{argument} requires {range}; received '{value}'.");
        }

        return parsed;
    }

    private static string ReadValue(string[] args, ref int index, string argument)
    {
        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ArgumentException($"{argument} requires a value.");
        }

        return args[index];
    }
}

public sealed class BenchmarkHelpRequestedException : Exception;
