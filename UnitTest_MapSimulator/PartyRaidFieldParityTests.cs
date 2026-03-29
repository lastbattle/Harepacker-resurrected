using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class PartyRaidFieldParityTests
{
    [Fact]
    public void FieldStageBadgesFollowClientHorizontalStep()
    {
        PartyRaidField field = new();
        field.BindMap(new MapInfo
        {
            id = 923020100,
            fieldType = FieldType.FIELDTYPE_PARTYRAID,
            onUserEnter = "PRaid_W_Enter"
        });

        field.SetMineStage(3);
        field.SetOtherStage(5);

        Assert.Equal(-2, field.GetMineStateX());
        Assert.Equal(48, field.GetOtherStateX());
        Assert.Equal(88, field.GetMineStateY());
        Assert.Equal(110, field.GetOtherStateY());
    }

    [Fact]
    public void FieldStageBadgePositionsClampToClientStageRange()
    {
        PartyRaidField field = new();
        field.BindMap(new MapInfo
        {
            id = 923020100,
            fieldType = FieldType.FIELDTYPE_PARTYRAID
        });

        field.SetMineStage(0);
        field.SetOtherStage(9);

        Assert.Equal(-48, field.GetMineStateX());
        Assert.Equal(48, field.GetOtherStateX());
    }

    [Fact]
    public void BindMap_ResultModeInfersOutcomeFromPartyRaidScripts()
    {
        PartyRaidField field = new();
        field.BindMap(new MapInfo
        {
            id = 923020499,
            onUserEnter = "PRaid_FailEnter"
        });

        Assert.True(field.IsActive);
        Assert.Equal(PartyRaidFieldMode.Result, field.Mode);
        Assert.Equal(PartyRaidResultOutcome.Lose, field.ResultOutcome);
    }
}
