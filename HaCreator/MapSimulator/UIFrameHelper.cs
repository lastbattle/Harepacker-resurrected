using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator
{
    public class UIFrameHelper
    {
        /// <summary>
        /// Draws the frame of a UI without image background
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="backgroundColor"></param>
        /// <param name="targetImageWidth"></param>
        /// <param name="targetImageHeight"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawUIFrame(System.Drawing.Graphics graphics,
            System.Drawing.Color backgroundColor,
            int targetImageWidth, int targetImageHeight)
        {
            DrawUIFrame(graphics, backgroundColor, 
                null, null, null, null, null, null, null, null, null, 0, 
                targetImageWidth, targetImageHeight);
        }

        /// <summary>
        /// Draws the frame of a UI
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="background">Background color of the frame</param>
        /// <param name="ne">Top right [Optional path] </param>
        /// <param name="nw">Top left [Optional path] </param>
        /// <param name="se">Bottom right [Optional path] </param>
        /// <param name="sw">Bottom left [Optional path] </param>
        /// <param name="e">Right [Optional path] </param>
        /// <param name="w">Left [Optional path] </param>
        /// <param name="n">Top [Optional path] </param>
        /// <param name="s">Bottom [Optional path] </param>
        /// <param name="c">Fills throughout the entire row and column. [Optional path] </param>
        /// <param name="targetImageWidth"></param>
        /// <param name="targetImageHeight"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawUIFrame(System.Drawing.Graphics graphics,
            System.Drawing.Color backgroundColor,
            Bitmap ne, Bitmap nw, Bitmap se, Bitmap sw,
            Bitmap e, Bitmap w, Bitmap n, Bitmap s,
            Bitmap c, int startBgYPaint,
            int targetImageWidth, int targetImageHeight)
        {
            RectangleF fillRectangleBGRange = new RectangleF(
                w != null ? (w.Width / 3) : 2,
                n != null ? (n.Height / 3) : 2,
                w != null ? (targetImageWidth - (w.Width / 2) - 1) : (targetImageWidth - (2 / 2)),
                n != null ? (targetImageHeight - (n.Height / 2) - 1) : (targetImageHeight - (2 / 2) - 1));

            // Background color
            using (var backgroundBrush = new SolidBrush(backgroundColor))
            {
                graphics.FillRectangle(backgroundBrush, fillRectangleBGRange);
            }

            // Fill background with bitmap using TextureBrush for hardware-accelerated tiling
            if (c != null)
            {
                int startX = (int)fillRectangleBGRange.X;
                int startY = (int)fillRectangleBGRange.Y + startBgYPaint;
                int width = (int)fillRectangleBGRange.Width;
                int height = (int)fillRectangleBGRange.Height + 5 - startBgYPaint;

                using (var textureBrush = new System.Drawing.TextureBrush(c, System.Drawing.Drawing2D.WrapMode.Tile))
                {
                    textureBrush.TranslateTransform(startX, startY);
                    graphics.FillRectangle(textureBrush, startX, startY, width, height);
                }
            }

            // Frames
            if (n != null && s != null && w != null && e != null && ne != null && sw != null && nw != null && se != null)
            {
                const int MARGIN_HORIZONTAL_BORDER_PX = 1;
                int rightEdgeX = targetImageWidth - e.Width + MARGIN_HORIZONTAL_BORDER_PX;
                int rightCornerX = targetImageWidth - ne.Width + MARGIN_HORIZONTAL_BORDER_PX;
                int bottomEdgeY = targetImageHeight - sw.Height;

                // Fill top from (Top left to top right)
                for (int i = nw.Width; i <= (targetImageWidth - nw.Width); i += n.Width)
                    graphics.DrawImageUnscaled(n, i, 0);

                // Fill Bottom from (Bottom left to bottom right)
                for (int i = sw.Width; i <= (targetImageWidth - sw.Width); i += s.Width)
                    graphics.DrawImageUnscaled(s, i, targetImageHeight - s.Height);

                // Fill Left from (Bottom left to top left)
                for (int i = bottomEdgeY; i >= nw.Height; i -= w.Height)
                    graphics.DrawImageUnscaled(w, 0, i);

                // Fill right from (Bottom right to top right)
                for (int i = bottomEdgeY; i >= nw.Height; i -= e.Height)
                    graphics.DrawImageUnscaled(e, rightEdgeX, i);

                // Frame corners
                graphics.DrawImageUnscaled(nw, 0, 0); // top left
                graphics.DrawImageUnscaled(ne, rightCornerX, 0); // top right
                graphics.DrawImageUnscaled(sw, 0, bottomEdgeY); // bottom left
                graphics.DrawImageUnscaled(se, rightCornerX, bottomEdgeY); // bottom right
            }
        }
    }
}
