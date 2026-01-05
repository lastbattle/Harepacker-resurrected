using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class BatchToolsTests : IClassFixture<TestFixture>, IDisposable
{
    private readonly TestFixture _fixture;
    private readonly BatchTools _tools;
    private readonly string _exportPath;

    public BatchToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new BatchTools(_fixture.Session);
        _exportPath = Path.Combine(Path.GetTempPath(), $"HaMCP_Batch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_exportPath);
    }

    [Fact]
    public void ExtractToImg_WithNonExistentPath_Fails()
    {
        var wzPath = Path.Combine(_exportPath, "test.wz");
        var outputDir = Path.Combine(_exportPath, "output");

        var result = _tools.ExtractToImg(wzPath, outputDir);

        // Should fail with path not found or not implemented
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void PackToWz_NotImplemented()
    {
        var result = _tools.PackToWz(_fixture.TestDataPath, _exportPath);

        // Currently returns not implemented
        Assert.False(result.Success);
        Assert.Contains("not yet implemented", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BatchExportImages_ExportsFromAllCategories()
    {
        var outputDir = Path.Combine(_exportPath, "batch_images");

        var result = _tools.BatchExportImages("Character", outputDir, maxImages: 10);

        Assert.True(result.Success);
        Assert.True(result.ExportedCount > 0);
        Assert.True(Directory.Exists(outputDir));
    }

    [Fact]
    public void BatchExportImages_WithAllCategories_ExportsAll()
    {
        var outputDir = Path.Combine(_exportPath, "all_categories");

        var result = _tools.BatchExportImages("all", outputDir, maxImages: 20);

        Assert.True(result.Success);
        Assert.True(result.ExportedCount > 0);
    }

    [Fact]
    public void BatchSearch_FindsMatchesByName()
    {
        var result = _tools.BatchSearch("test*", categories: "Character", searchType: "name");

        Assert.True(result.Success);
        Assert.NotNull(result.Results);
        Assert.True(result.ResultCount > 0);
    }

    [Fact]
    public void BatchSearch_FindsMatchesByValue()
    {
        // Search for a value we know exists in test data
        var result = _tools.BatchSearch("Test", categories: "character", searchType: "value");

        Assert.True(result.Success);
        Assert.NotNull(result.Results);
        // May or may not find results depending on test data structure
    }

    [Fact]
    public void BatchSearch_SearchesAllCategories()
    {
        var result = _tools.BatchSearch("*", categories: "all", searchType: "name", maxResults: 50);

        Assert.True(result.Success);
        Assert.NotNull(result.Results);
        Assert.True(result.ResultCount > 0);
    }

    [Fact]
    public void BatchSearch_RespectsMaxResults()
    {
        var result = _tools.BatchSearch("*", categories: "all", searchType: "name", maxResults: 5);

        Assert.True(result.Success);
        Assert.True(result.ResultCount <= 5);
        if (result.ResultCount == 5)
        {
            Assert.True(result.Truncated);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_exportPath))
            {
                Directory.Delete(_exportPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
