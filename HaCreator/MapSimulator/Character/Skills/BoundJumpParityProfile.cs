using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class BoundJumpParityProfile
    {
        internal static bool IsConstrainedType40BoundJumpActionName(string actionName)
        {
            // Keep non-explicit type-40 ownership on the rechecked WZ-authored
            // bound-jump movement profile set.
            return ActionTextContains(actionName, "doublejump")
                   || ActionTextContains(actionName, "flash jump")
                   || ActionTextContains(actionName, "archerdoublejump")
                   || ActionTextContains(actionName, "icedoublejump")
                   || ActionTextContains(actionName, "spiritjump")
                   || ActionTextContains(actionName, "swiftphantom")
                   || ActionTextContains(actionName, "demonjump")
                   || ActionTextContains(actionName, "demonfly");
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
