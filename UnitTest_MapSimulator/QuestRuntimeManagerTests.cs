using System.Collections.Generic;
using HaCreator.MapSimulator.Interaction;

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
    }
}
