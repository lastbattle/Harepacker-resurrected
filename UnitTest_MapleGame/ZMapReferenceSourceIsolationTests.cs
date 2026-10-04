using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Character;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class ZMapReferenceSourceIsolationTests
{
    [Fact]
    public void SourceSpecificMappings_RemainStableWhenSourcesAreInterleaved()
    {
        // smap encodes slots as concatenated two-character tokens.
        var first = new FixtureSource("firstLayer", "AaBb");
        var second = new FixtureSource("secondLayer", "CcDd");

        Assert.True(ZMapReference.HasZLayer("firstLayer", first));
        Assert.False(ZMapReference.HasZLayer("firstLayer", second));
        Assert.True(ZMapReference.HasZLayer("firstLayer", first));
        Assert.Equal(new[] { "Aa", "Bb" }, ZMapReference.GetSlotTokens("firstLayer", first));
        Assert.Equal(new[] { "Cc", "Dd" }, ZMapReference.GetSlotTokens("secondLayer", second));
    }

    private sealed class FixtureSource : IRuntimeAssetSource
    {
        private readonly WzImage _zMap;
        private readonly WzImage _sMap;

        public FixtureSource(string layer, string slot)
        {
            _zMap = new WzImage("zmap.img") { Parsed = true };
            _zMap.AddProperty(new WzSubProperty(layer));
            _sMap = new WzImage("smap.img") { Parsed = true };
            _sMap.AddProperty(new WzStringProperty(layer, slot));
        }

        public string Name => "zmap fixture";
        public bool IsInitialized => true;
        public RuntimeAssetSourceOwnership Ownership => RuntimeAssetSourceOwnership.Borrowed;
        public RuntimeAssetSourceVersion Version => null;

        public WzImage FindImage(string category, string imageName) =>
            category.Equals("base", StringComparison.OrdinalIgnoreCase)
                ? imageName.Equals("zmap.img", StringComparison.OrdinalIgnoreCase) ? _zMap
                : imageName.Equals("smap.img", StringComparison.OrdinalIgnoreCase) ? _sMap
                : null
                : null;

        public WzObject FindObject(string category, string name) => null;
        public IReadOnlyList<WzImage> GetImagesInCategory(string category) => Array.Empty<WzImage>();
        public IReadOnlyList<WzImage> GetImagesInDirectory(string category, string subDirectory) => Array.Empty<WzImage>();
        public IReadOnlyList<string> GetImageNamesInDirectory(string category, string subDirectory) => Array.Empty<string>();
        public bool ImageExists(string category, string imageName) => FindImage(category, imageName) != null;
        public bool CategoryExists(string category) => category.Equals("base", StringComparison.OrdinalIgnoreCase);
        public IReadOnlyList<string> GetCategories() => new[] { "base" };
        public IReadOnlyList<string> GetSubdirectories(string category) => Array.Empty<string>();
        public IReadOnlyList<WzDirectory> GetDirectories(string baseCategory) => Array.Empty<WzDirectory>();
        public void PreloadCategory(string category) { }
    }
}
