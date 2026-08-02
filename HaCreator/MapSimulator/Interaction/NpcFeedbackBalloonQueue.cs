using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class NpcFeedbackBalloonQueue
    {
        private sealed class BalloonEntry
        {
            public int NpcId { get; init; }
            public string Text { get; init; } = string.Empty;
            public int DurationMs { get; init; }
        }

        private const int MinDurationMs = 2400;
        private const int MaxDurationMs = 5200;
        private const int DurationPerCharacterMs = 45;
        private readonly Queue<BalloonEntry> _pendingEntries = new();

        public int ActiveNpcId { get; private set; }
        public string ActiveText { get; private set; }
        public int ActiveExpiresAt { get; private set; }

        public bool Enqueue(int npcId, IEnumerable<string> messages, int currentTickCount)
        {
            if (npcId <= 0 || messages == null)
            {
                return false;
            }

            if (ActiveNpcId != 0 && ActiveNpcId != npcId)
            {
                Clear();
            }

            bool hadActiveBefore = HasActiveBalloon;

            foreach (string message in messages)
            {
                string sanitized = SanitizeMessage(message);
                if (string.IsNullOrWhiteSpace(sanitized))
                {
                    continue;
                }

                _pendingEntries.Enqueue(new BalloonEntry
                {
                    NpcId = npcId,
                    Text = sanitized,
                    DurationMs = GetDurationMs(sanitized)
                });
            }

            if (!hadActiveBefore)
            {
                return TryActivateNext(currentTickCount);
            }

            return false;
        }

        public bool Update(int currentTickCount)
        {
            if (HasActiveBalloon && currentTickCount < ActiveExpiresAt)
            {
                return false;
            }

            return TryActivateNext(currentTickCount);
        }

        public void Clear()
        {
            _pendingEntries.Clear();
            ActiveNpcId = 0;
            ActiveText = null;
            ActiveExpiresAt = 0;
        }

        public bool HasActiveBalloon => ActiveNpcId > 0 && !string.IsNullOrWhiteSpace(ActiveText);

        private bool TryActivateNext(int currentTickCount)
        {
            if (_pendingEntries.Count == 0)
            {
                bool hadActive = HasActiveBalloon;
                ActiveNpcId = 0;
                ActiveText = null;
                ActiveExpiresAt = 0;
                return hadActive;
            }

            BalloonEntry next = _pendingEntries.Dequeue();
            ActiveNpcId = next.NpcId;
            ActiveText = next.Text;
            ActiveExpiresAt = currentTickCount + next.DurationMs;
            return true;
        }

        private static string SanitizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            string trimmed = message.Trim();
            int newlineIndex = trimmed.IndexOf('\n');
            if (newlineIndex >= 0)
            {
                trimmed = trimmed.Substring(0, newlineIndex).Trim();
            }

            return trimmed;
        }

        private static int GetDurationMs(string text)
        {
            int duration = MinDurationMs + (text.Length * DurationPerCharacterMs);
            return Math.Clamp(duration, MinDurationMs, MaxDurationMs);
        }
    }
}
