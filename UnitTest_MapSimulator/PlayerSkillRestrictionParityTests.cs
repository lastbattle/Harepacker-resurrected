using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class PlayerSkillRestrictionParityTests
    {
        [Theory]
        [InlineData(172)]
        [InlineData(173)]
        public void PlayerSkillBlockingStatusMapper_MapsPolymorphMobSkills(int skillId)
        {
            bool mapped = PlayerSkillBlockingStatusMapper.TryMapMobSkill(skillId, out PlayerSkillBlockingStatus status);

            Assert.True(mapped);
            Assert.Equal(PlayerSkillBlockingStatus.Polymorph, status);
        }

        [Fact]
        public void PlayerSkillStateRestrictionEvaluator_BlocksPolymorphedPlayerThroughSharedStatusSeam()
        {
            var player = new PlayerCharacter(new CharacterBuild());
            var skill = new SkillData { SkillId = 1001004 };
            int currentTime = Environment.TickCount;

            player.ApplySkillBlockingStatus(PlayerSkillBlockingStatus.Polymorph, 5000, currentTime);

            string message = PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, skill, currentTime);

            Assert.Equal("Skills cannot be used while polymorphed.", message);
        }

        [Theory]
        [InlineData(4121003)]
        [InlineData(4221003)]
        public void PlayerSkillStateRestrictionEvaluator_BlocksClientTauntSkillsOnLadderOrRope(int skillId)
        {
            var player = new PlayerCharacter(new CharacterBuild());
            player.Physics.IsOnLadderOrRope = true;

            var skill = new SkillData
            {
                SkillId = skillId,
                ClientInfoType = 32
            };

            string message = PlayerSkillStateRestrictionEvaluator.GetRestrictionMessage(player, skill, Environment.TickCount);

            Assert.Equal("This skill cannot be used while on a ladder or rope.", message);
        }
    }
}
