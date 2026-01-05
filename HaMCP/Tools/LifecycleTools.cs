using System.ComponentModel;
using ModelContextProtocol.Server;
using HaMCP.Server;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for image lifecycle management (parsing/unparsing)
/// </summary>
[McpServerToolType]
public class LifecycleTools
{
    private readonly WzSessionManager _session;

    public LifecycleTools(WzSessionManager session)
    {
        _session = session;
    }

    [McpServerTool(Name = "parse_image"), Description("Parse an image (load properties into memory)")]
    public ParseResult ParseImage(
        [Description("Category name")] string category,
        [Description("Image name")] string image)
    {
        if (!_session.IsInitialized)
        {
            return new ParseResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var count = _session.ParseImage(category, image);
            return new ParseResult
            {
                Success = true,
                Category = category,
                Image = image,
                PropertyCount = count
            };
        }
        catch (Exception ex)
        {
            return new ParseResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "unparse_image"), Description("Unparse an image (free memory)")]
    public UnparseResult UnparseImage(
        [Description("Category name")] string category,
        [Description("Image name")] string image)
    {
        if (!_session.IsInitialized)
        {
            return new UnparseResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            _session.UnparseImage(category, image);
            return new UnparseResult
            {
                Success = true,
                Category = category,
                Image = image
            };
        }
        catch (Exception ex)
        {
            return new UnparseResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "is_image_parsed"), Description("Check if an image is currently parsed")]
    public IsParsedResult IsImageParsed(
        [Description("Category name")] string category,
        [Description("Image name")] string image)
    {
        if (!_session.IsInitialized)
        {
            return new IsParsedResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var parsed = _session.IsImageParsed(category, image);
            return new IsParsedResult
            {
                Success = true,
                Category = category,
                Image = image,
                IsParsed = parsed
            };
        }
        catch (Exception ex)
        {
            return new IsParsedResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "get_parsed_images"), Description("List all currently parsed images")]
    public ParsedImagesResult GetParsedImages()
    {
        if (!_session.IsInitialized)
        {
            return new ParsedImagesResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var images = _session.GetParsedImages();
            return new ParsedImagesResult
            {
                Success = true,
                Count = images.Count,
                Images = images
            };
        }
        catch (Exception ex)
        {
            return new ParsedImagesResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "preload_category"), Description("Preload all images in a category")]
    public PreloadResult PreloadCategory(
        [Description("Category name")] string category)
    {
        if (!_session.IsInitialized)
        {
            return new PreloadResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var ds = _session.DataSource;

            // Count images before
            int beforeCount = 0;
            foreach (var img in ds.GetImagesInCategory(category))
            {
                if (!img.Parsed) beforeCount++;
            }

            // Preload
            ds.PreloadCategory(category);

            // Count images after
            int loadedCount = 0;
            foreach (var img in ds.GetImagesInCategory(category))
            {
                if (img.Parsed) loadedCount++;
            }

            return new PreloadResult
            {
                Success = true,
                Category = category,
                LoadedCount = loadedCount,
                NewlyLoaded = beforeCount
            };
        }
        catch (Exception ex)
        {
            return new PreloadResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "unload_category"), Description("Unload all images in a category (free memory)")]
    public UnloadResult UnloadCategory(
        [Description("Category name")] string category)
    {
        if (!_session.IsInitialized)
        {
            return new UnloadResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var ds = _session.DataSource;
            int unloadedCount = 0;

            foreach (var img in ds.GetImagesInCategory(category))
            {
                if (img.Parsed)
                {
                    img.UnparseImage();
                    unloadedCount++;
                }
            }

            return new UnloadResult
            {
                Success = true,
                Category = category,
                UnloadedCount = unloadedCount
            };
        }
        catch (Exception ex)
        {
            return new UnloadResult { Success = false, Error = ex.Message };
        }
    }
}

// Result types

public class ParseResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public string? Image { get; set; }
    public int PropertyCount { get; set; }
}

public class UnparseResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public string? Image { get; set; }
}

public class IsParsedResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public string? Image { get; set; }
    public bool IsParsed { get; set; }
}

public class ParsedImagesResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int Count { get; set; }
    public List<ParsedImageInfo>? Images { get; set; }
}

public class PreloadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public int LoadedCount { get; set; }
    public int NewlyLoaded { get; set; }
}

public class UnloadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public int UnloadedCount { get; set; }
}
