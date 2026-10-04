using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.WorldMap;

namespace HaCreator.WorldMap;

/// <summary>A semantic field difference between two WorldMap documents.</summary>
public sealed record WorldMapSemanticChange(string Path, string Before, string After);

public sealed class WorldMapSemanticReport
{
    public WorldMapSemanticReport(IEnumerable<WorldMapSemanticChange> changes)
    {
        Changes = (changes ?? Enumerable.Empty<WorldMapSemanticChange>()).ToArray();
    }

    public IReadOnlyList<WorldMapSemanticChange> Changes { get; }
    public bool IsEquivalent => Changes.Count == 0;
}

/// <summary>Compares editable native fields without decoding or hashing PNG payloads.</summary>
public static class WorldMapSemanticComparer
{
    public static WorldMapSemanticReport Compare(WorldMapDocument before, WorldMapDocument after)
    {
        var changes = new List<WorldMapSemanticChange>();
        if (before == null || after == null)
        {
            if (!ReferenceEquals(before, after)) changes.Add(new WorldMapSemanticChange(string.Empty, Describe(before), Describe(after)));
            return new WorldMapSemanticReport(changes);
        }

        Add(changes, "imageName", before.ImageName, after.ImageName);
        WorldMapSurface left = before.Surface, right = after.Surface;
        if (left == null || right == null)
        {
            if (!ReferenceEquals(left, right)) changes.Add(new WorldMapSemanticChange("surface", Describe(left), Describe(right)));
            return new WorldMapSemanticReport(changes);
        }

        Add(changes, "info/WorldMap", left.LogicalName, right.LogicalName);
        Add(changes, "info/parentMap", left.ParentName, right.ParentName);
        Add(changes, "info/Memo_JP", left.MemoJp, right.MemoJp);
        CompareCanvas(changes, "BaseImg/0", left.BaseImage, right.BaseImage);
        CompareEntries(changes, left.Entries, right.Entries);
        CompareLinks(changes, left.Links, right.Links);
        CompareFog(changes, left.FogLayers, right.FogLayers);
        return new WorldMapSemanticReport(changes);
    }

    private static void CompareEntries(List<WorldMapSemanticChange> changes, IList<WorldMapMapEntry> left, IList<WorldMapMapEntry> right)
    {
        Add(changes, "MapList/count", left.Count, right.Count);
        int count = Math.Min(left.Count, right.Count);
        for (int i = 0; i < count; i++)
        {
            WorldMapMapEntry a = left[i], b = right[i];
            string path = $"MapList/{i}";
            Add(changes, path + "/key", a.Key, b.Key);
            Add(changes, path + "/type", a.Type, b.Type);
            Add(changes, path + "/spot", a.Spot, b.Spot);
            if (!a.MapIds.SequenceEqual(b.MapIds))
                Add(changes, path + "/mapNo", string.Join(",", a.MapIds), string.Join(",", b.MapIds));
            Add(changes, path + "/title", a.Title, b.Title);
            Add(changes, path + "/desc", a.Description, b.Description);
            Add(changes, path + "/townDesc", a.TownDescription, b.TownDescription);
            Add(changes, path + "/noToolTip", a.NoToolTip, b.NoToolTip);
            Add(changes, path + "/noInfo", a.NoInfo, b.NoInfo);
            Add(changes, path + "/linkQuestID", a.LinkQuestId, b.LinkQuestId);
            Add(changes, path + "/partExtend", a.PartExtend, b.PartExtend);
            CompareCanvas(changes, path + "/path", a.Path, b.Path);
        }
    }

    private static void CompareLinks(List<WorldMapSemanticChange> changes, IList<WorldMapLink> left, IList<WorldMapLink> right)
    {
        Add(changes, "MapLink/count", left.Count, right.Count);
        int count = Math.Min(left.Count, right.Count);
        for (int i = 0; i < count; i++)
        {
            WorldMapLink a = left[i], b = right[i];
            string path = $"MapLink/{i}";
            Add(changes, path + "/key", a.Key, b.Key);
            Add(changes, path + "/toolTip", a.ToolTip, b.ToolTip);
            Add(changes, path + "/spot", a.Spot, b.Spot);
            Add(changes, path + "/linkMap", a.LinkMap, b.LinkMap);
            CompareCanvas(changes, path + "/link/linkImg", a.LinkImage, b.LinkImage);
        }
    }

    private static void CompareFog(List<WorldMapSemanticChange> changes, IList<WorldMapFogLayer> left, IList<WorldMapFogLayer> right)
    {
        Add(changes, "Fog/count", left.Count, right.Count);
        int count = Math.Min(left.Count, right.Count);
        for (int i = 0; i < count; i++)
        {
            WorldMapFogLayer a = left[i], b = right[i];
            string path = $"Fog/{i}";
            Add(changes, path + "/key", a.Key, b.Key);
            Add(changes, path + "/quest", a.Quest, b.Quest);
            Add(changes, path + "/qState", a.QState, b.QState);
            CompareCanvas(changes, path + "/0", a.Image, b.Image);
        }
    }

    private static void CompareCanvas(List<WorldMapSemanticChange> changes, string path, WorldMapCanvasRef left, WorldMapCanvasRef right)
    {
        if (left == null || right == null)
        {
            Add(changes, path, left != null, right != null);
            return;
        }
        Add(changes, path + "/size", $"{left.Width}x{left.Height}", $"{right.Width}x{right.Height}");
        Add(changes, path + "/origin", left.HasOrigin ? left.Origin : (Point?)null, right.HasOrigin ? right.Origin : (Point?)null);
        Add(changes, path + "/z", left.HasZ ? left.Z : (int?)null, right.HasZ ? right.Z : (int?)null);
        Add(changes, path + "/_inlink", left.Inlink, right.Inlink);
        Add(changes, path + "/_outlink", left.Outlink, right.Outlink);
    }

    private static void Add<T>(List<WorldMapSemanticChange> changes, string path, T before, T after)
    {
        if (EqualityComparer<T>.Default.Equals(before, after)) return;
        changes.Add(new WorldMapSemanticChange(path, Describe(before), Describe(after)));
    }

    private static string Describe(object value)
    {
        if (value == null) return string.Empty;
        if (value is IEnumerable<int> ids) return string.Join(",", ids);
        if (value is Point point) return $"{point.X},{point.Y}";
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}

public sealed record WorldMapTransactionResult(
    bool Succeeded,
    IReadOnlyList<string> AffectedImages,
    IReadOnlyList<string> Errors,
    IReadOnlyList<WorldMapDiagnostic> Diagnostics,
    WorldMapSourceMode Mode,
    bool RolledBack = false)
{
    public static WorldMapTransactionResult Failed(WorldMapSourceMode mode, IEnumerable<string> errors, IEnumerable<WorldMapDiagnostic> diagnostics = null) =>
        new(false, Array.Empty<string>(), (errors ?? Enumerable.Empty<string>()).ToArray(),
            (diagnostics ?? Enumerable.Empty<WorldMapDiagnostic>()).ToArray(), mode);
}

public sealed class WorldMapTransactionPlan
{
    internal WorldMapTransactionPlan(IEnumerable<string> affectedImages,
        IEnumerable<WorldMapDiagnostic> diagnostics,
        IEnumerable<string> errors)
    {
        AffectedImages = (affectedImages ?? Enumerable.Empty<string>()).ToArray();
        Diagnostics = (diagnostics ?? Enumerable.Empty<WorldMapDiagnostic>()).ToArray();
        Errors = (errors ?? Enumerable.Empty<string>()).ToArray();
    }

    public IReadOnlyList<string> AffectedImages { get; }
    public IReadOnlyList<WorldMapDiagnostic> Diagnostics { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool CanCommit => Errors.Count == 0 && !Diagnostics.Any(diagnostic => diagnostic.IsError);
}

/// <summary>
/// Validates, stages, and commits a set of native WorldMap images as one
/// reviewable operation.  The service itself has no WPF or renderer
/// dependencies; IDataSource ownership remains in WorldMapSourceOperations.
/// </summary>
public sealed class WorldMapTransactionService
{
    private readonly WorldMapSourceOperations _operations;
    private readonly List<WorldMapDocument> _knownDocuments;
    private readonly WorldMapMarkerRegistry _markerRegistry;

    public WorldMapTransactionService(WorldMapSourceOperations operations,
        IEnumerable<WorldMapDocument> knownDocuments = null,
        WorldMapMarkerRegistry markerRegistry = null)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _knownDocuments = (knownDocuments ?? Enumerable.Empty<WorldMapDocument>()).Where(document => document != null).ToList();
        _markerRegistry = markerRegistry;
    }

    public WorldMapSourceOperations Operations => _operations;
    public WorldMapSourceMode Mode => _operations.Mode;

    /// <summary>Stages detached candidates and validates them without writing to the source.</summary>
    public WorldMapTransactionPlan Plan(IEnumerable<WorldMapDocument> documents)
    {
        WorldMapDocument[] candidates = (documents ?? Enumerable.Empty<WorldMapDocument>()).Where(document => document != null).ToArray();
        var errors = new List<string>();
        errors.AddRange(candidates.Where(document => string.IsNullOrWhiteSpace(document.ImageName))
            .Select(_ => "WorldMap candidate image name is empty."));
        string[] duplicatePaths = candidates.GroupBy(document => NormalizeImageName(document.ImageName), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        errors.AddRange(duplicatePaths.Select(path => $"Duplicate candidate image: {path}"));
        WorldMapDocument[] all = MergeKnown(candidates);
        WorldMapValidationResult validation = WorldMapValidator.ValidateAll(all, _markerRegistry);
        errors.AddRange(validation.Errors.Select(error => error.Message));
        foreach (WorldMapDocument document in candidates)
        {
            try { _ = WorldMapWriter.ApplyToClone(document); }
            catch (Exception exception) { errors.Add($"{RelativePath(document.ImageName)}: {exception.Message}"); }
        }
        string[] paths = candidates.Select(document => RelativePath(document.ImageName)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new WorldMapTransactionPlan(paths, validation.Diagnostics, errors);
    }

    public WorldMapTransactionPlan Preview(IEnumerable<WorldMapDocument> documents) => Plan(documents);

    public WorldMapTransactionResult Commit(IEnumerable<WorldMapDocument> documents, bool verifyCandidates = true)
    {
        WorldMapDocument[] candidates = (documents ?? Enumerable.Empty<WorldMapDocument>()).Where(document => document != null).ToArray();
        if (candidates.Length == 0) return WorldMapTransactionResult.Failed(Mode, new[] { "No WorldMap documents were supplied." });

        if (candidates.Any(document => string.IsNullOrWhiteSpace(document.ImageName)))
            return WorldMapTransactionResult.Failed(Mode, new[] { "WorldMap candidate image name is empty." });

        string[] duplicatePaths = candidates.GroupBy(document => NormalizeImageName(document.ImageName), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicatePaths.Length > 0)
            return WorldMapTransactionResult.Failed(Mode, duplicatePaths.Select(path => $"Duplicate candidate image: {path}"));

        WorldMapDocument[] all = MergeKnown(candidates);
        WorldMapValidationResult validation = WorldMapValidator.ValidateAll(all, _markerRegistry);
        if (!validation.IsValid)
            return WorldMapTransactionResult.Failed(Mode, validation.Errors.Select(error => error.Message), validation.Diagnostics);

        var staged = new List<WorldMapImageCandidate>();
        var originals = new Dictionary<string, WorldMapImageCandidate>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        foreach (WorldMapDocument document in candidates)
        {
            string imageName = NormalizeImageName(document.ImageName);
            try
            {
                MapleLib.WzLib.WzImage original = _operations.Load(imageName);
                if (original != null) originals[imageName] = new WorldMapImageCandidate(imageName, original.DeepClone(), RelativePath(imageName));
                MapleLib.WzLib.WzImage detached = WorldMapWriter.ApplyToClone(document);
                if (verifyCandidates)
                {
                    WorldMapSemanticReport roundTrip = WorldMapSemanticComparer.Compare(document, WorldMapCodec.Read(detached));
                    if (!roundTrip.IsEquivalent)
                        errors.Add($"{RelativePath(imageName)}: staged candidate failed semantic round-trip ({roundTrip.Changes.Count} difference(s)).");
                }
                staged.Add(new WorldMapImageCandidate(imageName, detached, RelativePath(imageName)));
            }
            catch (Exception exception)
            {
                errors.Add($"{RelativePath(imageName)}: {exception.Message}");
            }
        }
        if (errors.Count > 0) return WorldMapTransactionResult.Failed(Mode, errors, validation.Diagnostics);

        WorldMapBatchSaveResult save = _operations.SaveBatch(staged);
        if (save.Succeeded)
        {
            foreach (WorldMapDocument document in candidates)
            {
                document.ReplaceRawImage(staged.First(candidate => string.Equals(candidate.ImageName, NormalizeImageName(document.ImageName), StringComparison.OrdinalIgnoreCase)).Image.DeepClone());
                document.AcceptChanges();
            }
            IReadOnlyList<string> affected = candidates.Select(document => RelativePath(document.ImageName)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return new WorldMapTransactionResult(true, affected, Array.Empty<string>(), validation.Diagnostics, Mode);
        }

        bool rolledBack = Rollback(staged, save.AffectedImages, originals, out IReadOnlyList<string> rollbackErrors);
        var combinedErrors = save.Errors.Concat(rollbackErrors).ToArray();
        return new WorldMapTransactionResult(false,
            save.AffectedImages.Select(RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            combinedErrors, validation.Diagnostics, Mode, rolledBack);
    }

    /// <summary>Create a detached child draft and add the reciprocal parent link to the supplied parent draft.</summary>
    public WorldMapDocument CreateChild(WorldMapDocument parent, string childImageName, string logicalName, bool addReciprocalMapLink = true)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        if (string.IsNullOrWhiteSpace(childImageName)) throw new ArgumentException("Child image name is required.", nameof(childImageName));
        if (string.IsNullOrWhiteSpace(logicalName)) throw new ArgumentException("Child logical name is required.", nameof(logicalName));
        WorldMapDocument child = WorldMapDocument.CreateNew(childImageName, logicalName);
        child.Surface.ParentName = parent.Surface.LogicalName;
        parent.IsDirty = true;
        if (addReciprocalMapLink)
        {
            WorldMapLink link = parent.Surface.AddLink();
            link.LinkMap = child.Surface.LogicalName;
        }
        return child;
    }

    public WorldMapTransactionResult CreateChildAndSave(WorldMapDocument parent, string childImageName, string logicalName, bool addReciprocalMapLink = true)
    {
        WorldMapDocument child = CreateChild(parent, childImageName, logicalName, addReciprocalMapLink);
        return Commit(new[] { parent, child });
    }

    public void Reparent(WorldMapDocument child, string newParentName)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        child.Surface.ParentName = string.IsNullOrWhiteSpace(newParentName) ? null : newParentName;
        child.IsDirty = true;
    }

    public int Reparent(WorldMapDocument child, string newParentName, IEnumerable<WorldMapDocument> documents)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        string oldParent = child.Surface.ParentName;
        Reparent(child, newParentName);
        int changed = 0;
        WorldMapDocument[] values = (documents ?? _knownDocuments).Where(value => value != null).ToArray();
        if (!string.IsNullOrWhiteSpace(oldParent))
        {
            WorldMapDocument old = values.FirstOrDefault(value => string.Equals(value.Surface.LogicalName, oldParent, StringComparison.OrdinalIgnoreCase));
            if (old != null)
            {
                int removed = old.Surface.Links.RemoveWhere(link => string.Equals(link.LinkMap, child.Surface.LogicalName, StringComparison.OrdinalIgnoreCase));
                if (removed > 0) { old.IsDirty = true; changed += removed; }
            }
        }
        if (!string.IsNullOrWhiteSpace(newParentName))
        {
            WorldMapDocument next = values.FirstOrDefault(value => string.Equals(value.Surface.LogicalName, newParentName, StringComparison.OrdinalIgnoreCase));
            if (next != null && !next.Surface.Links.Any(link => string.Equals(link.LinkMap, child.Surface.LogicalName, StringComparison.OrdinalIgnoreCase)))
            {
                next.Surface.AddLink().LinkMap = child.Surface.LogicalName;
                next.IsDirty = true;
                changed++;
            }
        }
        return changed;
    }

    /// <summary>Renames a logical surface and rewrites all native inbound references in the supplied set.</summary>
    public int RenameLogical(WorldMapDocument document, string newLogicalName, IEnumerable<WorldMapDocument> documents = null)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (string.IsNullOrWhiteSpace(newLogicalName)) throw new ArgumentException("Logical name is required.", nameof(newLogicalName));
        string old = document.Surface.LogicalName;
        if (string.Equals(old, newLogicalName, StringComparison.Ordinal)) return 0;
        document.Surface.LogicalName = newLogicalName;
        document.IsDirty = true;
        int changed = 0;
        IEnumerable<WorldMapDocument> rewriteSet = (documents ?? _knownDocuments).Where(value => value != null).Concat(new[] { document }).Distinct();
        foreach (WorldMapDocument candidate in rewriteSet)
        {
            if (ReferenceEquals(candidate, document)) continue;
            if (string.Equals(candidate.Surface.ParentName, old, StringComparison.OrdinalIgnoreCase))
            {
                candidate.Surface.ParentName = newLogicalName;
                candidate.IsDirty = true;
                changed++;
            }
            foreach (WorldMapLink link in candidate.Surface.Links)
            {
                if (!string.Equals(link.LinkMap, old, StringComparison.OrdinalIgnoreCase)) continue;
                link.LinkMap = newLogicalName;
                candidate.IsDirty = true;
                changed++;
            }
        }
        return changed;
    }

    /// <summary>Removes hierarchy membership while preserving the IMG asset and map markers.</summary>
    public int RemoveFromHierarchy(WorldMapDocument document, IEnumerable<WorldMapDocument> documents = null)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        string logical = document.Surface.LogicalName;
        int changed = 0;
        if (document.Surface.ParentName != null) { document.Surface.ParentName = null; changed++; }
        document.IsDirty = true;
        foreach (WorldMapDocument candidate in (documents ?? _knownDocuments).Where(value => value != null))
        {
            int removed = candidate.Surface.Links.RemoveWhere(link => string.Equals(link.LinkMap, logical, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) { candidate.IsDirty = true; changed += removed; }
        }
        return changed;
    }

    /// <summary>Deletes only when no parentMap or MapLink inbound references remain.</summary>
    public WorldMapTransactionResult Delete(WorldMapDocument document, IEnumerable<WorldMapDocument> documents = null)
    {
        if (document == null) return WorldMapTransactionResult.Failed(Mode, new[] { "WorldMap document is null." });
        WorldMapDocument[] values = (documents ?? _knownDocuments).Where(value => value != null).ToArray();
        var hierarchy = new WorldMapHierarchyIndex(values);
        IReadOnlyList<WorldMapHierarchyReference> inbound = hierarchy.GetInboundReferences(document.Surface.LogicalName);
        if (inbound.Count > 0)
            return WorldMapTransactionResult.Failed(Mode, new[] { $"Cannot delete '{document.Surface.LogicalName}'; {inbound.Count} inbound hierarchy reference(s) remain." });
        if (!_operations.StageDelete(document.ImageName, out string backupPath, out string error))
            return WorldMapTransactionResult.Failed(Mode, new[] { error ?? "WorldMap deletion failed." });
        return new WorldMapTransactionResult(true, new[] { FullPath(document.ImageName) }, Array.Empty<string>(), Array.Empty<WorldMapDiagnostic>(), Mode);
    }

    private WorldMapDocument[] MergeKnown(IEnumerable<WorldMapDocument> candidates)
    {
        var merged = new Dictionary<string, WorldMapDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (WorldMapDocument document in _knownDocuments) merged[NormalizeImageName(document.ImageName)] = document;
        foreach (WorldMapDocument document in candidates) merged[NormalizeImageName(document.ImageName)] = document;
        return merged.Values.ToArray();
    }

    private bool Rollback(IEnumerable<WorldMapImageCandidate> staged,
        IEnumerable<string> affectedImages,
        IReadOnlyDictionary<string, WorldMapImageCandidate> originals,
        out IReadOnlyList<string> errors)
    {
        var diagnostics = new List<string>();
        bool complete = true;
        HashSet<string> affected = new((affectedImages ?? Enumerable.Empty<string>()).Select(NormalizeImageName), StringComparer.OrdinalIgnoreCase);
        foreach (WorldMapImageCandidate candidate in staged.Where(candidate => affected.Contains(NormalizeImageName(candidate.ImageName))))
        {
            string imageName = NormalizeImageName(candidate.ImageName);
            if (originals.TryGetValue(imageName, out WorldMapImageCandidate original))
            {
                WorldMapBatchSaveResult restore = _operations.SaveBatch(new[] { original });
                if (!restore.Succeeded) { complete = false; diagnostics.AddRange(restore.Errors); }
            }
            else
            {
                if (!_operations.StageDelete(imageName, out string backup, out string error))
                {
                    complete = false;
                    diagnostics.Add(error ?? $"Unable to remove newly-created {RelativePath(imageName)} during rollback.");
                }
            }
        }
        errors = diagnostics;
        return complete;
    }

    private static string NormalizeImageName(string imageName)
    {
        string value = (imageName ?? string.Empty).Replace('\\', '/').Trim('/');
        int slash = value.LastIndexOf('/');
        if (slash >= 0) value = value[(slash + 1)..];
        if (!value.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) value += ".img";
        return value;
    }

    private static string RelativePath(string imageName) => $"WorldMap/{NormalizeImageName(imageName)}";
    private static string FullPath(string imageName) => $"Map/{RelativePath(imageName)}";
}

public static class WorldMapDraftLayout
{
    /// <summary>Places IDs row-major with deterministic spacing and no client-specific assumptions.</summary>
    public static IReadOnlyDictionary<int, Point> Suggest(IEnumerable<int> mapIds, int columns = 5,
        int originX = 0, int originY = 0, int spacingX = 96, int spacingY = 72)
    {
        columns = Math.Max(1, columns);
        var result = new Dictionary<int, Point>();
        int index = 0;
        foreach (int mapId in (mapIds ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct())
        {
            int row = index / columns;
            int column = index % columns;
            result[mapId] = new Point(originX + column * spacingX, originY + row * spacingY);
            index++;
        }
        return result;
    }
}

internal static class WorldMapListExtensions
{
    public static int RemoveWhere(this IList<WorldMapLink> links, Func<WorldMapLink, bool> predicate)
    {
        int removed = 0;
        for (int index = links.Count - 1; index >= 0; index--)
        {
            if (!predicate(links[index])) continue;
            links.RemoveAt(index);
            removed++;
        }
        return removed;
    }
}
