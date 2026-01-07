using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP.Tests;

/// <summary>
/// Integration tests against real MapleStory data.
/// Set HAMCP_TEST_DATA_PATH environment variable to run these tests.
/// These tests are skipped if the data path is not set or doesn't exist.
/// </summary>
public class IntegrationTests : IDisposable
{
    private static readonly string? RealDataPath = Environment.GetEnvironmentVariable("HAMCP_TEST_DATA_PATH");
    private readonly WzSessionManager _session;
    private readonly bool _dataExists;
    private readonly FileTools? _fileTools;

    // Discovered test resources (populated during init)
    private string? _testMobImage;
    private string? _testMobAnimation;
    private string? _testMapCategory;
    private string? _testMapImage;

    public IntegrationTests()
    {
        _dataExists = !string.IsNullOrEmpty(RealDataPath)
            && Directory.Exists(RealDataPath)
            && File.Exists(Path.Combine(RealDataPath, "manifest.json"));
        _session = new WzSessionManager();

        if (_dataExists)
        {
            _session.InitDataSource(RealDataPath!);
            _fileTools = new FileTools(_session);
            DiscoverTestResources();
        }
    }

    private void DiscoverTestResources()
    {
        // Find a mob image with animations
        var mobDir = Path.Combine(RealDataPath!, "Mob");
        if (Directory.Exists(mobDir))
        {
            var mobFiles = Directory.GetFiles(mobDir, "*.img").Take(20).ToArray();
            var imageTools = new ImageTools(_session);
            var navTools = new NavigationTools(_session);

            foreach (var mobFile in mobFiles)
            {
                var mobName = Path.GetFileName(mobFile);

                // Check if this mob has animation-like properties
                var props = navTools.ListProperties("Mob", mobName, null, compact: true, limit: 50);
                if (!props.Success || props.Data?.Properties == null) continue;

                // Look for common animation names
                var commonAnims = new[] { "stand", "move", "hit1", "die1", "attack1", "fly" };
                var foundAnim = props.Data.Properties
                    .Select(p => p.Name)
                    .FirstOrDefault(n => commonAnims.Contains(n, StringComparer.OrdinalIgnoreCase));

                if (foundAnim != null)
                {
                    _testMobImage = mobName;
                    _testMobAnimation = foundAnim;
                    break;
                }
            }
        }

        // Find first available map
        var mapDir = Path.Combine(RealDataPath!, "Map");
        if (Directory.Exists(mapDir))
        {
            // Try common map subdirectory patterns
            foreach (var subDir in new[] { "Map/Map0", "Map0", "" })
            {
                var fullPath = string.IsNullOrEmpty(subDir)
                    ? mapDir
                    : Path.Combine(mapDir, subDir.Replace("/", Path.DirectorySeparatorChar.ToString()));

                if (Directory.Exists(fullPath))
                {
                    var mapFiles = Directory.GetFiles(fullPath, "*.img").Take(1).ToArray();
                    if (mapFiles.Length > 0)
                    {
                        _testMapCategory = string.IsNullOrEmpty(subDir) ? "Map" : $"Map/{subDir}";
                        _testMapImage = Path.GetFileName(mapFiles[0]);
                        break;
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    [SkippableFact]
    public void GetAnimationFrames_WithRealMob_MetadataOnly_IsSmall()
    {
        Skip.IfNot(_dataExists, "Real data path not available");
        Skip.If(_testMobImage == null || _testMobAnimation == null, "No mob animation found");

        var tools = new ImageTools(_session);
        var result = tools.GetAnimationFrames("Mob", _testMobImage!, _testMobAnimation!, metadataOnly: true, limit: 5);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Data?.Frames);
        Assert.True(result.Data.Frames.Count <= 5);
        Assert.All(result.Data.Frames, f => Assert.Null(f.Base64Png));
    }

    [SkippableFact]
    public void GetAnimationFrames_WithRealMob_WithImageData_HasBase64()
    {
        Skip.IfNot(_dataExists, "Real data path not available");
        Skip.If(_testMobImage == null || _testMobAnimation == null, "No mob animation found");

        var tools = new ImageTools(_session);
        var result = tools.GetAnimationFrames("Mob", _testMobImage!, _testMobAnimation!, metadataOnly: false, limit: 2);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Data?.Frames);
        Assert.True(result.Data.Frames.Count <= 2);
        Assert.All(result.Data.Frames, f => Assert.NotNull(f.Base64Png));
    }

    [SkippableFact]
    public void ListProperties_WithRealMap_Pagination_Works()
    {
        Skip.IfNot(_dataExists, "Real data path not available");
        Skip.If(_testMapCategory == null || _testMapImage == null, "No map found");

        var tools = new NavigationTools(_session);
        var result = tools.ListProperties(_testMapCategory!, _testMapImage!, null, compact: true, offset: 0, limit: 5);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Data?.Properties);
        Assert.True(result.Data.Properties.Count <= 5);
        Assert.True(result.Data.TotalCount > 0);
        Assert.Equal(0, result.Data.Offset);
        Assert.Equal(5, result.Data.Limit);
    }

    [SkippableFact]
    public void SearchByName_WithRealData_CompactMode_IsSmall()
    {
        Skip.IfNot(_dataExists, "Real data path not available");

        var tools = new NavigationTools(_session);
        var result = tools.SearchByName("*", category: "Mob", maxResults: 10, compact: true);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Data?.Matches);
        foreach (var match in result.Data.Matches)
        {
            Assert.Null(match.Name);
            Assert.Null(match.Type);
            Assert.NotNull(match.Path);
        }
    }

    [SkippableFact]
    public void GetChildren_WithRealData_Pagination_Works()
    {
        Skip.IfNot(_dataExists, "Real data path not available");

        var tools = new PropertyTools(_session);
        var result = tools.GetChildren("String", "Mob.img", null, compact: true, offset: 0, limit: 10);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Data?.Children);
        Assert.True(result.Data.Children.Count <= 10);
        Assert.True(result.Data.TotalCount > 10);
        Assert.True(result.Data.HasMore);
    }

    [SkippableFact]
    public void GetTreeStructure_WithRealData_LimitsDepthAndChildren()
    {
        Skip.IfNot(_dataExists, "Real data path not available");
        Skip.If(_testMobImage == null, "No mob found");

        var tools = new NavigationTools(_session);
        var result = tools.GetTreeStructure("Mob", _testMobImage!, depth: 2, maxChildrenPerNode: 5);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Data?.Tree);

        if (result.Data.Tree.Children != null)
        {
            Assert.True(result.Data.Tree.Children.Count <= 5);
        }
    }

    [SkippableFact]
    public void ExportToJson_WithRealData_EnforcesInlineLimit()
    {
        Skip.IfNot(_dataExists, "Real data path not available");
        Skip.If(_testMobImage == null, "No mob found");

        var tools = new ExportTools(_session);
        var result = tools.ExportToJson("Mob", _testMobImage!, maxDepth: 10);

        if (!result.Success)
        {
            Assert.Contains("100KB", result.Error);
        }
    }

    [SkippableFact]
    public void ResponseSizeComparison_MetadataOnlyVsFull()
    {
        Skip.IfNot(_dataExists, "Real data path not available");
        Skip.If(_testMobImage == null || _testMobAnimation == null, "No mob animation found");

        var tools = new ImageTools(_session);
        var metadataResult = tools.GetAnimationFrames("Mob", _testMobImage!, _testMobAnimation!, metadataOnly: true, limit: 10);
        var fullResult = tools.GetAnimationFrames("Mob", _testMobImage!, _testMobAnimation!, metadataOnly: false, limit: 10);

        Assert.True(metadataResult.Success, metadataResult.Error);
        Assert.True(fullResult.Success, fullResult.Error);

        var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadataResult);
        var fullJson = System.Text.Json.JsonSerializer.Serialize(fullResult);

        Assert.True(metadataJson.Length < fullJson.Length,
            $"MetadataOnly ({metadataJson.Length} bytes) should be smaller than full ({fullJson.Length} bytes)");

        var ratio = (double)fullJson.Length / metadataJson.Length;
        Console.WriteLine($"Size comparison: MetadataOnly={metadataJson.Length} bytes, Full={fullJson.Length} bytes, Ratio={ratio:F1}x");
    }
}
