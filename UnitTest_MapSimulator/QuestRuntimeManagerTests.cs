using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Pools;
using HaCreator.Wz;
using MapleLib.WzLib.WzProperties;
using Xunit;

namespace UnitTest_MapSimulator;

public class QuestRuntimeManagerTests
{
    [Fact]
    public void QuestRuntimeManager_TracksItemRequirementsAndQuestRewardsAcrossQuestActions()
    {
        var previousInfoManager = HaCreator.Program.InfoManager;
        try
        {
            HaCreator.Program.InfoManager = CreateInfoManager();

            var questRuntime = new QuestRuntimeManager();
            var build = new CharacterBuild
            {
                Level = 15,
                Job = 0
            };

            QuestActionResult acceptResult = questRuntime.TryPerformPrimaryAction(1000, 2000, build);
            Assert.True(acceptResult.StateChanged);
            Assert.Equal(MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Started, questRuntime.GetCurrentState(1000));

            QuestActionResult blockedResult = questRuntime.TryPerformPrimaryAction(1000, 2000, build);
            Assert.False(blockedResult.StateChanged);
            Assert.Contains(blockedResult.Messages, message => message.Contains("Collect Proof of Practice x3 more.", StringComparison.Ordinal));

            questRuntime.RecordDropPickup(new DropItem
            {
                Type = DropType.QuestItem,
                ItemId = "4001000",
                Quantity = 2
            });

            Assert.Equal(2, questRuntime.GetTrackedItemCount(4001000));

            questRuntime.RecordDropPickup(new DropItem
            {
                Type = DropType.Item,
                ItemId = "4001000",
                Quantity = 1
            });

            QuestActionResult completionResult = questRuntime.TryPerformPrimaryAction(1000, 2000, build);
            Assert.True(completionResult.StateChanged);
            Assert.Equal(MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Completed, questRuntime.GetCurrentState(1000));
            Assert.Equal(0, questRuntime.GetTrackedItemCount(4001000));
            Assert.Equal(2, questRuntime.GetTrackedItemCount(2000000));
            Assert.Contains(completionResult.Messages, message => message.Contains("Consumed item: Proof of Practice x3", StringComparison.Ordinal));
            Assert.Contains(completionResult.Messages, message => message.Contains("Item reward: Beginner Potion x2", StringComparison.Ordinal));
        }
        finally
        {
            HaCreator.Program.InfoManager = previousInfoManager;
        }
    }

    private static WzInformationManager CreateInfoManager()
    {
        var infoManager = new WzInformationManager();
        infoManager.ItemNameCache[4001000] = Tuple.Create("Etc", "Proof of Practice", "Quest proof");
        infoManager.ItemNameCache[2000000] = Tuple.Create("Use", "Beginner Potion", "Test reward");

        infoManager.QuestInfos["1000"] = CreateQuestInfo();
        infoManager.QuestChecks["1000"] = CreateQuestChecks();
        infoManager.QuestActs["1000"] = CreateQuestActs();

        return infoManager;
    }

    private static WzSubProperty CreateQuestInfo()
    {
        var questInfo = new WzSubProperty("1000");
        questInfo.AddProperty(new WzStringProperty("name", "Proof of Practice"));
        questInfo.AddProperty(new WzStringProperty("summary", "Bring practice proof back to the trainer."));
        questInfo.AddProperty(new WzStringProperty("0", "Collect three practice proofs."));
        questInfo.AddProperty(new WzStringProperty("1", "Return when you have enough practice proofs."));
        questInfo.AddProperty(new WzStringProperty("2", "Well done."));
        return questInfo;
    }

    private static WzSubProperty CreateQuestChecks()
    {
        var root = new WzSubProperty("1000");

        var start = new WzSubProperty("0");
        start.AddProperty(new WzIntProperty("npc", 2000));
        start.AddProperty(new WzIntProperty("lvmin", 10));
        root.AddProperty(start);

        var end = new WzSubProperty("1");
        end.AddProperty(new WzIntProperty("npc", 2000));

        var items = new WzSubProperty("item");
        var item = new WzSubProperty("0");
        item.AddProperty(new WzIntProperty("id", 4001000));
        item.AddProperty(new WzIntProperty("count", 3));
        items.AddProperty(item);
        end.AddProperty(items);

        root.AddProperty(end);
        return root;
    }

    private static WzSubProperty CreateQuestActs()
    {
        var root = new WzSubProperty("1000");
        root.AddProperty(new WzSubProperty("0"));

        var end = new WzSubProperty("1");
        var items = new WzSubProperty("item");

        var consumeRequirement = new WzSubProperty("0");
        consumeRequirement.AddProperty(new WzIntProperty("id", 4001000));
        consumeRequirement.AddProperty(new WzIntProperty("count", -3));
        items.AddProperty(consumeRequirement);

        var reward = new WzSubProperty("1");
        reward.AddProperty(new WzIntProperty("id", 2000000));
        reward.AddProperty(new WzIntProperty("count", 2));
        items.AddProperty(reward);

        end.AddProperty(items);
        root.AddProperty(end);

        return root;
    }
}
