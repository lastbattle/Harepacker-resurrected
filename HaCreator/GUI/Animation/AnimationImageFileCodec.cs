using System;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingRectangle = System.Drawing.Rectangle;

namespace HaCreator.GUI.FrameAnimation;

internal static class AnimationImageFileCodec
{
    public static DrawingBitmap Load(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".webp", StringComparison.OrdinalIgnoreCase))
            return new DrawingBitmap(filePath);

        using SixLabors.ImageSharp.Image<Bgra32> image =
            SixLabors.ImageSharp.Image.Load<Bgra32>(filePath);
        return ToDrawingBitmap(image);
    }

    public static void SaveWebp(DrawingBitmap source, string filePath)
    {
        using DrawingBitmap normalized = Normalize(source);
        using SixLabors.ImageSharp.Image<Bgra32> image = ToImageSharpImage(normalized);
        using FileStream stream = File.Create(filePath);
        image.Save(stream, new WebpEncoder { FileFormat = WebpFileFormatType.Lossless });
    }

    private static DrawingBitmap ToDrawingBitmap(SixLabors.ImageSharp.Image<Bgra32> image)
    {
        var bitmap = new DrawingBitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(
            new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        bool succeeded = false;
        try
        {
            int rowBytes = image.Width * 4;
            byte[] pixels = new byte[rowBytes * image.Height];
            image.CopyPixelDataTo(pixels);
            for (int y = 0; y < image.Height; y++)
            {
                int rowOffset = data.Stride >= 0 ? y * data.Stride : (image.Height - 1 - y) * -data.Stride;
                Marshal.Copy(pixels, y * rowBytes, IntPtr.Add(data.Scan0, rowOffset), rowBytes);
            }
            succeeded = true;
        }
        finally
        {
            bitmap.UnlockBits(data);
            if (!succeeded)
                bitmap.Dispose();
        }

        return bitmap;
    }

    private static SixLabors.ImageSharp.Image<Bgra32> ToImageSharpImage(DrawingBitmap source)
    {
        BitmapData data = source.LockBits(
            new DrawingRectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = source.Width * 4;
            byte[] pixels = new byte[rowBytes * source.Height];
            for (int y = 0; y < source.Height; y++)
            {
                int rowOffset = data.Stride >= 0 ? y * data.Stride : (source.Height - 1 - y) * -data.Stride;
                Marshal.Copy(IntPtr.Add(data.Scan0, rowOffset), pixels, y * rowBytes, rowBytes);
            }

            return SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(pixels, source.Width, source.Height);
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    private static DrawingBitmap Normalize(DrawingBitmap source)
    {
        var result = new DrawingBitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using DrawingGraphics graphics = DrawingGraphics.FromImage(result);
        graphics.Clear(DrawingColor.Transparent);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImage(source, new DrawingRectangle(0, 0, source.Width, source.Height));
        return result;
    }
}
