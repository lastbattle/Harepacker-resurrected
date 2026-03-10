using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Applies fieldLimit-based restrictions to local skill usage.
    /// </summary>
    public static class FieldSkillRestrictionEvaluator
    {
        public static bool CanUseSkill(long fieldLimit, SkillData skill)
        {
            if (skill == null)
                return false;

            if (FieldLimitType.Unable_To_Use_Skill.Check(fieldLimit))
                return false;

            if (FieldLimitType.Move_Skill_Only.Check(fieldLimit) && !skill.IsMovement)
                return false;

            return true;
        }
    }
}
