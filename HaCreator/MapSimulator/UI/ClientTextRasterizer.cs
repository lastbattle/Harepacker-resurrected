using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SD = System.Drawing;
using SDImaging = System.Drawing.Imaging;
using SDText = System.Drawing.Text;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class ClientTextRasterizer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly string _fontFamily;
        private readonly float _basePointSize;
        private readonly SD.FontStyle _fontStyle;
        private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _measureCache = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Dictionary<int, SD.Font> _fontCache = new Dictionary<int, SD.Font>();
        private readonly SD.Bitmap _measureBitmap = new SD.Bitmap(1, 1, SDImaging.PixelFormat.Format32bppArgb);
        private readonly SD.Graphics _measureGraphics;
        private readonly SD.StringFormat _stringFormat = SD.StringFormat.GenericTypographic;

        public ClientTextRasterizer(GraphicsDevice graphicsDevice, string fontFamily = "Segoe UI", float basePointSize = 13f, SD.FontStyle fontStyle = SD.FontStyle.Regular)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _fontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily;
            _basePointSize = basePointSize <= 0f ? 13f : basePointSize;
            _fontStyle = fontStyle;

            _measureGraphics = SD.Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = SDText.TextRenderingHint.AntiAliasGridFit;
            _measureGraphics.PageUnit = SD.GraphicsUnit.Pixel;
        }

        public Vector2 MeasureString(string text, float scale = 1.0f)
        {
            string normalizedText = text ?? string.Empty;
            if (normalizedText.Length == 0)
            {
                return Vector2.Zero;
            }

            string cacheKey = BuildCacheKey(normalizedText, scale);
            if (_measureCache.TryGetValue(cacheKey, out Vector2 cachedSize))
            {
                return cachedSize;
            }

            using SD.Font font = CreateScaledFont(scale);
            SD.SizeF size = _measureGraphics.MeasureString(normalizedText, font, SD.PointF.Empty, _stringFormat);
            if (size.Width <= 0f || size.Height <= 0f)
            {
                size = _measureGraphics.MeasureString(normalizedText, font);
            }

            Vector2 measuredSize = new Vector2(
                (float)Math.Ceiling(size.Width),
                (float)Math.Ceiling(size.Height));
            _measureCache[cacheKey] = measuredSize;
            return measuredSize;
        }

        public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = 1.0f)
        {
            if (spriteBatch == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            Texture2D texture = GetOrCreateTexture(text, scale);
            if (texture == null)
            {
                return;
            }

            spriteBatch.Draw(texture, position, color);
        }

        private Texture2D GetOrCreateTexture(string text, float scale)
        {
            string cacheKey = BuildCacheKey(text, scale);
            if (_textureCache.TryGetValue(cacheKey, out Texture2D cachedTexture)
                && cachedTexture != null
                && !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            Vector2 measuredSize = MeasureString(text, scale);
            int width = Math.Max(1, (int)Math.Ceiling(measuredSize.X));
            int height = Math.Max(1, (int)Math.Ceiling(measuredSize.Y));

            using SD.Bitmap bitmap = new SD.Bitmap(width, height, SDImaging.PixelFormat.Format32bppArgb);
            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);
            using SD.Font font = CreateScaledFont(scale);
            using SD.SolidBrush brush = new SD.SolidBrush(SD.Color.White);

            graphics.Clear(SD.Color.Transparent);
            graphics.TextRenderingHint = SDText.TextRenderingHint.AntiAliasGridFit;
            graphics.PageUnit = SD.GraphicsUnit.Pixel;
            graphics.DrawString(text, font, brush, 0f, 0f, _stringFormat);

            Texture2D texture = bitmap.ToTexture2D(_graphicsDevice);
            _textureCache[cacheKey] = texture;
            return texture;
        }

        private SD.Font CreateScaledFont(float scale)
        {
            int fontSizeKey = QuantizeScale(scale);
            if (_fontCache.TryGetValue(fontSizeKey, out SD.Font cachedFont))
            {
                return (SD.Font)cachedFont.Clone();
            }

            float pointSize = Math.Max(1f, _basePointSize * Math.Max(0.1f, scale));
            SD.Font font = new SD.Font(_fontFamily, pointSize, _fontStyle, SD.GraphicsUnit.Point);
            _fontCache[fontSizeKey] = font;
            return (SD.Font)font.Clone();
        }

        private static int QuantizeScale(float scale)
        {
            return (int)Math.Round(Math.Max(0.1f, scale) * 1000f);
        }

        private static string BuildCacheKey(string text, float scale)
        {
            return QuantizeScale(scale).ToString(CultureInfo.InvariantCulture) + "|" + text;
        }
    }
}
