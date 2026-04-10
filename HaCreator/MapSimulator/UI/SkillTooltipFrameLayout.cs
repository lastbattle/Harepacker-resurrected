using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillTooltipFrameLayout
    {
        internal readonly record struct FrameGeometry(int Width, int Height, Point Origin);

        internal static Rectangle ResolveTooltipRect(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight,
            ReadOnlySpan<FrameGeometry> frameGeometries,
            ReadOnlySpan<int> framePreference,
            int edgePadding,
            out int tooltipFrameIndex)
        {
            Rectangle bestRect = Rectangle.Empty;
            int bestFrame = framePreference.Length > 0 ? framePreference[0] : 0;
            int bestOverflow = int.MaxValue;

            for (int i = 0; i < framePreference.Length; i++)
            {
                int frameIndex = framePreference[i];
                Rectangle candidate = CreateTooltipRectFromAnchor(
                    anchorPoint,
                    tooltipWidth,
                    tooltipHeight,
                    frameIndex,
                    frameGeometries);
                int overflow = ComputeTooltipOverflow(candidate, renderWidth, renderHeight, edgePadding);
                if (overflow == 0)
                {
                    tooltipFrameIndex = frameIndex;
                    return candidate;
                }

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    bestFrame = frameIndex;
                    bestRect = candidate;
                }
            }

            tooltipFrameIndex = bestFrame;
            return ClampTooltipRect(bestRect, renderWidth, renderHeight, edgePadding);
        }

        private static Rectangle CreateTooltipRectFromAnchor(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int frameIndex,
            ReadOnlySpan<FrameGeometry> frameGeometries)
        {
            if ((uint)frameIndex < (uint)frameGeometries.Length)
            {
                FrameGeometry geometry = frameGeometries[frameIndex];
                if (geometry.Width > 0 && geometry.Height > 0 && geometry.Origin != Point.Zero)
                {
                    float scaleX = tooltipWidth / (float)geometry.Width;
                    float scaleY = tooltipHeight / (float)geometry.Height;
                    return new Rectangle(
                        anchorPoint.X - (int)Math.Round(geometry.Origin.X * scaleX),
                        anchorPoint.Y - (int)Math.Round(geometry.Origin.Y * scaleY),
                        tooltipWidth,
                        tooltipHeight);
                }
            }

            return frameIndex switch
            {
                0 => new Rectangle(anchorPoint.X - tooltipWidth + 1, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight),
                2 => new Rectangle(anchorPoint.X - tooltipWidth + 1, anchorPoint.Y, tooltipWidth, tooltipHeight),
                _ => new Rectangle(anchorPoint.X, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight)
            };
        }

        private static int ComputeTooltipOverflow(Rectangle rect, int renderWidth, int renderHeight, int edgePadding)
        {
            int overflow = 0;
            if (rect.Left < edgePadding)
            {
                overflow += edgePadding - rect.Left;
            }

            if (rect.Top < edgePadding)
            {
                overflow += edgePadding - rect.Top;
            }

            if (rect.Right > renderWidth - edgePadding)
            {
                overflow += rect.Right - (renderWidth - edgePadding);
            }

            if (rect.Bottom > renderHeight - edgePadding)
            {
                overflow += rect.Bottom - (renderHeight - edgePadding);
            }

            return overflow;
        }

        private static Rectangle ClampTooltipRect(Rectangle rect, int renderWidth, int renderHeight, int edgePadding)
        {
            int minX = edgePadding;
            int minY = edgePadding;
            int maxX = Math.Max(minX, renderWidth - edgePadding - rect.Width);
            int maxY = Math.Max(minY, renderHeight - edgePadding - rect.Height);

            return new Rectangle(
                Math.Clamp(rect.X, minX, maxX),
                Math.Clamp(rect.Y, minY, maxY),
                rect.Width,
                rect.Height);
        }
    }
}
