using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class FieldSkillRestrictionEvaluatorTests
{
    [Fact]
    public void CoconutFieldTypeBlocksSkillsEvenWithoutUnableToUseSkillFieldLimit()
    {
        var mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_COCONUT,
            fieldLimit = 0
        };

        var skill = new SkillData
        {
            SkillId = 1001004,
            Name = "Power Strike"
        };

        string restriction = FieldSkillRestrictionEvaluator.GetRestrictionMessage(mapInfo, skill);

        Assert.Equal("Skills cannot be used while the Coconut minigame owns basic attacks.", restriction);
    }

    [Fact]
    public void SnowBallFieldTypeBlocksSkillsEvenWithoutUnableToUseSkillFieldLimit()
    {
        var mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_SNOWBALL,
            fieldLimit = 0
        };

        var skill = new SkillData
        {
            SkillId = 2001002,
            Name = "Magic Claw"
        };

        string restriction = FieldSkillRestrictionEvaluator.GetRestrictionMessage(mapInfo, skill);

        Assert.Equal("Skills cannot be used while the Snowball minigame owns basic attacks.", restriction);
    }

    [Fact]
    public void FieldLimitRestrictionStillWinsBeforeClientOwnedFieldTypeMessage()
    {
        var mapInfo = new MapInfo
        {
            fieldType = FieldType.FIELDTYPE_COCONUT,
            fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Skill
        };

        var skill = new SkillData
        {
            SkillId = 1001004,
            Name = "Power Strike"
        };

        string restriction = FieldSkillRestrictionEvaluator.GetRestrictionMessage(mapInfo, skill);

        Assert.Equal("This field forbids skill usage.", restriction);
    }
}
