using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;

namespace HaCreator.MapSimulator.Assets;

public interface IRuntimeMapProvider
{
    /// <summary>The caller owns and must dispose the returned map definition.</summary>
    RuntimeMapDefinition Load(int mapId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads saved maps through the session asset source and gives launch-time editor
/// snapshots precedence. It never opens editor tabs or creates editor map objects.
/// </summary>
public sealed class RuntimeMapProvider : IRuntimeMapProvider, IDisposable
{
    private readonly IRuntimeDataServices services;
    private readonly Dictionary<int, RuntimeMapDefinition> snapshots = new();
    private readonly object gate = new();
    private bool disposed;

    public RuntimeMapProvider(IRuntimeDataServices services, IEnumerable<RuntimeMapDefinition> overrides = null)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        try
        {
            foreach (RuntimeMapDefinition definition in overrides ?? Array.Empty<RuntimeMapDefinition>())
            {
                if (snapshots.ContainsKey(definition.MapId))
                    throw new ArgumentException($"Multiple launch snapshots have map id {definition.MapId}.", nameof(overrides));
                snapshots.Add(definition.MapId, definition.Clone());
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public RuntimeMapDefinition Load(int mapId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (snapshots.TryGetValue(mapId, out RuntimeMapDefinition snapshot))
                return snapshot.Clone();
            if (mapId < 0 || mapId > 999999999)
                throw new ArgumentOutOfRangeException(nameof(mapId));

            string id = mapId.ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
            WzImage image = services.Assets.FindImage("Map", $"Map/Map{id[0]}/{id}.img")
                ?? services.Assets.FindImage("Map", id + ".img");
            if (image == null)
                throw new FileNotFoundException($"Map {id} was not found in '{services.Assets.Name}'.");

            services.Catalog.TryGetMapName(id, out RuntimeMapName name);
            RuntimeMapDefinition result = new WzRuntimeMapReader().Read(image, new WzRuntimeMapReaderOptions
            {
                MapId = mapId,
                MapName = name?.MapName ?? id,
                StreetName = name?.StreetName ?? string.Empty,
                CategoryName = name?.CategoryName ?? string.Empty,
                TooltipStrings = services.Assets.FindImage("String", "ToolTipHelp.img"),
                AssetSource = services.Assets,
                Catalog = services.Catalog
            });
            if (cancellationToken.IsCancellationRequested)
            {
                result.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return result;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            foreach (RuntimeMapDefinition snapshot in snapshots.Values) snapshot.Dispose();
            snapshots.Clear();
        }
    }
}
