using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class FieldSkillRestrictionEvaluatorTests
{
    [Fact]
    public void CoconutFieldTypeAloneDoesNotBlockSkillsWhenBasicActionOwnerIsInactive()
    {
        MapInfo mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_COCONUT
        };

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(
            mapInfo,
            CreateSkill(1001005),
            currentJobId: 100,
            externalRestrictionMessage: null,
            new FieldSkillRestrictionEvaluator.RuntimeState
            {
                CoconutBasicActionOwned = false
            });

        Assert.Null(message);
    }

    [Fact]
    public void CoconutRoundOwnershipBlocksSkills()
    {
        MapInfo mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_COCONUT
        };

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(
            mapInfo,
            CreateSkill(1001005),
            currentJobId: 100,
            externalRestrictionMessage: null,
            new FieldSkillRestrictionEvaluator.RuntimeState
            {
                CoconutBasicActionOwned = true
            });

        Assert.Equal("Skills cannot be used while the Coconut minigame owns basic attacks.", message);
    }

    [Fact]
    public void SnowBallFieldTypeAloneDoesNotBlockSkillsWhenBasicActionOwnerIsInactive()
    {
        MapInfo mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_SNOWBALL
        };

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(
            mapInfo,
            CreateSkill(1001005),
            currentJobId: 100,
            externalRestrictionMessage: null,
            new FieldSkillRestrictionEvaluator.RuntimeState
            {
                SnowBallBasicActionOwned = false
            });

        Assert.Null(message);
    }

    [Fact]
    public void SnowBallRoundOwnershipBlocksSkills()
    {
        MapInfo mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_SNOWBALL
        };

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(
            mapInfo,
            CreateSkill(1001005),
            currentJobId: 100,
            externalRestrictionMessage: null,
            new FieldSkillRestrictionEvaluator.RuntimeState
            {
                SnowBallBasicActionOwned = true
            });

        Assert.Equal("Skills cannot be used while the Snowball minigame owns basic attacks.", message);
    }

    [Fact]
    public void GuildBossFieldTypeAloneDoesNotBlockSkillsWhenBasicActionOwnerIsInactive()
    {
        MapInfo mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_GUILDBOSS
        };

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(
            mapInfo,
            CreateSkill(1001005),
            currentJobId: 100,
            externalRestrictionMessage: null,
            new FieldSkillRestrictionEvaluator.RuntimeState
            {
                GuildBossBasicActionOwned = false
            });

        Assert.Null(message);
    }

    [Fact]
    public void GuildBossPulleyOwnershipBlocksSkills()
    {
        MapInfo mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_GUILDBOSS
        };

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(
            mapInfo,
            CreateSkill(1001005),
            currentJobId: 100,
            externalRestrictionMessage: null,
            new FieldSkillRestrictionEvaluator.RuntimeState
            {
                GuildBossBasicActionOwned = true
            });

        Assert.Equal("Skills cannot be used while the Guild Boss field owns basic attacks.", message);
    }

    [Fact]
    public void NestedStringNoSkillRestrictionStillBlocksListedSkill()
    {
        MapInfo mapInfo = new MapInfo();
        WzSubProperty noSkill = new WzSubProperty("noSkill");
        WzSubProperty nested = new WzSubProperty("nested");
        WzSubProperty skillList = new WzSubProperty("skill");
        skillList.AddProperty(new WzStringProperty("0", "1001005"));
        nested.AddProperty(skillList);
        noSkill.AddProperty(nested);
        mapInfo.additionalNonInfoProps.Add(noSkill);

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(
            mapInfo,
            CreateSkill(1001005),
            currentJobId: 100,
            externalRestrictionMessage: null,
            new FieldSkillRestrictionEvaluator.RuntimeState());

        Assert.Equal("This skill is forbidden in this field.", message);
    }

    private static SkillData CreateSkill(int skillId)
    {
        return new SkillData
        {
            SkillId = skillId,
            Job = skillId / 10000,
            Name = "Test Skill"
        };
    }
}
