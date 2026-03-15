using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class NpcInteractionOverlay
    {
        private const int WindowWidth = 560;
        private const int WindowHeight = 286;
        private const int Padding = 18;
        private const int CloseButtonSize = 22;
        private const int ButtonWidth = 84;
        private const int ButtonHeight = 28;
        private const int ButtonGap = 10;
        private const int EntryListWidth = 172;
        private const int ChoiceButtonWidth = 94;

        private readonly Texture2D _pixel;
        private readonly List<NpcInteractionEntry> _entries = new();
        private readonly Stack<PageContext> _pageContextStack = new();

        private SpriteFont _font;
        private string _npcName = "NPC";
        private int _selectedEntryIndex;
        private int _currentPage;
        private IReadOnlyList<NpcInteractionPage> _currentPages = Array.Empty<NpcInteractionPage>();

        private readonly struct PageContext
        {
            public PageContext(IReadOnlyList<NpcInteractionPage> pages, int pageIndex)
            {
                Pages = pages;
                PageIndex = pageIndex;
            }

            public IReadOnlyList<NpcInteractionPage> Pages { get; }
            public int PageIndex { get; }
        }

        public NpcInteractionOverlay(GraphicsDevice device)
        {
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public bool IsVisible { get; private set; }

        public NpcInteractionEntry SelectedEntry =>
            _selectedEntryIndex >= 0 && _selectedEntryIndex < _entries.Count ? _entries[_selectedEntryIndex] : null;

        public void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Open(NpcInteractionState state)
        {
            _npcName = string.IsNullOrWhiteSpace(state?.NpcName) ? "NPC" : state.NpcName;
            _entries.Clear();

            if (state?.Entries != null)
            {
                for (int i = 0; i < state.Entries.Count; i++)
                {
                    if (state.Entries[i] != null)
                    {
                        _entries.Add(state.Entries[i]);
                    }
                }
            }

            if (_entries.Count == 0)
            {
                _entries.Add(new NpcInteractionEntry
                {
                    EntryId = 0,
                    Kind = NpcInteractionEntryKind.Talk,
                    Title = "Talk",
                    Pages = new[]
                    {
                        new NpcInteractionPage
                        {
                            Text = "The NPC does not have dialogue text in the loaded data."
                        }
                    }
                });
            }

            _selectedEntryIndex = 0;
            if (state != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].EntryId == state.SelectedEntryId)
                    {
                        _selectedEntryIndex = i;
                        break;
                    }
                }
            }

            ResetCurrentPages();
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

        public NpcInteractionOverlayResult HandleMouse(MouseState mouseState, MouseState previousMouseState, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return default;
            }

            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                previousMouseState.LeftButton == ButtonState.Pressed;
            if (!leftReleased)
            {
                return default;
            }

            Rectangle windowRect = GetWindowRectangle(renderWidth, renderHeight);
            Point mousePoint = new Point(mouseState.X, mouseState.Y);

            if (!windowRect.Contains(mousePoint))
            {
                Close();
                return new NpcInteractionOverlayResult(true, false);
            }

            if (GetCloseButtonRectangle(windowRect).Contains(mousePoint))
            {
                Close();
                return new NpcInteractionOverlayResult(true, false);
            }

            Rectangle entryListRect = GetEntryListRectangle(windowRect);
            if (entryListRect.Contains(mousePoint))
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (!GetEntryRectangle(entryListRect, i).Contains(mousePoint))
                    {
                        continue;
                    }

                    _selectedEntryIndex = i;
                    ResetCurrentPages();
                    return new NpcInteractionOverlayResult(true, false);
                }
            }

            Rectangle[] choiceRects = GetChoiceButtonRectangles(windowRect, GetCurrentChoices().Count);
            for (int i = 0; i < choiceRects.Length; i++)
            {
                if (!choiceRects[i].Contains(mousePoint))
                {
                    continue;
                }

                NpcInteractionChoice choice = GetCurrentChoices()[i];
                if (choice.Pages.Count == 0)
                {
                    continue;
                }

                _pageContextStack.Push(new PageContext(_currentPages, _currentPage));
                _currentPages = choice.Pages;
                _currentPage = 0;
                return new NpcInteractionOverlayResult(true, false);
            }

            if (GetPrevButtonRectangle(windowRect).Contains(mousePoint))
            {
                if (_currentPage > 0)
                {
                    _currentPage--;
                }
                else if (_pageContextStack.Count > 0)
                {
                    PageContext context = _pageContextStack.Pop();
                    _currentPages = context.Pages;
                    _currentPage = context.PageIndex;
                }

                return new NpcInteractionOverlayResult(true, false);
            }

            if (GetNextButtonRectangle(windowRect).Contains(mousePoint))
            {
                if (_currentPage < GetCurrentPages().Count - 1)
                {
                    _currentPage++;
                }
                else
                {
                    Close();
                }

                return new NpcInteractionOverlayResult(true, false);
            }

            Rectangle primaryRect = GetPrimaryButtonRectangle(windowRect);
            if (!string.IsNullOrEmpty(GetPrimaryButtonText()) && primaryRect.Contains(mousePoint))
            {
                return new NpcInteractionOverlayResult(true, SelectedEntry?.PrimaryActionEnabled == true);
            }

            return new NpcInteractionOverlayResult(true, false);
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

            Rectangle entryListRect = GetEntryListRectangle(windowRect);
            DrawPanel(spriteBatch, entryListRect, new Color(27, 35, 49, 220), new Color(112, 126, 153));
            DrawEntryList(spriteBatch, entryListRect);

            Rectangle textRect = new Rectangle(
                entryListRect.Right + Padding,
                windowRect.Y + 54,
                windowRect.Width - EntryListWidth - (Padding * 3),
                windowRect.Height - 116);

            DrawEntryHeader(spriteBatch, textRect);
            Rectangle bodyRect = new Rectangle(textRect.X, textRect.Y + 38, textRect.Width, textRect.Height - 38);
            DrawWrappedText(spriteBatch, GetCurrentPageText(), bodyRect, new Color(246, 244, 238));
            DrawPageIndicator(spriteBatch, windowRect);

            Rectangle prevRect = GetPrevButtonRectangle(windowRect);
            Rectangle nextRect = GetNextButtonRectangle(windowRect);
            Rectangle primaryRect = GetPrimaryButtonRectangle(windowRect);

            IReadOnlyList<NpcInteractionChoice> choices = GetCurrentChoices();
            Rectangle[] choiceRects = GetChoiceButtonRectangles(windowRect, choices.Count);
            for (int i = 0; i < choiceRects.Length; i++)
            {
                DrawButton(spriteBatch, choiceRects[i], choices[i].Label, true);
            }

            DrawButton(spriteBatch, prevRect, "Prev", _currentPage > 0 || _pageContextStack.Count > 0);
            DrawButton(spriteBatch, nextRect, _currentPage < GetCurrentPages().Count - 1 ? "Next" : "Close", true);

            string primaryButtonText = GetPrimaryButtonText();
            if (!string.IsNullOrEmpty(primaryButtonText))
            {
                DrawButton(spriteBatch, primaryRect, primaryButtonText, SelectedEntry?.PrimaryActionEnabled == true);
            }
        }

        private void ResetCurrentPages()
        {
            _pageContextStack.Clear();
            _currentPages = SelectedEntry?.Pages ?? Array.Empty<NpcInteractionPage>();
            _currentPage = 0;
        }

        private IReadOnlyList<NpcInteractionPage> GetCurrentPages()
        {
            return _currentPages;
        }

        private string GetCurrentPageText()
        {
            IReadOnlyList<NpcInteractionPage> pages = GetCurrentPages();
            if (_currentPage < 0 || _currentPage >= pages.Count)
            {
                return string.Empty;
            }

            return pages[_currentPage].Text;
        }

        private IReadOnlyList<NpcInteractionChoice> GetCurrentChoices()
        {
            IReadOnlyList<NpcInteractionPage> pages = GetCurrentPages();
            if (_currentPage < 0 || _currentPage >= pages.Count)
            {
                return Array.Empty<NpcInteractionChoice>();
            }

            return pages[_currentPage].Choices ?? Array.Empty<NpcInteractionChoice>();
        }

        private string GetPrimaryButtonText()
        {
            return SelectedEntry?.PrimaryActionLabel ?? string.Empty;
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

        private void DrawEntryList(SpriteBatch spriteBatch, Rectangle entryListRect)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Rectangle itemRect = GetEntryRectangle(entryListRect, i);
                bool isSelected = i == _selectedEntryIndex;

                Color fill = isSelected ? new Color(71, 104, 149, 255) : new Color(37, 49, 69, 210);
                Color border = isSelected ? new Color(235, 218, 170) : new Color(85, 95, 112);
                DrawPanel(spriteBatch, itemRect, fill, border);

                DrawText(spriteBatch, _entries[i].Title, new Vector2(itemRect.X + 10, itemRect.Y + 7), Color.White);
                if (!string.IsNullOrWhiteSpace(_entries[i].Subtitle))
                {
                    DrawText(spriteBatch, _entries[i].Subtitle, new Vector2(itemRect.X + 10, itemRect.Y + 23), new Color(219, 214, 193));
                }
            }
        }

        private void DrawEntryHeader(SpriteBatch spriteBatch, Rectangle textRect)
        {
            NpcInteractionEntry entry = SelectedEntry;
            if (entry == null)
            {
                return;
            }

            DrawText(spriteBatch, entry.Title, new Vector2(textRect.X, textRect.Y), Color.White);
            if (!string.IsNullOrWhiteSpace(entry.Subtitle))
            {
                DrawText(spriteBatch, entry.Subtitle, new Vector2(textRect.X, textRect.Y + 18), new Color(224, 202, 145));
            }
        }

        private void DrawPageIndicator(SpriteBatch spriteBatch, Rectangle windowRect)
        {
            string pageText = $"{_currentPage + 1}/{Math.Max(1, GetCurrentPages().Count)}";
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

        private static Rectangle GetEntryListRectangle(Rectangle windowRect)
        {
            return new Rectangle(
                windowRect.X + Padding,
                windowRect.Y + 54,
                EntryListWidth,
                windowRect.Height - 116);
        }

        private static Rectangle GetEntryRectangle(Rectangle listRect, int index)
        {
            int itemHeight = 46;
            int itemGap = 6;
            int y = listRect.Y + 8 + index * (itemHeight + itemGap);
            return new Rectangle(listRect.X + 8, y, listRect.Width - 16, itemHeight);
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

        private static Rectangle GetPrimaryButtonRectangle(Rectangle windowRect)
        {
            Rectangle prevRect = GetPrevButtonRectangle(windowRect);
            return new Rectangle(windowRect.X + Padding, prevRect.Y, ButtonWidth + 12, ButtonHeight);
        }

        private static Rectangle[] GetChoiceButtonRectangles(Rectangle windowRect, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<Rectangle>();
            }

            const int columns = 3;
            int rows = (int)Math.Ceiling(count / (double)columns);
            int totalHeight = (rows * ButtonHeight) + ((rows - 1) * ButtonGap);
            int y = windowRect.Bottom - ButtonHeight - 30 - totalHeight;

            var rects = new Rectangle[count];
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                int itemsInRow = Math.Min(columns, count - (row * columns));
                int totalWidth = (ChoiceButtonWidth * itemsInRow) + (ButtonGap * (itemsInRow - 1));
                int startX = windowRect.X + ((windowRect.Width - totalWidth) / 2);

                rects[i] = new Rectangle(
                    startX + column * (ChoiceButtonWidth + ButtonGap),
                    y + row * (ButtonHeight + ButtonGap),
                    ChoiceButtonWidth,
                    ButtonHeight);
            }

            return rects;
        }
    }
}
