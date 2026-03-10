using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator
{
    public class FieldSkillRestrictionEvaluatorTests
    {
        [Fact]
        public void CanUseSkill_BlocksAllSkillsWhenMapDisablesSkillUsage()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Skill;
            var skill = new SkillData { IsMovement = true };

            bool canUse = FieldSkillRestrictionEvaluator.CanUseSkill(fieldLimit, skill);

            Assert.False(canUse);
        }

        [Fact]
        public void CanUseSkill_AllowsOnlyMovementSkillsWhenMoveSkillOnlyIsSet()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Move_Skill_Only;
            var movementSkill = new SkillData { IsMovement = true };
            var attackSkill = new SkillData { IsMovement = false, IsAttack = true };

            Assert.True(FieldSkillRestrictionEvaluator.CanUseSkill(fieldLimit, movementSkill));
            Assert.False(FieldSkillRestrictionEvaluator.CanUseSkill(fieldLimit, attackSkill));
        }

        [Fact]
        public void CanUseSkill_AllowsRegularSkillsWhenNoRelevantFieldLimitIsSet()
        {
            var skill = new SkillData { IsAttack = true };

            bool canUse = FieldSkillRestrictionEvaluator.CanUseSkill(0, skill);

            Assert.True(canUse);
        }
    }
}
