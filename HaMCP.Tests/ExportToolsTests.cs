using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class ExportToolsTests : IClassFixture<TestFixture>, IDisposable
{
    private readonly TestFixture _fixture;
    private readonly ExportTools _tools;
    private readonly string _exportPath;

    public ExportToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new ExportTools(_fixture.Session);
        _exportPath = Path.Combine(Path.GetTempPath(), $"HaMCP_Export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_exportPath);
    }

    [Fact]
    public void ExportToJson_ReturnsJsonString()
    {
        var result = _tools.ExportToJson("Character", "Test.img", maxDepth: 3);

        Assert.True(result.Success);
        Assert.NotNull(result.JsonData);
        Assert.Contains("testString", result.JsonData);
    }

    [Fact]
    public void ExportToJson_WithPath_ExportsSubtree()
    {
        var result = _tools.ExportToJson("Character", "Test.img", "info");

        Assert.True(result.Success);
        Assert.NotNull(result.JsonData);
    }

    [Fact]
    public void ExportToJson_ToFile_CreatesFile()
    {
        var outputPath = Path.Combine(_exportPath, "export.json");
        var result = _tools.ExportToJson("Character", "Test.img", null, 3, outputPath);

        Assert.True(result.Success);
        Assert.Equal(outputPath, result.OutputPath);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void ExportToXml_CreatesFile()
    {
        var outputPath = Path.Combine(_exportPath, "export.xml");
        var result = _tools.ExportToXml("Character", "Test.img", outputPath);

        Assert.True(result.Success);
        Assert.Equal(outputPath, result.OutputPath);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void ExportPng_CreatesFile()
    {
        var outputPath = Path.Combine(_exportPath, "canvas.png");
        var result = _tools.ExportPng("Character", "Test.img", "testCanvas", outputPath);

        Assert.True(result.Success);
        Assert.Equal(outputPath, result.OutputPath);
        Assert.Equal(32, result.Width);
        Assert.Equal(32, result.Height);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void ExportPng_WithInvalidPath_Fails()
    {
        var outputPath = Path.Combine(_exportPath, "invalid.png");
        var result = _tools.ExportPng("Character", "Test.img", "nonexistent", outputPath);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ExportAllImages_ExportsCanvases()
    {
        var outputDir = Path.Combine(_exportPath, "all_images");
        var result = _tools.ExportAllImages("Character", "Test.img", outputDir);

        Assert.True(result.Success);
        Assert.True(result.ExportedCount > 0);
        Assert.True(Directory.Exists(outputDir));
    }

    [Fact]
    public void ExportAllSounds_WithNoSounds_ReturnsEmpty()
    {
        var outputDir = Path.Combine(_exportPath, "all_sounds");
        var result = _tools.ExportAllSounds("Character", "Test.img", outputDir);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExportedCount);
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
