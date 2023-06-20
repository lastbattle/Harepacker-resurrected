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
using System.Drawing;

namespace HaCreator.MapSimulator.MapObjects.UIObject.Controls {

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
            return new HaUISize(width, height);
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}
