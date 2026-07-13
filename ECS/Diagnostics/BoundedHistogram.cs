using System;

namespace Crusaders30XX.Diagnostics
{
    /// <summary>
    /// Fixed-size timing distribution used by the session profiler. Percentiles are
    /// reported as the upper edge of the bucket containing the requested rank.
    /// </summary>
    internal sealed class BoundedHistogram
    {
        private const int FineBuckets = 400;   // 0-20 ms, 0.05 ms
        private const int MediumBuckets = 160; // 20-100 ms, 0.5 ms
        private const int CoarseBuckets = 80;  // 100-500 ms, 5 ms
        private readonly long[] _buckets = new long[FineBuckets + MediumBuckets + CoarseBuckets + 1];

        public long Count { get; private set; }
        public long SlowCount { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public double Sum { get; private set; }

        public void Add(double milliseconds, double slowThreshold = 16.67)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds)) return;
            milliseconds = Math.Max(0, milliseconds);
            _buckets[GetBucketIndex(milliseconds)]++;
            if (Count == 0)
            {
                Min = milliseconds;
                Max = milliseconds;
            }
            else
            {
                Min = Math.Min(Min, milliseconds);
                Max = Math.Max(Max, milliseconds);
            }

            Sum += milliseconds;
            Count++;
            if (milliseconds > slowThreshold) SlowCount++;
        }

        public double Percentile(double percentile)
        {
            if (Count == 0) return 0;
            long rank = Math.Max(1, (long)Math.Ceiling(Math.Clamp(percentile, 0, 1) * Count));
            long seen = 0;
            for (int i = 0; i < _buckets.Length; i++)
            {
                seen += _buckets[i];
                if (seen >= rank) return GetBucketUpperEdge(i);
            }

            return Max;
        }

        public void Clear()
        {
            Array.Clear(_buckets);
            Count = 0;
            SlowCount = 0;
            Min = 0;
            Max = 0;
            Sum = 0;
        }

        private static int GetBucketIndex(double milliseconds)
        {
            if (milliseconds < 20) return Math.Min(FineBuckets - 1, (int)(milliseconds / 0.05));
            if (milliseconds < 100) return FineBuckets + Math.Min(MediumBuckets - 1, (int)((milliseconds - 20) / 0.5));
            if (milliseconds < 500) return FineBuckets + MediumBuckets + Math.Min(CoarseBuckets - 1, (int)((milliseconds - 100) / 5));
            return FineBuckets + MediumBuckets + CoarseBuckets;
        }

        private double GetBucketUpperEdge(int index)
        {
            if (index < FineBuckets) return (index + 1) * 0.05;
            index -= FineBuckets;
            if (index < MediumBuckets) return 20 + (index + 1) * 0.5;
            index -= MediumBuckets;
            if (index < CoarseBuckets) return 100 + (index + 1) * 5;
            return Max;
        }
    }
}
