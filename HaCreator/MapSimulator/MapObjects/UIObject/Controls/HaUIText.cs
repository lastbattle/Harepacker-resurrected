using System;
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

        public void AddRenderable(IHaUIRenderable renderable) {
            throw new Exception("Not supported in HaUIText.");
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
