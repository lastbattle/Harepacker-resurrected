using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

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

        public static bool ShouldRegisterClientOwnedAttackTileOverlay(
            ActiveSummon summon,
            int skillLevel,
            int ownerCharacterLevel)
        {
            SkillAnimation zoneAnimation = summon?.SkillData?.ZoneEffect?.ResolveAnimationVariant(
                                            skillLevel > 0 ? skillLevel : summon.Level,
                                            Math.Max(1, ownerCharacterLevel),
                                            summon?.SkillData?.MaxLevel ?? 0)
                                        ?? summon?.SkillData?.ZoneAnimation;
            return summon != null
                   && SummonClientPostEffectRules.ShouldRegisterAttackTileOverlay(
                       summon.SkillId,
                       zoneAnimation);
        }

        public static bool ShouldRegisterClientOwnedReactiveAttackChainEffect(ActiveSummon summon)
        {
            return summon != null
                   && SummonClientPostEffectRules.ShouldRegisterReactiveAttackChainEffect(
                       summon.SkillId,
                       summon.SkillData);
        }

        internal static int ResolveClientOwnedPostAttackEffectDelayMs(ActiveSummon summon)
        {
            return SummonClientPostEffectRules.ResolvePostAttackEffectDelayMs(
                summon?.SkillData,
                summon?.CurrentAnimationBranchName);
        }

        internal static (Vector2 Source, Vector2 Target) ResolveClientOwnedReactiveAttackChainEndpoints(
            ActiveSummon summon,
            Rectangle targetHitbox,
            bool facingRight)
        {
            return summon == null
                ? (Vector2.Zero, Vector2.Zero)
                : SummonClientPostEffectRules.ResolveReactiveAttackChainEndpoints(
                    new Vector2(summon.PositionX, summon.PositionY),
                    targetHitbox,
                    facingRight);
        }

        internal static Rectangle BuildClientOwnedAttackTileOverlayArea(
            Vector2 anchor,
            ActiveSummon summon,
            string branchName = null)
        {
            return SummonClientPostEffectRules.BuildAttackTileOverlayArea(
                anchor,
                summon?.SkillData,
                branchName);
        }

        internal static IEnumerable<Point> EnumerateClientOwnedTileOverlayOrigins(
            Rectangle area,
            int tileWidth,
            int tileHeight,
            int effectDistance)
        {
            if (area.Width <= 0
                || area.Height <= 0
                || tileWidth <= 0
                || tileHeight <= 0)
            {
                yield break;
            }

            int horizontalStep = effectDistance > 0
                ? Math.Max(1, effectDistance)
                : tileWidth;
            int verticalStep = tileHeight;

            for (int worldY = area.Top; worldY < area.Bottom; worldY += verticalStep)
            {
                for (int worldX = area.Left; worldX < area.Right; worldX += horizontalStep)
                {
                    yield return new Point(worldX, worldY);
                }
            }
        }

        internal static int ResolveClientOwnedTileOverlayEffectDistance(
            ActiveSummon summon,
            int skillLevel,
            int ownerCharacterLevel)
        {
            return summon?.SkillData?.ZoneEffect?.ResolveEffectDistanceVariant(
                       skillLevel > 0 ? skillLevel : summon.Level,
                       Math.Max(1, ownerCharacterLevel))
                   ?? 0;
        }

        internal static string ResolveClientOwnedTileOverlayAnimationPath(
            ActiveSummon summon,
            int skillLevel,
            int ownerCharacterLevel)
        {
            SkillData skill = summon?.SkillData;
            return skill?.ZoneEffect?.ResolveTileUolPath(
                skillLevel > 0 ? skillLevel : summon?.Level ?? 1,
                Math.Max(1, ownerCharacterLevel))
                   ?? skill?.ZoneEffect?.ResolveAnimationVariantPath(
                       skillLevel > 0 ? skillLevel : summon?.Level ?? 1,
                       Math.Max(1, ownerCharacterLevel),
                       skill?.MaxLevel ?? 0);
        }

        internal static string ResolveClientOwnedReactiveAttackChainAnimationPath(
            ActiveSummon summon,
            int ownerCharacterLevel = 1,
            bool flip = false)
        {
            SkillData skill = summon?.SkillData;
            if (SummonClientPostEffectRules.IsReactiveAttackChainSkill(summon?.SkillId ?? 0))
            {
                string resolvedBallAnimationPath = skill?.Projectile?.ResolveGetBallLikeUolPath(
                    summon.Level,
                    Math.Max(1, ownerCharacterLevel),
                    flip,
                    skill?.MaxLevel ?? 0);
                if (!string.IsNullOrWhiteSpace(resolvedBallAnimationPath))
                {
                    return resolvedBallAnimationPath;
                }
            }

            return skill?.Projectile?.ResolveGetBallLikeUolPath(
                       summon?.Level ?? 1,
                       Math.Max(1, ownerCharacterLevel),
                       flip,
                       skill?.MaxLevel ?? 0)
                   ?? skill?.Projectile?.AnimationPath;
        }

        internal static Vector2 ResolvePacketOwnedMobAttackHitAnchor(
            Rectangle hitbox,
            Vector2 summonPosition,
            MobAnimationSet.AttackInfoMetadata attackInfo,
            bool facingRight,
            Random random,
            int hitFrameIndex = 0,
            int? hitAnimationSourceFrameIndex = null)
        {
            int metadataFrameIndex = hitAnimationSourceFrameIndex.HasValue
                ? hitAnimationSourceFrameIndex.Value + Math.Max(0, hitFrameIndex)
                : attackInfo?.ResolveHitAnimationMetadataFrameIndex(hitFrameIndex) ?? hitFrameIndex;
            bool hitAttach = attackInfo?.ResolveHitAttach(metadataFrameIndex) == true;
            bool facingAttach = attackInfo?.ResolveFacingAttach(metadataFrameIndex) == true;
            if (hitAttach)
            {
                return ResolvePacketOwnedAttachedHitPosition(
                    summonPosition,
                    ResolvePacketOwnedAuthoredHitOffset(attackInfo),
                    facingAttach,
                    facingRight);
            }

            if (!hitbox.IsEmpty)
            {
                return ResolvePacketOwnedDetachedMobAttackHitAnchor(hitbox, summonPosition, attackInfo, random);
            }

            if (attackInfo?.HasRangeOrigin == true)
            {
                return ResolvePacketOwnedAttachedHitPosition(
                    summonPosition,
                    ResolvePacketOwnedAuthoredHitOffset(attackInfo),
                    facingAttach,
                    facingRight);
            }

            return summonPosition;
        }

        internal static Vector2 ResolvePacketOwnedDetachedMobAttackHitAnchor(
            Rectangle hitbox,
            Vector2 summonPosition,
            MobAnimationSet.AttackInfoMetadata attackInfo,
            Random random)
        {
            if (!hitbox.IsEmpty)
            {
                Random resolvedRandom = random ?? Random.Shared;
                return new Vector2(
                    hitbox.Left + resolvedRandom.Next(Math.Max(1, hitbox.Width)),
                    hitbox.Top + resolvedRandom.Next(Math.Max(1, hitbox.Height)));
            }

            if (attackInfo?.HasRangeOrigin == true)
            {
                return ResolvePacketOwnedAttachedHitPosition(
                    summonPosition,
                    ResolvePacketOwnedAuthoredHitOffset(attackInfo),
                    mirrorOffsetWithFacing: false,
                    facingRight: true);
            }

            return summonPosition;
        }

        internal static Vector2 ResolvePacketOwnedAuthoredHitOffset(MobAnimationSet.AttackInfoMetadata attackInfo)
        {
            if (attackInfo?.HasRangeOrigin != true)
            {
                return Vector2.Zero;
            }

            return new Vector2(attackInfo.RangeOrigin.X, attackInfo.RangeOrigin.Y);
        }

        internal static Vector2 ResolvePacketOwnedAttachedHitPosition(
            Vector2 summonPosition,
            Vector2 authoredOffset,
            bool mirrorOffsetWithFacing,
            bool facingRight)
        {
            if (!mirrorOffsetWithFacing || facingRight)
            {
                return summonPosition + authoredOffset;
            }

            return summonPosition + new Vector2(-authoredOffset.X, authoredOffset.Y);
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
