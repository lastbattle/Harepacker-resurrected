using System.Drawing;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_WorldMapEditor;

/// <summary>
/// Builds small, in-memory WorldMap images that exercise the native property
/// shapes without requiring one of the optional client exports on disk.
/// The property ordering and sparse numeric keys intentionally mirror files
/// found in both legacy and modern clients.
/// </summary>
internal static class WorldMapFixtureFactory
{
    public const string RootName = "FixtureRoot";
    public const string ChildName = "FixtureChild";
    public const int FirstMapId = 100000000;
    public const int SecondMapId = 100000100;

    public static WzImage CreateSurface(
        string imageName = "WorldMapFixture.img",
        string worldMapName = RootName,
        string? parentMap = null,
        bool includeLink = true,
        bool includeFog = true)
    {
        var image = new WzImage(imageName);

        var info = new WzSubProperty("info");
        info.AddProperty(new WzStringProperty("WorldMap", worldMapName));
        if (!string.IsNullOrWhiteSpace(parentMap))
            info.AddProperty(new WzStringProperty("parentMap", parentMap));
        // This sibling is deliberately not understood by the codec. It must
        // survive a load/edit/save cycle byte-for-byte (semantically).
        info.AddProperty(new WzStringProperty("futureInfoField", "preserve-me"));
        image.AddProperty(info);

        var baseImage = new WzSubProperty("BaseImg");
        var baseCanvas = CreateCanvas("0", 640, 470);
        baseCanvas.AddProperty(new WzVectorProperty("origin", 320, 235));
        baseCanvas.AddProperty(new WzIntProperty("z", 0));
        baseImage.AddProperty(baseCanvas);
        image.AddProperty(baseImage);

        var mapList = new WzSubProperty("MapList");
        mapList.AddProperty(CreateMapEntry("7", type: 29, x: -119, y: -26,
            new[] { FirstMapId, SecondMapId }, title: "Fixture Town"));
        // Numeric keys are intentionally sparse and out of insertion order.
        mapList.AddProperty(CreateMapEntry("42", type: -1, x: 80, y: 35,
            new[] { 200000000 }, title: null));
        image.AddProperty(mapList);

        if (includeLink)
        {
            var links = new WzSubProperty("MapLink");
            var link = new WzSubProperty("3");
            link.AddProperty(new WzStringProperty("toolTip", "Open child"));
            link.AddProperty(new WzVectorProperty("spot", 20, 24));
            var linkBody = new WzSubProperty("link");
            linkBody.AddProperty(new WzStringProperty("linkMap", ChildName));
            var linkCanvas = CreateCanvas("linkImg", 218, 166);
            linkCanvas.AddProperty(new WzVectorProperty("origin", 109, 83));
            linkCanvas.AddProperty(new WzIntProperty("z", 1));
            linkBody.AddProperty(linkCanvas);
            // Preserve a future link field next to the known linkMap/linkImg.
            linkBody.AddProperty(new WzIntProperty("futureLinkField", 73));
            link.AddProperty(linkBody);
            links.AddProperty(link);
            image.AddProperty(links);
        }

        if (includeFog)
        {
            var fog = new WzSubProperty("Fog");
            var layer = new WzSubProperty("9");
            var fogCanvas = CreateCanvas("0", 640, 470);
            fogCanvas.AddProperty(new WzVectorProperty("origin", 320, 235));
            fogCanvas.AddProperty(new WzIntProperty("z", 2));
            layer.AddProperty(fogCanvas);
            layer.AddProperty(new WzIntProperty("quest", 12345));
            layer.AddProperty(new WzIntProperty("qState", 2));
            fog.AddProperty(layer);
            image.AddProperty(fog);
        }

        // Unknown root data is intentionally a different property type from
        // the nested unknowns to cover preservation of both shape and value.
        var unknownRoot = new WzSubProperty("futureRoot");
        unknownRoot.AddProperty(new WzIntProperty("version", 7));
        unknownRoot.AddProperty(new WzNullProperty("marker"));
        image.AddProperty(unknownRoot);

        image.Changed = false;
        image.Parsed = true;
        return image;
    }

    public static WzImage CreateChildSurface(bool includeLink = false) =>
        CreateSurface("WorldMapFixtureChild.img", ChildName, RootName, includeLink, includeFog: false);

    public static WzImage CreateExclusionList(string imageName = "SearchExcept.img")
    {
        var image = new WzImage(imageName);
        image.AddProperty(new WzIntProperty("0", FirstMapId));
        image.AddProperty(new WzIntProperty("4", SecondMapId));
        image.AddProperty(new WzIntProperty("19", 200000000));
        image.Changed = false;
        image.Parsed = true;
        return image;
    }

    public static WzSubProperty CreateMapEntry(
        string key,
        int type,
        int x,
        int y,
        IReadOnlyList<int> mapIds,
        string? title)
    {
        var entry = new WzSubProperty(key);
        entry.AddProperty(new WzIntProperty("type", type));
        entry.AddProperty(new WzVectorProperty("spot", x, y));
        var maps = new WzSubProperty("mapNo");
        for (int i = 0; i < mapIds.Count; i++)
            maps.AddProperty(new WzIntProperty((i * 3 + 1).ToString(), mapIds[i]));
        entry.AddProperty(maps);
        if (title is not null)
            entry.AddProperty(new WzStringProperty("title", title));
        entry.AddProperty(new WzStringProperty("futureEntryField", "preserve-entry"));
        return entry;
    }

    public static WzCanvasProperty CreateCanvas(string name, int width, int height)
    {
        var canvas = new WzCanvasProperty(name);
        var bitmap = new Bitmap(width, height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            graphics.Clear(Color.Transparent);
        var png = new WzPngProperty { PNG = bitmap };
        canvas.PngProperty = png;
        return canvas;
    }
}
