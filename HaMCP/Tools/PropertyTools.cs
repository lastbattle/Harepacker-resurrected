using System.ComponentModel;
using ModelContextProtocol.Server;
using HaMCP.Core;
using HaMCP.Server;
using HaMCP.Utils;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for property access
/// </summary>
[McpServerToolType]
public class PropertyTools : ToolBase
{
    public PropertyTools(WzSessionManager session) : base(session) { }

    [McpServerTool(Name = "get_property"), Description("Get a property with full metadata")]
    public new Result<GetPropertyData> GetProperty(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path within the image")] string path)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var prop = img.GetFromPath(path)
                ?? throw new InvalidOperationException($"Property not found: {path}");

            return new GetPropertyData
            {
                Category = category,
                Image = image,
                Path = path,
                Property = WzDataConverter.GetPropertyData(prop)
            };
        });
    }

    [McpServerTool(Name = "get_property_value"), Description("Get just the value of a property")]
    public Result<ValueData> GetPropertyValue(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var prop = img.GetFromPath(path)
                ?? throw new InvalidOperationException($"Property not found: {path}");

            return new ValueData
            {
                Type = prop.PropertyType.ToString(),
                Value = WzDataConverter.GetPropertyValue(prop)
            };
        });
    }

    [McpServerTool(Name = "get_string"), Description("Get a string property value")]
    public Result<StringValueData> GetString(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path,
        [Description("Default value if not found")] string? defaultValue = null)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var prop = img.GetFromPath(path) as WzStringProperty;

            return new StringValueData
            {
                Value = prop?.Value ?? defaultValue,
                Found = prop != null
            };
        });
    }

    [McpServerTool(Name = "get_int"), Description("Get an integer property value")]
    public Result<IntValueData> GetInt(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path,
        [Description("Default value if not found")] int? defaultValue = null)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var prop = img.GetFromPath(path);

            int? value = prop switch
            {
                WzIntProperty intProp => intProp.Value,
                WzShortProperty shortProp => shortProp.Value,
                WzLongProperty longProp => (int)longProp.Value,
                _ => null
            };

            return new IntValueData
            {
                Value = value ?? defaultValue,
                Found = value.HasValue
            };
        });
    }

    [McpServerTool(Name = "get_float"), Description("Get a float property value")]
    public Result<FloatValueData> GetFloat(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path,
        [Description("Default value if not found")] float? defaultValue = null)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var prop = img.GetFromPath(path);

            float? value = prop switch
            {
                WzFloatProperty floatProp => floatProp.Value,
                WzDoubleProperty doubleProp => (float)doubleProp.Value,
                WzIntProperty intProp => intProp.Value,
                _ => null
            };

            return new FloatValueData
            {
                Value = value ?? defaultValue,
                Found = value.HasValue
            };
        });
    }

    [McpServerTool(Name = "get_vector"), Description("Get a vector property (X, Y)")]
    public Result<VectorData> GetVector(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path)
    {
        return Execute(() =>
        {
            var prop = GetRequiredProperty<WzVectorProperty>(category, image, path, "Vector");
            return new VectorData { X = prop.X.Value, Y = prop.Y.Value };
        });
    }

    [McpServerTool(Name = "resolve_uol"), Description("Resolve a UOL (link) property to its target")]
    public Result<ResolveUolData> ResolveUol(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path")] string path)
    {
        return Execute(() =>
        {
            var prop = GetRequiredProperty<WzUOLProperty>(category, image, path, "UOL");
            var target = prop.LinkValue as WzImageProperty;

            if (target == null)
                throw new InvalidOperationException($"UOL target not found: {prop.Value}");

            return new ResolveUolData
            {
                UolPath = prop.Value,
                TargetPath = target.FullPath,
                TargetType = target.PropertyType.ToString(),
                TargetData = WzDataConverter.GetPropertyData(target)
            };
        });
    }

    [McpServerTool(Name = "get_children"), Description("Get all child properties of a node")]
    public Result<ChildrenData> GetChildren(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path (empty for root)")] string? path = null)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            IEnumerable<WzImageProperty>? children;

            if (string.IsNullOrEmpty(path))
            {
                children = img.WzProperties;
            }
            else
            {
                var prop = img.GetFromPath(path)
                    ?? throw new InvalidOperationException($"Property not found: {path}");
                children = prop.WzProperties;
            }

            return new ChildrenData
            {
                Path = path ?? "",
                Children = children?.Select(c => WzDataConverter.GetPropertyData(c)).ToList()
                    ?? new List<PropertyData>()
            };
        });
    }

    [McpServerTool(Name = "get_property_count"), Description("Count child properties of a node")]
    public Result<CountData> GetPropertyCount(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path (empty for root)")] string? path = null)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);

            if (string.IsNullOrEmpty(path))
                return new CountData { Count = img.WzProperties?.Count ?? 0 };

            var prop = img.GetFromPath(path)
                ?? throw new InvalidOperationException($"Property not found: {path}");

            return new CountData { Count = prop.WzProperties?.Count ?? 0 };
        });
    }

    [McpServerTool(Name = "iterate_properties"), Description("Iterate properties with pagination")]
    public Result<IterateData> IterateProperties(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path (empty for root)")] string? path = null,
        [Description("Offset to start from")] int offset = 0,
        [Description("Maximum number of properties to return")] int limit = 50)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            IEnumerable<WzImageProperty>? allChildren;

            if (string.IsNullOrEmpty(path))
            {
                allChildren = img.WzProperties;
            }
            else
            {
                var prop = img.GetFromPath(path)
                    ?? throw new InvalidOperationException($"Property not found: {path}");
                allChildren = prop.WzProperties;
            }

            var children = allChildren?.ToList() ?? new List<WzImageProperty>();
            var total = children.Count;
            var page = children.Skip(offset).Take(limit).ToList();

            return new IterateData
            {
                Path = path ?? "",
                Offset = offset,
                Limit = limit,
                TotalCount = total,
                HasMore = offset + page.Count < total,
                Properties = page.Select(c => WzDataConverter.GetPropertyData(c)).ToList()
            };
        });
    }

    [McpServerTool(Name = "get_properties_batch"), Description("Get multiple properties in a single request")]
    public Result<BatchPropertiesData> GetPropertiesBatch(
        [Description("Array of property requests, each with category, image, and path")] List<PropertyRequest> requests)
    {
        return Execute(() =>
        {
            var results = new List<BatchPropertyItem>();

            foreach (var request in requests)
            {
                try
                {
                    var img = GetImage(request.Category, request.Image);
                    var prop = img.GetFromPath(request.Path);

                    if (prop == null)
                    {
                        results.Add(new BatchPropertyItem
                        {
                            Category = request.Category,
                            Image = request.Image,
                            Path = request.Path,
                            Success = false,
                            Error = $"Property not found: {request.Path}"
                        });
                    }
                    else
                    {
                        results.Add(new BatchPropertyItem
                        {
                            Category = request.Category,
                            Image = request.Image,
                            Path = request.Path,
                            Success = true,
                            Property = WzDataConverter.GetPropertyData(prop)
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new BatchPropertyItem
                    {
                        Category = request.Category,
                        Image = request.Image,
                        Path = request.Path,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            return new BatchPropertiesData
            {
                TotalRequested = requests.Count,
                SuccessCount = results.Count(r => r.Success),
                FailedCount = results.Count(r => !r.Success),
                Results = results
            };
        });
    }
}

// Data types

public class GetPropertyData
{
    public string? Category { get; init; }
    public string? Image { get; init; }
    public string? Path { get; init; }
    public PropertyData? Property { get; init; }
}

public class ValueData
{
    public string? Type { get; init; }
    public object? Value { get; init; }
}

public class StringValueData
{
    public string? Value { get; init; }
    public bool Found { get; init; }
}

public class IntValueData
{
    public int? Value { get; init; }
    public bool Found { get; init; }
}

public class FloatValueData
{
    public float? Value { get; init; }
    public bool Found { get; init; }
}

public class VectorData
{
    public int X { get; init; }
    public int Y { get; init; }
}

public class ResolveUolData
{
    public string? UolPath { get; init; }
    public string? TargetPath { get; init; }
    public string? TargetType { get; init; }
    public PropertyData? TargetData { get; init; }
}

public class ChildrenData
{
    public string? Path { get; init; }
    public required List<PropertyData> Children { get; init; }
}

public class CountData
{
    public int Count { get; init; }
}

public class IterateData
{
    public string? Path { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
    public int TotalCount { get; init; }
    public bool HasMore { get; init; }
    public required List<PropertyData> Properties { get; init; }
}

public class PropertyRequest
{
    public required string Category { get; init; }
    public required string Image { get; init; }
    public required string Path { get; init; }
}

public class BatchPropertiesData
{
    public int TotalRequested { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public required List<BatchPropertyItem> Results { get; init; }
}

public class BatchPropertyItem
{
    public required string Category { get; init; }
    public required string Image { get; init; }
    public required string Path { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public PropertyData? Property { get; init; }
}
