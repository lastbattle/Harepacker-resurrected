using System.Drawing;

namespace HaCreator.MapSimulator.UI.Controls {

    /// <summary>
    /// The HaUIInfo that contains Bitmap, HaUIMargins, and HaUIPadding, to be stored into the HaUIGrid or HaUIStackPanel
    /// </summary>
    public class HaUIInfo {
        public Bitmap Bitmap;
        public HaUIMargin Margins;
        public HaUIMargin Padding;
        public HaUIAlignment HorizontalAlignment = HaUIAlignment.Start;
        public HaUIAlignment VerticalAlignment = HaUIAlignment.Start;

        /// <summary>
        /// The min width of this IHaUIRenderable
        /// </summary>
        public int MinHeight;

        /// <summary>
        /// The max width of this IHaUIRenderable
        /// </summary>
        public int MinWidth;
    }
}
