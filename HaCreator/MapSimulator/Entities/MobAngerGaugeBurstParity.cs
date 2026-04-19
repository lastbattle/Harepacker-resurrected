using System.Collections.Generic;
using HaCreator.MapSimulator.AI;
using HaSharedLibrary.Render.DX;

namespace HaCreator.MapSimulator.Entities
{
    internal static class MobAngerGaugeBurstParity
    {
        private const int MinimumFrameDelayMs = 10;

        public static int ResolveRepeatIntervalMs(IReadOnlyList<IDXObject> frames)
        {
            return ResolveRepeatIntervalMs(frames, specialAttackAfterMs: 0);
        }

        public static int ResolveRepeatIntervalMs(IReadOnlyList<IDXObject> frames, int specialAttackAfterMs)
        {
            if (specialAttackAfterMs > 0)
            {
                return specialAttackAfterMs;
            }

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

        public static int ResolveRepeatIntervalMs(
            IReadOnlyList<IDXObject> frames,
            MobAttackEntry currentAttack,
            int configuredSpecialAttackAfterMs)
        {
            if (currentAttack?.IsSpecialAttack == true && currentAttack.AttackAfter > 0)
            {
                return currentAttack.AttackAfter;
            }

            if (currentAttack?.IsSpecialAttack == true)
            {
                return ResolveRepeatIntervalMs(frames, configuredSpecialAttackAfterMs);
            }

            // Outside the active owner lane, cadence falls back to authored burst-frame timing.
            // This avoids carrying stale special-attack owner timing across state transitions.
            return ResolveRepeatIntervalMs(frames, specialAttackAfterMs: 0);
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
