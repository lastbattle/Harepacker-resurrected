using HaCreator.MapSimulator.Contracts;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HaCreator.MapSimulator.Assets;

public sealed class WzRuntimeMapReaderOptions
{
    public int MapId { get; init; }
    public string MapName { get; init; } = "<Untitled>";
    public string StreetName { get; init; } = "<Untitled>";
    public string CategoryName { get; init; } = "<Untitled>";
    public WzImage TooltipStrings { get; init; }
    public IRuntimeAssetSource AssetSource { get; init; }
    /// <summary>Optional runtime metadata catalog used to resolve life and reactor display names.</summary>
    public IRuntimeAssetCatalog Catalog { get; init; }
}

/// <summary>Reads a map IMG directly into the editor-free runtime contract.</summary>
public sealed class WzRuntimeMapReader
{
    private static readonly HashSet<string> HandledRoots = new(StringComparer.Ordinal)
    {
        "info", "directionInfo", "0", "1", "2", "3", "4", "5", "6", "7", "8",
        "life", "ladderRope", "reactor", "back", "foothold", "miniMap", "portal", "seat",
        "ToolTip", "clock", "shipObj", "area", "healer", "pulley", "BuffZone", "swimArea",
        "MirrorFieldData"
    };

    public RuntimeMapDefinition Read(WzImage mapImage, WzRuntimeMapReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(mapImage);
        ArgumentNullException.ThrowIfNull(options);
        using WzImage image = mapImage.DeepClone();
        if (!image.Parsed)
            image.ParseImage();
        if (image["info"] == null)
            throw new InvalidDataException($"Map image '{image.Name}' has no info node.");

        MapInfo mapInfo = new(image, options.MapName, options.StreetName, options.CategoryName)
        {
            id = options.MapId,
            mapType = GetMapType(image.Name)
        };
        try
        {
        foreach (WzImageProperty property in image.WzProperties)
            if (!HandledRoots.Contains(property.Name))
                mapInfo.additionalNonInfoProps.Add(property);

        Dimensions dimensions = ReadDimensions(image);
        var tiles = new List<RuntimeTileDefinition>();
        var objects = new List<RuntimeObjectDefinition>();
        ReadLayers(image, options.AssetSource, tiles, objects);

        return new RuntimeMapDefinition(
            mapInfo,
            dimensions.MapSize,
            dimensions.Center,
            dimensions.HasExplicitVr ? dimensions.VirtualBounds : null,
            dimensions.MinimapArea,
            dimensions.MinimapPosition,
            ReadMinimapPng(image),
            tiles,
            objects,
            ReadBackgrounds(image, options.AssetSource),
            ReadLife(image, "m", options.AssetSource, options.Catalog),
            ReadLife(image, "n", options.AssetSource, options.Catalog),
            ReadReactors(image, options.AssetSource, options.Catalog),
            ReadPortals(image),
            ReadFootholds(image),
            ReadRopes(image),
            ReadChairs(image),
            ReadTooltips(image, options.TooltipStrings, options.MapId),
            ReadMisc(image),
            ReadMirrorFields(image),
            Array.Empty<RuntimeOwnedAssetOverride>());
        }
        finally
        {
            RuntimeMapInfoCloner.DisposeParsedTypedMetadata(mapInfo);
        }
    }

    private static void ReadLayers(WzImage image, IRuntimeAssetSource source,
        List<RuntimeTileDefinition> tiles, List<RuntimeObjectDefinition> objects)
    {
        int order = 0;
        for (int layer = 0; layer <= 8; layer++)
        {
            WzImageProperty layerNode = image[layer.ToString(CultureInfo.InvariantCulture)];
            if (layerNode == null)
                continue;
            string tileSet = Text(layerNode["info"]?["tS"]);
            foreach (WzImageProperty item in Children(layerNode["obj"]))
            {
                string objectSet = Text(item["oS"]);
                string l0 = Text(item["l0"]);
                string l1 = Text(item["l1"]);
                string l2 = Text(item["l2"]);
                RuntimeAssetKey key = new("Map/Obj", $"{objectSet}.img/{l0}/{l1}/{l2}");
                WzImage objectImage = source?.FindImage("Map", $"Obj/{objectSet}.img");
                WzCanvasProperty preview = FindPreview(objectImage?[l0]?[l1]?[l2]);
                objects.Add(new RuntimeObjectDefinition
                {
                    Asset = key,
                    X = NormalizeX(Int(item["x"]), Bool(item["f"]), preview), Y = Int(item["y"]), Z = Int(item["z"]), Layer = layer,
                    Platform = Int(item["zM"]), DrawOrder = order++, ObjectSet = objectSet,
                    L0 = l0, L1 = l1, L2 = l2, Flip = Bool(item["f"]),
                    Rotation = OptionalBool(item["r"]), Hide = OptionalBool(item["hide"]),
                    Reactor = OptionalBool(item["reactor"]), Dynamic = OptionalBool(item["dynamic"]),
                    Flow = OptionalBool(item["flow"]), Rx = OptionalInt(item["rx"]),
                    Ry = OptionalInt(item["ry"]), Cx = OptionalInt(item["cx"]), Cy = OptionalInt(item["cy"]),
                    Origin = PreviewOrigin(preview),
                    Name = OptionalText(item["name"]), Tags = OptionalText(item["tags"]),
                    Quests = Children(item["quest"]).Select(q =>
                        new RuntimeObjectQuestDefinition(ParseName(q), Int(q))).ToArray()
                });
            }
            foreach (WzImageProperty item in Children(layerNode["tile"]))
            {
                string variant = Text(item["u"]);
                string number = Int(item["no"]).ToString(CultureInfo.InvariantCulture);
                WzImage assetImage = source?.FindImage("Map", $"Tile/{tileSet}.img");
                WzImageProperty asset = assetImage?[variant]?[number];
                RuntimeAssetKey key = new("Map/Tile", $"{tileSet}.img/{variant}/{number}");
                tiles.Add(new RuntimeTileDefinition(key, Int(item["x"]), Int(item["y"]), ParseName(item),
                    layer, Int(item["zM"]), order++, tileSet, variant, number,
                    Int(assetImage?["info"]?["mag"], 1), Int(asset?["z"])));
            }
        }
    }

    private static IEnumerable<RuntimeLifeDefinition> ReadLife(
        WzImage image,
        string requestedType,
        IRuntimeAssetSource source,
        IRuntimeAssetCatalog catalog)
    {
        foreach (WzImageProperty item in Children(image["life"]))
        {
            if (item.Name == "isCategory" || Text(item["type"]) != requestedType)
                continue;
            string id = Text(item["id"]);
            int x = Int(item["x"]), y = Int(item["y"]), cy = Int(item["cy"]);
            string category = requestedType == "m" ? "Mob" : "Npc";
            WzImage assetImage = FindLinkedTemplate(source, category, id);
            WzCanvasProperty preview = FindPreview(assetImage?["stand"]) ??
                (requestedType == "m" ? FindPreview(assetImage?["fly"]) : null);
            x = NormalizeX(x, Bool(item["f"]), preview);
            string displayName = requestedType == "m"
                ? ResolveMobName(catalog, id)
                : ResolveNpcName(catalog, id);
            yield return new RuntimeLifeDefinition
            {
                Asset = new RuntimeAssetKey(category, PadId(id) + ".img"),
                Id = id, DisplayName = displayName, X = x, Y = cy, Z = -1,
                Flip = Bool(item["f"]), LimitedName = OptionalText(item["limitedname"]),
                Hide = OptionalBool(item["hide"]), Rx0Shift = x - Int(item["rx0"]),
                Rx1Shift = Int(item["rx1"]) - x, YShift = cy - y,
                MobTime = OptionalInt(item["mobTime"]), Info = OptionalInt(item["info"]),
                Team = OptionalInt(item["team"])
            };
        }
    }

    private static IEnumerable<RuntimeReactorDefinition> ReadReactors(
        WzImage image,
        IRuntimeAssetSource source,
        IRuntimeAssetCatalog catalog)
    {
        foreach (WzImageProperty item in Children(image["reactor"]))
        {
            string id = Text(item["id"]);
            WzCanvasProperty preview = FindPreview(FindLinkedTemplate(source, "Reactor", id)?["0"]);
            string authoredName = OptionalText(item["name"]);
            string displayName = ResolveReactorName(catalog, id) ?? authoredName ?? id;
            yield return new RuntimeReactorDefinition(new RuntimeAssetKey("Reactor", PadId(id) + ".img"),
                id, displayName, NormalizeX(Int(item["x"]), Bool(item["f"]), preview), Int(item["y"]), -1, Bool(item["f"]),
                Int(item["reactorTime"]), authoredName);
        }
    }

    private static string ResolveMobName(IRuntimeAssetCatalog catalog, string id)
    {
        return catalog?.TryGetMobName(id, out RuntimeMobName value) == true
            && !string.IsNullOrWhiteSpace(value?.Name)
            ? value.Name
            : id;
    }

    private static string ResolveNpcName(IRuntimeAssetCatalog catalog, string id)
    {
        return catalog?.TryGetNpcName(id, out RuntimeNpcName value) == true
            && !string.IsNullOrWhiteSpace(value?.Name)
            ? value.Name
            : id;
    }

    private static string ResolveReactorName(IRuntimeAssetCatalog catalog, string id)
    {
        return catalog?.TryGetReactor(id, out RuntimeReactorAsset value) == true
            && !string.IsNullOrWhiteSpace(value?.Name)
            ? value.Name
            : null;
    }

    private static IEnumerable<RuntimePortalDefinition> ReadPortals(WzImage image)
    {
        foreach (WzImageProperty item in Children(image["portal"]))
        {
            int code = Int(item["pt"]);
            yield return new RuntimePortalDefinition
            {
                Asset = new RuntimeAssetKey("Map", $"MapHelper.img/portal/game/{code}"),
                X = Int(item["x"]), Y = Int(item["y"]), Z = -1,
                Image = OptionalText(item["image"]), Name = Text(item["pn"]),
                Type = FromSerializedPortalType(code),
                TargetName = Text(item["tn"]), TargetMapId = Int(item["tm"]),
                Script = OptionalText(item["script"]), Delay = OptionalInt(item["delay"]),
                HideTooltip = OptionalBool(item["hideTooltip"]), OnlyOnce = OptionalBool(item["onlyOnce"]),
                HorizontalImpact = OptionalInt(item["horizontalImpact"]),
                VerticalImpact = OptionalInt(item["verticalImpact"]),
                HorizontalRange = OptionalInt(item["hRange"]), VerticalRange = OptionalInt(item["vRange"]),
                ReactorName = OptionalText(item["reactorName"]),
                SessionValueKey = OptionalText(item["sessionValueKey"]),
                SessionValue = OptionalText(item["sessionValue"])
            };
        }
    }

    private static IEnumerable<RuntimeFootholdDefinition> ReadFootholds(WzImage image)
    {
        foreach (WzImageProperty layer in Children(image["foothold"]))
        foreach (WzImageProperty platform in Children(layer))
        foreach (WzImageProperty item in Children(platform))
        {
            int x1 = Int(item["x1"]), y1 = Int(item["y1"]), x2 = Int(item["x2"]), y2 = Int(item["y2"]);
            if (x1 == x2 && y1 == y2)
                continue;
            yield return new RuntimeFootholdDefinition(ParseName(item), Int(item["prev"]), Int(item["next"]),
                x1, y1, x2, y2, ParseName(layer), ParseName(platform), OptionalInt(item["force"]),
                OptionalInt(item["piece"]), OptionalBool(item["forbidFallDown"]), OptionalBool(item["cantThrough"]));
        }
    }

    private static IEnumerable<RuntimeRopeDefinition> ReadRopes(WzImage image) =>
        Children(image["ladderRope"]).Select(item => new RuntimeRopeDefinition(Int(item["x"]),
            Int(item["y1"]), Int(item["y2"]), Int(item["page"]), Bool(item["l"]), false, Bool(item["uf"])));

    private static IEnumerable<RuntimeChairDefinition> ReadChairs(WzImage image) =>
        Children(image["seat"]).OfType<WzVectorProperty>()
            .Select(item => new RuntimeChairDefinition(item.X.Value, item.Y.Value));

    private static IEnumerable<RuntimeBackgroundDefinition> ReadBackgrounds(WzImage image, IRuntimeAssetSource source)
    {
        int order = 0;
        foreach (WzImageProperty item in Children(image["back"]))
        {
            string set = Text(item["bS"]);
            string number = Int(item["no"]).ToString(CultureInfo.InvariantCulture);
            bool animated = Bool(item["ani"]);
            string spine = OptionalText(item["spineAni"]);
            string kind = spine != null ? "spine" : animated ? "ani" : "back";
            WzCanvasProperty preview = FindPreview(source?.FindImage("Map", $"Back/{set}.img")?[kind]?[number]);
            yield return new RuntimeBackgroundDefinition(new RuntimeAssetKey("Map/Back", $"{set}.img/{kind}/{number}"),
                NormalizeX(Int(item["x"]), Bool(item["f"]), preview), Int(item["y"]), order + 1, order++, set, number, Int(item["type"]),
                Int(item["rx"]), Int(item["ry"]), Int(item["cx"]), Int(item["cy"]), Int(item["a"]),
                Bool(item["front"]), Bool(item["f"]), Int(item["page"]), Int(item["screenMode"]),
                spine, Bool(item["spineRandomStart"]));
        }
    }

    private static WzImage FindLinkedTemplate(IRuntimeAssetSource source, string category, string id)
    {
        WzImage image = source?.FindImage(category, PadId(id) + ".img");
        string link = OptionalText(image?["info"]?["link"]);
        return link == null ? image : source?.FindImage(category, PadId(link) + ".img") ?? image;
    }

    private static WzCanvasProperty FindPreview(WzImageProperty property)
    {
        property = WzInfoTools.GetRealProperty(property);
        return property as WzCanvasProperty ?? WzInfoTools.GetRealProperty(property?["0"]) as WzCanvasProperty;
    }

    private static System.Drawing.Point PreviewOrigin(WzCanvasProperty preview)
    {
        System.Drawing.PointF origin = preview?.GetCanvasOriginPosition() ?? System.Drawing.PointF.Empty;
        return new System.Drawing.Point((int)origin.X, (int)origin.Y);
    }

    private static int NormalizeX(int serializedX, bool flip, WzCanvasProperty preview)
    {
        // Editor instances and runtime drawables use the left-facing frame anchor.
        // WZ stores the unflipped anchor. Missing preview data cannot provide its width;
        // retain the serialized coordinate until a complete source is available.
        if (!flip || preview == null) return serializedX;
        WzImageProperty linked = preview.GetLinkedWzImageProperty();
        int width = linked is WzCanvasProperty canvas ? canvas.PngProperty?.Width ?? 0 :
            linked is WzPngProperty png ? png.Width : preview.PngProperty?.Width ?? 0;
        return width <= 0 ? serializedX : serializedX - width + 2 * PreviewOrigin(preview).X;
    }

    private static IEnumerable<RuntimeTooltipDefinition> ReadTooltips(WzImage image, WzImage strings, int mapId)
    {
        WzImageProperty geometry = image["ToolTip"];
        WzImageProperty textRoot = strings?["Mapobject"]?[mapId.ToString(CultureInfo.InvariantCulture)];
        foreach (WzImageProperty item in Children(geometry).Where(p => !p.Name.EndsWith("char", StringComparison.Ordinal)))
        {
            if (!int.TryParse(item.Name, out int number))
                continue;
            WzImageProperty text = textRoot?[item.Name];
            yield return new RuntimeTooltipDefinition(Rect(item), OptionalText(text?["Title"]),
                OptionalText(text?["Desc"]), number, geometry[item.Name + "char"] is { } character ? Rect(character) : null);
        }
    }

    private static IEnumerable<RuntimeMiscDefinition> ReadMisc(WzImage image)
    {
        if (image["clock"] is { } clock)
            yield return new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Clock,
                Bounds = new Rectangle(Int(clock["x"]), Int(clock["y"]), Int(clock["width"]), Int(clock["height"])) };
        foreach (WzImageProperty item in Children(image["area"]))
            yield return RectMisc(RuntimeMiscKind.Area, item, item.Name);
        foreach (WzImageProperty item in Children(image["swimArea"]))
            yield return RectMisc(RuntimeMiscKind.SwimArea, item, item.Name);
        foreach (WzImageProperty item in Children(image["BuffZone"]))
            yield return RectMisc(RuntimeMiscKind.BuffZone, item, item.Name) with
                { ItemId = Int(item["ItemID"]), Interval = Int(item["Interval"]), Duration = Int(item["Duration"]) };
        if (image["shipObj"] is { } ship)
            yield return new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Ship,
                Bounds = new Rectangle(Int(ship["x"]), Int(ship["y"]), 0, 0), Asset = ObjectPathKey(Text(ship["shipObj"])),
                X0 = OptionalInt(ship["x0"]), ZValue = OptionalInt(ship["z"]), TimeMove = Int(ship["tMove"]),
                ShipKind = Int(ship["shipKind"]), Flip = Bool(ship["f"]) };
        if (image["healer"] is { } healer)
            yield return new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Healer,
                Bounds = new Rectangle(Int(healer["x"]), Int(healer["yMin"]), 0, Int(healer["yMax"]) - Int(healer["yMin"])),
                Asset = ObjectPathKey(Text(healer["healer"])), YMin = Int(healer["yMin"]), YMax = Int(healer["yMax"]),
                HealMin = Int(healer["healMin"]), HealMax = Int(healer["healMax"]), Fall = Int(healer["fall"]), Rise = Int(healer["rise"]) };
        if (image["pulley"] is { } pulley)
            yield return new RuntimeMiscDefinition { Kind = RuntimeMiscKind.Pulley,
                Bounds = new Rectangle(Int(pulley["x"]), Int(pulley["y"]), 0, 0),
                Asset = ObjectPathKey(Text(pulley["pulley"])) };
    }

    private static IEnumerable<RuntimeMirrorFieldDefinition> ReadMirrorFields(WzImage image)
    {
        var types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["mob"] = (int)RuntimeMirrorFieldType.Mob,
            ["user"] = (int)RuntimeMirrorFieldType.User,
            ["npc"] = (int)RuntimeMirrorFieldType.Npc
        };
        foreach (WzImageProperty holder in Children(image["MirrorFieldData"]))
        foreach (WzImageProperty group in Children(holder))
        {
            if (!types.TryGetValue(group.Name, out int type))
                continue;
            foreach (WzImageProperty item in Children(group))
            {
                WzVectorProperty offset = item["offset"] as WzVectorProperty;
                int ox = offset?.X.Value ?? 0, oy = offset?.Y.Value ?? 0;
                Rectangle bounds = Rect(item);
                bounds.Offset(-ox, -oy);
                yield return new RuntimeMirrorFieldDefinition(bounds, type, new Vector2(ox, oy),
                    new RuntimeReflectionDefinition(Int(item["gradient"]), Int(item["alpha"]),
                        OptionalText(item["objectForOverlay"]), Bool(item["reflection"]), Bool(item["alphaTest"])));
            }
        }
    }

    private static Dimensions ReadDimensions(WzImage image)
    {
        System.Drawing.Rectangle? explicitVr = MapInfo.GetVR(image);
        WzImageProperty minimap = image["miniMap"];
        bool hasMinimap = minimap != null;
        System.Drawing.Rectangle vr = explicitVr ?? ComputeFootholdVr(image)
            ?? throw new InvalidDataException($"Map image '{image.Name}' has neither minimap, VR, nor foothold bounds.");
        if (!hasMinimap)
        {
            Point size = new(vr.Width + 10, vr.Height + 10);
            Point center = new(5 - vr.Left, 5 - vr.Top);
            return new Dimensions(size, center, new Rectangle(vr.X, vr.Y, vr.Width, vr.Height),
                explicitVr.HasValue, Rectangle.Empty, System.Drawing.Point.Empty);
        }
        int width = Int(minimap["width"]), height = Int(minimap["height"]);
        int cx = Int(minimap["centerX"]), cy = Int(minimap["centerY"]);
        int leftTarget = 69 - cx, topTarget = 86 - cy, rightTarget = width - 138, bottomTarget = height - 172;
        int left = vr.Left < leftTarget ? leftTarget - vr.Left : 0;
        int top = vr.Top < topTarget ? topTarget - vr.Top : 0;
        int right = vr.Right > rightTarget ? vr.Right - rightTarget : 0;
        int bottom = vr.Bottom > bottomTarget ? vr.Bottom - bottomTarget : 0;
        return new Dimensions(new Point(width + left + right, height + top + bottom), new Point(cx + left, cy + top),
            new Rectangle(vr.X, vr.Y, vr.Width, vr.Height), explicitVr.HasValue,
            new Rectangle(-cx, -cy, width, height), new System.Drawing.Point(-cx, -cy));
    }

    private static System.Drawing.Rectangle? ComputeFootholdVr(WzImage image)
    {
        List<RuntimeFootholdDefinition> footholds = ReadFootholds(image).ToList();
        if (footholds.Count == 0)
            return null;
        int left = footholds.Min(f => Math.Min(f.X1, f.X2));
        int right = footholds.Max(f => Math.Max(f.X1, f.X2));
        int top = footholds.Min(f => Math.Min(f.Y1, f.Y2));
        int bottom = footholds.Max(f => Math.Max(f.Y1, f.Y2));
        int vrTop = Math.Min(bottom - 600, top - 360);
        return new System.Drawing.Rectangle(left - 10, vrTop, right - left + 20, bottom + 110 - vrTop);
    }

    private static byte[] ReadMinimapPng(WzImage image)
    {
        if (image["miniMap"]?["canvas"] is not WzCanvasProperty canvas)
            return Array.Empty<byte>();
        using System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static RuntimeMiscDefinition RectMisc(RuntimeMiscKind kind, WzImageProperty item, string id) =>
        new() { Kind = kind, Bounds = Rect(item), Identifier = id };
    private static RuntimeAssetKey ObjectPathKey(string path) => new("Map/Obj",
        path?.Replace('\\', '/').Replace("Map/Obj/", "", StringComparison.OrdinalIgnoreCase) ?? string.Empty);
    private static Rectangle Rect(WzImageProperty item)
    {
        // Mirror fields use vector corners; area/tooltip nodes use numeric edges.
        if (item?["lt"] is WzVectorProperty lt && item?["rb"] is WzVectorProperty rb)
            return new Rectangle(Math.Min(lt.X.Value, rb.X.Value), Math.Min(lt.Y.Value, rb.Y.Value),
                Math.Abs(rb.X.Value - lt.X.Value), Math.Abs(rb.Y.Value - lt.Y.Value));
        int x1 = Int(item?["x1"]), x2 = Int(item?["x2"]), y1 = Int(item?["y1"]), y2 = Int(item?["y2"]);
        return new Rectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }
    private static IEnumerable<WzImageProperty> Children(WzImageProperty property) =>
        property == null ? Array.Empty<WzImageProperty>() : property.WzProperties;
    private static int ParseName(WzImageProperty property) => int.TryParse(property?.Name, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out int value) ? value : 0;
    private static int Int(WzImageProperty property, int fallback = 0) => property == null ? fallback : InfoTool.GetInt(property, fallback);
    private static int? OptionalInt(WzImageProperty property) => property == null ? null : InfoTool.GetOptionalInt(property);
    private static string Text(WzImageProperty property) => property == null ? null : InfoTool.GetString(property);
    private static string OptionalText(WzImageProperty property) => property == null ? null : InfoTool.GetOptionalString(property);
    private static bool Bool(WzImageProperty property) => property != null && InfoTool.GetBool(property);
    private static MapleBool OptionalBool(WzImageProperty property) => property == null ? null : InfoTool.GetOptionalBool(property);
    private static string PadId(string id) => int.TryParse(id, out int value) ? value.ToString("D7", CultureInfo.InvariantCulture) : id;
    private static PortalType FromSerializedPortalType(int value)
    {
        // Default is an editor/runtime alias for Visible and has no numeric WZ pt value.
        int enumValue = value >= 3 ? value + 1 : value;
        if (!Enum.IsDefined(typeof(PortalType), enumValue))
            throw new InvalidDataException($"Unsupported portal type value '{value}'.");
        return (PortalType)enumValue;
    }
    private static MapType GetMapType(string name) => name switch
    {
        "MapLogin.img" or "MapLogin1.img" or "MapLogin2.img" or "MapLogin3.img" => MapType.MapLogin,
        "CashShopPreview.img" => MapType.CashShopPreview,
        "ITCPreview.img" => MapType.ITCPreview,
        _ => MapType.RegularMap
    };

    private readonly record struct Dimensions(Point MapSize, Point Center, Rectangle VirtualBounds,
        bool HasExplicitVr, Rectangle MinimapArea, System.Drawing.Point MinimapPosition);
}
