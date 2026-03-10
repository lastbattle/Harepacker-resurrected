using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class NpcInteractionOverlay
    {
        private const int WindowWidth = 440;
        private const int WindowHeight = 248;
        private const int Padding = 18;
        private const int CloseButtonSize = 22;
        private const int ButtonWidth = 84;
        private const int ButtonHeight = 28;
        private const int ButtonGap = 10;

        private readonly Texture2D _pixel;
        private readonly List<string> _pages = new();

        private SpriteFont _font;
        private string _npcName = "NPC";
        private int _currentPage;

        public NpcInteractionOverlay(GraphicsDevice device)
        {
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public bool IsVisible { get; private set; }

        public void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Open(string npcName, IReadOnlyList<string> pages)
        {
            _npcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
            _pages.Clear();

            if (pages != null)
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(pages[i]))
                    {
                        _pages.Add(pages[i].Trim());
                    }
                }
            }

            if (_pages.Count == 0)
            {
                _pages.Add("The NPC does not have dialogue text in the loaded data.");
            }

            _currentPage = 0;
            IsVisible = true;
        }

        public void Close()
        {
            IsVisible = false;
        }

        public bool ContainsPoint(int x, int y, int renderWidth, int renderHeight)
        {
            return IsVisible && GetWindowRectangle(renderWidth, renderHeight).Contains(x, y);
        }

        public bool HandleMouse(MouseState mouseState, MouseState previousMouseState, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                previousMouseState.LeftButton == ButtonState.Pressed;
            if (!leftReleased)
            {
                return false;
            }

            Rectangle windowRect = GetWindowRectangle(renderWidth, renderHeight);
            Point mousePoint = new Point(mouseState.X, mouseState.Y);

            if (!windowRect.Contains(mousePoint))
            {
                Close();
                return true;
            }

            if (GetCloseButtonRectangle(windowRect).Contains(mousePoint))
            {
                Close();
                return true;
            }

            if (GetPrevButtonRectangle(windowRect).Contains(mousePoint) && _currentPage > 0)
            {
                _currentPage--;
                return true;
            }

            if (GetNextButtonRectangle(windowRect).Contains(mousePoint))
            {
                if (_currentPage < _pages.Count - 1)
                {
                    _currentPage++;
                }
                else
                {
                    Close();
                }

                return true;
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return;
            }

            Rectangle windowRect = GetWindowRectangle(renderWidth, renderHeight);
            DrawPanel(spriteBatch, windowRect, new Color(18, 25, 39, 235), new Color(235, 218, 170));

            Rectangle titleBar = new Rectangle(windowRect.X, windowRect.Y, windowRect.Width, 38);
            spriteBatch.Draw(_pixel, titleBar, new Color(53, 79, 117, 255));

            DrawText(spriteBatch, _npcName, new Vector2(windowRect.X + Padding, windowRect.Y + 10), Color.White);

            Rectangle closeRect = GetCloseButtonRectangle(windowRect);
            DrawPanel(spriteBatch, closeRect, new Color(130, 51, 51, 255), new Color(255, 220, 220));
            DrawCenteredText(spriteBatch, "X", closeRect, Color.White);

            Rectangle textRect = new Rectangle(
                windowRect.X + Padding,
                windowRect.Y + 54,
                windowRect.Width - (Padding * 2),
                windowRect.Height - 116);

            DrawWrappedText(spriteBatch, GetCurrentPageText(), textRect, new Color(246, 244, 238));
            DrawPageIndicator(spriteBatch, windowRect);

            Rectangle prevRect = GetPrevButtonRectangle(windowRect);
            Rectangle nextRect = GetNextButtonRectangle(windowRect);

            DrawButton(spriteBatch, prevRect, "Prev", _currentPage > 0);
            DrawButton(spriteBatch, nextRect, _currentPage < _pages.Count - 1 ? "Next" : "Close", true);
        }

        private string GetCurrentPageText()
        {
            if (_currentPage < 0 || _currentPage >= _pages.Count)
            {
                return string.Empty;
            }

            return _pages[_currentPage];
        }

        private void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color fill, Color border)
        {
            spriteBatch.Draw(_pixel, rect, fill);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string label, bool enabled)
        {
            Color fill = enabled ? new Color(71, 104, 149, 255) : new Color(70, 70, 70, 200);
            Color border = enabled ? new Color(228, 216, 188) : new Color(130, 130, 130);

            DrawPanel(spriteBatch, rect, fill, border);
            DrawCenteredText(spriteBatch, label, rect, Color.White);
        }

        private void DrawPageIndicator(SpriteBatch spriteBatch, Rectangle windowRect)
        {
            string pageText = $"{_currentPage + 1}/{Math.Max(1, _pages.Count)}";
            Vector2 size = MeasureText(pageText);
            Vector2 position = new Vector2(windowRect.Right - Padding - size.X, windowRect.Bottom - 62);
            DrawText(spriteBatch, pageText, position, new Color(210, 210, 210));
        }

        private void DrawWrappedText(SpriteBatch spriteBatch, string text, Rectangle bounds, Color color)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] paragraphs = text.Replace("\r", string.Empty).Split('\n');
            float y = bounds.Y;

            for (int i = 0; i < paragraphs.Length; i++)
            {
                foreach (string line in WrapLine(paragraphs[i], bounds.Width))
                {
                    spriteBatch.DrawString(_font, line, new Vector2(bounds.X, y), color);
                    y += _font.LineSpacing;

                    if (y > bounds.Bottom - _font.LineSpacing)
                    {
                        return;
                    }
                }

                y += 4f;
            }
        }

        private IEnumerable<string> WrapLine(string text, float maxWidth)
        {
            if (_font == null)
            {
                yield return text ?? string.Empty;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                yield return string.Empty;
                yield break;
            }

            string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;

            for (int i = 0; i < words.Length; i++)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                if (_font.MeasureString(candidate).X <= maxWidth)
                {
                    currentLine = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    yield return currentLine;
                    currentLine = words[i];
                }
                else
                {
                    yield return words[i];
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, string text, Rectangle rect, Color color)
        {
            Vector2 size = MeasureText(text);
            Vector2 position = new Vector2(
                rect.X + ((rect.Width - size.X) / 2f),
                rect.Y + ((rect.Height - size.Y) / 2f));

            DrawText(spriteBatch, text, position, color);
        }

        private void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            spriteBatch.DrawString(_font, text, position, color);
        }

        private Vector2 MeasureText(string text)
        {
            return _font?.MeasureString(text) ?? Vector2.Zero;
        }

        private static Rectangle GetWindowRectangle(int renderWidth, int renderHeight)
        {
            int x = (renderWidth - WindowWidth) / 2;
            int y = Math.Max(32, renderHeight - WindowHeight - 140);
            return new Rectangle(x, y, WindowWidth, WindowHeight);
        }

        private static Rectangle GetCloseButtonRectangle(Rectangle windowRect)
        {
            return new Rectangle(windowRect.Right - CloseButtonSize - 10, windowRect.Y + 8, CloseButtonSize, CloseButtonSize);
        }

        private static Rectangle GetPrevButtonRectangle(Rectangle windowRect)
        {
            int y = windowRect.Bottom - ButtonHeight - 18;
            int x = windowRect.Right - (ButtonWidth * 2) - ButtonGap - Padding;
            return new Rectangle(x, y, ButtonWidth, ButtonHeight);
        }

        private static Rectangle GetNextButtonRectangle(Rectangle windowRect)
        {
            Rectangle prevRect = GetPrevButtonRectangle(windowRect);
            return new Rectangle(prevRect.Right + ButtonGap, prevRect.Y, ButtonWidth, ButtonHeight);
        }
    }
}
