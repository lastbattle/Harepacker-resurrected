using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal static class ClientTextDrawing
    {
        private static readonly Dictionary<GraphicsDevice, ClientTextRasterizer> Rasterizers = new();
        private static readonly object Sync = new();

        public static Vector2 Measure(SpriteBatch spriteBatch, string text, float scale = 1.0f, SpriteFont fallbackFont = null)
        {
            return Measure(spriteBatch?.GraphicsDevice, text, scale, fallbackFont);
        }

        public static Vector2 Measure(GraphicsDevice graphicsDevice, string text, float scale = 1.0f, SpriteFont fallbackFont = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            ClientTextRasterizer rasterizer = GetRasterizer(graphicsDevice);
            if (rasterizer != null)
            {
                return rasterizer.MeasureString(text, scale);
            }

            return fallbackFont?.MeasureString(text) * scale ?? Vector2.Zero;
        }

        public static void Draw(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = 1.0f, SpriteFont fallbackFont = null, float? maxWidth = null)
        {
            if (spriteBatch == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            ClientTextRasterizer rasterizer = GetRasterizer(spriteBatch.GraphicsDevice);
            if (rasterizer != null)
            {
                rasterizer.DrawString(spriteBatch, text, position, color, scale, maxWidth);
                return;
            }

            if (fallbackFont == null)
            {
                return;
            }

            spriteBatch.DrawString(fallbackFont, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        public static void DrawShadowed(SpriteBatch spriteBatch, string text, Vector2 position, Color textColor, SpriteFont fallbackFont = null, float scale = 1.0f)
        {
            if (spriteBatch == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Draw(spriteBatch, text, position + Vector2.One, Color.Black, scale, fallbackFont);
            Draw(spriteBatch, text, position, textColor, scale, fallbackFont);
        }

        public static void DrawOutlined(SpriteBatch spriteBatch, string text, Vector2 position, Color textColor, Color outlineColor, SpriteFont fallbackFont = null, float scale = 1.0f)
        {
            if (spriteBatch == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Draw(spriteBatch, text, position + new Vector2(-1, 0), outlineColor, scale, fallbackFont);
            Draw(spriteBatch, text, position + new Vector2(1, 0), outlineColor, scale, fallbackFont);
            Draw(spriteBatch, text, position + new Vector2(0, -1), outlineColor, scale, fallbackFont);
            Draw(spriteBatch, text, position + new Vector2(0, 1), outlineColor, scale, fallbackFont);
            Draw(spriteBatch, text, position, textColor, scale, fallbackFont);
        }

        private static ClientTextRasterizer GetRasterizer(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return null;
            }

            lock (Sync)
            {
                if (Rasterizers.TryGetValue(graphicsDevice, out ClientTextRasterizer existing))
                {
                    return existing;
                }

                ClientTextRasterizer created = new ClientTextRasterizer(graphicsDevice);
                Rasterizers[graphicsDevice] = created;
                return created;
            }
        }
    }
}
