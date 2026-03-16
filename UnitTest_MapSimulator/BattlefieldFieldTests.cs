using HaCreator.MapSimulator.Effects;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_MapSimulator
{
    public class BattlefieldFieldTests
    {
        [Fact]
        public void Configure_LoadsBattlefieldMapDefaultsFromCopiedWzData()
        {
            MapInfo mapInfo = new MapInfo();
            WzSubProperty battleField = new WzSubProperty("battleField");
            battleField["timeDefault"] = new WzIntProperty("timeDefault", 300);
            battleField["timeFinish"] = new WzIntProperty("timeFinish", 3);
            battleField["rewardMapWinWolf"] = new WzIntProperty("rewardMapWinWolf", 910040003);
            battleField["rewardMapWinSheep"] = new WzIntProperty("rewardMapWinSheep", 910040004);
            battleField["effectWin"] = new WzStringProperty("effectWin", "event/coconut/victory");
            battleField["effectLose"] = new WzStringProperty("effectLose", "event/coconut/lose");
            mapInfo.additionalNonInfoProps.Add(battleField);

            BattlefieldField field = new();
            field.Enable();
            field.Configure(mapInfo);

            Assert.Equal(300, field.DefaultDurationSeconds);
            Assert.Equal(3, field.FinishDurationSeconds);
            Assert.Equal(910040003, field.RewardMapWinWolf);
            Assert.Equal(910040004, field.RewardMapWinSheep);
            Assert.Equal("event/coconut/victory", field.EffectWinPath);
            Assert.Equal("event/coconut/lose", field.EffectLosePath);
        }

        [Fact]
        public void OnClock_TypeTwoStartsPacketOwnedTimer()
        {
            BattlefieldField field = new();
            field.Enable();

            field.OnClock(2, 120, 1000);
            field.Update(61000, 0f);

            Assert.Equal(60, field.RemainingSeconds);
        }

        [Fact]
        public void OnScoreUpdate_ClampsBothTeamScoresToClientByteRange()
        {
            BattlefieldField field = new();
            field.Enable();

            field.OnScoreUpdate(999, -5, 1000);

            Assert.Equal(byte.MaxValue, field.WolvesScore);
            Assert.Equal(0, field.SheepScore);
        }
    }
}
