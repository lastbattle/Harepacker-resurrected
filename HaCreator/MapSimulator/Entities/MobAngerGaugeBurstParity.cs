using System.Collections.Generic;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Interaction;
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

            return nextAllowedTick != int.MinValue && HasReachedTick(currentTick, nextAllowedTick);
        }

        public static string ResolveOwnerEffectPath(string mobTemplateId, string loadedEffectPath)
        {
            string ownerPath = MapleStoryStringPool.ResolveMobAngerGaugeBurstPath(mobTemplateId);
            return !string.IsNullOrWhiteSpace(ownerPath) ? ownerPath : loadedEffectPath;
        }

        public static bool CanRegisterOwnerBurst(
            IReadOnlyList<IDXObject> frames,
            string effectPath)
        {
            return frames != null
                && frames.Count > 0
                && !string.IsNullOrWhiteSpace(effectPath);
        }

        public static bool HasReplayGateElapsed(int currentTick, int startTick, int intervalMs)
        {
            return startTick == int.MinValue
                || intervalMs <= 0
                || unchecked(currentTick - startTick) >= intervalMs;
        }

        private static bool HasReachedTick(int currentTick, int targetTick)
        {
            return unchecked(currentTick - targetTick) >= 0;
        }
    }
}
