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

        [Fact]
        public void GetRestrictionMessage_ReturnsSkillUsageMessageWhenFieldBlocksAllSkills()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Skill;
            var skill = new SkillData { IsAttack = true };

            string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(fieldLimit, skill);

            Assert.Equal("This field forbids skill usage.", message);
        }

        [Fact]
        public void GetRestrictionMessage_ReturnsMovementOnlyMessageWhenNonMovementSkillIsBlocked()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Move_Skill_Only;
            var skill = new SkillData { IsAttack = true };

            string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(fieldLimit, skill);

            Assert.Equal("Only movement skills can be used in this field.", message);
        }

        [Fact]
        public void GetRestrictionMessage_ReturnsMysticDoorMessageWhenFieldBlocksDoor()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Mystic_Door;
            var skill = new SkillData { SkillId = 2311002, Name = "Mystic Door" };

            string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(fieldLimit, skill);

            Assert.Equal("Mystic Door cannot be used in this field.", message);
        }
    }
}
