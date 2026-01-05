using System.ComponentModel;
using ModelContextProtocol.Server;
using HaMCP.Server;
using HaMCP.Utils;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for analysis and statistics
/// </summary>
[McpServerToolType]
public class AnalysisTools
{
    private readonly WzSessionManager _session;

    public AnalysisTools(WzSessionManager session)
    {
        _session = session;
    }

    [McpServerTool(Name = "get_statistics"), Description("Get overall statistics for the data source")]
    public StatisticsResult GetStatistics()
    {
        if (!_session.IsInitialized)
        {
            return new StatisticsResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var ds = _session.DataSource;
            var stats = ds.GetStats();
            var categories = ds.GetCategories().ToList();

            var categoryStats = new List<CategoryStats>();
            int totalImages = 0;
            int totalParsed = 0;

            foreach (var cat in categories)
            {
                var images = ds.GetImagesInCategory(cat).ToList();
                var parsedCount = images.Count(img => img.Parsed);
                totalImages += images.Count;
                totalParsed += parsedCount;

                categoryStats.Add(new CategoryStats
                {
                    Name = cat,
                    ImageCount = images.Count,
                    ParsedCount = parsedCount
                });
            }

            return new StatisticsResult
            {
                Success = true,
                CategoryCount = categories.Count,
                TotalImageCount = totalImages,
                ParsedImageCount = totalParsed,
                CacheHitCount = stats.CacheHitCount,
                CacheMissCount = stats.CacheMissCount,
                CacheHitRatio = stats.CacheHitRatio,
                DiskReadCount = stats.DiskReadCount,
                MemoryUsageBytes = stats.MemoryUsageBytes,
                Categories = categoryStats
            };
        }
        catch (Exception ex)
        {
            return new StatisticsResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "get_category_summary"), Description("Get a summary of a specific category")]
    public CategorySummaryResult GetCategorySummary(
        [Description("Category name")] string category)
    {
        if (!_session.IsInitialized)
        {
            return new CategorySummaryResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var ds = _session.DataSource;
            var images = ds.GetImagesInCategory(category).ToList();
            var subdirs = ds.GetSubdirectories(category).ToList();

            int canvasCount = 0;
            int soundCount = 0;
            int propertyCount = 0;

            var imageSummaries = new List<ImageSummary>();

            foreach (var img in images.Take(100)) // Limit to avoid long operations
            {
                var wasParsed = img.Parsed;
                if (!img.Parsed) img.ParseImage();

                var propCount = CountPropertiesRecursive(img, 0, 5);
                var canvases = CountCanvasesRecursive(img, 0, 10);
                var sounds = CountSoundsRecursive(img, 0, 10);

                propertyCount += propCount;
                canvasCount += canvases;
                soundCount += sounds;

                imageSummaries.Add(new ImageSummary
                {
                    Name = img.Name,
                    PropertyCount = propCount,
                    CanvasCount = canvases,
                    SoundCount = sounds
                });

                if (!wasParsed) img.UnparseImage();
            }

            return new CategorySummaryResult
            {
                Success = true,
                Category = category,
                ImageCount = images.Count,
                SubdirectoryCount = subdirs.Count,
                Subdirectories = subdirs,
                TotalPropertyCount = propertyCount,
                TotalCanvasCount = canvasCount,
                TotalSoundCount = soundCount,
                Images = imageSummaries,
                Truncated = images.Count > 100
            };
        }
        catch (Exception ex)
        {
            return new CategorySummaryResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "find_broken_uols"), Description("Find UOL references that don't resolve to valid targets")]
    public BrokenUolsResult FindBrokenUols(
        [Description("Category to search (optional - searches all if not specified)")] string? category = null,
        [Description("Specific image to search (optional)")] string? image = null,
        [Description("Maximum results to return")] int maxResults = 100)
    {
        if (!_session.IsInitialized)
        {
            return new BrokenUolsResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var brokenUols = new List<BrokenUol>();
            var ds = _session.DataSource;

            IEnumerable<string> categories = string.IsNullOrEmpty(category)
                ? ds.GetCategories()
                : new[] { category };

            foreach (var cat in categories)
            {
                IEnumerable<WzImage> images;

                if (!string.IsNullOrEmpty(image))
                {
                    var img = ds.GetImage(cat, image);
                    images = img != null ? new[] { img } : Enumerable.Empty<WzImage>();
                }
                else
                {
                    images = ds.GetImagesInCategory(cat);
                }

                foreach (var img in images)
                {
                    var wasParsed = img.Parsed;
                    if (!img.Parsed) img.ParseImage();

                    FindBrokenUolsRecursive(img, cat, img.Name, "", brokenUols, maxResults);

                    if (!wasParsed) img.UnparseImage();

                    if (brokenUols.Count >= maxResults) break;
                }

                if (brokenUols.Count >= maxResults) break;
            }

            return new BrokenUolsResult
            {
                Success = true,
                Count = brokenUols.Count,
                Truncated = brokenUols.Count >= maxResults,
                BrokenUols = brokenUols
            };
        }
        catch (Exception ex)
        {
            return new BrokenUolsResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "compare_properties"), Description("Compare two property trees and find differences")]
    public CompareResult CompareProperties(
        [Description("First category")] string category1,
        [Description("First image")] string image1,
        [Description("First path")] string path1,
        [Description("Second category")] string category2,
        [Description("Second image")] string image2,
        [Description("Second path")] string path2,
        [Description("Maximum depth to compare")] int maxDepth = 5)
    {
        if (!_session.IsInitialized)
        {
            return new CompareResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img1 = _session.GetImage(category1, image1);
            var img2 = _session.GetImage(category2, image2);

            var prop1 = img1.GetFromPath(path1);
            var prop2 = img2.GetFromPath(path2);

            if (prop1 == null)
            {
                return new CompareResult { Success = false, Error = $"Property not found: {path1}" };
            }
            if (prop2 == null)
            {
                return new CompareResult { Success = false, Error = $"Property not found: {path2}" };
            }

            var differences = new List<PropertyDifference>();
            CompareRecursive(prop1, prop2, "", differences, 0, maxDepth);

            return new CompareResult
            {
                Success = true,
                Path1 = $"{category1}/{image1}/{path1}",
                Path2 = $"{category2}/{image2}/{path2}",
                DifferenceCount = differences.Count,
                Differences = differences
            };
        }
        catch (Exception ex)
        {
            return new CompareResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "get_version_info"), Description("Get version information for the current data source")]
    public VersionInfoResult GetVersionInfo()
    {
        if (!_session.IsInitialized)
        {
            return new VersionInfoResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var versionInfo = _session.VersionInfo;
            var ds = _session.DataSource;

            return new VersionInfoResult
            {
                Success = true,
                Name = ds.Name,
                Version = versionInfo?.Version ?? "unknown",
                DisplayName = versionInfo?.DisplayName,
                SourceRegion = versionInfo?.SourceRegion,
                IsPreBB = versionInfo?.IsPreBB ?? false,
                Is64Bit = versionInfo?.Is64Bit ?? false,
                Features = null // Reserved for future use
            };
        }
        catch (Exception ex)
        {
            return new VersionInfoResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "validate_image"), Description("Validate an image structure for common issues")]
    public ValidationResult ValidateImage(
        [Description("Category name")] string category,
        [Description("Image name")] string image)
    {
        if (!_session.IsInitialized)
        {
            return new ValidationResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var issues = new List<ValidationIssue>();
            var stats = new ValidationStats();

            ValidateRecursive(img, "", issues, stats, 0, 20);

            return new ValidationResult
            {
                Success = true,
                Category = category,
                Image = image,
                IsValid = issues.Count == 0,
                IssueCount = issues.Count,
                Stats = stats,
                Issues = issues
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult { Success = false, Error = ex.Message };
        }
    }

    private int CountPropertiesRecursive(WzObject obj, int depth, int maxDepth)
    {
        if (depth > maxDepth) return 0;

        int count = 0;
        IEnumerable<WzImageProperty>? children = null;

        if (obj is WzImage img)
        {
            children = img.WzProperties;
        }
        else if (obj is WzImageProperty prop)
        {
            count = 1;
            children = prop.WzProperties;
        }

        if (children != null)
        {
            foreach (var child in children)
            {
                count += CountPropertiesRecursive(child, depth + 1, maxDepth);
            }
        }

        return count;
    }

    private int CountCanvasesRecursive(WzObject obj, int depth, int maxDepth)
    {
        if (depth > maxDepth) return 0;

        int count = 0;
        IEnumerable<WzImageProperty>? children = null;

        if (obj is WzImage img)
        {
            children = img.WzProperties;
        }
        else if (obj is WzImageProperty prop)
        {
            if (prop is WzCanvasProperty) count = 1;
            children = prop.WzProperties;
        }

        if (children != null)
        {
            foreach (var child in children)
            {
                count += CountCanvasesRecursive(child, depth + 1, maxDepth);
            }
        }

        return count;
    }

    private int CountSoundsRecursive(WzObject obj, int depth, int maxDepth)
    {
        if (depth > maxDepth) return 0;

        int count = 0;
        IEnumerable<WzImageProperty>? children = null;

        if (obj is WzImage img)
        {
            children = img.WzProperties;
        }
        else if (obj is WzImageProperty prop)
        {
            if (prop is WzBinaryProperty) count = 1;
            children = prop.WzProperties;
        }

        if (children != null)
        {
            foreach (var child in children)
            {
                count += CountSoundsRecursive(child, depth + 1, maxDepth);
            }
        }

        return count;
    }

    private void FindBrokenUolsRecursive(WzObject obj, string category, string imageName, string path,
        List<BrokenUol> results, int maxResults)
    {
        if (results.Count >= maxResults) return;

        IEnumerable<WzImageProperty>? children = null;

        if (obj is WzImage img)
        {
            children = img.WzProperties;
        }
        else if (obj is WzImageProperty prop)
        {
            if (prop is WzUOLProperty uol)
            {
                try
                {
                    var target = uol.LinkValue;
                    if (target == null)
                    {
                        results.Add(new BrokenUol
                        {
                            Category = category,
                            Image = imageName,
                            Path = path,
                            UolValue = uol.Value,
                            Reason = "Target not found"
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new BrokenUol
                    {
                        Category = category,
                        Image = imageName,
                        Path = path,
                        UolValue = uol.Value,
                        Reason = ex.Message
                    });
                }
            }
            children = prop.WzProperties;
        }

        if (children != null)
        {
            foreach (var child in children)
            {
                if (results.Count >= maxResults) return;
                var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}/{child.Name}";
                FindBrokenUolsRecursive(child, category, imageName, childPath, results, maxResults);
            }
        }
    }

    private void CompareRecursive(WzObject obj1, WzObject obj2, string path,
        List<PropertyDifference> differences, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        // Compare types
        var type1 = obj1 is WzImageProperty p1 ? p1.PropertyType.ToString() : obj1.GetType().Name;
        var type2 = obj2 is WzImageProperty p2 ? p2.PropertyType.ToString() : obj2.GetType().Name;

        if (type1 != type2)
        {
            differences.Add(new PropertyDifference
            {
                Path = path,
                Type = "TypeMismatch",
                Value1 = type1,
                Value2 = type2
            });
            return;
        }

        // Compare values
        if (obj1 is WzImageProperty prop1 && obj2 is WzImageProperty prop2)
        {
            var val1 = WzDataConverter.GetPropertyValue(prop1);
            var val2 = WzDataConverter.GetPropertyValue(prop2);

            var str1 = val1?.ToString() ?? "null";
            var str2 = val2?.ToString() ?? "null";

            if (str1 != str2)
            {
                differences.Add(new PropertyDifference
                {
                    Path = path,
                    Type = "ValueDifference",
                    Value1 = str1,
                    Value2 = str2
                });
            }
        }

        // Compare children
        var children1 = GetChildren(obj1).ToDictionary(c => c.Name);
        var children2 = GetChildren(obj2).ToDictionary(c => c.Name);

        foreach (var name in children1.Keys.Union(children2.Keys))
        {
            var childPath = string.IsNullOrEmpty(path) ? name : $"{path}/{name}";

            if (!children1.ContainsKey(name))
            {
                differences.Add(new PropertyDifference
                {
                    Path = childPath,
                    Type = "OnlyInSecond",
                    Value2 = children2[name] is WzImageProperty cp ? cp.PropertyType.ToString() : "?"
                });
            }
            else if (!children2.ContainsKey(name))
            {
                differences.Add(new PropertyDifference
                {
                    Path = childPath,
                    Type = "OnlyInFirst",
                    Value1 = children1[name] is WzImageProperty cp ? cp.PropertyType.ToString() : "?"
                });
            }
            else
            {
                CompareRecursive(children1[name], children2[name], childPath, differences, depth + 1, maxDepth);
            }
        }
    }

    private void ValidateRecursive(WzObject obj, string path, List<ValidationIssue> issues,
        ValidationStats stats, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        IEnumerable<WzImageProperty>? children = null;

        if (obj is WzImage img)
        {
            stats.TotalProperties++;
            children = img.WzProperties;
        }
        else if (obj is WzImageProperty prop)
        {
            stats.TotalProperties++;

            switch (prop)
            {
                case WzCanvasProperty canvas:
                    stats.CanvasCount++;
                    try
                    {
                        var bitmap = canvas.GetLinkedWzCanvasBitmap();
                        if (bitmap == null)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Path = path,
                                Severity = "Warning",
                                Message = "Canvas has null bitmap"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Path = path,
                            Severity = "Error",
                            Message = $"Failed to load canvas: {ex.Message}"
                        });
                    }
                    break;

                case WzBinaryProperty sound:
                    stats.SoundCount++;
                    if (sound.Length == 0)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Path = path,
                            Severity = "Warning",
                            Message = "Sound has zero length"
                        });
                    }
                    break;

                case WzUOLProperty uol:
                    stats.UolCount++;
                    try
                    {
                        var target = uol.LinkValue;
                        if (target == null)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Path = path,
                                Severity = "Error",
                                Message = $"Broken UOL reference: {uol.Value}"
                            });
                        }
                    }
                    catch
                    {
                        issues.Add(new ValidationIssue
                        {
                            Path = path,
                            Severity = "Error",
                            Message = $"Failed to resolve UOL: {uol.Value}"
                        });
                    }
                    break;

                case WzStringProperty str:
                    stats.StringCount++;
                    break;

                case WzIntProperty:
                case WzShortProperty:
                case WzLongProperty:
                    stats.IntCount++;
                    break;

                case WzFloatProperty:
                case WzDoubleProperty:
                    stats.FloatCount++;
                    break;

                case WzVectorProperty:
                    stats.VectorCount++;
                    break;

                case WzSubProperty:
                    stats.SubPropertyCount++;
                    break;
            }

            children = prop.WzProperties;
        }

        if (children != null)
        {
            foreach (var child in children)
            {
                var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}/{child.Name}";
                ValidateRecursive(child, childPath, issues, stats, depth + 1, maxDepth);
            }
        }
    }

    private static IEnumerable<WzObject> GetChildren(WzObject obj)
    {
        if (obj is WzImage img)
        {
            return img.WzProperties?.Cast<WzObject>() ?? Enumerable.Empty<WzObject>();
        }
        else if (obj is WzImageProperty prop && prop.WzProperties != null)
        {
            return prop.WzProperties.Cast<WzObject>();
        }
        return Enumerable.Empty<WzObject>();
    }
}

// Result types

public class StatisticsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int CategoryCount { get; set; }
    public int TotalImageCount { get; set; }
    public int ParsedImageCount { get; set; }
    public int CacheHitCount { get; set; }
    public int CacheMissCount { get; set; }
    public double CacheHitRatio { get; set; }
    public int DiskReadCount { get; set; }
    public long MemoryUsageBytes { get; set; }
    public List<CategoryStats>? Categories { get; set; }
}

public class CategoryStats
{
    public required string Name { get; set; }
    public int ImageCount { get; set; }
    public int ParsedCount { get; set; }
}

public class CategorySummaryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public int ImageCount { get; set; }
    public int SubdirectoryCount { get; set; }
    public List<string>? Subdirectories { get; set; }
    public int TotalPropertyCount { get; set; }
    public int TotalCanvasCount { get; set; }
    public int TotalSoundCount { get; set; }
    public List<ImageSummary>? Images { get; set; }
    public bool Truncated { get; set; }
}

public class ImageSummary
{
    public required string Name { get; set; }
    public int PropertyCount { get; set; }
    public int CanvasCount { get; set; }
    public int SoundCount { get; set; }
}

public class BrokenUolsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int Count { get; set; }
    public bool Truncated { get; set; }
    public List<BrokenUol>? BrokenUols { get; set; }
}

public class BrokenUol
{
    public required string Category { get; set; }
    public required string Image { get; set; }
    public required string Path { get; set; }
    public required string UolValue { get; set; }
    public required string Reason { get; set; }
}

public class CompareResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Path1 { get; set; }
    public string? Path2 { get; set; }
    public int DifferenceCount { get; set; }
    public List<PropertyDifference>? Differences { get; set; }
}

public class PropertyDifference
{
    public required string Path { get; set; }
    public required string Type { get; set; }
    public string? Value1 { get; set; }
    public string? Value2 { get; set; }
}

public class VersionInfoResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? DisplayName { get; set; }
    public string? SourceRegion { get; set; }
    public bool IsPreBB { get; set; }
    public bool Is64Bit { get; set; }
    public List<string>? Features { get; set; }
}

public class ValidationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public string? Image { get; set; }
    public bool IsValid { get; set; }
    public int IssueCount { get; set; }
    public ValidationStats? Stats { get; set; }
    public List<ValidationIssue>? Issues { get; set; }
}

public class ValidationStats
{
    public int TotalProperties { get; set; }
    public int CanvasCount { get; set; }
    public int SoundCount { get; set; }
    public int UolCount { get; set; }
    public int StringCount { get; set; }
    public int IntCount { get; set; }
    public int FloatCount { get; set; }
    public int VectorCount { get; set; }
    public int SubPropertyCount { get; set; }
}

public class ValidationIssue
{
    public required string Path { get; set; }
    public required string Severity { get; set; }
    public required string Message { get; set; }
}
