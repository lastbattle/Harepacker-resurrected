using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.WorldMap;

public sealed record WorldMapSourceProfile(
    string Name,
    WorldMapSourceMode Mode = WorldMapSourceMode.Unknown,
    bool UsesSeparatedCanvases = false,
    bool SupportsCreate = false,
    bool SupportsDelete = false,
    string WorldMapDirectory = "Map/WorldMap")
{
    public static WorldMapSourceProfile Legacy(string name = "Legacy") => new(name, WorldMapSourceMode.Img, false, true, true);
    public static WorldMapSourceProfile Modern(string name = "Modern") => new(name, WorldMapSourceMode.Img, true, true, true);
}
public sealed record WorldMapMarkerAsset(
    int Type,
    string Name,
    int Width,
    int Height,
    System.Drawing.Point Origin,
    int Z,
    int UsageCount = 0)
{
    public bool IsKnown => Width > 0 && Height > 0;
}

/// <summary>Dynamic marker picker data from Map/MapHelper.img/worldMap/mapImage.</summary>
public sealed class WorldMapMarkerRegistry
{
    private readonly Dictionary<int, WorldMapMarkerAsset> _assets = new();
    public IReadOnlyDictionary<int, WorldMapMarkerAsset> Assets => new ReadOnlyDictionary<int, WorldMapMarkerAsset>(_assets);
    public IReadOnlyList<int> Types => _assets.Keys.OrderBy(value => value).ToArray();
    public bool Contains(int type) => _assets.ContainsKey(type);
    public bool TryGet(int type, out WorldMapMarkerAsset asset) => _assets.TryGetValue(type, out asset);

    public static WorldMapMarkerRegistry FromImage(WzImage mapHelperImage)
    {
        if (mapHelperImage == null) throw new ArgumentNullException(nameof(mapHelperImage));
        return FromProperty(mapHelperImage.GetFromPath("worldMap/mapImage"));
    }

    public static WorldMapMarkerRegistry FromProperty(WzImageProperty property)
    {
        var registry = new WorldMapMarkerRegistry();
        foreach (WzImageProperty child in property?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
        {
            if (!int.TryParse(child.Name, out int type) || child is not WzCanvasProperty canvas) continue;
            System.Drawing.Point origin = (canvas[WzCanvasProperty.OriginPropertyName] as WzVectorProperty)?.Pos ?? System.Drawing.Point.Empty;
            int z = (canvas["z"] as WzIntProperty)?.Value ?? 0;
            registry._assets[type] = new WorldMapMarkerAsset(type, child.Name, canvas.PngProperty?.Width ?? 0,
                canvas.PngProperty?.Height ?? 0, origin, z);
        }
        return registry;
    }

    public WorldMapMarkerRegistry WithUsage(IEnumerable<WorldMapDocument> documents)
    {
        var usage = new Dictionary<int, int>();
        foreach (WorldMapDocument document in documents ?? Enumerable.Empty<WorldMapDocument>())
            foreach (int type in document.Surface.Entries.Select(entry => entry.Type))
                usage[type] = usage.TryGetValue(type, out int count) ? count + 1 : 1;
        var result = new WorldMapMarkerRegistry();
        foreach ((int type, WorldMapMarkerAsset asset) in _assets)
            result._assets[type] = asset with { UsageCount = usage.TryGetValue(type, out int count) ? count : 0 };
        return result;
    }
}

/// <summary>In-memory catalog for lazy/fixture-driven WorldMap documents.</summary>
public class WorldMapCatalog
{
    private readonly Dictionary<string, WorldMapDocument> _documents = new(StringComparer.OrdinalIgnoreCase);
    protected readonly List<string> _diagnostics = new();
    public WorldMapCatalog(IEnumerable<WorldMapDocument> documents = null, WorldMapSourceProfile profile = null)
    {
        Profile = profile ?? new WorldMapSourceProfile("Unknown");
        foreach (WorldMapDocument document in documents ?? Enumerable.Empty<WorldMapDocument>())
            if (document != null) _documents[Normalize(document.ImageName)] = document;
    }

    public WorldMapSourceProfile Profile { get; }
    public IReadOnlyList<WorldMapDocument> Documents => _documents.Values.ToArray();
    public IReadOnlyList<string> Diagnostics => _diagnostics;
    public WorldMapHierarchyIndex Hierarchy => new(Documents);
    public IReadOnlyDictionary<string, WorldMapDocument> ByImageName => new ReadOnlyDictionary<string, WorldMapDocument>(_documents);
    public WorldMapMarkerRegistry MarkerRegistry { get; private set; } = new();
    public IReadOnlyList<int> SearchExceptMapIds { get; protected set; } = Array.Empty<int>();
    public IReadOnlyList<int> SearchExceptNpcMapIds { get; protected set; } = Array.Empty<int>();

    public static WorldMapCatalog FromImages(IEnumerable<WzImage> images, WorldMapSourceProfile profile = null)
    {
        var docs = new List<WorldMapDocument>();
        var catalog = new WorldMapCatalog(null, profile);
        foreach (WzImage image in images ?? Enumerable.Empty<WzImage>())
        {
            if (image == null) continue;
            if (WorldMapCodec.IsExclusionImage(image.Name))
            {
                if (image.Name.EndsWith("ForNPC.img", StringComparison.OrdinalIgnoreCase)) catalog.SearchExceptNpcMapIds = WorldMapCodec.ReadExclusions(image);
                else catalog.SearchExceptMapIds = WorldMapCodec.ReadExclusions(image);
                continue;
            }
            try { docs.Add(WorldMapCodec.Read(image)); }
            catch (Exception exception) { catalog._diagnostics.Add($"{image.Name}: {exception.Message}"); }
        }
        foreach (WorldMapDocument document in docs) catalog._documents[Normalize(document.ImageName)] = document;
        return catalog;
    }

    public void SetMarkerRegistry(WorldMapMarkerRegistry registry) => MarkerRegistry = registry ?? new WorldMapMarkerRegistry();
    public bool TryGet(string imageName, out WorldMapDocument document) => _documents.TryGetValue(Normalize(imageName), out document);
    public IReadOnlyList<WorldMapDocument> FindByMapId(int mapId) => Documents.Where(document => document.Surface.Entries.Any(entry => entry.MapIds.Contains(mapId))).ToArray();

    private static string Normalize(string name)
    {
        string value = (name ?? string.Empty).Replace('\\', '/').Trim('/');
        if (value.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) value = value[..^4];
        int slash = value.LastIndexOf('/');
        return (slash < 0 ? value : value[(slash + 1)..]).ToLowerInvariant();
    }
}

public sealed class WorldMapSourceCatalog : WorldMapCatalog
{
    public WorldMapSourceCatalog(IEnumerable<WorldMapDocument> documents = null, WorldMapSourceProfile profile = null) : base(documents, profile) { }
    public static new WorldMapSourceCatalog FromImages(IEnumerable<WzImage> images, WorldMapSourceProfile profile = null)
    {
        WorldMapCatalog catalog = WorldMapCatalog.FromImages(images, profile);
        var result = new WorldMapSourceCatalog(catalog.Documents, profile);
        result.SetMarkerRegistry(catalog.MarkerRegistry);
        result.SearchExceptMapIds = catalog.SearchExceptMapIds;
        result.SearchExceptNpcMapIds = catalog.SearchExceptNpcMapIds;
        result._diagnostics.AddRange(catalog.Diagnostics);
        return result;
    }
}
