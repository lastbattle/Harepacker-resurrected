using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.WorldMap;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.WorldMap;

/// <summary>Converts the shared editable model to the simulator's graphics-thread definition.</summary>
public static class WorldMapRuntimeAdapter
{
    public static IReadOnlyList<WorldMapUI.WorldMapSurfaceDefinition> ToSurfaceDefinitions(
        IEnumerable<WorldMapDocument> documents,
        Func<WorldMapCanvasRef, Texture2D> textureResolver)
    {
        var result = new List<WorldMapUI.WorldMapSurfaceDefinition>();
        foreach (WorldMapDocument document in documents ?? Enumerable.Empty<WorldMapDocument>())
        {
            WorldMapSurface surface = document?.Surface;
            if (surface == null || string.IsNullOrWhiteSpace(surface.LogicalName))
                continue;
            Texture2D texture = textureResolver?.Invoke(surface.BaseImage);
            if (texture == null)
                continue;
            var spots = new Dictionary<int, Microsoft.Xna.Framework.Point>();
            foreach (WorldMapMapEntry entry in surface.Entries)
            {
                foreach (int mapId in entry.MapIds.Where(id => id > 0))
                    spots[mapId] = new Microsoft.Xna.Framework.Point(entry.Spot.X, entry.Spot.Y);
            }
            Point origin = surface.BaseImage?.Origin ?? Point.Empty;
            result.Add(new WorldMapUI.WorldMapSurfaceDefinition
            {
                SurfaceName = surface.LogicalName,
                ParentSurfaceName = surface.ParentName ?? string.Empty,
                BaseTexture = texture,
                BaseOrigin = new Microsoft.Xna.Framework.Point(origin.X, origin.Y),
                MapSpots = spots
            });
        }
        return result;
    }
}
