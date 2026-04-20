using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class QuestDetailRichTextMarkerParityTests
{
    [Fact]
    public void FormatPreservingQuestDetailMarkers_PreservesFontAndResetMarkers()
    {
        string formatted = NpcDialogueTextFormatter.FormatPreservingQuestDetailMarkers(
            "#fnArial#A#fn##fs16#B#fs#");

        Assert.Contains("{{QUESTFONT:Arial}}", formatted);
        Assert.Contains("{{QUESTFONTRESET}}", formatted);
        Assert.Contains("{{QUESTFONTSIZE:16}}", formatted);
        Assert.Contains("{{QUESTFONTSIZERESET}}", formatted);
    }

    [Fact]
    public void TryParseQuestFontResetMarker_AcceptsClientResetTokenShape()
    {
        Assert.True(QuestDetailWindow.TryParseQuestFontResetMarkerForTesting("{{QUESTFONTRESET}}"));
        Assert.True(QuestDetailWindow.TryParseQuestFontResetMarkerForTesting("{{questfontreset}}"));
        Assert.False(QuestDetailWindow.TryParseQuestFontResetMarkerForTesting("{{QUESTFONT:Arial}}"));
    }

    [Fact]
    public void TryParseQuestFontSizeResetMarker_AcceptsClientResetTokenShape()
    {
        Assert.True(QuestDetailWindow.TryParseQuestFontSizeResetMarkerForTesting("{{QUESTFONTSIZERESET}}"));
        Assert.True(QuestDetailWindow.TryParseQuestFontSizeResetMarkerForTesting("{{questfontsizereset}}"));
        Assert.False(QuestDetailWindow.TryParseQuestFontSizeResetMarkerForTesting("{{QUESTFONTSIZE:11}}"));
    }

    [Fact]
    public void QuestDetailInlineReference_DefaultSource_IsUnknown()
    {
        QuestDetailInlineReference reference = new(
            QuestDetailInlineReferenceKind.Item,
            4030000,
            "Etc item");

        Assert.Equal(QuestDetailInlineReferenceSource.Unknown, reference.Source);
    }

    [Fact]
    public void ShouldRouteInlineReferenceToDemandDeliveryHandoff_RoutesOnlyDemandSources()
    {
        QuestDetailInlineReference requirementTextReference = new(
            QuestDetailInlineReferenceKind.Item,
            4030000,
            "Etc item",
            QuestDetailInlineReferenceSource.RequirementText);
        QuestDetailInlineReference requirementLineReference = new(
            QuestDetailInlineReferenceKind.Item,
            4030000,
            "Etc item",
            QuestDetailInlineReferenceSource.RequirementLine);
        QuestDetailInlineReference rewardTextReference = new(
            QuestDetailInlineReferenceKind.Item,
            4030000,
            "Etc item",
            QuestDetailInlineReferenceSource.RewardText);
        QuestDetailInlineReference summaryReference = new(
            QuestDetailInlineReferenceKind.Item,
            4030000,
            "Etc item",
            QuestDetailInlineReferenceSource.SummaryText);
        QuestDetailInlineReference npcRequirementReference = new(
            QuestDetailInlineReferenceKind.Npc,
            1012000,
            "NPC",
            QuestDetailInlineReferenceSource.RequirementText);

        Assert.True(MapSimulator.ShouldRouteInlineReferenceToDemandDeliveryHandoffForTesting(requirementTextReference));
        Assert.True(MapSimulator.ShouldRouteInlineReferenceToDemandDeliveryHandoffForTesting(requirementLineReference));
        Assert.False(MapSimulator.ShouldRouteInlineReferenceToDemandDeliveryHandoffForTesting(rewardTextReference));
        Assert.False(MapSimulator.ShouldRouteInlineReferenceToDemandDeliveryHandoffForTesting(summaryReference));
        Assert.False(MapSimulator.ShouldRouteInlineReferenceToDemandDeliveryHandoffForTesting(npcRequirementReference));
    }
}
