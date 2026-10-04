using MapleLib.Img;
using MapleLib.WzLib;

namespace UnitTest_WorldMapEditor;

/// <summary>Minimal IDataSource test double for metadata-only world-map tests.</summary>
internal sealed class InMemoryWorldMapDataSource : IDataSource
{
    private readonly Dictionary<string, WzImage> _imagesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string Category, string Name)> _assets = new();

    public bool SaveResult { get; set; } = true;
    public int SaveCount { get; private set; }
    public int? FailOnSaveNumber { get; set; }

    public string Name => "WorldMap synthetic source";
    public bool IsInitialized => true;
    public VersionInfo VersionInfo => null!;

    public void AddMap(int mapId, WzImage image)
    {
        string id = mapId.ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
        _imagesByPath[$"Map/Map{id[0]}/{id}.img"] = image;
    }

    public void AddImage(string category, string name, WzImage image)
    {
        _assets.Add((category, name));
        _imagesByPath[$"{category}/{name}"] = image;
    }

    public WzImage GetImage(string category, string imageName)
    {
        string key = $"{category}/{imageName}";
        return _imagesByPath.TryGetValue(key, out WzImage? image) ? image : null;
    }

    public WzImage GetImageByPath(string relativePath) =>
        _imagesByPath.TryGetValue(relativePath.Replace('\\', '/'), out WzImage? image) ? image : null;

    public IEnumerable<WzImage> GetImagesInCategory(string category) =>
        _imagesByPath.Where(pair => pair.Key.StartsWith(category + "/", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value).Distinct();

    public IEnumerable<WzImage> GetImagesInDirectory(string category, string subDirectory) =>
        GetImagesInCategory(category);

    public IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory) =>
        GetImagesInCategory(category).Select(image => image.Name);

    public bool ImageExists(string category, string imageName) =>
        _assets.Contains((category, imageName)) || _imagesByPath.ContainsKey($"{category}/{imageName}");

    public bool CategoryExists(string category) =>
        _imagesByPath.Keys.Any(key => key.StartsWith(category + "/", StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> GetCategories() => _imagesByPath.Keys
        .Select(key => key.Split('/')[0]).Distinct(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> GetSubdirectories(string category) => Array.Empty<string>();
    public WzDirectory GetDirectory(string category) => null!;
    public IEnumerable<WzDirectory> GetDirectories(string baseCategory) => Array.Empty<WzDirectory>();
    public void PreloadCategory(string category) { }
    public void ClearCache() { }
    public DataSourceStats GetStats() => new();
    public bool SaveImage(string category, WzImage image, string? relativePath = null)
    {
        SaveCount++;
        if (!SaveResult || (FailOnSaveNumber.HasValue && SaveCount == FailOnSaveNumber.Value))
            return false;
        string path = relativePath?.Replace('\\', '/') ?? image.Name;
        _imagesByPath[$"{category}/{path}"] = image;
        return true;
    }
    public void MarkImageUpdated(string category, WzImage image) { }
    public void Dispose() { }
}
