using System.ComponentModel;
using ModelContextProtocol.Server;
using HaMCP.Core;
using HaMCP.Server;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for file and data source operations
/// </summary>
[McpServerToolType]
public class FileTools : ToolBase
{
    public FileTools(WzSessionManager session) : base(session) { }

    [McpServerTool(Name = "init_data_source"), Description("Initialize an IMG filesystem data source from a directory path")]
    public Result<DataSourceData> InitDataSource(
        [Description("Path to the extracted IMG filesystem directory (containing manifest.json)")] string basePath)
    {
        return ExecuteRaw(() =>
        {
            var info = Session.InitDataSource(basePath);
            return new DataSourceData
            {
                Path = info.Path,
                Name = info.Name,
                Version = info.Version,
                DisplayName = info.DisplayName,
                Categories = info.Categories,
                IsPreBB = info.IsPreBB,
                Is64Bit = info.Is64Bit
            };
        });
    }

    [McpServerTool(Name = "scan_img_directories"), Description("Scan a directory for available IMG filesystem data sources")]
    public Result<ScanData> ScanImgDirectories(
        [Description("Directory path to scan")] string path,
        [Description("Scan subdirectories recursively (default: true)")] bool recursive = true)
    {
        return ExecuteRaw(() =>
        {
            var sources = Session.ScanForDataSources(path, recursive);
            return new ScanData
            {
                DataSources = sources.Select(s => new DataSourceEntry
                {
                    Path = s.Path,
                    Version = s.Version,
                    DisplayName = s.DisplayName,
                    SizeBytes = s.Size,
                    SizeFormatted = FormatSize(s.Size)
                }).ToList()
            };
        });
    }

    [McpServerTool(Name = "get_data_source_info"), Description("Get information about the currently loaded data source")]
    public Result<DataSourceInfoData> GetDataSourceInfo()
    {
        return Execute(() =>
        {
            var ds = Session.DataSource;
            var versionInfo = Session.VersionInfo;
            var stats = ds.GetStats();

            return new DataSourceInfoData
            {
                Name = ds.Name,
                Version = versionInfo?.Version,
                DisplayName = versionInfo?.DisplayName,
                SourceRegion = versionInfo?.SourceRegion,
                IsPreBB = versionInfo?.IsPreBB ?? false,
                Is64Bit = versionInfo?.Is64Bit ?? false,
                CategoryCount = stats.CategoryCount,
                ImageCount = stats.ImageCount,
                CachedImageCount = stats.CachedImageCount,
                CacheHitRatio = stats.CacheHitRatio
            };
        });
    }

    [McpServerTool(Name = "list_categories"), Description("List all available categories in the current data source")]
    public Result<CategoryListData> ListCategories()
    {
        return Execute(() =>
        {
            var ds = Session.DataSource;
            var categories = ds.GetCategories().ToList();

            return new CategoryListData
            {
                Categories = categories.Select(c => new CategoryEntry
                {
                    Name = c,
                    Exists = ds.CategoryExists(c)
                }).ToList()
            };
        });
    }

    [McpServerTool(Name = "list_images_in_category"), Description("List all .img files in a category")]
    public Result<ImageListData> ListImagesInCategory(
        [Description("Category name (e.g., 'Map', 'Mob', 'Npc')")] string category,
        [Description("Subdirectory within the category (optional)")] string? subdirectory = null)
    {
        return Execute(() =>
        {
            var ds = Session.DataSource;
            IEnumerable<string> imageNames;

            if (string.IsNullOrEmpty(subdirectory))
            {
                imageNames = ds.GetImagesInCategory(category).Select(i => i.Name);
            }
            else
            {
                imageNames = ds.GetImageNamesInDirectory(category, subdirectory);
            }

            return new ImageListData
            {
                Category = category,
                Subdirectory = subdirectory,
                Images = imageNames.Select(n => new ImageEntry
                {
                    Name = n,
                    FullPath = string.IsNullOrEmpty(subdirectory)
                        ? $"{category}/{n}"
                        : $"{category}/{subdirectory}/{n}"
                }).ToList()
            };
        });
    }

    [McpServerTool(Name = "get_cache_stats"), Description("Get cache statistics for the current data source")]
    public Result<CacheStatsData> GetCacheStats()
    {
        return Execute(() =>
        {
            var stats = Session.DataSource.GetStats();
            return new CacheStatsData
            {
                CachedImageCount = stats.CachedImageCount,
                CacheHitCount = stats.CacheHitCount,
                CacheMissCount = stats.CacheMissCount,
                CacheHitRatio = stats.CacheHitRatio,
                DiskReadCount = stats.DiskReadCount,
                MemoryUsageBytes = stats.MemoryUsageBytes,
                MemoryUsageFormatted = FormatSize(stats.MemoryUsageBytes)
            };
        });
    }

    [McpServerTool(Name = "clear_cache"), Description("Clear the loaded image cache")]
    public Result<ClearCacheData> ClearCache()
    {
        return Execute(() =>
        {
            var beforeStats = Session.DataSource.GetStats();
            Session.DataSource.ClearCache();
            var afterStats = Session.DataSource.GetStats();

            return new ClearCacheData
            {
                ClearedCount = beforeStats.CachedImageCount - afterStats.CachedImageCount
            };
        });
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

// Data types (no Success/Error - that's in Result<T>)

public class DataSourceData
{
    public string? Path { get; init; }
    public string? Name { get; init; }
    public string? Version { get; init; }
    public string? DisplayName { get; init; }
    public List<string>? Categories { get; init; }
    public bool IsPreBB { get; init; }
    public bool Is64Bit { get; init; }
}

public class ScanData
{
    public required List<DataSourceEntry> DataSources { get; init; }
}

public class DataSourceEntry
{
    public required string Path { get; init; }
    public string? Version { get; init; }
    public string? DisplayName { get; init; }
    public long SizeBytes { get; init; }
    public string? SizeFormatted { get; init; }
}

public class DataSourceInfoData
{
    public string? Name { get; init; }
    public string? Version { get; init; }
    public string? DisplayName { get; init; }
    public string? SourceRegion { get; init; }
    public bool IsPreBB { get; init; }
    public bool Is64Bit { get; init; }
    public int CategoryCount { get; init; }
    public int ImageCount { get; init; }
    public int CachedImageCount { get; init; }
    public double CacheHitRatio { get; init; }
}

public class CategoryListData
{
    public required List<CategoryEntry> Categories { get; init; }
}

public class CategoryEntry
{
    public required string Name { get; init; }
    public bool Exists { get; init; }
}

public class ImageListData
{
    public string? Category { get; init; }
    public string? Subdirectory { get; init; }
    public required List<ImageEntry> Images { get; init; }
}

public class ImageEntry
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
}

public class CacheStatsData
{
    public int CachedImageCount { get; init; }
    public int CacheHitCount { get; init; }
    public int CacheMissCount { get; init; }
    public double CacheHitRatio { get; init; }
    public int DiskReadCount { get; init; }
    public long MemoryUsageBytes { get; init; }
    public string? MemoryUsageFormatted { get; init; }
}

public class ClearCacheData
{
    public int ClearedCount { get; init; }
}
