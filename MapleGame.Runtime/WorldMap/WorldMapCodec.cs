using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.WorldMap;

/// <summary>Lossless reader for the common WorldMap IMG shape.</summary>
public static class WorldMapCodec
{
    public static WorldMapDocument Read(WzImage image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        WzImage raw = image.DeepClone();
        string imageName = raw.Name ?? string.Empty;
        WzSubProperty info = raw["info"] as WzSubProperty;
        string logical = GetString(info, "WorldMap");
        string fallback = StripImgExtension(imageName);
        WorldMapSurface surface = new(logical ?? fallback)
        {
            ParentName = GetString(info, "parentMap"),
            MemoJp = GetString(raw, "Memo_JP")
        };
        if (raw["BaseImg"] is WzSubProperty baseImg)
            surface.BaseImage = ReadCanvas(GetProperty(baseImg, "0"));
        surface.CaptureOriginalState(
            logical,
            info?["WorldMap"] != null,
            GetString(info, "parentMap"),
            GetString(raw, "Memo_JP"),
            raw["Memo_JP"] != null,
            info != null,
            raw["BaseImg"] != null,
            surface.BaseImage,
            raw["MapList"] != null,
            raw["MapLink"] != null,
            raw["Fog"] != null);

        if (raw["MapList"] is WzSubProperty mapList)
        {
            foreach (WzImageProperty child in mapList.WzProperties)
            {
                if (child == null) continue;
                surface.Entries.Add(ReadEntry(child));
            }
        }

        if (raw["MapLink"] is WzSubProperty links)
        {
            foreach (WzImageProperty child in links.WzProperties)
            {
                if (child == null) continue;
                surface.Links.Add(ReadLink(child));
            }
        }

        if (raw["Fog"] is WzSubProperty fog)
        {
            foreach (WzImageProperty child in fog.WzProperties)
            {
                if (child == null) continue;
                surface.FogLayers.Add(ReadFog(child));
            }
        }

        WorldMapDocument document = new(imageName, surface)
        {
            RawImage = raw,
            IsNew = false,
            IsDirty = false
        };
        surface.RawImage = raw;
        surface.AttachDocument(document);
        return document;
    }

    public static WorldMapDocument Decode(WzImage image)
    {
        return Read(image);
    }

    /// <summary>Compatibility overload used by source repositories that already normalized an image name.</summary>
    public static WorldMapDocument Read(WzImage image, string imageName)
    {
        WorldMapDocument document = Read(image);
        if (!string.IsNullOrWhiteSpace(imageName))
            document.ImageName = imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? imageName : imageName + ".img";
        return document;
    }
    public static bool IsExclusionImage(string imageName)
    {
        string normalized = (imageName ?? string.Empty).Replace('\\', '/');
        string leaf = normalized[(normalized.LastIndexOf('/') + 1)..];
        if (!leaf.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) leaf += ".img";
        return leaf.Equals("SearchExcept.img", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("SearchExceptForNPC.img", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<int> ReadExclusions(WzImage image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        var result = new List<int>();
        foreach (WzImageProperty property in image.WzProperties ?? Enumerable.Empty<WzImageProperty>())
        {
            int? value = GetInt(property);
            if (value.HasValue) result.Add(value.Value);
        }
        return result;
    }
    private static WorldMapMapEntry ReadEntry(WzImageProperty property)
    {
        WzSubProperty node = property as WzSubProperty;
        WorldMapMapEntry entry = new(property.Name);
        entry.Type = GetInt(node, "type") ?? 0;
        entry.Spot = GetPoint(node, "spot") ?? Point.Empty;
        entry.Title = GetString(node, "title");
        entry.Description = GetString(node, "desc");
        entry.TownDescription = GetString(node, "townDesc");
        entry.NoToolTip = GetInt(node, "noToolTip");
        entry.NoInfo = GetInt(node, "noInfo");
        entry.LinkQuestId = GetInt(node, "linkQuestID");
        entry.PartExtend = GetInt(node, "partExtend");
        entry.Path = ReadCanvas(GetProperty(node, "path"));
        List<int> originalMapIds = ReadMapIds(node, out _);
        entry.CaptureOriginalState(
            property.Name,
            entry.Type,
            entry.Spot,
            originalMapIds,
            entry.Title,
            entry.Description,
            entry.TownDescription,
            entry.NoToolTip,
            entry.NoInfo,
            entry.LinkQuestId,
            entry.PartExtend,
            node?["type"] != null,
            node?["spot"] != null,
            node?["mapNo"] != null,
            node?["title"] != null,
            node?["desc"] != null,
            node?["townDesc"] != null,
            node?["noToolTip"] != null,
            node?["noInfo"] != null,
            node?["path"] != null,
            node?["linkQuestID"] != null,
            node?["partExtend"] != null,
            entry.Path);
        entry.MapIds.Clear();
        foreach (int mapId in originalMapIds) entry.MapIds.Add(mapId);
        return entry;
    }

    private static WorldMapLink ReadLink(WzImageProperty property)
    {
        WzSubProperty node = property as WzSubProperty;
        WzSubProperty nested = node?["link"] as WzSubProperty;
        WorldMapLink link = new(property.Name)
        {
            ToolTip = GetString(node, "toolTip"),
            Spot = GetPoint(node, "spot"),
            LinkMap = GetString(nested, "linkMap"),
            LinkImage = ReadCanvas(GetProperty(nested, "linkImg"))
        };
        link.CaptureOriginalState(
            property.Name,
            link.ToolTip,
            link.Spot,
            link.LinkMap,
            node?["toolTip"] != null,
            node?["spot"] != null,
            nested != null,
            nested?["linkMap"] != null,
            link.LinkImage);
        return link;
    }

    private static WorldMapFogLayer ReadFog(WzImageProperty property)
    {
        WzSubProperty node = property as WzSubProperty;
        WorldMapFogLayer layer = new(property.Name)
        {
            Image = ReadCanvas(GetProperty(node, "0")),
            Quest = GetInt(node, "quest"),
            QState = GetInt(node, "qState")
        };
        layer.CaptureOriginalState(
            property.Name,
            layer.Quest,
            layer.QState,
            node?["0"] != null,
            node?["quest"] != null,
            node?["qState"] != null,
            layer.Image);
        return layer;
    }

    private static WorldMapCanvasRef ReadCanvas(WzImageProperty property)
    {
        if (property is not WzCanvasProperty canvas) return null;
        return new WorldMapCanvasRef(canvas);
    }

    private static List<int> ReadMapIds(WzSubProperty node, out bool direct)
    {
        direct = false;
        List<int> ids = new();
        WzImageProperty mapNo = node?["mapNo"];
        if (mapNo == null) return ids;
        if (mapNo is not WzSubProperty sub)
        {
            int? value = GetInt(mapNo);
            if (value.HasValue) ids.Add(value.Value);
            direct = true;
            return ids;
        }
        foreach (WzImageProperty child in sub.WzProperties)
        {
            if (int.TryParse(child.Name, out _) && GetInt(child).HasValue)
                ids.Add(GetInt(child).Value);
        }
        return ids;
    }
    private static bool StringEquals(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
    private static string StripImgExtension(string name) { return name != null && name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name ?? string.Empty; }

    private static WzImageProperty GetProperty(WzImage image, string name) { return image?[name]; }
    private static WzImageProperty GetProperty(WzSubProperty property, string name) { return property?[name]; }
    private static string GetString(WzImage image, string name) { return (image?[name] as WzStringProperty)?.Value; }
    private static string GetString(WzSubProperty property, string name) { return (property?[name] as WzStringProperty)?.Value; }
    private static int? GetInt(WzImageProperty property)
    {
        if (property is WzIntProperty i) return i.Value;
        if (property is WzShortProperty s) return s.Value;
        if (property is WzLongProperty l) return checked((int)l.Value);
        return property?.WzValue is int value ? value : null;
    }
    private static int? GetInt(WzSubProperty property, string name) { return GetInt(property?[name]); }
    private static Point? GetPoint(WzSubProperty property, string name)
    {
        return (property?[name] as WzVectorProperty)?.Pos;
    }
}
