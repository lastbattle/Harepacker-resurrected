using System.ComponentModel;
using System.Text.Json;
using System.Xml.Linq;
using ModelContextProtocol.Server;
using HaMCP.Server;
using HaMCP.Utils;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for exporting WZ data to various formats
/// </summary>
[McpServerToolType]
public class ExportTools
{
    private readonly WzSessionManager _session;

    public ExportTools(WzSessionManager session)
    {
        _session = session;
    }

    [McpServerTool(Name = "export_to_json"), Description("Export a property tree to JSON format")]
    public ExportJsonResult ExportToJson(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path (empty for entire image)")] string? path = null,
        [Description("Maximum depth to export")] int maxDepth = 10,
        [Description("Output file path (optional - returns JSON if not specified)")] string? outputPath = null)
    {
        if (!_session.IsInitialized)
        {
            return new ExportJsonResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            WzObject target;

            if (string.IsNullOrEmpty(path))
            {
                target = img;
            }
            else
            {
                var prop = img.GetFromPath(path);
                if (prop == null)
                {
                    return new ExportJsonResult { Success = false, Error = $"Property not found: {path}" };
                }
                target = prop;
            }

            var json = ConvertToJson(target, 0, maxDepth);
            var jsonString = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });

            if (!string.IsNullOrEmpty(outputPath))
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(outputPath, jsonString);

                return new ExportJsonResult
                {
                    Success = true,
                    OutputPath = outputPath,
                    Size = jsonString.Length
                };
            }

            return new ExportJsonResult
            {
                Success = true,
                JsonData = jsonString,
                Size = jsonString.Length
            };
        }
        catch (Exception ex)
        {
            return new ExportJsonResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "export_to_xml"), Description("Export a property tree to XML format")]
    public ExportXmlResult ExportToXml(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Output file path")] string outputPath,
        [Description("Property path (empty for entire image)")] string? path = null,
        [Description("Maximum depth to export")] int maxDepth = 10)
    {
        if (!_session.IsInitialized)
        {
            return new ExportXmlResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            WzObject target;

            if (string.IsNullOrEmpty(path))
            {
                target = img;
            }
            else
            {
                var prop = img.GetFromPath(path);
                if (prop == null)
                {
                    return new ExportXmlResult { Success = false, Error = $"Property not found: {path}" };
                }
                target = prop;
            }

            var xml = ConvertToXml(target, 0, maxDepth);
            var doc = new XDocument(xml);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            doc.Save(outputPath);

            return new ExportXmlResult
            {
                Success = true,
                OutputPath = outputPath,
                Size = new FileInfo(outputPath).Length
            };
        }
        catch (Exception ex)
        {
            return new ExportXmlResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "export_png"), Description("Export a canvas property to PNG file")]
    public ExportPngResult ExportPng(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path,
        [Description("Output file path")] string outputPath)
    {
        if (!_session.IsInitialized)
        {
            return new ExportPngResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path) as WzCanvasProperty;

            if (prop == null)
            {
                return new ExportPngResult { Success = false, Error = $"Canvas property not found: {path}" };
            }

            var bitmap = prop.GetLinkedWzCanvasBitmap();
            if (bitmap == null)
            {
                return new ExportPngResult { Success = false, Error = "Failed to extract bitmap" };
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);

            return new ExportPngResult
            {
                Success = true,
                OutputPath = outputPath,
                Width = bitmap.Width,
                Height = bitmap.Height,
                Size = new FileInfo(outputPath).Length
            };
        }
        catch (Exception ex)
        {
            return new ExportPngResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "export_mp3"), Description("Export a sound property to MP3 file")]
    public ExportMp3Result ExportMp3(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the sound")] string path,
        [Description("Output file path")] string outputPath)
    {
        if (!_session.IsInitialized)
        {
            return new ExportMp3Result { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path) as WzBinaryProperty;

            if (prop == null)
            {
                return new ExportMp3Result { Success = false, Error = $"Sound property not found: {path}" };
            }

            var data = prop.GetBytes(false);
            if (data == null || data.Length == 0)
            {
                return new ExportMp3Result { Success = false, Error = "Failed to extract sound data" };
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(outputPath, data);

            return new ExportMp3Result
            {
                Success = true,
                OutputPath = outputPath,
                Duration = prop.Length,
                Frequency = prop.Frequency,
                Size = data.Length
            };
        }
        catch (Exception ex)
        {
            return new ExportMp3Result { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "export_all_images"), Description("Export all canvas properties from a path to PNG files")]
    public BatchExportResult ExportAllImages(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Output directory")] string outputDir,
        [Description("Property path (empty for entire image)")] string? path = null,
        [Description("Maximum depth to search")] int maxDepth = 10)
    {
        if (!_session.IsInitialized)
        {
            return new BatchExportResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            WzObject target;

            if (string.IsNullOrEmpty(path))
            {
                target = img;
            }
            else
            {
                var prop = img.GetFromPath(path);
                if (prop == null)
                {
                    return new BatchExportResult { Success = false, Error = $"Property not found: {path}" };
                }
                target = prop;
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var exported = new List<ExportedItem>();
            var failed = new List<FailedItem>();

            ExportCanvasesRecursive(target, "", outputDir, exported, failed, 0, maxDepth);

            return new BatchExportResult
            {
                Success = true,
                OutputDirectory = outputDir,
                ExportedCount = exported.Count,
                FailedCount = failed.Count,
                Exported = exported,
                Failed = failed
            };
        }
        catch (Exception ex)
        {
            return new BatchExportResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "export_all_sounds"), Description("Export all sound properties from a path to MP3 files")]
    public BatchExportResult ExportAllSounds(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Output directory")] string outputDir,
        [Description("Property path (empty for entire image)")] string? path = null,
        [Description("Maximum depth to search")] int maxDepth = 10)
    {
        if (!_session.IsInitialized)
        {
            return new BatchExportResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            WzObject target;

            if (string.IsNullOrEmpty(path))
            {
                target = img;
            }
            else
            {
                var prop = img.GetFromPath(path);
                if (prop == null)
                {
                    return new BatchExportResult { Success = false, Error = $"Property not found: {path}" };
                }
                target = prop;
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var exported = new List<ExportedItem>();
            var failed = new List<FailedItem>();

            ExportSoundsRecursive(target, "", outputDir, exported, failed, 0, maxDepth);

            return new BatchExportResult
            {
                Success = true,
                OutputDirectory = outputDir,
                ExportedCount = exported.Count,
                FailedCount = failed.Count,
                Exported = exported,
                Failed = failed
            };
        }
        catch (Exception ex)
        {
            return new BatchExportResult { Success = false, Error = ex.Message };
        }
    }

    private void ExportCanvasesRecursive(WzObject obj, string path, string outputDir,
        List<ExportedItem> exported, List<FailedItem> failed, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        IEnumerable<WzImageProperty>? children = null;

        if (obj is WzImage img)
        {
            children = img.WzProperties;
        }
        else if (obj is WzImageProperty prop)
        {
            if (prop is WzCanvasProperty canvas)
            {
                try
                {
                    var bitmap = canvas.GetLinkedWzCanvasBitmap();
                    if (bitmap != null)
                    {
                        var fileName = path.Replace("/", "_").Replace("\\", "_");
                        if (string.IsNullOrEmpty(fileName)) fileName = "root";
                        var filePath = Path.Combine(outputDir, $"{fileName}.png");

                        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                        exported.Add(new ExportedItem
                        {
                            SourcePath = path,
                            OutputPath = filePath,
                            Size = new FileInfo(filePath).Length
                        });
                    }
                }
                catch (Exception ex)
                {
                    failed.Add(new FailedItem { Path = path, Error = ex.Message });
                }
            }

            children = prop.WzProperties;
        }

        if (children != null)
        {
            foreach (var child in children)
            {
                var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}/{child.Name}";
                ExportCanvasesRecursive(child, childPath, outputDir, exported, failed, depth + 1, maxDepth);
            }
        }
    }

    private void ExportSoundsRecursive(WzObject obj, string path, string outputDir,
        List<ExportedItem> exported, List<FailedItem> failed, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        IEnumerable<WzImageProperty>? children = null;

        if (obj is WzImage img)
        {
            children = img.WzProperties;
        }
        else if (obj is WzImageProperty prop)
        {
            if (prop is WzBinaryProperty sound)
            {
                try
                {
                    var data = sound.GetBytes(false);
                    if (data != null && data.Length > 0)
                    {
                        var fileName = path.Replace("/", "_").Replace("\\", "_");
                        if (string.IsNullOrEmpty(fileName)) fileName = "root";
                        var filePath = Path.Combine(outputDir, $"{fileName}.mp3");

                        File.WriteAllBytes(filePath, data);
                        exported.Add(new ExportedItem
                        {
                            SourcePath = path,
                            OutputPath = filePath,
                            Size = data.Length
                        });
                    }
                }
                catch (Exception ex)
                {
                    failed.Add(new FailedItem { Path = path, Error = ex.Message });
                }
            }

            children = prop.WzProperties;
        }

        if (children != null)
        {
            foreach (var child in children)
            {
                var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}/{child.Name}";
                ExportSoundsRecursive(child, childPath, outputDir, exported, failed, depth + 1, maxDepth);
            }
        }
    }

    private Dictionary<string, object?> ConvertToJson(WzObject obj, int depth, int maxDepth)
    {
        var result = new Dictionary<string, object?>
        {
            ["_name"] = obj.Name,
            ["_type"] = obj is WzImageProperty typeProp ? typeProp.PropertyType.ToString() : obj.GetType().Name
        };

        if (obj is WzImageProperty imgProp)
        {
            var value = WzDataConverter.GetPropertyValue(imgProp);
            if (value != null)
            {
                result["_value"] = value;
            }
        }

        if (depth < maxDepth)
        {
            IEnumerable<WzImageProperty>? children = null;

            if (obj is WzImage img)
            {
                children = img.WzProperties;
            }
            else if (obj is WzImageProperty childProp)
            {
                children = childProp.WzProperties;
            }

            if (children != null && children.Any())
            {
                var childDict = new Dictionary<string, object?>();
                foreach (var child in children)
                {
                    childDict[child.Name] = ConvertToJson(child, depth + 1, maxDepth);
                }
                result["_children"] = childDict;
            }
        }

        return result;
    }

    private XElement ConvertToXml(WzObject obj, int depth, int maxDepth)
    {
        var element = new XElement("property",
            new XAttribute("name", obj.Name));

        if (obj is WzImageProperty prop)
        {
            element.Add(new XAttribute("type", prop.PropertyType.ToString()));

            var value = WzDataConverter.GetPropertyValue(prop);
            if (value != null && !(value is CanvasValue) && !(value is SoundValue) && !(value is SubPropertyValue))
            {
                element.Add(new XAttribute("value", value.ToString() ?? ""));
            }
        }
        else
        {
            element.Add(new XAttribute("type", obj.GetType().Name));
        }

        if (depth < maxDepth)
        {
            IEnumerable<WzImageProperty>? children = null;

            if (obj is WzImage img)
            {
                children = img.WzProperties;
            }
            else if (obj is WzImageProperty imgProp)
            {
                children = imgProp.WzProperties;
            }

            if (children != null)
            {
                foreach (var child in children)
                {
                    element.Add(ConvertToXml(child, depth + 1, maxDepth));
                }
            }
        }

        return element;
    }
}

// Result types

public class ExportJsonResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputPath { get; set; }
    public string? JsonData { get; set; }
    public long Size { get; set; }
}

public class ExportXmlResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputPath { get; set; }
    public long Size { get; set; }
}

public class ExportPngResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputPath { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Size { get; set; }
}

public class ExportMp3Result
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputPath { get; set; }
    public int Duration { get; set; }
    public int Frequency { get; set; }
    public long Size { get; set; }
}

public class BatchExportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputDirectory { get; set; }
    public int ExportedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ExportedItem>? Exported { get; set; }
    public List<FailedItem>? Failed { get; set; }
}

public class ExportedItem
{
    public required string SourcePath { get; set; }
    public required string OutputPath { get; set; }
    public long Size { get; set; }
}

public class FailedItem
{
    public required string Path { get; set; }
    public required string Error { get; set; }
}
