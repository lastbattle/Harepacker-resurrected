using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;

namespace UnitTest_MapSimulator;

public sealed class QuestRuntimeManagerSubJobFlagTests
{
    [Theory]
    [InlineData(0, 0, (int)CharacterSubJobFlagType.Adventurer, true)]
    [InlineData(0, 1, (int)CharacterSubJobFlagType.Adventurer_DualBlade, true)]
    [InlineData(0, 2, (int)CharacterSubJobFlagType.Adventurer_Cannoner, true)]
    [InlineData(0, 0, (int)CharacterSubJobFlagType.Adventurer_DualBlade, false)]
    [InlineData(0, 1, (int)CharacterSubJobFlagType.Adventurer_Cannoner, false)]
    [InlineData(434, 0, (int)CharacterSubJobFlagType.Adventurer_DualBlade, true)]
    [InlineData(434, 0, (int)CharacterSubJobFlagType.Adventurer, true)]
    [InlineData(531, 0, (int)CharacterSubJobFlagType.Adventurer_Cannoner, true)]
    [InlineData(1100, 0, (int)CharacterSubJobFlagType.Adventurer, false)]
    public void MatchesQuestSubJobFlags_ResolvesExplorerBranches(int job, int subJob, int flags, bool expected)
    {
        bool actual = QuestRuntimeManager.MatchesQuestSubJobFlags(job, subJob, flags);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatQuestSubJobFlagsText_FormatsKnownExplorerBranches()
    {
        int flags = (int)(CharacterSubJobFlagType.Adventurer |
                          CharacterSubJobFlagType.Adventurer_DualBlade |
                          CharacterSubJobFlagType.Adventurer_Cannoner);

        string text = QuestRuntimeManager.FormatQuestSubJobFlagsText(flags);

        Assert.Equal("Explorer, Dual Blade, Cannoneer", text);
    }
}
