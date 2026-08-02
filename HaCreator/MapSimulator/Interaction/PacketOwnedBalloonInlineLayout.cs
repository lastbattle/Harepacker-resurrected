using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketOwnedBalloonInlineLayout
    {
        internal static Point ResolveDisplaySize(int sourceWidth, int sourceHeight, int maxDimension, int fallbackSize)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                int clampedFallback = Math.Max(1, fallbackSize);
                return new Point(clampedFallback, clampedFallback);
            }

            int clampedMaxDimension = Math.Max(1, maxDimension);
            int largestDimension = Math.Max(sourceWidth, sourceHeight);
            if (largestDimension <= clampedMaxDimension)
            {
                return new Point(sourceWidth, sourceHeight);
            }

            float scale = clampedMaxDimension / (float)largestDimension;
            return new Point(
                Math.Max(1, (int)System.Math.Round(sourceWidth * scale)),
                Math.Max(1, (int)System.Math.Round(sourceHeight * scale)));
        }

        internal static int ResolveAdvanceWidth(
            int sourceWidth,
            int sourceHeight,
            int maxDimension,
            int fallbackSize,
            int spacing)
        {
            Point size = ResolveDisplaySize(sourceWidth, sourceHeight, maxDimension, fallbackSize);
            return size.X + System.Math.Max(0, spacing);
        }

        internal static float ResolveVerticalOffset(
            int sourceWidth,
            int sourceHeight,
            int lineHeight,
            int maxDimension,
            int fallbackSize)
        {
            Point size = ResolveDisplaySize(sourceWidth, sourceHeight, maxDimension, fallbackSize);
            return System.Math.Max(0f, (lineHeight - size.Y) / 2f);
        }
    }
}
