using MapleLib.Img;
using MapleLib.WzLib;
using System.Text.Json;

namespace HaMCP.Server;

/// <summary>
/// Manages the loaded data source state for the MCP server
/// </summary>
public class WzSessionManager : IDisposable
{
    private IDataSource? _dataSource;
    private readonly Dictionary<string, WzImage> _parsedImages = new();
    private bool _disposed;

    /// <summary>
    /// Gets whether a data source is currently initialized
    /// </summary>
    public bool IsInitialized => _dataSource?.IsInitialized ?? false;

    /// <summary>
    /// Gets the current data source (throws if not initialized)
    /// </summary>
    public IDataSource DataSource => _dataSource
        ?? throw new InvalidOperationException("Data source not initialized. Call init_data_source first.");

    /// <summary>
    /// Gets the version info for the current data source
    /// </summary>
    public VersionInfo? VersionInfo => _dataSource?.VersionInfo;

    /// <summary>
    /// Initializes a new IMG filesystem data source
    /// </summary>
    public DataSourceInfo InitDataSource(string basePath)
    {
        // Dispose existing data source if any
        _dataSource?.Dispose();
        _parsedImages.Clear();

        // Validate path exists
        if (!Directory.Exists(basePath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {basePath}");
        }

        // Create new data source
        _dataSource = new ImgFileSystemDataSource(basePath);

        var versionInfo = _dataSource.VersionInfo;
        var categories = _dataSource.GetCategories().ToList();

        return new DataSourceInfo
        {
            Path = basePath,
            Name = _dataSource.Name,
            Version = versionInfo?.Version ?? "unknown",
            DisplayName = versionInfo?.DisplayName ?? basePath,
            Categories = categories,
            IsPreBB = versionInfo?.IsPreBB ?? false,
            Is64Bit = versionInfo?.Is64Bit ?? false
        };
    }

    /// <summary>
    /// Scans a directory for IMG filesystem data sources
    /// </summary>
    public List<AvailableDataSource> ScanForDataSources(string path, bool recursive = true)
    {
        var found = new List<AvailableDataSource>();

        if (!Directory.Exists(path))
        {
            return found;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var dir in Directory.EnumerateDirectories(path, "*", searchOption))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    found.Add(new AvailableDataSource
                    {
                        Path = dir,
                        Version = manifest?.Version,
                        DisplayName = manifest?.DisplayName ?? Path.GetFileName(dir),
                        Size = GetDirectorySize(dir)
                    });
                }
                catch
                {
                    // Skip directories with invalid manifests
                }
            }
        }

        // Also check the root path
        var rootManifest = Path.Combine(path, "manifest.json");
        if (File.Exists(rootManifest))
        {
            try
            {
                var json = File.ReadAllText(rootManifest);
                var manifest = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                found.Insert(0, new AvailableDataSource
                {
                    Path = path,
                    Version = manifest?.Version,
                    DisplayName = manifest?.DisplayName ?? Path.GetFileName(path),
                    Size = GetDirectorySize(path)
                });
            }
            catch { }
        }

        return found;
    }

    /// <summary>
    /// Gets an image from the current data source, parsing it if necessary
    /// </summary>
    public WzImage GetImage(string category, string imageName)
    {
        var img = DataSource.GetImage(category, imageName);
        if (img == null)
        {
            throw new FileNotFoundException($"Image not found: {category}/{imageName}");
        }

        // Ensure the image is parsed
        if (!img.Parsed)
        {
            img.ParseImage();
        }

        return img;
    }

    /// <summary>
    /// Checks if an image is currently parsed
    /// </summary>
    public bool IsImageParsed(string category, string imageName)
    {
        var img = DataSource.GetImage(category, imageName);
        return img?.Parsed ?? false;
    }

    /// <summary>
    /// Parses an image (loads its properties into memory)
    /// </summary>
    public int ParseImage(string category, string imageName)
    {
        var img = GetImage(category, imageName);
        if (!img.Parsed)
        {
            img.ParseImage();
        }
        return img.WzProperties?.Count ?? 0;
    }

    /// <summary>
    /// Unparses an image (frees memory)
    /// </summary>
    public void UnparseImage(string category, string imageName)
    {
        var img = DataSource.GetImage(category, imageName);
        if (img?.Parsed == true)
        {
            img.UnparseImage();
        }
    }

    /// <summary>
    /// Gets a list of all parsed images
    /// </summary>
    public List<ParsedImageInfo> GetParsedImages()
    {
        var result = new List<ParsedImageInfo>();

        foreach (var category in DataSource.GetCategories())
        {
            foreach (var img in DataSource.GetImagesInCategory(category))
            {
                if (img.Parsed)
                {
                    result.Add(new ParsedImageInfo
                    {
                        Category = category,
                        Name = img.Name,
                        PropertyCount = img.WzProperties?.Count ?? 0
                    });
                }
            }
        }

        return result;
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _dataSource?.Dispose();
        _parsedImages.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Information about the current data source
/// </summary>
public class DataSourceInfo
{
    public required string Path { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string DisplayName { get; set; }
    public required List<string> Categories { get; set; }
    public bool IsPreBB { get; set; }
    public bool Is64Bit { get; set; }
}

/// <summary>
/// Information about an available data source found during scanning
/// </summary>
public class AvailableDataSource
{
    public required string Path { get; set; }
    public string? Version { get; set; }
    public string? DisplayName { get; set; }
    public long Size { get; set; }
}

/// <summary>
/// Information about a parsed image
/// </summary>
public class ParsedImageInfo
{
    public required string Category { get; set; }
    public required string Name { get; set; }
    public int PropertyCount { get; set; }
}
