using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

namespace HaCreator.MapSimulator.Contracts;

/// <summary>One NPC name shown by the expanded minimap.</summary>
public sealed record RuntimeMinimapNpcMarker(string Name);

/// <summary>
/// Detached map data consumed by the minimap renderer. Bitmap inputs are cloned
/// on construction and owned by this value, so the renderer never retains a
/// Board, MapInfo, or editor cache reference.
/// </summary>
public sealed class RuntimeMinimapData : IDisposable
{
    private bool _disposed;

    public RuntimeMinimapData(
        int mapId,
        bool zeroSideOnly,
        string mapName,
        string streetName,
        string mapMarkName,
        Bitmap miniMap,
        Bitmap mapMarkImage = null,
        IEnumerable<RuntimeMinimapNpcMarker> npcMarkers = null)
    {
        MapId = mapId;
        ZeroSideOnly = zeroSideOnly;
        MapName = mapName ?? string.Empty;
        StreetName = streetName ?? string.Empty;
        MapMarkName = mapMarkName ?? string.Empty;
        MiniMap = CloneBitmap(miniMap);
        MapMarkImage = CloneBitmap(mapMarkImage);
        NpcMarkers = new ReadOnlyCollection<RuntimeMinimapNpcMarker>(
            (npcMarkers ?? Enumerable.Empty<RuntimeMinimapNpcMarker>())
                .Where(marker => marker != null && !string.IsNullOrWhiteSpace(marker.Name))
                .Select(marker => new RuntimeMinimapNpcMarker(marker.Name.Trim()))
                .ToArray());
    }

    public int MapId { get; }
    public bool ZeroSideOnly { get; }
    public string MapName { get; }
    public string StreetName { get; }
    public string MapMarkName { get; }
    public Bitmap MiniMap { get; }
    public Bitmap MapMarkImage { get; }
    public IReadOnlyList<RuntimeMinimapNpcMarker> NpcMarkers { get; }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        MiniMap?.Dispose();
        MapMarkImage?.Dispose();
    }

    private static Bitmap CloneBitmap(Bitmap source) =>
        source == null ? null : new Bitmap(source);
}
