using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

public class AudioToolsTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    private readonly AudioTools _tools;

    public AudioToolsTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.InitializeDataSource();
        _tools = new AudioTools(_fixture.Session);
    }

    [Fact]
    public void GetSoundInfo_WithNoSound_Fails()
    {
        // Our test data doesn't have actual sound properties
        var result = _tools.GetSoundInfo("Character", "Test.img", "testString");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListSoundsInImage_WithNoSounds_ReturnsEmpty()
    {
        // Character/Test.img has no sound properties
        var result = _tools.ListSoundsInImage("Character", "Test.img");

        Assert.True(result.Success);
        Assert.NotNull(result.Sounds);
        Assert.Empty(result.Sounds);
    }

    [Fact]
    public void GetSoundData_WithInvalidPath_Fails()
    {
        var result = _tools.GetSoundData("Character", "Test.img", "nonexistent");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ResolveSoundLink_WithNonSound_Fails()
    {
        var result = _tools.ResolveSoundLink("Character", "Test.img", "testString");

        Assert.False(result.Success);
        Assert.Contains("not a sound", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
