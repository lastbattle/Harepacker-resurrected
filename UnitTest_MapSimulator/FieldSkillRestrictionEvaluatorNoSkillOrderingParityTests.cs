using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class FieldSkillRestrictionEvaluatorNoSkillOrderingParityTests
{
    [Fact]
    public void GetRestrictionMessage_FieldTypeRestrictionTakesPrecedenceOverNoSkillLists()
    {
        MapInfo mapInfo = CreateMapInfo(
            fieldType: null,
            noSkillClasses: null,
            noSkillSkills: new[] { 1009 });
        SkillData skill = CreateSkill(1009);

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(mapInfo, skill, currentJobId: 0);

        Assert.Equal("This event skill can only be used in Dojo or Balrog fields.", message);
    }

    [Fact]
    public void GetRestrictionMessage_NoSkillClassTakesPrecedenceOverNoSkillSkill()
    {
        MapInfo mapInfo = CreateMapInfo(
            fieldType: FieldType.FIELDTYPE_DOJANG,
            noSkillClasses: new[] { 2 },
            noSkillSkills: new[] { 2111002 });
        SkillData skill = CreateSkill(2111002);

        string message = FieldSkillRestrictionEvaluator.GetRestrictionMessage(mapInfo, skill, currentJobId: 211);

        Assert.Equal("This field forbids skills for your job branch.", message);
    }

    private static SkillData CreateSkill(int skillId)
    {
        return new SkillData
        {
            SkillId = skillId,
            Job = skillId / 10000
        };
    }

    private static MapInfo CreateMapInfo(FieldType? fieldType, int[]? noSkillClasses, int[]? noSkillSkills)
    {
        MapInfo mapInfo = new()
        {
            fieldType = fieldType
        };

        WzSubProperty noSkill = new("noSkill");
        if (noSkillClasses is { Length: > 0 })
        {
            noSkill.AddProperty(CreateNumberListProperty("class", noSkillClasses));
        }

        if (noSkillSkills is { Length: > 0 })
        {
            noSkill.AddProperty(CreateNumberListProperty("skill", noSkillSkills));
        }

        mapInfo.additionalNonInfoProps.Add(noSkill);
        return mapInfo;
    }

    private static WzSubProperty CreateNumberListProperty(string name, IReadOnlyList<int> values)
    {
        WzSubProperty property = new(name);
        for (int i = 0; i < values.Count; i++)
        {
            property.AddProperty(new WzIntProperty(i.ToString(), values[i]));
        }

        return property;
    }
}
