using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapleLib.Converters;

namespace HaCreator.MapSimulator
{
    public class UIFrameHelper
    {
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
            graphics.FillRectangle(new SolidBrush(backgroundColor), fillRectangleBGRange);

            // Fill background with bitmap
            if (c != null)
            {
                for (int column = (int)fillRectangleBGRange.Y; column < fillRectangleBGRange.Width; column += c.Width)
                    for (int row = (int)fillRectangleBGRange.X; row < fillRectangleBGRange.Height; row += c.Height)
                        graphics.DrawImage(c.ToImage(), new PointF(row, column));
            }

            // Frames
            if (n != null && s != null && w != null && e != null && ne != null && sw != null && nw != null && se != null)
            {
                for (int i = nw.Width; i <= (targetImageWidth - nw.Width); i += n.Width) // Fill top from (Top left to top right)
                    graphics.DrawImage(n.ToImage(), new PointF(i, 0));

                for (int i = sw.Width; i <= (targetImageWidth - sw.Width); i += s.Width) // Fill Bottom from (Bottom left to bottom right)
                    graphics.DrawImage(s.ToImage(), new PointF(i, targetImageHeight - s.Height));

                for (int i = targetImageHeight - sw.Height; i >= nw.Height; i -= w.Height) // Fill Left from (Bottom left to top left)
                    graphics.DrawImage(w.ToImage(), new PointF(0, i));

                for (int i = targetImageHeight - sw.Height; i >= nw.Height; i -= e.Height) // Fill right from (Bottom right to top right)
                    graphics.DrawImage(e.ToImage(), new PointF(targetImageWidth - e.Width, i));

                // Frame corners
                graphics.DrawImage(ne.ToImage(), new PointF(targetImageWidth - ne.Width, 0)); // top right
                graphics.DrawImage(nw.ToImage(), new PointF(0, 0)); // top left
                graphics.DrawImage(se.ToImage(), new PointF(targetImageWidth - ne.Width, targetImageHeight - sw.Height)); // bottom right
                graphics.DrawImage(sw.ToImage(), new PointF(0, targetImageHeight - sw.Height)); // bottom left
            }
        }
    }
}
