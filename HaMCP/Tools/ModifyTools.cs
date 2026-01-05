using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using ModelContextProtocol.Server;
using HaMCP.Server;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for modifying WZ properties
/// </summary>
[McpServerToolType]
public class ModifyTools
{
    private readonly WzSessionManager _session;

    public ModifyTools(WzSessionManager session)
    {
        _session = session;
    }

    [McpServerTool(Name = "set_string"), Description("Set a string property value")]
    public ModifyResult SetString(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path,
        [Description("New string value")] string value)
    {
        if (!_session.IsInitialized)
        {
            return new ModifyResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path) as WzStringProperty;

            if (prop == null)
            {
                return new ModifyResult { Success = false, Error = $"String property not found: {path}" };
            }

            var oldValue = prop.Value;
            prop.Value = value;

            return new ModifyResult
            {
                Success = true,
                Path = path,
                OldValue = oldValue,
                NewValue = value
            };
        }
        catch (Exception ex)
        {
            return new ModifyResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "set_int"), Description("Set an integer property value")]
    public ModifyResult SetInt(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path,
        [Description("New integer value")] int value)
    {
        if (!_session.IsInitialized)
        {
            return new ModifyResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path);

            string? oldValue = null;

            switch (prop)
            {
                case WzIntProperty intProp:
                    oldValue = intProp.Value.ToString();
                    intProp.Value = value;
                    break;
                case WzShortProperty shortProp:
                    oldValue = shortProp.Value.ToString();
                    shortProp.Value = (short)value;
                    break;
                case WzLongProperty longProp:
                    oldValue = longProp.Value.ToString();
                    longProp.Value = value;
                    break;
                default:
                    return new ModifyResult { Success = false, Error = $"Integer property not found: {path}" };
            }

            return new ModifyResult
            {
                Success = true,
                Path = path,
                OldValue = oldValue,
                NewValue = value.ToString()
            };
        }
        catch (Exception ex)
        {
            return new ModifyResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "set_float"), Description("Set a float property value")]
    public ModifyResult SetFloat(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path,
        [Description("New float value")] float value)
    {
        if (!_session.IsInitialized)
        {
            return new ModifyResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path);

            string? oldValue = null;

            switch (prop)
            {
                case WzFloatProperty floatProp:
                    oldValue = floatProp.Value.ToString();
                    floatProp.Value = value;
                    break;
                case WzDoubleProperty doubleProp:
                    oldValue = doubleProp.Value.ToString();
                    doubleProp.Value = value;
                    break;
                default:
                    return new ModifyResult { Success = false, Error = $"Float property not found: {path}" };
            }

            return new ModifyResult
            {
                Success = true,
                Path = path,
                OldValue = oldValue,
                NewValue = value.ToString()
            };
        }
        catch (Exception ex)
        {
            return new ModifyResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "set_vector"), Description("Set a vector property (X, Y)")]
    public ModifyResult SetVector(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path,
        [Description("X value")] int x,
        [Description("Y value")] int y)
    {
        if (!_session.IsInitialized)
        {
            return new ModifyResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path) as WzVectorProperty;

            if (prop == null)
            {
                return new ModifyResult { Success = false, Error = $"Vector property not found: {path}" };
            }

            var oldValue = $"({prop.X.Value}, {prop.Y.Value})";
            prop.X.Value = x;
            prop.Y.Value = y;

            return new ModifyResult
            {
                Success = true,
                Path = path,
                OldValue = oldValue,
                NewValue = $"({x}, {y})"
            };
        }
        catch (Exception ex)
        {
            return new ModifyResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "add_property"), Description("Add a new property to a parent")]
    public AddPropertyResult AddProperty(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Parent property path (empty for image root)")] string parentPath,
        [Description("Name of the new property")] string name,
        [Description("Property type (String, Int, Short, Long, Float, Double, Vector, SubProperty, Null)")] string type,
        [Description("Initial value (optional, depends on type)")] string? value = null)
    {
        if (!_session.IsInitialized)
        {
            return new AddPropertyResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);

            IPropertyContainer parent;
            if (string.IsNullOrEmpty(parentPath))
            {
                parent = img;
            }
            else
            {
                var parentProp = img.GetFromPath(parentPath);
                if (parentProp == null)
                {
                    return new AddPropertyResult { Success = false, Error = $"Parent not found: {parentPath}" };
                }
                parent = parentProp as IPropertyContainer
                    ?? throw new InvalidOperationException($"Parent is not a property container: {parentPath}");
            }

            WzImageProperty newProp = type.ToLowerInvariant() switch
            {
                "string" => new WzStringProperty(name, value ?? ""),
                "int" => new WzIntProperty(name, int.TryParse(value, out var i) ? i : 0),
                "short" => new WzShortProperty(name, short.TryParse(value, out var s) ? s : (short)0),
                "long" => new WzLongProperty(name, long.TryParse(value, out var l) ? l : 0),
                "float" => new WzFloatProperty(name, float.TryParse(value, out var f) ? f : 0f),
                "double" => new WzDoubleProperty(name, double.TryParse(value, out var d) ? d : 0.0),
                "vector" => CreateVector(name, value),
                "subproperty" or "sub" => new WzSubProperty(name),
                "null" => new WzNullProperty(name),
                _ => throw new ArgumentException($"Unknown property type: {type}")
            };

            parent.AddProperty(newProp);

            return new AddPropertyResult
            {
                Success = true,
                ParentPath = parentPath,
                Name = name,
                Type = type,
                FullPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}"
            };
        }
        catch (Exception ex)
        {
            return new AddPropertyResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "delete_property"), Description("Delete a property")]
    public DeleteResult DeleteProperty(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to delete")] string path)
    {
        if (!_session.IsInitialized)
        {
            return new DeleteResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path);

            if (prop == null)
            {
                return new DeleteResult { Success = false, Error = $"Property not found: {path}" };
            }

            var parent = prop.Parent as IPropertyContainer;
            if (parent == null)
            {
                return new DeleteResult { Success = false, Error = "Cannot delete root property" };
            }

            parent.RemoveProperty(prop);

            return new DeleteResult
            {
                Success = true,
                Path = path,
                Type = prop.PropertyType.ToString()
            };
        }
        catch (Exception ex)
        {
            return new DeleteResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "rename_property"), Description("Rename a property")]
    public RenameResult RenameProperty(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path,
        [Description("New name")] string newName)
    {
        if (!_session.IsInitialized)
        {
            return new RenameResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path);

            if (prop == null)
            {
                return new RenameResult { Success = false, Error = $"Property not found: {path}" };
            }

            var oldName = prop.Name;
            prop.Name = newName;

            return new RenameResult
            {
                Success = true,
                OldName = oldName,
                NewName = newName,
                OldPath = path,
                NewPath = path.Substring(0, path.LastIndexOf('/') + 1) + newName
            };
        }
        catch (Exception ex)
        {
            return new RenameResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "copy_property"), Description("Deep copy a property to another location")]
    public CopyResult CopyProperty(
        [Description("Source category")] string srcCategory,
        [Description("Source image")] string srcImage,
        [Description("Source property path")] string srcPath,
        [Description("Destination category")] string destCategory,
        [Description("Destination image")] string destImage,
        [Description("Destination parent path")] string destParentPath,
        [Description("New property name (optional, uses source name if not specified)")] string? newName = null)
    {
        if (!_session.IsInitialized)
        {
            return new CopyResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var srcImg = _session.GetImage(srcCategory, srcImage);
            var srcProp = srcImg.GetFromPath(srcPath);

            if (srcProp == null)
            {
                return new CopyResult { Success = false, Error = $"Source property not found: {srcPath}" };
            }

            var destImg = _session.GetImage(destCategory, destImage);

            IPropertyContainer destParent;
            if (string.IsNullOrEmpty(destParentPath))
            {
                destParent = destImg;
            }
            else
            {
                var parentProp = destImg.GetFromPath(destParentPath);
                if (parentProp == null)
                {
                    return new CopyResult { Success = false, Error = $"Destination parent not found: {destParentPath}" };
                }
                destParent = parentProp as IPropertyContainer
                    ?? throw new InvalidOperationException($"Destination is not a property container");
            }

            var clonedProp = srcProp.DeepClone();
            if (!string.IsNullOrEmpty(newName))
            {
                clonedProp.Name = newName;
            }

            destParent.AddProperty(clonedProp);

            return new CopyResult
            {
                Success = true,
                SourcePath = $"{srcCategory}/{srcImage}/{srcPath}",
                DestinationPath = string.IsNullOrEmpty(destParentPath)
                    ? $"{destCategory}/{destImage}/{clonedProp.Name}"
                    : $"{destCategory}/{destImage}/{destParentPath}/{clonedProp.Name}"
            };
        }
        catch (Exception ex)
        {
            return new CopyResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "set_canvas_bitmap"), Description("Replace a canvas image with a new PNG")]
    public ModifyResult SetCanvasBitmap(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path,
        [Description("Base64 encoded PNG data")] string base64Png)
    {
        if (!_session.IsInitialized)
        {
            return new ModifyResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path) as WzCanvasProperty;

            if (prop == null)
            {
                return new ModifyResult { Success = false, Error = $"Canvas property not found: {path}" };
            }

            var pngData = Convert.FromBase64String(base64Png);
            using var ms = new MemoryStream(pngData);
            using var bitmap = new Bitmap(ms);

            var oldSize = $"{prop.PngProperty?.Width}x{prop.PngProperty?.Height}";
            prop.PngProperty.PNG = bitmap;

            return new ModifyResult
            {
                Success = true,
                Path = path,
                OldValue = oldSize,
                NewValue = $"{bitmap.Width}x{bitmap.Height}"
            };
        }
        catch (Exception ex)
        {
            return new ModifyResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "set_canvas_origin"), Description("Set the origin point of a canvas")]
    public ModifyResult SetCanvasOrigin(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path,
        [Description("X origin")] int x,
        [Description("Y origin")] int y)
    {
        if (!_session.IsInitialized)
        {
            return new ModifyResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);
            var prop = img.GetFromPath(path) as WzCanvasProperty;

            if (prop == null)
            {
                return new ModifyResult { Success = false, Error = $"Canvas property not found: {path}" };
            }

            var origin = prop["origin"] as WzVectorProperty;
            string? oldValue = null;

            if (origin != null)
            {
                oldValue = $"({origin.X.Value}, {origin.Y.Value})";
                origin.X.Value = x;
                origin.Y.Value = y;
            }
            else
            {
                // Create new origin property
                var newOrigin = new WzVectorProperty("origin",
                    new WzIntProperty("X", x),
                    new WzIntProperty("Y", y));
                prop.AddProperty(newOrigin);
                oldValue = "(none)";
            }

            return new ModifyResult
            {
                Success = true,
                Path = path + "/origin",
                OldValue = oldValue,
                NewValue = $"({x}, {y})"
            };
        }
        catch (Exception ex)
        {
            return new ModifyResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "import_png"), Description("Import a PNG as a new canvas property")]
    public ImportResult ImportPng(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Parent property path")] string parentPath,
        [Description("Name for the new canvas")] string name,
        [Description("Base64 encoded PNG data")] string base64Png,
        [Description("Origin X (optional)")] int? originX = null,
        [Description("Origin Y (optional)")] int? originY = null)
    {
        if (!_session.IsInitialized)
        {
            return new ImportResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);

            IPropertyContainer parent;
            if (string.IsNullOrEmpty(parentPath))
            {
                parent = img;
            }
            else
            {
                var parentProp = img.GetFromPath(parentPath);
                if (parentProp == null)
                {
                    return new ImportResult { Success = false, Error = $"Parent not found: {parentPath}" };
                }
                parent = parentProp as IPropertyContainer
                    ?? throw new InvalidOperationException($"Parent is not a property container");
            }

            var pngData = Convert.FromBase64String(base64Png);
            using var ms = new MemoryStream(pngData);
            using var bitmap = new Bitmap(ms);

            var canvas = new WzCanvasProperty(name);
            canvas.PngProperty = new WzPngProperty();
            canvas.PngProperty.PNG = bitmap;

            if (originX.HasValue && originY.HasValue)
            {
                var origin = new WzVectorProperty("origin",
                    new WzIntProperty("X", originX.Value),
                    new WzIntProperty("Y", originY.Value));
                canvas.AddProperty(origin);
            }

            parent.AddProperty(canvas);

            return new ImportResult
            {
                Success = true,
                Path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}",
                Type = "Canvas",
                Width = bitmap.Width,
                Height = bitmap.Height
            };
        }
        catch (Exception ex)
        {
            return new ImportResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "import_sound"), Description("Import audio data as a new sound property")]
    public ImportResult ImportSound(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Parent property path")] string parentPath,
        [Description("Name for the new sound")] string name,
        [Description("Base64 encoded MP3 data")] string base64Audio)
    {
        if (!_session.IsInitialized)
        {
            return new ImportResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);

            IPropertyContainer parent;
            if (string.IsNullOrEmpty(parentPath))
            {
                parent = img;
            }
            else
            {
                var parentProp = img.GetFromPath(parentPath);
                if (parentProp == null)
                {
                    return new ImportResult { Success = false, Error = $"Parent not found: {parentPath}" };
                }
                parent = parentProp as IPropertyContainer
                    ?? throw new InvalidOperationException($"Parent is not a property container");
            }

            var audioData = Convert.FromBase64String(base64Audio);

            // Create a temp file for the audio data
            var tempFile = Path.Combine(Path.GetTempPath(), $"harepacker_import_{Guid.NewGuid()}.mp3");
            try
            {
                File.WriteAllBytes(tempFile, audioData);
                var sound = new WzBinaryProperty(name, tempFile);
                parent.AddProperty(sound);
            }
            finally
            {
                // Clean up temp file
                try { File.Delete(tempFile); } catch { }
            }

            return new ImportResult
            {
                Success = true,
                Path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}",
                Type = "Sound",
                DataSize = audioData.Length
            };
        }
        catch (Exception ex)
        {
            return new ImportResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "save_image"), Description("Save changes to an image to disk")]
    public SaveResult SaveImage(
        [Description("Category name")] string category,
        [Description("Image name")] string image)
    {
        if (!_session.IsInitialized)
        {
            return new SaveResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var img = _session.GetImage(category, image);

            // Get the file path from the data source
            var ds = _session.DataSource;
            ds.SaveImage(category, img);

            return new SaveResult
            {
                Success = true,
                Category = category,
                Image = image
            };
        }
        catch (Exception ex)
        {
            return new SaveResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "discard_changes"), Description("Discard unsaved changes to an image")]
    public DiscardResult DiscardChanges(
        [Description("Category name")] string category,
        [Description("Image name")] string image)
    {
        if (!_session.IsInitialized)
        {
            return new DiscardResult { Success = false, Error = "No data source initialized" };
        }

        try
        {
            var ds = _session.DataSource;
            var img = ds.GetImage(category, image);

            if (img == null)
            {
                return new DiscardResult { Success = false, Error = $"Image not found: {category}/{image}" };
            }

            // Unparse and re-parse to reload from disk
            if (img.Parsed)
            {
                img.UnparseImage();
            }
            img.ParseImage();

            return new DiscardResult
            {
                Success = true,
                Category = category,
                Image = image
            };
        }
        catch (Exception ex)
        {
            return new DiscardResult { Success = false, Error = ex.Message };
        }
    }

    private static WzVectorProperty CreateVector(string name, string? value)
    {
        int x = 0, y = 0;
        if (!string.IsNullOrEmpty(value))
        {
            // Parse "x,y" or "(x, y)" format
            var cleaned = value.Trim('(', ')', ' ');
            var parts = cleaned.Split(',', ' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();
            if (parts.Length >= 2)
            {
                int.TryParse(parts[0].Trim(), out x);
                int.TryParse(parts[1].Trim(), out y);
            }
        }
        return new WzVectorProperty(name,
            new WzIntProperty("X", x),
            new WzIntProperty("Y", y));
    }
}

// Result types

public class ModifyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Path { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class AddPropertyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ParentPath { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? FullPath { get; set; }
}

public class DeleteResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Path { get; set; }
    public string? Type { get; set; }
}

public class RenameResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OldName { get; set; }
    public string? NewName { get; set; }
    public string? OldPath { get; set; }
    public string? NewPath { get; set; }
}

public class CopyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? SourcePath { get; set; }
    public string? DestinationPath { get; set; }
}

public class ImportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Path { get; set; }
    public string? Type { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int DataSize { get; set; }
}

public class SaveResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public string? Image { get; set; }
}

public class DiscardResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Category { get; set; }
    public string? Image { get; set; }
}
