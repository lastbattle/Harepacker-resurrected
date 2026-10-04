using System;
using System.Collections.Generic;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.WorldMap;

/// <summary>Codec for SearchExcept.img and SearchExceptForNPC.img.</summary>
public sealed class WorldMapExclusionList
{
    public WorldMapExclusionList(string imageName) => ImageName = imageName ?? string.Empty;
    public string ImageName { get; set; }
    public IList<(string Key, int MapId)> Entries { get; } = new List<(string Key, int MapId)>();
    public WzImage RawImage { get; private set; }

    public static bool IsExclusionImage(string imageName)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(imageName ?? string.Empty);
        return name.Equals("SearchExcept", StringComparison.OrdinalIgnoreCase) || name.Equals("SearchExceptForNPC", StringComparison.OrdinalIgnoreCase);
    }

    public static WorldMapExclusionList Read(WzImage image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        image.ParseImage();
        var result = new WorldMapExclusionList(image.Name) { RawImage = image.DeepClone() };
        foreach (WzImageProperty property in image.WzProperties)
            if (property is WzIntProperty value) result.Entries.Add((property.Name, value.Value));
        return result;
    }

    public WzImage Write()
    {
        WzImage image = RawImage?.DeepClone() ?? new WzImage(ImageName);
        HashSet<string> keys = Entries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        foreach (WzImageProperty property in image.WzProperties.ToArray())
            if (property is WzIntProperty && !keys.Contains(property.Name)) image.RemoveProperty(property);
        foreach ((string key, int mapId) in Entries)
        {
            image.RemoveProperty(key);
            image.AddProperty(new WzIntProperty(key, mapId));
        }
        image.Changed = true;
        return image;
    }

    public void Add(int mapId)
    {
        var used = Entries.Select(e => int.TryParse(e.Key, out int n) ? n : -1).ToHashSet();
        int key = 0; while (used.Contains(key)) key++;
        Entries.Add((key.ToString(), mapId));
    }
}
