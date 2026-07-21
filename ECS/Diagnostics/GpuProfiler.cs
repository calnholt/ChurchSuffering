using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChurchSuffering.ECS.Components;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics
{
    public readonly struct RenderWorkload
    {
        public long Clears { get; init; }
        public long Draws { get; init; }
        public long Primitives { get; init; }
        public long Sprites { get; init; }
        public long Targets { get; init; }
        public long Textures { get; init; }
        public long PixelShaders { get; init; }
        public long VertexShaders { get; init; }

        public static RenderWorkload FromDifference(GraphicsMetrics after, GraphicsMetrics before)
        {
            GraphicsMetrics delta = after - before;
            return new RenderWorkload
            {
                Clears = delta.ClearCount,
                Draws = delta.DrawCount,
                Primitives = delta.PrimitiveCount,
                Sprites = delta.SpriteCount,
                Targets = delta.TargetCount,
                Textures = delta.TextureCount,
                PixelShaders = delta.PixelShaderCount,
                VertexShaders = delta.VertexShaderCount,
            };
        }

        public static RenderWorkload operator +(RenderWorkload left, RenderWorkload right) => new()
        {
            Clears = left.Clears + right.Clears,
            Draws = left.Draws + right.Draws,
            Primitives = left.Primitives + right.Primitives,
            Sprites = left.Sprites + right.Sprites,
            Targets = left.Targets + right.Targets,
            Textures = left.Textures + right.Textures,
            PixelShaders = left.PixelShaders + right.PixelShaders,
            VertexShaders = left.VertexShaders + right.VertexShaders,
        };
    }

    internal interface IGpuQueryBackend : IDisposable
    {
        string BackendName { get; }
        bool IsSupported { get; }
        string Status { get; }
        void CreateQueries(uint[] queryIds);
        void DeleteQueries(uint[] queryIds);
        void WriteTimestamp(uint queryId);
        bool IsResultAvailable(uint queryId);
        ulong GetResultNanoseconds(uint queryId);
    }

    internal sealed class GpuQueryProfiler : IDisposable
    {
        internal const int PairCapacity = 512;

        private sealed class PendingPair
        {
            public string Name;
            public uint Start;
            public uint End;
            public long FrameId;
            public SceneId SceneId;
            public RenderWorkload Workload;
        }

        private readonly IGpuQueryBackend _backend;
        private readonly Queue<(uint Start, uint End)> _available = new(PairCapacity);
        private readonly List<PendingPair> _pending = new(PairCapacity);
        private bool _disposed;

        public int DroppedSamples { get; private set; }
        public int QueryErrors { get; private set; }
        public int PendingCount => _pending.Count;
        public bool IsSupported => !_disposed && _backend.IsSupported;
        public string BackendName => _backend.BackendName;
        public string Status => _disposed ? "shut down" : _backend.Status;

        public GpuQueryProfiler(IGpuQueryBackend backend)
        {
            _backend = backend;
            if (!_backend.IsSupported) return;

            var ids = new uint[PairCapacity * 2];
            try
            {
                _backend.CreateQueries(ids);
                for (int i = 0; i < ids.Length; i += 2) _available.Enqueue((ids[i], ids[i + 1]));
            }
            catch
            {
                QueryErrors++;
            }
        }

        public QueryToken Begin(string name, long frameId, SceneId sceneId)
        {
            if (!IsSupported || _available.Count == 0)
            {
                if (IsSupported) DroppedSamples++;
                return default;
            }

            var pair = _available.Dequeue();
            try
            {
                _backend.WriteTimestamp(pair.Start);
                return new QueryToken(this, name, pair.Start, pair.End, frameId, sceneId);
            }
            catch
            {
                QueryErrors++;
                _available.Enqueue(pair);
                return default;
            }
        }

        private void End(QueryToken token, RenderWorkload workload)
        {
            try
            {
                _backend.WriteTimestamp(token.EndId);
                _pending.Add(new PendingPair
                {
                    Name = token.Name,
                    Start = token.StartId,
                    End = token.EndId,
                    FrameId = token.FrameId,
                    SceneId = token.SceneId,
                    Workload = workload,
                });
            }
            catch
            {
                QueryErrors++;
                _available.Enqueue((token.StartId, token.EndId));
            }
        }

        public void Poll(Action<string, double, long, SceneId, RenderWorkload> completed)
        {
            if (!IsSupported) return;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingPair pair = _pending[i];
                try
                {
                    if (!_backend.IsResultAvailable(pair.Start) || !_backend.IsResultAvailable(pair.End)) continue;
                    ulong start = _backend.GetResultNanoseconds(pair.Start);
                    ulong end = _backend.GetResultNanoseconds(pair.End);
                    completed(pair.Name, end >= start ? (end - start) / 1_000_000.0 : 0, pair.FrameId, pair.SceneId, pair.Workload);
                }
                catch
                {
                    QueryErrors++;
                }

                _available.Enqueue((pair.Start, pair.End));
                _pending.RemoveAt(i);
            }
        }

        public void Reset()
        {
            if (_disposed) return;
            DisposeQueries();
            _pending.Clear();
            _available.Clear();
            if (!_backend.IsSupported) return;
            try
            {
                var ids = new uint[PairCapacity * 2];
                _backend.CreateQueries(ids);
                for (int i = 0; i < ids.Length; i += 2) _available.Enqueue((ids[i], ids[i + 1]));
            }
            catch { QueryErrors++; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            DisposeQueries();
            _pending.Clear();
            _available.Clear();
            _backend.Dispose();
            _disposed = true;
        }

        private void DisposeQueries()
        {
            if (!_backend.IsSupported) return;
            var ids = new List<uint>(_available.Count * 2 + _pending.Count * 2);
            foreach (var pair in _available) { ids.Add(pair.Start); ids.Add(pair.End); }
            foreach (var pair in _pending) { ids.Add(pair.Start); ids.Add(pair.End); }
            if (ids.Count == 0) return;
            try { _backend.DeleteQueries(ids.ToArray()); } catch { QueryErrors++; }
        }

        internal readonly struct QueryToken
        {
            private readonly GpuQueryProfiler _owner;
            public string Name { get; }
            public uint StartId { get; }
            public uint EndId { get; }
            public long FrameId { get; }
            public SceneId SceneId { get; }
            public bool IsValid => _owner != null;

            public QueryToken(GpuQueryProfiler owner, string name, uint startId, uint endId, long frameId, SceneId sceneId)
            {
                _owner = owner;
                Name = name;
                StartId = startId;
                EndId = endId;
                FrameId = frameId;
                SceneId = sceneId;
            }

            public void End(RenderWorkload workload)
            {
                if (IsValid) _owner.End(this, workload);
            }
        }
    }

    public static class GpuProfiler
    {
        private static GraphicsDevice _graphicsDevice;
        private static GpuQueryProfiler _queries;
        private static bool _capturePaused;

        public static bool CollectionEnabled => GpuProfilingRuntimeOptions.Enabled && _graphicsDevice != null;
        public static bool TimerSupported => _queries?.IsSupported == true;
        public static string BackendName => _queries?.BackendName ?? "not initialized";
        public static string Status => !CollectionEnabled ? "disabled" : _queries?.Status ?? "counters only";
        public static int DroppedSamples => _queries?.DroppedSamples ?? 0;
        public static int QueryErrors => _queries?.QueryErrors ?? 0;
        public static int PendingQueries => _queries?.PendingCount ?? 0;

        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            if (!GpuProfilingRuntimeOptions.Enabled) return;
            try { _queries = new GpuQueryProfiler(new DesktopGlTimerQueryBackend()); }
            catch { _queries = null; }
        }

        public static bool ShouldMeasure(string name) => CollectionEnabled && !_capturePaused && FrameProfiler.IsGpuScope(name);

        public static void Measure(string name, Action action)
        {
            GraphicsMetrics before = _graphicsDevice.Metrics;
            var token = _queries?.Begin(name, FrameProfiler.CurrentRenderFrameId, FrameProfiler.ActiveSceneId) ?? default;
            try { action(); }
            finally
            {
                RenderWorkload workload = RenderWorkload.FromDifference(_graphicsDevice.Metrics, before);
                FrameProfiler.AddWorkloadSample(name, workload);
                token.End(workload);
            }
        }

        public static void BeginRenderFrame()
        {
            _queries?.Poll(FrameProfiler.AddGpuSample);
        }

        public static void Reset()
        {
            _queries?.Reset();
        }

        public static void PauseCapture() => _capturePaused = true;
        public static void ResumeCapture() => _capturePaused = false;

        public static void Shutdown()
        {
            _queries?.Dispose();
            _queries = null;
            _graphicsDevice = null;
            _capturePaused = false;
        }
    }

    internal sealed class DesktopGlTimerQueryBackend : IGpuQueryBackend
    {
        private const uint Timestamp = 0x8E28;
        private const uint QueryResult = 0x8866;
        private const uint QueryResultAvailable = 0x8867;
        private const uint Version = 0x1F02;
        private const uint Extensions = 0x1F03;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GenQueriesDelegate(int count, [Out] uint[] ids);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DeleteQueriesDelegate(int count, uint[] ids);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void QueryCounterDelegate(uint id, uint target);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GetQueryObjectivDelegate(uint id, uint pname, out int value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GetQueryObjectui64vDelegate(uint id, uint pname, out ulong value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetStringDelegate(uint name);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GL_GetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string proc);

        private readonly GenQueriesDelegate _genQueries;
        private readonly DeleteQueriesDelegate _deleteQueries;
        private readonly QueryCounterDelegate _queryCounter;
        private readonly GetQueryObjectivDelegate _getQueryObjectiv;
        private readonly GetQueryObjectui64vDelegate _getQueryObjectui64v;

        public string BackendName => "DesktopGL OpenGL timestamp queries";
        public bool IsSupported { get; }
        public string Status { get; }

        public DesktopGlTimerQueryBackend()
        {
            try
            {
                _genQueries = Load<GenQueriesDelegate>("glGenQueries");
                _deleteQueries = Load<DeleteQueriesDelegate>("glDeleteQueries");
                _queryCounter = Load<QueryCounterDelegate>("glQueryCounter");
                _getQueryObjectiv = Load<GetQueryObjectivDelegate>("glGetQueryObjectiv");
                _getQueryObjectui64v = Load<GetQueryObjectui64vDelegate>("glGetQueryObjectui64v");
                var getString = Load<GetStringDelegate>("glGetString");
                string version = getString == null ? string.Empty : Marshal.PtrToStringAnsi(getString(Version)) ?? string.Empty;
                string extensions = getString == null ? string.Empty : Marshal.PtrToStringAnsi(getString(Extensions)) ?? string.Empty;
                bool advertised = IsAtLeastOpenGl33(version) ||
                    extensions.Contains("GL_ARB_timer_query", StringComparison.Ordinal);
                IsSupported = advertised && _genQueries != null && _deleteQueries != null && _queryCounter != null &&
                    _getQueryObjectiv != null && _getQueryObjectui64v != null;
                Status = IsSupported
                    ? $"timer queries available ({version})"
                    : $"timer-query support unavailable ({(string.IsNullOrWhiteSpace(version) ? "no current OpenGL context" : version)})";
            }
            catch (Exception ex)
            {
                IsSupported = false;
                Status = $"timer queries unavailable: {ex.GetType().Name}";
            }
        }

        public void CreateQueries(uint[] queryIds) => _genQueries(queryIds.Length, queryIds);
        public void DeleteQueries(uint[] queryIds) => _deleteQueries(queryIds.Length, queryIds);
        public void WriteTimestamp(uint queryId) => _queryCounter(queryId, Timestamp);
        public bool IsResultAvailable(uint queryId) { _getQueryObjectiv(queryId, QueryResultAvailable, out int available); return available != 0; }
        public ulong GetResultNanoseconds(uint queryId) { _getQueryObjectui64v(queryId, QueryResult, out ulong value); return value; }
        public void Dispose() { }

        private static T Load<T>(string name) where T : Delegate
        {
            IntPtr address = SDL_GL_GetProcAddress(name);
            return address == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        private static bool IsAtLeastOpenGl33(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            int start = 0;
            while (start < version.Length && !char.IsDigit(version[start])) start++;
            if (start >= version.Length) return false;
            int dot = version.IndexOf('.', start);
            if (dot < 0 || !int.TryParse(version.AsSpan(start, dot - start), out int major)) return false;
            int end = dot + 1;
            while (end < version.Length && char.IsDigit(version[end])) end++;
            if (!int.TryParse(version.AsSpan(dot + 1, end - dot - 1), out int minor)) return false;
            return major > 3 || major == 3 && minor >= 3;
        }
    }
}
