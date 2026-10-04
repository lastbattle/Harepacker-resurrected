using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Pools;
using MapleLib.WzLib;
using Microsoft.Xna.Framework.Graphics;
using Moq;
using System.Runtime.CompilerServices;

namespace UnitTest_MapSimulator;

public sealed class CharacterLoaderLookupTests
{
    [Fact]
    public void LoadFaceUsesTargetedImageLookupInsteadOfCategoryRoot()
    {
        using var image = new WzImage("00000000.img") { Parsed = true };
        var assets = new SpyAssetSource(image);
        var catalog = new Mock<IRuntimeAssetCatalog>();
        catalog.SetupGet(value => value.Assets).Returns(assets);
        var services = new RuntimeDataServices(assets, catalog.Object);
        using var texturePool = new TexturePool();
        var device = (GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice));
        var loader = new CharacterLoader(null, device, texturePool, services);

        Assert.NotNull(loader.LoadFace(0));
        Assert.Equal("Character", assets.LastImageCategory);
        Assert.Equal("Face/00000000.img", assets.LastImagePath);
        Assert.False(assets.RootLookupRequested);
    }

    private sealed class SpyAssetSource(WzImage faceImage) : IRuntimeAssetSource
    {
        public string Name => "fixture";
        public bool IsInitialized => true;
        public RuntimeAssetSourceOwnership Ownership => RuntimeAssetSourceOwnership.Borrowed;
        public RuntimeAssetSourceVersion Version => null;
        public string LastImageCategory { get; private set; }
        public string LastImagePath { get; private set; }
        public bool RootLookupRequested { get; private set; }

        public WzImage FindImage(string category, string imageName)
        {
            LastImageCategory = category;
            LastImagePath = imageName;
            return string.Equals(category, "Character", StringComparison.OrdinalIgnoreCase)
                && string.Equals(imageName, "Face/00000000.img", StringComparison.OrdinalIgnoreCase)
                ? faceImage
                : null;
        }

        public WzObject FindObject(string category, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                RootLookupRequested = true;
            throw new InvalidOperationException("Category-root lookup is not allowed in this test.");
        }

        public IReadOnlyList<WzImage> GetImagesInCategory(string category) => Array.Empty<WzImage>();
        public IReadOnlyList<WzImage> GetImagesInDirectory(string category, string subDirectory) => Array.Empty<WzImage>();
        public IReadOnlyList<string> GetImageNamesInDirectory(string category, string subDirectory) => Array.Empty<string>();
        public bool ImageExists(string category, string imageName) => false;
        public bool CategoryExists(string category) => false;
        public IReadOnlyList<string> GetCategories() => Array.Empty<string>();
        public IReadOnlyList<string> GetSubdirectories(string category) => Array.Empty<string>();
        public IReadOnlyList<WzDirectory> GetDirectories(string baseCategory) => Array.Empty<WzDirectory>();
        public void PreloadCategory(string category) { }
    }
}
