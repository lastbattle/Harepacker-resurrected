using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.WorldMap;

namespace HaCreator.WorldMap;

/// <summary>Write options for the native WorldMap IMG codec.</summary>
public sealed class WorldMapCodecOptions
{
    public bool PreserveUnknownProperties { get; set; } = true;
    public bool PreserveChildOrdering { get; set; } = true;
    public bool CanonicalizeNewDocuments { get; set; } = true;
}

/// <summary>
/// Lossless writer for the common WorldMap IMG shape. Existing images
/// are cloned before editing; only fields represented by the editable model
/// are patched, leaving all other properties and canvas payloads untouched.
/// </summary>
public static class WorldMapWriter
{
    public static WzImage Write(WorldMapDocument document, WorldMapCodecOptions options = null)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        options ??= new WorldMapCodecOptions();
        WzImage result = document.RawImage?.DeepClone() ?? new WzImage(document.ImageName);
        result.Name = document.ImageName;
        PatchDocument(result, document, options);
        result.Changed = true;
        return result;
    }

    public static WzImage Encode(WorldMapDocument document, WorldMapCodecOptions options = null)
    {
        return Write(document, options);
    }

    /// <summary>Applies edits to a cloned native image, retaining the source image as an untouched baseline.</summary>
    public static WzImage ApplyToClone(WorldMapDocument document, WorldMapCodecOptions options = null)
    {
        return Write(document, options);
    }

    private static void PatchDocument(WzImage image, WorldMapDocument document, WorldMapCodecOptions options)
    {
        WorldMapSurface surface = document.Surface;
        bool isNew = document.IsNew || surface.IsNew || document.RawImage == null;

        WzSubProperty info = image["info"] as WzSubProperty;
        bool logicalChanged = isNew || surface.OriginalHasLogicalName
            ? !StringEquals(surface.LogicalName, surface.OriginalLogicalName)
            : !StringEquals(surface.LogicalName, StripImgExtension(document.ImageName));
        bool parentChanged = isNew || surface.OriginalHasInfo
            ? !StringEquals(surface.ParentName, surface.OriginalParentName)
            : surface.ParentName != null;
        bool memoChanged = isNew || surface.OriginalHasMemoJp
            ? !StringEquals(surface.MemoJp, surface.OriginalMemoJp)
            : surface.MemoJp != null;

        if (info == null && (isNew || logicalChanged || parentChanged))
            info = EnsureSubProperty(image, "info");
        if (info != null)
        {
            PatchString(info, "WorldMap", surface.LogicalName, surface.OriginalLogicalName,
                surface.OriginalHasLogicalName, logicalChanged || isNew);
            PatchString(info, "parentMap", surface.ParentName, surface.OriginalParentName,
                surface.OriginalParentName != null, parentChanged || isNew);
            if (!HasProperties(info)) image.RemoveProperty(info);
        }
        PatchString(image, "Memo_JP", surface.MemoJp, surface.OriginalMemoJp,
            surface.OriginalHasMemoJp, memoChanged || isNew);

        if (surface.BaseImage != null)
        {
            WzSubProperty baseImg = image["BaseImg"] as WzSubProperty;
            bool changed = isNew || !CanvasEquivalent(surface.BaseImage, surface.OriginalBaseImage);
            if (baseImg == null && changed)
                baseImg = EnsureSubProperty(image, "BaseImg");
            if (baseImg != null)
            {
                WorldMapCanvasRef original = surface.OriginalBaseImage;
                PatchCanvas(baseImg, "0", surface.BaseImage, original, true);
            }
        }
        else if (surface.OriginalHasBaseImage && image["BaseImg"] != null && isNew)
        {
            image.RemoveProperty("BaseImg");
        }

        PatchEntries(image, surface, isNew, options);
        PatchLinks(image, surface, isNew, options);
        PatchFog(image, surface, isNew, options);
    }

    private static void PatchEntries(WzImage image, WorldMapSurface surface, bool force, WorldMapCodecOptions options)
    {
        WzSubProperty mapList = image["MapList"] as WzSubProperty;
        bool needsList = force ? surface.Entries.Count > 0 : surface.Entries.Any(IsEntryChanged);
        if (mapList == null && needsList)
            mapList = EnsureSubProperty(image, "MapList");
        if (mapList == null) return;

        HashSet<WzImageProperty> retained = new();
        foreach (WorldMapMapEntry entry in surface.Entries)
        {
            WzImageProperty child = FindOriginalChild(mapList, entry.OriginalKey, entry.Key, entry.IsNew);
            WzSubProperty node;
            if (child is WzSubProperty sub)
            {
                node = sub;
                retained.Add(child);
            }
            else
            {
                node = new WzSubProperty(entry.Key);
                mapList.AddProperty(node);
                retained.Add(node);
            }
            node.Name = entry.Key ?? string.Empty;
            PatchEntry(node, entry, force || entry.IsNew);
        }

        foreach (WzImageProperty child in mapList.WzProperties.ToList())
        {
            if (!retained.Contains(child) && !surface.Entries.Any(e => e.OriginalKey == child.Name || (!e.IsNew && e.Key == child.Name)))
                mapList.RemoveProperty(child);
        }
        if (mapList.WzProperties.Count == 0 && !surface.OriginalHasMapList)
            image.RemoveProperty(mapList);
    }

    private static void PatchEntry(WzSubProperty node, WorldMapMapEntry entry, bool force)
    {
        PatchInt(node, "type", entry.Type, entry.OriginalType, entry.OriginalHasType, force);
        PatchPoint(node, "spot", entry.Spot, entry.OriginalSpot, entry.OriginalHasSpot, force);
        PatchString(node, "title", entry.Title, entry.OriginalTitle, entry.OriginalHasTitle, force && entry.Title != null);
        PatchString(node, "desc", entry.Description, entry.OriginalDescription, entry.OriginalHasDescription, force && entry.Description != null);
        PatchString(node, "townDesc", entry.TownDescription, entry.OriginalTownDescription, entry.OriginalHasTownDescription, force && entry.TownDescription != null);
        PatchNullableInt(node, "noToolTip", entry.NoToolTip, entry.OriginalNoToolTip, entry.OriginalHasNoToolTip, force && entry.NoToolTip.HasValue);
        PatchNullableInt(node, "noInfo", entry.NoInfo, entry.OriginalNoInfo, entry.OriginalHasNoInfo, force && entry.NoInfo.HasValue);
        PatchNullableInt(node, "linkQuestID", entry.LinkQuestId, entry.OriginalLinkQuestId, entry.OriginalHasLinkQuestId, force && entry.LinkQuestId.HasValue);
        PatchNullableInt(node, "partExtend", entry.PartExtend, entry.OriginalPartExtend, entry.OriginalHasPartExtend, force && entry.PartExtend.HasValue);
        PatchCanvas(node, "path", entry.Path, entry.OriginalPath, entry.OriginalHasPath);
        PatchMapIds(node, entry, force);
    }

    private static void PatchMapIds(WzSubProperty node, WorldMapMapEntry entry, bool force)
    {
        List<int> current = entry.MapIds.ToList();
        IReadOnlyList<int> original = entry.OriginalMapIds ?? Array.Empty<int>();
        if (!force && current.SequenceEqual(original)) return;
        WzImageProperty mapNo = node["mapNo"];
        if (current.Count == 0)
        {
            if (mapNo != null && (force || entry.OriginalHasMapNo)) node.RemoveProperty(mapNo);
            return;
        }
        if (current.Count == 1 && mapNo != null && mapNo is not WzSubProperty)
        {
            PatchInt(node, "mapNo", current[0], original.Count == 1 ? original[0] : 0, mapNo != null, true);
            return;
        }
        WzSubProperty list = mapNo as WzSubProperty;
        if (list == null)
        {
            if (mapNo != null) node.RemoveProperty(mapNo);
            list = new WzSubProperty("mapNo");
            node.AddProperty(list);
        }
        List<WzImageProperty> numeric = list.WzProperties.Where(p => int.TryParse(p.Name, out _)).ToList();
        for (int i = 0; i < current.Count; i++)
        {
            if (i < numeric.Count)
            {
                numeric[i].Name = numeric[i].Name ?? i.ToString();
                SetNumericValue(numeric[i], current[i]);
            }
            else
            {
                list.AddProperty(new WzIntProperty(NextNumericName(list.WzProperties), current[i]));
            }
        }
        for (int i = numeric.Count - 1; i >= current.Count; i--)
            list.RemoveProperty(numeric[i]);
    }

    private static void PatchLinks(WzImage image, WorldMapSurface surface, bool force, WorldMapCodecOptions options)
    {
        WzSubProperty links = image["MapLink"] as WzSubProperty;
        bool needs = force ? surface.Links.Count > 0 : surface.Links.Any(IsLinkChanged);
        if (links == null && needs) links = EnsureSubProperty(image, "MapLink");
        if (links == null) return;
        HashSet<WzImageProperty> retained = new();
        foreach (WorldMapLink link in surface.Links)
        {
            WzImageProperty raw = FindOriginalChild(links, link.OriginalKey, link.Key, link.IsNew);
            WzSubProperty node;
            if (raw is WzSubProperty sub) { node = sub; retained.Add(raw); }
            else { node = new WzSubProperty(link.Key); links.AddProperty(node); retained.Add(node); }
            node.Name = link.Key ?? string.Empty;
            PatchLink(node, link, force || link.IsNew);
        }
        foreach (WzImageProperty child in links.WzProperties.ToList())
            if (!retained.Contains(child) && !surface.Links.Any(l => l.OriginalKey == child.Name || (!l.IsNew && l.Key == child.Name))) links.RemoveProperty(child);
        if (links.WzProperties.Count == 0 && !surface.OriginalHasMapLinks) image.RemoveProperty(links);
    }

    private static void PatchLink(WzSubProperty node, WorldMapLink link, bool force)
    {
        PatchString(node, "toolTip", link.ToolTip, link.OriginalToolTip, link.OriginalHasToolTip, force && link.ToolTip != null);
        PatchNullablePoint(node, "spot", link.Spot, link.OriginalSpot, link.OriginalHasSpot, force && link.Spot.HasValue);
        bool needsNested = force || link.LinkMap != null || link.LinkImage != null || link.OriginalHasLink;
        WzSubProperty nested = node["link"] as WzSubProperty;
        if (nested == null && needsNested) nested = EnsureSubProperty(node, "link");
        if (nested == null) return;
        PatchString(nested, "linkMap", link.LinkMap, link.OriginalLinkMap, link.OriginalHasLinkMap, force && link.LinkMap != null);
        PatchCanvas(nested, "linkImg", link.LinkImage, link.OriginalLinkImage, link.OriginalLinkImage != null);
        if (!HasProperties(nested) && !link.OriginalHasLink) node.RemoveProperty(nested);
    }

    private static void PatchFog(WzImage image, WorldMapSurface surface, bool force, WorldMapCodecOptions options)
    {
        WzSubProperty fog = image["Fog"] as WzSubProperty;
        bool needs = force ? surface.FogLayers.Count > 0 : surface.FogLayers.Any(IsFogChanged);
        if (fog == null && needs) fog = EnsureSubProperty(image, "Fog");
        if (fog == null) return;
        HashSet<WzImageProperty> retained = new();
        foreach (WorldMapFogLayer layer in surface.FogLayers)
        {
            WzImageProperty raw = FindOriginalChild(fog, layer.OriginalKey, layer.Key, layer.IsNew);
            WzSubProperty node;
            if (raw is WzSubProperty sub) { node = sub; retained.Add(raw); }
            else { node = new WzSubProperty(layer.Key); fog.AddProperty(node); retained.Add(node); }
            node.Name = layer.Key ?? string.Empty;
            PatchCanvas(node, "0", layer.Image, layer.OriginalImage, layer.OriginalHasImage);
            PatchNullableInt(node, "quest", layer.Quest, layer.OriginalQuest, layer.OriginalHasQuest, force && layer.Quest.HasValue);
            PatchNullableInt(node, "qState", layer.QState, layer.OriginalQState, layer.OriginalHasQState, force && layer.QState.HasValue);
        }
        foreach (WzImageProperty child in fog.WzProperties.ToList())
            if (!retained.Contains(child) && !surface.FogLayers.Any(l => l.OriginalKey == child.Name || (!l.IsNew && l.Key == child.Name))) fog.RemoveProperty(child);
        if (fog.WzProperties.Count == 0 && !surface.OriginalHasFog) image.RemoveProperty(fog);
    }

    private static bool IsEntryChanged(WorldMapMapEntry entry)
    {
        return entry.IsNew || entry.Key != entry.OriginalKey || entry.Type != entry.OriginalType || entry.Spot != entry.OriginalSpot
            || !StringEquals(entry.Title, entry.OriginalTitle) || !StringEquals(entry.Description, entry.OriginalDescription)
            || !StringEquals(entry.TownDescription, entry.OriginalTownDescription) || entry.NoToolTip != entry.OriginalNoToolTip
            || entry.NoInfo != entry.OriginalNoInfo || entry.LinkQuestId != entry.OriginalLinkQuestId || entry.PartExtend != entry.OriginalPartExtend
            || !entry.MapIds.SequenceEqual(entry.OriginalMapIds ?? new List<int>()) || !CanvasEquivalent(entry.Path, entry.OriginalPath);
    }

    private static bool IsLinkChanged(WorldMapLink link)
    {
        return link.IsNew || link.Key != link.OriginalKey || !StringEquals(link.ToolTip, link.OriginalToolTip) || link.Spot != link.OriginalSpot
            || !StringEquals(link.LinkMap, link.OriginalLinkMap) || !CanvasEquivalent(link.LinkImage, link.OriginalLinkImage);
    }

    private static bool IsFogChanged(WorldMapFogLayer layer)
    {
        return layer.IsNew || layer.Key != layer.OriginalKey || layer.Quest != layer.OriginalQuest || layer.QState != layer.OriginalQState
            || !CanvasEquivalent(layer.Image, layer.OriginalImage);
    }

    private static WzImageProperty FindOriginalChild(WzSubProperty parent, string originalKey, string currentKey, bool isNew)
    {
        if (!isNew && !string.IsNullOrEmpty(originalKey))
        {
            WzImageProperty found = parent[originalKey];
            if (found != null) return found;
        }
        return isNew ? null : parent[currentKey];
    }

    private static WzSubProperty EnsureSubProperty(WzImage image, string name)
    {
        WzSubProperty result = new(name);
        image.AddProperty(result);
        return result;
    }

    private static WzSubProperty EnsureSubProperty(WzSubProperty parent, string name)
    {
        WzSubProperty result = new(name);
        parent.AddProperty(result);
        return result;
    }

    private static bool HasProperties(WzSubProperty property)
    {
        return property?.WzProperties != null && property.WzProperties.Count > 0;
    }

    private static void PatchString(WzImage image, string name, string current, string original, bool originalPresent, bool force)
    {
        PatchValue(image, name, current, original, originalPresent, force,
            existing => existing is WzStringProperty,
            value => new WzStringProperty(name, value),
            (existing, value) => ((WzStringProperty)existing).Value = value);
    }

    private static void PatchString(WzSubProperty parent, string name, string current, string original, bool originalPresent, bool force)
    {
        bool changed = force || !StringEquals(current, original) || (current == null && originalPresent);
        if (!changed) return;
        WzImageProperty existing = parent[name];
        if (current == null)
        {
            if (existing != null) parent.RemoveProperty(existing);
            return;
        }
        if (existing is WzStringProperty text) text.Value = current;
        else
        {
            if (existing != null) parent.RemoveProperty(existing);
            parent.AddProperty(new WzStringProperty(name, current));
        }
    }

    private static void PatchString(WzCanvasProperty parent, string name, string current, string original, bool originalPresent, bool force)
    {
        bool changed = force || !StringEquals(current, original) || (current == null && originalPresent);
        if (!changed) return;
        WzImageProperty existing = parent[name];
        if (current == null)
        {
            if (existing != null) parent.RemoveProperty(existing);
            return;
        }
        if (existing is WzStringProperty text) text.Value = current;
        else
        {
            if (existing != null) parent.RemoveProperty(existing);
            parent.AddProperty(new WzStringProperty(name, current));
        }
    }

    private static void PatchInt(WzSubProperty parent, string name, int current, int original, bool originalPresent, bool force)
    {
        if (!force && originalPresent && current == original || !force && !originalPresent && current == 0) return;
        WzImageProperty existing = parent[name];
        if (existing != null && IsNumeric(existing)) SetNumericValue(existing, current);
        else if (current != 0 || force)
        {
            if (existing != null) parent.RemoveProperty(existing);
            parent.AddProperty(new WzIntProperty(name, current));
        }
        else if (existing != null) parent.RemoveProperty(existing);
    }

    private static void PatchNullableInt(WzSubProperty parent, string name, int? current, int? original, bool originalPresent, bool force)
    {
        bool changed = current != original || current.HasValue != originalPresent;
        if (!changed && !force) return;
        if (!current.HasValue)
        {
            if (parent[name] != null) parent.RemoveProperty(name);
            return;
        }
        PatchInt(parent, name, current.Value, original ?? 0, originalPresent, true);
    }

    private static void PatchPoint(WzSubProperty parent, string name, Point current, Point original, bool originalPresent, bool force)
    {
        if (!force && originalPresent && current == original || !force && !originalPresent && current == Point.Empty) return;
        WzImageProperty existing = parent[name];
        if (existing is WzVectorProperty vector)
        {
            vector.X.Value = current.X;
            vector.Y.Value = current.Y;
        }
        else if (current != Point.Empty || force)
        {
            if (existing != null) parent.RemoveProperty(existing);
            parent.AddProperty(new WzVectorProperty(name, current.X, current.Y));
        }
        else if (existing != null) parent.RemoveProperty(existing);
    }

    private static void PatchNullablePoint(WzSubProperty parent, string name, Point? current, Point? original, bool originalPresent, bool force)
    {
        bool changed = current != original || current.HasValue != originalPresent;
        if (!changed && !force) return;
        if (!current.HasValue)
        {
            if (parent[name] != null) parent.RemoveProperty(name);
            return;
        }
        PatchPoint(parent, name, current.Value, original ?? Point.Empty, originalPresent, true);
    }

    private static void PatchCanvas(WzSubProperty parent, string name, WorldMapCanvasRef current, WorldMapCanvasRef original, bool originalPresent)
    {
        if (current == null)
        {
            if (originalPresent && parent[name] != null) parent.RemoveProperty(name);
            return;
        }
        WzImageProperty existing = parent[name];
        WzCanvasProperty canvas = existing as WzCanvasProperty;
        if (canvas == null)
        {
            if (current.RawProperty == null) return;
            canvas = current.RawProperty.DeepClone() as WzCanvasProperty;
            if (canvas == null) return;
            canvas.Name = name;
            if (existing != null) parent.RemoveProperty(existing);
            parent.AddProperty(canvas);
        }
        bool changed = original == null || !CanvasEquivalent(current, original);
        if (changed) PatchCanvasMetadata(canvas, current, original);
    }

    private static void PatchCanvas(WzSubProperty parent, string name, WorldMapCanvasRef current, WorldMapCanvasRef original)
    {
        PatchCanvas(parent, name, current, original, original != null);
    }

    private static void PatchCanvasMetadata(WzCanvasProperty canvas, WorldMapCanvasRef current, WorldMapCanvasRef original)
    {
        bool originChanged = original == null || current.HasOrigin != original.HasOrigin || current.Origin != original.Origin;
        if (originChanged)
        {
            WzImageProperty old = canvas[WzCanvasProperty.OriginPropertyName];
            if (!current.HasOrigin)
            {
                if (old != null) canvas.RemoveProperty(old);
            }
            else if (old is WzVectorProperty vector)
            {
                vector.X.Value = current.Origin.X;
                vector.Y.Value = current.Origin.Y;
            }
            else
            {
                if (old != null) canvas.RemoveProperty(old);
                canvas.AddProperty(new WzVectorProperty(WzCanvasProperty.OriginPropertyName, current.Origin.X, current.Origin.Y));
            }
        }
        bool zChanged = original == null || current.HasZ != original.HasZ || current.Z != original.Z;
        if (zChanged)
        {
            WzImageProperty old = canvas["z"];
            if (!current.HasZ)
            {
                if (old != null) canvas.RemoveProperty(old);
            }
            else if (old != null && IsNumeric(old)) SetNumericValue(old, current.Z);
            else
            {
                if (old != null) canvas.RemoveProperty(old);
                canvas.AddProperty(new WzIntProperty("z", current.Z));
            }
        }
        PatchString(canvas, WzCanvasProperty.InlinkPropertyName, current.Inlink, original?.Inlink, original?.Inlink != null, original == null || !StringEquals(current.Inlink, original.Inlink));
        PatchString(canvas, WzCanvasProperty.OutlinkPropertyName, current.Outlink, original?.Outlink, original?.Outlink != null, original == null || !StringEquals(current.Outlink, original.Outlink));
    }

    private static void PatchValue<T>(WzImage image, string name, T current, T original, bool originalPresent, bool force,
        Func<WzImageProperty, bool> compatible, Func<T, WzImageProperty> create, Action<WzImageProperty, T> assign)
    {
        bool changed = force || !EqualityComparer<T>.Default.Equals(current, original) || (current is null && originalPresent);
        if (!changed) return;
        WzImageProperty existing = image[name];
        if (current is null)
        {
            if (existing != null) image.RemoveProperty(existing);
            return;
        }
        if (existing != null && compatible(existing)) assign(existing, current);
        else
        {
            if (existing != null) image.RemoveProperty(existing);
            image.AddProperty(create(current));
        }
    }

    private static bool IsNumeric(WzImageProperty property)
    {
        return property is WzIntProperty || property is WzShortProperty || property is WzLongProperty;
    }

    private static void SetNumericValue(WzImageProperty property, int value)
    {
        if (property is WzIntProperty i) i.Value = value;
        else if (property is WzShortProperty s) s.Value = checked((short)value);
        else if (property is WzLongProperty l) l.Value = value;
        else property.SetValue(value);
    }

    private static string NextNumericName(IEnumerable<WzImageProperty> properties)
    {
        HashSet<int> used = new(properties.Select(p => int.TryParse(p.Name, out int n) ? n : -1));
        int next = 0;
        while (used.Contains(next)) next++;
        return next.ToString();
    }

    private static bool CanvasEquivalent(WorldMapCanvasRef a, WorldMapCanvasRef b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        return a.Width == b.Width && a.Height == b.Height && a.HasOrigin == b.HasOrigin && a.Origin == b.Origin
            && a.HasZ == b.HasZ && a.Z == b.Z && StringEquals(a.Inlink, b.Inlink) && StringEquals(a.Outlink, b.Outlink);
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
