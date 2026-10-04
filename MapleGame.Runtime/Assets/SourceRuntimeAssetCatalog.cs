using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Assets;

/// <summary>
/// IDataSource-independent metadata catalog for the runtime. It reads String and
/// asset images through IRuntimeAssetSource, so it works with IMG, WZ, hybrid, and
/// editor-preview sources without importing WzInformationManager or Program.
/// </summary>
public sealed class SourceRuntimeAssetCatalog : IRuntimeAssetCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeMapName> _maps =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, RuntimeItemName> _items = new();
    private readonly Dictionary<string, RuntimeMobName> _mobs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeNpcName> _npcs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeSkillName> _skills =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeBgmReference> _bgms =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RuntimeReactorAsset> _reactors =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _mapsLoaded;
    private bool _itemsLoaded;
    private bool _mobsLoaded;
    private bool _npcsLoaded;
    private bool _skillsLoaded;
    private bool _reactorsLoaded;

    public SourceRuntimeAssetCatalog(IRuntimeAssetSource assets, IRuntimeDiagnostics diagnostics = null)
    {
        Assets = assets ?? throw new ArgumentNullException(nameof(assets));
        Diagnostics = diagnostics ?? NullRuntimeDiagnostics.Instance;
    }

    public IRuntimeAssetSource Assets { get; }
    public IRuntimeDiagnostics Diagnostics { get; }
    public bool IsPreBBDataWzFormat => Assets.Version?.IsPreBBDataWzFormat ??
        Assets.Version?.IsPreBB ?? false;

    public WzImage GetTileSet(string name) => FindMapSet("Tile", name);
    public WzImage GetObjectSet(string name) => FindMapSet("Obj", name);
    public WzImage GetBackgroundSet(string name) => FindMapSet("Back", name);

    public bool TryGetMapName(string mapId, out RuntimeMapName value)
    {
        EnsureMaps();
        return _maps.TryGetValue(NormalizeId(mapId, 9), out value);
    }

    public IReadOnlyDictionary<string, RuntimeMapName> GetMapNames()
    {
        EnsureMaps();
        return new ReadOnlyDictionary<string, RuntimeMapName>(_maps);
    }

    public IReadOnlyDictionary<int, RuntimeItemName> GetItemNames()
    {
        EnsureItems();
        return new ReadOnlyDictionary<int, RuntimeItemName>(_items);
    }

    public bool TryGetItemName(int itemId, out RuntimeItemName value)
    {
        EnsureItems();
        return _items.TryGetValue(itemId, out value);
    }

    public bool TryGetMobName(string mobId, out RuntimeMobName value)
    {
        EnsureMobs();
        string normalized = NormalizeId(mobId, 7);
        if (_mobs.TryGetValue(normalized, out value))
            return true;
        return _mobs.TryGetValue(TrimLeadingZeroes(normalized), out value);
    }

    public bool TryGetNpcName(string npcId, out RuntimeNpcName value)
    {
        EnsureNpcs();
        string normalized = NormalizeId(npcId, 7);
        if (_npcs.TryGetValue(normalized, out value))
            return true;
        return _npcs.TryGetValue(TrimLeadingZeroes(normalized), out value);
    }

    public bool TryGetSkillName(string skillId, out RuntimeSkillName value)
    {
        EnsureSkills();
        return _skills.TryGetValue(skillId ?? string.Empty, out value);
    }

    public bool TryGetBgm(string name, out RuntimeBgmAsset value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(name) || !TryResolveBgmPath(name, out string imagePath, out string propertyPath))
            return false;

        string key = imagePath + "/" + propertyPath;
        lock (_gate)
        {
            if (!_bgms.ContainsKey(key))
                _bgms[key] = new RuntimeBgmReference(imagePath, propertyPath);
        }

        WzImage image = Assets.FindImage("Sound", imagePath);
        if (image == null)
            return false;
        image.ParseImage();
        WzImageProperty property = image.GetFromPath(propertyPath);
        WzBinaryProperty binary = property as WzBinaryProperty;
        if (binary == null)
        {
            try { binary = property?.GetLinkedWzImageProperty() as WzBinaryProperty; }
            catch (Exception error) { Diagnostics.Report("Failed to resolve BGM link.", error); }
        }
        if (binary == null)
            return false;
        value = new RuntimeBgmAsset(imagePath, propertyPath, binary);
        return true;
    }

    public bool TryGetReactor(string reactorId, out RuntimeReactorAsset value)
    {
        EnsureReactors();
        string normalized = NormalizeId(reactorId, 7);
        if (_reactors.TryGetValue(normalized, out value))
            return true;
        return _reactors.TryGetValue(TrimLeadingZeroes(normalized), out value);
    }

    public bool TryGetItemIcon(int itemId, string categoryName, out WzCanvasProperty value)
    {
        value = null;
        string category = categoryName ?? string.Empty;
        WzImage image = null;
        if (IsEquipmentCategory(category))
        {
            TryGetEquipment(itemId, category, out image);
            value = image?["info"]?["icon"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
        }
        else
        {
            string imagePath = ItemImagePath(itemId, category);
            image = Assets.FindImage("Item", imagePath);
            if (IsPetCategory(category))
            {
                value = image?["info"]?["icon"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
            }
            else
            {
                string padded = itemId.ToString("D8", CultureInfo.InvariantCulture);
                WzImageProperty item = image?[itemId.ToString(CultureInfo.InvariantCulture)] ?? image?[padded];
                value = item?["info"]?["icon"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
            }
        }
        return value != null;
    }

    public bool TryGetMobIcon(int mobId, out WzCanvasProperty value)
    {
        value = null;
        WzImage image = Assets.FindImage("Mob", mobId.ToString("D7", CultureInfo.InvariantCulture) + ".img");
        value = image?["stand"]?["0"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
        return value != null;
    }

    public bool TryGetEquipment(int itemId, string categoryName, out WzImage value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(categoryName))
            return false;
        string imageName = itemId.ToString("D8", CultureInfo.InvariantCulture) + ".img";
        value = Assets.FindImage("Character", categoryName + "/" + imageName);
        return value != null;
    }

    public IReadOnlyList<string> GetMobIds()
    {
        EnsureMobs();
        return GetIds("Mob", _mobs.Keys);
    }

    public IReadOnlyList<string> GetNpcIds()
    {
        EnsureNpcs();
        return GetIds("Npc", _npcs.Keys);
    }

    public IReadOnlyList<string> GetReactorIds()
    {
        EnsureReactors();
        return GetIds("Reactor", _reactors.Keys);
    }

    private WzImage FindMapSet(string folder, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return Assets.FindImage("Map", folder + "/" + name + ".img") ??
            Assets.FindImage("Map", folder + "/" + name);
    }

    private void EnsureMaps()
    {
        lock (_gate)
        {
            if (_mapsLoaded)
                return;
            _mapsLoaded = true;
        }
        WzImage image = Assets.FindImage("String", "Map.img");
        if (image == null)
            return;
        image.ParseImage();
        foreach (WzImageProperty category in image.WzProperties)
        foreach (WzImageProperty map in Children(category))
        {
            string id = NormalizeId(map.Name, 9);
            string mapName = Text(map["mapName"]) ?? "NO NAME";
            string street = Text(map["streetName"]) ?? string.Empty;
            lock (_gate)
                _maps.TryAdd(id, new RuntimeMapName(street, mapName, category.Name));
        }
    }

    private void EnsureMobs()
    {
        lock (_gate) { if (_mobsLoaded) return; _mobsLoaded = true; }
        WzImage image = Assets.FindImage("String", "Mob.img");
        LoadSimpleNames(image, (id, node) =>
        {
            string normalized = NormalizeId(id, 7);
            lock (_gate) _mobs.TryAdd(normalized, new RuntimeMobName(Text(node["name"]) ?? "NO NAME"));
        });
    }

    private void EnsureNpcs()
    {
        lock (_gate) { if (_npcsLoaded) return; _npcsLoaded = true; }
        WzImage image = Assets.FindImage("String", "Npc.img");
        LoadSimpleNames(image, (id, node) =>
        {
            string normalized = NormalizeId(id, 7);
            lock (_gate) _npcs.TryAdd(normalized, new RuntimeNpcName(
                Text(node["name"]) ?? "NO NAME", Text(node["func"]) ?? string.Empty));
        });
    }

    private void EnsureSkills()
    {
        lock (_gate) { if (_skillsLoaded) return; _skillsLoaded = true; }
        WzImage image = Assets.FindImage("String", "Skill.img");
        LoadSimpleNames(image, (id, node) =>
        {
            lock (_gate) _skills.TryAdd(id, new RuntimeSkillName(
                Text(node["name"]) ?? "NO NAME", Text(node["desc"]) ?? "NO DESC"));
        });
    }

    private void EnsureItems()
    {
        lock (_gate) { if (_itemsLoaded) return; _itemsLoaded = true; }
        foreach ((string Image, string Category) entry in new[]
        {
            ("Eqp.img", "Eqp"), ("Ins.img", "Ins"), ("Cash.img", "Cash"),
            ("Consume.img", "Consume"), ("Etc.img", "Etc"), ("Pet.img", "Pet")
        })
        {
            WzImage image = Assets.FindImage("String", entry.Image);
            if (image == null)
                continue;
            image.ParseImage();
            foreach (WzImageProperty child in image.WzProperties)
                ReadItemNodes(child, entry.Category);
        }
    }

    private void ReadItemNodes(WzImageProperty node, string category)
    {
        if (node == null)
            return;
        string itemName = Text(node["name"]);
        if (int.TryParse(node.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) &&
            itemName != null)
        {
            lock (_gate)
                _items.TryAdd(id, new RuntimeItemName(category, itemName, Text(node["desc"]) ?? "NO DESC"));
        }
        foreach (WzImageProperty child in Children(node))
            ReadItemNodes(child, category == "Eqp" && itemName == null ? node.Name : category);
    }

    private void EnsureReactors()
    {
        lock (_gate) { if (_reactorsLoaded) return; _reactorsLoaded = true; }
        foreach (string id in Assets.GetImageNamesInDirectory("Reactor", string.Empty))
        {
            string normalized = NormalizeId(id, 7);
            WzImage image = Assets.FindImage("Reactor", EnsureImg(id));
            if (image == null)
                continue;
            WzImageProperty info = image["info"];
            string name = Text(info?["info"]) ?? Text(info?["viewName"]) ?? string.Empty;
            lock (_gate)
                _reactors.TryAdd(normalized, new RuntimeReactorAsset(normalized, name, image));
        }
    }

    private void LoadSimpleNames(WzImage image, Action<string, WzImageProperty> add)
    {
        if (image == null)
            return;
        image.ParseImage();
        foreach (WzImageProperty node in image.WzProperties)
            add(node.Name, node);
    }

    private IReadOnlyList<string> GetIds(string category, IEnumerable<string> cached)
    {
        HashSet<string> ids = new(cached ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (string name in Assets.GetImageNamesInDirectory(category, string.Empty) ?? Array.Empty<string>())
        {
            string id = TrimLeadingZeroes(PathWithoutExtension(name));
            if (id.Length == 0)
                id = "0";
            ids.Add(id);
        }
        return new ReadOnlyCollection<string>(ids.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IEnumerable<WzImageProperty> Children(WzImageProperty property) =>
        property?.WzProperties == null
            ? Array.Empty<WzImageProperty>()
            : property.WzProperties.Cast<WzImageProperty>();
    private static string Text(WzImageProperty property) => property switch
    {
        WzStringProperty text => text.Value,
        _ => property?.WzValue?.ToString()
    };
    private static string EnsureImg(string name) =>
        name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? name : name + ".img";
    private static string PathWithoutExtension(string name) =>
        name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    private static string NormalizeId(string value, int width)
    {
        if (!int.TryParse(PathWithoutExtension(value ?? string.Empty), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int id))
            return value ?? string.Empty;
        return id.ToString("D" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
    private static string TrimLeadingZeroes(string value)
    {
        string trimmed = (value ?? string.Empty).TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }
    private static bool IsEquipmentCategory(string category) =>
        string.Equals(category, "Eqp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(category, "Character", StringComparison.OrdinalIgnoreCase);
    private static bool IsPetCategory(string category) =>
        string.Equals(category, "Pet", StringComparison.OrdinalIgnoreCase);
    private static string ItemImagePath(int id, string category)
    {
        string padded = id.ToString("D8", CultureInfo.InvariantCulture);
        if (IsPetCategory(category))
            return "Pet/" + id.ToString(CultureInfo.InvariantCulture) + ".img";
        string folder = string.Equals(category, "Ins", StringComparison.OrdinalIgnoreCase) ? "Install" : category;
        return folder + "/" + padded[..4] + ".img";
    }
    private static bool TryResolveBgmPath(string name, out string imagePath, out string propertyPath)
    {
        imagePath = null;
        propertyPath = null;
        string[] segments = (name ?? string.Empty).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        int first = segments.Length > 0 && string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (segments.Length - first < 2)
            return false;
        int imageEnd = Array.FindIndex(segments, first, segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
        if (imageEnd < first)
            imageEnd = first;
        if (imageEnd >= segments.Length - 1)
            return false;
        imagePath = string.Join('/', segments.Skip(first).Take(imageEnd - first + 1));
        imagePath = EnsureImg(imagePath);
        propertyPath = string.Join('/', segments.Skip(imageEnd + 1));
        return propertyPath.Length > 0;
    }

    private sealed record RuntimeBgmReference(string ImagePath, string PropertyPath);
}
