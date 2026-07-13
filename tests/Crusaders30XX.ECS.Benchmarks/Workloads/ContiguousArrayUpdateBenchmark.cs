using Crusaders30XX.ECS.Benchmarks.Benchmarking;

namespace Crusaders30XX.ECS.Benchmarks.Workloads;

/// <summary>
/// Validates the harness with a predictable allocation-free array loop. This is not a
/// data-oriented ECS result and must not be used for migration throughput gates.
/// </summary>
public sealed class ContiguousArrayUpdateBenchmark : IMicrobenchmark
{
    private const int Rows = 10_000;
    private readonly int returnedComponentCount;
    private float[] x = [];
    private float[] y = [];
    private float[] dx = [];
    private float[] dy = [];
    private int[] hp = [];
    private int[] maximumHp = [];

    public ContiguousArrayUpdateBenchmark(int returnedComponentCount)
    {
        if (returnedComponentCount is not (2 or 4))
        {
            throw new ArgumentOutOfRangeException(
                nameof(returnedComponentCount),
                "Only the canonical two- and four-component validation loops are supported.");
        }

        this.returnedComponentCount = returnedComponentCount;
    }

    public string Name => $"harness-validation-contiguous-{returnedComponentCount}-component-update";
    public string Category => "harness-validation";
    public int EntityCount => Rows;
    public int ComponentCount => Rows * returnedComponentCount;
    public long OperationsPerIteration => Rows;
    public long ProcessedRowsPerIteration => Rows;

    public void Initialize(int seed)
    {
        var random = new Random(seed);
        x = new float[Rows];
        y = new float[Rows];
        dx = new float[Rows];
        dy = new float[Rows];
        hp = returnedComponentCount == 4 ? new int[Rows] : [];
        maximumHp = returnedComponentCount == 4 ? new int[Rows] : [];
        for (int i = 0; i < Rows; i++)
        {
            x[i] = random.NextSingle() * 1920f;
            y[i] = random.NextSingle() * 1080f;
            dx[i] = random.NextSingle() - 0.5f;
            dy[i] = random.NextSingle() - 0.5f;
            if (returnedComponentCount == 4)
            {
                hp[i] = random.Next(10, 80);
                maximumHp[i] = 80;
            }
        }
    }

    public long RunBatch(int iterations)
    {
        long checksum = 0;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int i = 0; i < Rows; i++)
            {
                // Apply and undo a deterministic update so every sampled batch starts
                // from identical data without a measured reset pass.
                float originalX = x[i];
                float originalY = y[i];
                int originalHp = hp.GetValueOrDefault(i);
                x[i] = originalX + dx[i];
                y[i] = originalY + dy[i];
                if (returnedComponentCount == 4)
                {
                    int delta = (i & 1) == 0 ? 1 : -1;
                    hp[i] = Math.Clamp(hp[i] + delta, 0, maximumHp[i]);
                }

                checksum = unchecked(checksum + BitConverter.SingleToInt32Bits(x[i]) + hp.GetValueOrDefault(i));
                x[i] = originalX;
                y[i] = originalY;
                if (returnedComponentCount == 4)
                {
                    hp[i] = originalHp;
                }
            }
        }

        return checksum;
    }
}

internal static class ArrayExtensions
{
    public static int GetValueOrDefault(this int[] values, int index) =>
        values.Length == 0 ? 0 : values[index];
}
