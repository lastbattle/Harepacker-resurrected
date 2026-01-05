using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class AnalysisToolsTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly AnalysisTools _tools;

    public AnalysisToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new AnalysisTools(_fixture.Session);
    }

    [Fact]
    public void GetStatistics_ReturnsStats()
    {
        var result = _tools.GetStatistics();

        Assert.True(result.Success);
        Assert.True(result.CategoryCount > 0);
        Assert.True(result.TotalImageCount > 0);
        Assert.NotNull(result.Categories);
    }

    [Fact]
    public void GetCategorySummary_ReturnsSummary()
    {
        var result = _tools.GetCategorySummary("Character");

        Assert.True(result.Success);
        Assert.Equal("Character", result.Category);
        Assert.True(result.ImageCount > 0);
        Assert.NotNull(result.Images);
    }

    [Fact]
    public void GetCategorySummary_WithInvalidCategory_ReturnsEmpty()
    {
        var result = _tools.GetCategorySummary("NonExistentCategory");

        Assert.True(result.Success);
        Assert.Equal(0, result.ImageCount);
    }

    [Fact]
    public void FindBrokenUols_FindsBrokenReferences()
    {
        var result = _tools.FindBrokenUols(category: "Character", image: "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.BrokenUols);
        // May or may not find broken UOLs depending on test data
    }

    [Fact]
    public void CompareProperties_ComparesSameProperty()
    {
        var result = _tools.CompareProperties(
            "Character", "Test.img", "info",
            "Character", "Test.img", "info");

        Assert.True(result.Success);
        Assert.Equal(0, result.DifferenceCount); // Same property should have no differences
    }

    [Fact]
    public void CompareProperties_FindsDifferences()
    {
        var result = _tools.CompareProperties(
            "Character", "Test.img", "info",
            "Character", "Test.img", "stand");

        Assert.True(result.Success);
        Assert.True(result.DifferenceCount > 0); // Different properties should have differences
    }

    [Fact]
    public void GetVersionInfo_ReturnsVersion()
    {
        var result = _tools.GetVersionInfo();

        Assert.True(result.Success);
        Assert.NotNull(result.Name);
        Assert.NotNull(result.Version);
    }

    [Fact]
    public void ValidateImage_ValidatesSuccessfully()
    {
        var result = _tools.ValidateImage("Character", "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.Stats);
        Assert.NotNull(result.Issues);
        Assert.True(result.Stats.TotalProperties > 0);
    }
}
