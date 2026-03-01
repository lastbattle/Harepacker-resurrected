using System.Drawing;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.UI.Controls {

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
