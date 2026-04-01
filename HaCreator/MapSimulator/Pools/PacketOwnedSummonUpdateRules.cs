using HaCreator.MapSimulator.Character.Skills;
using System;

namespace HaCreator.MapSimulator.Pools
{
    internal static class PacketOwnedSummonUpdateRules
    {
        public static bool ShouldResolveBodyContact(
            ActiveSummon summon,
            bool registersAsPuppet,
            int currentTime,
            int bodyContactCooldownMs)
        {
            return summon != null
                   && !summon.IsPendingRemoval
                   && summon.HitPeriodRemainingMs == 0
                   && registersAsPuppet
                   && currentTime - summon.LastBodyContactTime >= bodyContactCooldownMs;
        }

        public static bool ShouldTriggerExpirySelfDestruct(ActiveSummon summon, int currentTime)
        {
            return summon?.SkillData?.SelfDestructMinion == true
                   && !summon.ExpiryActionTriggered
                   && summon.HasReachedNaturalExpiry(currentTime);
        }

        public static (int RemovalAnimationStartTime, int PendingRemovalTime) BuildSelfDestructRemovalSchedule(
            int currentTime,
            int attackWindowMs,
            int removalWindowMs)
        {
            int safeAttackWindowMs = Math.Max(0, attackWindowMs);
            int safeRemovalWindowMs = Math.Max(1, removalWindowMs);
            int removalAnimationStartTime = currentTime + safeAttackWindowMs;
            return (removalAnimationStartTime, removalAnimationStartTime + safeRemovalWindowMs);
        }
    }
}
