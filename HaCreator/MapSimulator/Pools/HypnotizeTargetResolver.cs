using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;

namespace HaCreator.MapSimulator.Pools
{
    internal static class HypnotizeTargetResolver
    {
        private const float MinimumHypnotizeTargetRange = 320f;

        public static MobItem ResolveTarget(MobItem source, IReadOnlyList<MobItem> activeMobs)
        {
            if (source?.AI == null || source.AI.IsDead || activeMobs == null || activeMobs.Count == 0)
            {
                return null;
            }

            float maxDistance = Math.Max(MinimumHypnotizeTargetRange, source.AI.AggroRange);
            int currentTargetId = source.AI.ExternalTargetSource == MobExternalTargetSource.Hypnotize
                ? source.AI.Target.TargetId
                : 0;
            int? sourceTeam = source.MobInstance?.Team;
            float maxDistanceSq = maxDistance * maxDistance;
            MobItem bestTarget = null;
            int bestPriorityTier = int.MaxValue;
            float bestDistanceSq = maxDistanceSq;

            for (int i = 0; i < activeMobs.Count; i++)
            {
                MobItem candidate = activeMobs[i];
                if (!IsEligibleTarget(source, candidate, allowEncounterTargets: true))
                {
                    continue;
                }

                float dx = candidate.CurrentX - source.CurrentX;
                float dy = candidate.CurrentY - source.CurrentY;
                float distanceSq = dx * dx + dy * dy;
                if (distanceSq > maxDistanceSq)
                {
                    continue;
                }

                int candidatePriorityTier = ResolvePriorityTier(sourceTeam, candidate.MobInstance?.Team, candidate.UsesMobCombatLane);
                if (!ShouldPreferCandidate(
                    currentTargetId,
                    bestTarget?.PoolId ?? 0,
                    currentPriorityTier: bestPriorityTier,
                    candidatePriorityTier: candidatePriorityTier,
                    currentDistanceSq: bestDistanceSq,
                    candidateDistanceSq: distanceSq,
                    candidateId: candidate.PoolId))
                {
                    continue;
                }

                bestPriorityTier = candidatePriorityTier;
                bestDistanceSq = distanceSq;
                bestTarget = candidate;
            }

            return bestTarget;
        }

        private static bool IsEligibleTarget(MobItem source, MobItem candidate, bool allowEncounterTargets)
        {
            if (source == null ||
                candidate == null ||
                ReferenceEquals(source, candidate) ||
                candidate.AI == null ||
                candidate.AI.IsDead ||
                candidate.AI.IsHypnotized ||
                candidate.AI.IsDoomed)
            {
                return false;
            }

            if (!allowEncounterTargets && candidate.UsesMobCombatLane)
            {
                return false;
            }

            if (!source.UsesMobCombatLane && candidate.IsProtectedFromPlayerDamage)
            {
                return false;
            }

            return true;
        }

        internal static int ResolvePriorityTier(int? sourceTeam, int? candidateTeam, bool usesEncounterTarget)
        {
            if (sourceTeam.HasValue && candidateTeam.HasValue && sourceTeam.Value == candidateTeam.Value)
            {
                return usesEncounterTarget ? 0 : 1;
            }

            if (sourceTeam.HasValue)
            {
                return usesEncounterTarget ? 2 : 3;
            }

            return usesEncounterTarget ? 0 : 1;
        }

        internal static bool ShouldPreferCandidate(
            int currentTargetId,
            int currentBestId,
            int currentPriorityTier,
            int candidatePriorityTier,
            float currentDistanceSq,
            float candidateDistanceSq,
            int candidateId)
        {
            if (candidatePriorityTier != currentPriorityTier)
            {
                return candidatePriorityTier < currentPriorityTier;
            }

            if (currentTargetId > 0)
            {
                if (candidateId == currentTargetId)
                {
                    return true;
                }

                if (currentBestId == currentTargetId)
                {
                    return false;
                }
            }

            if (candidateDistanceSq != currentDistanceSq)
            {
                return candidateDistanceSq < currentDistanceSq;
            }

            return currentBestId <= 0 || candidateId < currentBestId;
        }
    }
}
