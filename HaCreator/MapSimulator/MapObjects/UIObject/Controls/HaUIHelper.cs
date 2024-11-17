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

using System.Drawing;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.MapObjects.UIObject.Controls {

    public class HaUIHelper {

        /// <summary>
        /// Calculates the offset for the alignment (start, center, end)
        /// </summary>
        /// <param name="total"></param>
        /// <param name="child"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateAlignmentOffset(int total, int child, HaUIAlignment alignment) {
            switch (alignment) {
                case HaUIAlignment.Center:
                    return (total - child) / 2;
                case HaUIAlignment.End:
                    return total - child;
                default: // HaUIAlignment.Start
                    return 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="toDrawUI"></param>
        /// <param name="color_bgFill"></param>
        /// <param name="ne"></param>
        /// <param name="nw"></param>
        /// <param name="se"></param>
        /// <param name="sw"></param>
        /// <param name="e"></param>
        /// <param name="w"></param>
        /// <param name="n"></param>
        /// <param name="s"></param>
        /// <param name="c">The frame background color</param>
        /// <param name="startBgXPaint">The y coordinates to add before painting the frame bg color</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static System.Drawing.Bitmap RenderAndMergeMinimapUIFrame(HaUIStackPanel toDrawUI, System.Drawing.Color color_bgFill, 
            Bitmap ne, Bitmap nw, Bitmap se, Bitmap sw,
            Bitmap e, Bitmap w, Bitmap n, Bitmap s,
            Bitmap c, int startBgYPaint) {
            HaUISize size = toDrawUI.GetSize();

            System.Drawing.Bitmap finalBitmap = new System.Drawing.Bitmap(size.Width, size.Height);

            // draw UI frame first
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(finalBitmap)) {
                // Frames and background
                UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, c, startBgYPaint, size.Width, size.Height);

                // then render the full thing on top of it
                System.Drawing.Bitmap miniMapWithoutFrameBitmap = toDrawUI.Render();

                // Now you can do whatever you want with finalBitmap,
                // like creating a Texture2D, etc.
                graphics.DrawImage(miniMapWithoutFrameBitmap, new System.Drawing.PointF(0, 0));
            }
            return finalBitmap;
        }
    }
}
