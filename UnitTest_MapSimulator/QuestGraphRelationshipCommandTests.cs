using HaCreator.GUI.Quest;
using HaCreator.GUI.Quest.Graph;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace UnitTest_MapSimulator;

public sealed class QuestGraphRelationshipCommandTests
{
    [Fact]
    public void AddNextQuestUpdatesRawAndModelAndSupportsUndoRedo()
    {
        QuestEditorModel source = new() { Id = 100 };
        QuestEditorModel target = new() { Id = 101 };
        WzSubProperty rawAct = new("100");

        QuestGraphRelationshipResult result = QuestGraphRelationshipCommand.TryAdd(
            source,
            rawAct,
            QuestGraphRelationshipKind.NextQuest,
            QuestGraphRelationshipPhase.End,
            target.Id,
            QuestStateType.Completed,
            [source, target]);

        Assert.True(result.Success, result.Error);
        Assert.Equal(target.Id, Assert.Single(source.ActEndInfo).Amount);
        Assert.Equal(target.Id, Assert.IsType<WzIntProperty>(rawAct["1"]["nextQuest"]).Value);

        Assert.True(result.Operation!.TryUndo(out string undoError), undoError);
        Assert.Empty(source.ActEndInfo);
        Assert.Null(rawAct["1"]);

        Assert.True(result.Operation.TryRedo(out string redoError), redoError);
        Assert.Equal(target.Id, Assert.Single(source.ActEndInfo).Amount);
        Assert.Equal(target.Id, Assert.IsType<WzIntProperty>(rawAct["1"]["nextQuest"]).Value);
    }

    [Fact]
    public void ReplaceRequirementPreservesUnknownRawChildren()
    {
        QuestEditorModel source = new() { Id = 200 };
        QuestEditorModel oldTarget = new() { Id = 199 };
        QuestEditorModel newTarget = new() { Id = 198 };
        QuestEditorCheckInfoModel check = new(QuestEditorCheckType.Quest);
        check.QuestReqs.Add(new QuestEditorQuestReqModel { QuestId = oldTarget.Id, QuestState = QuestStateType.Completed });
        source.CheckStartInfo.Add(check);

        WzSubProperty rawCheck = CreateRequirementRoot("200", "0", oldTarget.Id, QuestStateType.Completed);
        WzSubProperty rawRequirement = (WzSubProperty)rawCheck["0"]["quest"]["0"];
        rawRequirement.AddProperty(new WzStringProperty("futureMetadata", "preserve me"));
        QuestGraphRelationshipAddress address = new(
            source.Id,
            QuestGraphRelationshipKind.CheckQuestRequirement,
            QuestGraphRelationshipPhase.Start,
            oldTarget.Id,
            QuestStateType.Completed,
            modelIndex: 0,
            requirementIndex: 0);

        QuestGraphRelationshipResult result = QuestGraphRelationshipCommand.TryReplace(
            source,
            rawCheck,
            address,
            newTarget.Id,
            QuestStateType.Started,
            [source, oldTarget, newTarget]);

        Assert.True(result.Success, result.Error);
        Assert.Equal(newTarget.Id, check.QuestReqs[0].QuestId);
        Assert.Equal(QuestStateType.Started, check.QuestReqs[0].QuestState);
        Assert.Equal("preserve me", Assert.IsType<WzStringProperty>(rawRequirement["futureMetadata"]).Value);
        Assert.Equal(newTarget.Id, Assert.IsType<WzIntProperty>(rawRequirement["id"]).Value);

        Assert.True(result.Operation!.TryUndo(out string undoError), undoError);
        Assert.Equal(oldTarget.Id, check.QuestReqs[0].QuestId);
        Assert.Equal(QuestStateType.Completed, check.QuestReqs[0].QuestState);
        Assert.Equal("preserve me", Assert.IsType<WzStringProperty>(rawRequirement["futureMetadata"]).Value);
    }

    [Fact]
    public void RemoveRequirementRestoresOriginalRawOrderOnUndo()
    {
        QuestEditorModel source = new() { Id = 300 };
        QuestEditorModel first = new() { Id = 301 };
        QuestEditorModel second = new() { Id = 302 };
        QuestEditorCheckInfoModel check = new(QuestEditorCheckType.Quest);
        check.QuestReqs.Add(new QuestEditorQuestReqModel { QuestId = first.Id, QuestState = QuestStateType.Completed });
        check.QuestReqs.Add(new QuestEditorQuestReqModel { QuestId = second.Id, QuestState = QuestStateType.Started });
        source.CheckEndInfo.Add(check);

        WzSubProperty rawCheck = CreateRequirementRoot("300", "1", first.Id, QuestStateType.Completed);
        WzSubProperty rawQuest = (WzSubProperty)rawCheck["1"]["quest"];
        WzSubProperty secondRaw = new("1");
        secondRaw.AddProperty(new WzIntProperty("id", second.Id));
        secondRaw.AddProperty(new WzIntProperty("state", (int)QuestStateType.Started));
        rawQuest.AddProperty(secondRaw);
        QuestGraphRelationshipAddress address = new(
            source.Id,
            QuestGraphRelationshipKind.CheckQuestRequirement,
            QuestGraphRelationshipPhase.End,
            first.Id,
            QuestStateType.Completed,
            modelIndex: 0,
            requirementIndex: 0);

        QuestGraphRelationshipResult result = QuestGraphRelationshipCommand.TryRemove(source, rawCheck, address);

        Assert.True(result.Success, result.Error);
        Assert.Equal(second.Id, Assert.Single(check.QuestReqs).QuestId);
        Assert.Equal("1", Assert.Single(rawQuest.WzProperties).Name);

        Assert.True(result.Operation!.TryUndo(out string undoError), undoError);
        Assert.Equal(new[] { first.Id, second.Id }, check.QuestReqs.Select(item => item.QuestId));
        Assert.Equal(new[] { "0", "1" }, rawQuest.WzProperties.Select(item => item.Name));
    }

    [Fact]
    public void ValidationRejectsSelfDuplicateAndCycleBeforeRawMutation()
    {
        QuestEditorModel first = new() { Id = 400 };
        QuestEditorModel second = new() { Id = 401 };
        first.ActEndInfo.Add(new QuestEditorActInfoModel { ActType = QuestEditorActType.NextQuest, Amount = second.Id });
        WzSubProperty rawAct = new("401");

        QuestGraphRelationshipResult self = QuestGraphRelationshipCommand.TryAdd(
            second, rawAct, QuestGraphRelationshipKind.NextQuest, QuestGraphRelationshipPhase.End,
            second.Id, QuestStateType.Completed, [first, second]);
        Assert.Equal(QuestGraphRelationshipErrorCode.SelfLink, self.ErrorCode);

        QuestGraphRelationshipResult cycle = QuestGraphRelationshipCommand.TryAdd(
            second, rawAct, QuestGraphRelationshipKind.NextQuest, QuestGraphRelationshipPhase.End,
            first.Id, QuestStateType.Completed, [first, second]);
        Assert.Equal(QuestGraphRelationshipErrorCode.Cycle, cycle.ErrorCode);
        Assert.Empty(second.ActEndInfo);
        Assert.Empty(rawAct.WzProperties);
    }

    [Fact]
    public void ExpandedRequirementEdgeRetainsEditableAddress()
    {
        QuestEditorModel source = new() { Id = 500 };
        QuestEditorCheckInfoModel check = new(QuestEditorCheckType.Quest);
        check.QuestReqs.Add(new QuestEditorQuestReqModel { QuestId = 499, QuestState = QuestStateType.Completed });
        source.CheckStartInfo.Add(check);

        QuestGraphSnapshot graph = QuestGraphBuilder.Build(
            [source],
            source,
            new QuestGraphLens
            {
                IncludeActs = false,
                IncludeConversations = false,
                IncludeUnrelatedQuests = true,
                ExpandRequirementTrees = true,
            });

        QuestGraphEdgeModel edge = Assert.Single(graph.Edges.Where(item => item.Kind == QuestGraphEdgeKind.CheckQuestRequirement));
        Assert.True(edge.IsEditable);
        Assert.Equal(source.Id, edge.Relationship!.OwnerQuestId);
        Assert.Equal(0, edge.Relationship.ModelIndex);
        Assert.Equal(0, edge.Relationship.RequirementIndex);
    }

    [Fact]
    public void AddRequirementRejectsRawModelMismatchWithoutMutation()
    {
        QuestEditorModel source = new() { Id = 600 };
        QuestEditorModel existingTarget = new() { Id = 601 };
        QuestEditorModel newTarget = new() { Id = 602 };
        WzSubProperty rawCheck = CreateRequirementRoot("600", "0", existingTarget.Id, QuestStateType.Completed);

        QuestGraphRelationshipResult result = QuestGraphRelationshipCommand.TryAdd(
            source,
            rawCheck,
            QuestGraphRelationshipKind.CheckQuestRequirement,
            QuestGraphRelationshipPhase.Start,
            newTarget.Id,
            QuestStateType.Completed,
            [source, existingTarget, newTarget]);

        Assert.False(result.Success);
        Assert.Equal(QuestGraphRelationshipErrorCode.UnsupportedRawShape, result.ErrorCode);
        Assert.Empty(source.CheckStartInfo);
        WzSubProperty rawQuest = Assert.IsType<WzSubProperty>(rawCheck["0"]["quest"]);
        Assert.Single(rawQuest.WzProperties);
        Assert.Equal(existingTarget.Id, Assert.IsType<WzIntProperty>(rawQuest["0"]["id"]).Value);
    }

    [Fact]
    public void ValidationRejectsCycleAcrossNextAndRequirementRelationships()
    {
        QuestEditorModel first = new() { Id = 700 };
        QuestEditorModel second = new() { Id = 701 };
        first.ActEndInfo.Add(new QuestEditorActInfoModel { ActType = QuestEditorActType.NextQuest, Amount = second.Id });
        WzSubProperty rawCheck = new("701");

        QuestGraphRelationshipResult result = QuestGraphRelationshipCommand.TryAdd(
            second,
            rawCheck,
            QuestGraphRelationshipKind.CheckQuestRequirement,
            QuestGraphRelationshipPhase.Start,
            first.Id,
            QuestStateType.Completed,
            [first, second]);

        Assert.False(result.Success);
        Assert.Equal(QuestGraphRelationshipErrorCode.Cycle, result.ErrorCode);
        Assert.Empty(second.CheckStartInfo);
        Assert.Empty(rawCheck.WzProperties);
    }

    [Fact]
    public void UndoRefusesToOverwriteExternalModelChange()
    {
        QuestEditorModel source = new() { Id = 800 };
        QuestEditorModel oldTarget = new() { Id = 801 };
        QuestEditorModel newTarget = new() { Id = 802 };
        QuestEditorModel externalTarget = new() { Id = 803 };
        source.ActEndInfo.Add(new QuestEditorActInfoModel { ActType = QuestEditorActType.NextQuest, Amount = oldTarget.Id });
        WzSubProperty rawAct = new("800");
        WzSubProperty phase = new("1");
        WzIntProperty rawNext = new("nextQuest", oldTarget.Id);
        phase.AddProperty(rawNext);
        rawAct.AddProperty(phase);
        QuestGraphRelationshipAddress address = new(
            source.Id,
            QuestGraphRelationshipKind.NextQuest,
            QuestGraphRelationshipPhase.End,
            oldTarget.Id,
            modelIndex: 0);

        QuestGraphRelationshipResult result = QuestGraphRelationshipCommand.TryReplace(
            source, rawAct, address, newTarget.Id, QuestStateType.Completed,
            [source, oldTarget, newTarget, externalTarget]);
        Assert.True(result.Success, result.Error);

        source.ActEndInfo[0].Amount = externalTarget.Id;
        Assert.False(result.Operation!.TryUndo(out _));
        Assert.Equal(externalTarget.Id, source.ActEndInfo[0].Amount);
        Assert.Equal(newTarget.Id, rawNext.Value);
        Assert.True(result.Operation.IsApplied);
    }

    [Fact]
    public void DuplicateRawPhaseIsRejectedAsAmbiguous()
    {
        QuestEditorModel source = new() { Id = 900 };
        QuestEditorModel target = new() { Id = 901 };
        WzSubProperty rawAct = new("900");
        rawAct.AddProperty(new WzSubProperty("1"));
        rawAct.AddProperty(new WzSubProperty("1"));

        QuestGraphRelationshipResult result = QuestGraphRelationshipCommand.TryAdd(
            source,
            rawAct,
            QuestGraphRelationshipKind.NextQuest,
            QuestGraphRelationshipPhase.End,
            target.Id,
            QuestStateType.Completed,
            [source, target]);

        Assert.False(result.Success);
        Assert.Equal(QuestGraphRelationshipErrorCode.UnsupportedRawShape, result.ErrorCode);
        Assert.Empty(source.ActEndInfo);
        Assert.Equal(2, rawAct.WzProperties.Count);
    }

    [Fact]
    public void FailedRequirementAddDoesNotLeaveCreatedRawPhase()
    {
        QuestEditorModel source = new() { Id = 950 };
        QuestEditorModel existingTarget = new() { Id = 951 };
        QuestEditorModel newTarget = new() { Id = 952 };
        QuestEditorCheckInfoModel check = new(QuestEditorCheckType.Quest);
        check.QuestReqs.Add(new QuestEditorQuestReqModel { QuestId = existingTarget.Id, QuestState = QuestStateType.Completed });
        source.CheckStartInfo.Add(check);
        WzSubProperty rawCheck = new("950");

        QuestGraphRelationshipResult result = QuestGraphRelationshipCommand.TryAdd(
            source, rawCheck, QuestGraphRelationshipKind.CheckQuestRequirement,
            QuestGraphRelationshipPhase.Start, newTarget.Id, QuestStateType.Completed,
            [source, existingTarget, newTarget]);

        Assert.False(result.Success);
        Assert.Equal(QuestGraphRelationshipErrorCode.UnsupportedRawShape, result.ErrorCode);
        Assert.Empty(rawCheck.WzProperties);
        Assert.Single(check.QuestReqs);
    }

    private static WzSubProperty CreateRequirementRoot(string questId, string phaseName, int targetId, QuestStateType state)
    {
        WzSubProperty root = new(questId);
        WzSubProperty phase = new(phaseName);
        WzSubProperty quest = new("quest");
        WzSubProperty requirement = new("0");
        requirement.AddProperty(new WzIntProperty("id", targetId));
        requirement.AddProperty(new WzIntProperty("state", (int)state));
        quest.AddProperty(requirement);
        phase.AddProperty(quest);
        root.AddProperty(phase);
        return root;
    }
}
