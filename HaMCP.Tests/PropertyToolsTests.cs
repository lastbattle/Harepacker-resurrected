using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class PropertyToolsTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly PropertyTools _tools;

    public PropertyToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new PropertyTools(_fixture.Session);
    }

    [Fact]
    public void GetProperty_ReturnsPropertyData()
    {
        var result = _tools.GetProperty("Character", "Test.img", "testString");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Property);
        Assert.Equal("testString", result.Data.Property.Name);
        Assert.Equal("String", result.Data.Property.Type);
    }

    [Fact]
    public void GetProperty_WithInvalidPath_Fails()
    {
        var result = _tools.GetProperty("Character", "Test.img", "nonexistent");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void GetPropertyValue_ReturnsValue()
    {
        var result = _tools.GetPropertyValue("Character", "Test.img", "testInt");

        Assert.True(result.Success);
        Assert.Equal(42, result.Data?.Value);
    }

    [Fact]
    public void GetString_ReturnsStringValue()
    {
        var result = _tools.GetString("Character", "Test.img", "testString");

        Assert.True(result.Success);
        Assert.Equal("Hello World", result.Data?.Value);
    }

    [Fact]
    public void GetString_WithNonString_ReturnsNotFound()
    {
        var result = _tools.GetString("Character", "Test.img", "testInt");

        Assert.True(result.Success);
        Assert.False(result.Data?.Found);
        Assert.Null(result.Data?.Value);
    }

    [Fact]
    public void GetInt_ReturnsIntValue()
    {
        var result = _tools.GetInt("Character", "Test.img", "testInt");

        Assert.True(result.Success);
        Assert.Equal(42, result.Data?.Value);
    }

    [Fact]
    public void GetFloat_ReturnsFloatValue()
    {
        var result = _tools.GetFloat("Character", "Test.img", "testFloat");

        Assert.True(result.Success);
        Assert.True(result.Data?.Found);
        Assert.NotNull(result.Data?.Value);
        Assert.True(Math.Abs(result.Data.Value.Value - 3.14f) < 0.01f);
    }

    [Fact]
    public void GetVector_ReturnsVectorValue()
    {
        var result = _tools.GetVector("Character", "Test.img", "testVector");

        Assert.True(result.Success);
        Assert.Equal(100, result.Data?.X);
        Assert.Equal(200, result.Data?.Y);
    }

    [Fact]
    public void ResolveUol_ResolvesToTarget()
    {
        var result = _tools.ResolveUol("Character", "Test.img", "testUol");

        Assert.True(result.Success);
        Assert.Equal("info/name", result.Data?.UolPath);
    }

    [Fact]
    public void GetChildren_ReturnsChildProperties()
    {
        var result = _tools.GetChildren("Character", "Test.img", "info");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Children);
        Assert.True(result.Data.Children.Count >= 2);
    }

    [Fact]
    public void GetChildren_Pagination_ReturnsCorrectPage()
    {
        var result = _tools.GetChildren("Character", "Test.img", "info", compact: true, offset: 0, limit: 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Children);
        Assert.Single(result.Data.Children);
        Assert.True(result.Data.TotalCount >= 2);
        Assert.Equal(0, result.Data.Offset);
        Assert.Equal(1, result.Data.Limit);
        Assert.True(result.Data.HasMore);
    }

    [Fact]
    public void GetChildren_CompactMode_ExcludesFullPath()
    {
        var result = _tools.GetChildren("Character", "Test.img", "info", compact: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Children);
        // Compact mode excludes FullPath
        Assert.All(result.Data.Children, c => Assert.Null(c.FullPath));
    }

    [Fact]
    public void GetChildren_NonCompactMode_IncludesFullPath()
    {
        var result = _tools.GetChildren("Character", "Test.img", "info", compact: false);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Children);
        // Non-compact includes FullPath
        Assert.All(result.Data.Children, c => Assert.NotNull(c.FullPath));
    }

    [Fact]
    public void GetPropertyCount_ReturnsCount()
    {
        var result = _tools.GetPropertyCount("Character", "Test.img", "info");

        Assert.True(result.Success);
        Assert.True(result.Data?.Count >= 2);
    }

    [Fact]
    public void IterateProperties_ReturnsPaginatedResults()
    {
        var result = _tools.IterateProperties("Character", "Test.img", null, 0, 3);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Properties);
        Assert.True(result.Data.Properties.Count <= 3);
        Assert.True(result.Data.TotalCount > 0);
    }

    [Fact]
    public void GetPropertiesBatch_ReturnsMultipleProperties()
    {
        var requests = new List<PropertyRequest>
        {
            new PropertyRequest { Category = "Character", Image = "Test.img", Path = "testString" },
            new PropertyRequest { Category = "Character", Image = "Test.img", Path = "testInt" },
            new PropertyRequest { Category = "Character", Image = "Test.img", Path = "nonexistent" }
        };

        var result = _tools.GetPropertiesBatch(requests);

        Assert.True(result.Success);
        Assert.Equal(3, result.Data?.TotalRequested);
        Assert.Equal(2, result.Data?.SuccessCount);
        Assert.Equal(1, result.Data?.FailedCount);
    }
}
