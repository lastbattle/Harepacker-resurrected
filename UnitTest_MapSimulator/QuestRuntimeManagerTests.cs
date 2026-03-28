using System.Linq;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class QuestRuntimeManagerTests
{
    [Fact]
    public void ParseScriptNames_SplitsDelimitedStringValues()
    {
        WzStringProperty property = new("startscript", "q10008s; q10008s, q10009s");

        IReadOnlyList<string> names = QuestRuntimeManager.ParseScriptNames(property);

        Assert.Equal(new[] { "q10008s", "q10009s" }, names.ToArray());
    }

    [Fact]
    public void ParseScriptNames_FlattensNestedPropertiesAndDeduplicates()
    {
        WzSubProperty property = new("endscript");
        property.AddProperty(new WzStringProperty("0", "q10002e"));

        WzSubProperty nested = new("nested");
        nested.AddProperty(new WzStringProperty("0", "q10003e"));
        nested.AddProperty(new WzStringProperty("1", "q10002e"));
        property.AddProperty(nested);

        IReadOnlyList<string> names = QuestRuntimeManager.ParseScriptNames(property);

        Assert.Equal(new[] { "q10002e", "q10003e" }, names.OrderBy(static name => name).ToArray());
    }

    [Fact]
    public void ParseSkillRequirements_TreatsBareSkillIdsAsAcquireRequirements()
    {
        WzSubProperty property = new("skill");
        WzSubProperty requirement = new("0");
        requirement.AddProperty(new WzIntProperty("id", 1007));
        property.AddProperty(requirement);

        IReadOnlyList<QuestRuntimeManager.QuestSkillRequirement> requirements = QuestRuntimeManager.ParseSkillRequirements(property);

        Assert.Single(requirements);
        Assert.True(requirements[0].MustBeAcquired);
        Assert.Equal(1007, requirements[0].SkillId);
    }

    [Theory]
    [InlineData(2218, 2200, true)]
    [InlineData(2218, 2001, true)]
    [InlineData(2218, 2300, false)]
    [InlineData(531, 500, true)]
    [InlineData(531, 501, true)]
    public void MatchesAllowedJobs_UsesQuestJobAncestors(int currentJob, int allowedJob, bool expected)
    {
        bool matches = QuestRuntimeManager.MatchesAllowedJobs(currentJob, new[] { allowedJob });

        Assert.Equal(expected, matches);
    }

    [Theory]
    [InlineData(531, 2, 8200, true)]
    [InlineData(2218, 0, 16, true)]
    [InlineData(2218, 0, 8, false)]
    public void MatchesRewardItemFilterCore_UsesResolvedQuestClass(int currentJob, int currentSubJob, int jobClassBitfield, bool expected)
    {
        bool matches = QuestRuntimeManager.MatchesRewardItemFilterCore(
            currentJob,
            currentSubJob,
            CharacterGender.Male,
            jobClassBitfield,
            0,
            CharacterGenderType.Both);

        Assert.Equal(expected, matches);
    }

    [Fact]
    public void Format_ResolvesQuestItemCountAndQuestStateTokensFromRuntimeContext()
    {
        NpcDialogueFormattingContext context = new()
        {
            ResolveItemCountText = static itemId => itemId == 4031988 ? "1" : "0",
            ResolveQuestStateText = static questId => questId == 1041 ? "Completed" : "Not started"
        };

        string formatted = NpcDialogueTextFormatter.Format(
            "#i4031988# #t4031988# #b#c4031988# / 1#k (#y1041# - #u1041#)",
            context);

        Assert.Equal("Item #4031988 1 / 1 (Quest #1041 - Completed)", formatted);
    }

    [Fact]
    public void FormatPages_UsesRawQuestPageTextWhenApplyingRuntimeFormatting()
    {
        NpcDialogueFormattingContext context = new()
        {
            ResolveItemCountText = static itemId => itemId == 4032271 ? "12" : "0",
            ResolveQuestStateText = static _ => "Not started"
        };

        IReadOnlyList<NpcInteractionPage> formattedPages = NpcDialogueTextFormatter.FormatPages(
            new[]
            {
                new NpcInteractionPage
                {
                    RawText = "#i4032271:# #t4032271:# #c4032271# / 30",
                    Text = "stale"
                }
            },
            context);

        Assert.Single(formattedPages);
        Assert.Equal("Item #4032271: 12 / 30", formattedPages[0].Text);
    }
}
