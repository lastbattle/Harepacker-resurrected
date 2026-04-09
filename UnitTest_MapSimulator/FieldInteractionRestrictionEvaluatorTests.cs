using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class FieldInteractionRestrictionEvaluatorTests
{
    [Theory]
    [InlineData(MapSimulatorWindowNames.MemoMailbox)]
    [InlineData(MapSimulatorWindowNames.MemoSend)]
    [InlineData(MapSimulatorWindowNames.MemoGet)]
    [InlineData(MapSimulatorWindowNames.QuestDelivery)]
    public void GetWindowRestrictionMessage_UsesParcelRestrictionForParcelOwnedWindows(string windowName)
    {
        string message = FieldInteractionRestrictionEvaluator.GetWindowRestrictionMessage(
            FieldLimitType.Parcel_Open_Limit.Value,
            windowName);

        Assert.Equal("Parcel-owned delivery and mailbox windows cannot be opened in this map.", message);
    }

    [Fact]
    public void GetWindowRestrictionMessage_UsesQuestAlertRestrictionForQuestAlarm()
    {
        string message = FieldInteractionRestrictionEvaluator.GetWindowRestrictionMessage(
            FieldLimitType.No_Quest_Alert.Value,
            MapSimulatorWindowNames.QuestAlarm);

        Assert.Equal("Quest alert windows are disabled in this map.", message);
    }

    [Fact]
    public void GetWindowRestrictionMessage_IgnoresUnrelatedWindows()
    {
        string message = FieldInteractionRestrictionEvaluator.GetWindowRestrictionMessage(
            FieldLimitType.Parcel_Open_Limit.Value | FieldLimitType.No_Quest_Alert.Value,
            MapSimulatorWindowNames.CharacterInfo);

        Assert.Null(message);
    }
}
