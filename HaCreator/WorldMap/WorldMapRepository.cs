using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.WorldMap;

public sealed record WorldMapAssetInfo(string ImageName, string RelativePath, bool IsExclusion, string LogicalName, string ParentName);

/// <summary>Repository facade for lazy WorldMap loading and source-aware commits.</summary>
public sealed class WorldMapRepository
{
    private readonly WorldMapSourceOperations _operations;
    private readonly Dictionary<string, string> _revisions = new(StringComparer.OrdinalIgnoreCase);

    public WorldMapRepository(IDataSource source) => _operations = new WorldMapSourceOperations(source);
    public IDataSource DataSource => _operations.DataSource;
    public WorldMapSourceOperations Operations => _operations;
    public WorldMapSourceCapabilities Capabilities => _operations.Capabilities;

    public IReadOnlyList<string> EnumerateNames() => _operations.EnumerateImageNames();

    public IReadOnlyList<WorldMapAssetInfo> EnumerateAssets()
    {
        var result = new List<WorldMapAssetInfo>();
        foreach (string name in EnumerateNames())
        {
            bool exclusion = WorldMapExclusionList.IsExclusionImage(name);
            if (exclusion)
            {
                result.Add(new WorldMapAssetInfo(name, $"WorldMap/{name}.img", true, name, string.Empty));
                continue;
            }
            WzImage image = _operations.Load(name);
            if (image == null) { result.Add(new WorldMapAssetInfo(name, $"WorldMap/{name}.img", false, name, string.Empty)); continue; }
            try
            {
                WorldMapDocument doc = WorldMapCodec.Read(image);
                result.Add(new WorldMapAssetInfo(name, $"WorldMap/{name}.img", false, doc.Surface.LogicalName, doc.Surface.ParentName));
            }
            catch { result.Add(new WorldMapAssetInfo(name, $"WorldMap/{name}.img", false, name, string.Empty)); }
        }
        return result;
    }

    public WorldMapDocument Load(string imageName)
    {
        WzImage image = _operations.Load(imageName) ?? throw new InvalidOperationException($"WorldMap image not found: {imageName}");
        WorldMapDocument document = WorldMapCodec.Read(image);
        _revisions[Normalize(imageName)] = Revision(image);
        return document;
    }

    public WorldMapDocument Create(string imageName, string logicalName = null) =>
        WorldMapDocument.CreateNew(imageName, logicalName);

    public WorldMapDocument Duplicate(string sourceImageName, string destinationImageName, string logicalName = null)
    {
        WorldMapDocument source = Load(sourceImageName).DeepClone();
        source.ImageName = destinationImageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? destinationImageName : destinationImageName + ".img";
        source.Surface.LogicalName = logicalName ?? source.Surface.LogicalName;
        source.MarkNew();
        return source;
    }

    public WorldMapBatchSaveResult Save(WorldMapDocument document, bool verifyReload = true)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        string key = Normalize(document.ImageName);
        if (_revisions.TryGetValue(key, out string expected))
        {
            WzImage current = _operations.Load(document.ImageName);
            if (current == null || !string.Equals(expected, Revision(current), StringComparison.Ordinal))
                return new WorldMapBatchSaveResult(false, Array.Empty<string>(), new[] { $"External revision conflict for {document.ImageName}." }, _operations.Mode);
        }
        WzImage candidate;
        try { candidate = WorldMapWriter.Write(document); }
        catch (Exception ex) { return new WorldMapBatchSaveResult(false, Array.Empty<string>(), new[] { ex.Message }, _operations.Mode); }
        WorldMapBatchSaveResult result = _operations.SaveBatch(new[] { new WorldMapImageCandidate(document.ImageName, candidate) });
        if (!result.Succeeded || !verifyReload) return result;
        try
        {
            WorldMapDocument reopened = Load(document.ImageName);
            if (!SemanticEquals(document, reopened))
                return result with { Succeeded = false, Errors = result.Errors.Concat(new[] { "Reload verification changed edited WorldMap semantics." }).ToArray() };
            document.ReplaceRawImage(candidate.DeepClone());
            document.AcceptChanges();
            return result;
        }
        catch (Exception ex)
        {
            return result with { Succeeded = false, Errors = result.Errors.Concat(new[] { $"Reload verification failed: {ex.Message}" }).ToArray() };
        }
    }

    public bool Delete(string imageName, WorldMapHierarchyIndex hierarchy, out string error)
    {
        error = null;
        if (hierarchy != null && hierarchy.GetInboundReferences(WorldMapCodecName(imageName)).Count > 0)
        { error = "The surface still has inbound hierarchy references."; return false; }
        return _operations.StageDelete(imageName, out _, out error);
    }

    private static string WorldMapCodecName(string imageName) => imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? imageName[..^4] : imageName;
    private static string Normalize(string name)
    {
        string value = (name ?? string.Empty).Trim();
        if (value.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) value = value[..^4];
        return value.ToLowerInvariant();
    }
    private static string Revision(WzImage image)
    {
        if (image == null) return string.Empty;
        image.ParseImage();
        var value = new StringBuilder(1024)
            .Append(image.Name).Append('|').Append(image.Checksum).Append('|').Append(image.BlockSize);
        foreach (WzImageProperty property in image.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            AppendRevision(value, property);
        return value.ToString();
    }

    private static void AppendRevision(StringBuilder value, WzImageProperty property)
    {
        value.Append('\n').Append(property.GetType().Name).Append(':').Append(property.Name).Append('=');
        if (property is WzCanvasProperty canvas)
        {
            value.Append(canvas.PngProperty?.Width).Append('x').Append(canvas.PngProperty?.Height)
                .Append(':').Append(canvas.PngProperty?.Format);
        }
        else if (property.WzProperties == null || property.WzProperties.Count == 0)
        {
            try { value.Append(Convert.ToString(property.WzValue, CultureInfo.InvariantCulture)); }
            catch { value.Append("<unavailable>"); }
        }
        foreach (WzImageProperty child in property.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            AppendRevision(value, child);
    }
    private static bool SemanticEquals(WorldMapDocument a, WorldMapDocument b)
    {
        if (a?.Surface == null || b?.Surface == null) return false;
        if (!string.Equals(a.Surface.LogicalName, b.Surface.LogicalName, StringComparison.Ordinal) || a.Surface.Entries.Count != b.Surface.Entries.Count) return false;
        for (int i = 0; i < a.Surface.Entries.Count; i++)
        {
            WorldMapMapEntry x = a.Surface.Entries[i], y = b.Surface.Entries[i];
            if (x.Key != y.Key || x.Type != y.Type || x.Spot != y.Spot || !x.MapIds.SequenceEqual(y.MapIds) || x.Title != y.Title || x.Description != y.Description) return false;
        }
        return true;
    }
}
