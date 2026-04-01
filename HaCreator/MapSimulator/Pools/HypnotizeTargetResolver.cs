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

            MobItem currentTarget = FindByPoolId(activeMobs, currentTargetId);
            if (IsEligibleTarget(source, currentTarget, allowEncounterTargets: true))
            {
                return currentTarget;
            }

            MobItem sameTeamStandardTarget = FindNearestTarget(source, activeMobs, maxDistance, requireMatchingTeam: true, allowEncounterTargets: false);
            if (sameTeamStandardTarget != null)
            {
                return sameTeamStandardTarget;
            }

            MobItem sameTeamEncounterTarget = FindNearestTarget(source, activeMobs, maxDistance, requireMatchingTeam: true, allowEncounterTargets: true);
            if (sameTeamEncounterTarget != null)
            {
                return sameTeamEncounterTarget;
            }

            MobItem standardTarget = FindNearestTarget(source, activeMobs, maxDistance, requireMatchingTeam: false, allowEncounterTargets: false);
            if (standardTarget != null)
            {
                return standardTarget;
            }

            return FindNearestTarget(source, activeMobs, maxDistance, requireMatchingTeam: false, allowEncounterTargets: true);
        }

        private static MobItem FindNearestTarget(
            MobItem source,
            IReadOnlyList<MobItem> activeMobs,
            float maxDistance,
            bool requireMatchingTeam,
            bool allowEncounterTargets)
        {
            MobItem nearest = null;
            float nearestDistanceSq = maxDistance * maxDistance;

            for (int i = 0; i < activeMobs.Count; i++)
            {
                MobItem candidate = activeMobs[i];
                if (!IsEligibleTarget(source, candidate, allowEncounterTargets))
                {
                    continue;
                }

                if (requireMatchingTeam && !SharesTeam(source, candidate))
                {
                    continue;
                }

                float dx = candidate.CurrentX - source.CurrentX;
                float dy = candidate.CurrentY - source.CurrentY;
                float distanceSq = dx * dx + dy * dy;
                if (distanceSq >= nearestDistanceSq)
                {
                    continue;
                }

                nearestDistanceSq = distanceSq;
                nearest = candidate;
            }

            return nearest;
        }

        private static MobItem FindByPoolId(IReadOnlyList<MobItem> activeMobs, int poolId)
        {
            if (poolId <= 0)
            {
                return null;
            }

            for (int i = 0; i < activeMobs.Count; i++)
            {
                MobItem candidate = activeMobs[i];
                if (candidate?.PoolId == poolId)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsEligibleTarget(MobItem source, MobItem candidate, bool allowEncounterTargets)
        {
            if (source == null ||
                candidate == null ||
                ReferenceEquals(source, candidate) ||
                candidate.AI == null ||
                candidate.AI.IsDead ||
                candidate.AI.IsHypnotized)
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

        private static bool SharesTeam(MobItem source, MobItem candidate)
        {
            int? sourceTeam = source?.MobInstance?.Team;
            int? candidateTeam = candidate?.MobInstance?.Team;
            return sourceTeam.HasValue
                && candidateTeam.HasValue
                && sourceTeam.Value == candidateTeam.Value;
        }
    }
}
