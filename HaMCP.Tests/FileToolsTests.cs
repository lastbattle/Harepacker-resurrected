using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class FileToolsTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly FileTools _tools;

    public FileToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _tools = new FileTools(_fixture.Session);
    }

    [Fact]
    public void InitDataSource_WithValidPath_Succeeds()
    {
        var result = _tools.InitDataSource(_fixture.TestDataPath);

        Assert.True(result.Success);
        Assert.Equal(_fixture.TestDataPath, result.Data?.Path);
        Assert.NotNull(result.Data?.Categories);
        Assert.Contains(result.Data.Categories, c => c.Equals("Character", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InitDataSource_WithInvalidPath_Fails()
    {
        var result = _tools.InitDataSource("C:\\NonExistent\\Path\\12345");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ScanImgDirectories_FindsDataSources()
    {
        var result = _tools.ScanImgDirectories(_fixture.TestDataPath, false);

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.DataSources);
        Assert.True(result.Data.DataSources.Count >= 1);
    }

    [Fact]
    public void GetDataSourceInfo_WhenInitialized_ReturnsInfo()
    {
        _fixture.InitializeDataSource();

        var result = _tools.GetDataSourceInfo();

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Name);
        Assert.True(result.Data.CategoryCount > 0);
    }

    [Fact]
    public void GetDataSourceInfo_WhenNotInitialized_Fails()
    {
        using var session = new WzSessionManager();
        var tools = new FileTools(session);

        var result = tools.GetDataSourceInfo();

        Assert.False(result.Success);
        Assert.Contains("data source", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListCategories_ReturnsAllCategories()
    {
        _fixture.InitializeDataSource();

        var result = _tools.ListCategories();

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Categories);
        var categoryNames = result.Data.Categories.Select(c => c.Name).ToList();
        Assert.Contains(categoryNames, c => c.Equals("Character", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListImagesInCategory_ReturnsImages()
    {
        _fixture.InitializeDataSource();

        var result = _tools.ListImagesInCategory("Character");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Images);
        Assert.True(result.Data.Images.Count > 0);
    }

    [Fact]
    public void ListImagesInCategory_WithInvalidCategory_ReturnsEmpty()
    {
        _fixture.InitializeDataSource();

        var result = _tools.ListImagesInCategory("NonExistentCategory");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Images);
        Assert.Empty(result.Data.Images);
    }

    [Fact]
    public void GetCacheStats_ReturnsStats()
    {
        _fixture.InitializeDataSource();

        var result = _tools.GetCacheStats();

        Assert.True(result.Success);
        Assert.True(result.Data?.CacheHitRatio >= 0);
    }

    [Fact]
    public void ClearCache_Succeeds()
    {
        _fixture.InitializeDataSource();

        var result = _tools.ClearCache();

        Assert.True(result.Success);
    }
}
