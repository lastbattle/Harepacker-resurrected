using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using HaMCP.Core;
using HaMCP.Server;
using HaMCP.Utils;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaMCP.Tools;

/// <summary>
/// MCP tools for navigating and searching WZ data
/// </summary>
[McpServerToolType]
public class NavigationTools : ToolBase
{
    public NavigationTools(WzSessionManager session) : base(session) { }

    [McpServerTool(Name = "get_subdirectories"), Description("List subdirectories within a category")]
    public Result<SubdirectoryData> GetSubdirectories(
        [Description("Category name (e.g., 'Map', 'Mob')")] string category)
    {
        return Execute(() =>
        {
            var subdirs = Session.DataSource.GetSubdirectories(category).ToList();
            return new SubdirectoryData
            {
                Category = category,
                Subdirectories = subdirs
            };
        });
    }

    [McpServerTool(Name = "list_properties"), Description("List child properties of a node in an image. Use compact=true for smaller responses.")]
    public Result<PropertyListData> ListProperties(
        [Description("Category name")] string category,
        [Description("Image name (e.g., 'Map.img' or '100000000.img')")] string image,
        [Description("Property path within the image (empty for root)")] string? path = null,
        [Description("Return compact format with just names and types (default: true)")] bool compact = true,
        [Description("Offset for pagination (default: 0)")] int offset = 0,
        [Description("Maximum properties to return (default: 100, max: 500)")] int limit = 100)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            WzObject? target = string.IsNullOrEmpty(path) ? img : img.GetFromPath(path);

            if (target == null)
                throw new InvalidOperationException($"Path not found: {path}");

            // Clamp limit
            limit = Math.Clamp(limit, 1, 500);

            var allChildren = GetChildren(target).ToList();
            var totalCount = allChildren.Count;
            var pageChildren = allChildren.Skip(offset).Take(limit);

            return new PropertyListData
            {
                Category = category,
                Image = image,
                Path = path ?? "",
                TotalCount = totalCount,
                Offset = offset,
                Limit = limit,
                HasMore = offset + limit < totalCount,
                Properties = pageChildren.Select(c => new PropertyInfo
                {
                    Name = c.Name,
                    Type = GetPropertyTypeName(c),
                    HasChildren = compact ? (bool?)null : HasChildren(c),
                    ChildCount = HasChildren(c) ? GetChildCount(c) : null,
                    Value = compact ? null : GetSimpleValue(c)
                }).ToList()
            };
        });
    }

    [McpServerTool(Name = "get_tree_structure"), Description("Get hierarchical property tree structure. Use shallow depth for large trees.")]
    public Result<TreeData> GetTreeStructure(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path (empty for root)")] string? path = null,
        [Description("Maximum depth to traverse (default: 2, max: 5)")] int depth = 2,
        [Description("Maximum children per node (default: 50, max: 200)")] int maxChildrenPerNode = 50)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            WzObject? target = string.IsNullOrEmpty(path) ? img : img.GetFromPath(path);

            if (target == null)
                throw new InvalidOperationException($"Path not found: {path}");

            // Clamp parameters
            depth = Math.Clamp(depth, 1, 5);
            maxChildrenPerNode = Math.Clamp(maxChildrenPerNode, 1, 200);

            var ctx = new TreeBuildContext { NodeCount = 0, MaxTotalNodes = 1000 };

            return new TreeData
            {
                Category = category,
                Image = image,
                Path = path ?? "",
                Tree = BuildTree(target, depth, 0, maxChildrenPerNode, ctx)
            };
        });
    }

    [McpServerTool(Name = "search_by_name"), Description("Search for properties by name pattern. Use compact=true for smaller responses.")]
    public Result<SearchData> SearchByName(
        [Description("Search pattern (case-insensitive, supports * wildcards)")] string pattern,
        [Description("Category to search in (optional, searches all if not specified)")] string? category = null,
        [Description("Specific image to search in (optional)")] string? image = null,
        [Description("Maximum results to return (default: 50, max: 200)")] int maxResults = 50,
        [Description("Return compact format with paths only (default: true)")] bool compact = true)
    {
        return Execute(() =>
        {
            // Clamp maxResults
            maxResults = Math.Clamp(maxResults, 1, 200);

            var results = new List<SearchMatch>();
            var regex = WildcardToRegex(pattern);

            IEnumerable<string> categories = string.IsNullOrEmpty(category)
                ? Session.DataSource.GetCategories()
                : new[] { category };

            foreach (var cat in categories)
            {
                IEnumerable<WzImage> images;
                if (!string.IsNullOrEmpty(image))
                {
                    var img = Session.DataSource.GetImage(cat, image);
                    images = img != null ? new[] { img } : Enumerable.Empty<WzImage>();
                }
                else
                {
                    images = Session.DataSource.GetImagesInCategory(cat);
                }

                foreach (var img in images)
                {
                    if (!img.Parsed) img.ParseImage();
                    SearchInObject(img, cat, img.Name, "", regex, results, maxResults, compact);
                    if (results.Count >= maxResults) break;
                }
                if (results.Count >= maxResults) break;
            }

            return new SearchData
            {
                Pattern = pattern,
                Matches = results,
                TotalFound = results.Count,
                Truncated = results.Count >= maxResults
            };
        });
    }

    [McpServerTool(Name = "search_by_value"), Description("Search for properties by value. Use compact=true for smaller responses.")]
    public Result<SearchData> SearchByValue(
        [Description("Value to search for (string representation)")] string value,
        [Description("Property type to filter (optional: String, Int, Float, etc.)")] string? type = null,
        [Description("Category to search in (optional)")] string? category = null,
        [Description("Specific image to search in (optional)")] string? image = null,
        [Description("Maximum results to return (default: 50, max: 200)")] int maxResults = 50,
        [Description("Return compact format with paths only (default: true)")] bool compact = true)
    {
        return Execute(() =>
        {
            // Clamp maxResults
            maxResults = Math.Clamp(maxResults, 1, 200);

            var results = new List<SearchMatch>();

            IEnumerable<string> categories = string.IsNullOrEmpty(category)
                ? Session.DataSource.GetCategories()
                : new[] { category };

            foreach (var cat in categories)
            {
                IEnumerable<WzImage> images;
                if (!string.IsNullOrEmpty(image))
                {
                    var img = Session.DataSource.GetImage(cat, image);
                    images = img != null ? new[] { img } : Enumerable.Empty<WzImage>();
                }
                else
                {
                    images = Session.DataSource.GetImagesInCategory(cat);
                }

                foreach (var img in images)
                {
                    if (!img.Parsed) img.ParseImage();
                    SearchByValueInObject(img, cat, img.Name, "", value, type, results, maxResults, compact);
                    if (results.Count >= maxResults) break;
                }
                if (results.Count >= maxResults) break;
            }

            return new SearchData
            {
                Pattern = value,
                Matches = results,
                TotalFound = results.Count,
                Truncated = results.Count >= maxResults
            };
        });
    }

    [McpServerTool(Name = "get_property_path"), Description("Get the full path of a property")]
    public Result<PropertyPathData> GetPropertyPath(
        [Description("Category name")] string category,
        [Description("Image name")] string image,
        [Description("Property path within the image")] string path)
    {
        return Execute(() =>
        {
            var img = GetImage(category, image);
            var prop = img.GetFromPath(path)
                ?? throw new InvalidOperationException($"Property not found: {path}");

            return new PropertyPathData
            {
                Category = category,
                Image = image,
                RelativePath = path,
                FullPath = prop.FullPath,
                AbsolutePath = $"{category}/{image}/{path}"
            };
        });
    }

    #region Helper Methods

    private void SearchByValueInObject(WzObject obj, string category, string imageName, string path,
        string searchValue, string? typeFilter, List<SearchMatch> results, int maxResults, bool compact)
    {
        if (results.Count >= maxResults) return;

        foreach (var child in GetChildren(obj))
        {
            if (results.Count >= maxResults) return;
            var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}/{child.Name}";

            if (child is WzImageProperty prop)
            {
                if (!string.IsNullOrEmpty(typeFilter) &&
                    !prop.PropertyType.ToString().Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
                {
                    if (HasChildren(child))
                        SearchByValueInObject(child, category, imageName, childPath, searchValue, typeFilter, results, maxResults, compact);
                    continue;
                }

                var propValue = GetPropertyValueString(prop);
                if (propValue != null && propValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchMatch
                    {
                        Category = category,
                        Image = imageName,
                        Path = childPath,
                        Name = compact ? null : child.Name,
                        Type = compact ? null : GetPropertyTypeName(child),
                        Value = compact ? null : propValue
                    });
                }
            }

            if (HasChildren(child))
                SearchByValueInObject(child, category, imageName, childPath, searchValue, typeFilter, results, maxResults, compact);
        }
    }

    private static string? GetPropertyValueString(WzImageProperty prop) => prop switch
    {
        WzStringProperty s => s.Value,
        WzIntProperty i => i.Value.ToString(),
        WzShortProperty s => s.Value.ToString(),
        WzLongProperty l => l.Value.ToString(),
        WzFloatProperty f => f.Value.ToString(),
        WzDoubleProperty d => d.Value.ToString(),
        WzUOLProperty u => u.Value,
        _ => null
    };

    private void SearchInObject(WzObject obj, string category, string imageName, string path,
        Regex pattern, List<SearchMatch> results, int maxResults, bool compact)
    {
        if (results.Count >= maxResults) return;

        foreach (var child in GetChildren(obj))
        {
            if (results.Count >= maxResults) return;
            var childPath = string.IsNullOrEmpty(path) ? child.Name : $"{path}/{child.Name}";

            if (pattern.IsMatch(child.Name))
            {
                results.Add(new SearchMatch
                {
                    Category = category,
                    Image = imageName,
                    Path = childPath,
                    Name = compact ? null : child.Name,
                    Type = compact ? null : GetPropertyTypeName(child)
                });
            }

            if (HasChildren(child))
                SearchInObject(child, category, imageName, childPath, pattern, results, maxResults, compact);
        }
    }

    private static Regex WildcardToRegex(string pattern)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new Regex(regex, RegexOptions.IgnoreCase);
    }

    private TreeNode BuildTree(WzObject obj, int maxDepth, int currentDepth, int maxChildrenPerNode, TreeBuildContext ctx)
    {
        ctx.NodeCount++;

        var node = new TreeNode
        {
            Name = obj.Name,
            Type = GetPropertyTypeName(obj),
            Value = GetSimpleValue(obj)
        };

        if (currentDepth < maxDepth && HasChildren(obj) && ctx.NodeCount < ctx.MaxTotalNodes)
        {
            var children = GetChildren(obj).Take(maxChildrenPerNode).ToList();
            var totalChildren = GetChildCount(obj) ?? 0;

            var childNodes = new List<TreeNode>();
            foreach (var child in children)
            {
                if (ctx.NodeCount >= ctx.MaxTotalNodes) break;
                childNodes.Add(BuildTree(child, maxDepth, currentDepth + 1, maxChildrenPerNode, ctx));
            }
            node.Children = childNodes;

            // Indicate if there are more children
            if (totalChildren > maxChildrenPerNode)
            {
                node.TruncatedChildren = totalChildren - maxChildrenPerNode;
            }
        }

        return node;
    }

    private class TreeBuildContext
    {
        public int NodeCount { get; set; }
        public int MaxTotalNodes { get; set; }
    }

    private static IEnumerable<WzObject> GetChildren(WzObject obj) => obj switch
    {
        WzImage img => img.WzProperties?.Cast<WzObject>() ?? Enumerable.Empty<WzObject>(),
        WzImageProperty prop when prop.WzProperties != null => prop.WzProperties.Cast<WzObject>(),
        WzDirectory dir => dir.WzDirectories.Cast<WzObject>().Concat(dir.WzImages),
        _ => Enumerable.Empty<WzObject>()
    };

    private static bool HasChildren(WzObject obj) => obj switch
    {
        WzImage img => img.WzProperties?.Count > 0,
        WzImageProperty prop => prop.WzProperties?.Count > 0,
        WzDirectory dir => dir.WzDirectories.Count > 0 || dir.WzImages.Count > 0,
        _ => false
    };

    private static int? GetChildCount(WzObject obj) => obj switch
    {
        WzImage img => img.WzProperties?.Count,
        WzImageProperty prop => prop.WzProperties?.Count,
        WzDirectory dir => dir.WzDirectories.Count + dir.WzImages.Count,
        _ => null
    };

    private static string GetPropertyTypeName(WzObject obj) => obj switch
    {
        WzImage => "Image",
        WzDirectory => "Directory",
        WzImageProperty prop => prop.PropertyType.ToString(),
        _ => obj.GetType().Name
    };

    private static object? GetSimpleValue(WzObject obj) =>
        obj is WzImageProperty prop ? WzDataConverter.GetPropertyValue(prop) : null;

    #endregion
}

// Data types

public class SubdirectoryData
{
    public string? Category { get; init; }
    public List<string>? Subdirectories { get; init; }
}

public class PropertyListData
{
    public string? Category { get; init; }
    public string? Image { get; init; }
    public string? Path { get; init; }
    public int TotalCount { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
    public bool HasMore { get; init; }
    public List<PropertyInfo>? Properties { get; init; }
}

public class PropertyInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool? HasChildren { get; init; }
    public int? ChildCount { get; init; }
    public object? Value { get; init; }
}

public class TreeData
{
    public string? Category { get; init; }
    public string? Image { get; init; }
    public string? Path { get; init; }
    public TreeNode? Tree { get; init; }
}

public class TreeNode
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public object? Value { get; init; }
    public List<TreeNode>? Children { get; set; }
    public int? TruncatedChildren { get; set; }
}

public class SearchData
{
    public string? Pattern { get; init; }
    public List<SearchMatch>? Matches { get; init; }
    public int TotalFound { get; init; }
    public bool Truncated { get; init; }
}

public class SearchMatch
{
    public required string Category { get; init; }
    public required string Image { get; init; }
    public required string Path { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Value { get; init; }
}

public class PropertyPathData
{
    public string? Category { get; init; }
    public string? Image { get; init; }
    public string? RelativePath { get; init; }
    public string? FullPath { get; init; }
    public string? AbsolutePath { get; init; }
}
