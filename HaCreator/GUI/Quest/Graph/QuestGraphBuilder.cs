#nullable enable

using HaCreator.GUI.Quest;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HaCreator.GUI.Quest.Graph;

/// <summary>
/// Converts the editor's quest collections into a deterministic, read-only
/// graph.  All values are copied while building; no collection or model is
/// modified by this type.
/// </summary>
public static class QuestGraphBuilder
{
    /// <summary>Builds a graph from the supplied quests.</summary>
    public static QuestGraphSnapshot Build(
        IEnumerable<QuestEditorModel>? quests,
        QuestEditorModel? selectedQuest,
        QuestGraphLens? lens = null) =>
        BuildCore(quests, selectedQuest, lens);

    private static QuestGraphSnapshot BuildCore(
        IEnumerable<QuestEditorModel>? quests,
        QuestEditorModel? selectedQuest,
        QuestGraphLens? lens)
    {
        lens ??= QuestGraphLens.Default;

        var diagnostics = new List<string>();
        var nodes = new Dictionary<string, QuestGraphNodeModel>(StringComparer.Ordinal);
        var edges = new List<QuestGraphEdgeModel>();
        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);

        // Materialise and order the input first.  ObservableCollection order is
        // meaningful to Say.img, while quest order is not; using id then the
        // original index gives stable output without changing either source.
        var orderedQuests = (quests ?? Enumerable.Empty<QuestEditorModel>())
            .Select((quest, index) => (quest, index))
            .Where(item => item.quest != null)
            .OrderBy(item => item.quest!.Id)
            .ThenBy(item => item.index)
            .ToArray();

        var knownQuestIds = new HashSet<int>();
        foreach (var (quest, _) in orderedQuests)
        {
            if (!knownQuestIds.Add(quest!.Id))
            {
                diagnostics.Add($"Duplicate quest id {quest.Id}; only the first model is represented.");
                continue;
            }

            AddQuestNode(nodes, quest, isDangling: false);
        }

        // A selected model can be supplied independently of the collection in
        // a filtered editor view.  Include it as a normal node when necessary.
        if (selectedQuest != null && !knownQuestIds.Contains(selectedQuest.Id))
        {
            knownQuestIds.Add(selectedQuest.Id);
            AddQuestNode(nodes, selectedQuest, isDangling: false);
        }

        int? selectedQuestId = selectedQuest?.Id ?? lens.FocusQuestId;

        var processedQuestIds = new HashSet<int>();
        foreach (var (quest, _) in orderedQuests)
        {
            if (quest == null || !nodes.ContainsKey(QuestNodeId(quest.Id)))
                continue; // duplicate quest id
            if (!processedQuestIds.Add(quest.Id))
                continue; // duplicate quest id

            BuildActEdges(quest, nodes, edges, edgeKeys, diagnostics, lens, knownQuestIds);
            BuildCheckEdges(quest, nodes, edges, edgeKeys, diagnostics, lens, knownQuestIds);
            BuildConversationEdges(quest, nodes, edges, edgeKeys, diagnostics, lens);
        }

        // The selected quest may not have been part of the ordered input.
        if (selectedQuest != null && !orderedQuests.Any(item => ReferenceEquals(item.quest, selectedQuest)))
        {
            BuildActEdges(selectedQuest, nodes, edges, edgeKeys, diagnostics, lens, knownQuestIds);
            BuildCheckEdges(selectedQuest, nodes, edges, edgeKeys, diagnostics, lens, knownQuestIds);
            BuildConversationEdges(selectedQuest, nodes, edges, edgeKeys, diagnostics, lens);
        }

        var visibleNodeIds = ApplyLens(nodes, edges, lens, selectedQuestId);
        var finalNodes = nodes.Values
            .Where(node => visibleNodeIds.Contains(node.Id))
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var finalEdges = edges
            .Where(edge => visibleNodeIds.Contains(edge.SourceId) && visibleNodeIds.Contains(edge.TargetId))
            .OrderBy(edge => edge.SourceId, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Kind)
            .ThenBy(edge => edge.Label, StringComparer.Ordinal)
            .ThenBy(edge => edge.Provenance, StringComparer.Ordinal)
            .ToArray();

        return new QuestGraphSnapshot(
            finalNodes,
            finalEdges,
            diagnostics.Distinct(StringComparer.Ordinal).OrderBy(message => message, StringComparer.Ordinal),
            selectedQuestId);
    }

    /// <summary>Convenience overload for callers that only need to set a lens.</summary>
    public static QuestGraphSnapshot Build(
        IEnumerable<QuestEditorModel>? quests,
        QuestGraphLens? lens) =>
        BuildCore(quests, selectedQuest: null, lens);

    /// <summary>Builds a graph for one quest.</summary>
    public static QuestGraphSnapshot Build(
        QuestEditorModel? quest,
        QuestGraphLens? lens = null) =>
        BuildCore(quest == null ? Enumerable.Empty<QuestEditorModel>() : new[] { quest }, quest, lens);

    /// <summary>Stable id used by quest nodes and dangling quest references.</summary>
    public static string QuestNodeId(int questId) => $"quest:{questId}";

    private static void AddQuestNode(
        IDictionary<string, QuestGraphNodeModel> nodes,
        QuestEditorModel quest,
        bool isDangling)
    {
        string title = string.IsNullOrWhiteSpace(quest.Name) ? $"Quest {quest.Id}" : quest.Name;
        string subtitle = $"#{quest.Id}";
        nodes.TryAdd(
            QuestNodeId(quest.Id),
            new QuestGraphNodeModel(
                QuestNodeId(quest.Id),
                title,
                subtitle,
                quest.Id,
                isDangling ? QuestGraphNodeKind.DanglingQuest : QuestGraphNodeKind.Quest,
                isDangling,
                $"QuestEditorModel[{quest.Id}]"));
    }

    private static void AddDanglingTarget(
        IDictionary<string, QuestGraphNodeModel> nodes,
        int targetQuestId,
        QuestGraphLens lens,
        IList<string> diagnostics)
    {
        if (targetQuestId == 0)
            return;

        if (!lens.IncludeDanglingTargets)
        {
            diagnostics.Add($"Quest target {targetQuestId} is not shown because dangling targets are disabled.");
            return;
        }

        string id = QuestNodeId(targetQuestId);
        nodes.TryAdd(
            id,
            new QuestGraphNodeModel(
                id,
                $"Quest {targetQuestId}",
                "Dangling target",
                targetQuestId,
                QuestGraphNodeKind.DanglingQuest,
                isDangling: true,
                provenance: $"Dangling quest target[{targetQuestId}]"));
    }

    private static void BuildActEdges(
        QuestEditorModel quest,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens,
        ISet<int> knownQuestIds)
    {
        if (!lens.IncludeActs)
            return;

        string sourceId = QuestNodeId(quest.Id);
        AddActCollectionEdges(quest, quest.ActStartInfo, "start", sourceId, nodes, edges, edgeKeys, diagnostics, lens, knownQuestIds);
        AddActCollectionEdges(quest, quest.ActEndInfo, "end", sourceId, nodes, edges, edgeKeys, diagnostics, lens, knownQuestIds);
    }

    private static void AddActCollectionEdges(
        QuestEditorModel quest,
        IEnumerable<QuestEditorActInfoModel>? acts,
        string phase,
        string sourceId,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens,
        ISet<int> knownQuestIds)
    {
        if (acts == null)
            return;

        int index = 0;
        foreach (QuestEditorActInfoModel act in acts)
        {
            if (act == null)
            {
                index++;
                continue;
            }

            string provenance = $"Act{Capitalize(phase)}Info[{index}]";
            if (act.ActType == QuestEditorActType.NextQuest)
            {
                int targetId = SafeQuestId(act.Amount);
                if (targetId == 0)
                {
                    if (act.Amount != 0)
                        diagnostics.Add($"{provenance}.nextQuest has an invalid target id {act.Amount}.");
                }
                else
                {
                    TryAddQuestEdge(
                        sourceId,
                        targetId,
                        QuestGraphEdgeKind.NextQuest,
                        $"Act {phase} · nextQuest",
                        $"{provenance}.nextQuest",
                        nodes,
                        edges,
                        edgeKeys,
                        diagnostics,
                        lens,
                        knownQuestIds,
                        relationship: new QuestGraphRelationshipAddress(
                            quest.Id,
                            QuestGraphRelationshipKind.NextQuest,
                            ParsePhase(phase),
                            targetId,
                            modelIndex: index));
                }
            }
            else if (act.ActType == QuestEditorActType.Quest && lens.IncludeQuestRequirements)
            {
                if (act.QuestReqs == null || act.QuestReqs.Count == 0)
                {
                    diagnostics.Add($"{provenance}.quest has no quest requirements.");
                }
                else
                {
                    for (int reqIndex = 0; reqIndex < act.QuestReqs.Count; reqIndex++)
                    {
                        QuestEditorQuestReqModel? req = act.QuestReqs[reqIndex];
                        if (req == null)
                            continue;
                        int targetId = req.QuestId;
                        if (targetId == 0)
                        {
                            diagnostics.Add($"{provenance}.QuestReqs[{reqIndex}] has an empty target id.");
                            continue;
                        }

                        TryAddQuestEdge(
                            sourceId,
                            targetId,
                            QuestGraphEdgeKind.ActQuestRequirement,
                            $"Act {phase} · quest ({req.QuestState})",
                            $"{provenance}.QuestReqs[{reqIndex}]",
                            nodes,
                            edges,
                            edgeKeys,
                            diagnostics,
                            lens,
                            knownQuestIds);
                    }
                }
            }
            index++;
        }
    }

    private static void BuildCheckEdges(
        QuestEditorModel quest,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens,
        ISet<int> knownQuestIds)
    {
        if (!lens.IncludeChecks || !lens.IncludeQuestRequirements)
            return;

        string sourceId = QuestNodeId(quest.Id);
        AddCheckCollectionEdges(quest.CheckStartInfo, "start", sourceId, nodes, edges, edgeKeys, diagnostics, lens, knownQuestIds);
        AddCheckCollectionEdges(quest.CheckEndInfo, "end", sourceId, nodes, edges, edgeKeys, diagnostics, lens, knownQuestIds);
    }

    private static void AddCheckCollectionEdges(
        IEnumerable<QuestEditorCheckInfoModel>? checks,
        string phase,
        string sourceId,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens,
        ISet<int> knownQuestIds)
    {
        if (checks == null)
            return;

        if (lens.ExpandRequirementTrees)
        {
            AddExpandedRequirementTree(
                checks,
                phase,
                sourceId,
                nodes,
                edges,
                edgeKeys,
                diagnostics,
                lens,
                knownQuestIds);
            return;
        }

        int index = 0;
        foreach (QuestEditorCheckInfoModel check in checks)
        {
            if (check?.CheckType == QuestEditorCheckType.Quest)
            {
                string provenance = $"Check{Capitalize(phase)}Info[{index}]";
                if (check.QuestReqs == null || check.QuestReqs.Count == 0)
                {
                    diagnostics.Add($"{provenance}.QuestReqs is empty.");
                }
                else
                {
                    for (int reqIndex = 0; reqIndex < check.QuestReqs.Count; reqIndex++)
                    {
                        QuestEditorQuestReqModel? req = check.QuestReqs[reqIndex];
                        if (req == null)
                            continue;
                        if (req.QuestId == 0)
                        {
                            diagnostics.Add($"{provenance}.QuestReqs[{reqIndex}] has an empty target id.");
                            continue;
                        }

                        TryAddPrerequisiteEdge(
                            sourceId,
                            req.QuestId,
                            QuestGraphEdgeKind.CheckQuestRequirement,
                            $"Check {phase} · quest ({req.QuestState})",
                            $"{provenance}.QuestReqs[{reqIndex}]",
                            nodes,
                            edges,
                            edgeKeys,
                            diagnostics,
                            lens,
                            knownQuestIds,
                            relationship: new QuestGraphRelationshipAddress(
                                ownerQuestId: ParseQuestId(sourceId) ?? 0,
                                kind: QuestGraphRelationshipKind.CheckQuestRequirement,
                                phase: ParsePhase(phase),
                                targetQuestId: req.QuestId,
                                questState: req.QuestState,
                                modelIndex: index,
                                requirementIndex: reqIndex));
                    }
                }
            }
            index++;
        }
    }

    private static void AddExpandedRequirementTree(
        IEnumerable<QuestEditorCheckInfoModel> checks,
        string phase,
        string dependentId,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens,
        ISet<int> knownQuestIds)
    {
        (QuestEditorCheckInfoModel Check, int ModelIndex)[] materialized = checks
            .Select((check, modelIndex) => (check, modelIndex))
            .Where(item => item.check != null)
            .Select(item => (item.check!, item.modelIndex))
            .ToArray();
        if (materialized.Length == 0)
            return;

        string groupId = $"{dependentId}:requirements:{phase}";
        AddNode(
            nodes,
            new QuestGraphNodeModel(
                groupId,
                $"{Capitalize(phase)} requirements",
                $"ALL · {materialized.Length} condition{(materialized.Length == 1 ? string.Empty : "s")}",
                ParseQuestId(dependentId),
                QuestGraphNodeKind.RequirementGroup,
                provenance: $"Check{Capitalize(phase)}Info"));
        AddEdge(
            edges,
            edgeKeys,
            groupId,
            dependentId,
            QuestGraphEdgeKind.RequirementGroup,
            $"gates {phase}",
            $"Check{Capitalize(phase)}Info");

        for (int index = 0; index < materialized.Length; index++)
        {
            QuestEditorCheckInfoModel check = materialized[index].Check;
            int modelIndex = materialized[index].ModelIndex;
            string provenance = $"Check{Capitalize(phase)}Info[{modelIndex}]";

            if (check.CheckType == QuestEditorCheckType.Quest && check.QuestReqs != null && check.QuestReqs.Count > 0)
            {
                for (int reqIndex = 0; reqIndex < check.QuestReqs.Count; reqIndex++)
                {
                    QuestEditorQuestReqModel req = check.QuestReqs[reqIndex];
                    if (req == null || req.QuestId == 0)
                    {
                        diagnostics.Add($"{provenance}.QuestReqs[{reqIndex}] has an empty target id.");
                        continue;
                    }

                    if (!knownQuestIds.Contains(req.QuestId))
                        AddDanglingTarget(nodes, req.QuestId, lens, diagnostics);
                    string prerequisiteId = QuestNodeId(req.QuestId);
                    if (!nodes.ContainsKey(prerequisiteId))
                        continue;

                    AddEdge(
                        edges,
                        edgeKeys,
                        prerequisiteId,
                        groupId,
                        QuestGraphEdgeKind.CheckQuestRequirement,
                        $"quest {req.QuestId} = {req.QuestState}",
                        $"{provenance}.QuestReqs[{reqIndex}]",
                        new QuestGraphRelationshipAddress(
                            ownerQuestId: ParseQuestId(dependentId) ?? 0,
                            kind: QuestGraphRelationshipKind.CheckQuestRequirement,
                            phase: ParsePhase(phase),
                            targetQuestId: req.QuestId,
                            questState: req.QuestState,
                            modelIndex: modelIndex,
                            requirementIndex: reqIndex));
                }
                continue;
            }

            string predicateId = $"{groupId}:predicate:{index}";
            AddNode(
                nodes,
                new QuestGraphNodeModel(
                    predicateId,
                    DescribeCheck(check),
                    check.CheckType.ToOriginalString(),
                    ParseQuestId(dependentId),
                    QuestGraphNodeKind.Requirement,
                    provenance: provenance));
            AddEdge(
                edges,
                edgeKeys,
                predicateId,
                groupId,
                QuestGraphEdgeKind.RequirementPredicate,
                check.CheckType.ToOriginalString(),
                provenance);
        }
    }

    private static string DescribeCheck(QuestEditorCheckInfoModel check)
    {
        string type = check.CheckType.ToOriginalString();
        List<string> details = [];
        if (check.Amount != 0)
            details.Add(check.Amount.ToString());
        if (!string.IsNullOrWhiteSpace(check.Text))
            details.Add(check.Text);
        if (check.Boolean)
            details.Add("enabled");
        if (check.Date != default)
            details.Add(check.Date.ToString("yyyy-MM-dd"));
        if (check.SelectedNumbersItem?.Count > 0)
            details.Add($"{check.SelectedNumbersItem.Count} values");
        if (check.SelectedReqItems?.Count > 0)
            details.Add($"{check.SelectedReqItems.Count} items");
        if (check.Skills?.Count > 0)
            details.Add($"{check.Skills.Count} skills");
        if (check.Jobs?.Count > 0)
            details.Add($"{check.Jobs.Count} jobs");
        if (check.MobReqs?.Count > 0)
            details.Add($"{check.MobReqs.Count} mobs");
        if (check.QuestInfo?.Count > 0)
            details.Add($"{check.QuestInfo.Count} info");
        if (check.QuestInfoEx?.Count > 0)
            details.Add($"{check.QuestInfoEx.Count} infoEx");
        return details.Count == 0 ? type : $"{type}: {string.Join(", ", details)}";
    }

    private static void BuildConversationEdges(
        QuestEditorModel quest,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens)
    {
        if (!lens.IncludeConversations)
            return;

        string sourceId = QuestNodeId(quest.Id);
        AddSayCollectionEdges(quest.SayInfoStartQuest, "start", sourceId, nodes, edges, edgeKeys, diagnostics, lens);
        AddSayCollectionEdges(quest.SayInfoEndQuest, "end", sourceId, nodes, edges, edgeKeys, diagnostics, lens);

        if (!lens.IncludeStopResponses)
            return;

        AddStopCollectionEdges(quest.SayInfoStop_StartQuest, "start", sourceId, nodes, edges, edgeKeys, diagnostics, lens);
        AddStopCollectionEdges(quest.SayInfoStop_EndQuest, "end", sourceId, nodes, edges, edgeKeys, diagnostics, lens);
    }

    private static void AddSayCollectionEdges(
        IEnumerable<QuestEditorSayModel>? conversations,
        string phase,
        string sourceId,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens)
    {
        if (conversations == null)
            return;

        int index = 0;
        string previousId = sourceId;
        foreach (QuestEditorSayModel conversation in conversations)
        {
            if (conversation == null)
            {
                index++;
                continue;
            }

            string provenance = $"SayInfo{Capitalize(phase)}Quest[{index}]";
            string conversationId = $"{sourceId}:say:{phase}:{index}";
            string title = string.IsNullOrWhiteSpace(conversation.NpcConversation)
                ? $"Conversation {index + 1}"
                : conversation.NpcConversation;
            AddNode(
                nodes,
                new QuestGraphNodeModel(
                    conversationId,
                    title,
                    $"Say {phase} · {conversation.ConversationType}",
                    questId: ParseQuestId(sourceId),
                    QuestGraphNodeKind.Conversation,
                    provenance: provenance));
            AddEdge(
                edges,
                edgeKeys,
                previousId,
                conversationId,
                QuestGraphEdgeKind.Conversation,
                index == 0 ? $"Say {phase} · entry" : "next",
                provenance);

            if (lens.IncludeResponses)
            {
                AddResponseEdges(conversationId, "yes", conversation.YesResponses, provenance, nodes, edges, edgeKeys, QuestGraphEdgeKind.ConversationResponse, lens, questId: ParseQuestId(sourceId));
                AddResponseEdges(conversationId, "no", conversation.NoResponses, provenance, nodes, edges, edgeKeys, QuestGraphEdgeKind.ConversationResponse, lens, questId: ParseQuestId(sourceId));
                AddResponseEdges(conversationId, "ask", conversation.AskResponses, provenance, nodes, edges, edgeKeys, QuestGraphEdgeKind.ConversationResponse, lens, questId: ParseQuestId(sourceId));
            }

            previousId = conversationId;
            index++;
        }
    }

    private static void AddStopCollectionEdges(
        IEnumerable<QuestEditorSayEndQuestModel>? stopConversations,
        string phase,
        string sourceId,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens)
    {
        if (stopConversations == null)
            return;

        int index = 0;
        foreach (QuestEditorSayEndQuestModel stopConversation in stopConversations)
        {
            if (stopConversation == null)
            {
                index++;
                continue;
            }

            string branch = stopConversation.ConversationType.ToString().ToLowerInvariant();
            string provenance = $"SayInfoStop_{Capitalize(phase)}Quest[{index}]";
            string stopId = $"{sourceId}:stop:{phase}:{index}:{branch}";
            AddNode(
                nodes,
                new QuestGraphNodeModel(
                    stopId,
                    $"Stop · {branch}",
                    $"Say stop {phase}",
                    ParseQuestId(sourceId),
                    QuestGraphNodeKind.StopConversation,
                    provenance: provenance));
            AddEdge(
                edges,
                edgeKeys,
                sourceId,
                stopId,
                QuestGraphEdgeKind.StopConversation,
                $"Say stop {phase} · {branch}",
                provenance);

            if (lens.IncludeResponses)
            {
                AddResponseEdges(stopId, branch, stopConversation.Responses, provenance, nodes, edges, edgeKeys, QuestGraphEdgeKind.StopResponse, lens, ParseQuestId(sourceId), stopResponse: true);
            }

            index++;
        }
    }

    private static void AddResponseEdges(
        string sourceId,
        string branch,
        IEnumerable<QuestEditorSayResponseModel>? responses,
        string provenance,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        QuestGraphEdgeKind edgeKind,
        QuestGraphLens lens,
        int? questId,
        bool stopResponse = false)
    {
        if (!lens.IncludeResponses || responses == null)
            return;

        int index = 0;
        foreach (QuestEditorSayResponseModel response in responses)
        {
            if (response == null)
            {
                index++;
                continue;
            }

            string responseId = $"{sourceId}:response:{branch}:{index}";
            string responseProvenance = $"{provenance}.{branch}[{index}]";
            AddNode(
                nodes,
                new QuestGraphNodeModel(
                    responseId,
                    string.IsNullOrWhiteSpace(response.Text) ? $"{branch} response {index + 1}" : response.Text,
                    stopResponse ? $"Stop · {branch}" : $"{branch} response {index + 1}",
                    questId,
                    QuestGraphNodeKind.Response,
                    provenance: responseProvenance));
            AddEdge(
                edges,
                edgeKeys,
                sourceId,
                responseId,
                edgeKind,
                branch,
                responseProvenance);
            index++;
        }
    }

    private static void TryAddQuestEdge(
        string sourceId,
        int targetQuestId,
        QuestGraphEdgeKind kind,
        string label,
        string provenance,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens,
        ISet<int> knownQuestIds,
        QuestGraphRelationshipAddress? relationship = null)
    {
        if (targetQuestId <= 0)
        {
            diagnostics.Add($"{provenance} has an invalid target id {targetQuestId}.");
            return;
        }

        if (!knownQuestIds.Contains(targetQuestId))
        {
            AddDanglingTarget(nodes, targetQuestId, lens, diagnostics);
            if (lens.IncludeDanglingTargets)
                diagnostics.Add($"{provenance} references missing quest target {targetQuestId}; a dangling node was created.");
        }

        string targetId = QuestNodeId(targetQuestId);
        if (!nodes.ContainsKey(targetId))
            return;

        AddEdge(edges, edgeKeys, sourceId, targetId, kind, label, provenance, relationship);
    }

    private static void TryAddPrerequisiteEdge(
        string dependentId,
        int prerequisiteQuestId,
        QuestGraphEdgeKind kind,
        string label,
        string provenance,
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        IList<string> diagnostics,
        QuestGraphLens lens,
        ISet<int> knownQuestIds,
        QuestGraphRelationshipAddress? relationship = null)
    {
        if (prerequisiteQuestId <= 0)
        {
            diagnostics.Add($"{provenance} has an invalid prerequisite id {prerequisiteQuestId}.");
            return;
        }

        if (!knownQuestIds.Contains(prerequisiteQuestId))
            AddDanglingTarget(nodes, prerequisiteQuestId, lens, diagnostics);

        string prerequisiteId = QuestNodeId(prerequisiteQuestId);
        if (!nodes.ContainsKey(prerequisiteId))
            return;

        AddEdge(edges, edgeKeys, prerequisiteId, dependentId, kind, label, provenance, relationship);
    }

    private static void AddNode(IDictionary<string, QuestGraphNodeModel> nodes, QuestGraphNodeModel node) =>
        nodes.TryAdd(node.Id, node);

    private static void AddEdge(
        ICollection<QuestGraphEdgeModel> edges,
        ISet<string> edgeKeys,
        string sourceId,
        string targetId,
        QuestGraphEdgeKind kind,
        string label,
        string provenance,
        QuestGraphRelationshipAddress? relationship = null)
    {
        string key = $"{sourceId}\u001f{targetId}\u001f{kind}\u001f{label}\u001f{provenance}";
        if (!edgeKeys.Add(key))
            return;
        edges.Add(new QuestGraphEdgeModel(
            $"edge:{SanitizeId(sourceId)}->{SanitizeId(targetId)}:{kind}:{edgeKeys.Count}",
            sourceId,
            targetId,
            kind,
            label,
            provenance,
            relationship,
            relationship == null ? ReadOnlyReasonFor(kind) : null));
    }

    private static HashSet<string> ApplyLens(
        IDictionary<string, QuestGraphNodeModel> nodes,
        IList<QuestGraphEdgeModel> edges,
        QuestGraphLens lens,
        int? selectedQuestId)
    {
        var visible = new HashSet<string>(nodes.Keys, StringComparer.Ordinal);

        foreach (QuestGraphNodeModel node in nodes.Values)
        {
            if (node.Kind == QuestGraphNodeKind.DanglingQuest && !lens.IncludeDanglingTargets)
                visible.Remove(node.Id);
            else if ((node.Kind == QuestGraphNodeKind.Conversation || node.Kind == QuestGraphNodeKind.StopConversation) && !lens.IncludeConversations)
                visible.Remove(node.Id);
            else if (node.Kind == QuestGraphNodeKind.StopConversation && !lens.IncludeStopResponses)
                visible.Remove(node.Id);
            else if (node.Kind == QuestGraphNodeKind.Response && (!lens.IncludeResponses || !lens.IncludeConversations))
                visible.Remove(node.Id);
            else if (node.Kind == QuestGraphNodeKind.Response && !lens.IncludeStopResponses &&
                     node.Provenance.Contains("SayInfoStop_", StringComparison.Ordinal))
                visible.Remove(node.Id);
        }

        var edgeVisible = edges.Where(edge =>
            visible.Contains(edge.SourceId) && visible.Contains(edge.TargetId) &&
            (lens.IncludeActs || (edge.Kind != QuestGraphEdgeKind.NextQuest && edge.Kind != QuestGraphEdgeKind.ActQuestRequirement)) &&
            (lens.IncludeChecks || edge.Kind != QuestGraphEdgeKind.CheckQuestRequirement) &&
            (lens.IncludeQuestRequirements || (edge.Kind != QuestGraphEdgeKind.ActQuestRequirement && edge.Kind != QuestGraphEdgeKind.CheckQuestRequirement)) &&
            (lens.IncludeStopResponses || (edge.Kind != QuestGraphEdgeKind.StopConversation && edge.Kind != QuestGraphEdgeKind.StopResponse)))
            .ToArray();

        if (!lens.IncludeUnrelatedQuests && (selectedQuestId ?? lens.FocusQuestId).HasValue)
        {
            string focusId = QuestNodeId((selectedQuestId ?? lens.FocusQuestId)!.Value);
            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string id in visible)
                adjacency[id] = new List<string>();
            foreach (QuestGraphEdgeModel edge in edgeVisible)
            {
                if (!adjacency.ContainsKey(edge.SourceId) || !adjacency.ContainsKey(edge.TargetId))
                    continue;
                // Treat the focus as a connected-component root.  This keeps
                // prerequisite and successor links in a selected quest view.
                adjacency[edge.SourceId].Add(edge.TargetId);
                adjacency[edge.TargetId].Add(edge.SourceId);
            }

            var connected = new HashSet<string>(StringComparer.Ordinal);
            if (adjacency.ContainsKey(focusId))
            {
                var queue = new Queue<(string id, int depth)>();
                queue.Enqueue((focusId, 0));
                connected.Add(focusId);
                while (queue.Count > 0)
                {
                    (string id, int depth) = queue.Dequeue();
                    if (lens.MaxDepth >= 0 && depth >= lens.MaxDepth)
                        continue;
                    foreach (string neighbour in adjacency[id].OrderBy(value => value, StringComparer.Ordinal))
                    {
                        if (connected.Add(neighbour))
                            queue.Enqueue((neighbour, depth + 1));
                    }
                }
            }

            visible.IntersectWith(connected);
        }

        return visible;
    }

    private static int SafeQuestId(long amount) => amount is >= int.MinValue and <= int.MaxValue ? (int)amount : 0;

    private static int? ParseQuestId(string sourceId)
    {
        const string prefix = "quest:";
        if (sourceId.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(sourceId.AsSpan(prefix.Length), out int questId))
            return questId;
        return null;
    }

    private static string SanitizeId(string value) => value.Replace(':', '_').Replace(' ', '_');

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static QuestGraphRelationshipPhase ParsePhase(string phase) =>
        string.Equals(phase, "end", StringComparison.OrdinalIgnoreCase)
            ? QuestGraphRelationshipPhase.End
            : QuestGraphRelationshipPhase.Start;

    private static string ReadOnlyReasonFor(QuestGraphEdgeKind kind) => "QuestEditor_GraphReadOnly";
}
