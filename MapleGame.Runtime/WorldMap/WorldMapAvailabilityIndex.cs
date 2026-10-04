using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HaSharedLibrary.Wz;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.WorldMap;

/// <summary>One metadata-first projection of a map's native life entries.</summary>
public sealed record WorldMapAvailabilityRecord(
    int MapId,
    string ImagePath,
    string StreetName,
    string MapName,
    string CategoryName,
    bool MapExists,
    IReadOnlyDictionary<string, int> NpcOccurrences,
    IReadOnlyDictionary<string, int> MobOccurrences,
    IReadOnlyList<string> MissingNpcAssets,
    IReadOnlyList<string> MissingMobAssets,
    bool HasCategorisedLife,
    IReadOnlyList<string> Diagnostics,
    string SourceRevision)
{
    public IEnumerable<string> NpcIds => NpcOccurrences.Keys;
    public IEnumerable<string> MobIds => MobOccurrences.Keys;
}

/// <summary>Names used to present a map in the World Map UI.</summary>
public sealed record WorldMapAvailabilityNames(
    string StreetName,
    string MapName,
    string CategoryName);

/// <summary>Provides map display names without coupling the simulator to editor services.</summary>
public interface IWorldMapAvailabilityLookup
{
    bool TryGetMapNames(int mapId, out WorldMapAvailabilityNames names);
}

/// <summary>
/// Cancellation-aware, metadata-only map life index used by the World Map Editor.
/// It deliberately does not instantiate board items or decode entity frames.
/// </summary>
public sealed class WorldMapAvailabilityIndex : IDisposable
{
    private readonly IDataSource _source;
    private readonly IWorldMapAvailabilityLookup _lookup;
    private readonly ConcurrentDictionary<int, Lazy<Task<WorldMapAvailabilityRecord>>> _records = new();
    private readonly SemaphoreSlim _gate;
    private readonly string _sourceRevision;
    private bool _disposed;

    public WorldMapAvailabilityIndex(IDataSource source, IWorldMapAvailabilityLookup lookup = null, int maxConcurrency = 3)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _lookup = lookup;
        _gate = new SemaphoreSlim(Math.Max(1, maxConcurrency), Math.Max(1, maxConcurrency));
        _sourceRevision = source.VersionInfo?.Version ?? source.Name ?? string.Empty;
    }

    public IDataSource DataSource => _source;
    public int CachedCount => _records.Count;

    public Task<WorldMapAvailabilityRecord> GetAsync(int mapId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (mapId <= 0)
            return Task.FromResult(CreateMissing(mapId, "Map ID must be positive."));

        Lazy<Task<WorldMapAvailabilityRecord>> lazy = _records.GetOrAdd(mapId,
            id => new Lazy<Task<WorldMapAvailabilityRecord>>(() => LoadAsync(id), LazyThreadSafetyMode.ExecutionAndPublication));
        return AwaitWithCancellation(lazy.Value, cancellationToken);
    }

    public async IAsyncEnumerable<WorldMapAvailabilityRecord> ScanAsync(IEnumerable<int> mapIds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (int mapId in (mapIds ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await GetAsync(mapId, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Invalidate(int mapId)
    {
        if (mapId > 0)
            _records.TryRemove(mapId, out _);
    }

    public void InvalidateAll() => _records.Clear();

    private async Task<WorldMapAvailabilityRecord> LoadAsync(int mapId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ReadRecord(mapId)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private WorldMapAvailabilityRecord ReadRecord(int mapId)
    {
        string id = mapId.ToString("D9", CultureInfo.InvariantCulture);
        string path = $"Map/Map{ id[0] }/{id}.img";
        WzImage image = _source.GetImageByPath(path) ?? _source.GetImage("Map", $"Map{ id[0] }/{id}.img");
        WorldMapAvailabilityNames names = null;
        if (_lookup?.TryGetMapNames(mapId, out WorldMapAvailabilityNames resolvedNames) == true)
            names = resolvedNames;
        var npc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var mob = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var missingNpc = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingMob = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<string>();
        bool categorised = false;

        if (image == null)
        {
            diagnostics.Add($"Map image not found: {path}");
            return new WorldMapAvailabilityRecord(mapId, path, names?.StreetName ?? string.Empty, names?.MapName ?? string.Empty,
                names?.CategoryName ?? string.Empty, false, new ReadOnlyDictionary<string, int>(npc), new ReadOnlyDictionary<string, int>(mob),
                missingNpc.ToArray(), missingMob.ToArray(), false, diagnostics.AsReadOnly(), _sourceRevision);
        }

        try
        {
            WzImageProperty life = image["life"];
            if (life == null)
            {
                diagnostics.Add("Map has no life container.");
            }
            else
            {
                categorised = (life["isCategory"] as WzIntProperty)?.Value != 0;
                if (categorised)
                    diagnostics.Add("Categorised life is not expanded by the metadata index.");
                foreach (WzImageProperty child in life.WzProperties ?? Enumerable.Empty<WzImageProperty>())
                {
                    if (child is not WzSubProperty entry)
                        continue;
                    string type = ReadString(entry["type"]);
                    string entityId = ReadString(entry["id"]);
                    if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(entityId))
                    {
                        diagnostics.Add($"Life entry '{entry.Name}' is missing type or id.");
                        continue;
                    }
                    if (string.Equals(type, "n", StringComparison.OrdinalIgnoreCase))
                    {
                        npc[entityId] = npc.TryGetValue(entityId, out int count) ? count + 1 : 1;
                        if (!_source.ImageExists("Npc", $"{entityId}.img") && !_source.ImageExists("Npc", $"Npc/{entityId}.img"))
                            missingNpc.Add(entityId);
                    }
                    else if (string.Equals(type, "m", StringComparison.OrdinalIgnoreCase))
                    {
                        mob[entityId] = mob.TryGetValue(entityId, out int count) ? count + 1 : 1;
                        string mobFile = WzInfoTools.AddLeadingZeros(entityId, 7) + ".img";
                        if (!_source.ImageExists("Mob", mobFile) && !_source.ImageExists("Mob", $"Mob/{mobFile}"))
                            missingMob.Add(entityId);
                    }
                    else
                    {
                        diagnostics.Add($"Unsupported life type '{type}' in entry '{entry.Name}'.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Life scan failed: {ex.Message}");
        }

        return new WorldMapAvailabilityRecord(mapId, path, names?.StreetName ?? string.Empty, names?.MapName ?? string.Empty,
            names?.CategoryName ?? string.Empty, true, new ReadOnlyDictionary<string, int>(npc), new ReadOnlyDictionary<string, int>(mob),
            missingNpc.ToArray(), missingMob.ToArray(), categorised, diagnostics.AsReadOnly(), _sourceRevision);
    }

    private WorldMapAvailabilityRecord CreateMissing(int mapId, string diagnostic) =>
        new(mapId, string.Empty, string.Empty, string.Empty, string.Empty, false,
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()),
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()),
            Array.Empty<string>(), Array.Empty<string>(), false, new[] { diagnostic }, _sourceRevision);

    private static string ReadString(WzImageProperty property)
    {
        if (property == null) return null;
        try { return property.GetString(); } catch { return property.WzValue?.ToString(); }
    }

    private static async Task<T> AwaitWithCancellation<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
            return await task.ConfigureAwait(false);
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WorldMapAvailabilityIndex));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _records.Clear();
        _gate.Dispose();
    }
}
