using System.Drawing;
using MapleLib.WzLib.WzProperties;

namespace MapleLib.Tests;

public class WzPngPropertyTests
{
    [Fact]
    public void GetImageFalse_DoesNotCacheDecodedBitmap()
    {
        using Bitmap sourceBitmap = new Bitmap(2, 2);
        sourceBitmap.SetPixel(0, 0, Color.Red);
        sourceBitmap.SetPixel(1, 0, Color.Green);
        sourceBitmap.SetPixel(0, 1, Color.Blue);
        sourceBitmap.SetPixel(1, 1, Color.White);

        using WzPngProperty source = new WzPngProperty { PNG = (Bitmap)sourceBitmap.Clone() };
        using WzPngProperty property = CreateDetachedProperty(source);

        using Bitmap first = property.GetImage(false);
        using Bitmap second = property.GetImage(false);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetImageTrue_CachesDecodedBitmap()
    {
        using Bitmap sourceBitmap = new Bitmap(2, 2);
        sourceBitmap.SetPixel(0, 0, Color.Red);
        sourceBitmap.SetPixel(1, 0, Color.Green);
        sourceBitmap.SetPixel(0, 1, Color.Blue);
        sourceBitmap.SetPixel(1, 1, Color.White);

        using WzPngProperty source = new WzPngProperty { PNG = (Bitmap)sourceBitmap.Clone() };
        using WzPngProperty property = CreateDetachedProperty(source);

        Bitmap first = property.GetImage(true);
        Bitmap second = property.GetImage(false);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    private static WzPngProperty CreateDetachedProperty(WzPngProperty source)
    {
        byte[] compressedBytes = source.GetCompressedBytes(true);
        var property = new WzPngProperty();
        property.SetCompressedBytes((byte[])compressedBytes.Clone(), source.Width, source.Height, source.Format);
        return property;
    }
}
