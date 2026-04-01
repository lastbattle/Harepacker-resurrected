using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class WildHunterSwallowParity
    {
        public const int DigestDurationMs = 3000;
        public const int WriggleIntervalMs = 500;

        public static int GetSuspensionDurationMs()
        {
            return WriggleIntervalMs;
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
