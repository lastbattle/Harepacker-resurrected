using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator
{
    public class FieldRuleRuntimeTests
    {
        [Fact]
        public void Reset_ReportsConfiguredEntryRules()
        {
            MapInfo mapInfo = new MapInfo
            {
                timeLimit = 90,
                decHP = 50,
                decInterval = 15000,
                allowedItem = new List<int> { 2022539, 2022540, 2022541 },
                protectItem = new List<int> { 1102109 },
                lvLimit = 30,
                fieldLimit = (1L << (int)FieldLimitType.Unable_To_Migrate)
                             | (1L << (int)FieldLimitType.Unable_To_Use_Mystic_Door)
            };

            FieldRuleRuntime runtime = new FieldRuleRuntime(mapInfo);

            IReadOnlyList<string> messages = runtime.Reset(0);

            Assert.Contains(messages, message => message.Contains("Field timer started: 1:30."));
            Assert.Contains(messages, message => message.Contains("Environmental damage: 50 HP every 15s."));
            Assert.Contains(messages, message => message.Contains("Allowed-item rule active (3 item(s))"));
            Assert.Contains(messages, message => message.Contains("This map forbids map transfer."));
            Assert.Contains(messages, message => message.Contains("Mystic Door is disabled in this map."));
            Assert.Contains(messages, message => message.Contains("lvLimit=30"));
        }

        [Fact]
        public void Update_RequestsReturnMapWhenTimeLimitExpires()
        {
            MapInfo mapInfo = new MapInfo
            {
                timeLimit = 5,
                returnMap = 105100100
            };

            FieldRuleRuntime runtime = new FieldRuleRuntime(mapInfo);
            runtime.Reset(1000);

            FieldRuleUpdateResult result = runtime.Update(6001, playerAlive: true, pendingMapChange: false);

            Assert.Equal(105100100, result.TransferMapId);
            Assert.Contains(result.Messages, message => message.Contains("Field timer expired."));
            Assert.Contains(result.OverlayMessages, message => message.Contains("Field timer expired."));
        }

        [Fact]
        public void Update_AppliesEnvironmentalDamageAtConfiguredInterval()
        {
            MapInfo mapInfo = new MapInfo
            {
                decHP = 10,
                decInterval = 15000
            };

            FieldRuleRuntime runtime = new FieldRuleRuntime(mapInfo);
            runtime.Reset(0);

            FieldRuleUpdateResult earlyResult = runtime.Update(14999, playerAlive: true, pendingMapChange: false);
            FieldRuleUpdateResult tickResult = runtime.Update(15000, playerAlive: true, pendingMapChange: false);

            Assert.Equal(0, earlyResult.EnvironmentalDamage);
            Assert.Equal(10, tickResult.EnvironmentalDamage);
            Assert.True(tickResult.TriggerDamageMist);
        }

        [Fact]
        public void Update_TreatsSmallDecIntervalValuesAsSeconds()
        {
            MapInfo mapInfo = new MapInfo
            {
                decHP = 10,
                decInterval = 15
            };

            FieldRuleRuntime runtime = new FieldRuleRuntime(mapInfo);
            runtime.Reset(0);

            FieldRuleUpdateResult result = runtime.Update(15000, playerAlive: true, pendingMapChange: false);

            Assert.Equal(10, result.EnvironmentalDamage);
        }
    }
}
