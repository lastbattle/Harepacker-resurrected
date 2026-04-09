using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Pools
{
    internal static class PacketOwnedSummonUpdateRules
    {
        private const int ClientConfirmedPhoenixLegacySkillId = 3121006;
        private const int ClientConfirmedPhoenixCurrentSkillId = 3120010;
        private const int ClientConfirmedFrostpreyLegacySkillId = 3221005;
        private const int ClientConfirmedFrostpreyCurrentSkillId = 3211005;
        private const int ClientConfirmedShadowMesoReactiveSkillId = 4111007;
        private const float ClientConfirmedReactiveChainSourceOffsetX = 25f;
        private const float ClientConfirmedReactiveChainSourceOffsetY = -25f;
        private const float ClientConfirmedReactiveChainMaxDistance = 600f;
        private const int ClientConfirmedReactiveChainTargetInset = 10;

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

        public static bool ResolvePacketAttackFacingRight(
            ActiveSummon summon,
            byte moveActionRaw,
            bool packetFacingLeft,
            bool fallbackFacingRight)
        {
            if (ShouldUseMoveActionFacingForAttack(summon)
                && moveActionRaw != 0)
            {
                return (moveActionRaw & 1) == 0;
            }

            return !packetFacingLeft;
        }

        public static bool ShouldRegisterClientOwnedAttackTileOverlay(ActiveSummon summon)
        {
            if (summon?.SkillData?.ZoneAnimation?.Frames.Count <= 0)
            {
                return false;
            }

            return summon.SkillId == ClientConfirmedPhoenixLegacySkillId
                   || summon.SkillId == ClientConfirmedPhoenixCurrentSkillId
                   || summon.SkillId == ClientConfirmedFrostpreyLegacySkillId
                   || summon.SkillId == ClientConfirmedFrostpreyCurrentSkillId;
        }

        public static bool ShouldRegisterClientOwnedReactiveAttackChainEffect(ActiveSummon summon)
        {
            return summon?.SkillId == ClientConfirmedShadowMesoReactiveSkillId
                   && HasClientOwnedReactiveAttackChainVisual(summon.SkillData);
        }

        internal static (Vector2 Source, Vector2 Target) ResolveClientOwnedReactiveAttackChainEndpoints(
            ActiveSummon summon,
            Rectangle targetHitbox,
            bool facingRight)
        {
            if (summon == null)
            {
                return (Vector2.Zero, Vector2.Zero);
            }

            Vector2 sourceAnchor = new(summon.PositionX, summon.PositionY + ClientConfirmedReactiveChainSourceOffsetY);
            Vector2 chainSource = new(
                sourceAnchor.X + (facingRight ? ClientConfirmedReactiveChainSourceOffsetX : -ClientConfirmedReactiveChainSourceOffsetX),
                sourceAnchor.Y);
            Vector2 chainTarget = sourceAnchor;

            if (targetHitbox.Width > 0 && targetHitbox.Height > 0)
            {
                float centerX = (targetHitbox.Left + targetHitbox.Right) * 0.5f;
                float insetLeft = targetHitbox.Left + ClientConfirmedReactiveChainTargetInset;
                float insetRight = targetHitbox.Right - ClientConfirmedReactiveChainTargetInset;
                float targetX = centerX < summon.PositionX
                    ? Math.Max(centerX, insetRight)
                    : Math.Min(centerX, insetLeft);
                float targetY = MathHelper.Clamp(sourceAnchor.Y, targetHitbox.Top, targetHitbox.Bottom);
                chainTarget = new Vector2(targetX, targetY);

                Vector2 delta = chainTarget - sourceAnchor;
                float distance = delta.Length();
                if (distance > ClientConfirmedReactiveChainMaxDistance && distance > 0f)
                {
                    chainTarget = sourceAnchor + delta / distance * ClientConfirmedReactiveChainMaxDistance;
                }
            }

            return (chainSource, chainTarget);
        }

        internal static Rectangle BuildClientOwnedAttackTileOverlayArea(Vector2 anchor, ActiveSummon summon)
        {
            int left = summon?.SkillData?.SummonAttackRangeLeft ?? 0;
            int right = summon?.SkillData?.SummonAttackRangeRight ?? 0;
            int top = summon?.SkillData?.SummonAttackRangeTop ?? 0;
            int bottom = (summon?.SkillData?.SummonAttackRangeBottom ?? 0) + 100;

            int x = (int)MathF.Round(anchor.X) + left;
            int y = (int)MathF.Round(anchor.Y) + top;
            int width = Math.Max(1, right - left);
            int height = Math.Max(1, bottom - top);
            return new Rectangle(x, y, width, height);
        }

        public static int ClearAttachedHitEffects<T>(
            IList<T> hitEffects,
            int summonObjectId,
            Func<T, int> attachedSummonObjectIdSelector)
        {
            if (hitEffects == null
                || summonObjectId <= 0
                || attachedSummonObjectIdSelector == null)
            {
                return 0;
            }

            int removedCount = 0;
            for (int index = hitEffects.Count - 1; index >= 0; index--)
            {
                T effect = hitEffects[index];
                if (effect is null || attachedSummonObjectIdSelector(effect) != summonObjectId)
                {
                    continue;
                }

                hitEffects.RemoveAt(index);
                removedCount++;
            }

            return removedCount;
        }

        private static bool ShouldUseMoveActionFacingForAttack(ActiveSummon summon)
        {
            return SummonRuntimeRules.UsesReactiveDamageTriggerSummon(summon?.SkillData);
        }

        private static bool HasClientOwnedReactiveAttackChainVisual(SkillData skillData)
        {
            if (skillData == null)
            {
                return false;
            }

            if (skillData.SummonProjectileAnimations?.Count > 0)
            {
                return true;
            }

            return skillData.Projectile?.Animation?.Frames.Count > 0;
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
