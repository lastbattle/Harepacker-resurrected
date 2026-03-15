using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Pools;
using HaCreator.Wz;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator
{
    public class QuestRuntimeManagerTests
    {
        [Fact]
        public void TryAcceptFromQuestWindow_RequiresStartItemsBeforeAccepting()
        {
            WzInformationManager originalInfoManager = HaCreator.Program.InfoManager;

            try
            {
                HaCreator.Program.InfoManager = CreateInfoManagerWithStartItemQuest();

                var runtime = new QuestRuntimeManager();
                var build = new CharacterBuild { Level = 10, Job = 0 };

                QuestWindowActionResult blocked = runtime.TryAcceptFromQuestWindow(1000, build);
                Assert.False(blocked.StateChanged);
                Assert.Contains("Collect Apple x2 more.", blocked.Messages);

                runtime.RecordDropPickup(new DropItem
                {
                    Type = DropType.QuestItem,
                    ItemId = "2000000",
                    Quantity = 1
                });

                blocked = runtime.TryAcceptFromQuestWindow(1000, build);
                Assert.False(blocked.StateChanged);
                Assert.Contains("Collect Apple x1 more.", blocked.Messages);

                runtime.RecordDropPickup(new DropItem
                {
                    Type = DropType.QuestItem,
                    ItemId = "2000000",
                    Quantity = 1
                });

                QuestWindowActionResult accepted = runtime.TryAcceptFromQuestWindow(1000, build);
                Assert.True(accepted.StateChanged);
                Assert.Contains("Accepted quest: Apple Delivery", accepted.Messages);
            }
            finally
            {
                HaCreator.Program.InfoManager = originalInfoManager;
            }
        }

        [Fact]
        public void BuildQuestLogSnapshot_ShowsStartItemRequirementProgress()
        {
            WzInformationManager originalInfoManager = HaCreator.Program.InfoManager;

            try
            {
                HaCreator.Program.InfoManager = CreateInfoManagerWithStartItemQuest();

                var runtime = new QuestRuntimeManager();
                var build = new CharacterBuild { Level = 10, Job = 0 };

                runtime.RecordDropPickup(new DropItem
                {
                    Type = DropType.QuestItem,
                    ItemId = "2000000",
                    Quantity = 1
                });

                QuestLogSnapshot snapshot = runtime.BuildQuestLogSnapshot(QuestLogTabType.Available, build, showAllLevels: true);
                QuestLogEntrySnapshot entry = Assert.Single(snapshot.Entries);

                Assert.False(entry.CanStart);
                QuestLogLineSnapshot itemLine = Assert.Single(entry.RequirementLines.Where(line => line.Label == "Item"));
                Assert.Equal("Apple 1/2", itemLine.Text);
                Assert.False(itemLine.IsComplete);

                QuestWindowDetailState detail = runtime.GetQuestWindowDetailState(1000, build);
                Assert.NotNull(detail);
                Assert.Contains("Item: Apple 1/2", detail.RequirementText);
                Assert.Contains("Collect Apple x1 more.", detail.RequirementText);
                Assert.False(detail.PrimaryActionEnabled);
            }
            finally
            {
                HaCreator.Program.InfoManager = originalInfoManager;
            }
        }

        private static WzInformationManager CreateInfoManagerWithStartItemQuest()
        {
            var infoManager = new WzInformationManager();
            infoManager.ItemNameCache[2000000] = Tuple.Create("Consume", "Apple", "Fresh apple");
            infoManager.NpcNameCache["1000"] = Tuple.Create("Maria", "Starter NPC");

            var questInfo = new WzSubProperty("1000");
            questInfo.AddProperty(new WzStringProperty("name", "Apple Delivery"));
            questInfo.AddProperty(new WzStringProperty("summary", "Bring apples before we can start."));
            questInfo.AddProperty(new WzStringProperty("demandSummary", "Maria wants two apples."));
            infoManager.QuestInfos["1000"] = questInfo;

            var startCheck = new WzSubProperty("0");
            startCheck.AddProperty(new WzIntProperty("npc", 1000));

            var startItems = new WzSubProperty("item");
            var firstRequirement = new WzSubProperty("0");
            firstRequirement.AddProperty(new WzIntProperty("id", 2000000));
            firstRequirement.AddProperty(new WzIntProperty("count", 2));
            startItems.AddProperty(firstRequirement);
            startCheck.AddProperty(startItems);

            var check = new WzSubProperty("1000");
            check.AddProperty(startCheck);
            infoManager.QuestChecks["1000"] = check;

            return infoManager;
        }
    }
}
