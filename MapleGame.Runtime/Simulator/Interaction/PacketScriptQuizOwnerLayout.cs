using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketScriptQuizOwnerLayout
    {
        private const int PreviewMargin = 18;
        private const int PreviewGap = 14;
        private const float MinPreviewScale = 0.35f;
        private const float MaxPreviewScale = 1f;

        internal static Rectangle ResolvePreviewBounds(
            int renderWidth,
            int renderHeight,
            int panelWidth,
            int panelHeight,
            int overlayTop)
        {
            Rectangle[] bounds = ResolveCenteredStackBounds(
                renderWidth,
                renderHeight,
                overlayTop,
                new Point(panelWidth, panelHeight));
            return bounds.Length == 0 ? Rectangle.Empty : bounds[0];
        }

        internal static Rectangle[] ResolveCenteredStackBounds(
            int renderWidth,
            int renderHeight,
            int overlayTop,
            params Point[] panelSizes)
        {
            if (panelSizes == null || panelSizes.Length == 0)
            {
                return Array.Empty<Rectangle>();
            }

            int anchorTop = Math.Max(PreviewMargin, overlayTop);
            int maxPanelWidth = 0;
            int totalPanelHeight = 0;
            int validPanelCount = 0;
            for (int i = 0; i < panelSizes.Length; i++)
            {
                Point panelSize = panelSizes[i];
                if (panelSize.X <= 0 || panelSize.Y <= 0)
                {
                    continue;
                }

                maxPanelWidth = Math.Max(maxPanelWidth, panelSize.X);
                totalPanelHeight += panelSize.Y;
                validPanelCount++;
            }

            if (validPanelCount == 0 || maxPanelWidth <= 0 || totalPanelHeight <= 0)
            {
                return Array.Empty<Rectangle>();
            }

            totalPanelHeight += PreviewGap * Math.Max(0, validPanelCount - 1);
            float widthScale = (renderWidth - (PreviewMargin * 2)) / (float)maxPanelWidth;
            float heightScale = (renderHeight - anchorTop - PreviewMargin) / (float)totalPanelHeight;
            float scale = MathF.Min(MaxPreviewScale, MathF.Min(widthScale, heightScale));
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = MinPreviewScale;
            }

            scale = Math.Max(scale, MinPreviewScale);
            int scaledTotalHeight = 0;
            for (int i = 0; i < panelSizes.Length; i++)
            {
                Point panelSize = panelSizes[i];
                if (panelSize.X <= 0 || panelSize.Y <= 0)
                {
                    continue;
                }

                scaledTotalHeight += Math.Max(1, (int)Math.Round(panelSize.Y * scale));
            }

            scaledTotalHeight += PreviewGap * Math.Max(0, validPanelCount - 1);
            int startY = Math.Max(anchorTop, (renderHeight - scaledTotalHeight) / 2);
            if (startY + scaledTotalHeight > renderHeight - PreviewMargin)
            {
                startY = Math.Max(anchorTop, renderHeight - scaledTotalHeight - PreviewMargin);
            }

            Rectangle[] bounds = new Rectangle[panelSizes.Length];
            int drawY = startY;
            for (int i = 0; i < panelSizes.Length; i++)
            {
                Point panelSize = panelSizes[i];
                if (panelSize.X <= 0 || panelSize.Y <= 0)
                {
                    bounds[i] = Rectangle.Empty;
                    continue;
                }

                int scaledWidth = Math.Max(1, (int)Math.Round(panelSize.X * scale));
                int scaledHeight = Math.Max(1, (int)Math.Round(panelSize.Y * scale));
                int drawX = (renderWidth - scaledWidth) / 2;
                bounds[i] = new Rectangle(drawX, drawY, scaledWidth, scaledHeight);
                drawY += scaledHeight + PreviewGap;
            }

            return bounds;
        }

        internal static Rectangle AnchorRect(Rectangle previewBounds, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int sourcePanelWidth, int sourcePanelHeight)
        {
            if (previewBounds == Rectangle.Empty || sourcePanelWidth <= 0 || sourcePanelHeight <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
            {
                return Rectangle.Empty;
            }

            int x = previewBounds.X + (int)Math.Round(previewBounds.Width * (sourceX / (double)sourcePanelWidth));
            int y = previewBounds.Y + (int)Math.Round(previewBounds.Height * (sourceY / (double)sourcePanelHeight));
            int width = Math.Max(1, (int)Math.Round(previewBounds.Width * (sourceWidth / (double)sourcePanelWidth)));
            int height = Math.Max(1, (int)Math.Round(previewBounds.Height * (sourceHeight / (double)sourcePanelHeight)));
            return new Rectangle(x, y, width, height);
        }

        internal static Rectangle ResolveStackedPreviewBounds(Rectangle upperBounds, int panelWidth, int panelHeight, int renderWidth, int renderHeight)
        {
            if (upperBounds == Rectangle.Empty)
            {
                return ResolvePreviewBounds(renderWidth, renderHeight, panelWidth, panelHeight, PreviewMargin);
            }

            Rectangle previewBounds = ResolvePreviewBounds(
                renderWidth,
                renderHeight,
                panelWidth,
                panelHeight,
                upperBounds.Bottom + PreviewGap);
            if (previewBounds.Y <= upperBounds.Bottom)
            {
                int y = upperBounds.Bottom + PreviewGap;
                if (y + previewBounds.Height <= renderHeight - PreviewMargin)
                {
                    previewBounds.Y = y;
                }
            }

            return previewBounds;
        }
    }
}
