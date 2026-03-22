using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class PartyRaidFieldTests
{
    [Fact]
    public void BindMap_UsesOnUserEnterToInferResultOutcome()
    {
        PartyRaidField field = new();
        MapInfo map = new()
        {
            id = 923020010,
            fieldType = FieldType.FIELDTYPE_PARTYRAID_RESULT,
            onUserEnter = "PRaid_FailEnter"
        };

        field.BindMap(map);

        Assert.True(field.IsActive);
        Assert.Equal(PartyRaidFieldMode.Result, field.Mode);
        Assert.Equal(PartyRaidResultOutcome.Lose, field.ResultOutcome);
    }

    [Fact]
    public void OnFieldSetVariable_AcceptsBatteryAliasesAndClampsToCapacity()
    {
        PartyRaidField field = new();
        field.BindMap(new MapInfo
        {
            id = 923020100,
            fieldType = FieldType.FIELDTYPE_PARTYRAID
        });

        Assert.True(field.OnFieldSetVariable("batteryMax", "12"));
        Assert.True(field.OnFieldSetVariable("charge", "20"));

        Assert.Equal(12, field.BatteryCapacity);
        Assert.Equal(12, field.BatteryCharge);
        Assert.Contains("battery 12/12", field.DescribeStatus());
    }

    [Fact]
    public void OnPartyValue_AcceptsBatteryAlias()
    {
        PartyRaidField field = new();
        field.BindMap(new MapInfo
        {
            id = 923020120,
            fieldType = FieldType.FIELDTYPE_PARTYRAID
        });

        Assert.True(field.OnPartyValue("batteryPoint", "37"));

        Assert.Equal(37, field.BatteryCharge);
    }

    [Fact]
    public void OnSessionValue_AcceptsResultAliases()
    {
        PartyRaidField field = new();
        field.BindMap(new MapInfo
        {
            id = 923020020,
            fieldType = FieldType.FIELDTYPE_PARTYRAID_RESULT,
            onUserEnter = "PRaid_FailEnter"
        });

        Assert.True(field.OnSessionValue("partyPoint", "1200"));
        Assert.True(field.OnSessionValue("rewardBonus", "300"));
        Assert.True(field.OnSessionValue("sum", "1500"));

        Assert.Equal(1200, field.ResultPoint);
        Assert.Equal(300, field.ResultBonus);
        Assert.Equal(1500, field.ResultTotal);
    }
}
