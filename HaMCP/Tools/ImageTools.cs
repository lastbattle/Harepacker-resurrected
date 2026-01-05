using System.ComponentModel;
using ModelContextProtocol.Server;
using HaMCP.Core;
using HaMCP.Server;
using HaMCP.Utils;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for canvas/image operations
/// </summary>
[McpServerToolType]
public class ImageTools : ToolBase
{
    public ImageTools(WzSessionManager session) : base(session) { }

    [McpServerTool(Name = "get_canvas_bitmap"), Description("Get canvas image as PNG (base64 encoded)")]
    public Result<CanvasBitmapData> GetCanvasBitmap(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path)
    {
        return Execute(() =>
        {
            var prop = GetRequiredProperty<WzCanvasProperty>(category, image, path, "Canvas");
            var base64 = WzDataConverter.GetCanvasBase64(prop)
                ?? throw new InvalidOperationException("Failed to extract bitmap");

            return new CanvasBitmapData
            {
                Width = prop.PngProperty?.Width ?? 0,
                Height = prop.PngProperty?.Height ?? 0,
                Base64Png = base64
            };
        });
    }

    [McpServerTool(Name = "get_canvas_info"), Description("Get canvas metadata without the actual image data")]
    public Result<CanvasInfoData> GetCanvasInfo(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path)
    {
        return Execute(() =>
        {
            var prop = GetRequiredProperty<WzCanvasProperty>(category, image, path, "Canvas");
            return new CanvasInfoData
            {
                Width = prop.PngProperty?.Width ?? 0,
                Height = prop.PngProperty?.Height ?? 0,
                Format = prop.PngProperty?.Format.ToString(),
                HasChildren = prop.WzProperties?.Count > 0,
                ChildCount = prop.WzProperties?.Count ?? 0,
                Origin = WzDataConverter.GetCanvasOrigin(prop),
                Delay = WzDataConverter.GetCanvasDelay(prop)
            };
        });
    }

    [McpServerTool(Name = "get_canvas_origin"), Description("Get the canvas origin point (draw offset)")]
    public Result<Point2D> GetCanvasOrigin(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path)
    {
        return Execute(() =>
        {
            var prop = GetRequiredProperty<WzCanvasProperty>(category, image, path, "Canvas");
            var origin = WzDataConverter.GetCanvasOrigin(prop)
                ?? throw new InvalidOperationException("No origin property found");
            return new Point2D(origin.X, origin.Y);
        });
    }

    [McpServerTool(Name = "get_canvas_delay"), Description("Get animation frame delay in milliseconds")]
    public Result<DelayData> GetCanvasDelay(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path)
    {
        return Execute(() =>
        {
            var prop = GetRequiredProperty<WzCanvasProperty>(category, image, path, "Canvas");
            return new DelayData { Delay = WzDataConverter.GetCanvasDelay(prop) };
        });
    }

    [McpServerTool(Name = "get_animation_frames"), Description("Get all animation frames with metadata")]
    public Result<AnimationData> GetAnimationFrames(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the animation container")] string path)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var prop = img.GetFromPath(path)
                ?? throw new InvalidOperationException($"Property not found: {path}");

            var frames = new List<FrameData>();
            int totalDuration = 0;

            for (int i = 0; ; i++)
            {
                if (prop[i.ToString()] is not WzCanvasProperty frameProp) break;

                var origin = WzDataConverter.GetCanvasOrigin(frameProp);
                var delay = WzDataConverter.GetCanvasDelay(frameProp);

                frames.Add(new FrameData
                {
                    Index = i,
                    Width = frameProp.PngProperty?.Width ?? 0,
                    Height = frameProp.PngProperty?.Height ?? 0,
                    Origin = origin,
                    Delay = delay,
                    Base64Png = WzDataConverter.GetCanvasBase64(frameProp)
                });

                totalDuration += delay;
            }

            return new AnimationData
            {
                FrameCount = frames.Count,
                TotalDuration = totalDuration,
                Frames = frames
            };
        });
    }

    [McpServerTool(Name = "list_canvas_in_image"), Description("List all canvas properties in an image")]
    public Result<CanvasListData> ListCanvasInImage(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Maximum depth to search")] int maxDepth = 10)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var canvases = new List<CanvasEntry>();
            FindCanvases(img, "", canvases, 0, maxDepth);

            return new CanvasListData
            {
                Category = category,
                Image = image,
                Count = canvases.Count,
                Canvases = canvases
            };
        });
    }

    [McpServerTool(Name = "get_canvas_head"), Description("Get the canvas head position (used for character rendering)")]
    public Result<Point2D> GetCanvasHead(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path)
    {
        return Execute(() =>
        {
            var prop = GetRequiredProperty<WzCanvasProperty>(category, image, path, "Canvas");
            var head = prop["head"] as WzVectorProperty
                ?? throw new InvalidOperationException("No head property found");
            return new Point2D(head.X.Value, head.Y.Value);
        });
    }

    [McpServerTool(Name = "get_canvas_bounds"), Description("Get the canvas bounds (lt - left-top position)")]
    public Result<BoundsData> GetCanvasBounds(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path)
    {
        return Execute(() =>
        {
            var prop = GetRequiredProperty<WzCanvasProperty>(category, image, path, "Canvas");
            var lt = prop["lt"] as WzVectorProperty;
            var rb = prop["rb"] as WzVectorProperty;

            return new BoundsData
            {
                HasLt = lt != null,
                LtX = lt?.X.Value ?? 0,
                LtY = lt?.Y.Value ?? 0,
                HasRb = rb != null,
                RbX = rb?.X.Value ?? 0,
                RbY = rb?.Y.Value ?? 0
            };
        });
    }

    [McpServerTool(Name = "resolve_canvas_link"), Description("Resolve _inlink or _outlink canvas references")]
    public Result<ResolveCanvasData> ResolveCanvasLink(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path to the canvas")] string path)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var prop = GetRequiredProperty<WzCanvasProperty>(category, image, path, "Canvas");

            // Check for _inlink
            if (prop["_inlink"] is WzStringProperty inlink)
            {
                var targetPath = inlink.Value;
                var targetProp = img.GetFromPath(targetPath) as WzCanvasProperty;

                if (targetProp != null)
                {
                    return new ResolveCanvasData
                    {
                        LinkType = "inlink",
                        LinkPath = targetPath,
                        Width = targetProp.PngProperty?.Width ?? 0,
                        Height = targetProp.PngProperty?.Height ?? 0,
                        Base64Png = WzDataConverter.GetCanvasBase64(targetProp)
                    };
                }
                throw new InvalidOperationException($"Inlink target not found: {targetPath}");
            }

            // Check for _outlink
            if (prop["_outlink"] is WzStringProperty outlink)
            {
                throw new InvalidOperationException($"Outlink resolution requires cross-image access. Path: {outlink.Value}");
            }

            // No link - return the canvas itself
            return new ResolveCanvasData
            {
                LinkType = "none",
                Width = prop.PngProperty?.Width ?? 0,
                Height = prop.PngProperty?.Height ?? 0,
                Base64Png = WzDataConverter.GetCanvasBase64(prop)
            };
        });
    }

    private void FindCanvases(WzObject obj, string path, List<CanvasEntry> results, int depth, int maxDepth)
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
                results.Add(new CanvasEntry
                {
                    Path = path,
                    Width = canvas.PngProperty?.Width ?? 0,
                    Height = canvas.PngProperty?.Height ?? 0
                });
            }
            children = prop.WzProperties;
        }

        if (children != null)
        {
            foreach (var child in children)
            {
                var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}/{child.Name}";
                FindCanvases(child, childPath, results, depth + 1, maxDepth);
            }
        }
    }
}

// Data types

public class CanvasBitmapData
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string? Base64Png { get; init; }
}

public class CanvasInfoData
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string? Format { get; init; }
    public bool HasChildren { get; init; }
    public int ChildCount { get; init; }
    public VectorValue? Origin { get; init; }
    public int Delay { get; init; }
}

public class DelayData
{
    public int Delay { get; init; }
}

public class AnimationData
{
    public int FrameCount { get; init; }
    public int TotalDuration { get; init; }
    public required List<FrameData> Frames { get; init; }
}

public class FrameData
{
    public int Index { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public VectorValue? Origin { get; init; }
    public int Delay { get; init; }
    public string? Base64Png { get; init; }
}

public class CanvasListData
{
    public string? Category { get; init; }
    public string? Image { get; init; }
    public int Count { get; init; }
    public List<CanvasEntry>? Canvases { get; init; }
}

public class CanvasEntry
{
    public required string Path { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

public class BoundsData
{
    public bool HasLt { get; init; }
    public int LtX { get; init; }
    public int LtY { get; init; }
    public bool HasRb { get; init; }
    public int RbX { get; init; }
    public int RbY { get; init; }
}

public class ResolveCanvasData
{
    public string? LinkType { get; init; }
    public string? LinkPath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string? Base64Png { get; init; }
}
