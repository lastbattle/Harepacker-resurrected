using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.WorldMap;

/// <summary>
/// An editable representation of one Map/WorldMap IMG image.  The native
/// image is retained by the codec so fields that are not understood by this
/// model survive a read/write cycle.
/// </summary>
public sealed class WorldMapDocument
{
    private string _imageName;

    public WorldMapDocument(string imageName, WorldMapSurface surface)
    {
        _imageName = imageName ?? string.Empty;
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
    }

    public string ImageName
    {
        get { return _imageName; }
        set { _imageName = value ?? string.Empty; }
    }

    /// <summary>Category-relative path used by IMG-backed data sources.</summary>
    public string ImagePath
    {
        get { return "WorldMap/" + ImageName; }
    }

    public WorldMapSurface Surface { get; private set; }

    /// <summary>The cloned source image used as the lossless serialization baseline.</summary>
    public WzImage RawImage { get; internal set; }

    public bool IsNew { get; internal set; }

    public bool IsDirty { get; set; }

    /// <summary>Marks this detached document as a new authoring document.</summary>
    public void MarkNew()
    {
        IsNew = true;
        Surface.IsNew = true;
        IsDirty = true;
    }

    /// <summary>Replaces the detached native baseline after an editor save.</summary>
    public void ReplaceRawImage(WzImage image)
    {
        RawImage = image;
        Surface.RawImage = image;
    }

    public static WorldMapDocument CreateNew(string imageName, string logicalName = null)
    {
        string name = imageName ?? string.Empty;
        if (!name.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            name += ".img";
        var surface = new WorldMapSurface(logicalName ?? name[..^4]);
        var canvas = new WzCanvasProperty("0") { PngProperty = new WzPngProperty() };
        using (var bitmap = new Bitmap(640, 470, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            canvas.PngProperty.PNG = bitmap;
        }
        canvas.AddProperty(new WzVectorProperty("origin", 320, 235));
        canvas.AddProperty(new WzIntProperty("z", 0));
        surface.BaseImage = new WorldMapCanvasRef(canvas);
        return new WorldMapDocument(name, surface) { IsNew = true, IsDirty = true };
    }

    public WorldMapDocument DeepClone()
    {
        WorldMapDocument clone = new(ImageName, Surface.DeepClone())
        {
            RawImage = RawImage?.DeepClone(),
            IsNew = IsNew,
            IsDirty = IsDirty
        };
        clone.Surface.AttachDocument(clone);
        return clone;
    }

    /// <summary>
    /// Re-baselines provenance after a successful native save.  This keeps a
    /// later edit that restores an old value from being mistaken for an
    /// unchanged field against the pre-save snapshot.
    /// </summary>
    public void AcceptChanges()
    {
        if (RawImage == null)
        {
            IsNew = false;
            Surface.IsNew = false;
            IsDirty = false;
            return;
        }
        WorldMapDocument baseline = WorldMapCodec.Read(RawImage);
        ImageName = baseline.ImageName;
        Surface = baseline.Surface;
        RawImage = baseline.RawImage;
        Surface.IsNew = false;
        Surface.AttachDocument(this);
        IsNew = false;
        IsDirty = false;
    }

    /// <summary>Replaces this document with a detached snapshot for undo/redo.</summary>
    public void ReplaceFrom(WorldMapDocument other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        ImageName = other.ImageName;
        Surface = other.Surface.DeepClone();
        Surface.AttachDocument(this);
        RawImage = other.RawImage?.DeepClone();
        IsNew = other.IsNew;
        IsDirty = other.IsDirty;
    }
}

public sealed class WorldMapSurface
{
    private readonly List<WorldMapMapEntry> _entries = new();
    private readonly List<WorldMapLink> _links = new();
    private readonly List<WorldMapFogLayer> _fogLayers = new();

    public WorldMapSurface(string logicalName = "")
    {
        LogicalName = logicalName ?? string.Empty;
    }

    public string LogicalName { get; set; }
    public string ParentName { get; set; }
    public string MemoJp { get; set; }
    public WorldMapCanvasRef BaseImage { get; set; }

    public IList<WorldMapMapEntry> Entries { get { return _entries; } }
    public IList<WorldMapMapEntry> MapList { get { return _entries; } }
    public IList<WorldMapLink> Links { get { return _links; } }
    public IList<WorldMapLink> MapLinks { get { return _links; } }
    public IList<WorldMapFogLayer> FogLayers { get { return _fogLayers; } }

    public bool IsNew { get; internal set; }
    public string OriginalLogicalName { get; private set; }
    public bool OriginalHasLogicalName { get; private set; }
    public string OriginalParentName { get; private set; }
    public string OriginalMemoJp { get; private set; }
    public bool OriginalHasMemoJp { get; private set; }
    public bool OriginalHasInfo { get; private set; }
    public bool OriginalHasBaseImage { get; private set; }
    public WorldMapCanvasRef OriginalBaseImage { get; private set; }
    public bool OriginalHasMapList { get; private set; }
    public bool OriginalHasMapLinks { get; private set; }
    public bool OriginalHasFog { get; private set; }
    internal WzImage RawImage;

    public void CaptureOriginalState(
        string logicalName,
        bool hasLogicalName,
        string parentName,
        string memoJp,
        bool hasMemoJp,
        bool hasInfo,
        bool hasBaseImage,
        WorldMapCanvasRef baseImage,
        bool hasMapList,
        bool hasMapLinks,
        bool hasFog)
    {
        OriginalLogicalName = logicalName;
        OriginalHasLogicalName = hasLogicalName;
        OriginalParentName = parentName;
        OriginalMemoJp = memoJp;
        OriginalHasMemoJp = hasMemoJp;
        OriginalHasInfo = hasInfo;
        OriginalHasBaseImage = hasBaseImage;
        OriginalBaseImage = baseImage?.DeepClone();
        OriginalHasMapList = hasMapList;
        OriginalHasMapLinks = hasMapLinks;
        OriginalHasFog = hasFog;
    }

    public WorldMapSurface DeepClone()
    {
        WorldMapSurface clone = new(LogicalName)
        {
            ParentName = ParentName,
            MemoJp = MemoJp,
            BaseImage = BaseImage?.DeepClone(),
            IsNew = IsNew,
            OriginalLogicalName = OriginalLogicalName,
            OriginalHasLogicalName = OriginalHasLogicalName,
            OriginalParentName = OriginalParentName,
            OriginalMemoJp = OriginalMemoJp,
            OriginalHasMemoJp = OriginalHasMemoJp,
            OriginalHasInfo = OriginalHasInfo,
            OriginalHasBaseImage = OriginalHasBaseImage,
            OriginalBaseImage = OriginalBaseImage?.DeepClone(),
            OriginalHasMapList = OriginalHasMapList,
            OriginalHasMapLinks = OriginalHasMapLinks,
            OriginalHasFog = OriginalHasFog,
            RawImage = RawImage?.DeepClone()
        };
        foreach (WorldMapMapEntry entry in _entries)
            clone._entries.Add(entry.DeepClone());
        foreach (WorldMapLink link in _links)
            clone._links.Add(link.DeepClone());
        foreach (WorldMapFogLayer fog in _fogLayers)
            clone._fogLayers.Add(fog.DeepClone());
        return clone;
    }

    internal void AttachDocument(WorldMapDocument document)
    {
        RawImage = document?.RawImage;
    }

    public WorldMapMapEntry AddEntry(string key = null)
    {
        string next = key ?? NextNumericKey(_entries.Select(e => e.Key));
        WorldMapMapEntry entry = new(next) { IsNew = true };
        _entries.Add(entry);
        return entry;
    }

    public bool RemoveEntry(WorldMapMapEntry entry) { return _entries.Remove(entry); }
    public WorldMapLink AddLink(string key = null)
    {
        string next = key ?? NextNumericKey(_links.Select(e => e.Key));
        WorldMapLink link = new(next) { IsNew = true };
        _links.Add(link);
        return link;
    }

    public bool RemoveLink(WorldMapLink link) { return _links.Remove(link); }
    public WorldMapFogLayer AddFogLayer(string key = null)
    {
        string next = key ?? NextNumericKey(_fogLayers.Select(e => e.Key));
        WorldMapFogLayer fog = new(next) { IsNew = true };
        _fogLayers.Add(fog);
        return fog;
    }

    public bool RemoveFogLayer(WorldMapFogLayer fog) { return _fogLayers.Remove(fog); }

    private static string NextNumericKey(IEnumerable<string> keys)
    {
        HashSet<int> used = new(keys.Select(k => int.TryParse(k, out int n) ? n : -1));
        int next = 0;
        while (used.Contains(next)) next++;
        return next.ToString();
    }
}

public sealed class WorldMapMapEntry
{
    private readonly List<int> _mapIds = new();

    public WorldMapMapEntry(string key = "0") { Key = key ?? string.Empty; }
    public string Key { get; set; }
    public int Type { get; set; }
    public Point Spot { get; set; }
    public IList<int> MapIds { get { return _mapIds; } }
    public IList<int> MapNo { get { return _mapIds; } }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Desc { get { return Description; } set { Description = value; } }
    public string TownDescription { get; set; }
    public string TownDesc { get { return TownDescription; } set { TownDescription = value; } }
    public int? NoToolTip { get; set; }
    public int? NoInfo { get; set; }
    public WorldMapCanvasRef Path { get; set; }
    public int? LinkQuestId { get; set; }
    public int? PartExtend { get; set; }

    public bool IsNew { get; internal set; }
    public string OriginalKey { get; private set; }
    public int OriginalType { get; private set; }
    public Point OriginalSpot { get; private set; }
    public IReadOnlyList<int> OriginalMapIds { get; private set; }
    public string OriginalTitle { get; private set; }
    public string OriginalDescription { get; private set; }
    public string OriginalTownDescription { get; private set; }
    public int? OriginalNoToolTip { get; private set; }
    public int? OriginalNoInfo { get; private set; }
    public int? OriginalLinkQuestId { get; private set; }
    public int? OriginalPartExtend { get; private set; }
    public bool OriginalHasType { get; private set; }
    public bool OriginalHasSpot { get; private set; }
    public bool OriginalHasMapNo { get; private set; }
    public bool OriginalHasTitle { get; private set; }
    public bool OriginalHasDescription { get; private set; }
    public bool OriginalHasTownDescription { get; private set; }
    public bool OriginalHasNoToolTip { get; private set; }
    public bool OriginalHasNoInfo { get; private set; }
    public bool OriginalHasPath { get; private set; }
    public bool OriginalHasLinkQuestId { get; private set; }
    public bool OriginalHasPartExtend { get; private set; }
    public WorldMapCanvasRef OriginalPath { get; private set; }

    public void CaptureOriginalState(
        string key,
        int type,
        Point spot,
        IEnumerable<int> mapIds,
        string title,
        string description,
        string townDescription,
        int? noToolTip,
        int? noInfo,
        int? linkQuestId,
        int? partExtend,
        bool hasType,
        bool hasSpot,
        bool hasMapNo,
        bool hasTitle,
        bool hasDescription,
        bool hasTownDescription,
        bool hasNoToolTip,
        bool hasNoInfo,
        bool hasPath,
        bool hasLinkQuestId,
        bool hasPartExtend,
        WorldMapCanvasRef path)
    {
        OriginalKey = key;
        OriginalType = type;
        OriginalSpot = spot;
        OriginalMapIds = mapIds == null ? null : new List<int>(mapIds);
        OriginalTitle = title;
        OriginalDescription = description;
        OriginalTownDescription = townDescription;
        OriginalNoToolTip = noToolTip;
        OriginalNoInfo = noInfo;
        OriginalLinkQuestId = linkQuestId;
        OriginalPartExtend = partExtend;
        OriginalHasType = hasType;
        OriginalHasSpot = hasSpot;
        OriginalHasMapNo = hasMapNo;
        OriginalHasTitle = hasTitle;
        OriginalHasDescription = hasDescription;
        OriginalHasTownDescription = hasTownDescription;
        OriginalHasNoToolTip = hasNoToolTip;
        OriginalHasNoInfo = hasNoInfo;
        OriginalHasPath = hasPath;
        OriginalHasLinkQuestId = hasLinkQuestId;
        OriginalHasPartExtend = hasPartExtend;
        OriginalPath = path?.DeepClone();
    }

    public WorldMapMapEntry DeepClone()
    {
        WorldMapMapEntry clone = new(Key)
        {
            Type = Type,
            Spot = Spot,
            Title = Title,
            Description = Description,
            TownDescription = TownDescription,
            NoToolTip = NoToolTip,
            NoInfo = NoInfo,
            Path = Path?.DeepClone(),
            LinkQuestId = LinkQuestId,
            PartExtend = PartExtend,
            IsNew = IsNew,
            OriginalKey = OriginalKey,
            OriginalType = OriginalType,
            OriginalSpot = OriginalSpot,
            OriginalMapIds = OriginalMapIds == null ? null : new List<int>(OriginalMapIds),
            OriginalTitle = OriginalTitle,
            OriginalDescription = OriginalDescription,
            OriginalTownDescription = OriginalTownDescription,
            OriginalNoToolTip = OriginalNoToolTip,
            OriginalNoInfo = OriginalNoInfo,
            OriginalLinkQuestId = OriginalLinkQuestId,
            OriginalPartExtend = OriginalPartExtend,
            OriginalHasType = OriginalHasType,
            OriginalHasSpot = OriginalHasSpot,
            OriginalHasMapNo = OriginalHasMapNo,
            OriginalHasTitle = OriginalHasTitle,
            OriginalHasDescription = OriginalHasDescription,
            OriginalHasTownDescription = OriginalHasTownDescription,
            OriginalHasNoToolTip = OriginalHasNoToolTip,
            OriginalHasNoInfo = OriginalHasNoInfo,
            OriginalHasPath = OriginalHasPath,
            OriginalHasLinkQuestId = OriginalHasLinkQuestId,
            OriginalHasPartExtend = OriginalHasPartExtend,
            OriginalPath = OriginalPath?.DeepClone()
        };
        clone._mapIds.AddRange(_mapIds);
        return clone;
    }
}

public sealed class WorldMapLink
{
    public WorldMapLink(string key = "0") { Key = key ?? string.Empty; }
    public string Key { get; set; }
    public string ToolTip { get; set; }
    public string Tooltip { get { return ToolTip; } set { ToolTip = value; } }
    public Point? Spot { get; set; }
    public string LinkMap { get; set; }
    public WorldMapCanvasRef LinkImage { get; set; }

    public bool IsNew { get; internal set; }
    public string OriginalKey { get; private set; }
    public string OriginalToolTip { get; private set; }
    public Point? OriginalSpot { get; private set; }
    public string OriginalLinkMap { get; private set; }
    public bool OriginalHasToolTip { get; private set; }
    public bool OriginalHasSpot { get; private set; }
    public bool OriginalHasLink { get; private set; }
    public bool OriginalHasLinkMap { get; private set; }
    public WorldMapCanvasRef OriginalLinkImage { get; private set; }

    public void CaptureOriginalState(
        string key,
        string toolTip,
        Point? spot,
        string linkMap,
        bool hasToolTip,
        bool hasSpot,
        bool hasLink,
        bool hasLinkMap,
        WorldMapCanvasRef linkImage)
    {
        OriginalKey = key;
        OriginalToolTip = toolTip;
        OriginalSpot = spot;
        OriginalLinkMap = linkMap;
        OriginalHasToolTip = hasToolTip;
        OriginalHasSpot = hasSpot;
        OriginalHasLink = hasLink;
        OriginalHasLinkMap = hasLinkMap;
        OriginalLinkImage = linkImage?.DeepClone();
    }

    public WorldMapLink DeepClone()
    {
        return new WorldMapLink(Key)
        {
            ToolTip = ToolTip,
            Spot = Spot,
            LinkMap = LinkMap,
            LinkImage = LinkImage?.DeepClone(),
            IsNew = IsNew,
            OriginalKey = OriginalKey,
            OriginalToolTip = OriginalToolTip,
            OriginalSpot = OriginalSpot,
            OriginalLinkMap = OriginalLinkMap,
            OriginalHasToolTip = OriginalHasToolTip,
            OriginalHasSpot = OriginalHasSpot,
            OriginalHasLink = OriginalHasLink,
            OriginalHasLinkMap = OriginalHasLinkMap,
            OriginalLinkImage = OriginalLinkImage?.DeepClone()
        };
    }
}

public sealed class WorldMapFogLayer
{
    public WorldMapFogLayer(string key = "0") { Key = key ?? string.Empty; }
    public string Key { get; set; }
    public WorldMapCanvasRef Image { get; set; }
    public WorldMapCanvasRef Canvas { get { return Image; } set { Image = value; } }
    public int? Quest { get; set; }
    public int? QuestId { get { return Quest; } set { Quest = value; } }
    public int? QState { get; set; }

    public bool IsNew { get; internal set; }
    public string OriginalKey { get; private set; }
    public int? OriginalQuest { get; private set; }
    public int? OriginalQState { get; private set; }
    public bool OriginalHasImage { get; private set; }
    public bool OriginalHasQuest { get; private set; }
    public bool OriginalHasQState { get; private set; }
    public WorldMapCanvasRef OriginalImage { get; private set; }

    public void CaptureOriginalState(
        string key,
        int? quest,
        int? qState,
        bool hasImage,
        bool hasQuest,
        bool hasQState,
        WorldMapCanvasRef image)
    {
        OriginalKey = key;
        OriginalQuest = quest;
        OriginalQState = qState;
        OriginalHasImage = hasImage;
        OriginalHasQuest = hasQuest;
        OriginalHasQState = hasQState;
        OriginalImage = image?.DeepClone();
    }

    public WorldMapFogLayer DeepClone()
    {
        return new WorldMapFogLayer(Key)
        {
            Image = Image?.DeepClone(),
            Quest = Quest,
            QState = QState,
            IsNew = IsNew,
            OriginalKey = OriginalKey,
            OriginalQuest = OriginalQuest,
            OriginalQState = OriginalQState,
            OriginalHasImage = OriginalHasImage,
            OriginalHasQuest = OriginalHasQuest,
            OriginalHasQState = OriginalHasQState,
            OriginalImage = OriginalImage?.DeepClone()
        };
    }
}

/// <summary>Reference to a WZ canvas and its placement metadata.</summary>
public sealed class WorldMapCanvasRef
{
    private WzCanvasProperty _rawCanvas;

    public WorldMapCanvasRef() { }

    public WorldMapCanvasRef(WzCanvasProperty canvas)
    {
        _rawCanvas = canvas;
        ReadMetadata(canvas);
    }

    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public Point Origin { get; set; }
    public bool HasOrigin { get; set; }
    public int Z { get; set; }
    public bool HasZ { get; set; }
    public string Inlink { get; set; }
    public string Outlink { get; set; }
    public WzCanvasProperty RawProperty { get { return _rawCanvas; } }

    /// <summary>Replaces the native canvas payload and refreshes its metadata.</summary>
    public void ReplaceCanvas(WzCanvasProperty canvas)
    {
        _rawCanvas = canvas;
        ReadMetadata(canvas);
    }

    public WorldMapCanvasRef DeepClone()
    {
        WorldMapCanvasRef clone = new()
        {
            _rawCanvas = _rawCanvas?.DeepClone() as WzCanvasProperty,
            Width = Width,
            Height = Height,
            Origin = Origin,
            HasOrigin = HasOrigin,
            Z = Z,
            HasZ = HasZ,
            Inlink = Inlink,
            Outlink = Outlink
        };
        return clone;
    }

    internal void ReadMetadata(WzCanvasProperty canvas)
    {
        if (canvas == null) return;
        Width = canvas.PngProperty?.Width ?? 0;
        Height = canvas.PngProperty?.Height ?? 0;
        WzVectorProperty origin = canvas[WzCanvasProperty.OriginPropertyName] as WzVectorProperty;
        if (origin != null)
        {
            Origin = origin.Pos;
            HasOrigin = true;
        }
        WzIntProperty z = canvas["z"] as WzIntProperty;
        if (z != null)
        {
            Z = z.Value;
            HasZ = true;
        }
        Inlink = (canvas[WzCanvasProperty.InlinkPropertyName] as WzStringProperty)?.Value;
        Outlink = (canvas[WzCanvasProperty.OutlinkPropertyName] as WzStringProperty)?.Value;
    }
}
