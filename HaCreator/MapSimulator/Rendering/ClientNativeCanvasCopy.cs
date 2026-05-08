using System;
using System.Drawing;
using System.Drawing.Drawing2D;

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

            for (int sourceY = 0; sourceY < source.Height; sourceY++)
            {
                int destinationY = y + sourceY;
                if (destinationY < 0 || destinationY >= destination.Height)
                {
                    continue;
                }

                for (int sourceX = 0; sourceX < source.Width; sourceX++)
                {
                    int destinationX = x + sourceX;
                    if (destinationX < 0 || destinationX >= destination.Width)
                    {
                        continue;
                    }

                    Color sourcePixel = source.GetPixel(sourceX, sourceY);
                    if (sourcePixel.A == 0)
                    {
                        continue;
                    }

                    Color destinationPixel = destination.GetPixel(destinationX, destinationY);
                    destination.SetPixel(destinationX, destinationY, BlendAlpha255(destinationPixel, sourcePixel));
                }
            }
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

            int inverseSourceAlpha = Alpha255 - source.A;
            int destinationAlphaContribution = DivideBy255Rounded(destination.A * inverseSourceAlpha);
            int alpha = source.A + destinationAlphaContribution;
            if (alpha <= 0)
            {
                return Color.Transparent;
            }

            int red = Unpremultiply(
                source.R * source.A + DivideBy255Rounded(destination.R * destination.A * inverseSourceAlpha),
                alpha);
            int green = Unpremultiply(
                source.G * source.A + DivideBy255Rounded(destination.G * destination.A * inverseSourceAlpha),
                alpha);
            int blue = Unpremultiply(
                source.B * source.A + DivideBy255Rounded(destination.B * destination.A * inverseSourceAlpha),
                alpha);

            return Color.FromArgb(
                ClampByte(alpha),
                ClampByte(red),
                ClampByte(green),
                ClampByte(blue));
        }

        private static int DivideBy255Rounded(int value)
        {
            return (value + 127) / Alpha255;
        }

        private static int Unpremultiply(int premultipliedChannel, int alpha)
        {
            return alpha <= 0 ? 0 : (premultipliedChannel + (alpha / 2)) / alpha;
        }

        private static int ClampByte(int value)
        {
            return Math.Max(0, Math.Min(Alpha255, value));
        }
    }
}
