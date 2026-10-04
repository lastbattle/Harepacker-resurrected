using System;
using System.Drawing;
using System.IO;
using System.Linq;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Assets;

public static class RuntimeMinimapFactory
{
    public static RuntimeMinimapData Create(RuntimeMapDefinition map, IRuntimeDataServices services)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(services);
        var info = map.CreateMapInfo();
        try
        {
            byte[] png = map.CreateMinimapPngCopy();
            using var stream = new MemoryStream(png, writable: false);
            using Bitmap minimap = png.Length == 0 ? null : new Bitmap(stream);
            var mark = services.Assets.FindObject("Map", $"MapHelper.img/mark/{info.mapMark}") as WzCanvasProperty;
            using Bitmap markBitmap = mark?.GetLinkedWzCanvasBitmap();
            // RuntimeMinimapData owns copies of both decoded bitmaps.
            return new RuntimeMinimapData(map.MapId, info.zeroSideOnly, map.MapName,
                map.StreetName, info.mapMark, minimap, markBitmap,
                map.Npcs.Select(npc => new RuntimeMinimapNpcMarker(
                    services.Catalog.TryGetNpcName(npc.Id, out RuntimeNpcName name)
                        ? name.Name : npc.DisplayName)));
        }
        finally { info.Image?.Dispose(); }
    }
}
