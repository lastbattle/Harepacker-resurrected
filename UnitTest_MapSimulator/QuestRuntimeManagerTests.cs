using System.Collections.Generic;
using HaCreator.MapSimulator.Interaction;
using HaCreator.Wz;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator
{
    public sealed class QuestRuntimeManagerTests
    {
        [Fact]
        public void SelectIssueConversationPagesCore_PrefersNpcBranch_OnStartedQuestAtStarterNpc()
        {
            NpcInteractionPage npcPage = new() { Text = "Go see the delivery target." };
            NpcInteractionPage itemPage = new() { Text = "Bring the quest item." };
            var stopPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>
            {
                ["npc"] = new[] { npcPage },
                ["item"] = new[] { itemPage }
            };

            IReadOnlyList<NpcInteractionPage> result = QuestRuntimeManager.SelectIssueConversationPagesCore(
                QuestStateType.Started,
                isCompletionNpc: false,
                hasMissingItems: true,
                areAllRequiredItemsMissing: true,
                hasMissingMobs: false,
                hasUnmetQuestRequirements: false,
                stopPages,
                lostPages: new[] { new NpcInteractionPage { Text = "Lost the quest item." } });

            Assert.Same(npcPage, Assert.Single(result));
        }

        [Fact]
        public void SelectIssueConversationPagesCore_UsesItemBranch_OnCompletionNpc()
        {
            NpcInteractionPage npcPage = new() { Text = "Go see the delivery target." };
            NpcInteractionPage itemPage = new() { Text = "Bring the quest item." };
            var stopPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>
            {
                ["npc"] = new[] { npcPage },
                ["item"] = new[] { itemPage }
            };

            IReadOnlyList<NpcInteractionPage> result = QuestRuntimeManager.SelectIssueConversationPagesCore(
                QuestStateType.Started,
                isCompletionNpc: true,
                hasMissingItems: true,
                areAllRequiredItemsMissing: false,
                hasMissingMobs: false,
                hasUnmetQuestRequirements: false,
                stopPages,
                lostPages: new[] { new NpcInteractionPage { Text = "Lost the quest item." } });

            Assert.Same(itemPage, Assert.Single(result));
        }

        [Fact]
        public void NpcDialogueTextFormatter_ResolvesMobAndQuestAmountTokensFromQuestChecks()
        {
            HaCreator.Program.InfoManager = new WzInformationManager();
            HaCreator.Program.InfoManager.MobNameCache["1210102"] = "Orange Mushroom";

            var questCheck = new WzSubProperty("10171");
            var completion = new WzSubProperty("1");
            var mobs = new WzSubProperty("mob");
            var mobEntry = new WzSubProperty("0");
            mobEntry.AddProperty(new WzIntProperty("id", 1210102));
            mobEntry.AddProperty(new WzIntProperty("count", 1));
            mobs.AddProperty(mobEntry);
            completion.AddProperty(mobs);
            questCheck.AddProperty(completion);
            HaCreator.Program.InfoManager.QuestChecks["10171"] = questCheck;

            string formatted = NpcDialogueTextFormatter.Format("#o1210102# #a10171# #M10171#");

            Assert.Equal("Orange Mushroom 1 Orange Mushroom", formatted);
        }

        [Fact]
        public void NpcDialogueTextFormatter_FallsBackToSelectedMobPlaceholders_WhenQuestUsesSelectedMobFlag()
        {
            HaCreator.Program.InfoManager = new WzInformationManager();

            var questInfo = new WzSubProperty("10450");
            questInfo.AddProperty(new WzIntProperty("selectedMob", 1));
            HaCreator.Program.InfoManager.QuestInfos["10450"] = questInfo;

            string formatted = NpcDialogueTextFormatter.Format("Hunt #M10450# for #x10450#.");

            Assert.Equal("Hunt the selected monster for the selected bonus amount.", formatted);
        }
    }
}
