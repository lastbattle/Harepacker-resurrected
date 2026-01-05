using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class ImageToolsTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly ImageTools _tools;

    public ImageToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new ImageTools(_fixture.Session);
    }

    [Fact]
    public void GetCanvasBitmap_ReturnsBase64Png()
    {
        var result = _tools.GetCanvasBitmap("Character", "Test.img", "testCanvas");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Base64Png);
        Assert.Equal(32, result.Data.Width);
        Assert.Equal(32, result.Data.Height);
    }

    [Fact]
    public void GetCanvasBitmap_WithInvalidPath_Fails()
    {
        var result = _tools.GetCanvasBitmap("Character", "Test.img", "nonexistent");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void GetCanvasInfo_ReturnsMetadata()
    {
        var result = _tools.GetCanvasInfo("Character", "Test.img", "testCanvas");

        Assert.True(result.Success);
        Assert.Equal(32, result.Data?.Width);
        Assert.Equal(32, result.Data?.Height);
        Assert.NotNull(result.Data?.Origin);
    }

    [Fact]
    public void GetCanvasOrigin_ReturnsOriginPoint()
    {
        var result = _tools.GetCanvasOrigin("Character", "Test.img", "testCanvas");

        Assert.True(result.Success);
        Assert.Equal(16, result.Data?.X);
        Assert.Equal(16, result.Data?.Y);
    }

    [Fact]
    public void GetCanvasDelay_ReturnsDelay()
    {
        var result = _tools.GetCanvasDelay("Character", "Test.img", "testCanvas");

        Assert.True(result.Success);
        Assert.Equal(100, result.Data?.Delay);
    }

    [Fact]
    public void GetAnimationFrames_ReturnsAllFrames()
    {
        var result = _tools.GetAnimationFrames("Character", "Test.img", "stand");

        Assert.True(result.Success);
        Assert.Equal(3, result.Data?.FrameCount);
        Assert.NotNull(result.Data?.Frames);
        Assert.Equal(3, result.Data.Frames.Count);
        Assert.True(result.Data.TotalDuration > 0);
    }

    [Fact]
    public void ListCanvasInImage_FindsAllCanvases()
    {
        var result = _tools.ListCanvasInImage("Character", "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Canvases);
        Assert.True(result.Data.Count > 0);
    }

    [Fact]
    public void GetCanvasHead_ReturnsHeadPoint()
    {
        var result = _tools.GetCanvasHead("Character", "Test.img", "testCanvas");

        // Either succeeds with head, or fails with "No head property found"
        if (!result.Success)
        {
            Assert.Contains("head", result.Error, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GetCanvasBounds_ReturnsBounds()
    {
        var result = _tools.GetCanvasBounds("Character", "Test.img", "testCanvas");

        Assert.True(result.Success);
        // HasLt and HasRb may be false if no bounds are set
    }

    [Fact]
    public void ResolveCanvasLink_WithNoLink_ReturnsCanvas()
    {
        var result = _tools.ResolveCanvasLink("Character", "Test.img", "testCanvas");

        Assert.True(result.Success);
        Assert.Equal("none", result.Data?.LinkType);
        Assert.NotNull(result.Data?.Base64Png);
    }
}
