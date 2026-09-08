using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapEditor.Simulation;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Interaction;
using HaCreator.Wz;
using MapleLib.WzLib;

namespace UnitTest_MapSimulator;

public sealed class EditorRuntimeCatalogTests
{
    [Fact]
    public void SocialRoomResolvesSampleIdentityUsingItsOwnCatalog()
    {
        var source = new EmptySource();
        var firstInfo = new WzInformationManager();
        firstInfo.ItemNameCache[1082002] = Tuple.Create("Glove", "Brown Work Gloves", "First source");
        var secondInfo = new WzInformationManager();
        secondInfo.ItemNameCache[1082003] = Tuple.Create("Glove", "Brown Work Gloves", "Second source");
        var first = SocialRoomRuntime.CreatePersonalShopSample(new RuntimeDataServices(source,
            new HaCreatorRuntimeAssetCatalog(firstInfo, source)));
        var second = SocialRoomRuntime.CreatePersonalShopSample(new RuntimeDataServices(source,
            new HaCreatorRuntimeAssetCatalog(secondInfo, source)));

        Assert.Equal(1082002, first.Items[0].ItemId);
        Assert.Equal(1082003, second.Items[0].ItemId);
        Assert.Equal(1082002, first.Items[0].ItemId);
        Assert.Equal(0, new SocialRoomItemEntry("Owner", "Brown Work Gloves", 1, 0, "").ItemId);
    }

    [Fact]
    public void PreviewNamesSurviveEditorCacheReplacement()
    {
        var information = new WzInformationManager();
        information.MapsNameCache["100000000"] = Tuple.Create("Street", "Unsaved name", "Victoria");
        information.ItemNameCache[2000000] = Tuple.Create("Consume", "Potion", "Restores HP");
        information.NpcNameCache["1000000"] = Tuple.Create("Guide", "Helper");
        var catalog = new HaCreatorRuntimeAssetCatalog(information, new EmptySource());

        information.MapsNameCache.Clear();
        information.ItemNameCache[2000000] = Tuple.Create("Cash", "Other source", "Other description");
        information.NpcNameCache.Clear();

        Assert.True(catalog.TryGetMapName("100000000", out var map));
        Assert.Equal("Unsaved name", map.MapName);
        Assert.Equal("Street", map.StreetName);
        Assert.True(catalog.TryGetItemName(2000000, out var item));
        Assert.Equal("Potion", item.Name);
        Assert.True(catalog.TryGetNpcName("1000000", out var npc));
        Assert.Equal("Guide", npc.Name);
        Assert.Equal("Unsaved name", catalog.GetMapNames()["100000000"].MapName);
    }

    [Fact]
    public void EnhancementOwnerPathsRemainIsolatedAcrossSourceCatalogs()
    {
        using var firstImage = EnhancementImage("UI/first");
        using var secondImage = EnhancementImage("UI/second");
        var firstSource = new EmptySource(firstImage);
        var secondSource = new EmptySource(secondImage);
        var first = new HaCreatorRuntimeAssetCatalog(new WzInformationManager(), firstSource);
        var second = new HaCreatorRuntimeAssetCatalog(new WzInformationManager(), secondSource);

        Assert.Equal("UI/first", ItemUpgradeUI.ResolveConsumableOwnerPathForTests(first, 5062000));
        Assert.Equal("UI/second", ItemUpgradeUI.ResolveConsumableOwnerPathForTests(second, 5062000));
        Assert.Equal("UI/first", ItemUpgradeUI.ResolveConsumableOwnerPathForTests(first, 5062000));
    }

    private static WzImage EnhancementImage(string path)
    {
        var image = new WzImage("0506.img") { Parsed = true };
        var item = new WzSubProperty("05062000");
        var info = new WzSubProperty("info");
        info.AddProperty(new WzStringProperty("path", path));
        item.AddProperty(info);
        image.AddProperty(item);
        return image;
    }
    private sealed class EmptySource : IRuntimeAssetSource
    {
        private readonly WzImage? image;
        public EmptySource(WzImage? image = null) => this.image = image;
        public string Name => "empty fixture";
        public bool IsInitialized => true;
        public RuntimeAssetSourceOwnership Ownership => RuntimeAssetSourceOwnership.Borrowed;
        public RuntimeAssetSourceVersion Version => null;
        public WzImage FindImage(string category, string imageName) => category == "Item" && imageName == "Cash/0506.img" ? image : null;
        public WzObject FindObject(string category, string name) => null;
        public IReadOnlyList<WzImage> GetImagesInCategory(string category) => [];
        public IReadOnlyList<WzImage> GetImagesInDirectory(string category, string subDirectory) => [];
        public IReadOnlyList<string> GetImageNamesInDirectory(string category, string subDirectory) => [];
        public bool ImageExists(string category, string imageName) => false;
        public bool CategoryExists(string category) => false;
        public IReadOnlyList<string> GetCategories() => [];
        public IReadOnlyList<string> GetSubdirectories(string category) => [];
        public IReadOnlyList<WzDirectory> GetDirectories(string baseCategory) => [];
        public void PreloadCategory(string category) { }
    }
}
