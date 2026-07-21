using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChurchSuffering.ECS.Services
{
    public static class LoggingService
    {
        private static readonly object _syncRoot = new();
        private static List<JsonObject> _buffer = new();
        private static int _callCount = 0;
        private static int _frameCount = 0;
        private static int _seqCounter = 0;
        private const int FlushEveryFrames = 300;
        private const int FlushEveryNCalls = 50;
        private const string LogPath = "logs/session.ndjson";

        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = false };

        [Conditional("DEBUG")]
        public static void Initialize()
        {
            lock (_syncRoot)
            {
                _buffer = new();
                _callCount = 0;
                _frameCount = 0;
                _seqCounter = 0;
                Directory.CreateDirectory("logs");
                File.WriteAllText(LogPath, string.Empty);
            }
        }

        [Conditional("DEBUG")]
        public static void Tick()
        {
            bool shouldFlush;
            lock (_syncRoot)
            {
                _frameCount++;
                shouldFlush = _frameCount % FlushEveryFrames == 0 || _callCount >= FlushEveryNCalls;
            }

            if (shouldFlush) FlushInternal();
        }

        [Conditional("DEBUG")]
        public static void Flush()
        {
            FlushInternal();
        }

        [Conditional("DEBUG")]
        internal static void Append(string context, JsonObject entry)
        {
            string json;
            lock (_syncRoot)
            {
                entry["seq"] = ++_seqCounter;
                entry["frame"] = _frameCount;
                entry["context"] = context;

                json = JsonSerializer.Serialize(entry, _writeOptions);
                _buffer.Add(entry);
                _callCount++;
            }

            Console.WriteLine(json);
        }

        private static void FlushInternal()
        {
            try
            {
                lock (_syncRoot)
                {
                    if (_buffer.Count == 0) return;

                    using var writer = new StreamWriter(LogPath, append: true);
                    foreach (var entry in _buffer)
                    {
                        writer.WriteLine(JsonSerializer.Serialize(entry, _writeOptions));
                    }

                    _buffer.Clear();
                    _callCount = 0;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[LoggingService] Flush failed: {ex.Message}"); }
        }
    }
}
