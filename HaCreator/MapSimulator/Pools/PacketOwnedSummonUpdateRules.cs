using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Pools
{
    internal static class PacketOwnedSummonUpdateRules
    {
        public static SummonMovementStyle ResolveEffectiveMovementStyle(ActiveSummon summon)
        {
            if (summon == null)
            {
                return SummonMovementStyle.Stationary;
            }

            // Prefer loaded WZ summon movement metadata when available so packet-owned passive
            // updates stay aligned with the authored summon branch profile.
            SummonMovementStyle wzProfileStyle = summon.SkillData?.SummonMovementStyle ?? SummonMovementStyle.Stationary;
            if (wzProfileStyle != SummonMovementStyle.Stationary)
            {
                return wzProfileStyle;
            }

            if (summon.MovementStyle != SummonMovementStyle.Stationary)
            {
                return summon.MovementStyle;
            }

            return SummonMovementResolver.ResolveStyle(summon.MoveAbility);
        }

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

        public static Vector2 ResolvePassiveTargetPosition(
            ActiveSummon summon,
            Vector2? ownerPosition,
            bool ownerFacingRight,
            int currentTime)
        {
            if (summon == null)
            {
                return Vector2.Zero;
            }

            float elapsedSeconds = Math.Max(0f, (currentTime - summon.StartTime) / 1000f);
            Vector2 resolvedOwnerPosition = ownerPosition ?? new Vector2(summon.AnchorX, summon.AnchorY);
            SummonMovementStyle movementStyle = ResolveEffectiveMovementStyle(summon);

            return movementStyle switch
            {
                SummonMovementStyle.GroundFollow => new Vector2(
                    resolvedOwnerPosition.X + (ownerFacingRight ? 70f : -70f),
                    resolvedOwnerPosition.Y - 25f),
                SummonMovementStyle.HoverFollow => new Vector2(
                    resolvedOwnerPosition.X + (ownerFacingRight ? 60f : -60f) + MathF.Sin(elapsedSeconds * 2.1f + summon.ObjectId) * 14f,
                    resolvedOwnerPosition.Y - 65f + MathF.Cos(elapsedSeconds * 3.3f + summon.ObjectId * 0.5f) * 8f),
                SummonMovementStyle.DriftAroundOwner => new Vector2(
                    resolvedOwnerPosition.X + MathF.Cos(elapsedSeconds * 1.6f + summon.ObjectId) * 65f,
                    resolvedOwnerPosition.Y - 52f + MathF.Sin(elapsedSeconds * 2.8f + summon.ObjectId * 0.75f) * 18f),
                SummonMovementStyle.HoverAroundAnchor => new Vector2(
                    summon.AnchorX + MathF.Sin(elapsedSeconds * 1.3f + summon.ObjectId) * 80f,
                    summon.AnchorY - 35f + MathF.Cos(elapsedSeconds * 2.0f + summon.ObjectId * 0.35f) * 16f),
                _ => new Vector2(summon.AnchorX, summon.AnchorY)
            };
        }

        public static Vector2 ResolvePassiveStepPosition(
            ActiveSummon summon,
            Vector2 targetPosition,
            float deltaTimeSeconds)
        {
            if (summon == null)
            {
                return targetPosition;
            }

            if (deltaTimeSeconds <= 0f)
            {
                return targetPosition;
            }

            return ResolveEffectiveMovementStyle(summon) switch
            {
                SummonMovementStyle.GroundFollow => new Vector2(
                    MoveTowards(summon.PositionX, targetPosition.X, 220f * deltaTimeSeconds),
                    MoveTowards(summon.PositionY, targetPosition.Y, 260f * deltaTimeSeconds)),
                SummonMovementStyle.HoverFollow => new Vector2(
                    MoveTowards(summon.PositionX, targetPosition.X, 260f * deltaTimeSeconds),
                    MoveTowards(summon.PositionY, targetPosition.Y, 300f * deltaTimeSeconds)),
                _ => targetPosition
            };
        }

        public static bool ShouldUseAnchorBoundPassiveFallback(ActiveSummon summon)
        {
            return SummonMovementResolver.IsAnchorBound(ResolveEffectiveMovementStyle(summon));
        }

        public static bool ShouldEmitPassiveEffectFromMotion(ActiveSummon summon)
        {
            return ShouldUseAnchorBoundPassiveFallback(summon);
        }

        public static SummonActorState ResolveIdleActorState(ActiveSummon summon, int currentTime, int teslaCoilSkillId)
        {
            if (summon?.SkillData == null)
            {
                return SummonActorState.Idle;
            }

            if (summon.SkillId == teslaCoilSkillId
                && summon.SkillData.SummonAttackPrepareAnimation?.Frames.Count > 0
                && (summon.TeslaCoilState == 1
                    || summon.TeslaCoilState == 2
                    || summon.LastAttackAnimationStartTime == int.MinValue))
            {
                return SummonActorState.Prepare;
            }

            if (summon.ActorState == SummonActorState.Spawn
                && IsSpawnAnimationActive(summon, currentTime))
            {
                return SummonActorState.Spawn;
            }

            return SummonActorState.Idle;
        }

        private static bool IsSpawnAnimationActive(ActiveSummon summon, int currentTime)
        {
            SkillAnimation spawnAnimation = summon?.SkillData?.SummonSpawnAnimation;
            if (spawnAnimation?.Frames.Count <= 0)
            {
                return false;
            }

            int spawnDuration = spawnAnimation.TotalDuration;
            if (spawnDuration <= 0)
            {
                spawnAnimation.CalculateDuration();
                spawnDuration = spawnAnimation.TotalDuration;
            }

            return spawnDuration > 0
                   && currentTime - summon.StartTime < spawnDuration;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
            {
                return target;
            }

            return current + MathF.Sign(target - current) * maxDelta;
        }
    }
}
