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

namespace HaCreator.MapSimulator.MapObjects.UIObject.Controls {

    public class HaUIText : IHaUIRenderable {
        private string _text;
        private Font font;
        private Color color;

        private HaUIInfo _info;

        public HaUIText(string text, Color color, string font, float fontSize, float userScreenScaleFactor) {
            this._text = text;
            this.font = new System.Drawing.Font(font, fontSize / userScreenScaleFactor);
            this.color = color;

            this._info = new HaUIInfo();
        }

        public HaUIText(string text, Font font, Color color) {
            this._text = text;
            this.font = font;
            this.color = color;
            this._info = new HaUIInfo();
        }


        public Bitmap Render() {
            HaUISize size = GetSize();

            Bitmap textBitmap = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(textBitmap)) {
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(color)) {
                    g.DrawString(_text, font, brush, _info.Margins.Left, _info.Margins.Top);
                }
            }
            return textBitmap;
        }

        public HaUISize GetSize() {
            HaUISize size = GetTextSizeWithoutMargins();
            return new HaUISize(size.Width + _info.Margins.Left + _info.Margins.Right, size.Height + _info.Margins.Top + _info.Margins.Bottom);
        }

        private HaUISize GetTextSizeWithoutMargins() {
            using (Bitmap tempBitmap = new Bitmap(1, 1))
            using (Graphics tempGraphics = Graphics.FromImage(tempBitmap)) {
                SizeF size = tempGraphics.MeasureString(_text, font);
                return new HaUISize((int)size.Width, (int) size.Height);
            }
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}
