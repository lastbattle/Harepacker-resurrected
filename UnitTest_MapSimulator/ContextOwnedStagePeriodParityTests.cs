using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator;

public class ContextOwnedStagePeriodParityTests
{
    [Fact]
    public void BuildForTesting_ParsesMixedCaseStageListAndPeriodAliases()
    {
        WzSubProperty stageSystemRoot = Node("StageSystemRoot",
            Node("Halloween",
                Node("StageList",
                    Node("0",
                        Int("BackColor", unchecked((int)0xFF112233u)),
                        Node("StageKeyword", Str("0", "autumn")),
                        Node("QuestID", Int("0", 20001)),
                        Node("FieldId", Int("0", 910000000))))));

        ContextOwnedStageSystemCatalog catalog = ContextOwnedStageSystemCatalog.BuildForTesting(stageSystemRoot, out string error);

        Assert.Null(error);
        Assert.NotNull(catalog);
        Assert.True(catalog.TryGetPeriod("Halloween", 0, out ContextOwnedStagePeriodCatalogEntry period));
        Assert.Contains("autumn", period.Keywords);
        Assert.Contains(20001, period.EnabledQuestIds);
        Assert.Contains(910000000, period.AffectedMapIds);
    }

    [Fact]
    public void BuildForTesting_ParsesNestedMixedCaseStageAffectedMapAliasesWithQuestStateAndRandTimeGate()
    {
        const string theme = "RainTheme";
        const byte mode = 1;
        const int fieldId = 100000001;
        const int questId = 30001;

        WzSubProperty stageSystemRoot = BuildSimpleStageSystem(theme, mode, "rain", questId);
        WzSubProperty stageAffectedMapRoot = Node("StageAffectedMapRoot",
            Node("Row",
                Node("FieldID", Node("container", Int("value", fieldId))),
                Node("StageKeyword", Node("entry", Str("value", "rain"))),
                Node("QuestID", Node("entry", Int("value", questId))),
                Node("QuestState", Node("entry", Int("value", (int)QuestStateType.Completed))),
                Node("Priority", Node("entry", Int("value", 7))),
                Node("RandTime", Node("entry", Int("value", 5)))));

        ContextOwnedStageSystemCatalog catalog = ContextOwnedStageSystemCatalog.BuildForTesting(
            stageSystemRoot,
            out string error,
            stageAffectedMapRoot: stageAffectedMapRoot);

        Assert.Null(error);
        Assert.NotNull(catalog);
        Assert.True(catalog.TryGetPeriod(theme, mode, out ContextOwnedStagePeriodCatalogEntry period));

        HashSet<int> beforeRandTime = catalog.ResolveAffectedMaps(
            period,
            questIdValue => questIdValue == questId ? QuestStateType.Completed : QuestStateType.Not_Started,
            elapsedStagePeriodMilliseconds: 4999);
        Assert.DoesNotContain(fieldId, beforeRandTime);

        HashSet<int> afterRandTime = catalog.ResolveAffectedMaps(
            period,
            questIdValue => questIdValue == questId ? QuestStateType.Completed : QuestStateType.Not_Started,
            elapsedStagePeriodMilliseconds: 5000);
        Assert.Contains(fieldId, afterRandTime);
    }

    [Fact]
    public void BuildForTesting_SuppressesStructuralAliasNamesFromWrapperOnlyKeywordTrees()
    {
        WzSubProperty stageSystemRoot = Node("StageSystemRoot",
            Node("Holiday",
                Node("stage",
                    Node("0",
                        Node("stageKeyword",
                            Node("keyword"),
                            Node("aKeyword"),
                            Node("enabledQuest"),
                            Node("questID"),
                            Node("affectedMap"),
                            Node("fieldID"),
                            Node("priority"),
                            Node("questState"),
                            Node("randTime"))))));

        ContextOwnedStageSystemCatalog catalog = ContextOwnedStageSystemCatalog.BuildForTesting(stageSystemRoot, out string error);

        Assert.Null(error);
        Assert.NotNull(catalog);
        Assert.True(catalog.TryGetPeriod("Holiday", 0, out ContextOwnedStagePeriodCatalogEntry period));
        Assert.Empty(period.Keywords);
    }

    [Fact]
    public void BuildForTesting_DoesNotLeakNumericFieldFallbackIntoQuestAliasBranches()
    {
        const string theme = "RainTheme";
        const byte mode = 1;
        const int validFieldId = 100000123;

        WzSubProperty stageSystemRoot = BuildSimpleStageSystem(theme, mode, "rain");
        WzSubProperty stageAffectedMapRoot = Node("StageAffectedMapRoot",
            Node("QuestOnlyWrapper",
                Node("StageKeyword", Str("0", "rain")),
                Node("QuestID", Node("nested", Int("0", 9100)))),
            Node("ValidFieldAliasWrapper",
                Node("StageKeyword", Str("0", "rain")),
                Node("FieldID", Node(validFieldId.ToString(), Node("meta")))));

        ContextOwnedStageSystemCatalog catalog = ContextOwnedStageSystemCatalog.BuildForTesting(
            stageSystemRoot,
            out string error,
            stageAffectedMapRoot: stageAffectedMapRoot);

        Assert.Null(error);
        Assert.NotNull(catalog);
        Assert.True(catalog.TryGetPeriod(theme, mode, out ContextOwnedStagePeriodCatalogEntry period));

        HashSet<int> affectedMaps = catalog.ResolveAffectedMaps(period);
        Assert.Contains(validFieldId, affectedMaps);
        Assert.DoesNotContain(9100, affectedMaps);
    }

    private static WzSubProperty BuildSimpleStageSystem(string theme, byte mode, string keyword, int? questId = null)
    {
        List<WzImageProperty> periodChildren = new()
        {
            Node("stageKeyword", Str("0", keyword))
        };
        if (questId.HasValue)
        {
            periodChildren.Add(Node("enabledQuest", Int("0", questId.Value)));
        }

        return Node("StageSystemRoot",
            Node(theme,
                Node("stageList",
                    Node(mode.ToString(), periodChildren.ToArray()))));
    }

    private static WzSubProperty Node(string name, params WzImageProperty[] children)
    {
        WzSubProperty node = new(name);
        foreach (WzImageProperty child in children ?? Array.Empty<WzImageProperty>())
        {
            node.AddProperty(child);
        }

        return node;
    }

    private static WzStringProperty Str(string name, string value)
    {
        return new WzStringProperty(name, value);
    }

    private static WzIntProperty Int(string name, int value)
    {
        return new WzIntProperty(name, value);
    }
}
