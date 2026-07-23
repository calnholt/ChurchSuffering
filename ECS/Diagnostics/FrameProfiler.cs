using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.Diagnostics
{
    public static class FrameProfiler
    {
        public enum SampleKind { Leaf, Inclusive }

        public struct Sample
        {
            public string Name { get; set; }
            public int Calls { get; set; }
            public double TotalMs { get; set; }
            public double AvgMs { get; set; }
            public double FrameAvgMs { get; set; }
        }

        public struct SystemStats
        {
            public string Name;
            public double MinMs;
            public double MaxMs;
            public double AvgMs;
            public double P95Ms;
            public int SlowFrames;
            public int Frames;
        }

        public readonly struct LiveFrameMetrics
        {
            public double RenderCadenceMs { get; init; }
            public double CpuBusyMs { get; init; }
            public double OutsideMs { get; init; }
            public double GpuMs { get; init; }
            public RenderWorkload Workload { get; init; }
        }

        private readonly struct FrameMarker
        {
            public double TimeSeconds { get; init; }
            public long FrameId { get; init; }
        }

        private readonly struct Entry
        {
            public string Name { get; init; }
            public long Ticks { get; init; }
            public double TimeSeconds { get; init; }
            public long FrameId { get; init; }
            public bool Gpu { get; init; }
        }

        private sealed class SessionAccumulator
        {
            public readonly BoundedHistogram Histogram = new();
            public void Add(double milliseconds) => Histogram.Add(milliseconds);
        }

        private sealed class WorkloadAccumulator
        {
            public RenderWorkload Total;
            public long Samples;
            public void Add(RenderWorkload workload) { Total += workload; Samples++; }
        }

        private static readonly object Sync = new();
        private static readonly Queue<Entry> Entries = new(4096);
        private static readonly Queue<FrameMarker> Frames = new(512);
        private static readonly Dictionary<string, long> CurrentCpuTotals = new(128);
        private static readonly Dictionary<string, RenderWorkload> CurrentWorkloads = new(32);
        private static readonly Dictionary<string, SessionAccumulator> CpuSession = new(192);
        private static readonly Dictionary<string, SessionAccumulator> GpuSession = new(32);
        private static readonly Dictionary<string, WorkloadAccumulator> WorkloadSession = new(32);
        private static readonly Dictionary<SceneId, Dictionary<string, SessionAccumulator>> SceneCpu = new();
        private static readonly Dictionary<SceneId, Dictionary<string, SessionAccumulator>> SceneGpu = new();
        private static readonly Dictionary<SceneId, int> SceneRenderedFrames = new();
        private static readonly HashSet<string> GpuScopes = new(StringComparer.Ordinal)
        {
            "Game1.Draw.Commands",
            "Game1.Draw.SceneSetupAndDrawScene",
            "Game1.Draw.ShaderComposite",
            "Game1.Draw.Present",
            "BattleSceneSystem.Draw",
            "Battle.BackgroundCompositePass",
            "Battle.OrdinaryUiPass",
            "Battle.CardStatusPass",
            "Battle.HandPass",
            "Battle.HandBaseCards",
            "Battle.HandDecorations",
            "Battle.GlobalOverlays",
            "ModularEffectScreenDisplaySystem.Draw",
            "ModularEffectPrimitiveDisplaySystem.Draw",
            "HandDisplaySystem.DrawHand",
            "CardListModalSystem.DrawForeground",
            "BoosterPackOpeningDisplaySystem.Draw",
            "WayStationCollectionModalV2.Draw",
        };

        private static readonly BoundedHistogram RenderCadenceSession = new();
        private static readonly BoundedHistogram CpuBusySession = new();
        private static readonly BoundedHistogram OutsideSession = new();
        private static readonly BoundedHistogram GpuFrameSession = new();
        private static long _currentRenderFrameId;
        private static long _renderedFrames;
        private static long _updateIterations;
        private static long _lastRenderStart;
        private static long _busyStart;
        private static double _lastBusyMs;
        private static SceneId _lastRenderedScene = SceneId.None;
        private static bool _skipCurrentFrame;
        private static LiveFrameMetrics _latestFrame;

        public static double WindowSeconds { get; set; } = 1;
        public static long CurrentRenderFrameId => _currentRenderFrameId;
        public static SceneId ActiveSceneId { get; private set; } = SceneId.None;
        internal static bool IsGpuScope(string name) => GpuScopes.Contains(name);

        public static void BeginUpdateIteration(bool skipSessionAccumulation = false)
        {
            lock (Sync)
            {
                _updateIterations++;
                _skipCurrentFrame |= skipSessionAccumulation;
                if (_busyStart == 0) _busyStart = Stopwatch.GetTimestamp();
            }
        }

        public static void BeginRenderFrame(bool skipSessionAccumulation = false)
        {
            lock (Sync)
            {
                long nowTicks = Stopwatch.GetTimestamp();
                _skipCurrentFrame |= skipSessionAccumulation;
                if (_busyStart == 0) _busyStart = nowTicks;
                if (_lastRenderStart != 0 && !_skipCurrentFrame)
                {
                    double cadenceMs = TicksToMs(nowTicks - _lastRenderStart);
                    double outsideMs = Math.Max(0, cadenceMs - _lastBusyMs);
                    RenderCadenceSession.Add(cadenceMs);
                    OutsideSession.Add(outsideMs);
                    _latestFrame = new LiveFrameMetrics
                    {
                        RenderCadenceMs = cadenceMs,
                        CpuBusyMs = _latestFrame.CpuBusyMs,
                        OutsideMs = outsideMs,
                        GpuMs = _latestFrame.GpuMs,
                        Workload = _latestFrame.Workload,
                    };
                }

                _lastRenderStart = nowTicks;
                _currentRenderFrameId++;
                Frames.Enqueue(new FrameMarker { TimeSeconds = TimestampSeconds(nowTicks), FrameId = _currentRenderFrameId });
                PruneOld(TimestampSeconds(nowTicks));
            }
            GpuProfiler.BeginRenderFrame();
        }

        public static void EndRenderFrame()
        {
            lock (Sync)
            {
                long nowTicks = Stopwatch.GetTimestamp();
                double busyMs = _busyStart == 0 ? 0 : TicksToMs(nowTicks - _busyStart);
                _lastBusyMs = busyMs;
                _busyStart = 0;

                if (!_skipCurrentFrame)
                {
                    CommitCurrentFrameLocked(busyMs);
                    CpuBusySession.Add(busyMs);
                    _renderedFrames++;
                    SceneRenderedFrames.TryGetValue(ActiveSceneId, out int sceneFrames);
                    SceneRenderedFrames[ActiveSceneId] = sceneFrames + 1;
                    _lastRenderedScene = ActiveSceneId;
                    CurrentWorkloads.TryGetValue("Game1.Draw.Commands", out RenderWorkload workload);
                    _latestFrame = new LiveFrameMetrics
                    {
                        RenderCadenceMs = _latestFrame.RenderCadenceMs,
                        CpuBusyMs = busyMs,
                        OutsideMs = _latestFrame.OutsideMs,
                        GpuMs = _latestFrame.GpuMs,
                        Workload = workload,
                    };
                }

                CurrentCpuTotals.Clear();
                CurrentWorkloads.Clear();
                _skipCurrentFrame = false;
            }
        }

        public static void SetActiveScene(SceneId sceneId) => ActiveSceneId = sceneId;

        [Obsolete("Use BeginUpdateIteration and BeginRenderFrame.")]
        public static void BeginGameFrame(Microsoft.Xna.Framework.GameTime gameTime, bool skipSessionAccumulation = false) =>
            BeginUpdateIteration(skipSessionAccumulation);

        [Obsolete("Use EndRenderFrame.")]
        public static void EndGameFrame(Microsoft.Xna.Framework.GameTime gameTime) => EndRenderFrame();

        public static void AddSample(string name, long elapsedTicks, SampleKind kind = SampleKind.Leaf)
        {
            lock (Sync)
            {
                double now = TimestampSeconds(Stopwatch.GetTimestamp());
                Entries.Enqueue(new Entry { Name = name, Ticks = elapsedTicks, TimeSeconds = now, FrameId = _currentRenderFrameId, Gpu = false });
                CurrentCpuTotals.TryGetValue(name, out long total);
                CurrentCpuTotals[name] = total + elapsedTicks;
                PruneOld(now);
            }
        }

        public static void Measure(string name, Action action)
        {
            long started = Stopwatch.GetTimestamp();
            try
            {
                if (GpuProfiler.ShouldMeasure(name)) GpuProfiler.Measure(name, action);
                else action();
            }
            finally { AddSample(name, Stopwatch.GetTimestamp() - started); }
        }

        public static T Measure<T>(string name, Func<T> func)
        {
            long started = Stopwatch.GetTimestamp();
            try { return func(); }
            finally { AddSample(name, Stopwatch.GetTimestamp() - started); }
        }

        [Conditional("DEBUG")]
        public static void MeasureInclusive(string name, Action action)
        {
            long started = Stopwatch.GetTimestamp();
            try
            {
                if (GpuProfiler.ShouldMeasure(name)) GpuProfiler.Measure(name, action);
                else action();
            }
            finally { AddSample(name, Stopwatch.GetTimestamp() - started, SampleKind.Inclusive); }
        }

        public static ScopeTimer Scope(string name) => new(name, SampleKind.Leaf);

        public readonly struct ScopeTimer : IDisposable
        {
            private readonly string _name;
            private readonly SampleKind _kind;
            private readonly long _started;
            internal ScopeTimer(string name, SampleKind kind) { _name = name; _kind = kind; _started = Stopwatch.GetTimestamp(); }
            public void Dispose() => AddSample(_name, Stopwatch.GetTimestamp() - _started, _kind);
        }

        internal static void AddWorkloadSample(string name, RenderWorkload workload)
        {
            lock (Sync)
            {
                CurrentWorkloads.TryGetValue(name, out RenderWorkload current);
                CurrentWorkloads[name] = current + workload;
            }
        }

        internal static void AddGpuSample(string name, double milliseconds, long frameId, SceneId sceneId, RenderWorkload workload)
        {
            lock (Sync)
            {
                GetAccumulator(GpuSession, name).Add(milliseconds);
                GetAccumulator(GetSceneMap(SceneGpu, sceneId), name).Add(milliseconds);
                double now = TimestampSeconds(Stopwatch.GetTimestamp());
                Entries.Enqueue(new Entry
                {
                    Name = name,
                    Ticks = (long)(milliseconds / 1000.0 * Stopwatch.Frequency),
                    TimeSeconds = now,
                    FrameId = frameId,
                    Gpu = true,
                });
                if (name == "Game1.Draw.Commands")
                {
                    GpuFrameSession.Add(milliseconds);
                    _latestFrame = new LiveFrameMetrics
                    {
                        RenderCadenceMs = _latestFrame.RenderCadenceMs,
                        CpuBusyMs = _latestFrame.CpuBusyMs,
                        OutsideMs = _latestFrame.OutsideMs,
                        GpuMs = milliseconds,
                        Workload = _latestFrame.Workload,
                    };
                }
                PruneOld(now);
            }
        }

        public static LiveFrameMetrics GetLatestFrameMetrics()
        {
            lock (Sync) return _latestFrame;
        }

        public static List<Sample> GetTopSamples(int count) => GetTopSamples(count, gpu: false);
        public static List<Sample> GetTopGpuSamples(int count) => GetTopSamples(count, gpu: true);

        private static List<Sample> GetTopSamples(int count, bool gpu)
        {
            lock (Sync)
            {
                double cutoff = TimestampSeconds(Stopwatch.GetTimestamp()) - WindowSeconds;
                int framesInWindow = Math.Max(1, Frames.Count(frame => frame.TimeSeconds >= cutoff));
                return Entries
                    .Where(entry => entry.Gpu == gpu && entry.TimeSeconds >= cutoff)
                    .GroupBy(entry => entry.Name)
                    .Select(group =>
                    {
                        long ticks = group.Sum(entry => entry.Ticks);
                        int calls = group.Count();
                        double totalMs = TicksToMs(ticks);
                        return new Sample
                        {
                            Name = group.Key,
                            Calls = calls,
                            TotalMs = totalMs,
                            AvgMs = calls == 0 ? 0 : totalMs / calls,
                            FrameAvgMs = totalMs / framesInWindow,
                        };
                    })
                    .OrderByDescending(sample => sample.FrameAvgMs)
                    .Take(count)
                    .ToList();
            }
        }

        public static bool TryGetSessionStats(string name, bool gpu, out SystemStats stats)
        {
            lock (Sync)
            {
                var map = gpu ? GpuSession : CpuSession;
                if (!map.TryGetValue(name, out SessionAccumulator accumulator))
                {
                    stats = default;
                    return false;
                }
                stats = ToStats(name, accumulator);
                return true;
            }
        }

        public static bool TryGetAverageDrawCalls(string name, out double drawCalls)
        {
            lock (Sync)
            {
                if (!WorkloadSession.TryGetValue(name, out WorkloadAccumulator accumulator) ||
                    accumulator.Samples <= 0)
                {
                    drawCalls = 0;
                    return false;
                }

                drawCalls = accumulator.Total.Draws / (double)accumulator.Samples;
                return true;
            }
        }

        [Conditional("DEBUG")]
        public static void ResetSession()
        {
            lock (Sync)
            {
                CpuSession.Clear(); GpuSession.Clear(); WorkloadSession.Clear(); SceneCpu.Clear(); SceneGpu.Clear();
                SceneRenderedFrames.Clear(); CurrentCpuTotals.Clear(); CurrentWorkloads.Clear();
                RenderCadenceSession.Clear(); CpuBusySession.Clear(); OutsideSession.Clear(); GpuFrameSession.Clear();
                _renderedFrames = 0; _updateIterations = 0;
            }
        }

        [Conditional("DEBUG")]
        public static void WriteReport(string path, SceneId sceneAtQuit, bool shadersEnabled)
        {
            string report;
            lock (Sync)
            {
                var sb = new StringBuilder(16384);
                sb.AppendLine("Church Suffering Performance Report");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("CPU values use wall-clock Stopwatch timestamps. GPU values are delayed, asynchronous command timings and exclude swap/vsync.");
                sb.AppendLine("P95 is the upper edge of a bounded deterministic histogram bucket.");
                sb.AppendLine();
                sb.AppendLine("=== Session ===");
                sb.AppendLine($"Scene at quit: {sceneAtQuit}");
                sb.AppendLine($"Rendered frames: {_renderedFrames}");
                sb.AppendLine($"Update iterations: {_updateIterations}");
                sb.AppendLine($"Shaders enabled: {(shadersEnabled ? "yes" : "no")}");
                sb.AppendLine($"GPU profiling requested: {(GpuProfilingRuntimeOptions.Enabled ? "yes" : "no")}");
                sb.AppendLine($"GPU backend: {GpuProfiler.BackendName}");
                sb.AppendLine($"GPU status: {GpuProfiler.Status}");
                sb.AppendLine($"GPU unresolved query pairs: {GpuProfiler.PendingQueries}");
                sb.AppendLine($"GPU dropped samples: {GpuProfiler.DroppedSamples}");
                sb.AppendLine($"GPU query errors: {GpuProfiler.QueryErrors}");
                sb.AppendLine("Rendered frames by scene:");
                foreach (SceneId scene in Enum.GetValues(typeof(SceneId)))
                {
                    SceneRenderedFrames.TryGetValue(scene, out int frames);
                    sb.AppendLine($"  {scene}: {frames}");
                }

                sb.AppendLine();
                sb.AppendLine("=== Render frame wall clock (global) ===");
                AppendHistogram(sb, "Render cadence (ms)", RenderCadenceSession);
                AppendHistogram(sb, "CPU busy (ms)", CpuBusySession);
                AppendHistogram(sb, "Outside/pacing/driver (ms)", OutsideSession);
                AppendHistogram(sb, "GPU commands (ms)", GpuFrameSession);
                sb.AppendLine("  Outside/pacing/driver = render-start cadence minus the preceding update+draw CPU busy interval, clamped at zero.");

                AppendScopeSection(sb, "CPU scopes (global)", CpuSession);
                AppendScopeSection(sb, "GPU scopes (global)", GpuSession);
                AppendWorkloadSection(sb);

                foreach (SceneId scene in Enum.GetValues(typeof(SceneId)))
                {
                    if (!SceneRenderedFrames.TryGetValue(scene, out int frames) || frames < 60) continue;
                    if (SceneCpu.TryGetValue(scene, out var cpu)) AppendScopeSection(sb, $"CPU scopes ({scene}, {frames} rendered frames)", cpu);
                    if (SceneGpu.TryGetValue(scene, out var gpu)) AppendScopeSection(sb, $"GPU scopes ({scene}, delayed samples)", gpu);
                }
                report = sb.ToString();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "logs");
            File.WriteAllText(path, report);
        }

        private static void CommitCurrentFrameLocked(double busyMs)
        {
            var sceneMap = GetSceneMap(SceneCpu, ActiveSceneId);
            foreach (var pair in CurrentCpuTotals)
            {
                double ms = TicksToMs(pair.Value);
                GetAccumulator(CpuSession, pair.Key).Add(ms);
                GetAccumulator(sceneMap, pair.Key).Add(ms);
            }
            foreach (var pair in CurrentWorkloads)
            {
                GetWorkloadAccumulator(pair.Key).Add(pair.Value);
            }
        }

        private static Dictionary<string, SessionAccumulator> GetSceneMap(
            Dictionary<SceneId, Dictionary<string, SessionAccumulator>> maps,
            SceneId scene)
        {
            if (!maps.TryGetValue(scene, out var map))
            {
                map = new Dictionary<string, SessionAccumulator>(128);
                maps[scene] = map;
            }
            return map;
        }

        private static SessionAccumulator GetAccumulator(Dictionary<string, SessionAccumulator> map, string name)
        {
            if (!map.TryGetValue(name, out var accumulator))
            {
                accumulator = new SessionAccumulator();
                map[name] = accumulator;
            }
            return accumulator;
        }

        private static WorkloadAccumulator GetWorkloadAccumulator(string name)
        {
            if (!WorkloadSession.TryGetValue(name, out var accumulator))
            {
                accumulator = new WorkloadAccumulator();
                WorkloadSession[name] = accumulator;
            }
            return accumulator;
        }

        private static SystemStats ToStats(string name, SessionAccumulator accumulator)
        {
            var histogram = accumulator.Histogram;
            return new SystemStats
            {
                Name = name,
                MinMs = histogram.Min,
                MaxMs = histogram.Max,
                AvgMs = histogram.Count == 0 ? 0 : histogram.Sum / histogram.Count,
                P95Ms = histogram.Percentile(0.95),
                SlowFrames = (int)Math.Min(int.MaxValue, histogram.SlowCount),
                Frames = (int)Math.Min(int.MaxValue, histogram.Count),
            };
        }

        private static void AppendHistogram(StringBuilder sb, string label, BoundedHistogram histogram)
        {
            if (histogram.Count == 0) { sb.AppendLine($"{label,-28} no samples"); return; }
            sb.AppendLine($"{label,-28} min={histogram.Min,8:0.00} max={histogram.Max,8:0.00} avg={histogram.Sum / histogram.Count,8:0.00} p95={histogram.Percentile(0.95),8:0.00} samples={histogram.Count,8}");
        }

        private static void AppendScopeSection(StringBuilder sb, string title, Dictionary<string, SessionAccumulator> map)
        {
            sb.AppendLine();
            sb.AppendLine($"=== {title} ===");
            sb.AppendLine($"{"Name",-48} {"Min",8} {"Max",8} {"Avg",8} {"P95",8} {"Slow",7} {"Samples",9}");
            sb.AppendLine(new string('-', 104));
            foreach (var stats in map.Select(pair => ToStats(pair.Key, pair.Value)).Where(row => row.AvgMs >= 0.01).OrderByDescending(row => row.AvgMs))
            {
                sb.AppendLine($"{stats.Name,-48} {stats.MinMs,8:0.00} {stats.MaxMs,8:0.00} {stats.AvgMs,8:0.00} {stats.P95Ms,8:0.00} {stats.SlowFrames,7} {stats.Frames,9}");
            }
        }

        private static void AppendWorkloadSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("=== Rendering workload (average per timed scope call) ===");
            sb.AppendLine($"{"Name",-42} {"Draws",8} {"Sprites",9} {"Prims",9} {"Targets",8} {"Textures",9} {"Shaders",9} {"Clears",8}");
            sb.AppendLine(new string('-', 110));
            foreach (var pair in WorkloadSession.OrderByDescending(pair => pair.Value.Total.Draws))
            {
                double n = Math.Max(1, pair.Value.Samples);
                RenderWorkload w = pair.Value.Total;
                sb.AppendLine($"{pair.Key,-42} {w.Draws / n,8:0.0} {w.Sprites / n,9:0.0} {w.Primitives / n,9:0.0} {w.Targets / n,8:0.0} {w.Textures / n,9:0.0} {(w.PixelShaders + w.VertexShaders) / n,9:0.0} {w.Clears / n,8:0.0}");
            }
        }

        private static double TicksToMs(long ticks) => ticks * (1000.0 / Stopwatch.Frequency);
        private static double TimestampSeconds(long ticks) => ticks / (double)Stopwatch.Frequency;

        private static void PruneOld(double now)
        {
            double cutoff = now - Math.Max(1, WindowSeconds) * 1.5;
            while (Entries.Count > 0 && Entries.Peek().TimeSeconds < cutoff) Entries.Dequeue();
            while (Frames.Count > 0 && Frames.Peek().TimeSeconds < cutoff) Frames.Dequeue();
        }
    }
}
