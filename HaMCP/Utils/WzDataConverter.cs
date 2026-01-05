using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Drawing;

namespace HaMCP.Utils;

/// <summary>
/// Utility class for converting WZ property values to serializable formats
/// </summary>
public static class WzDataConverter
{
    /// <summary>
    /// Gets the value of a property in a serializable format
    /// </summary>
    public static object? GetPropertyValue(WzImageProperty prop)
    {
        return prop switch
        {
            WzNullProperty => null,
            WzShortProperty shortProp => shortProp.Value,
            WzIntProperty intProp => intProp.Value,
            WzLongProperty longProp => longProp.Value,
            WzFloatProperty floatProp => floatProp.Value,
            WzDoubleProperty doubleProp => doubleProp.Value,
            WzStringProperty stringProp => stringProp.Value,
            WzVectorProperty vectorProp => new VectorValue { X = vectorProp.X.Value, Y = vectorProp.Y.Value },
            WzUOLProperty uolProp => new UolValue { Path = uolProp.Value },
            WzCanvasProperty canvasProp => new CanvasValue
            {
                Width = canvasProp.PngProperty?.Width ?? 0,
                Height = canvasProp.PngProperty?.Height ?? 0,
                HasChildren = canvasProp.WzProperties?.Count > 0
            },
            WzBinaryProperty soundProp => new SoundValue
            {
                Length = soundProp.Length,
                Frequency = soundProp.Frequency
            },
            WzConvexProperty convexProp => new ConvexValue { PointCount = convexProp.WzProperties?.Count ?? 0 },
            WzSubProperty subProp => new SubPropertyValue { ChildCount = subProp.WzProperties?.Count ?? 0 },
            WzLuaProperty luaProp => "[Lua Script]",
            _ => prop.ToString()
        };
    }

    /// <summary>
    /// Gets detailed property information
    /// </summary>
    public static PropertyData GetPropertyData(WzImageProperty prop)
    {
        var data = new PropertyData
        {
            Name = prop.Name,
            Type = prop.PropertyType.ToString(),
            FullPath = prop.FullPath
        };

        switch (prop)
        {
            case WzNullProperty:
                data.Value = null;
                break;

            case WzShortProperty shortProp:
                data.Value = shortProp.Value;
                break;

            case WzIntProperty intProp:
                data.Value = intProp.Value;
                break;

            case WzLongProperty longProp:
                data.Value = longProp.Value;
                break;

            case WzFloatProperty floatProp:
                data.Value = floatProp.Value;
                break;

            case WzDoubleProperty doubleProp:
                data.Value = doubleProp.Value;
                break;

            case WzStringProperty stringProp:
                data.Value = stringProp.Value;
                break;

            case WzVectorProperty vectorProp:
                data.Value = new VectorValue { X = vectorProp.X.Value, Y = vectorProp.Y.Value };
                data.HasChildren = false;
                break;

            case WzUOLProperty uolProp:
                data.Value = uolProp.Value;
                data.UolPath = uolProp.Value;
                break;

            case WzCanvasProperty canvasProp:
                data.Value = new CanvasValue
                {
                    Width = canvasProp.PngProperty?.Width ?? 0,
                    Height = canvasProp.PngProperty?.Height ?? 0,
                    HasChildren = canvasProp.WzProperties?.Count > 0
                };
                data.HasChildren = canvasProp.WzProperties?.Count > 0;
                data.ChildCount = canvasProp.WzProperties?.Count ?? 0;
                break;

            case WzBinaryProperty soundProp:
                data.Value = new SoundValue
                {
                    Length = soundProp.Length,
                    Frequency = soundProp.Frequency
                };
                break;

            case WzSubProperty subProp:
                data.HasChildren = true;
                data.ChildCount = subProp.WzProperties?.Count ?? 0;
                data.ChildNames = subProp.WzProperties?.Select(p => p.Name).ToList();
                break;

            case WzConvexProperty convexProp:
                data.HasChildren = true;
                data.ChildCount = convexProp.WzProperties?.Count ?? 0;
                break;

            case WzLuaProperty:
                data.Value = "[Lua Script]";
                break;
        }

        return data;
    }

    /// <summary>
    /// Resolves a UOL property to its target
    /// </summary>
    public static WzImageProperty? ResolveUol(WzUOLProperty uol)
    {
        return uol.LinkValue as WzImageProperty;
    }

    /// <summary>
    /// Gets canvas PNG as base64
    /// </summary>
    public static string? GetCanvasBase64(WzCanvasProperty canvas)
    {
        try
        {
            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap == null) return null;

            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets canvas origin point
    /// </summary>
    public static VectorValue? GetCanvasOrigin(WzCanvasProperty canvas)
    {
        var origin = canvas["origin"] as WzVectorProperty;
        if (origin != null)
        {
            return new VectorValue { X = origin.X.Value, Y = origin.Y.Value };
        }
        return null;
    }

    /// <summary>
    /// Gets animation frame delay
    /// </summary>
    public static int GetCanvasDelay(WzCanvasProperty canvas)
    {
        var delay = canvas["delay"] as WzIntProperty;
        return delay?.Value ?? 100; // Default 100ms
    }
}

// Value types for serialization

public class VectorValue
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class UolValue
{
    public required string Path { get; set; }
}

public class CanvasValue
{
    public int Width { get; set; }
    public int Height { get; set; }
    public bool HasChildren { get; set; }
}

public class SoundValue
{
    public int Length { get; set; }
    public int Frequency { get; set; }
}

public class ConvexValue
{
    public int PointCount { get; set; }
}

public class SubPropertyValue
{
    public int ChildCount { get; set; }
}

public class PropertyData
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string FullPath { get; set; }
    public object? Value { get; set; }
    public bool HasChildren { get; set; }
    public int ChildCount { get; set; }
    public List<string>? ChildNames { get; set; }
    public string? UolPath { get; set; }
}
