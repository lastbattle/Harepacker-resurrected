using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace UnitTest_MapSimulator;

public sealed class NpcQuestIssueConversationParityTests
{
    [Fact]
    public void UnmetTraitRequirementPrefersAuthoredTraitStopBranch()
    {
        var traitPages = new[]
        {
            new NpcInteractionPage { Text = "You need more charm." }
        };
        var defaultPages = new[]
        {
            new NpcInteractionPage { Text = "Default blocked text." }
        };
        var stopPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase)
        {
            ["charmMin"] = traitPages,
            ["default"] = defaultPages
        };

        IReadOnlyList<NpcInteractionPage> selectedPages =
            QuestRuntimeManager.SelectIssueConversationPagesCore(
                QuestStateType.Not_Started,
                isCompletionNpc: false,
                hasMissingItems: false,
                areAllRequiredItemsMissing: false,
                hasMissingMobs: false,
                hasUnmetQuestRequirements: false,
                hasUnmetJobRequirement: false,
                hasUnmetQuestRecordRequirements: false,
                hasUnmetTraitRequirement: true,
                hasUnmetLevelRequirement: false,
                hasUnmetFameRequirement: false,
                hasUnmetSkillRequirement: false,
                hasUnmetPetRequirement: false,
                hasUnmetMesoRequirement: false,
                hasUnmetAvailabilityRequirement: false,
                hasUnmetEquipRequirement: false,
                missingItemStopBranchIds: Array.Empty<int>(),
                missingMobStopBranchIds: Array.Empty<int>(),
                unmetQuestStopBranchIds: Array.Empty<int>(),
                stopPages,
                lostPages: Array.Empty<NpcInteractionPage>());

        Assert.Same(traitPages, selectedPages);
    }

    [Fact]
    public void UnmetTraitRequirementFallsBackToDefaultWhenNoTraitStopBranchExists()
    {
        var defaultPages = new[]
        {
            new NpcInteractionPage { Text = "Default blocked text." }
        };
        var stopPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = defaultPages
        };

        IReadOnlyList<NpcInteractionPage> selectedPages =
            QuestRuntimeManager.SelectIssueConversationPagesCore(
                QuestStateType.Not_Started,
                isCompletionNpc: false,
                hasMissingItems: false,
                areAllRequiredItemsMissing: false,
                hasMissingMobs: false,
                hasUnmetQuestRequirements: false,
                hasUnmetJobRequirement: false,
                hasUnmetQuestRecordRequirements: false,
                hasUnmetTraitRequirement: true,
                hasUnmetLevelRequirement: false,
                hasUnmetFameRequirement: false,
                hasUnmetSkillRequirement: false,
                hasUnmetPetRequirement: false,
                hasUnmetMesoRequirement: false,
                hasUnmetAvailabilityRequirement: false,
                hasUnmetEquipRequirement: false,
                missingItemStopBranchIds: Array.Empty<int>(),
                missingMobStopBranchIds: Array.Empty<int>(),
                unmetQuestStopBranchIds: Array.Empty<int>(),
                stopPages,
                lostPages: Array.Empty<NpcInteractionPage>());

        Assert.Same(defaultPages, selectedPages);
    }
}
