using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class NavigationToolsTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly NavigationTools _tools;

    public NavigationToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new NavigationTools(_fixture.Session);
    }

    [Fact]
    public void GetSubdirectories_ReturnsSubdirs()
    {
        var result = _tools.GetSubdirectories("Map");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Subdirectories);
        Assert.Contains("Map0", result.Data.Subdirectories);
    }

    [Fact]
    public void ListProperties_ReturnsRootProperties()
    {
        var result = _tools.ListProperties("Character", "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Properties);
        Assert.True(result.Data.Properties.Count > 0);
    }

    [Fact]
    public void ListProperties_WithPath_ReturnsChildProperties()
    {
        var result = _tools.ListProperties("Character", "Test.img", "info");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Properties);
        Assert.True(result.Data.Properties.Count > 0);
    }

    [Fact]
    public void ListProperties_Pagination_ReturnsCorrectPage()
    {
        var result = _tools.ListProperties("Character", "Test.img", null, compact: true, offset: 0, limit: 2);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Properties);
        Assert.True(result.Data.Properties.Count <= 2);
        Assert.True(result.Data.TotalCount > 0);
        Assert.Equal(0, result.Data.Offset);
        Assert.Equal(2, result.Data.Limit);
    }

    [Fact]
    public void ListProperties_CompactMode_ExcludesValue()
    {
        var result = _tools.ListProperties("Character", "Test.img", null, compact: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Properties);
        // Compact mode excludes Value
        Assert.All(result.Data.Properties, p => Assert.Null(p.Value));
    }

    [Fact]
    public void ListProperties_NonCompactMode_IncludesValue()
    {
        var result = _tools.ListProperties("Character", "Test.img", null, compact: false);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Properties);
        // Non-compact includes HasChildren
        Assert.All(result.Data.Properties, p => Assert.NotNull(p.HasChildren));
    }

    [Fact]
    public void GetTreeStructure_ReturnsTree()
    {
        var result = _tools.GetTreeStructure("Character", "Test.img", depth: 2);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Tree);
        Assert.Equal("Test.img", result.Data.Tree.Name);
    }

    [Fact]
    public void GetTreeStructure_WithMaxChildrenPerNode_LimitsChildren()
    {
        var result = _tools.GetTreeStructure("Character", "Test.img", depth: 2, maxChildrenPerNode: 2);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Tree);
        // Tree should be limited to maxChildrenPerNode
        if (result.Data.Tree.Children != null && result.Data.Tree.Children.Count > 0)
        {
            Assert.True(result.Data.Tree.Children.Count <= 2);
        }
    }

    [Fact]
    public void SearchByName_FindsMatches()
    {
        var result = _tools.SearchByName("test*", category: "Character", image: "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Matches);
        Assert.True(result.Data.Matches.Count > 0);
    }

    [Fact]
    public void SearchByName_WithNoMatches_ReturnsEmpty()
    {
        var result = _tools.SearchByName("nonexistent12345", category: "Character", image: "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Matches);
        Assert.Empty(result.Data.Matches);
    }

    [Fact]
    public void SearchByName_CompactMode_ExcludesNameAndType()
    {
        var result = _tools.SearchByName("test*", category: "Character", image: "Test.img", compact: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Matches);
        Assert.True(result.Data.Matches.Count > 0);
        // Compact mode excludes Name and Type
        Assert.All(result.Data.Matches, m => Assert.Null(m.Name));
        Assert.All(result.Data.Matches, m => Assert.Null(m.Type));
        // But includes Category, Image, Path
        Assert.All(result.Data.Matches, m => Assert.NotNull(m.Category));
        Assert.All(result.Data.Matches, m => Assert.NotNull(m.Image));
        Assert.All(result.Data.Matches, m => Assert.NotNull(m.Path));
    }

    [Fact]
    public void SearchByName_NonCompactMode_IncludesNameAndType()
    {
        var result = _tools.SearchByName("test*", category: "Character", image: "Test.img", compact: false);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Matches);
        Assert.True(result.Data.Matches.Count > 0);
        // Non-compact includes Name and Type
        Assert.All(result.Data.Matches, m => Assert.NotNull(m.Name));
        Assert.All(result.Data.Matches, m => Assert.NotNull(m.Type));
    }

    [Fact]
    public void SearchByValue_FindsMatches()
    {
        var result = _tools.SearchByValue("Hello", category: "Character", image: "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Matches);
        Assert.True(result.Data.Matches.Count > 0);
    }

    [Fact]
    public void SearchByValue_WithTypeFilter_FiltersResults()
    {
        var result = _tools.SearchByValue("42", type: "Int", category: "Character", image: "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Matches);
    }

    [Fact]
    public void SearchByValue_CompactMode_ExcludesValueField()
    {
        var result = _tools.SearchByValue("Hello", category: "Character", image: "Test.img", compact: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Matches);
        // Compact mode excludes Value
        Assert.All(result.Data.Matches, m => Assert.Null(m.Value));
    }

    [Fact]
    public void GetPropertyPath_ReturnsFullPath()
    {
        var result = _tools.GetPropertyPath("Character", "Test.img", "info/name");

        Assert.True(result.Success);
        Assert.Equal("info/name", result.Data?.RelativePath);
        Assert.NotNull(result.Data?.FullPath);
    }

    [Fact]
    public void GetPropertyPath_WithInvalidPath_Fails()
    {
        var result = _tools.GetPropertyPath("Character", "Test.img", "nonexistent/path");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}
