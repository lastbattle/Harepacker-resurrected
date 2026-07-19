using HaCreator.GUI.FrameAnimation.AI;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace UnitTest_AnimationEditor;

public class AnimationImageProcessorTests
{
    [Fact]
    public void RemoveBackgroundOnlyClearsMatchingPixelsConnectedToEdge()
    {
        using var source = SolidBitmap(7, 7, Color.White);
        using (Graphics graphics = Graphics.FromImage(source))
            graphics.DrawRectangle(Pens.Black, 1, 1, 4, 4);
        source.SetPixel(3, 3, Color.White);

        using Bitmap result = AnimationImageProcessor.RemoveBackground(source, colorTolerance: 0);

        Assert.Equal(0, result.GetPixel(0, 0).A);
        Assert.Equal(255, result.GetPixel(1, 1).A);
        Assert.Equal(Color.White.ToArgb(), result.GetPixel(3, 3).ToArgb());
    }

    [Fact]
    public void SaturatedMatteCleanupPreservesEnclosedMatchingSpriteColor()
    {
        using var source = SolidBitmap(9, 9, Color.Lime);
        using (Graphics graphics = Graphics.FromImage(source))
            graphics.FillRectangle(Brushes.Black, 1, 1, 7, 7);
        source.SetPixel(4, 4, Color.Lime);

        using Bitmap result = AnimationImageProcessor.RemoveBackground(source, colorTolerance: 0);

        Assert.Equal(0, result.GetPixel(0, 0).A);
        Assert.Equal(Color.Lime.ToArgb(), result.GetPixel(4, 4).ToArgb());
    }

    [Fact]
    public void SaturatedMatteCleanupRemovesBoundaryFringeButPreservesInteriorHue()
    {
        Color fringe = Color.FromArgb(32, 192, 32);
        using var source = SolidBitmap(9, 9, Color.Lime);
        using (Graphics graphics = Graphics.FromImage(source))
            graphics.FillRectangle(Brushes.Red, 2, 2, 5, 5);
        source.SetPixel(2, 4, fringe);
        source.SetPixel(4, 4, fringe);

        using Bitmap result = AnimationImageProcessor.RemoveBackground(source, colorTolerance: 28);

        Assert.Equal(0, result.GetPixel(2, 4).A);
        Assert.Equal(fringe.ToArgb(), result.GetPixel(4, 4).ToArgb());
    }

    [Fact]
    public void FindAlphaBoundsReturnsExactOpaqueRectangle()
    {
        using var bitmap = new Bitmap(10, 8, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            graphics.FillRectangle(Brushes.Red, 2, 3, 4, 2);

        Rectangle? bounds = AnimationImageProcessor.FindAlphaBounds(bitmap);

        Assert.Equal(new Rectangle(2, 3, 4, 2), bounds);
    }

    [Fact]
    public void AlignmentMatchesBottomCenterAnchorsInWorldSpace()
    {
        using var reference = new Bitmap(12, 12, PixelFormat.Format32bppArgb);
        using var generated = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(reference))
            graphics.FillRectangle(Brushes.Black, 2, 3, 6, 7); // anchor 5,9
        using (Graphics graphics = Graphics.FromImage(generated))
            graphics.FillRectangle(Brushes.Black, 4, 2, 10, 14); // anchor 9,15

        Point origin = AnimationImageProcessor.CalculateAlignedOrigin(
            generated, reference, new Point(5, 9));

        Assert.Equal(new Point(9, 15), origin);
    }

    [Fact]
    public void ProcessTrimsBitmapAndTranslatesAlignedOrigin()
    {
        using var generated = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(generated))
            graphics.FillRectangle(Brushes.Black, 2, 3, 3, 4);

        using ProcessedAnimationImage result = AnimationImageProcessor.Process(generated,
            options: new AnimationImageProcessingOptions
            {
                RemoveEdgeBackground = false,
                TrimTransparentPixels = true
            });

        Assert.Equal(3, result.Bitmap.Width);
        Assert.Equal(4, result.Bitmap.Height);
        Assert.Equal(new Rectangle(2, 3, 3, 4), result.OriginalAlphaBounds);
        Assert.Equal(new Point(1, 3), result.Origin);
    }

    [Fact]
    public void FullyTransparentImageRemainsValidOneFrameBitmap()
    {
        using var generated = new Bitmap(4, 5, PixelFormat.Format32bppArgb);

        using ProcessedAnimationImage result = AnimationImageProcessor.Process(generated);

        Assert.Equal(4, result.Bitmap.Width);
        Assert.Equal(5, result.Bitmap.Height);
        Assert.Equal(new Point(2, 4), result.Origin);
    }

    [Fact]
    public void ResizeDoesNotBleedRemovedChromaIntoTransparentEdges()
    {
        using var generated = SolidBitmap(64, 64, Color.Magenta);
        using (Graphics graphics = Graphics.FromImage(generated))
            graphics.FillEllipse(Brushes.Orange, 16, 16, 32, 32);

        using ProcessedAnimationImage result = AnimationImageProcessor.Process(generated, options:
            new AnimationImageProcessingOptions
            {
                RemoveEdgeBackground = true,
                TrimTransparentPixels = true,
                MaxOutputDimension = 16,
                TransparentPadding = 2
            });

        for (int y = 0; y < result.Bitmap.Height; y++)
        for (int x = 0; x < result.Bitmap.Width; x++)
        {
            Color pixel = result.Bitmap.GetPixel(x, y);
            bool magentaSpill = pixel.A > 0 && pixel.R > 120 && pixel.B > 120 && pixel.G < 80;
            Assert.False(magentaSpill);
        }
    }

    [Fact]
    public void ProcessAlwaysClearsRgbHiddenBehindTransparentPixels()
    {
        using var generated = new Bitmap(3, 2, PixelFormat.Format32bppArgb);
        WriteRawPixel(generated, 0, 0, 0x00FF00FF);
        generated.SetPixel(1, 0, Color.Black);

        using ProcessedAnimationImage result = AnimationImageProcessor.Process(generated, options:
            new AnimationImageProcessingOptions
            {
                RemoveEdgeBackground = false,
                TrimTransparentPixels = false,
                MatchReferenceScale = false
            });

        Assert.Equal(0, ReadRawPixel(result.Bitmap, 0, 0));
    }

    [Fact]
    public void TransparentPaddingPreservesBottomCenterWorldAnchor()
    {
        using var generated = new Bitmap(3, 4, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(generated))
            graphics.Clear(Color.Black);

        using ProcessedAnimationImage result = AnimationImageProcessor.Process(generated, options:
            new AnimationImageProcessingOptions
            {
                RemoveEdgeBackground = false,
                TrimTransparentPixels = true,
                MatchReferenceScale = false,
                TransparentPadding = 3
            });

        Assert.Equal(new Point(4, 6), result.Origin);
        Rectangle bounds = AnimationImageProcessor.FindAlphaBounds(result.Bitmap)!.Value;
        Point worldAnchor = new(bounds.Left + bounds.Width / 2 - result.Origin.X,
            bounds.Bottom - 1 - result.Origin.Y);
        Assert.Equal(Point.Empty, worldAnchor);
    }

    [Fact]
    public void ProcessMatchesGeneratedScaleToReferenceAlphaBounds()
    {
        using var reference = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using var generated = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(reference))
            graphics.FillRectangle(Brushes.Black, 5, 5, 10, 10);
        using (Graphics graphics = Graphics.FromImage(generated))
            graphics.FillRectangle(Brushes.Black, 10, 10, 80, 80);

        using ProcessedAnimationImage result = AnimationImageProcessor.Process(
            generated, reference, new Point(10, 15),
            new AnimationImageProcessingOptions { RemoveEdgeBackground = false, TrimTransparentPixels = true });

        Rectangle bounds = AnimationImageProcessor.FindAlphaBounds(result.Bitmap)!.Value;
        Assert.InRange(bounds.Width, 9, 11);
        Assert.InRange(bounds.Height, 9, 11);
    }

    [Fact]
    public void ReferenceAlignmentSurvivesTrimResizeAndPadding()
    {
        using var reference = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using var generated = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(reference))
            graphics.FillRectangle(Brushes.Black, 5, 5, 10, 10);
        using (Graphics graphics = Graphics.FromImage(generated))
            graphics.FillRectangle(Brushes.Black, 10, 10, 80, 80);

        using ProcessedAnimationImage result = AnimationImageProcessor.Process(
            generated, reference, new Point(7, 12),
            new AnimationImageProcessingOptions
            {
                RemoveEdgeBackground = false,
                TrimTransparentPixels = true,
                TransparentPadding = 3
            });

        Rectangle generatedBounds = AnimationImageProcessor.FindAlphaBounds(result.Bitmap)!.Value;
        Rectangle referenceBounds = AnimationImageProcessor.FindAlphaBounds(reference)!.Value;
        Point generatedWorldAnchor = new(
            generatedBounds.Left + generatedBounds.Width / 2 - result.Origin.X,
            generatedBounds.Bottom - 1 - result.Origin.Y);
        Point referenceWorldAnchor = new(
            referenceBounds.Left + referenceBounds.Width / 2 - 7,
            referenceBounds.Bottom - 1 - 12);
        Assert.Equal(referenceWorldAnchor, generatedWorldAnchor);
    }

    [Fact]
    public void ReferenceScaleStillHonorsMaximumOutputDimension()
    {
        using var reference = SolidBitmap(900, 700, Color.Black);
        using var generated = SolidBitmap(20, 10, Color.Black);

        using ProcessedAnimationImage result = AnimationImageProcessor.Process(
            generated, reference, Point.Empty,
            new AnimationImageProcessingOptions
            {
                RemoveEdgeBackground = false,
                TrimTransparentPixels = true,
                MatchReferenceScale = true,
                MaxOutputDimension = 128
            });

        Assert.Equal(128, Math.Max(result.Bitmap.Width, result.Bitmap.Height));
    }

    private static Bitmap SolidBitmap(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private static int ReadRawPixel(Bitmap bitmap, int x, int y)
    {
        Rectangle area = new(x, y, 1, 1);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try { return Marshal.ReadInt32(data.Scan0); }
        finally { bitmap.UnlockBits(data); }
    }

    private static void WriteRawPixel(Bitmap bitmap, int x, int y, int value)
    {
        Rectangle area = new(x, y, 1, 1);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { Marshal.WriteInt32(data.Scan0, value); }
        finally { bitmap.UnlockBits(data); }
    }
}
