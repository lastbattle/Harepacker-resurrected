using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketScriptQuizOwnerLayout
    {
        private const int PreviewMargin = 18;
        private const int PreviewGap = 14;
        private const float MaxPreviewHeightRatio = 0.56f;
        private const float MinPreviewScale = 0.58f;
        private const float MaxPreviewScale = 0.9f;

        internal static Rectangle ResolvePreviewBounds(
            int renderWidth,
            int renderHeight,
            int panelWidth,
            int panelHeight,
            int overlayTop)
        {
            if (panelWidth <= 0 || panelHeight <= 0)
            {
                return Rectangle.Empty;
            }

            float availableHeight = Math.Max(panelHeight, (renderHeight * MaxPreviewHeightRatio) - PreviewMargin);
            float scale = Math.Clamp(availableHeight / panelHeight, MinPreviewScale, MaxPreviewScale);
            int scaledWidth = Math.Max(1, (int)Math.Round(panelWidth * scale));
            int scaledHeight = Math.Max(1, (int)Math.Round(panelHeight * scale));
            int x = Math.Max(PreviewMargin, renderWidth - scaledWidth - PreviewMargin);
            int y = Math.Max(PreviewMargin, overlayTop);
            if (y + scaledHeight > renderHeight - PreviewMargin)
            {
                y = Math.Max(PreviewMargin, renderHeight - scaledHeight - PreviewMargin);
            }

            return new Rectangle(x, y, scaledWidth, scaledHeight);
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

            Rectangle previewBounds = ResolvePreviewBounds(renderWidth, renderHeight, panelWidth, panelHeight, upperBounds.Bottom + PreviewGap);
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
