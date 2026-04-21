using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class BoundJumpParityProfile
    {
        private const int WindWalkSkillId = 11101005;
        private const int WildHunterJaguarJumpSkillId = 33001002;
        private const int NightLordFlashJumpSkillId = 4111006;
        private const int ShadowerFlashJumpSkillId = 4211009;
        private const int DualBladeFlashJumpSkillId = 4321003;
        private const int NightWalkerFlashJumpSkillId = 14101004;
        private const int RocketBoosterSkillId = 35101004;
        private static readonly HashSet<string> ConstrainedType40BoundJumpActionNameMarkers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "doublejump",
                "flash jump",
                "flashjump",
                "archerdoublejump",
                "icedoublejump",
                "spiritjump",
                "swiftphantom",
                "htswiftphantom",
                "demonjump",
                "demonjumpfoward",
                "demonjumpforward",
                "demonfly"
            };

        internal static bool IsDirectBoundJumpSkillId(int skillId, bool includeRocketBoosterSkillId = true)
        {
            if (skillId == WindWalkSkillId
                || skillId == WildHunterJaguarJumpSkillId
                || skillId == NightLordFlashJumpSkillId
                || skillId == ShadowerFlashJumpSkillId
                || skillId == DualBladeFlashJumpSkillId
                || skillId == NightWalkerFlashJumpSkillId)
            {
                return true;
            }

            return includeRocketBoosterSkillId && skillId == RocketBoosterSkillId;
        }

        internal static bool IsGroundedStartDirectBoundJumpSkillId(int skillId)
        {
            return skillId == WindWalkSkillId
                   || skillId == RocketBoosterSkillId;
        }

        internal static bool IsConstrainedType40BoundJumpActionName(string actionName)
        {
            // Keep non-explicit type-40 ownership on the rechecked WZ-authored
            // bound-jump movement profile set.
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string marker in ConstrainedType40BoundJumpActionNameMarkers)
            {
                if (ActionTextContains(actionName, marker))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsConstrainedType40BoundJumpSkillId(int skillId)
        {
            // Keep constrained type-40 ownership on the rechecked non-direct
            // bound-jump profiles even when action rows are missing at runtime.
            return IsConstrainedType40IceDoubleJumpSkillId(skillId)
                   || skillId == 13101004
                   || skillId is 3101003
                       or 3201003
                   || skillId is 23001002
                       or 24001002
                       or 30010183
                       or 30010184
                       or 30010186
                       or 5081003
                       or 51001003;
        }

        private static bool IsConstrainedType40IceDoubleJumpSkillId(int skillId)
        {
            // WZ-authoring keeps this family on type-40 `iceDoubleJump` rows across
            // beginner/cygnus/aran/resistance starter variants.
            return skillId is 1098
                or 11098
                or 10001098
                or 20001098
                or 20011098
                or 20021098
                or 30001098
                or 30011098
                or 50001098;
        }

        private static bool ActionTextContains(string actionName, string value)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && !string.IsNullOrWhiteSpace(value)
                   && actionName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
