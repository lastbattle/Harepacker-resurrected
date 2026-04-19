using HaCreator.MapSimulator.Character.Skills;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class FieldSkillRestrictionEvaluatorUnableToUseSkillTests
    {
        [Fact]
        public void UnableToUseSkillField_BlocksRecoveredMeleeSkillId5121004()
        {
            MapInfo mapInfo = CreateMapInfoWithFieldLimit(FieldLimitType.Unable_To_Use_Skill);
            SkillData skill = CreateSkill(5121004);

            bool canUse = FieldSkillRestrictionEvaluator.CanUseSkill(mapInfo, skill, currentJobId: 512);

            Assert.False(canUse);
        }

        [Theory]
        [InlineData(15101003)]
        [InlineData(3121003)]
        [InlineData(3221003)]
        [InlineData(5201006)]
        [InlineData(32101001)]
        [InlineData(32111011)]
        public void UnableToUseSkillField_BlocksRecoveredClientCallerSkillIds(int skillId)
        {
            MapInfo mapInfo = CreateMapInfoWithFieldLimit(FieldLimitType.Unable_To_Use_Skill);
            SkillData skill = CreateSkill(skillId);

            bool canUse = FieldSkillRestrictionEvaluator.CanUseSkill(mapInfo, skill, currentJobId: 0);

            Assert.False(canUse);
        }

        [Fact]
        public void UnableToUseSkillField_AllowsNeighboringUnlistedSkill()
        {
            MapInfo mapInfo = CreateMapInfoWithFieldLimit(FieldLimitType.Unable_To_Use_Skill);
            SkillData skill = CreateSkill(5121003);

            bool canUse = FieldSkillRestrictionEvaluator.CanUseSkill(mapInfo, skill, currentJobId: 512);

            Assert.True(canUse);
        }

        [Fact]
        public void NoSkillClass_Sub10000SkillIdUsesSkillRootZeroClass()
        {
            MapInfo classOneMap = CreateMapInfoWithNoSkillClass(1);
            SkillData sub10000Skill = CreateSkill(900);

            string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(classOneMap, sub10000Skill, currentJobId: 434);

            Assert.Equal("This field forbids skills for your job branch.", message);
        }

        [Fact]
        public void NoSkillClass_Sub10000SkillIdDoesNotFallbackToCurrentJobClass()
        {
            MapInfo classFourMap = CreateMapInfoWithNoSkillClass(4);
            SkillData sub10000Skill = CreateSkill(900);

            string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(classFourMap, sub10000Skill, currentJobId: 434);

            Assert.Null(message);
        }

        [Fact]
        public void NoSkillClass_ZeroSkillIdFallsBackToCurrentJobClass()
        {
            MapInfo classFourMap = CreateMapInfoWithNoSkillClass(4);
            SkillData unavailableSkill = CreateSkill(0);

            string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(classFourMap, unavailableSkill, currentJobId: 434);

            Assert.Equal("This field forbids skills for your job branch.", message);
        }

        private static MapInfo CreateMapInfoWithFieldLimit(FieldLimitType fieldLimit)
        {
            return new MapInfo
            {
                fieldLimit = 1L << (int)fieldLimit
            };
        }

        private static MapInfo CreateMapInfoWithNoSkillClass(int listedClass)
        {
            WzSubProperty classNode = new("class");
            classNode.AddProperty(new WzIntProperty("0", listedClass));

            WzSubProperty noSkillNode = new("noSkill");
            noSkillNode.AddProperty(classNode);

            WzImage image = new("test.img");
            image.AddProperty(noSkillNode);

            return new MapInfo
            {
                Image = image
            };
        }

        private static SkillData CreateSkill(int skillId)
        {
            return new SkillData
            {
                SkillId = skillId
            };
        }
    }
}
