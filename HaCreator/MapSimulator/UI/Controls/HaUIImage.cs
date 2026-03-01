using System;
using System.Drawing;

namespace HaCreator.MapSimulator.UI.Controls {

    public class HaUIImage : IHaUIRenderable {
        private HaUIInfo _info;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="bitmapInfo"></param>
        public HaUIImage(HaUIInfo bitmapInfo) {
            this._info = bitmapInfo;

            if (bitmapInfo.Bitmap == null) {
                throw new ArgumentException("Bitmap cannot be null for HaUIImage");
            }
        }

        public void AddRenderable(IHaUIRenderable renderable) {
            throw new Exception("Not supported in HaUIImage.");
        }

        public Bitmap Render() {
            HaUISize size = GetSize();
            int totalWidth = size.Width;
            int totalHeight = size.Height;

            Bitmap bitmap = new Bitmap(totalWidth, totalHeight);
            using (Graphics g = Graphics.FromImage(bitmap)) {
                g.Clear(Color.Transparent);

                // Calculate alignment
                int x = HaUIHelper.CalculateAlignmentOffset(_info.Bitmap.Width, totalWidth, _info.HorizontalAlignment);
                int y = HaUIHelper.CalculateAlignmentOffset(_info.Bitmap.Height, totalHeight, _info.VerticalAlignment);

                g.DrawImage(_info.Bitmap, x, y, _info.Bitmap.Width, _info.Bitmap.Height);
            }

            return bitmap;
        }

        public HaUISize GetSize() {
            int width = _info.Bitmap.Width + _info.Margins.Bottom;
            int height = _info.Bitmap.Height + _info.Margins.Top;
            //if (bitmapInfo.HasUIPadding) {
                width += _info.Padding.Top;
                height += _info.Padding.Bottom;
            //}
            // normalise to MinHeight and MinWidth
            return new HaUISize(
                Math.Max(_info.MinWidth, width),
                Math.Max(_info.MinHeight, height));
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}
