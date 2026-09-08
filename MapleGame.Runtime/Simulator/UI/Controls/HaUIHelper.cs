using System;
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
        public static Bitmap RenderAndMergeMinimapUIFrame(HaUIStackPanel toDrawUI, Color color_bgFill, 
            Bitmap ne, Bitmap nw, Bitmap se, Bitmap sw,
            Bitmap e, Bitmap w, Bitmap n, Bitmap s,
            Bitmap c, int startBgYPaint) {
            HaUISize size = toDrawUI.GetSize();

            Bitmap finalBitmap = new Bitmap(size.Width, size.Height);

            // draw UI frame first
            using (Graphics graphics = Graphics.FromImage(finalBitmap)) {
                // Frames and background
                UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, c, startBgYPaint, size.Width, size.Height);

                // then render the full thing on top of it
                Bitmap miniMapWithoutFrameBitmap = toDrawUI.Render();

                // Now you can do whatever you want with finalBitmap,
                // like creating a Texture2D, etc.
                graphics.DrawImage(miniMapWithoutFrameBitmap, new PointF(0, 0));
            }
            return finalBitmap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitmap RenderAndMergeMinimapCollapsedBar(
            HaUIStackPanel toDrawUI,
            Color color_bgFill,
            Bitmap left,
            Bitmap center,
            Bitmap right) {
            HaUISize size = toDrawUI.GetSize();
            int contentWidth = Math.Max(1, size.Width);
            int contentHeight = Math.Max(1, size.Height);
            int frameHeight = Math.Max(left?.Height ?? 0, Math.Max(center?.Height ?? 0, right?.Height ?? 0));
            int finalWidth = Math.Max(contentWidth, (left?.Width ?? 0) + (right?.Width ?? 0) + 1);
            int finalHeight = Math.Max(contentHeight, frameHeight > 0 ? frameHeight : contentHeight);

            Bitmap finalBitmap = new Bitmap(finalWidth, finalHeight);

            using (Graphics graphics = Graphics.FromImage(finalBitmap)) {
                using (var backgroundBrush = new SolidBrush(color_bgFill))
                {
                    graphics.FillRectangle(backgroundBrush, 0, 0, finalWidth, finalHeight);
                }

                if (center != null)
                {
                    int startX = left?.Width ?? 0;
                    int endX = finalWidth - (right?.Width ?? 0);
                    int tileWidth = Math.Max(1, center.Width);
                    int centerY = Math.Max(0, (finalHeight - center.Height) / 2);
                    for (int x = startX; x < endX; x += tileWidth)
                    {
                        graphics.DrawImageUnscaled(center, x, centerY);
                    }
                }

                if (left != null)
                {
                    graphics.DrawImageUnscaled(left, 0, Math.Max(0, (finalHeight - left.Height) / 2));
                }

                if (right != null)
                {
                    graphics.DrawImageUnscaled(right, finalWidth - right.Width, Math.Max(0, (finalHeight - right.Height) / 2));
                }

                Bitmap contentBitmap = toDrawUI.Render();
                int contentY = Math.Max(0, (finalHeight - contentBitmap.Height) / 2);
                graphics.DrawImage(contentBitmap, new PointF(0, contentY));
            }

            return finalBitmap;
        }
    }
}
