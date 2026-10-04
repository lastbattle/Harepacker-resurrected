using HaCreator.GUI.Quest;
using HaCreator.GUI.Quest.Graph;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System.Threading;
using System.Windows;

namespace UnitTest_MapSimulator;

public sealed class QuestGraphBuilderTests
{
    [Fact]
    public void GraphViewInitializesOnStaThread()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                _ = new QuestGraphView();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void FlowIncludesNextQuestAndDanglingTargets()
    {
        QuestEditorModel quest = new() { Id = 100, Name = "First" };
        quest.ActEndInfo.Add(new QuestEditorActInfoModel
        {
            ActType = QuestEditorActType.NextQuest,
            Amount = 101,
        });

        QuestGraphSnapshot snapshot = QuestGraphBuilder.Build(
            new[] { quest },
            quest,
            new QuestGraphLens
            {
                IncludeConversations = false,
                IncludeChecks = false,
                IncludeUnrelatedQuests = true,
            });

        Assert.Contains(snapshot.Nodes, node => node.Id == QuestGraphBuilder.QuestNodeId(101) && node.IsDangling);
        Assert.Contains(snapshot.Edges, edge => edge.Kind == QuestGraphEdgeKind.NextQuest && edge.TargetId == QuestGraphBuilder.QuestNodeId(101));
    }

    [Fact]
    public void RequirementsIncludeQuestStateAndProvenance()
    {
        QuestEditorModel quest = new() { Id = 200, Name = "Dependent" };
        QuestEditorCheckInfoModel check = new(QuestEditorCheckType.Quest);
        check.QuestReqs.Add(new QuestEditorQuestReqModel
        {
            QuestId = 199,
            QuestState = QuestStateType.Completed,
        });
        quest.CheckStartInfo.Add(check);

        QuestGraphSnapshot snapshot = QuestGraphBuilder.Build(
            new[] { quest },
            quest,
            new QuestGraphLens
            {
                IncludeActs = false,
                IncludeConversations = false,
                IncludeUnrelatedQuests = true,
            });

        QuestGraphEdgeModel edge = Assert.Single(snapshot.Edges);
        Assert.Equal(QuestGraphEdgeKind.CheckQuestRequirement, edge.Kind);
        Assert.Equal(QuestGraphBuilder.QuestNodeId(199), edge.SourceId);
        Assert.Equal(QuestGraphBuilder.QuestNodeId(200), edge.TargetId);
        Assert.Contains("Completed", edge.Label);
        Assert.Contains("CheckStartInfo", edge.Provenance);
    }

    [Fact]
    public void DialogueIncludesKnownResponseBranches()
    {
        QuestEditorModel quest = new() { Id = 300, Name = "Conversation" };
        QuestEditorSayModel say = new()
        {
            ConversationType = QuestEditorConversationType.YesNo,
            NpcConversation = "Will you help?",
        };
        say.YesResponses.Add(new QuestEditorSayResponseModel { Text = "Yes." });
        say.NoResponses.Add(new QuestEditorSayResponseModel { Text = "No." });
        quest.SayInfoStartQuest.Add(say);
        quest.SayInfoStartQuest.Add(new QuestEditorSayModel
        {
            ConversationType = QuestEditorConversationType.NextPrev,
            NpcConversation = "Then let us begin.",
        });

        QuestGraphSnapshot snapshot = QuestGraphBuilder.Build(
            new[] { quest },
            quest,
            new QuestGraphLens
            {
                IncludeActs = false,
                IncludeChecks = false,
                IncludeUnrelatedQuests = false,
            });

        Assert.Contains(snapshot.Nodes, node => node.Kind == QuestGraphNodeKind.Conversation);
        Assert.Equal(2, snapshot.Edges.Count(edge => edge.Kind == QuestGraphEdgeKind.ConversationResponse));
        Assert.Contains(snapshot.Edges, edge =>
            edge.Kind == QuestGraphEdgeKind.Conversation &&
            edge.SourceId.EndsWith(":say:start:0", StringComparison.Ordinal) &&
            edge.TargetId.EndsWith(":say:start:1", StringComparison.Ordinal));
    }

    [Fact]
    public void ExpandedRequirementsCreatePhaseGroupAndPredicateNodes()
    {
        QuestEditorModel quest = new() { Id = 400, Name = "Gated" };
        quest.CheckStartInfo.Add(new QuestEditorCheckInfoModel(QuestEditorCheckType.LvMin)
        {
            Amount = 30,
        });
        quest.CheckStartInfo.Add(new QuestEditorCheckInfoModel(QuestEditorCheckType.Npc)
        {
            Amount = 1012000,
        });

        QuestGraphSnapshot snapshot = QuestGraphBuilder.Build(
            new[] { quest },
            quest,
            new QuestGraphLens
            {
                IncludeActs = false,
                IncludeConversations = false,
                IncludeUnrelatedQuests = false,
                ExpandRequirementTrees = true,
            });

        Assert.Contains(snapshot.Nodes, node => node.Kind == QuestGraphNodeKind.RequirementGroup);
        Assert.Equal(2, snapshot.Nodes.Count(node => node.Kind == QuestGraphNodeKind.Requirement));
        Assert.Equal(2, snapshot.Edges.Count(edge => edge.Kind == QuestGraphEdgeKind.RequirementPredicate));
        Assert.Contains(snapshot.Edges, edge => edge.Kind == QuestGraphEdgeKind.RequirementGroup);
    }

    [Fact]
    public void LayoutIsDeterministicForTheSameSnapshot()
    {
        QuestGraphSnapshot snapshot = new(
            new[]
            {
                new QuestGraphNodeModel("a", "A", string.Empty, 1, QuestGraphNodeKind.Quest),
                new QuestGraphNodeModel("b", "B", string.Empty, 2, QuestGraphNodeKind.Quest),
            },
            new[]
            {
                new QuestGraphEdgeModel("e", "a", "b", QuestGraphEdgeKind.NextQuest, "next"),
            });

        IReadOnlyDictionary<string, Rect> first = QuestGraphLayout.Layout(snapshot);
        IReadOnlyDictionary<string, Rect> second = QuestGraphLayout.Layout(snapshot);

        Assert.Equal(first.Keys.OrderBy(key => key), second.Keys.OrderBy(key => key));
        foreach (string id in first.Keys)
            Assert.Equal(first[id], second[id]);
        Assert.True(first["b"].Left > first["a"].Left);
    }
}
