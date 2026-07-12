using System;
using System.Collections.Generic;
using System.Linq;

namespace Crusaders30XX.ECS.Systems
{
    internal enum GuardianSpeechCategory
    {
        Phase,
        Card,
        Medal,
        EnemyAttack,
    }

    internal enum GuardianFlightGesture
    {
        None,
        Flourish,
        CardHop,
        MedalLoop,
        EnemyBrace,
    }

    internal sealed record GuardianSpeechRequest(
        string SourceKey,
        string Text,
        GuardianSpeechCategory Category,
        int Priority,
        float EnqueuedAt,
        float MaxAge,
        GuardianFlightGesture Gesture)
    {
        public bool IsUrgent => Category is GuardianSpeechCategory.EnemyAttack or GuardianSpeechCategory.Medal;
        public bool IsExpired(float now) => now - EnqueuedAt > MaxAge;
    }

    internal sealed class GuardianAngelSpeechQueue
    {
        public const int MaximumPending = 8;
        private readonly List<GuardianSpeechRequest> _pending = [];

        internal IReadOnlyList<GuardianSpeechRequest> Pending => _pending;

        public bool Enqueue(GuardianSpeechRequest request, float now)
        {
            RemoveExpired(now);
            if (_pending.Any(existing => existing.Category == request.Category && existing.SourceKey == request.SourceKey))
                return false;

            _pending.Add(request);
            if (_pending.Count > MaximumPending)
            {
                GuardianSpeechRequest evicted = _pending
                    .OrderBy(item => item.Priority)
                    .ThenBy(item => item.EnqueuedAt)
                    .First();
                _pending.Remove(evicted);
            }
            return _pending.Contains(request);
        }

        public bool TryDequeue(float now, out GuardianSpeechRequest request)
        {
            RemoveExpired(now);
            request = _pending
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.EnqueuedAt)
                .FirstOrDefault();
            if (request == null) return false;
            _pending.Remove(request);
            return true;
        }

        public static bool ShouldInterrupt(GuardianSpeechRequest active, GuardianSpeechRequest incoming) =>
            active != null && incoming.IsUrgent && !active.IsUrgent;

        public static float GetDisplayDuration(string text)
        {
            int length = text?.Length ?? 0;
            return Math.Clamp(1.6f + length * 0.025f, 1.6f, 3.2f);
        }

        public void Clear() => _pending.Clear();

        private void RemoveExpired(float now) => _pending.RemoveAll(item => item.IsExpired(now));
    }
}
