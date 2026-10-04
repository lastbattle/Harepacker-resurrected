using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.WorldMap;

public sealed record WorldMapHierarchyReference(string SourceName, string TargetName, string Kind, string Key);

/// <summary>Indexes parentMap and MapLink relationships without decoding canvas payloads.</summary>
public sealed class WorldMapHierarchyIndex
{
    private readonly Dictionary<string, WorldMapDocument> _byImage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorldMapDocument> _byLogical = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<WorldMapDocument>> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<WorldMapHierarchyReference>> _inbound = new(StringComparer.OrdinalIgnoreCase);

    public WorldMapHierarchyIndex(IEnumerable<WorldMapDocument> documents)
    {
        Documents = (documents ?? Enumerable.Empty<WorldMapDocument>()).Where(d => d != null).ToArray();
        foreach (WorldMapDocument document in Documents)
        {
            _byImage[Normalize(document.ImageName)] = document;
            if (!string.IsNullOrWhiteSpace(document.Surface.LogicalName) && !_byLogical.ContainsKey(document.Surface.LogicalName))
                _byLogical[document.Surface.LogicalName] = document;
        }
        BuildEdges();
    }

    public IReadOnlyList<WorldMapDocument> Documents { get; }
    public IReadOnlyList<WorldMapDocument> Roots => Documents.Where(IsRoot).ToArray();
    public IReadOnlyDictionary<string, WorldMapDocument> ByImageName => _byImage;
    public IReadOnlyDictionary<string, WorldMapDocument> ByLogicalName => _byLogical;

    public bool TryGetByImageName(string imageName, out WorldMapDocument document) => _byImage.TryGetValue(Normalize(imageName), out document);
    public bool TryGetByLogicalName(string logicalName, out WorldMapDocument document) => _byLogical.TryGetValue(logicalName ?? string.Empty, out document);
    public WorldMapDocument Find(string name)
    {
        if (TryGetByLogicalName(name, out WorldMapDocument document)) return document;
        return TryGetByImageName(name, out document) ? document : null;
    }

    public IReadOnlyList<WorldMapDocument> GetChildren(string parentName)
    {
        return parentName != null && _children.TryGetValue(parentName, out List<WorldMapDocument> children)
            ? children.ToArray() : Array.Empty<WorldMapDocument>();
    }

    public IReadOnlyList<WorldMapDocument> FindByMapId(int mapId)
    {
        if (mapId <= 0) return Array.Empty<WorldMapDocument>();
        return Documents.Where(d => d.Surface.Entries.Any(e => e.MapIds.Contains(mapId))).ToArray();
    }

    public IReadOnlyList<WorldMapHierarchyReference> GetInboundReferences(string targetName)
    {
        if (targetName != null && _inbound.TryGetValue(targetName, out List<WorldMapHierarchyReference> references))
            return references.ToArray();
        WorldMapDocument target = Find(targetName);
        if (target != null && _inbound.TryGetValue(target.Surface.LogicalName, out references))
            return references.ToArray();
        return Array.Empty<WorldMapHierarchyReference>();
    }

    public IReadOnlyList<string> GetCycleMembers()
    {
        var cycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (WorldMapDocument document in Documents)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string current = document.Surface.LogicalName;
            while (!string.IsNullOrWhiteSpace(current) && seen.Add(current))
            {
                WorldMapDocument next = Find(current);
                current = next?.Surface.ParentName;
            }
            if (!string.IsNullOrWhiteSpace(current) && seen.Contains(current)) cycles.Add(current);
        }
        return cycles.ToArray();
    }

    public bool HasCycles => GetCycleMembers().Count > 0;

    public IReadOnlyList<string> GetDuplicateLogicalNames()
    {
        return Documents.Where(document => !string.IsNullOrWhiteSpace(document.Surface.LogicalName))
            .GroupBy(document => document.Surface.LogicalName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
    }

    private void BuildEdges()
    {
        foreach (WorldMapDocument document in Documents)
        {
            string source = document.Surface.LogicalName;
            string parent = document.Surface.ParentName;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                if (!_children.TryGetValue(parent, out List<WorldMapDocument> children))
                    _children[parent] = children = new List<WorldMapDocument>();
                children.Add(document);
                AddInbound(parent, new WorldMapHierarchyReference(source, parent, "parentMap", "info/parentMap"));
            }
            foreach (WorldMapLink link in document.Surface.Links)
            {
                if (string.IsNullOrWhiteSpace(link.LinkMap)) continue;
                AddInbound(link.LinkMap, new WorldMapHierarchyReference(source, link.LinkMap, "MapLink", link.Key));
            }
        }
    }

    private void AddInbound(string target, WorldMapHierarchyReference reference)
    {
        if (!_inbound.TryGetValue(target, out List<WorldMapHierarchyReference> list))
            _inbound[target] = list = new List<WorldMapHierarchyReference>();
        list.Add(reference);
    }

    private bool IsRoot(WorldMapDocument document)
    {
        return string.IsNullOrWhiteSpace(document.Surface.ParentName)
            || !TryGetByLogicalName(document.Surface.ParentName, out _);
    }

    private static string Normalize(string name)
    {
        string value = (name ?? string.Empty).Replace('\\', '/').Trim('/');
        if (value.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) value = value[..^4];
        int slash = value.LastIndexOf('/');
        return (slash >= 0 ? value[(slash + 1)..] : value).ToLowerInvariant();
    }
}
