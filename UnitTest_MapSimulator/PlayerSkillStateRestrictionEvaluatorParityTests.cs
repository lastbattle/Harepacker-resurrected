using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class PlayerSkillStateRestrictionEvaluatorParityTests
    {
        [Theory]
        [InlineData(1311006)]
        [InlineData(5111006)]
        [InlineData(5121001)]
        [InlineData(5221003)]
        [InlineData(32001007)]
        [InlineData(35121004)]
        [InlineData(1009)]
        [InlineData(10001009)]
        [InlineData(1020)]
        [InlineData(10001020)]
        [InlineData(3120010)]
        [InlineData(15111003)]
        [InlineData(15111004)]
        [InlineData(4341004)]
        public void ClientMeleeAirborneNoFootholdGate_BlocksRecoveredSkillIds(int skillId)
        {
            bool blocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                skillId,
                isOnFoothold: false,
                isUserFlying: false);

            Assert.True(blocked);
        }

        [Theory]
        [InlineData(1311006)]
        [InlineData(5111006)]
        [InlineData(1009)]
        public void ClientMeleeAirborneNoFootholdGate_AllowsSameSkillsWhenGroundedOrFlying(int skillId)
        {
            bool groundedBlocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                skillId,
                isOnFoothold: true,
                isUserFlying: false);
            bool flyingBlocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                skillId,
                isOnFoothold: false,
                isUserFlying: true);

            Assert.False(groundedBlocked);
            Assert.False(flyingBlocked);
        }

        [Theory]
        [InlineData(33101005)]
        [InlineData(32101001)]
        [InlineData(5201006)]
        [InlineData(0)]
        [InlineData(-1)]
        public void ClientMeleeAirborneNoFootholdGate_DoesNotBlockUnlistedSkillIds(int skillId)
        {
            bool blocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                skillId,
                isOnFoothold: false,
                isUserFlying: false);

            Assert.False(blocked);
        }

        [Fact]
        public void ClientMeleeAirborneNoFootholdGate_BlocksListedSkillWhenMeleePathIsUsed()
        {
            var skill = new SkillData
            {
                SkillId = 1311006,
                IsAttack = true,
                AttackType = SkillAttackType.Melee
            };

            bool blocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                skill,
                isOnFoothold: false,
                isUserFlying: false);

            Assert.True(blocked);
        }

        [Fact]
        public void ClientMeleeAirborneNoFootholdGate_DoesNotBlockListedSkillOutsideMeleePath()
        {
            var skill = new SkillData
            {
                SkillId = 1311006,
                IsAttack = true,
                AttackType = SkillAttackType.Ranged
            };

            bool blocked = PlayerSkillStateRestrictionEvaluator.ShouldBlockClientMeleeAirborneNoFoothold(
                skill,
                isOnFoothold: false,
                isUserFlying: false);

            Assert.False(blocked);
        }
    }
}
