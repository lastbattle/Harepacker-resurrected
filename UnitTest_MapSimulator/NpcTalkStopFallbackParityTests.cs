using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class NpcTalkStopFallbackParityTests
    {
        [Fact]
        public void SelectIssueConversationPagesCore_UsesNumericStopFallbackWhenNamedBranchesMissing()
        {
            IReadOnlyList<NpcInteractionPage> numericPages = CreatePages("Numeric fallback");
            var stopPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase)
            {
                ["0"] = numericPages
            };

            IReadOnlyList<NpcInteractionPage> selected = QuestRuntimeManager.SelectIssueConversationPagesCore(
                QuestStateType.Not_Started,
                isCompletionNpc: false,
                hasMissingItems: false,
                areAllRequiredItemsMissing: false,
                hasMissingMobs: false,
                hasUnmetQuestRequirements: false,
                hasUnmetJobRequirement: false,
                hasUnmetQuestRecordRequirements: false,
                stopPages,
                Array.Empty<NpcInteractionPage>());

            Assert.Same(numericPages, selected);
        }

        [Fact]
        public void SelectIssueConversationPagesCore_PrefersDefaultBeforeNumericStopFallback()
        {
            IReadOnlyList<NpcInteractionPage> defaultPages = CreatePages("Default");
            IReadOnlyList<NpcInteractionPage> numericPages = CreatePages("Numeric fallback");
            var stopPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = defaultPages,
                ["0"] = numericPages
            };

            IReadOnlyList<NpcInteractionPage> selected = QuestRuntimeManager.SelectIssueConversationPagesCore(
                QuestStateType.Not_Started,
                isCompletionNpc: false,
                hasMissingItems: false,
                areAllRequiredItemsMissing: false,
                hasMissingMobs: false,
                hasUnmetQuestRequirements: false,
                hasUnmetJobRequirement: false,
                hasUnmetQuestRecordRequirements: false,
                stopPages,
                Array.Empty<NpcInteractionPage>());

            Assert.Same(defaultPages, selected);
        }

        [Fact]
        public void SelectIssueConversationPagesCore_UsesLowestAvailableNumericStopBranch()
        {
            IReadOnlyList<NpcInteractionPage> branchOnePages = CreatePages("Branch one");
            var stopPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = branchOnePages
            };

            IReadOnlyList<NpcInteractionPage> selected = QuestRuntimeManager.SelectIssueConversationPagesCore(
                QuestStateType.Not_Started,
                isCompletionNpc: false,
                hasMissingItems: false,
                areAllRequiredItemsMissing: false,
                hasMissingMobs: false,
                hasUnmetQuestRequirements: false,
                hasUnmetJobRequirement: false,
                hasUnmetQuestRecordRequirements: false,
                stopPages,
                Array.Empty<NpcInteractionPage>());

            Assert.Same(branchOnePages, selected);
        }

        [Fact]
        public void SelectIssueConversationPagesCore_UsesNumericStopFallbackBeyondBranchThree()
        {
            IReadOnlyList<NpcInteractionPage> branchFivePages = CreatePages("Branch five");
            var stopPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase)
            {
                ["5"] = branchFivePages
            };

            IReadOnlyList<NpcInteractionPage> selected = QuestRuntimeManager.SelectIssueConversationPagesCore(
                QuestStateType.Not_Started,
                isCompletionNpc: false,
                hasMissingItems: false,
                areAllRequiredItemsMissing: false,
                hasMissingMobs: false,
                hasUnmetQuestRequirements: false,
                hasUnmetJobRequirement: false,
                hasUnmetQuestRecordRequirements: false,
                stopPages,
                Array.Empty<NpcInteractionPage>());

            Assert.Same(branchFivePages, selected);
        }

        [Fact]
        public void ParseConversationStopPages_ExposesNumericStopBranches()
        {
            var root = new WzSubProperty("0");
            var stop = new WzSubProperty("stop");
            var branchFive = new WzSubProperty("5");
            branchFive.AddProperty(new WzStringProperty("0", "Numeric branch dialogue"));
            stop.AddProperty(branchFive);
            root.AddProperty(stop);

            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> parsed =
                QuestRuntimeManager.ParseConversationStopPages(root);

            Assert.True(parsed.TryGetValue("5", out IReadOnlyList<NpcInteractionPage> pages));
            Assert.Single(pages);
            Assert.Equal("Numeric branch dialogue", pages[0].Text);
        }

        private static IReadOnlyList<NpcInteractionPage> CreatePages(string text)
        {
            return new[]
            {
                new NpcInteractionPage
                {
                    Text = text
                }
            };
        }
    }
}
