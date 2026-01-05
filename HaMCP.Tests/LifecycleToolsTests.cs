using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class LifecycleToolsTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly LifecycleTools _tools;

    public LifecycleToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new LifecycleTools(_fixture.Session);
    }

    [Fact]
    public void ParseImage_ParsesAndReturnsCount()
    {
        var result = _tools.ParseImage("Character", "Test.img");

        Assert.True(result.Success);
        Assert.True(result.PropertyCount > 0);
    }

    [Fact]
    public void IsImageParsed_ReturnsParsedStatus()
    {
        // First parse the image
        _tools.ParseImage("Character", "Test.img");

        var result = _tools.IsImageParsed("Character", "Test.img");

        Assert.True(result.Success);
        Assert.True(result.IsParsed);
    }

    [Fact]
    public void UnparseImage_FreesMemory()
    {
        // First parse
        _tools.ParseImage("Character", "Test.img");

        // Then unparse
        var result = _tools.UnparseImage("Character", "Test.img");

        Assert.True(result.Success);

        // Verify unparsed
        var checkResult = _tools.IsImageParsed("Character", "Test.img");
        Assert.False(checkResult.IsParsed);
    }

    [Fact]
    public void GetParsedImages_ListsParsedImages()
    {
        // Parse an image first
        _tools.ParseImage("Character", "Test.img");

        var result = _tools.GetParsedImages();

        Assert.True(result.Success);
        Assert.NotNull(result.Images);
        Assert.True(result.Images.Count > 0);
    }

    [Fact]
    public void PreloadCategory_LoadsAllImages()
    {
        var result = _tools.PreloadCategory("Character");

        Assert.True(result.Success);
        Assert.True(result.LoadedCount > 0);
    }

    [Fact]
    public void UnloadCategory_FreesAllImages()
    {
        // First preload
        _tools.PreloadCategory("Character");

        // Then unload
        var result = _tools.UnloadCategory("Character");

        Assert.True(result.Success);
        Assert.True(result.UnloadedCount > 0);
    }
}
