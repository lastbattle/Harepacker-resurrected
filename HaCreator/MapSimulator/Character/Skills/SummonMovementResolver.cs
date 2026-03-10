using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character.Skills
{
    public readonly record struct SummonMovementProfile(
        int MoveAbility,
        SummonMovementStyle Style,
        float SpawnDistanceX);

    public static class SummonMovementResolver
    {
        private static readonly HashSet<int> StationarySpawnFarSkills = new()
        {
            3111002,
            3211002,
            13111004,
            33111003
        };

        private static readonly HashSet<int> StationarySpawnBehindSkills = new()
        {
            4341006
        };

        private static readonly HashSet<int> StationarySpawnNearBehindSkills = new()
        {
            33101008
        };

        // `is_summon_octopus_skill` confirms 5211001 as one of the fixed-placement octopus summons.
        private static readonly HashSet<int> StationaryOctopusSkills = new()
        {
            5211001
        };

        public static SummonMovementProfile Resolve(int skillId, IEnumerable<string> branchNames)
        {
            var normalizedBranches = new HashSet<string>(
                branchNames?
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim().ToLowerInvariant())
                    ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            int moveAbility = ResolveMoveAbility(skillId, normalizedBranches);
            return new SummonMovementProfile(
                moveAbility,
                ResolveStyle(moveAbility),
                ResolveSpawnDistanceX(skillId));
        }

        public static int ResolveMoveAbility(int skillId, IReadOnlyCollection<string> branchNames)
        {
            if (IsStationaryPlacementSkill(skillId))
                return 0;

            bool hasMove = branchNames.Contains("move");
            bool hasWalk = branchNames.Contains("walk");
            bool hasFly = branchNames.Contains("fly");
            bool hasStand = branchNames.Contains("stand");

            if (hasMove)
                return 2;

            if (hasWalk)
                return 1;

            if (hasFly && hasStand)
                return 4;

            if (hasFly)
                return 5;

            if (hasStand)
                return 1;

            return 0;
        }

        public static SummonMovementStyle ResolveStyle(int moveAbility)
        {
            return moveAbility switch
            {
                1 => SummonMovementStyle.GroundFollow,
                2 => SummonMovementStyle.DriftAroundOwner,
                4 => SummonMovementStyle.HoverFollow,
                5 => SummonMovementStyle.HoverAroundAnchor,
                _ => SummonMovementStyle.Stationary
            };
        }

        public static float ResolveSpawnDistanceX(int skillId)
        {
            if (StationaryOctopusSkills.Contains(skillId))
                return 45f;

            if (StationarySpawnBehindSkills.Contains(skillId))
                return -50f;

            if (StationarySpawnNearBehindSkills.Contains(skillId))
                return -30f;

            if (StationarySpawnFarSkills.Contains(skillId))
                return 200f;

            return 50f;
        }

        public static Vector2 ResolveSpawnPosition(
            SummonMovementStyle style,
            float spawnDistanceX,
            Vector2 playerPosition,
            bool facingRight)
        {
            float facingDistanceX = facingRight ? spawnDistanceX : -spawnDistanceX;

            return style switch
            {
                SummonMovementStyle.GroundFollow => new Vector2(
                    playerPosition.X + (facingRight ? 70f : -70f),
                    playerPosition.Y - 25f),
                SummonMovementStyle.HoverFollow => new Vector2(
                    playerPosition.X + (facingRight ? 60f : -60f),
                    playerPosition.Y - 65f),
                SummonMovementStyle.DriftAroundOwner => new Vector2(
                    playerPosition.X + (facingRight ? 45f : -45f),
                    playerPosition.Y - 50f),
                SummonMovementStyle.HoverAroundAnchor => new Vector2(
                    playerPosition.X + (facingRight ? 80f : -80f),
                    playerPosition.Y - 60f),
                _ => new Vector2(
                    playerPosition.X + facingDistanceX,
                    playerPosition.Y - 25f)
            };
        }

        public static bool IsAnchorBound(SummonMovementStyle style)
        {
            return style == SummonMovementStyle.Stationary
                || style == SummonMovementStyle.HoverAroundAnchor;
        }

        private static bool IsStationaryPlacementSkill(int skillId)
        {
            return StationarySpawnFarSkills.Contains(skillId)
                || StationarySpawnBehindSkills.Contains(skillId)
                || StationarySpawnNearBehindSkills.Contains(skillId)
                || StationaryOctopusSkills.Contains(skillId)
                || skillId == 35111002;
        }
    }
}
