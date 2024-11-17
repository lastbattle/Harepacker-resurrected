/* Copyright(c) 2023, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/


using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
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
            graphics.FillRectangle(new SolidBrush(backgroundColor), fillRectangleBGRange);

            // Fill background with bitmap
            if (c != null)
            {
                // Calculate the actual drawing boundaries
                int startX = (int)fillRectangleBGRange.X;
                int startY = (int)fillRectangleBGRange.Y + startBgYPaint;
                int endX = (int)(fillRectangleBGRange.X + fillRectangleBGRange.Width);
                int endY = (int)(fillRectangleBGRange.Y + fillRectangleBGRange.Height) + 5;

                // Draw the colored background pattern within the boundaries
                for (int y = startY; y < endY; y += c.Height)
                {
                    for (int x = startX; x < endX; x += c.Width)
                    {
                        // Calculate the remaining space for this tile
                        int remainingWidth = Math.Min(c.Width, endX - x);
                        int remainingHeight = Math.Min(c.Height, endY - y);

                        if (remainingWidth <= 0 || remainingHeight <= 0)
                            continue;

                        if (remainingWidth == c.Width && remainingHeight == c.Height)
                        {
                            // Draw full tile
                            graphics.DrawImage(c, new Point(x, y));
                        }
                        else
                        {
                            // Draw partial tile for edge cases
                            Rectangle sourceRect = new(0, 0, remainingWidth, remainingHeight);
                            Rectangle destRect = new(x, y, remainingWidth, remainingHeight);

                            graphics.DrawImage(c, destRect, sourceRect, GraphicsUnit.Pixel);
                        }
                    }
                }
            }

            // Frames
            if (n != null && s != null && w != null && e != null && ne != null && sw != null && nw != null && se != null)
            {
                const int MARGIN_HORIZONTAL_BORDER_PX = 1;

                for (int i = nw.Width; i <= (targetImageWidth - nw.Width); i += n.Width) // Fill top from (Top left to top right)
                    graphics.DrawImage(n.ToImage(), new PointF(i, 0));

                for (int i = sw.Width; i <= (targetImageWidth - sw.Width); i += s.Width) // Fill Bottom from (Bottom left to bottom right)
                    graphics.DrawImage(s.ToImage(), new PointF(i, targetImageHeight - s.Height));

                for (int i = targetImageHeight - sw.Height; i >= nw.Height; i -= w.Height) // Fill Left from (Bottom left to top left)
                    graphics.DrawImage(w.ToImage(), new PointF(0, i));

                for (int i = targetImageHeight - sw.Height; i >= nw.Height; i -= e.Height) // Fill right from (Bottom right to top right)
                    graphics.DrawImage(e.ToImage(), new PointF(targetImageWidth - e.Width + MARGIN_HORIZONTAL_BORDER_PX, i));

                // Frame corners
                graphics.DrawImage(nw.ToImage(), new PointF(0, 0)); // top left
                graphics.DrawImage(ne.ToImage(), new PointF(targetImageWidth - ne.Width + MARGIN_HORIZONTAL_BORDER_PX, 0)); // top right

                graphics.DrawImage(sw.ToImage(), new PointF(0, targetImageHeight - sw.Height)); // bottom left
                graphics.DrawImage(se.ToImage(), new PointF(targetImageWidth - ne.Width + MARGIN_HORIZONTAL_BORDER_PX, targetImageHeight - sw.Height)); // bottom right
            }
        }
    }
}
