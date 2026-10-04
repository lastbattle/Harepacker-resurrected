using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class WzCanvasLinkResolutionTests
{
    [Fact]
    public void ExternalOutlinkPreservesBackslashPropertyName()
    {
        WzImage targetImage = new WzImage("UIWindowPL.img");
        WzSubProperty waterSmash = new WzSubProperty("WaterSmash");
        WzSubProperty scoreBoard = new WzSubProperty("ScoreBoard");
        WzSubProperty number = new WzSubProperty("number");
        WzCanvasProperty targetCanvas = new WzCanvasProperty("\\");
        number.AddProperty(targetCanvas);
        scoreBoard.AddProperty(number);
        waterSmash.AddProperty(scoreBoard);
        targetImage.AddProperty(waterSmash);

        WzCanvasProperty sourceCanvas = new WzCanvasProperty("\\");
        sourceCanvas.AddProperty(new WzStringProperty(
            WzCanvasProperty.OutlinkPropertyName,
            "UI/UIWindowPL.img/WaterSmash/ScoreBoard/number/\\"));

        Func<string, WzImage> previousResolver = WzCanvasProperty.ExternalImageResolver;
        string? resolvedImagePath = null;
        try
        {
            WzCanvasProperty.ExternalImageResolver = path =>
            {
                resolvedImagePath = path;
                return targetImage;
            };

            WzImageProperty result = sourceCanvas.GetLinkedWzImageProperty();

            Assert.Same(targetCanvas, result);
            Assert.Equal("UI/UIWindowPL.img", resolvedImagePath);
        }
        finally
        {
            WzCanvasProperty.ExternalImageResolver = previousResolver;
        }
    }
}
