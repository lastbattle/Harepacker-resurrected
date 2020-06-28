using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HaRepacker.Converter;

namespace HaCreator.MapSimulator
{
    public class UIFrameHelper
    {
        /// <summary>
        /// Draws the frame of a UI
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="background">Background color of the frame</param>
        /// <param name="ne">Top right</param>
        /// <param name="nw">Top left</param>
        /// <param name="se">Bottom right</param>
        /// <param name="sw">Bottom left</param>
        /// <param name="e">Right</param>
        /// <param name="w">Left</param>
        /// <param name="n">Top</param>
        /// <param name="s">Bottom</param>
        /// <param name="c">Fills throughout the entire row and column. This path is optional</param>
        /// <param name="targetImageWidth"></param>
        /// <param name="targetImageHeight"></param>
        public static void DrawUIFrame(System.Drawing.Graphics graphics,
            System.Drawing.Color backgroundColor,
            Bitmap ne, Bitmap nw, Bitmap se, Bitmap sw,
             Bitmap e, Bitmap w, Bitmap n, Bitmap s,
             Bitmap c,
            int targetImageWidth, int targetImageHeight)
        {
            RectangleF fillRectangleBGRange = new RectangleF(
                w != null ? (w.Width / 3) : 2,
                n != null ? (n.Height / 3) : 2,
                w != null ? (targetImageWidth - (w.Width / 2) - 1) : (targetImageWidth - (2 / 2)),
                n != null ? (targetImageHeight - (n.Height / 2) - 1) : (targetImageHeight - (2 / 2) - 1));

            // Background color
            graphics.FillRectangle(new System.Drawing.SolidBrush(backgroundColor), fillRectangleBGRange);

            // Fill background with bitmap
            if (c != null)
            {
                for (int column = (int)fillRectangleBGRange.Y; column < fillRectangleBGRange.Width; column += c.Width)
                    for (int row = (int)fillRectangleBGRange.X; row < fillRectangleBGRange.Height; row += c.Height)
                        graphics.DrawImage(c.ToImage(), new System.Drawing.PointF(row, column));
            }

            // Frames
            if (n != null && s != null && w != null && e != null && ne != null && sw != null && nw != null && se != null)
            {
                for (int i = nw.Width; i < (targetImageWidth - nw.Width); i += n.Width) // Fill top from (Top left to top right)
                    graphics.DrawImage(n.ToImage(), new System.Drawing.PointF(i, 0));

                for (int i = sw.Width; i < (targetImageWidth - sw.Width); i += s.Width) // Fill Bottom from (Bottom left to bottom right)
                    graphics.DrawImage(s.ToImage(), new System.Drawing.PointF(i, targetImageHeight - se.Height));

                for (int i = targetImageHeight - sw.Height; i >= nw.Height; i -= w.Height) // Fill Left from (Bottom left to top left)
                    graphics.DrawImage(w.ToImage(), new System.Drawing.PointF(0, i));

                for (int i = targetImageHeight - sw.Height; i >= nw.Height; i -= e.Height) // Fill right from (Bottom right to top right)
                    graphics.DrawImage(e.ToImage(), new System.Drawing.PointF(targetImageWidth - e.Width, i));

                // Frame corners
                graphics.DrawImage(ne.ToImage(), new System.Drawing.PointF(targetImageWidth - ne.Width, 0)); // top right
                graphics.DrawImage(nw.ToImage(), new System.Drawing.PointF(0, 0)); // top left
                graphics.DrawImage(se.ToImage(), new System.Drawing.PointF(targetImageWidth - ne.Width, targetImageHeight - sw.Height)); // bottom right
                graphics.DrawImage(sw.ToImage(), new System.Drawing.PointF(0, targetImageHeight - sw.Height)); // bottom left
            }
        }
    }
}
