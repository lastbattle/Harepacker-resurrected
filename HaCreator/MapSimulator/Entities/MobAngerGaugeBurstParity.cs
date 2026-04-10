using System.Collections.Generic;
using HaSharedLibrary.Render.DX;

namespace HaCreator.MapSimulator.Entities
{
    internal static class MobAngerGaugeBurstParity
    {
        private const int MinimumFrameDelayMs = 10;

        public static int ResolveRepeatIntervalMs(IReadOnlyList<IDXObject> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int totalDurationMs = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDurationMs += frames[i]?.Delay > 0
                    ? frames[i].Delay
                    : MinimumFrameDelayMs;
            }

            return totalDurationMs;
        }

        public static bool ShouldRegisterBurst(
            int currentChargeCount,
            int chargeTarget,
            int previousChargeCount,
            int nextAllowedTick,
            int currentTick)
        {
            if (chargeTarget <= 0 || currentChargeCount < chargeTarget)
            {
                return false;
            }

            if (previousChargeCount < 0)
            {
                return false;
            }

            if (currentChargeCount > previousChargeCount)
            {
                return true;
            }

            return nextAllowedTick != int.MinValue && currentTick >= nextAllowedTick;
        }
    }
}
