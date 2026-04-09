using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class WildHunterSwallowParity
    {
        public const int DigestDurationMs = 3000;
        public const int WriggleIntervalMs = 500;
        public const int AbsorbOutcomeTimeoutFloorMs = 800;
        public const int AbsorbOutcomeTimeoutCeilingMs = 1800;
        public const int BufferedAbsorbOutcomeLifetimeMs = 1800;

        public static int GetSuspensionDurationMs()
        {
            return WriggleIntervalMs;
        }

        public static int ResolveAbsorbOutcomeTimeoutMs(int actionDurationMs, int fallbackDurationMs = 0)
        {
            int resolvedDuration = actionDurationMs > 0
                ? actionDurationMs
                : fallbackDurationMs;
            int baseDuration = resolvedDuration > 0
                ? resolvedDuration + WriggleIntervalMs
                : WriggleIntervalMs * 2;
            return Math.Clamp(baseDuration, AbsorbOutcomeTimeoutFloorMs, AbsorbOutcomeTimeoutCeilingMs);
        }

        public static int ResolveBufferedAbsorbOutcomeLifetimeMs()
        {
            return BufferedAbsorbOutcomeLifetimeMs;
        }

        public static int AdvanceWriggleSchedule(
            int nextWriggleTime,
            int digestCompleteTime,
            int currentTime,
            Action<int> onPulse)
        {
            while (currentTime >= nextWriggleTime && nextWriggleTime < digestCompleteTime)
            {
                onPulse?.Invoke(nextWriggleTime);
                nextWriggleTime += WriggleIntervalMs;
            }

            return nextWriggleTime;
        }
    }
}
