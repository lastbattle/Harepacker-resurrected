#nullable enable

using HaCreator.GUI.Quest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HaCreator.GUI.Quest.Graph;

/// <summary>
/// A read-only graph snapshot.  Collections are copied on construction and
/// exposed through read-only interfaces, making it safe to retain a snapshot
/// while the editor continues to change its quest models.
/// </summary>
public sealed class QuestGraphSnapshot
{
    private readonly ReadOnlyDictionary<string, QuestGraphNodeModel> _nodesById;

    public QuestGraphSnapshot(
        IEnumerable<QuestGraphNodeModel>? nodes = null,
        IEnumerable<QuestGraphEdgeModel>? edges = null,
        IEnumerable<string>? diagnostics = null,
        int? selectedQuestId = null)
    {
        Nodes = new ReadOnlyCollection<QuestGraphNodeModel>(
            (nodes ?? Array.Empty<QuestGraphNodeModel>()).ToArray());
        Edges = new ReadOnlyCollection<QuestGraphEdgeModel>(
            (edges ?? Array.Empty<QuestGraphEdgeModel>()).ToArray());
        Diagnostics = new ReadOnlyCollection<string>(
            (diagnostics ?? Array.Empty<string>()).ToArray());
        SelectedQuestId = selectedQuestId;
        _nodesById = new ReadOnlyDictionary<string, QuestGraphNodeModel>(
            Nodes.GroupBy(node => node.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
    }

    public IReadOnlyList<QuestGraphNodeModel> Nodes { get; }
    public IReadOnlyList<QuestGraphEdgeModel> Edges { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public int? SelectedQuestId { get; }

    public IReadOnlyDictionary<string, QuestGraphNodeModel> NodesById => _nodesById;

    public QuestGraphNodeModel? SelectedNode =>
        SelectedQuestId.HasValue && _nodesById.TryGetValue(
            QuestGraphBuilder.QuestNodeId(SelectedQuestId.Value), out QuestGraphNodeModel? node)
            ? node
            : null;

    /// <summary>Alias useful for controls that use the term warnings.</summary>
    public IReadOnlyList<string> Warnings => Diagnostics;

    public bool TryGetNode(string id, out QuestGraphNodeModel? node) =>
        _nodesById.TryGetValue(id, out node);
}
