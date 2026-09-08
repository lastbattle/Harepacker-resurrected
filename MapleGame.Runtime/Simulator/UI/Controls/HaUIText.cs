using System;
using System.Drawing;

namespace HaCreator.MapSimulator.UI.Controls {

    public class HaUIText : IHaUIRenderable {
        private string _text;
        private Font _font;
        private Color _color;

        private HaUIInfo _info;

        public HaUIText(string text, Color _color, string _font, float fontSize, float userScreenScaleFactor) {
            this._text = text;
            this._font = new System.Drawing.Font(_font, fontSize / userScreenScaleFactor);
            this._color = _color;

            this._info = new HaUIInfo();
        }

        public HaUIText(string text, Font _font, Color _color) {
            this._text = text;
            this._font = _font;
            this._color = _color;
            this._info = new HaUIInfo();
        }

        public void AddRenderable(IHaUIRenderable renderable) {
            throw new Exception("Not supported in HaUIText.");
        }

        public Bitmap Render() {
            HaUISize size = GetSize();

            Bitmap textBitmap = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(textBitmap)) {
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(_color)) {
                    g.DrawString(_text, _font, brush, _info.Margins.Left, _info.Margins.Top);
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
                SizeF size = tempGraphics.MeasureString(_text, _font);
                return new HaUISize((int)size.Width, (int) size.Height);
            }
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}
