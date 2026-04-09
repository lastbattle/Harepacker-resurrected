using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class MonsterCarnivalFieldParityTests
{
    [Theory]
    [InlineData(0x1017, "Maple Red")]
    [InlineData(0x1018, "Maple Blue")]
    [InlineData(0x101B, "You don't have enough CP to continue.")]
    [InlineData(0x1020, "\tYou have won the Monster Carnival. Please wait as you'll be transported out of here shortly.")]
    [InlineData(0x1024, "[%s] has summoned a being. [%s]")]
    [InlineData(0x1027, "\tMonster Carnival is now underway!!")]
    [InlineData(0x102A, "Since the leader of the Team [%s] quit the Monster Carnival%2C [%s] has been appointed as the new leader of the team.")]
    [InlineData(0x102C, "UI/UIWindow.img/MonsterCarnival/backgrnd")]
    [InlineData(0x1033, "UI/UIWindow.img/MonsterCarnival/Tab/disabled")]
    public void MapleStoryStringPool_ResolvesPinnedMonsterCarnivalEntries(int stringPoolId, string expectedText)
    {
        bool found = MapleStoryStringPool.TryGet(stringPoolId, out string text);

        Assert.True(found);
        Assert.Equal(expectedText, text);
    }

    [Fact]
    public void OnEnter_UsesClientOwnedStartNotice()
    {
        MonsterCarnivalField field = CreateConfiguredField();

        field.OnEnter(
            MonsterCarnivalTeam.Team0,
            personalCp: 10,
            personalTotalCp: 10,
            myTeamCp: 20,
            myTeamTotalCp: 20,
            enemyTeamCp: 15,
            enemyTeamTotalCp: 15);

        Assert.Equal(
            "Monster Carnival is now underway!! [StringPool 0x1027] Entered as Maple Red.",
            field.CurrentStatusMessage);
    }

    [Fact]
    public void OnRequestFailure_UsesRecoveredStringPoolText()
    {
        MonsterCarnivalField field = CreateConfiguredField();

        field.OnRequestFailure(reasonCode: 1, tickCount: 0);

        Assert.Equal(
            "You don't have enough CP to continue. [StringPool 0x101B]",
            field.CurrentStatusMessage);
    }

    [Theory]
    [InlineData(8, "You have won the Monster Carnival. Please wait as you'll be transported out of here shortly. [StringPool 0x1020]")]
    [InlineData(9, "Unfortunately, you have lost the Monster Carnival. Please wait as you'll be transported out of here shortly. [StringPool 0x1021]")]
    [InlineData(10, "Despite the Overtime, the carnival ended in a draw. Please wait as you'll be transported out of here shortly. [StringPool 0x1022]")]
    [InlineData(11, "Monster Carnival has ended abruptly due to the opposing team leaving the game too early. Please wait as you'll be transported out of here shortly. [StringPool 0x1023]")]
    public void OnShowGameResult_UsesExactClientOwnedStrings(int resultCode, string expectedMessage)
    {
        MonsterCarnivalField field = CreateConfiguredField();

        field.OnShowGameResult(resultCode, tickCount: 0);

        Assert.Equal(expectedMessage, field.CurrentStatusMessage);
    }

    [Fact]
    public void OnShowMemberOutMessage_UsesExactLeaderReplacementString()
    {
        MonsterCarnivalField field = CreateConfiguredField();

        field.OnShowMemberOutMessage(messageType: 6, MonsterCarnivalTeam.Team1, "BlueLeader", tickCount: 0);

        Assert.Equal(
            "Since the leader of the Team [Maple Blue] quit the Monster Carnival, BlueLeader has been appointed as the new leader of the team. [StringPool 0x102A]",
            field.CurrentStatusMessage);
    }

    private static MonsterCarnivalField CreateConfiguredField()
    {
        MonsterCarnivalField field = new();
        field.Configure(new MonsterCarnivalFieldDefinition
        {
            MapId = 980000101,
            FieldType = FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE,
            MapType = 1,
            DeathCp = 10
        });

        return field;
    }
}
