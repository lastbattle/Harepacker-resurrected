#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace HaCreator.GUI.Quest.Graph;

/// <summary>Options for the deterministic layered graph layout.</summary>
public sealed class QuestGraphLayoutOptions
{
    public double NodeWidth { get; init; } = 240d;
    public double NodeHeight { get; init; } = 72d;
    public double HorizontalSpacing { get; init; } = 64d;
    public double VerticalSpacing { get; init; } = 24d;
    public double LeftMargin { get; init; } = 24d;
    public double TopMargin { get; init; } = 24d;

    internal double SafeNodeWidth => IsFinitePositive(NodeWidth) ? NodeWidth : 240d;
    internal double SafeNodeHeight => IsFinitePositive(NodeHeight) ? NodeHeight : 72d;
    internal double SafeHorizontalSpacing => IsFiniteNonNegative(HorizontalSpacing) ? HorizontalSpacing : 64d;
    internal double SafeVerticalSpacing => IsFiniteNonNegative(VerticalSpacing) ? VerticalSpacing : 24d;
    internal double SafeLeftMargin => IsFiniteNonNegative(LeftMargin) ? LeftMargin : 24d;
    internal double SafeTopMargin => IsFiniteNonNegative(TopMargin) ? TopMargin : 24d;

    private static bool IsFinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
    private static bool IsFiniteNonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}

/// <summary>
/// A deterministic Sugiyama-style layered layout.  The method returns a new
/// map of node ids to WPF rectangles and does not mutate the graph snapshot.
/// </summary>
public static class QuestGraphLayout
{
    public static IReadOnlyDictionary<string, Rect> Layout(
        QuestGraphSnapshot snapshot,
        QuestGraphLayoutOptions? options = null)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        options ??= new QuestGraphLayoutOptions();

        var nodes = snapshot.Nodes
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var nodeIds = new HashSet<string>(nodes.Select(node => node.Id), StringComparer.Ordinal);
        var edges = snapshot.Edges
            .Where(edge => nodeIds.Contains(edge.SourceId) && nodeIds.Contains(edge.TargetId))
            .OrderBy(edge => edge.SourceId, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Kind)
            .ThenBy(edge => edge.Label, StringComparer.Ordinal)
            .ToArray();

        var incoming = nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.Ordinal);
        var outgoing = nodes.ToDictionary(node => node.Id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (QuestGraphEdgeModel edge in edges)
        {
            // Self-edges do not affect a layer and should not prevent a node
            // from being considered a root.
            if (string.Equals(edge.SourceId, edge.TargetId, StringComparison.Ordinal))
                continue;
            outgoing[edge.SourceId].Add(edge.TargetId);
            incoming[edge.TargetId]++;
        }

        // Kahn's algorithm gives stable topological ordering for acyclic
        // portions.  A sorted queue means equal-rank nodes remain deterministic
        // regardless of collection insertion order.
        var queue = new SortedSet<string>(
            incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            StringComparer.Ordinal);
        var rank = nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.Ordinal);
        var processed = new HashSet<string>(StringComparer.Ordinal);

        while (queue.Count > 0)
        {
            string source = queue.Min!;
            queue.Remove(source);
            if (!processed.Add(source))
                continue;

            foreach (string target in outgoing[source].Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
            {
                rank[target] = Math.Max(rank[target], rank[source] + 1);
                incoming[target]--;
                if (incoming[target] == 0)
                    queue.Add(target);
            }
        }

        // Cycles have no zero-incoming root.  Assign their ranks from a stable
        // fixed point; each unprocessed node starts at the greatest rank of any
        // already-ranked predecessor (or zero), then receives a deterministic
        // tie-break rank within the cycle.
        foreach (QuestGraphNodeModel node in nodes.Where(item => !processed.Contains(item.Id)))
        {
            int predecessorRank = edges
                .Where(edge => edge.TargetId == node.Id && processed.Contains(edge.SourceId))
                .Select(edge => rank[edge.SourceId] + 1)
                .DefaultIfEmpty(0)
                .Max();
            rank[node.Id] = Math.Max(rank[node.Id], predecessorRank);
        }

        var layers = nodes
            .GroupBy(node => rank[node.Id])
            .OrderBy(group => group.Key)
            .ToArray();

        var result = new Dictionary<string, Rect>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            int row = 0;
            foreach (QuestGraphNodeModel node in layer.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                double x = options.SafeLeftMargin + layer.Key * (options.SafeNodeWidth + options.SafeHorizontalSpacing);
                double y = options.SafeTopMargin + row * (options.SafeNodeHeight + options.SafeVerticalSpacing);
                result[node.Id] = new Rect(x, y, options.SafeNodeWidth, options.SafeNodeHeight);
                row++;
            }
        }

        return result;
    }

    public static IReadOnlyDictionary<string, Rect> Layout(QuestGraphSnapshot snapshot) =>
        Layout(snapshot, options: null);

    public static IReadOnlyDictionary<string, Rect> Layout(
        QuestGraphSnapshot snapshot,
        double horizontalSpacing,
        double verticalSpacing)
    {
        return Layout(snapshot, new QuestGraphLayoutOptions
        {
            HorizontalSpacing = horizontalSpacing,
            VerticalSpacing = verticalSpacing,
        });
    }
}
