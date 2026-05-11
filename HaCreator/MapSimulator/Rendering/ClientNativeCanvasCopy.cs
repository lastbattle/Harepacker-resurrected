using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace HaCreator.MapSimulator.Rendering
{
    internal static class ClientNativeCanvasCopy
    {
        internal const int Alpha255 = 255;

        internal static CompositingMode CompositingMode => System.Drawing.Drawing2D.CompositingMode.SourceOver;
        internal static InterpolationMode InterpolationMode => System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        internal static PixelOffsetMode PixelOffsetMode => System.Drawing.Drawing2D.PixelOffsetMode.Half;
        internal static CompositingQuality CompositingQuality => System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        internal static SmoothingMode SmoothingMode => System.Drawing.Drawing2D.SmoothingMode.None;
        internal static GraphicsUnit PageUnit => GraphicsUnit.Pixel;
        internal static float PageScale => 1f;

        internal static void ApplySettings(Graphics graphics)
        {
            if (graphics == null)
            {
                return;
            }

            graphics.CompositingMode = CompositingMode;
            graphics.CompositingQuality = CompositingQuality;
            graphics.InterpolationMode = InterpolationMode;
            graphics.PixelOffsetMode = PixelOffsetMode;
            graphics.SmoothingMode = SmoothingMode;
            graphics.PageUnit = PageUnit;
            graphics.PageScale = PageScale;
        }

        internal static void CopyAlpha255(Bitmap destination, Bitmap source, int x, int y)
        {
            if (destination == null || source == null)
            {
                return;
            }

            using Graphics graphics = Graphics.FromImage(destination);
            ApplySettings(graphics);
            graphics.DrawImageUnscaled(source, x, y);
        }

        internal static Color[] CopyAlpha255PixelsForTesting(
            Color[] destinationPixels,
            int destinationWidth,
            int destinationHeight,
            Color[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            int x,
            int y)
        {
            if (destinationPixels == null ||
                sourcePixels == null ||
                destinationWidth <= 0 ||
                destinationHeight <= 0 ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                destinationPixels.Length < destinationWidth * destinationHeight ||
                sourcePixels.Length < sourceWidth * sourceHeight)
            {
                return Array.Empty<Color>();
            }

            using Bitmap destination = CreateBitmapFromPixelsForTesting(
                destinationPixels,
                destinationWidth,
                destinationHeight);
            using Bitmap source = CreateBitmapFromPixelsForTesting(
                sourcePixels,
                sourceWidth,
                sourceHeight);
            CopyAlpha255(destination, source, x, y);
            return ReadBitmapPixelsForTesting(destination);
        }

        private static Bitmap CreateBitmapFromPixelsForTesting(Color[] pixels, int width, int height)
        {
            Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
            for (int pixelY = 0; pixelY < height; pixelY++)
            {
                for (int pixelX = 0; pixelX < width; pixelX++)
                {
                    bitmap.SetPixel(pixelX, pixelY, pixels[pixelY * width + pixelX]);
                }
            }

            return bitmap;
        }

        private static Color[] ReadBitmapPixelsForTesting(Bitmap bitmap)
        {
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return Array.Empty<Color>();
            }

            Color[] pixels = new Color[bitmap.Width * bitmap.Height];
            for (int pixelY = 0; pixelY < bitmap.Height; pixelY++)
            {
                for (int pixelX = 0; pixelX < bitmap.Width; pixelX++)
                {
                    pixels[pixelY * bitmap.Width + pixelX] = bitmap.GetPixel(pixelX, pixelY);
                }
            }

            return pixels;
        }

        internal static Color BlendAlpha255(Color destination, Color source)
        {
            if (source.A == 0)
            {
                return destination;
            }

            if (source.A == Alpha255)
            {
                return source;
            }

            using Bitmap destinationBitmap = new(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using Bitmap sourceBitmap = new(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            destinationBitmap.SetPixel(0, 0, destination);
            sourceBitmap.SetPixel(0, 0, source);
            CopyAlpha255(destinationBitmap, sourceBitmap, 0, 0);
            return destinationBitmap.GetPixel(0, 0);
        }

        internal static Color BlendAlpha255(params Color[] layers)
        {
            Color result = Color.Transparent;
            if (layers == null)
            {
                return result;
            }

            foreach (Color layer in layers)
            {
                result = BlendAlpha255(result, layer);
            }

            return result;
        }
    }
}
