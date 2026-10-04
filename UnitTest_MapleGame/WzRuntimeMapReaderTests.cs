using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Moq;
using System.Drawing;

namespace UnitTest_MapleGame;

public class WzRuntimeMapReaderTests
{
    [Fact]
    public void ReadsSyntheticMapWithoutEditorObjectsOrSourceMutation()
    {
        using WzImage map = CreateMap();
        using WzImage strings = CreateTooltipStrings();
        int originalRoots = map.WzProperties.Count;
        var catalog = new Mock<IRuntimeAssetCatalog>();
        RuntimeMobName mobName = new("Green Snail");
        RuntimeNpcName npcName = new("Maple Administrator", "");
        RuntimeReactorAsset reactorName = new("200100", "Switch", null);
        catalog.Setup(value => value.TryGetMobName("100100", out mobName)).Returns(true);
        catalog.Setup(value => value.TryGetNpcName("9000000", out npcName)).Returns(true);
        catalog.Setup(value => value.TryGetReactor("200100", out reactorName)).Returns(true);

        using RuntimeMapDefinition result = new WzRuntimeMapReader().Read(map, new WzRuntimeMapReaderOptions
        {
            MapId = 100000001,
            MapName = "Henesys",
            StreetName = "Victoria Road",
            CategoryName = "Town",
            TooltipStrings = strings,
            Catalog = catalog.Object
        });

        Assert.Equal(100000001, result.MapId);
        Assert.Equal("Henesys", result.MapName);
        Assert.Equal("Victoria Road", result.StreetName);
        Assert.Equal("Town", result.CategoryName);
        Assert.Equal(new Microsoft.Xna.Framework.Rectangle(-100, -200, 1000, 700), result.VirtualBounds);
        Assert.Equal(new Microsoft.Xna.Framework.Point(1038, 778), result.MapSize);
        Assert.Equal(new Microsoft.Xna.Framework.Point(200, 286), result.CenterPoint);
        Assert.Equal(new System.Drawing.Point(-200, -180), result.MinimapPosition);
        Assert.NotEmpty(result.CreateMinimapPngCopy());

        RuntimeObjectDefinition obj = Assert.Single(result.Objects);
        Assert.Equal(new RuntimeAssetKey("Map/Obj", "house.img/a/b/0"), obj.Asset);
        Assert.Equal((11, 12, 13, 2, 9), (obj.X, obj.Y, obj.Z, obj.Layer, obj.Platform));
        Assert.Equal(new RuntimeObjectQuestDefinition(123, 2), Assert.Single(obj.Quests));
        RuntimeTileDefinition tile = Assert.Single(result.Tiles);
        Assert.Equal(new RuntimeAssetKey("Map/Tile", "grass.img/bsc/4"), tile.Asset);
        Assert.True(obj.DrawOrder < tile.DrawOrder);

        Assert.Equal(("100100", "Green Snail"), (Assert.Single(result.Mobs).Id, result.Mobs[0].DisplayName));
        Assert.Equal(("9000000", "Maple Administrator"), (Assert.Single(result.Npcs).Id, result.Npcs[0].DisplayName));
        Assert.Equal(("200100", "Switch"), (Assert.Single(result.Reactors).Id, result.Reactors[0].DisplayName));
        RuntimePortalDefinition portal = Assert.Single(result.Portals);
        Assert.Equal(PortalType.Script, portal.Type);
        Assert.Equal((222, "next", "go", 25), (portal.TargetMapId, portal.TargetName, portal.Script, portal.Delay));
        Assert.Equal(2, result.Backgrounds.Count);
        Assert.False(result.Backgrounds[0].Front);
        Assert.True(result.Backgrounds[1].Front);

        RuntimeFootholdDefinition foothold = Assert.Single(result.Footholds);
        Assert.Equal((10, 0, 11, 2, 9), (foothold.Number, foothold.Previous, foothold.Next, foothold.Layer, foothold.Platform));
        Assert.True(foothold.ForbidFallDown);
        Assert.Single(result.Ropes);
        Assert.Equal(new RuntimeChairDefinition(77, 88), Assert.Single(result.Chairs));
        Assert.Equal("Welcome", Assert.Single(result.Tooltips).Title);
        Assert.Contains(result.Misc, item => item.Kind == RuntimeMiscKind.Clock);
        Assert.Contains(result.Misc, item => item.Kind == RuntimeMiscKind.Area && item.Identifier == "safe");
        Assert.Contains(result.Misc, item => item.Kind == RuntimeMiscKind.BuffZone && item.ItemId == 202);
        RuntimeMirrorFieldDefinition mirror = Assert.Single(result.MirrorFields);
        Assert.Equal(1, mirror.Type);
        Assert.Equal(new Microsoft.Xna.Framework.Vector2(5, 6), mirror.Offset);

        MapInfo info = result.CreateMapInfo();
        Assert.Equal("Bgm00/FloralLife", info.bgm);
        Assert.Contains(info.additionalNonInfoProps, property => property.Name == "futureFeature");
        ((WzIntProperty)info.additionalNonInfoProps.Single(p => p.Name == "futureFeature")["value"]).Value = 999;
        Assert.Equal(55, ((WzIntProperty)map["futureFeature"]["value"]).Value);
        Assert.Equal(originalRoots, map.WzProperties.Count);
        Assert.True(map.Parsed);
        using WzImage extensions = result.CreateExtensionTreeCopy();
        Assert.Equal(1, result.ExtensionSchemaVersion);
        Assert.Equal(1, Assert.IsType<WzIntProperty>(extensions["schemaVersion"]).Value);
        var extensionValue = Assert.IsType<WzIntProperty>(
            extensions["additionalNonInfoProps"]["futureFeature"]["value"]);
        Assert.Equal(55, extensionValue.Value);
        extensionValue.Value = 123;
        using WzImage independentExtensions = result.CreateExtensionTreeCopy();
        Assert.Equal(55, Assert.IsType<WzIntProperty>(
            independentExtensions["additionalNonInfoProps"]["futureFeature"]["value"]).Value);
        info.Image.Dispose();
    }

    [Fact]
    public void MissingLifeAndReactorNamesFallBackToTheirIds()
    {
        using WzImage map = CreateMap();
        WzSubProperty life = Assert.IsType<WzSubProperty>(map["life"]);
        life.AddProperty(Life("2", "9999999", "m", 140, 250));
        WzSubProperty reactors = Assert.IsType<WzSubProperty>(map["reactor"]);
        reactors.AddProperty(Node("1", Int("x", 45), Int("y", 55), Text("id", "299999"), Int("reactorTime", 3)));

        using RuntimeMapDefinition result = new WzRuntimeMapReader().Read(map, new WzRuntimeMapReaderOptions());

        Assert.Contains(result.Mobs, value => value.Id == "9999999" && value.DisplayName == "9999999");
        Assert.Contains(result.Reactors, value => value.Id == "299999" && value.DisplayName == "299999");
    }

    [Fact]
    public void DerivesBoundsFromFootholdsWhenMinimapAndExplicitVrAreAbsent()
    {
        using WzImage map = CreateMap();
        WzSubProperty info = Assert.IsType<WzSubProperty>(map["info"]);
        foreach (string name in new[] { "VRLeft", "VRTop", "VRRight", "VRBottom" })
            info.RemoveProperty(info[name]);
        map.RemoveProperty(map["miniMap"]);

        using RuntimeMapDefinition result = new WzRuntimeMapReader().Read(map,
            new WzRuntimeMapReaderOptions { MapId = 1 });

        Assert.Null(result.VirtualBounds);
        Assert.Equal(new Microsoft.Xna.Framework.Point(150, 720), result.MapSize);
        Assert.Empty(result.CreateMinimapPngCopy());
    }

    private static WzImage CreateMap()
    {
        var image = new WzImage("100000001.img") { Parsed = true };
        image.AddProperty(Node("info", Text("bgm", "Bgm00/FloralLife"), Int("VRLeft", -100),
            Int("VRTop", -200), Int("VRRight", 900), Int("VRBottom", 500)));
        image.AddProperty(Node("futureFeature", Int("value", 55)));

        image.AddProperty(Node("2", Node("info", Text("tS", "grass")),
            Node("obj", Node("0", Int("x", 11), Int("y", 12), Int("z", 13), Int("zM", 9),
                Text("oS", "house"), Text("l0", "a"), Text("l1", "b"), Text("l2", "0"),
                Int("f", 1), Node("quest", Int("123", 2)))),
            Node("tile", Node("7", Int("x", 21), Int("y", 22), Int("zM", 9), Text("u", "bsc"), Int("no", 4)))));
        image.AddProperty(Node("life",
            Life("0", "100100", "m", 100, 210),
            Life("1", "9000000", "n", 120, 230)));
        image.AddProperty(Node("reactor", Node("0", Int("x", 30), Int("y", 40), Text("id", "200100"),
            Int("f", 1), Int("reactorTime", 3), Text("name", "switch"))));
        image.AddProperty(Node("portal", Node("0", Int("x", 50), Int("y", 60), Int("pt", 7),
            Int("tm", 222), Text("tn", "next"), Text("pn", "sp"), Text("script", "go"), Int("delay", 25),
            Int("hideTooltip", 1), Int("onlyOnce", 1), Int("horizontalImpact", 4), Int("verticalImpact", 5),
            Int("hRange", 6), Int("vRange", 7), Text("reactorName", "r"), Text("sessionValueKey", "k"), Text("sessionValue", "v"))));
        image.AddProperty(Node("foothold", Node("2", Node("9", Node("10", Int("x1", 0), Int("y1", 100),
            Int("x2", 120), Int("y2", 100), Int("prev", 0), Int("next", 11), Int("force", 4), Int("piece", 5),
            Int("forbidFallDown", 1), Int("cantThrough", 1))))));
        image.AddProperty(Node("ladderRope", Node("0", Int("x", 70), Int("y1", 20), Int("y2", 90),
            Int("page", 2), Int("l", 1), Int("uf", 1))));
        image.AddProperty(Node("seat", new WzVectorProperty("0", new WzIntProperty("x", 77), new WzIntProperty("y", 88))));
        image.AddProperty(Node("back",
            Background("0", "sky", 0), Background("1", "front", 1)));
        image.AddProperty(Node("ToolTip", RectNode("0", 1, 2, 30, 40), RectNode("0char", 5, 6, 15, 16)));
        image.AddProperty(Node("clock", Int("x", 1), Int("y", 2), Int("width", 30), Int("height", 40)));
        image.AddProperty(Node("area", RectNode("safe", -10, -20, 10, 20)));
        image.AddProperty(Node("BuffZone", Node("warm", Int("x1", 0), Int("y1", 0), Int("x2", 20), Int("y2", 30),
            Int("ItemID", 202), Int("Interval", 5), Int("Duration", 6))));

        var mirrorItem = RectNode("0", 10, 20, 50, 80);
        mirrorItem.AddProperty(new WzVectorProperty("offset", 5, 6));
        mirrorItem.AddProperty(Int("gradient", 7));
        mirrorItem.AddProperty(Int("alpha", 8));
        mirrorItem.AddProperty(Text("objectForOverlay", "obj"));
        mirrorItem.AddProperty(Int("reflection", 1));
        mirrorItem.AddProperty(Int("alphaTest", 1));
        image.AddProperty(Node("MirrorFieldData", Node("0", Node("mob", mirrorItem))));

        using var bitmap = new Bitmap(4, 3);
        using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Blue);
        var canvas = new WzCanvasProperty("canvas") { PngProperty = new WzPngProperty() };
        canvas.PngProperty.PNG = bitmap;
        image.AddProperty(Node("miniMap", Int("width", 300), Int("height", 200), Int("centerX", 200),
            Int("centerY", 180), canvas));
        image.Changed = false;
        return image;
    }

    private static WzImage CreateTooltipStrings()
    {
        var image = new WzImage("ToolTipHelp.img") { Parsed = true };
        image.AddProperty(Node("Mapobject", Node("100000001", Node("0", Text("Title", "Welcome"), Text("Desc", "Town")))));
        image.Changed = false;
        return image;
    }

    private static WzSubProperty Life(string name, string id, string type, int x, int cy) =>
        Node(name, Text("id", id), Text("type", type), Int("x", x), Int("y", cy - 10), Int("cy", cy),
            Int("rx0", x - 20), Int("rx1", x + 30), Int("f", 1), Int("hide", 1), Int("mobTime", 4),
            Int("info", 5), Int("team", 6), Text("limitedname", "event"));

    private static WzSubProperty Background(string name, string set, int front) =>
        Node(name, Int("x", 1), Int("y", 2), Int("rx", 3), Int("ry", 4), Int("cx", 5), Int("cy", 6),
            Int("a", 200), Int("type", 0), Int("front", front), Int("page", 1), Int("screenMode", 2),
            Int("f", 1), Text("bS", set), Int("ani", 0), Int("no", 3));

    private static WzSubProperty RectNode(string name, int x1, int y1, int x2, int y2) =>
        Node(name, Int("x1", x1), Int("y1", y1), Int("x2", x2), Int("y2", y2));

    private static WzSubProperty Node(string name, params WzImageProperty[] children)
    {
        var node = new WzSubProperty(name);
        foreach (WzImageProperty child in children) node.AddProperty(child);
        return node;
    }

    private static WzIntProperty Int(string name, int value) => new(name, value);
    private static WzStringProperty Text(string name, string value) => new(name, value);
}
