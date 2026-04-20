using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public class PlayerSkillStateRestrictionEvaluatorMeleeAirborneParityTests
    {
        [Theory]
        [InlineData(5111005, true)]
        [InlineData(5111006, false)]
        [InlineData(4331004, false)]
        [InlineData(4341004, true)]
        public void ShouldBlockClientMeleeAirborneNoFoothold_ShouldMatchRecoveredDenyTable(int skillId, bool expectedBlocked)
        {
            bool blocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                skillId,
                isOnFoothold: false,
                isUserFlying: false);

            Assert.Equal(expectedBlocked, blocked);
        }

        [Fact]
        public void ShouldBlockClientMeleeAirborneNoFoothold_ShouldAllowRecoveredSkillIdsWhenFlying()
        {
            bool blocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                5111005,
                isOnFoothold: false,
                isUserFlying: true);

            Assert.False(blocked);
        }

        [Fact]
        public void ShouldBlockClientMeleeAirborneNoFoothold_ShouldIgnoreMagicAndRangedSkills()
        {
            var magicSkill = new SkillData
            {
                SkillId = 5111005,
                IsAttack = true,
                AttackType = SkillAttackType.Magic
            };

            var rangedSkill = new SkillData
            {
                SkillId = 5111005,
                IsAttack = true,
                AttackType = SkillAttackType.Ranged
            };

            bool magicBlocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                magicSkill,
                isOnFoothold: false,
                isUserFlying: false);
            bool rangedBlocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                rangedSkill,
                isOnFoothold: false,
                isUserFlying: false);

            Assert.False(magicBlocked);
            Assert.False(rangedBlocked);
        }
    }
}
