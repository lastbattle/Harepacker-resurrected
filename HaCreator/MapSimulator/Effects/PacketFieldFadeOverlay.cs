using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Effects
{
    internal sealed class PacketFieldFadeOverlay
    {
        private readonly List<FadeEntry> _entries = new();

        public bool IsActive => _entries.Count > 0;
        public int ActiveFadeCount => _entries.Count;
        public int RequestedLayerZ => TryGetLatestEntry(out FadeEntry entry) ? entry.LayerZ : 0;
        public int StartingAlpha => TryGetLatestEntry(out FadeEntry entry) ? entry.StartingAlpha : 0;
        public int FadeInMs => TryGetLatestEntry(out FadeEntry entry) ? entry.FadeInMs : 0;
        public int HoldMs => TryGetLatestEntry(out FadeEntry entry) ? entry.HoldMs : 0;
        public int FadeOutMs => TryGetLatestEntry(out FadeEntry entry) ? entry.FadeOutMs : 0;
        public int StartedAt => TryGetLatestEntry(out FadeEntry entry) ? entry.StartedAt : 0;
        public int FadeOutStartsAt => TryGetLatestEntry(out FadeEntry entry) ? entry.FadeOutStartsAt : int.MinValue;
        public int ExpiresAt => TryGetLatestEntry(out FadeEntry entry) ? entry.ExpiresAt : int.MinValue;

        public bool Start(int fadeInMs, int holdMs, int fadeOutMs, int startingAlpha, int layerZ, int currentTickCount)
        {
            int resolvedFadeInMs = Math.Max(0, fadeInMs);
            int resolvedHoldMs = Math.Max(0, holdMs);
            int resolvedFadeOutMs = Math.Max(0, fadeOutMs);
            if (resolvedFadeInMs + resolvedHoldMs + resolvedFadeOutMs <= 0)
            {
                return false;
            }

            _entries.Add(new FadeEntry(
                resolvedFadeInMs,
                resolvedHoldMs,
                resolvedFadeOutMs,
                Math.Clamp(startingAlpha, 0, byte.MaxValue),
                layerZ,
                currentTickCount));
            return true;
        }

        public void Clear()
        {
            _entries.Clear();
        }

        public int RemoveLayer(int layerZ)
        {
            int removedCount = 0;
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                if (_entries[index].LayerZ != layerZ)
                {
                    continue;
                }

                _entries.RemoveAt(index);
                removedCount++;
            }

            return removedCount;
        }

        public int ForceFadeOutPending(int fadeOutMs, int currentTickCount)
        {
            if (_entries.Count == 0)
            {
                return 0;
            }

            int forcedCount = 0;
            int resolvedFadeOutMs = Math.Max(0, fadeOutMs);
            for (int index = 0; index < _entries.Count; index++)
            {
                FadeEntry entry = _entries[index];
                if (entry.HasFadeOutStarted(currentTickCount))
                {
                    continue;
                }

                _entries[index] = entry.WithForcedFadeOut(currentTickCount, resolvedFadeOutMs);
                forcedCount++;
            }

            return forcedCount;
        }

        public void Update(int currentTickCount)
        {
            if (_entries.Count == 0)
            {
                return;
            }

            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                if (unchecked(currentTickCount - _entries[index].ExpiresAt) >= 0)
                {
                    _entries.RemoveAt(index);
                }
            }
        }

        public bool TryGetOverlay(int currentTickCount, out Color color)
        {
            color = Color.Transparent;
            Update(currentTickCount);
            if (_entries.Count == 0)
            {
                return false;
            }

            float combinedAlpha = 0f;
            for (int i = 0; i < _entries.Count; i++)
            {
                float alpha = GetAlpha(_entries[i], currentTickCount);
                if (alpha <= 0f)
                {
                    continue;
                }

                combinedAlpha = 1f - ((1f - combinedAlpha) * (1f - alpha));
            }

            if (combinedAlpha <= 0f)
            {
                return false;
            }

            color = Color.Black * combinedAlpha;
            return true;
        }

        private bool TryGetLatestEntry(out FadeEntry entry)
        {
            if (_entries.Count > 0)
            {
                entry = _entries[^1];
                return true;
            }

            entry = default;
            return false;
        }

        private static float GetAlpha(in FadeEntry entry, int currentTickCount)
        {
            int elapsed = Math.Max(0, unchecked(currentTickCount - entry.StartedAt));
            float startingAlpha = entry.StartingAlpha / (float)byte.MaxValue;
            if (entry.FadeInMs > 0 && elapsed < entry.FadeInMs)
            {
                float fadeProgress = MathHelper.Clamp((float)elapsed / entry.FadeInMs, 0f, 1f);
                return MathHelper.Lerp(startingAlpha, 1f, fadeProgress);
            }

            int fadeOutStartsAt = entry.GetFadeOutStartsAt();
            if (unchecked(currentTickCount - fadeOutStartsAt) < 0)
            {
                return 1f;
            }

            int fadeOutMs = entry.GetFadeOutDurationMs();
            if (fadeOutMs > 0)
            {
                elapsed = Math.Max(0, unchecked(currentTickCount - fadeOutStartsAt));
                if (elapsed < fadeOutMs)
                {
                    return 1f - MathHelper.Clamp((float)elapsed / fadeOutMs, 0f, 1f);
                }
            }

            return 0f;
        }

        private readonly record struct FadeEntry(
            int FadeInMs,
            int HoldMs,
            int FadeOutMs,
            int StartingAlpha,
            int LayerZ,
            int StartedAt,
            int ForcedFadeOutStartsAt = int.MinValue,
            int ForcedFadeOutMs = -1)
        {
            public int GetFadeOutStartsAt() =>
                ForcedFadeOutStartsAt != int.MinValue
                    ? ForcedFadeOutStartsAt
                    : StartedAt + FadeInMs + HoldMs;

            public int GetFadeOutDurationMs() =>
                ForcedFadeOutMs >= 0
                    ? ForcedFadeOutMs
                    : FadeOutMs;

            public int ExpiresAt => GetFadeOutStartsAt() + GetFadeOutDurationMs();

            public bool HasFadeOutStarted(int currentTickCount) =>
                unchecked(currentTickCount - GetFadeOutStartsAt()) >= 0;

            public FadeEntry WithForcedFadeOut(int forceStartTickCount, int forceFadeOutMs) =>
                this with
                {
                    ForcedFadeOutStartsAt = forceStartTickCount,
                    ForcedFadeOutMs = Math.Max(0, forceFadeOutMs)
                };
        }
    }
}
