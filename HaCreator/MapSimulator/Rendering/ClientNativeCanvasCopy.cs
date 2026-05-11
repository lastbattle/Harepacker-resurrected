using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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

            Color[] result = destinationPixels.Take(destinationWidth * destinationHeight).ToArray();
            for (int sourceY = 0; sourceY < sourceHeight; sourceY++)
            {
                int destinationY = y + sourceY;
                if (destinationY < 0 || destinationY >= destinationHeight)
                {
                    continue;
                }

                for (int sourceX = 0; sourceX < sourceWidth; sourceX++)
                {
                    int destinationX = x + sourceX;
                    if (destinationX < 0 || destinationX >= destinationWidth)
                    {
                        continue;
                    }

                    int destinationIndex = destinationY * destinationWidth + destinationX;
                    int sourceIndex = sourceY * sourceWidth + sourceX;
                    result[destinationIndex] = BlendAlpha255(result[destinationIndex], sourcePixels[sourceIndex]);
                }
            }

            return result;
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
