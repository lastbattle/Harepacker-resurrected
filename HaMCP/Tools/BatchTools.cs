using System.ComponentModel;
using ModelContextProtocol.Server;
using HaMCP.Server;
using MapleLib.Img;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for batch operations and WZ conversion
/// </summary>
[McpServerToolType]
public class BatchTools
{
    private readonly WzSessionManager _session;

    public BatchTools(WzSessionManager session)
    {
        _session = session;
    }

    [McpServerTool(Name = "extract_to_img"), Description("Extract WZ files to IMG filesystem format")]
    public ExtractResult ExtractToImg(
        [Description("Path to WZ file or directory containing WZ files")] string wzPath,
        [Description("Output directory for IMG filesystem")] string outputDir,
        [Description("Version key for WZ decryption (empty for auto-detect)")] string? versionKey = null,
        [Description("Create version manifest")] bool createManifest = true)
    {
        try
        {
            if (!Directory.Exists(wzPath) && !File.Exists(wzPath))
            {
                return new ExtractResult { Success = false, Error = $"Path not found: {wzPath}" };
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var extractor = new WzExtractor();
            var result = extractor.ExtractToImgFileSystem(wzPath, outputDir, versionKey, createManifest);

            return new ExtractResult
            {
                Success = result.Success,
                Error = result.ErrorMessage,
                OutputDirectory = outputDir,
                CategoriesExtracted = result.CategoriesExtracted,
                ImagesExtracted = result.ImagesExtracted,
                Errors = result.Errors?.ToList()
            };
        }
        catch (Exception ex)
        {
            return new ExtractResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "pack_to_wz"), Description("Pack IMG filesystem back to WZ files")]
    public PackResult PackToWz(
        [Description("Path to IMG filesystem directory")] string imgPath,
        [Description("Output directory for WZ files")] string outputDir,
        [Description("WZ version to create")] int wzVersion = 83,
        [Description("Category to pack (optional - packs all if not specified)")] string? category = null)
    {
        try
        {
            if (!Directory.Exists(imgPath))
            {
                return new PackResult { Success = false, Error = $"Directory not found: {imgPath}" };
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var packer = new WzPacker();
            var result = packer.PackToWz(imgPath, outputDir, wzVersion, category);

            return new PackResult
            {
                Success = result.Success,
                Error = result.ErrorMessage,
                OutputDirectory = outputDir,
                FilesCreated = result.FilesCreated,
                TotalSize = result.TotalSize,
                Errors = result.Errors?.ToList()
            };
        }
        catch (Exception ex)
        {
            return new PackResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "batch_export_images"), Description("Export all images from multiple categories")]
    public BatchExportImagesResult BatchExportImages(
        [Description("Categories to export (comma-separated, or 'all')")] string categories,
        [Description("Output directory")] string outputDir,
        [Description("Output format (png, jpg)")] string format = "png",
        [Description("Maximum images to export")] int maxImages = 1000)
    {
        if (!_session.IsInitialized)
        {
            return new BatchExportImagesResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var ds = _session.DataSource;
            IEnumerable<string> categoryList;

            if (categories.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                categoryList = ds.GetCategories();
            }
            else
            {
                categoryList = categories.Split(',').Select(c => c.Trim());
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var exported = new List<string>();
            var failed = new List<string>();
            int totalExported = 0;

            foreach (var category in categoryList)
            {
                if (totalExported >= maxImages) break;

                var categoryDir = Path.Combine(outputDir, category);
                if (!Directory.Exists(categoryDir))
                {
                    Directory.CreateDirectory(categoryDir);
                }

                foreach (var img in ds.GetImagesInCategory(category))
                {
                    if (totalExported >= maxImages) break;

                    try
                    {
                        var wasParsed = img.Parsed;
                        if (!img.Parsed) img.ParseImage();

                        var imageDir = Path.Combine(categoryDir, Path.GetFileNameWithoutExtension(img.Name));
                        ExportCanvasesFromImage(img, imageDir, format, exported, failed, ref totalExported, maxImages);

                        if (!wasParsed) img.UnparseImage();
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{category}/{img.Name}: {ex.Message}");
                    }
                }
            }

            return new BatchExportImagesResult
            {
                Success = true,
                OutputDirectory = outputDir,
                ExportedCount = exported.Count,
                FailedCount = failed.Count,
                Truncated = totalExported >= maxImages,
                SampleExported = exported.Take(20).ToList(),
                Failed = failed.Take(20).ToList()
            };
        }
        catch (Exception ex)
        {
            return new BatchExportImagesResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "batch_search"), Description("Search across multiple categories")]
    public BatchSearchResult BatchSearch(
        [Description("Search pattern (supports wildcards)")] string pattern,
        [Description("Categories to search (comma-separated, or 'all')")] string categories = "all",
        [Description("Search type: name, value, or both")] string searchType = "name",
        [Description("Maximum results")] int maxResults = 100)
    {
        if (!_session.IsInitialized)
        {
            return new BatchSearchResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var ds = _session.DataSource;
            IEnumerable<string> categoryList;

            if (categories.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                categoryList = ds.GetCategories();
            }
            else
            {
                categoryList = categories.Split(',').Select(c => c.Trim());
            }

            var results = new List<BatchSearchMatch>();
            var regex = WildcardToRegex(pattern);

            foreach (var category in categoryList)
            {
                if (results.Count >= maxResults) break;

                foreach (var img in ds.GetImagesInCategory(category))
                {
                    if (results.Count >= maxResults) break;

                    try
                    {
                        var wasParsed = img.Parsed;
                        if (!img.Parsed) img.ParseImage();

                        SearchInImage(img, category, img.Name, regex, searchType, results, maxResults);

                        if (!wasParsed) img.UnparseImage();
                    }
                    catch
                    {
                        // Skip images that can't be parsed
                    }
                }
            }

            return new BatchSearchResult
            {
                Success = true,
                Pattern = pattern,
                SearchType = searchType,
                ResultCount = results.Count,
                Truncated = results.Count >= maxResults,
                Results = results
            };
        }
        catch (Exception ex)
        {
            return new BatchSearchResult { Success = false, Error = ex.Message };
        }
    }

    private void ExportCanvasesFromImage(MapleLib.WzLib.WzImage img, string outputDir,
        string format, List<string> exported, List<string> failed, ref int count, int max)
    {
        if (count >= max) return;

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        foreach (var prop in img.WzProperties ?? Enumerable.Empty<MapleLib.WzLib.WzImageProperty>())
        {
            ExportCanvasesRecursive(prop, "", outputDir, format, exported, failed, ref count, max);
        }
    }

    private void ExportCanvasesRecursive(MapleLib.WzLib.WzImageProperty prop, string path,
        string outputDir, string format, List<string> exported, List<string> failed, ref int count, int max)
    {
        if (count >= max) return;

        if (prop is MapleLib.WzLib.WzProperties.WzCanvasProperty canvas)
        {
            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap != null)
                {
                    var fileName = string.IsNullOrEmpty(path) ? prop.Name : path.Replace("/", "_");
                    var filePath = Path.Combine(outputDir, $"{fileName}.{format}");

                    var imageFormat = format.ToLowerInvariant() == "jpg"
                        ? System.Drawing.Imaging.ImageFormat.Jpeg
                        : System.Drawing.Imaging.ImageFormat.Png;

                    bitmap.Save(filePath, imageFormat);
                    exported.Add(filePath);
                    count++;
                }
            }
            catch (Exception ex)
            {
                failed.Add($"{path}: {ex.Message}");
            }
        }

        var childPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}/{prop.Name}";
        foreach (var child in prop.WzProperties ?? Enumerable.Empty<MapleLib.WzLib.WzImageProperty>())
        {
            ExportCanvasesRecursive(child, childPath, outputDir, format, exported, failed, ref count, max);
        }
    }

    private void SearchInImage(MapleLib.WzLib.WzImage img, string category, string imageName,
        System.Text.RegularExpressions.Regex regex, string searchType, List<BatchSearchMatch> results, int max)
    {
        foreach (var prop in img.WzProperties ?? Enumerable.Empty<MapleLib.WzLib.WzImageProperty>())
        {
            SearchInProperty(prop, category, imageName, "", regex, searchType, results, max);
        }
    }

    private void SearchInProperty(MapleLib.WzLib.WzImageProperty prop, string category, string imageName,
        string path, System.Text.RegularExpressions.Regex regex, string searchType, List<BatchSearchMatch> results, int max)
    {
        if (results.Count >= max) return;

        var currentPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}/{prop.Name}";
        var matched = false;

        // Check name
        if (searchType == "name" || searchType == "both")
        {
            if (regex.IsMatch(prop.Name))
            {
                matched = true;
            }
        }

        // Check value
        if (!matched && (searchType == "value" || searchType == "both"))
        {
            var valueStr = GetPropertyValueString(prop);
            if (valueStr != null && regex.IsMatch(valueStr))
            {
                matched = true;
            }
        }

        if (matched)
        {
            results.Add(new BatchSearchMatch
            {
                Category = category,
                Image = imageName,
                Path = currentPath,
                Name = prop.Name,
                Type = prop.PropertyType.ToString(),
                Value = GetPropertyValueString(prop)
            });
        }

        // Recurse
        foreach (var child in prop.WzProperties ?? Enumerable.Empty<MapleLib.WzLib.WzImageProperty>())
        {
            SearchInProperty(child, category, imageName, currentPath, regex, searchType, results, max);
        }
    }

    private static string? GetPropertyValueString(MapleLib.WzLib.WzImageProperty prop)
    {
        return prop switch
        {
            MapleLib.WzLib.WzProperties.WzStringProperty s => s.Value,
            MapleLib.WzLib.WzProperties.WzIntProperty i => i.Value.ToString(),
            MapleLib.WzLib.WzProperties.WzShortProperty sh => sh.Value.ToString(),
            MapleLib.WzLib.WzProperties.WzLongProperty l => l.Value.ToString(),
            MapleLib.WzLib.WzProperties.WzFloatProperty f => f.Value.ToString(),
            MapleLib.WzLib.WzProperties.WzDoubleProperty d => d.Value.ToString(),
            MapleLib.WzLib.WzProperties.WzUOLProperty u => u.Value,
            _ => null
        };
    }

    private static System.Text.RegularExpressions.Regex WildcardToRegex(string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return new System.Text.RegularExpressions.Regex(regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}

// Placeholder classes for WZ extraction/packing (would need actual implementation in MapleLib)
public class WzExtractor
{
    public ExtractionResult ExtractToImgFileSystem(string wzPath, string outputDir, string? versionKey, bool createManifest)
    {
        // This would need to be implemented in MapleLib
        // For now, return a not-implemented result
        return new ExtractionResult
        {
            Success = false,
            ErrorMessage = "WZ extraction not yet implemented. Use external tools or HaRepacker GUI."
        };
    }
}

public class WzPacker
{
    public PackingResult PackToWz(string imgPath, string outputDir, int wzVersion, string? category)
    {
        // This would need to be implemented in MapleLib
        // For now, return a not-implemented result
        return new PackingResult
        {
            Success = false,
            ErrorMessage = "WZ packing not yet implemented. Use external tools or HaRepacker GUI."
        };
    }
}

public class ExtractionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int CategoriesExtracted { get; set; }
    public int ImagesExtracted { get; set; }
    public List<string>? Errors { get; set; }
}

public class PackingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int FilesCreated { get; set; }
    public long TotalSize { get; set; }
    public List<string>? Errors { get; set; }
}

// Result types

public class ExtractResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputDirectory { get; set; }
    public int CategoriesExtracted { get; set; }
    public int ImagesExtracted { get; set; }
    public List<string>? Errors { get; set; }
}

public class PackResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputDirectory { get; set; }
    public int FilesCreated { get; set; }
    public long TotalSize { get; set; }
    public List<string>? Errors { get; set; }
}

public class BatchExportImagesResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputDirectory { get; set; }
    public int ExportedCount { get; set; }
    public int FailedCount { get; set; }
    public bool Truncated { get; set; }
    public List<string>? SampleExported { get; set; }
    public List<string>? Failed { get; set; }
}

public class BatchSearchResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Pattern { get; set; }
    public string? SearchType { get; set; }
    public int ResultCount { get; set; }
    public bool Truncated { get; set; }
    public List<BatchSearchMatch>? Results { get; set; }
}

public class BatchSearchMatch
{
    public required string Category { get; set; }
    public required string Image { get; set; }
    public required string Path { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? Value { get; set; }
}
