using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapEditor.Simulation;

/// <summary>
/// Captures immutable editor names at preview launch. Image assets always come
/// from the session source, which owns their lifetime and authored overrides.
/// The running preview retains no editor information manager or cached WZ nodes.
/// </summary>
public sealed class HaCreatorRuntimeAssetCatalog : IRuntimeAssetCatalog
{
    private readonly Dictionary<string, Tuple<string, string, string>> mapNames;
    private readonly Dictionary<int, Tuple<string, string, string>> itemNames;
    private readonly Dictionary<string, string> mobNames;
    private readonly Dictionary<string, Tuple<string, string>> npcNames;
    private readonly Dictionary<string, Tuple<string, string>> skillNames;
    private readonly Dictionary<string, string> reactorNames;
    private readonly Dictionary<string, (string ImageName, string PropertyPath)> bgms;
    private readonly SourceRuntimeAssetCatalog _sourceFallback;
    private readonly IRuntimeDiagnostics _diagnostics;

    public HaCreatorRuntimeAssetCatalog(
        WzInformationManager information,
        IRuntimeAssetSource assets,
        IRuntimeDiagnostics diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(information);
        mapNames = new(information.MapsNameCache, StringComparer.OrdinalIgnoreCase);
        itemNames = new(information.ItemNameCache);
        mobNames = new(information.MobNameCache, StringComparer.OrdinalIgnoreCase);
        npcNames = new(information.NpcNameCache, StringComparer.OrdinalIgnoreCase);
        skillNames = new(information.SkillNameCache, StringComparer.OrdinalIgnoreCase);
        reactorNames = information.Reactors.ToDictionary(pair => pair.Key, pair => pair.Value?.Name,
            StringComparer.OrdinalIgnoreCase);
        bgms = information.BGMs.Where(pair => pair.Value != null).ToDictionary(
            pair => pair.Key, pair => (pair.Value.ImageName, pair.Value.PropertyPath),
            StringComparer.OrdinalIgnoreCase);
        Assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _diagnostics = diagnostics ?? NullRuntimeDiagnostics.Instance;
        _sourceFallback = new SourceRuntimeAssetCatalog(Assets, _diagnostics);
    }

    public IRuntimeAssetSource Assets { get; }

    public bool IsPreBBDataWzFormat => Assets.Version?.IsPreBBDataWzFormat ??
        Assets.Version?.IsPreBB ?? false;

    public WzImage GetTileSet(string name) => _sourceFallback.GetTileSet(name);
    public WzImage GetObjectSet(string name) => _sourceFallback.GetObjectSet(name);
    public WzImage GetBackgroundSet(string name) => _sourceFallback.GetBackgroundSet(name);

    public bool TryGetMapName(string mapId, out RuntimeMapName value)
    {
        value = null;
        if (TryGetStringValue(mapNames, mapId, 9,
                out Tuple<string, string, string> cached) && cached != null)
        {
            value = new RuntimeMapName(cached.Item1, cached.Item2, cached.Item3);
            return true;
        }
        return _sourceFallback.TryGetMapName(mapId, out value);
    }

    public IReadOnlyDictionary<string, RuntimeMapName> GetMapNames()
    {
        var names = new Dictionary<string, RuntimeMapName>(_sourceFallback.GetMapNames());
        foreach (var pair in mapNames)
            if (pair.Value != null)
                names[pair.Key] = new RuntimeMapName(pair.Value.Item1, pair.Value.Item2, pair.Value.Item3);
        return new ReadOnlyDictionary<string, RuntimeMapName>(names);
    }

    public IReadOnlyDictionary<int, RuntimeItemName> GetItemNames()
    {
        var names = new Dictionary<int, RuntimeItemName>(_sourceFallback.GetItemNames());
        foreach (var pair in itemNames)
            if (pair.Value != null)
                names[pair.Key] = new RuntimeItemName(pair.Value.Item1, pair.Value.Item2, pair.Value.Item3);
        return new ReadOnlyDictionary<int, RuntimeItemName>(names);
    }

    public bool TryGetItemName(int itemId, out RuntimeItemName value)
    {
        value = null;
        if (itemNames.TryGetValue(itemId,
                out Tuple<string, string, string> cached) && cached != null)
        {
            value = new RuntimeItemName(cached.Item1, cached.Item2, cached.Item3);
            return true;
        }
        return _sourceFallback.TryGetItemName(itemId, out value);
    }

    public bool TryGetMobName(string mobId, out RuntimeMobName value)
    {
        value = null;
        if (TryGetStringValue(mobNames, mobId, 7,
                out string cached) && cached != null)
        {
            value = new RuntimeMobName(cached);
            return true;
        }
        return _sourceFallback.TryGetMobName(mobId, out value);
    }

    public bool TryGetNpcName(string npcId, out RuntimeNpcName value)
    {
        value = null;
        if (TryGetStringValue(npcNames, npcId, 7,
                out Tuple<string, string> cached) && cached != null)
        {
            value = new RuntimeNpcName(cached.Item1, cached.Item2);
            return true;
        }
        return _sourceFallback.TryGetNpcName(npcId, out value);
    }

    public bool TryGetSkillName(string skillId, out RuntimeSkillName value)
    {
        value = null;
        if (skillNames.TryGetValue(skillId ?? string.Empty,
                out Tuple<string, string> cached) && cached != null)
        {
            value = new RuntimeSkillName(cached.Item1, cached.Item2);
            return true;
        }
        return _sourceFallback.TryGetSkillName(skillId, out value);
    }

    public bool TryGetBgm(string name, out RuntimeBgmAsset value)
    {
        value = null;
        if (!TryGetBgmEntry(name, out string imagePath, out string propertyPath))
            return _sourceFallback.TryGetBgm(name, out value);

        WzImage image = Assets.FindImage("Sound", imagePath) ??
            Assets.FindImage("Sound", EnsureImg(imagePath));
        if (image == null)
            return _sourceFallback.TryGetBgm(name, out value);

        image.ParseImage();
        WzImageProperty property = image.GetFromPath(propertyPath);
        WzBinaryProperty binary = property as WzBinaryProperty;
        if (binary == null)
        {
            try
            {
                binary = property?.GetLinkedWzImageProperty() as WzBinaryProperty;
            }
            catch (Exception error)
            {
                _diagnostics.Report("Failed to resolve editor BGM link.", error);
            }
        }

        if (binary == null)
            return _sourceFallback.TryGetBgm(name, out value);

        value = new RuntimeBgmAsset(imagePath, propertyPath, binary);
        return true;
    }

    public bool TryGetReactor(string reactorId, out RuntimeReactorAsset value)
    {
        value = null;
        string normalized = NormalizeId(reactorId, 7);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        WzImage image = Assets.FindImage("Reactor", EnsureImg(normalized));
        if (image == null)
            return _sourceFallback.TryGetReactor(reactorId, out value);

        image.ParseImage();
        WzImage linked = ResolveLinkedReactor(image);
        string name = GetCachedReactorName(normalized);
        if (name == null)
        {
            WzImageProperty info = image["info"];
            name = Text(info?["info"]) ?? Text(info?["viewName"]) ?? string.Empty;
        }

        value = new RuntimeReactorAsset(normalized, name, linked ?? image);
        return true;
    }

    public bool TryGetItemIcon(int itemId, string categoryName, out WzCanvasProperty value) =>
        _sourceFallback.TryGetItemIcon(itemId, categoryName, out value);

    public bool TryGetMobIcon(int mobId, out WzCanvasProperty value) =>
        _sourceFallback.TryGetMobIcon(mobId, out value);

    public bool TryGetEquipment(int itemId, string categoryName, out WzImage value) =>
        _sourceFallback.TryGetEquipment(itemId, categoryName, out value);

    public IReadOnlyList<string> GetMobIds() => GetIds("Mob", mobNames.Keys);

    public IReadOnlyList<string> GetNpcIds() => GetIds("Npc", npcNames.Keys);

    public IReadOnlyList<string> GetReactorIds() => GetIds("Reactor", reactorNames.Keys);

    private bool TryGetBgmEntry(string name, out string imagePath, out string propertyPath)
    {
        imagePath = null;
        propertyPath = null;
        if (!string.IsNullOrWhiteSpace(name) && bgms.TryGetValue(name, out var entry))
        {
            imagePath = entry.ImageName;
            propertyPath = entry.PropertyPath;
            return !string.IsNullOrWhiteSpace(imagePath) && !string.IsNullOrWhiteSpace(propertyPath);
        }
        return TryResolveBgmPath(name, out imagePath, out propertyPath);
    }

    private string GetCachedReactorName(string normalizedId)
    {
        foreach (var pair in reactorNames)
        {
            if (Candidates(normalizedId, 7).Any(candidate =>
                    string.Equals(candidate, pair.Key, StringComparison.OrdinalIgnoreCase)))
                return pair.Value;
        }
        return null;
    }

    private IReadOnlyList<string> GetIds(string category, IEnumerable<string> cachedIds)
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        foreach (string cachedId in cachedIds ?? Array.Empty<string>())
        {
            string normalized = TrimLeadingZeroes(PathWithoutExtension(cachedId));
            ids.Add(normalized.Length == 0 ? "0" : normalized);
        }
        foreach (string imageName in Assets.GetImageNamesInDirectory(category, string.Empty) ?? Array.Empty<string>())
        {
            string normalized = TrimLeadingZeroes(PathWithoutExtension(imageName));
            ids.Add(normalized.Length == 0 ? "0" : normalized);
        }
        return new ReadOnlyCollection<string>(ids.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool TryGetStringValue<T>(IDictionary<string, T> values, string id,
        int width, out T value)
    {
        foreach (string candidate in Candidates(id, width))
        {
            if (values.TryGetValue(candidate, out value))
                return true;
        }
        value = default;
        return false;
    }

    private static IEnumerable<string> Candidates(string id, int width)
    {
        string raw = id ?? string.Empty;
        if (raw.Length != 0)
            yield return raw;

        string withoutExtension = PathWithoutExtension(raw);
        if (int.TryParse(withoutExtension, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int numeric))
        {
            string padded = numeric.ToString("D" + width.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
            if (!string.Equals(raw, padded, StringComparison.OrdinalIgnoreCase))
                yield return padded;

            string unpadded = TrimLeadingZeroes(withoutExtension);
            if (unpadded.Length == 0)
                unpadded = "0";
            if (!string.Equals(raw, unpadded, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(padded, unpadded, StringComparison.OrdinalIgnoreCase))
                yield return unpadded;
        }
    }

    private WzImage ResolveLinkedReactor(WzImage image)
    {
        WzStringProperty link = image["info"]?["link"] as WzStringProperty;
        if (link == null)
            return null;
        string linkedId = NormalizeId(link.Value, 7);
        if (string.IsNullOrWhiteSpace(linkedId))
            return null;
        WzImage linked = Assets.FindImage("Reactor", EnsureImg(linkedId));
        linked?.ParseImage();
        return linked;
    }

    private static string Text(WzImageProperty property) => property switch
    {
        WzStringProperty text => text.Value,
        _ => property?.WzValue?.ToString()
    };

    private static string EnsureImg(string name) =>
        name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? name : name + ".img";

    private static string PathWithoutExtension(string name) =>
        name != null && name.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name ?? string.Empty;

    private static string NormalizeId(string value, int width)
    {
        string path = PathWithoutExtension(value);
        if (!int.TryParse(path, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            return path;
        return id.ToString("D" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private static string TrimLeadingZeroes(string value)
    {
        string trimmed = (value ?? string.Empty).TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static bool TryResolveBgmPath(string name, out string imagePath, out string propertyPath)
    {
        imagePath = null;
        propertyPath = null;
        string[] segments = (name ?? string.Empty).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        int first = segments.Length > 0 && string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (segments.Length - first < 2)
            return false;
        int imageEnd = Array.FindIndex(segments, first,
            segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
        if (imageEnd < first)
            imageEnd = first;
        if (imageEnd >= segments.Length - 1)
            return false;
        imagePath = EnsureImg(string.Join('/', segments.Skip(first).Take(imageEnd - first + 1)));
        propertyPath = string.Join('/', segments.Skip(imageEnd + 1));
        return propertyPath.Length > 0;
    }
}
