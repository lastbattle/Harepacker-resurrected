using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator;

public class RuntimeMapSnapshotTests
{
    [Fact]
    public void DefinitionOwnsMutableMapEntityAndBinaryInputs()
    {
        var backingImage = ImageWithValue("map.img", "backing", 31);
        var sourceInfo = new MapInfo
        {
            id = 100000000,
            strMapName = "Snapshot",
            protectItem = [100, 200],
            allowedItem = [300],
            Image = backingImage
        };
        var unsupported = new WzIntProperty("unsupported", 41);
        var additional = new WzIntProperty("additional", 42);
        sourceInfo.unsupportedInfoProperties.Add(unsupported);
        sourceInfo.additionalProps.Add(additional);

        byte[] minimap = [1, 2, 3];
        byte[] preview = [4, 5, 6];
        var assetImage = ImageWithValue("asset.img", "frame", 51);
        var key = new RuntimeAssetKey("Map/Obj", "memory:test");
        using var inputOverride = new RuntimeOwnedAssetOverride(key, assetImage, preview);
        var quests = new List<RuntimeObjectQuestDefinition> { new(7, 1) };
        var objects = new List<RuntimeObjectDefinition>
        {
            new() { Asset = key, X = 10, Y = 20, Quests = quests }
        };

        using var definition = CreateDefinition(sourceInfo, minimap, objects, [inputOverride]);

        sourceInfo.strMapName = "Mutated";
        sourceInfo.protectItem[0] = -1;
        sourceInfo.allowedItem.Clear();
        unsupported.Value = -2;
        additional.Value = -3;
        ((WzIntProperty)backingImage["backing"]).Value = -4;
        minimap[0] = 99;
        preview[0] = 99;
        quests[0] = new RuntimeObjectQuestDefinition(8, 2);
        objects.Clear();
        ((WzIntProperty)assetImage["frame"]).Value = -5;

        MapInfo first = definition.CreateMapInfo();
        Assert.Equal("Snapshot", first.strMapName);
        Assert.Equal([100, 200], first.protectItem);
        Assert.Equal([300], first.allowedItem);
        Assert.Equal(41, ((WzIntProperty)first.unsupportedInfoProperties.Single()).Value);
        Assert.Equal(42, ((WzIntProperty)first.additionalProps.Single()).Value);
        Assert.Equal(31, ((WzIntProperty)first.Image["backing"]).Value);
        Assert.Equal(new byte[] { 1, 2, 3 }, definition.CreateMinimapPngCopy());
        Assert.Equal(new byte[] { 4, 5, 6 }, definition.CreateAssetPreviewPngCopy(key));
        Assert.Equal(new RuntimeObjectQuestDefinition(7, 1), Assert.Single(Assert.Single(definition.Objects).Quests));

        first.protectItem[0] = 999;
        ((WzIntProperty)first.Image["backing"]).Value = 999;
        MapInfo second = definition.CreateMapInfo();
        Assert.Equal(100, second.protectItem[0]);
        Assert.Equal(31, ((WzIntProperty)second.Image["backing"]).Value);

        Assert.True(definition.TryCreateAssetImageRoot(key, out WzImage firstAsset));
        ((WzIntProperty)firstAsset["frame"]).Value = 999;
        Assert.True(definition.TryCreateAssetImageRoot(key, out WzImage secondAsset));
        Assert.Equal(51, ((WzIntProperty)secondAsset["frame"]).Value);
        firstAsset.Dispose();
        secondAsset.Dispose();
        first.Image.Dispose();
        second.Image.Dispose();
        backingImage.Dispose();
        assetImage.Dispose();
    }

    private static RuntimeMapDefinition CreateDefinition(
        MapInfo mapInfo,
        byte[] minimap,
        IEnumerable<RuntimeObjectDefinition> objects,
        IEnumerable<RuntimeOwnedAssetOverride> overrides)
    {
        return new RuntimeMapDefinition(
            mapInfo,
            new Point(200, 100),
            new Point(100, 50),
            null,
            Rectangle.Empty,
            System.Drawing.Point.Empty,
            minimap,
            [],
            objects,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            overrides);
    }

    private static WzImage ImageWithValue(string name, string propertyName, int value)
    {
        var image = new WzImage(name) { Parsed = true };
        image.AddProperty(new WzIntProperty(propertyName, value));
        image.Changed = true;
        return image;
    }

}
